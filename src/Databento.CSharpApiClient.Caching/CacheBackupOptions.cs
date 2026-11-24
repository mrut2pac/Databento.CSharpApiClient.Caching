using System;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Settings of a <see cref="DatabentoCacheBackup"/>
    /// </summary>
    public sealed class CacheBackupOptions
    {
        /// <summary>
        /// Gets or sets the cache root to back up, which holds <c>metadata.json</c> and one folder per Databento dataset.
        /// </summary>
        public string CacheRoot { get; set; }

        /// <summary>
        /// Gets or sets the store the backups go into.
        /// </summary>
        public IBackupStore Store { get; set; }

        /// <summary>
        /// Gets or sets the folder a backup's tar is written and checked in before it goes into the store, with room for the whole cache;
        /// it neither holds nor is inside the cache root.
        /// </summary>
        public string StagingFolder { get; set; }

        /// <summary>
        /// Gets or sets which backups are kept once a new one is in the store; the newest 5 by default.
        /// </summary>
        public BackupRetention Retention { get; set; } = BackupRetention.KeepLast(5);

        /// <summary>
        /// Gets or sets the most day files a cache may hold and still be backed up unforced; 0 or more. A cache past it is compacted
        /// first, since each day file is a tar entry of its own.
        /// </summary>
        public int MaxBackupDayFiles { get; set; } = 10_000;

        /// <summary>
        /// Gets or sets the clock that names each backup by its start.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    }
}
