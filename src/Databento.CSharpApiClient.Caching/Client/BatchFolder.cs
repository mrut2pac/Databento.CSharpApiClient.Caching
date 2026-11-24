using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// A Databento batch download folder: the query its <c>metadata.json</c> records, the dataset conditions of its <c>condition.json</c>,
    /// and its data files, DBN or JSON, zstd-compressed or not, split by day, week, month or none, by symbol, or by size
    /// </summary>
    internal sealed class BatchFolder
    {
        private const string MetadataFileName = "metadata.json";

        private const string ConditionFileName = "condition.json";

        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// The folder's own files, which hold no records
        /// </summary>
        private static readonly HashSet<string> FolderFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MetadataFileName, ConditionFileName, "manifest.json", "symbology.json" };

        private BatchFolder()
        {
        }

        /// <summary>
        /// Gets the folder's path.
        /// </summary>
        public string Path { get; private init; }

        /// <summary>
        /// Gets the Databento dataset of the job.
        /// </summary>
        public string Dataset { get; private init; }

        /// <summary>
        /// Gets the Databento schema of the job, e.g. <c>cbbo-1m</c>.
        /// </summary>
        public string Schema { get; private init; }

        /// <summary>
        /// Gets the raw symbols the job asked for.
        /// </summary>
        public IReadOnlyList<string> Symbols { get; private init; }

        /// <summary>
        /// Gets the start of the job's range, UTC.
        /// </summary>
        public DateTime StartUtc { get; private init; }

        /// <summary>
        /// Gets the end of the job's range, UTC, exclusive.
        /// </summary>
        public DateTime EndUtc { get; private init; }

        /// <summary>
        /// Gets whether the data files are split by size, so a day's records may span several of them.
        /// </summary>
        public bool SplitBySize { get; private init; }

        /// <summary>
        /// Gets the dataset condition of each date <c>condition.json</c> lists.
        /// </summary>
        public IReadOnlyDictionary<DateTime, string> Conditions { get; private init; }

        /// <summary>
        /// Gets the data files' full paths, in name order.
        /// </summary>
        public IReadOnlyList<string> DataFiles { get; private init; }

        /// <summary>
        /// Reads a folder's metadata and lists its data files
        /// </summary>
        /// <param name="path">The folder</param>
        /// <returns>The folder</returns>
        /// <exception cref="DirectoryNotFoundException">There is no such folder</exception>
        /// <exception cref="InvalidDataException">The folder is no batch download this cache takes: no <c>metadata.json</c> or <c>condition.json</c>,
        /// a query of other than raw symbols, CSV files, or data files that are not the query's encoding</exception>
        public static BatchFolder Read(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            if(!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(Invariant($"There is no batch folder {path}."));
            }

            string metadataPath = System.IO.Path.Combine(path, MetadataFileName);
            string conditionPath = System.IO.Path.Combine(path, ConditionFileName);
            if(!File.Exists(metadataPath) || !File.Exists(conditionPath))
            {
                throw new InvalidDataException(Invariant($"{path} holds no {MetadataFileName} and {ConditionFileName}: it is no Databento batch download."));
            }

            try
            {
                using(JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataPath)))
                {
                    JsonElement query = metadata.RootElement.GetProperty("query");
                    JsonElement customizations = metadata.RootElement.GetProperty("customizations");
                    string stypeIn = query.GetProperty("stype_in").GetString();
                    if(!string.Equals(stypeIn, "raw_symbol", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(Invariant($"The job asked for {stypeIn} symbols; the cache is keyed by raw symbols only."));
                    }

                    string encoding = query.GetProperty("encoding").GetString();
                    if(!string.Equals(encoding, "dbn", StringComparison.Ordinal) && !string.Equals(encoding, "json", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(Invariant($"The job's files are {encoding}; only DBN and JSON files are taken."));
                    }

                    List<string> dataFiles = ListDataFiles(path, encoding);
                    ThrowIfFilesMissing(path);
                    return new BatchFolder
                    {
                        Path = path,
                        Dataset = query.GetProperty("dataset").GetString(),
                        Schema = query.GetProperty("schema").GetString(),
                        Symbols = query.GetProperty("symbols").EnumerateArray().Select(symbol => symbol.GetString()).Distinct(StringComparer.Ordinal).ToList(),
                        StartUtc = FromNanoseconds(query.GetProperty("start").GetInt64()),
                        EndUtc = FromNanoseconds(query.GetProperty("end").GetInt64()),
                        SplitBySize = customizations.TryGetProperty("split_size", out JsonElement splitSize) && splitSize.ValueKind == JsonValueKind.Number,
                        Conditions = ReadConditions(conditionPath),
                        DataFiles = dataFiles,
                    };
                }
            }
            catch(Exception ex) when(ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException)
            {
                throw new InvalidDataException(Invariant($"{path} has a {MetadataFileName} or {ConditionFileName} that cannot be read: {ex.Message}"), ex);
            }
        }

        /// <summary>
        /// Tells whether a data file is DBN, rather than JSON
        /// </summary>
        public static bool IsDbn(string path)
        {
            return path.EndsWith(".dbn", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dbn.zst", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tells whether a data file is zstd-compressed
        /// </summary>
        public static bool IsZstd(string path)
        {
            return path.EndsWith(".zst", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the UTC days the job's range holds whole, and of those the ones whose condition makes them final: <c>available</c>, or no
        /// condition at all (a day the dataset has no data for)
        /// </summary>
        /// <param name="leftOut">The days the range touches that are not final, or not whole</param>
        /// <returns>The final whole days</returns>
        public List<DateTime> GetFinalDays(out List<DateTime> leftOut)
        {
            List<DateTime> final = new List<DateTime>();
            leftOut = new List<DateTime>();
            DateTime last = this.EndUtc.AddTicks(-1).Date;
            for(DateTime day = this.StartUtc.Date; day <= last; day = day.AddDays(1))
            {
                bool whole = day >= this.StartUtc && day.AddDays(1) <= this.EndUtc;
                bool available = !this.Conditions.TryGetValue(day, out string condition) || string.Equals(condition, "available", StringComparison.Ordinal);
                (whole && available ? final : leftOut).Add(DateTime.SpecifyKind(day, DateTimeKind.Utc));
            }

            return final;
        }

        /// <summary>
        /// Checks every data file <c>manifest.json</c> lists is in the folder at its listed size, so a folder copied in part is refused rather
        /// than its missing days saved as empty
        /// </summary>
        /// <exception cref="InvalidDataException">A listed data file is missing or of another size</exception>
        private static void ThrowIfFilesMissing(string path)
        {
            string manifestPath = System.IO.Path.Combine(path, "manifest.json");
            if(!File.Exists(manifestPath))
            {
                return;
            }

            using(JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath)))
            {
                foreach(JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray())
                {
                    string name = file.GetProperty("filename").GetString();
                    if(FolderFiles.Contains(name))
                    {
                        continue;
                    }

                    FileInfo info = new FileInfo(System.IO.Path.Combine(path, System.IO.Path.GetFileName(name)));
                    long size = file.GetProperty("size").GetInt64();
                    if(!info.Exists || info.Length != size)
                    {
                        throw new InvalidDataException(Invariant($"{name}, which manifest.json lists at {size} bytes, is {(info.Exists ? Invariant($"{info.Length} bytes") : "missing")}: the folder is not whole."));
                    }
                }
            }
        }

        private static DateTime FromNanoseconds(long nanoseconds)
        {
            return DateTime.SpecifyKind(DateTime.UnixEpoch.AddTicks(nanoseconds / 100), DateTimeKind.Utc);
        }

        private static Dictionary<DateTime, string> ReadConditions(string path)
        {
            Dictionary<DateTime, string> conditions = new Dictionary<DateTime, string>();
            using(JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach(JsonElement day in document.RootElement.EnumerateArray())
                {
                    DateTime date = DateTime.ParseExact(day.GetProperty("date").GetString(), DateFormat, CultureInfo.InvariantCulture);
                    conditions[date] = day.GetProperty("condition").GetString();
                }
            }

            return conditions;
        }

        /// <summary>
        /// Lists the folder's data files, refusing any that is not the query's encoding
        /// </summary>
        private static List<string> ListDataFiles(string path, string encoding)
        {
            List<string> dataFiles = new List<string>();
            foreach(string file in Directory.GetFiles(path).OrderBy(file => file, StringComparer.Ordinal))
            {
                string name = System.IO.Path.GetFileName(file);
                if(FolderFiles.Contains(name))
                {
                    continue;
                }

                string extension = name.EndsWith(".zst", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.GetExtension(name.Substring(0, name.Length - 4)) : System.IO.Path.GetExtension(name);
                if(!string.Equals(extension, "." + encoding, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(Invariant($"{name} is not a {encoding} file, as the job's other files are."));
                }

                dataFiles.Add(file);
            }

            return dataFiles;
        }
    }
}
