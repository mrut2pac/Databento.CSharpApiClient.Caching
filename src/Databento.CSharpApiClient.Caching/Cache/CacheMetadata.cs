using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// The cache root's <c>metadata.json</c>: the format version of its files and the day cutoff they are cut by. It is written when
    /// the first file is saved into an empty root, and a root whose metadata this build does not accept is refused by everything that
    /// opens it. Version 2 adds DBN day files in day folders to the legacy JSON day files of version 1, so a version 1 cache is marked
    /// version 2 when it is opened, with no file rewritten, and a build that reads version 1 only refuses it from then on rather than
    /// taking its DBN day files for missing days.
    /// </summary>
    internal sealed class CacheMetadata
    {
        /// <summary>
        /// Name of the file in the cache root
        /// </summary>
        public const string FileName = "metadata.json";

        /// <summary>
        /// The format version this build writes and reads
        /// </summary>
        public const int CurrentFormatVersion = 2;

        /// <summary>
        /// The format version of caches of legacy JSON day files only, which this build reads and marks <see cref="CurrentFormatVersion"/>
        /// </summary>
        public const int LegacyFormatVersion = 1;

        /// <summary>
        /// The day cutoff of a cache whose days are UTC calendar days, as Databento cuts its data; the only one for now
        /// </summary>
        public const string UtcCutoff = "UTC";

        private CacheMetadata(int formatVersion, string dayCutoff)
        {
            this.FormatVersion = formatVersion;
            this.DayCutoff = dayCutoff;
        }

        /// <summary>
        /// Gets the format version of the cache's files.
        /// </summary>
        public int FormatVersion { get; }

        /// <summary>
        /// Gets the tag of the day cutoff the cache's days are cut by, which every file name carries, e.g. <c>UTC</c>.
        /// </summary>
        public string DayCutoff { get; }

        /// <summary>
        /// Reads a cache root's metadata, a version 1 cache as version 2. Unless told not to, it marks a version 1 cache version 2 on
        /// disk; when the mark cannot be written, as on a read-only copy, the cache is read all the same, and <see cref="EnsureCreated"/>
        /// refuses to save into it.
        /// </summary>
        /// <param name="cacheRoot">Cache root</param>
        /// <param name="mark">Whether a version 1 cache is marked version 2 on disk; not for a run that must change nothing, such as a dry run</param>
        /// <returns>The metadata, or <c>null</c> when the root has none</returns>
        /// <exception cref="InvalidDataException">The file is not metadata this build accepts: malformed, or of another format version or cutoff</exception>
        public static CacheMetadata Read(string cacheRoot, bool mark = true)
        {
            CacheMetadata onDisk = ReadOnDisk(cacheRoot, out string path);
            if(onDisk?.FormatVersion != LegacyFormatVersion)
            {
                return onDisk;
            }

            if(mark)
            {
                try
                {
                    Write(path, CurrentFormatVersion, onDisk.DayCutoff, overwrite: true);
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                {
                    // read as it is; saving into it waits until it can be marked
                }
            }

            return new CacheMetadata(CurrentFormatVersion, onDisk.DayCutoff);
        }

        /// <summary>
        /// Reads a cache root's metadata, writing it first when the root has none, so the first file saved into an empty root finds it
        /// there, and marking a version 1 cache version 2 before anything is saved into it. Of two writers at once, the first rename wins
        /// and the other reads its file.
        /// </summary>
        /// <param name="cacheRoot">Cache root, created when missing</param>
        /// <returns>The metadata</returns>
        /// <exception cref="InvalidDataException">The root's file is not metadata this build accepts</exception>
        /// <exception cref="IOException">A version 1 cache cannot be marked version 2, so a DBN day file saved into it would be taken for a
        /// missing day by an older build</exception>
        public static CacheMetadata EnsureCreated(string cacheRoot)
        {
            CacheMetadata existing = ReadOnDisk(cacheRoot, out string existingPath);
            if(existing?.FormatVersion == LegacyFormatVersion)
            {
                try
                {
                    Write(existingPath, CurrentFormatVersion, existing.DayCutoff, overwrite: true);
                }
                catch(UnauthorizedAccessException ex)
                {
                    throw new IOException(Invariant($"{existingPath} says format version {LegacyFormatVersion} and cannot be marked version {CurrentFormatVersion}, so nothing is saved into the cache: {ex.Message}"), ex);
                }

                return new CacheMetadata(CurrentFormatVersion, existing.DayCutoff);
            }

            if(existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(cacheRoot);
            string path = Path.Combine(cacheRoot, FileName);
            try
            {
                Write(path, CurrentFormatVersion, UtcCutoff, overwrite: false);
            }
            catch(IOException) when(File.Exists(path))
            {
                // another writer's file got there first
            }

            return Read(cacheRoot) ?? throw new InvalidDataException(Invariant($"{path} was written but cannot be found."));
        }

        /// <summary>
        /// Writes the metadata under a temporary name, then renames it into place
        /// </summary>
        private static void Write(string path, int formatVersion, string dayCutoff, bool overwrite)
        {
            string temporaryPath = Invariant($"{path}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, ToJson(formatVersion, dayCutoff), new UTF8Encoding(false));
                File.Move(temporaryPath, path, overwrite);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Reads the metadata as the file says it
        /// </summary>
        /// <returns>The metadata, or <c>null</c> when the root has none</returns>
        private static CacheMetadata ReadOnDisk(string cacheRoot, out string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(cacheRoot);

            path = Path.Combine(cacheRoot, FileName);
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return null;
            }

            return Parse(json, path);
        }

        private static CacheMetadata Parse(string json, string path)
        {
            int formatVersion;
            string dayCutoff;
            try
            {
                using(JsonDocument document = JsonDocument.Parse(json))
                {
                    formatVersion = document.RootElement.GetProperty("format_version").GetInt32();
                    dayCutoff = document.RootElement.GetProperty("day_cutoff").GetString();
                }
            }
            catch(Exception ex) when(ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException)
            {
                throw new InvalidDataException(Invariant($"{path} is not the metadata of a Databento cache: {ex.Message}"), ex);
            }

            if(formatVersion != CurrentFormatVersion && formatVersion != LegacyFormatVersion)
            {
                throw new InvalidDataException(Invariant($"{path} says format version {formatVersion}; this build reads versions {LegacyFormatVersion} and {CurrentFormatVersion} only."));
            }

            if(!string.Equals(dayCutoff, UtcCutoff, StringComparison.Ordinal))
            {
                throw new InvalidDataException(Invariant($"{path} says day cutoff '{dayCutoff}'; this build reads '{UtcCutoff}' only."));
            }

            return new CacheMetadata(formatVersion, dayCutoff);
        }

        private static string ToJson(int formatVersion, string dayCutoff)
        {
            using(MemoryStream stream = new MemoryStream())
            {
                using(Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("format_version", formatVersion);
                    writer.WriteString("day_cutoff", dayCutoff);
                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
