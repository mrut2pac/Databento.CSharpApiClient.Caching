using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Serves a time-series request from the cache: each UTC day the range touches, per symbol, from its day file, else the archive, else
    /// downloaded whole, split by symbol and day, saved once settled (its UTC day ended plus
    /// <see cref="CachedDatabentoClientOptions.SettleDelay"/>), and trimmed to the range when served. An empty day, or a bypass replacing a
    /// cached day, is saved only when Databento's condition for it is <c>available</c>, and days past the available end are served empty
    /// by default (<see cref="CachedDatabentoClientOptions.AvailableEndRefusal"/>).
    /// </summary>
    internal sealed class CachedTimeseries
    {
        private const string AllSymbols = "ALL_SYMBOLS";

        private readonly CachedDatabentoClientOptions options;

        private readonly DatabentoJsonClient jsonClient;

        /// <summary>
        /// The DBN client, or null when <see cref="CachedDatabentoClientOptions.Transport"/> is JSON, which never reads it
        /// </summary>
        private readonly DatabentoClient dbnClient;

        private readonly SymbolDayStore store;

        private readonly DatasetAvailability availability;

        /// <summary>
        /// The available ends read after Databento refused a download as past them, and when each is to be read again: until then later
        /// requests are cut at it before they are sent. Used only without <see cref="CachedDatabentoClientOptions.CheckAvailabilityFirst"/>.
        /// </summary>
        private readonly ConcurrentDictionary<(string Dataset, string Schema), (DateTime End, DateTimeOffset RefreshUtc)> availableEnds = new ConcurrentDictionary<(string Dataset, string Schema), (DateTime End, DateTimeOffset RefreshUtc)>();

        private readonly ILogger logger;

        /// <summary>
        /// Shared by every client of the process, so two clients on one cache root download a symbol-day once between them
        /// </summary>
        private static readonly DownloadClaims Claims = new DownloadClaims();

        /// <summary>
        /// The cache root's full path, which every claim key starts with
        /// </summary>
        private readonly string rootKey;

        public CachedTimeseries(CachedDatabentoClientOptions options, DatabentoJsonClient jsonClient, DatabentoClient dbnClient, SymbolDayStore store, DatasetAvailability availability)
        {
            this.options = options;
            this.jsonClient = jsonClient;
            this.dbnClient = dbnClient;
            this.store = store;
            this.availability = availability;
            this.logger = options.Logger;
            this.rootKey = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.CacheRoot));
        }

        /// <summary>
        /// Serves a range
        /// </summary>
        /// <param name="schema">Schema served</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The records of the range, in time order and, at the same time, in the order the symbols were given</returns>
        public async Task<TSource[]> GetAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            List<string> distinctSymbols = CheckRange(dataset, symbols, startUtc, endUtc);
            if(!Enum.IsDefined(cacheMode))
            {
                throw new ArgumentOutOfRangeException(nameof(cacheMode), cacheMode, null);
            }

            this.logger.TimeseriesRequested(schema.Name, dataset, distinctSymbols, startUtc, endUtc, cacheMode);
            List<DateTime> days = GetDays(startUtc, endUtc);
            Dictionary<SymbolDay, TSource[]> served = new Dictionary<SymbolDay, TSource[]>();
            HashSet<SymbolDay> pending = new HashSet<SymbolDay>(distinctSymbols.SelectMany(symbol => days.Select(day => new SymbolDay(symbol, day))));
            if(cacheMode != CacheMode.Bypass)
            {
                await this.ReadCachedAsync(schema, dataset, pending, served, cancellationToken).ConfigureAwait(false);
            }

            if(cacheMode == CacheMode.CacheOnly && pending.Count > 0)
            {
                throw new NotCachedException(dataset, schema.Name, pending.Select(symbolDay => new KeyValuePair<string, DateTime>(symbolDay.Symbol, symbolDay.Day)));
            }

            await this.DownloadPendingAsync(schema, dataset, pending, cacheMode, served, cancellationToken).ConfigureAwait(false);
            return Assemble(schema, distinctSymbols, days, served, startUtc.UtcDateTime, endUtc.UtcDateTime);
        }

        /// <summary>
        /// Makes sure the cache holds a range's symbol-days: finds the cached ones by day file name and archive index, reading no record,
        /// and downloads and saves the others as <see cref="GetAsync"/> with <see cref="CacheMode.Use"/> does, keeping no record once saved
        /// </summary>
        /// <param name="schema">Schema cached</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The symbol-days found cached, saved and not saved</returns>
        public async Task<EnsureCachedReport> EnsureCachedAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            List<string> distinctSymbols = CheckRange(dataset, symbols, startUtc, endUtc);
            List<DateTime> days = GetDays(startUtc, endUtc);
            List<SymbolDay> asked = distinctSymbols.SelectMany(symbol => days.Select(day => new SymbolDay(symbol, day))).ToList();
            HashSet<SymbolDay> pending = new HashSet<SymbolDay>(asked);
            await this.ReadCachedAsync(schema, dataset, pending, null, cancellationToken).ConfigureAwait(false);
            List<SymbolDay> missing = asked.Where(pending.Contains).ToList();
            DateTime? cutAtUtc = await this.DownloadPendingAsync(schema, dataset, pending, CacheMode.Use, null, cancellationToken).ConfigureAwait(false);

            // what the download left uncached is found as the cached days were: by name and index
            HashSet<SymbolDay> notCached = new HashSet<SymbolDay>(missing);
            await this.ReadCachedAsync(schema, dataset, notCached, null, cancellationToken).ConfigureAwait(false);
            DateTimeOffset nowUtc = this.options.TimeProvider.GetUtcNow();
            int pastEnd = notCached.Count(symbolDay => GetDayEnd(symbolDay.Day) > cutAtUtc);
            int notSettled = notCached.Count(symbolDay => !(GetDayEnd(symbolDay.Day) > cutAtUtc) && !IsSettled(symbolDay.Day, nowUtc, this.options.SettleDelay));
            List<KeyValuePair<string, DateTime>> notCachedDays = missing.Where(notCached.Contains).Select(symbolDay => new KeyValuePair<string, DateTime>(symbolDay.Symbol, symbolDay.Day)).ToList();
            return new EnsureCachedReport(asked.Count - missing.Count, missing.Count - notCached.Count, pastEnd, notSettled, notCachedDays);
        }

        /// <summary>
        /// Checks a range's arguments
        /// </summary>
        /// <returns>The symbols, each once, in the order given</returns>
        private static List<string> CheckRange(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            ArgumentException.ThrowIfNullOrEmpty(dataset);
            ArgumentNullException.ThrowIfNull(symbols);
            if(symbols.Count == 0 || symbols.Any(string.IsNullOrEmpty))
            {
                throw new ArgumentException("At least one symbol is needed, and none may be empty.", nameof(symbols));
            }

            if(symbols.Contains(AllSymbols, StringComparer.Ordinal))
            {
                throw new ArgumentException("ALL_SYMBOLS cannot be cached: ask for the raw symbols.", nameof(symbols));
            }

            if(endUtc <= startUtc)
            {
                throw new ArgumentException(Invariant($"The range ends ({endUtc:O}) at or before it starts ({startUtc:O})."), nameof(endUtc));
            }

            return symbols.Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Downloads the pending symbol-days, each claimed so one request at a time downloads it; a day another request was downloading is
        /// looked for in the cache once that request is done, and asked for again when it was not saved. With no <c>served</c>, no record is
        /// kept once saved.
        /// </summary>
        /// <returns>The earliest available end a download was cut at, or <c>null</c> when none was cut</returns>
        private async Task<DateTime?> DownloadPendingAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, HashSet<SymbolDay> pending, CacheMode cacheMode, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            DateTime? cutAtUtc = null;
            while(pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<SymbolDay> claimed = new List<SymbolDay>();
                List<SymbolDay> waited = new List<SymbolDay>();
                List<Task> releases = new List<Task>();
                foreach(SymbolDay symbolDay in pending)
                {
                    if(Claims.TryClaim(this.GetClaimKey(dataset, schema.Name, symbolDay), out Task released))
                    {
                        claimed.Add(symbolDay);
                    }
                    else
                    {
                        waited.Add(symbolDay);
                        releases.Add(released);
                    }
                }

                try
                {
                    if(claimed.Count > 0)
                    {
                        DateTime? cut = await this.DownloadAsync(schema, dataset, claimed, cacheMode, served, cancellationToken).ConfigureAwait(false);
                        cutAtUtc = cut < cutAtUtc || cutAtUtc == null ? cut : cutAtUtc;
                    }
                }
                finally
                {
                    foreach(SymbolDay symbolDay in claimed)
                    {
                        Claims.Release(this.GetClaimKey(dataset, schema.Name, symbolDay));
                    }
                }

                pending = new HashSet<SymbolDay>(waited);
                if(pending.Count > 0)
                {
                    // another request was downloading these: what it saved is read, what it did not is asked for on the next pass
                    this.logger.WaitingForDownload(pending.Count, schema.Name, dataset);
                    await Task.WhenAll(releases).WaitAsync(cancellationToken).ConfigureAwait(false);
                    if(cacheMode == CacheMode.Use)
                    {
                        await this.ReadCachedAsync(schema, dataset, pending, served, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return cutAtUtc;
        }

        /// <summary>
        /// Gets the UTC days a range touches
        /// </summary>
        private static List<DateTime> GetDays(DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            List<DateTime> days = new List<DateTime>();
            DateTime last = endUtc.UtcDateTime.AddTicks(-1).Date;
            for(DateTime day = startUtc.UtcDateTime.Date; day <= last; day = day.AddDays(1))
            {
                days.Add(DateTime.SpecifyKind(day, DateTimeKind.Utc));
            }

            return days;
        }

        private string GetClaimKey(string dataset, string schema, SymbolDay symbolDay)
        {
            return this.rootKey + "|" + dataset + "|" + schema + "|" + symbolDay.Symbol + "|" + symbolDay.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the records of the range, every symbol-day's records together, trimmed to the range and in time order
        /// </summary>
        private static TSource[] Assemble<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, List<string> symbols, List<DateTime> days, Dictionary<SymbolDay, TSource[]> served, DateTime startUtc, DateTime endUtc)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            List<TSource> records = new List<TSource>();
            foreach(string symbol in symbols)
            {
                foreach(DateTime day in days)
                {
                    if(served.TryGetValue(new SymbolDay(symbol, day), out TSource[] dayRecords))
                    {
                        records.AddRange(dayRecords);
                    }
                }
            }

            // OrderBy is stable, so records of the same time keep the order of the symbols given
            return records
                .Select(record => new TimedRecord<TSource>(record, schema.GetFilterTime(record)))
                .Where(timed => timed.Time.HasValue && timed.Time.Value >= startUtc && timed.Time.Value < endUtc)
                .OrderBy(timed => timed.Time.Value)
                .Select(timed => timed.Record)
                .ToArray();
        }

        /// <summary>
        /// Tells whether a symbology lookup failed in a way that only leaves the days unconfirmed: an answer that is no lookup's, or no answer
        /// </summary>
        private static bool IsLookupFailure(Exception ex, CancellationToken cancellationToken)
        {
            return ex is DatabentoHttpException || ex is HttpRequestException || ex is JsonException || ex is InvalidDataException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);
        }

        private static bool IsSymbologyRefusal(DatabentoHttpException ex)
        {
            return ex.Code == DatabentoErrorCase.SymbologyInvalidRequest || ex.Code == DatabentoErrorCase.SymbologyInvalidSymbol;
        }

        /// <summary>
        /// Reads the pending symbol-days the cache holds, listing each month folder once, and takes them out of the pending ones; with no
        /// <paramref name="served"/>, only finds them, by day file name and archive index, reading no record
        /// </summary>
        private async Task ReadCachedAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, HashSet<SymbolDay> pending, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            foreach(IGrouping<DateTime, SymbolDay> month in pending.GroupBy(symbolDay => new DateTime(symbolDay.Day.Year, symbolDay.Day.Month, 1)).ToList())
            {
                CacheMonthFolder folder = this.store.ListMonth(dataset, schema.Name, month.Key);
                foreach(SymbolDay symbolDay in month)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(served == null)
                    {
                        if(await this.store.ContainsAsync(schema, folder, symbolDay.Symbol, symbolDay.Day).ConfigureAwait(false))
                        {
                            pending.Remove(symbolDay);
                        }

                        continue;
                    }

                    TSource[] records = await this.store.ReadAsync(schema, folder, symbolDay.Symbol, symbolDay.Day, cancellationToken).ConfigureAwait(false);
                    if(records != null)
                    {
                        served[symbolDay] = records;
                        pending.Remove(symbolDay);
                    }
                }
            }
        }

        /// <summary>
        /// Serves a symbol-day's records, unless none are kept
        /// </summary>
        private static void Serve<TSource>(Dictionary<SymbolDay, TSource[]> served, SymbolDay symbolDay, IEnumerable<TSource> records)
        {
            if(served != null)
            {
                served[symbolDay] = records as TSource[] ?? records.ToArray();
            }
        }

        /// <summary>
        /// Downloads claimed symbol-days and serves them: symbols missing the same span of days go in one request per chunk of
        /// <see cref="CachedDatabentoClientOptions.MaxSymbolsPerRequest"/>, over that span's whole days, cut at the available end when one
        /// is known. By default it is known only after Databento refused a request past it: the end is then read and remembered, and the
        /// request cut and sent once more. The days a cut leaves out are served empty, or the refusal thrown once the other days are saved,
        /// as <see cref="CachedDatabentoClientOptions.AvailableEndRefusal"/> says.
        /// </summary>
        /// <returns>The earliest available end a chunk was cut at, or <c>null</c> when none was cut</returns>
        private async Task<DateTime?> DownloadAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, List<SymbolDay> claimed, CacheMode cacheMode, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            Dictionary<string, HashSet<DateTime>> daysBySymbol = claimed.GroupBy(symbolDay => symbolDay.Symbol, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(symbolDay => symbolDay.Day).ToHashSet(), StringComparer.Ordinal);
            DateTimeOffset nowUtc = this.options.TimeProvider.GetUtcNow();
            bool checkFirst = this.options.CheckAvailabilityFirst;

            // thrown once every chunk is served, with AvailableEndRefusal.Throw
            ExceptionDispatchInfo pastEnd = null;

            // the earliest end a chunk was cut at, for the days past it
            DateTime? cutAtUtc = null;
            foreach(IGrouping<DaySpan, string> span in daysBySymbol.Keys.GroupBy(symbol => new DaySpan(daysBySymbol[symbol].Min(), daysBySymbol[symbol].Max())))
            {
                DayAvailability dayAvailability = checkFirst ? await this.availability.GetAsync(this.jsonClient, dataset, schema.Name, span.Key.First, span.Key.Last, cancellationToken).ConfigureAwait(false) : null;
                DateTime startUtc = span.Key.First;
                DateTime spanEndUtc = span.Key.Last.AddDays(1);
                foreach(string[] chunk in span.Chunk(this.options.MaxSymbolsPerRequest))
                {
                    DateTime endUtc = this.CutAtAvailableEnd(schema.Name, dataset, spanEndUtc, dayAvailability, nowUtc);
                    ChunkAnswer<TJson> answer = null;
                    DatabentoHttpException refusal = null;
                    if(endUtc > startUtc)
                    {
                        try
                        {
                            answer = await this.DownloadChunkAsync(schema, dataset, chunk, startUtc, endUtc, !checkFirst, cancellationToken).ConfigureAwait(false);
                        }
                        catch(DatabentoHttpException ex) when(!checkFirst && IsPastAvailableEnd(ex))
                        {
                            DateTime? availableEndUtc = await this.ReadAvailableEndAsync(schema.Name, dataset, cancellationToken).ConfigureAwait(false);
                            if(availableEndUtc == null)
                            {
                                throw;
                            }

                            // cut at the end just read and sent once more: a second refusal propagates
                            refusal = ex;
                            endUtc = availableEndUtc < endUtc ? availableEndUtc.Value : endUtc;
                            if(endUtc > startUtc)
                            {
                                answer = await this.DownloadChunkAsync(schema, dataset, chunk, startUtc, endUtc, false, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }

                    if(answer != null)
                    {
                        await this.ServeChunkAsync(schema, dataset, chunk, daysBySymbol, answer, new DayFinality(nowUtc, this.options.SettleDelay, endUtc, dayAvailability), cacheMode, served, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // nothing of the span is available yet: served empty, saved never
                        this.logger.NotAvailableYet(schema.Name, dataset, chunk, startUtc, endUtc);
                        foreach(string symbol in chunk)
                        {
                            foreach(DateTime day in daysBySymbol[symbol])
                            {
                                Serve(served, new SymbolDay(symbol, day), Array.Empty<TSource>());
                            }
                        }
                    }

                    if(endUtc < spanEndUtc && (cutAtUtc == null || endUtc < cutAtUtc))
                    {
                        cutAtUtc = endUtc;
                    }

                    if(checkFirst || endUtc >= spanEndUtc)
                    {
                        continue;
                    }

                    if(this.options.AvailableEndRefusal == AvailableEndRefusal.Throw)
                    {
                        pastEnd ??= ExceptionDispatchInfo.Capture(refusal ?? CreatePastEndRefusal(schema.Name, dataset, startUtc, spanEndUtc, endUtc));
                        continue;
                    }

                    // a settled day served short is one a backtest expected data for, so it is told of above Debug
                    List<DateTime> settledPastEnd = chunk.SelectMany(symbol => daysBySymbol[symbol]).Where(day => GetDayEnd(day) > endUtc && IsSettled(day, nowUtc, this.options.SettleDelay)).ToList();
                    if(settledPastEnd.Count > 0)
                    {
                        this.logger.SettledDaysPastAvailableEnd(settledPastEnd.Count, schema.Name, dataset, settledPastEnd.Min(), settledPastEnd.Max(), endUtc);
                    }
                }
            }

            pastEnd?.Throw();
            return cutAtUtc;
        }

        /// <summary>
        /// Gets the end a request over whole days is sent with: cut at the available end the check found, or else at the one remembered for
        /// the schema while it is not due on the <see cref="CachedDatabentoClientOptions.AvailableEndRefreshInterval"/> grid
        /// </summary>
        private DateTime CutAtAvailableEnd(string schema, string dataset, DateTime spanEndUtc, DayAvailability dayAvailability, DateTimeOffset nowUtc)
        {
            DateTime? availableEndUtc = dayAvailability?.AvailableEndUtc;
            if(dayAvailability == null && this.availableEnds.TryGetValue((dataset, schema), out (DateTime End, DateTimeOffset RefreshUtc) remembered) && nowUtc < remembered.RefreshUtc)
            {
                availableEndUtc = remembered.End;
            }

            return availableEndUtc < spanEndUtc ? availableEndUtc.Value : spanEndUtc;
        }

        /// <summary>
        /// Tells whether a downloaded UTC day has settled, so it may be saved: its UTC day ended at least a delay before now
        /// </summary>
        /// <param name="dayUtc">The UTC day; its time of day is ignored</param>
        /// <param name="nowUtc">The time now</param>
        /// <param name="settleDelay">How long after its end the day settles</param>
        /// <returns><c>true</c> when the day's end plus <paramref name="settleDelay"/> is at or before <paramref name="nowUtc"/></returns>
        public static bool IsSettled(DateTime dayUtc, DateTimeOffset nowUtc, TimeSpan settleDelay)
        {
            return GetDayEnd(dayUtc) + settleDelay <= nowUtc.UtcDateTime;
        }

        /// <summary>
        /// Gets the end of a UTC day
        /// </summary>
        private static DateTime GetDayEnd(DateTime day)
        {
            return DateTime.SpecifyKind(day.Date, DateTimeKind.Utc).AddDays(1);
        }

        private static bool IsPastAvailableEnd(DatabentoHttpException ex)
        {
            return ex.Code == DatabentoErrorCase.DataEndAfterAvailableEnd || ex.Code == DatabentoErrorCase.DataStartAfterAvailableEnd;
        }

        /// <summary>
        /// Creates the refusal Databento would make of a request past a remembered available end, which the request was cut at instead of
        /// sent: a 422 <c>data_start_after_available_end</c> when it starts at or past the end, else <c>data_end_after_available_end</c>
        /// </summary>
        private static DatabentoHttpException CreatePastEndRefusal(string schema, string dataset, DateTime startUtc, DateTime endUtc, DateTime availableEndUtc)
        {
            bool startsPastIt = startUtc >= availableEndUtc;
            string message = Invariant($"The request for {schema} of {dataset} from {startUtc:O} to {endUtc:O} {(startsPastIt ? "starts at or past" : "ends past")} the available end {availableEndUtc:O} the cache remembers, so it was cut at it");
            return new DatabentoHttpException(422, message, startsPastIt ? "data_start_after_available_end" : "data_end_after_available_end");
        }

        /// <summary>
        /// Reads the available end of a dataset's schema and remembers it until the next multiple of
        /// <see cref="CachedDatabentoClientOptions.AvailableEndRefreshInterval"/> on the UTC clock after the read, so one read exactly on a
        /// line is used until the next. A failed read is logged; a read the caller cancels throws <see cref="OperationCanceledException"/>.
        /// </summary>
        /// <returns>The end read, or <c>null</c> when the read failed</returns>
        private async Task<DateTime?> ReadAvailableEndAsync(string schema, string dataset, CancellationToken cancellationToken)
        {
            DateTimeOffset readUtc = this.options.TimeProvider.GetUtcNow();
            DateTime availableEndUtc;
            try
            {
                availableEndUtc = await DatasetAvailability.GetAvailableEndAsync(this.jsonClient, dataset, schema, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                this.logger.AvailableEndReadFailed(schema, dataset, ex);
                return null;
            }

            long step = this.options.AvailableEndRefreshInterval.Ticks;
            DateTimeOffset refreshUtc = new DateTimeOffset(((readUtc.UtcTicks / step) + 1) * step, TimeSpan.Zero);
            this.availableEnds[(dataset, schema)] = (availableEndUtc, refreshUtc);
            this.logger.AvailableEndRemembered(schema, dataset, availableEndUtc, refreshUtc.UtcDateTime);
            return availableEndUtc;
        }

        /// <summary>
        /// Downloads a chunk's records, split by symbol and day. A symbology refusal of the chunk is looked up, and the request sent again with
        /// only the symbols listed in the span; the not-found symbols are then confirmed not listed. With <c>pastEndHandled</c> a refusal
        /// past the available end is not logged as a failed download, as the caller cuts the request and sends it once more.
        /// </summary>
        /// <exception cref="DatabentoHttpException">Databento refused the request, or refused it again with only the listed symbols</exception>
        private async Task<ChunkAnswer<TJson>> DownloadChunkAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, string[] chunk, DateTime startUtc, DateTime endUtc, bool pastEndHandled, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            TJson[] records;
            HashSet<string> notListed = new HashSet<string>(StringComparer.Ordinal);
            long startTimestamp = this.options.TimeProvider.GetTimestamp();

            // the symbols of the request answered or failed, and when it was sent: the retry's after a refusal
            string[] asked = chunk;

            // a refusal the lookup cannot resolve is logged as such where it is thrown
            Exception unresolved = null;
            try
            {
                try
                {
                    records = await this.FetchAsync(schema, dataset, chunk, startUtc, endUtc, cancellationToken).ConfigureAwait(false);
                }
                catch(DatabentoHttpException ex) when(IsSymbologyRefusal(ex))
                {
                    // looked up over the days asked for, which end at the available end
                    SymbolListing listing = await SymbolListing.ResolveAsync(this.jsonClient, dataset, chunk, startUtc.Date, endUtc.AddTicks(-1).Date, this.options.MaxSymbolsPerRequest, cancellationToken).ConfigureAwait(false);
                    notListed.UnionWith(chunk.Where(listing.IsConfirmedNotListed));
                    if(notListed.Count == 0 || chunk.Any(symbol => !listing.IsListed(symbol) && !notListed.Contains(symbol)))
                    {
                        // the refusal is not about a symbol the lookup can tell of, or the lookup left a symbol unanswered: nothing is saved for it
                        this.logger.RefusalUnresolved(schema.Name, dataset, chunk.Length, ex.Message);
                        unresolved = ex;
                        throw;
                    }

                    string[] listed = chunk.Where(listing.IsListed).ToArray();
                    if(listed.Length == 0)
                    {
                        this.logger.RefusedNoneListed(schema.Name, dataset, chunk.Length, ex.Message);
                        records = Array.Empty<TJson>();
                    }
                    else
                    {
                        this.logger.RefusedAskingAgain(schema.Name, dataset, chunk.Length, ex.Message, listed.Length);
                        asked = listed;
                        startTimestamp = this.options.TimeProvider.GetTimestamp();
                        records = await this.FetchAsync(schema, dataset, listed, startUtc, endUtc, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch(Exception ex) when(!ReferenceEquals(ex, unresolved) && !(pastEndHandled && ex is DatabentoHttpException http && IsPastAvailableEnd(http)) && !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                this.logger.DownloadFailed(schema.Name, dataset, asked, this.options.TimeProvider.GetElapsedTime(startTimestamp), ex);
                throw;
            }

            HashSet<string> requested = new HashSet<string>(chunk, StringComparer.Ordinal);
            Dictionary<SymbolDay, List<TJson>> bySymbolDay = new Dictionary<SymbolDay, List<TJson>>();
            foreach(TJson record in records)
            {
                // the clients set every record's symbol: from the request for one symbol, from the answer's mapping for several
                string symbol = record.Symbol;
                DateTime? time = schema.GetFilterTime(record);
                if(symbol == null || !requested.Contains(symbol) || !time.HasValue)
                {
                    continue;
                }

                SymbolDay symbolDay = new SymbolDay(symbol, DateTime.SpecifyKind(time.Value.Date, DateTimeKind.Utc));
                if(!bySymbolDay.TryGetValue(symbolDay, out List<TJson> dayRecords))
                {
                    dayRecords = new List<TJson>();
                    bySymbolDay.Add(symbolDay, dayRecords);
                }

                dayRecords.Add(record);
            }

            // nothing was asked again when no symbol of the chunk was listed
            if(notListed.Count < chunk.Length && this.logger.IsEnabled(LogLevel.Information))
            {
                int symbolsWithRecords = bySymbolDay.Keys.Select(symbolDay => symbolDay.Symbol).Distinct(StringComparer.Ordinal).Count();
                this.logger.Downloaded(records.Length, schema.Name, dataset, symbolsWithRecords, asked.Length, this.options.TimeProvider.GetElapsedTime(startTimestamp));
            }

            return new ChunkAnswer<TJson>(bySymbolDay, notListed);
        }

        /// <summary>
        /// Serves a chunk's claimed symbol-days and saves the final ones: a day with records always, an empty day as the policies say. The
        /// two suspect saves, an empty day and a bypass replacing a cached day, are made only for a day whose Databento condition is
        /// <c>available</c>: the conditions are read once for the chunk, and only when there is such a save to make. With
        /// <see cref="CachedDatabentoClientOptions.CheckAvailabilityFirst"/> a final day was already found available, so none is read.
        /// </summary>
        private async Task ServeChunkAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, string[] chunk, Dictionary<string, HashSet<DateTime>> daysBySymbol, ChunkAnswer<TJson> answer, DayFinality finality, CacheMode cacheMode, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            bool bypass = cacheMode == CacheMode.Bypass;
            List<SymbolDay> unconfirmedEmpty = new List<SymbolDay>();
            List<GuardedSave<TJson>> guarded = new List<GuardedSave<TJson>>();
            foreach(string symbol in chunk)
            {
                foreach(DateTime day in daysBySymbol[symbol].OrderBy(day => day))
                {
                    SymbolDay symbolDay = new SymbolDay(symbol, day);
                    bool final = finality.IsFinal(day);
                    answer.Records.TryGetValue(symbolDay, out List<TJson> records);

                    // only looked at: what a bypass serves is the download's, so no cache hit is logged
                    TSource[] cached = bypass && (final || records == null) ? await this.store.ReadAsync(schema, this.store.ListMonth(dataset, schema.Name, day), symbol, day, cancellationToken, logServed: false).ConfigureAwait(false) : null;
                    if(records != null)
                    {
                        Serve(served, symbolDay, records);
                        if(!final)
                        {
                            this.LogNotFinal(symbol, day, schema.Name, finality);
                        }
                        else if(cached != null)
                        {
                            guarded.Add(new GuardedSave<TJson>(symbolDay, records, null, cached.Length));
                        }
                        else
                        {
                            await this.store.SaveAsync(dataset, schema.Name, symbol, day, records, bypass, cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if(cached != null)
                    {
                        if(this.options.BypassMissAction == BypassMissAction.KeepCached || !final)
                        {
                            this.logger.BypassKeptCached(symbol, day, schema.Name);
                            Serve(served, symbolDay, cached);
                        }
                        else
                        {
                            Serve(served, symbolDay, Array.Empty<TSource>());
                            guarded.Add(new GuardedSave<TJson>(symbolDay, new List<TJson>(), "a bypass answered no records, so it replaces the cached day", cached.Length));
                        }

                        continue;
                    }

                    Serve(served, symbolDay, Array.Empty<TSource>());
                    if(!final)
                    {
                        this.LogNotFinal(symbol, day, schema.Name, finality);
                        continue;
                    }

                    if(this.options.EmptyDays == EmptyDayPolicy.Never)
                    {
                        this.logger.EmptyDayNotSaved(symbol, day, schema.Name, "EmptyDays is Never");
                        continue;
                    }

                    if(this.options.EmptyDays == EmptyDayPolicy.CacheAlways || answer.NotListed.Contains(symbol))
                    {
                        string reason = this.options.EmptyDays == EmptyDayPolicy.CacheAlways ? "EmptyDays is CacheAlways" : "it was not listed";
                        guarded.Add(new GuardedSave<TJson>(symbolDay, new List<TJson>(), reason, null));
                    }
                    else
                    {
                        unconfirmedEmpty.Add(symbolDay);
                    }
                }
            }

            if(unconfirmedEmpty.Count > 0)
            {
                foreach(SymbolDay symbolDay in await this.ConfirmNotListedAsync(dataset, schema.Name, unconfirmedEmpty, cancellationToken).ConfigureAwait(false))
                {
                    guarded.Add(new GuardedSave<TJson>(symbolDay, new List<TJson>(), "it was not listed", null));
                }
            }

            if(guarded.Count > 0)
            {
                await this.SaveIfAvailableAsync(dataset, schema.Name, guarded, bypass, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes the suspect saves of a chunk whose day's Databento condition is <c>available</c>, reading the conditions of their days in
        /// one call; the others are left unsaved, the cached day of a bypass kept, and what was downloaded still served. A failed read
        /// saves none of them and never throws over the answer served.
        /// </summary>
        private async Task SaveIfAvailableAsync<TJson>(string dataset, string schema, List<GuardedSave<TJson>> guarded, bool bypass, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<DateTime, string> notAvailable;
            try
            {
                notAvailable = await this.availability.GetNotFinalAsync(this.jsonClient, dataset, guarded.Select(save => save.SymbolDay.Day), cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                this.logger.ConditionsReadFailed(guarded.Count, schema, dataset, ex);
                notAvailable = null;
            }

            foreach(GuardedSave<TJson> save in guarded)
            {
                string symbol = save.SymbolDay.Symbol;
                DateTime day = save.SymbolDay.Day;
                string condition = null;
                if(notAvailable == null || notAvailable.TryGetValue(day, out condition))
                {
                    string reason = notAvailable == null ? "Databento's condition for the day could not be read" : condition == null ? "Databento lists no condition for the day, nor for a later one" : Invariant($"Databento's condition for the day is {condition}");
                    if(save.CachedCount.HasValue)
                    {
                        this.logger.BypassNotReplaced(symbol, day, schema, reason);
                    }
                    else
                    {
                        this.logger.EmptyDayNotSaved(symbol, day, schema, reason);
                    }

                    continue;
                }

                await this.store.SaveAsync(dataset, schema, symbol, day, save.Records, bypass, cancellationToken, save.EmptyReason).ConfigureAwait(false);
                if(save.CachedCount.HasValue)
                {
                    this.logger.BypassReplaced(symbol, day, schema, save.CachedCount.Value, save.Records.Count);
                }
            }
        }

        /// <summary>
        /// Logs a day served but not saved, and why
        /// </summary>
        private void LogNotFinal(string symbol, DateTime day, string schema, DayFinality finality)
        {
            if(!this.logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            string reason;
            if(!finality.IsSettled(day))
            {
                reason = finality.SettleDelay == TimeSpan.Zero ? "its UTC day has not ended" : Invariant($"its UTC day has not ended {finality.SettleDelay} ago");
            }
            else if(finality.Availability != null)
            {
                reason = Invariant($"Databento has not found it final; the available end is {finality.Availability.AvailableEndUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }
            else
            {
                reason = Invariant($"it ends past the available end {finality.EndUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }

            this.logger.NotFinal(symbol, day, schema, reason);
        }

        /// <summary>
        /// Gets the empty symbol-days the symbology lookup confirms were not listed, so they may be saved as empty; a failed lookup confirms
        /// none
        /// </summary>
        private async Task<List<SymbolDay>> ConfirmNotListedAsync(string dataset, string schema, List<SymbolDay> emptyDays, CancellationToken cancellationToken)
        {
            List<SymbolDay> confirmed = new List<SymbolDay>();
            SymbolListing listing;
            try
            {
                listing = await SymbolListing.ResolveAsync(this.jsonClient, dataset, emptyDays.Select(symbolDay => symbolDay.Symbol).Distinct(StringComparer.Ordinal).ToList(), emptyDays.Min(symbolDay => symbolDay.Day), emptyDays.Max(symbolDay => symbolDay.Day), this.options.MaxSymbolsPerRequest, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(IsLookupFailure(ex, cancellationToken))
            {
                this.logger.EmptyDaysLookupFailed(emptyDays.Count, schema, dataset, ex.Message);
                return confirmed;
            }

            foreach(SymbolDay symbolDay in emptyDays)
            {
                if(listing.IsConfirmedNotListed(symbolDay.Symbol, symbolDay.Day))
                {
                    confirmed.Add(symbolDay);
                }
                else
                {
                    this.logger.EmptyDayNotSaved(symbolDay.Symbol, symbolDay.Day, schema, "the symbology lookup has not confirmed it was not listed");
                }
            }

            return confirmed;
        }

        /// <summary>
        /// Downloads the records of symbols over a window, logging the request first
        /// </summary>
        private Task<TJson[]> FetchAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, string[] symbols, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            this.logger.Requesting(schema.Name, dataset, symbols, startUtc.Date, endUtc.AddTicks(-1).Date, startUtc, endUtc);
            RangeRequest request = new RangeRequest(dataset, symbols, startUtc, endUtc, cancellationToken);
            return this.options.Transport == DatabentoTransport.Dbn ? schema.DownloadDbn(this.dbnClient, request) : schema.DownloadJson(this.jsonClient, request);
        }

        /// <summary>
        /// A symbol and a UTC day
        /// </summary>
        private readonly record struct SymbolDay(string Symbol, DateTime Day);

        /// <summary>
        /// The first and last UTC day a symbol is missing
        /// </summary>
        private readonly record struct DaySpan(DateTime First, DateTime Last);

        /// <summary>
        /// A record and the time Databento filters it by
        /// </summary>
        private readonly record struct TimedRecord<TSource>(TSource Record, DateTime? Time);

        /// <summary>
        /// A chunk's records by symbol-day, and the symbols the lookup confirmed were not listed in the span
        /// </summary>
        private sealed record ChunkAnswer<TJson>(Dictionary<SymbolDay, List<TJson>> Records, HashSet<string> NotListed);

        /// <summary>
        /// A save made only when Databento's condition for the day is <c>available</c>: an empty day, or a bypass replacing a cached day
        /// </summary>
        /// <param name="SymbolDay">The symbol-day</param>
        /// <param name="Records">The records downloaded, none for an empty day</param>
        /// <param name="EmptyReason">Why an empty day is saved as empty, for the log; <c>null</c> for a day with records</param>
        /// <param name="CachedCount">The records of the cached day a bypass replaces, or <c>null</c> when none is cached</param>
        private sealed record GuardedSave<TJson>(SymbolDay SymbolDay, List<TJson> Records, string EmptyReason, int? CachedCount);

        /// <summary>
        /// Which downloaded days may be saved: those <see cref="CachedTimeseries.IsSettled"/> finds settled that end by the request's end and, when
        /// availability was checked first, that Databento also found final
        /// </summary>
        /// <param name="NowUtc">The time of the download</param>
        /// <param name="SettleDelay">How long after its end a day is first saved</param>
        /// <param name="EndUtc">The end the request was sent with, cut at the available end when one was known</param>
        /// <param name="Availability">What the check found, or <c>null</c> when availability was not checked</param>
        private sealed record DayFinality(DateTimeOffset NowUtc, TimeSpan SettleDelay, DateTime EndUtc, DayAvailability Availability)
        {
            public bool IsSettled(DateTime day)
            {
                return CachedTimeseries.IsSettled(day, this.NowUtc, this.SettleDelay);
            }

            public bool IsFinal(DateTime day)
            {
                return this.IsSettled(day) && GetDayEnd(day) <= this.EndUtc && (this.Availability == null || this.Availability.IsFinal(day));
            }
        }
    }
}
