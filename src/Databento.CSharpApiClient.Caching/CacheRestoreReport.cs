namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a <see cref="DatabentoCacheRestore"/> did, or for a dry run what a real restore would do to the cache
    /// </summary>
    public sealed class CacheRestoreReport
    {
        internal CacheRestoreReport(string backupName, int fileCount, long totalBytes, bool dryRun, string restoredFolder, int filesDeleted, int dayFilesKept, int filesMovedAside, string movedAsideFolder)
        {
            this.BackupName = backupName;
            this.FileCount = fileCount;
            this.TotalBytes = totalBytes;
            this.DryRun = dryRun;
            this.RestoredFolder = restoredFolder;
            this.FilesDeleted = filesDeleted;
            this.DayFilesKept = dayFilesKept;
            this.FilesMovedAside = filesMovedAside;
            this.MovedAsideFolder = movedAsideFolder;
        }

        /// <summary>
        /// Gets the name of the backup restored.
        /// </summary>
        public string BackupName { get; }

        /// <summary>
        /// Gets the number of files the backup holds.
        /// </summary>
        public int FileCount { get; }

        /// <summary>
        /// Gets the bytes of the backup's files together.
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// Gets whether this was a dry run, which changed nothing in the cache.
        /// </summary>
        public bool DryRun { get; }

        /// <summary>
        /// Gets the folder the backup's files are in now: the cache root, or for a dry run its folder under <see cref="CacheRestoreOptions.DryRunFolder"/>.
        /// </summary>
        public string RestoredFolder { get; }

        /// <summary>
        /// Gets the number of the cache's files deleted, or that a real restore would delete: its archive and temporary files with
        /// <see cref="ExistingCacheFiles.Keep"/>, every file with <see cref="ExistingCacheFiles.Delete"/>.
        /// </summary>
        public int FilesDeleted { get; }

        /// <summary>
        /// Gets the number of day files the cache already held and kept in place of the backup's copies, which only
        /// <see cref="ExistingCacheFiles.Keep"/> does; they are served ahead of the archive.
        /// </summary>
        public int DayFilesKept { get; }

        /// <summary>
        /// Gets the number of the cache's files moved into <see cref="MovedAsideFolder"/>, or that a real restore would move.
        /// </summary>
        public int FilesMovedAside { get; }

        /// <summary>
        /// Gets the folder the cache's files were moved into with <see cref="ExistingCacheFiles.MoveAside"/>, or would be for a dry run;
        /// <c>null</c> otherwise.
        /// </summary>
        public string MovedAsideFolder { get; }
    }
}
