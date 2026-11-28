using System;

namespace Databento.CSharpApiClient.Caching.Compaction
{
    /// <summary>
    /// What the group-months of one unpack run share: its settings, the cache's day cutoff, its report and its file operations
    /// </summary>
    internal sealed class UnpackRun
    {
        public UnpackRun(ArchiveUnpackerOptions options, string cutoff, ArchiveUnpackReport report, Action<string> dayFileWritten, Action<string> deleteFile, TimeProvider timeProvider)
        {
            this.Options = options;
            this.Cutoff = cutoff;
            this.Report = report;
            this.DayFileWritten = dayFileWritten;

            // the compactor's deletes, retried while a file is in use, with its default attempts and waits
            this.FileOperations = new ArchiveFileOperations(
                new ArchiveCompactorOptions
                {
                    CacheRoot = options.CacheRoot,
                    Logger = options.Logger,
                    TimeProvider = timeProvider,
                },
                deleteFile);
        }

        /// <summary>
        /// Gets the run's settings.
        /// </summary>
        public ArchiveUnpackerOptions Options { get; }

        /// <summary>
        /// Gets the cache's day cutoff tag.
        /// </summary>
        public string Cutoff { get; }

        /// <summary>
        /// Gets the run's report.
        /// </summary>
        public ArchiveUnpackReport Report { get; }

        /// <summary>
        /// Gets the deletes of the archive files retired.
        /// </summary>
        public ArchiveFileOperations FileOperations { get; }

        /// <summary>
        /// Gets what is told each day file's path once it is written; for tests, <c>null</c> otherwise.
        /// </summary>
        public Action<string> DayFileWritten { get; }
    }
}
