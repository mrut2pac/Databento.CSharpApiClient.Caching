namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a <see cref="DatabentoCacheBackup"/> put into the store
    /// </summary>
    public sealed class CacheBackupReport
    {
        internal CacheBackupReport(string name, int fileCount, long totalBytes, int dayFiles, int oldBackupsDeleted)
        {
            this.Name = name;
            this.FileCount = fileCount;
            this.TotalBytes = totalBytes;
            this.DayFiles = dayFiles;
            this.OldBackupsDeleted = oldBackupsDeleted;
        }

        /// <summary>
        /// Gets the backup's name in the store: its start, UTC, <c>yyyyMMddTHHmmss</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the number of files the backup holds.
        /// </summary>
        public int FileCount { get; }

        /// <summary>
        /// Gets the bytes of the backup's files together.
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// Gets the number of day files the cache held when the backup started; with the backup unforced, at most <see cref="CacheBackupOptions.MaxBackupDayFiles"/>.
        /// </summary>
        public int DayFiles { get; }

        /// <summary>
        /// Gets the number of older backups deleted by <see cref="CacheBackupOptions.Retention"/>.
        /// </summary>
        public int OldBackupsDeleted { get; }
    }
}
