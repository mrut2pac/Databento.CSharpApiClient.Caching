using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// What a benchmark run generates and measures, read from <c>benchmark.json</c>. Every step reads the same file, so a later step finds
    /// the data an earlier one made.
    /// </summary>
    internal sealed class BenchmarkSettings
    {
        /// <summary>
        /// Gets or sets the folder everything is written under: the cache, the backups, the restored copy and the results.
        /// </summary>
        public string WorkFolder { get; set; }

        /// <summary>
        /// Gets or sets the first UTC day generated.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the last UTC day generated, inclusive.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Gets or sets how many day files are written at once while seeding.
        /// </summary>
        public int Parallelism { get; set; } = 8;

        /// <summary>
        /// Gets or sets the compactor's <see cref="ArchiveCompactorOptions.MaxParallelGroups"/>.
        /// </summary>
        public int MaxParallelGroups { get; set; } = 8;

        /// <summary>
        /// Gets or sets how many symbol-days the random query pass reads.
        /// </summary>
        public int RandomQueries { get; set; } = 2000;

        /// <summary>
        /// Gets or sets how many symbol-days the sequential query pass reads.
        /// </summary>
        public int SequentialQueries { get; set; } = 2000;

        /// <summary>
        /// Gets or sets the numbers of concurrent readers each query pass is run with.
        /// </summary>
        public int[] QueryParallelism { get; set; } = { 1, 8 };

        /// <summary>
        /// Gets or sets the seed every random choice derives from, so two runs with the same settings generate and query the same data.
        /// </summary>
        public int Seed { get; set; } = 1;

        /// <summary>
        /// Gets or sets a description of the disk <see cref="WorkFolder"/> is on, such as its make and kind, for the report; optional.
        /// </summary>
        public string Disk { get; set; }

        /// <summary>
        /// Gets or sets the instruments generated.
        /// </summary>
        public List<InstrumentSettings> Instruments { get; set; } = new List<InstrumentSettings>();

        /// <summary>
        /// Gets the cache the seed writes and the queries read.
        /// </summary>
        public string CacheRoot => Path.Combine(this.WorkFolder, "cache");

        /// <summary>
        /// Gets the folder backups are written to.
        /// </summary>
        public string BackupFolder => Path.Combine(this.WorkFolder, "backups");

        /// <summary>
        /// Gets the staging folder backup and restore use.
        /// </summary>
        public string StagingFolder => Path.Combine(this.WorkFolder, "staging");

        /// <summary>
        /// Gets the cache a restore writes.
        /// </summary>
        public string RestoredRoot => Path.Combine(this.WorkFolder, "restored");

        /// <summary>
        /// Gets or sets the run the steps' results belong to, which names its folder under <see cref="ResultsRoot"/>; set from the command
        /// line, not the settings file.
        /// </summary>
        [JsonIgnore]
        public string RunName { get; set; }

        /// <summary>
        /// Gets the folder that holds one folder of results per run, so earlier runs' results and reports are kept.
        /// </summary>
        public string ResultsRoot => Path.Combine(this.WorkFolder, "results");

        /// <summary>
        /// Gets the folder this run's step results and report are written to.
        /// </summary>
        public string ResultsFolder => Path.Combine(this.ResultsRoot, this.RunName ?? throw new InvalidOperationException("No run is chosen."));

        /// <summary>
        /// Reads and checks the settings
        /// </summary>
        /// <exception cref="InvalidDataException">A setting is missing or out of range</exception>
        public static BenchmarkSettings Read(string path)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                Converters = { new JsonStringEnumConverter() },
            };
            BenchmarkSettings settings;
            try
            {
                settings = JsonSerializer.Deserialize<BenchmarkSettings>(File.ReadAllText(path), options);
            }
            catch(JsonException ex)
            {
                throw new InvalidDataException(Invariant($"{path} cannot be read: {ex.Message}"), ex);
            }

            if(settings == null || string.IsNullOrWhiteSpace(settings.WorkFolder))
            {
                throw new InvalidDataException(Invariant($"Set WorkFolder in {path}: the folder everything is written under."));
            }

            if(settings.StartDate == default || settings.EndDate == default)
            {
                throw new InvalidDataException("StartDate and EndDate must both be set.");
            }

            if(settings.EndDate < settings.StartDate)
            {
                throw new InvalidDataException(Invariant($"EndDate {settings.EndDate:yyyy-MM-dd} is before StartDate {settings.StartDate:yyyy-MM-dd}."));
            }

            if(settings.Parallelism < 1 || settings.MaxParallelGroups < 1 || settings.QueryParallelism == null || settings.QueryParallelism.Length == 0 || settings.QueryParallelism.Any(count => count < 1))
            {
                throw new InvalidDataException("Parallelism, MaxParallelGroups and every QueryParallelism must be at least 1.");
            }

            if(settings.RandomQueries < 1 || settings.SequentialQueries < 1)
            {
                throw new InvalidDataException("RandomQueries and SequentialQueries must be at least 1.");
            }

            if(settings.Instruments == null || settings.Instruments.Count == 0 || settings.Instruments.Any(instrument => instrument == null))
            {
                throw new InvalidDataException("At least one instrument is needed.");
            }

            string repeated = settings.Instruments.GroupBy(instrument => instrument.Root, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).FirstOrDefault();
            if(repeated != null)
            {
                throw new InvalidDataException(Invariant($"Root {repeated} is listed twice: each instrument needs its own root."));
            }

            settings.StartDate = DateTime.SpecifyKind(settings.StartDate.Date, DateTimeKind.Utc);
            settings.EndDate = DateTime.SpecifyKind(settings.EndDate.Date, DateTimeKind.Utc);
            foreach(InstrumentSettings instrument in settings.Instruments)
            {
                instrument.Check();
            }

            return settings;
        }
    }
}
