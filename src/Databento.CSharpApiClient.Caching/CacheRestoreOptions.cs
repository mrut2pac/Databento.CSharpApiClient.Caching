using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Settings of a <see cref="DatabentoCacheRestore"/>
    /// </summary>
    public sealed class CacheRestoreOptions
    {
        private ILogger logger = NullLogger.Instance;

        /// <summary>
        /// Gets or sets the cache root to restore; it need not exist.
        /// </summary>
        public string CacheRoot { get; set; }

        /// <summary>
        /// Gets or sets the store the backups are in.
        /// </summary>
        public IBackupStore Store { get; set; }

        /// <summary>
        /// Gets or sets the folder a backup is unpacked and checked in before it is moved into the cache, on the cache root's volume so
        /// each file moves by a rename; it neither holds nor is inside the cache root.
        /// </summary>
        public string StagingFolder { get; set; }

        /// <summary>
        /// Gets or sets what tells whether something is using the cache, such as a service holding a <see cref="CachedDatabentoClient"/> on
        /// it; <c>null</c> for no check, in which case whatever uses the cache must be stopped before a restore.
        /// </summary>
        public ICacheInUseChecker InUseChecker { get; set; }

        /// <summary>
        /// Gets or sets what happens to the files the cache root holds: day files kept by default, or everything moved aside or deleted
        /// for a restore into a clean root.
        /// </summary>
        public ExistingCacheFiles ExistingFiles { get; set; } = ExistingCacheFiles.Keep;

        /// <summary>
        /// Gets or sets whether the restore only unpacks and checks the backup into <see cref="DryRunFolder"/> and changes nothing in the cache.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Gets or sets the folder a dry run unpacks into, under the backup's name; it neither holds nor is inside the cache root or the staging folder.
        /// </summary>
        public string DryRunFolder { get; set; }

        /// <summary>
        /// Gets or sets the clock of the time written into the restore's marker.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>
        /// Gets or sets the logger the restore's steps are written to, with its refusals and failures. Default, and when set to <c>null</c>,
        /// <see cref="NullLogger.Instance"/>: nothing is logged.
        /// Its methods shall not throw; anything they throw is dropped, losing the line but never changing what is served or written.
        /// It may be called from several threads at once, so it must be thread-safe.
        /// </summary>
        public ILogger Logger
        {
            get => this.logger;
            set => this.logger = value ?? NullLogger.Instance;
        }
    }
}
