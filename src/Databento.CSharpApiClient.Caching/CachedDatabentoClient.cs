using System;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.Transport;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// The Databento client's time-series calls, served from a disk cache: each UTC day a range touches, per symbol, from its day file, else
    /// from the archive the compactor builds, else downloaded whole and saved as a day file before the call returns, once Databento says the
    /// day is final. So each symbol-day is paid for once. The calls take the client's arguments plus a <see cref="CacheMode"/>, and return
    /// the record interfaces, in time order, trimmed to the range.
    /// </summary>
    /// <remarks>
    /// Thread-safe: concurrent calls share the cache, and one download per symbol-day runs at a time within the process, across clients on
    /// the same root. A record's day is the UTC date of the timestamp Databento filters a range on, <c>ts_recv</c> where the schema has one,
    /// else <c>ts_event</c>. A symbol's download covers its first to its last uncached day, so cached days between them are bought again,
    /// and not saved over: ask for runs of days to avoid it.
    /// </remarks>
    public sealed partial class CachedDatabentoClient : IDisposable
    {
        private readonly DatabentoJsonClient jsonClient;

        private readonly DatabentoClient dbnClient;

        private readonly CachedTimeseries timeseries;

        private readonly BatchJobs batchJobs;

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

            this.jsonClient = new DatabentoJsonClient(settings.Databento, transport);
            this.dbnClient = new DatabentoClient(settings.Databento, transport);
            ArchiveCatalog catalog = new ArchiveCatalog(settings.TimeProvider, this.OnArchiveFileUnreadable);
            SymbolDayStore store = new SymbolDayStore(settings.CacheRoot, CacheMetadata.UtcCutoff, catalog);
            DatasetAvailability availability = new DatasetAvailability();
            this.timeseries = new CachedTimeseries(settings, this.jsonClient, this.dbnClient, store, availability);
            this.batchJobs = new BatchJobs(settings, this.jsonClient, store, availability, new BatchIngestion(settings, this.jsonClient, store));
        }

        /// <summary>
        /// Raised once per archive file per UTC day when an archive file cannot be read, for the host to alarm on. Meanwhile its days are
        /// served as <see cref="ArchiveFileUnreadableEventArgs"/> says, and the file is tried again after 10 minutes. A handler that throws is
        /// ignored.
        /// </summary>
        public event EventHandler<ArchiveFileUnreadableEventArgs> ArchiveFileUnreadable;

        /// <summary>
        /// Releases the Databento clients and their HTTP transport
        /// </summary>
        public void Dispose()
        {
            this.jsonClient.Dispose();
            this.dbnClient.Dispose();
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

            if(!Enum.IsDefined(options.Transport) || !Enum.IsDefined(options.EmptyDays) || !Enum.IsDefined(options.BypassMissAction))
            {
                throw new ArgumentException(Invariant($"Transport {options.Transport}, EmptyDays {options.EmptyDays} and BypassMissAction {options.BypassMissAction} must each be one of their values."), nameof(options));
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

            return new CachedDatabentoClientOptions
            {
                Databento = options.Databento,
                CacheRoot = options.CacheRoot,
                Transport = options.Transport,
                EmptyDays = options.EmptyDays,
                BypassMissAction = options.BypassMissAction,
                MaxSymbolsPerRequest = options.MaxSymbolsPerRequest,
                BatchPollInterval = options.BatchPollInterval,
                BatchDownloadFolder = options.BatchDownloadFolder,
                TimeProvider = options.TimeProvider,
            };
        }

        private void OnArchiveFileUnreadable(string path, Exception exception)
        {
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
