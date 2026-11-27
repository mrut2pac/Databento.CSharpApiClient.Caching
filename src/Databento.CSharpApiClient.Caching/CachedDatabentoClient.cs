using System;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.Transport;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// The Databento client's time-series calls, served from a disk cache: each UTC day a range touches, per symbol, from its day file, else
    /// from the archive the compactor builds, else downloaded whole and saved as a day file before the call returns, once its UTC day has
    /// ended plus <see cref="CachedDatabentoClientOptions.SettleDelay"/>, so each symbol-day is paid for once. A download goes straight to
    /// Databento with no metadata call before it, and one refused as past the available end is cut at that end and sent once more
    /// (<see cref="CachedDatabentoClientOptions.CheckAvailabilityFirst"/> reads the dataset's range and conditions first instead). The calls
    /// take the client's arguments plus a <see cref="CacheMode"/> and return the record interfaces in time order, trimmed to the range;
    /// Databento's metadata and symbology calls are passed through uncached, so one client serves all of them.
    /// </summary>
    /// <remarks>
    /// Thread-safe: concurrent calls share the cache, and one download per symbol-day runs at a time within the process, across clients on
    /// the same root. A record's day is the UTC date of the timestamp Databento filters a range on, <c>ts_recv</c> where the schema has one,
    /// else <c>ts_event</c>. A symbol's download covers its first to its last uncached day, so cached days between them are bought again,
    /// and not saved over: ask for runs of days to avoid it.
    /// </remarks>
    public sealed partial class CachedDatabentoClient : ICachedDatabentoClient
    {
        private readonly DatabentoJsonClient jsonClient;

        /// <summary>
        /// The DBN client the records are downloaded with, or null when they are downloaded as JSON
        /// </summary>
        private readonly DatabentoClient dbnClient;

        private readonly CachedTimeseries timeseries;

        private readonly BatchJobs batchJobs;

        /// <summary>
        /// The archive files read, kept open between calls
        /// </summary>
        private readonly ArchiveCatalog catalog;

        private readonly ILogger logger;

        private readonly SymbolDayStore store;

        /// <summary>
        /// Opens a cache
        /// </summary>
        /// <param name="options">Settings; read once, so later changes to them are not seen</param>
        /// <exception cref="ArgumentException">A setting is missing or out of range</exception>
        /// <exception cref="InvalidOperationException">A restore of the cache is in progress</exception>
        /// <exception cref="System.IO.InvalidDataException">The cache root's <c>metadata.json</c> is not one this build accepts</exception>
        public CachedDatabentoClient(CachedDatabentoClientOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Opens a cache whose downloads go through a given transport, for tests
        /// </summary>
        internal CachedDatabentoClient(CachedDatabentoClientOptions options, IHttpTransport transport)
        {
            CachedDatabentoClientOptions settings = CopyChecked(options);
            CacheRestoreMarker.ThrowIfPresent(settings.CacheRoot, "The cached client");
            CacheMetadata.Read(settings.CacheRoot);

            this.logger = settings.Logger;
            this.jsonClient = new DatabentoJsonClient(settings.Databento, transport);
            this.dbnClient = settings.Transport == DatabentoTransport.Dbn ? new DatabentoClient(settings.Databento, transport) : null;
            this.catalog = new ArchiveCatalog(settings.TimeProvider, this.OnArchiveFileUnreadable, settings.Logger);
            this.store = new SymbolDayStore(settings.CacheRoot, CacheMetadata.UtcCutoff, this.catalog, settings.Logger);
            DatasetAvailability availability = new DatasetAvailability();
            this.timeseries = new CachedTimeseries(settings, this.jsonClient, this.dbnClient, this.store, availability);
            this.batchJobs = new BatchJobs(settings, this.jsonClient, this.store, availability, new BatchIngestion(settings, this.jsonClient, this.store));
        }

        /// <summary>
        /// Raised once per archive file per UTC day when an archive file cannot be read, for the host to alarm on. Meanwhile its days are
        /// served as <see cref="ArchiveFileUnreadableEventArgs"/> says, and the file is tried again after 10 minutes. A handler that throws is
        /// ignored.
        /// </summary>
        public event EventHandler<ArchiveFileUnreadableEventArgs> ArchiveFileUnreadable;

        /// <summary>
        /// Gets the DBN client the records are downloaded with, or null when they are downloaded as JSON, for tests
        /// </summary>
        internal DatabentoClient DbnClient => this.dbnClient;

        /// <summary>
        /// Gets how many times a month folder was listed, for tests
        /// </summary>
        internal long MonthFolderListings => this.store.MonthFolderListings;

        /// <summary>
        /// Releases the Databento clients and their HTTP transport, and closes the archive files kept open
        /// </summary>
        public void Dispose()
        {
            this.jsonClient.Dispose();
            this.dbnClient?.Dispose();
            this.catalog.Dispose();
        }

        private static CachedDatabentoClientOptions CopyChecked(CachedDatabentoClientOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if(options.Databento == null)
            {
                throw new ArgumentException("Databento settings are needed.", nameof(options));
            }

            if(string.IsNullOrWhiteSpace(options.CacheRoot))
            {
                throw new ArgumentException("A cache root is needed.", nameof(options));
            }

            if(!Enum.IsDefined(options.Transport) || !Enum.IsDefined(options.EmptyDays) || !Enum.IsDefined(options.BypassMissAction) || !Enum.IsDefined(options.AvailableEndRefusal))
            {
                throw new ArgumentException(Invariant($"Transport {options.Transport}, EmptyDays {options.EmptyDays}, BypassMissAction {options.BypassMissAction} and AvailableEndRefusal {options.AvailableEndRefusal} must each be one of their values."), nameof(options));
            }

            if(options.MaxSymbolsPerRequest < 1 || options.MaxSymbolsPerRequest > CachedDatabentoClientOptions.MaxSymbolsDatabentoTakes)
            {
                throw new ArgumentException(Invariant($"MaxSymbolsPerRequest {options.MaxSymbolsPerRequest} must be 1 to {CachedDatabentoClientOptions.MaxSymbolsDatabentoTakes}."), nameof(options));
            }

            if(options.BatchPollInterval <= TimeSpan.Zero)
            {
                throw new ArgumentException(Invariant($"BatchPollInterval {options.BatchPollInterval} must be above zero."), nameof(options));
            }

            if(options.TimeProvider == null)
            {
                throw new ArgumentException("A TimeProvider is needed.", nameof(options));
            }

            if(options.SettleDelay < TimeSpan.Zero || options.SettleDelay > CachedDatabentoClientOptions.MaxSettleDelay)
            {
                throw new ArgumentException(Invariant($"SettleDelay {options.SettleDelay} must be {TimeSpan.Zero} to {CachedDatabentoClientOptions.MaxSettleDelay}, inclusive."), nameof(options));
            }

            if(options.AvailableEndRefreshInterval <= TimeSpan.Zero)
            {
                throw new ArgumentException(Invariant($"AvailableEndRefreshInterval {options.AvailableEndRefreshInterval} must be above zero."), nameof(options));
            }

            if(TimeSpan.TicksPerDay % options.AvailableEndRefreshInterval.Ticks != 0)
            {
                // the refresh grid starts at each UTC midnight, so an interval that does not divide a day would shift it from day to day
                throw new ArgumentException(Invariant($"AvailableEndRefreshInterval {options.AvailableEndRefreshInterval} must divide a day evenly."), nameof(options));
            }

            return new CachedDatabentoClientOptions
            {
                Databento = options.Databento,
                CacheRoot = options.CacheRoot,
                Transport = options.Transport,
                EmptyDays = options.EmptyDays,
                BypassMissAction = options.BypassMissAction,
                MaxSymbolsPerRequest = options.MaxSymbolsPerRequest,
                SettleDelay = options.SettleDelay,
                CheckAvailabilityFirst = options.CheckAvailabilityFirst,
                AvailableEndRefreshInterval = options.AvailableEndRefreshInterval,
                AvailableEndRefusal = options.AvailableEndRefusal,
                BatchPollInterval = options.BatchPollInterval,
                BatchDownloadFolder = options.BatchDownloadFolder,
                TimeProvider = options.TimeProvider,
                Logger = NonThrowingLogger.Wrap(options.Logger),
            };
        }

        private void OnArchiveFileUnreadable(string path, Exception exception)
        {
            this.logger.ArchiveFileUnreadable(path, ArchiveCatalog.UnreadableRecheckInterval, exception);
            try
            {
                this.ArchiveFileUnreadable?.Invoke(this, new ArchiveFileUnreadableEventArgs(path, exception));
            }
            catch(Exception)
            {
                // the host's handler failing must not fail the read that found the file
            }
        }
    }
}
