using System;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Settings of a <see cref="CachedDatabentoClient"/>
    /// </summary>
    public sealed class CachedDatabentoClientOptions
    {
        /// <summary>
        /// Databento's own limit on the symbols of one request
        /// </summary>
        public const int MaxSymbolsDatabentoTakes = 2000;

        /// <summary>
        /// Gets or sets the settings of the Databento clients the downloads go through: API key, base URI, timeout and retries. Required.
        /// </summary>
        public DatabentoOptions Databento { get; set; }

        /// <summary>
        /// Gets or sets the cache's root folder, created with the first file saved. Required.
        /// </summary>
        public string CacheRoot { get; set; }

        /// <summary>
        /// Gets or sets how records are downloaded. Default <see cref="DatabentoTransport.Json"/>.
        /// </summary>
        public DatabentoTransport Transport { get; set; } = DatabentoTransport.Json;

        /// <summary>
        /// Gets or sets when a final day answered with no records is saved as empty. Default <see cref="EmptyDayPolicy.CacheWhenNotListed"/>.
        /// </summary>
        public EmptyDayPolicy EmptyDays { get; set; } = EmptyDayPolicy.CacheWhenNotListed;

        /// <summary>
        /// Gets or sets what a <see cref="CacheMode.Bypass"/> request does with a cached day answered with no records. Default
        /// <see cref="Caching.BypassMissAction.KeepCached"/>.
        /// </summary>
        public BypassMissAction BypassMissAction { get; set; } = BypassMissAction.KeepCached;

        /// <summary>
        /// Gets or sets the most symbols one download request carries, 1 to 2,000. Default 2,000, Databento's limit.
        /// </summary>
        public int MaxSymbolsPerRequest { get; set; } = MaxSymbolsDatabentoTakes;

        /// <summary>
        /// Gets or sets how often a batch job's state is asked for while <see cref="CachedDatabentoClient.RunBatchJobAsync"/> waits for it.
        /// Default 30 seconds.
        /// </summary>
        public TimeSpan BatchPollInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the folder a batch job's files are downloaded into before they are saved as day files, each job in a folder of its
        /// own that is deleted after. Default <c>null</c>: the system's temporary folder. A job's files take as much room as it bought.
        /// </summary>
        public string BatchDownloadFolder { get; set; }

        /// <summary>
        /// Gets or sets the clock, which decides when an unreadable archive file is tried again and paces the batch job polls. Default <see cref="System.TimeProvider.System"/>.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    }
}
