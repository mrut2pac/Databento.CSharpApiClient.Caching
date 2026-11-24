using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Serves a time-series request from the cache: each UTC day the range touches, per symbol, from its day file, else the archive, else
    /// downloaded whole, split by symbol and day, saved once final, and trimmed to the range when served.
    /// </summary>
    internal sealed class CachedTimeseries
    {
        private const string AllSymbols = "ALL_SYMBOLS";

        private readonly CachedDatabentoClientOptions options;

        private readonly DatabentoJsonClient jsonClient;

        private readonly DatabentoClient dbnClient;

        private readonly SymbolDayStore store;

        private readonly DatasetAvailability availability;

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

            if(!Enum.IsDefined(cacheMode))
            {
                throw new ArgumentOutOfRangeException(nameof(cacheMode), cacheMode, null);
            }

            List<string> distinctSymbols = symbols.Distinct(StringComparer.Ordinal).ToList();
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
                        await this.DownloadAsync(schema, dataset, claimed, cacheMode, served, cancellationToken).ConfigureAwait(false);
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
                    await Task.WhenAll(releases).WaitAsync(cancellationToken).ConfigureAwait(false);
                    if(cacheMode == CacheMode.Use)
                    {
                        await this.ReadCachedAsync(schema, dataset, pending, served, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return Assemble(schema, distinctSymbols, days, served, startUtc.UtcDateTime, endUtc.UtcDateTime);
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
        /// Reads the pending symbol-days the cache holds, listing each month folder once, and takes them out of the pending ones
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
        /// Downloads claimed symbol-days and serves them: symbols missing the same span of days go in one request per chunk of
        /// <see cref="CachedDatabentoClientOptions.MaxSymbolsPerRequest"/>, over that span's whole days
        /// </summary>
        private async Task DownloadAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, List<SymbolDay> claimed, CacheMode cacheMode, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            Dictionary<string, HashSet<DateTime>> daysBySymbol = claimed.GroupBy(symbolDay => symbolDay.Symbol, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(symbolDay => symbolDay.Day).ToHashSet(), StringComparer.Ordinal);
            foreach(IGrouping<DaySpan, string> span in daysBySymbol.Keys.GroupBy(symbol => new DaySpan(daysBySymbol[symbol].Min(), daysBySymbol[symbol].Max())))
            {
                DayAvailability dayAvailability = await this.availability.GetAsync(this.jsonClient, dataset, schema.Name, span.Key.First, span.Key.Last, cancellationToken).ConfigureAwait(false);
                DateTime startUtc = span.Key.First;
                DateTime endUtc = span.Key.Last.AddDays(1);
                if(dayAvailability.AvailableEndUtc < endUtc)
                {
                    endUtc = dayAvailability.AvailableEndUtc;
                }

                foreach(string[] chunk in span.Chunk(this.options.MaxSymbolsPerRequest))
                {
                    if(endUtc <= startUtc)
                    {
                        // nothing of the span is available yet: served empty, saved never
                        foreach(string symbol in chunk)
                        {
                            foreach(DateTime day in daysBySymbol[symbol])
                            {
                                served[new SymbolDay(symbol, day)] = Array.Empty<TSource>();
                            }
                        }

                        continue;
                    }

                    ChunkAnswer<TJson> answer = await this.DownloadChunkAsync(schema, dataset, chunk, startUtc, endUtc, cancellationToken).ConfigureAwait(false);
                    await this.ServeChunkAsync(schema, dataset, chunk, daysBySymbol, answer, dayAvailability, cacheMode, served, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Downloads a chunk's records, split by symbol and day. A symbology refusal of the chunk is looked up, and the request sent again with
        /// only the symbols listed in the span; the not-found symbols are then confirmed not listed.
        /// </summary>
        /// <exception cref="DatabentoHttpException">Databento refused the request, or refused it again with only the listed symbols</exception>
        private async Task<ChunkAnswer<TJson>> DownloadChunkAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, string[] chunk, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            TJson[] records;
            HashSet<string> notListed = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                records = await this.FetchAsync(schema, new RangeRequest(dataset, chunk, startUtc, endUtc, cancellationToken)).ConfigureAwait(false);
            }
            catch(DatabentoHttpException ex) when(IsSymbologyRefusal(ex))
            {
                // looked up over the days asked for, which end at the available end
                SymbolListing listing = await SymbolListing.ResolveAsync(this.jsonClient, dataset, chunk, startUtc.Date, endUtc.AddTicks(-1).Date, this.options.MaxSymbolsPerRequest, cancellationToken).ConfigureAwait(false);
                notListed.UnionWith(chunk.Where(listing.IsConfirmedNotListed));
                if(notListed.Count == 0 || chunk.Any(symbol => !listing.IsListed(symbol) && !notListed.Contains(symbol)))
                {
                    // the refusal is not about a symbol the lookup can tell of, or the lookup left a symbol unanswered: nothing is saved for it
                    throw;
                }

                string[] listed = chunk.Where(listing.IsListed).ToArray();
                records = listed.Length == 0 ? Array.Empty<TJson>() : await this.FetchAsync(schema, new RangeRequest(dataset, listed, startUtc, endUtc, cancellationToken)).ConfigureAwait(false);
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

            return new ChunkAnswer<TJson>(bySymbolDay, notListed);
        }

        /// <summary>
        /// Serves a chunk's claimed symbol-days and saves the final ones: a day with records always, an empty day as the policies say
        /// </summary>
        private async Task ServeChunkAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, string dataset, string[] chunk, Dictionary<string, HashSet<DateTime>> daysBySymbol, ChunkAnswer<TJson> answer, DayAvailability dayAvailability, CacheMode cacheMode, Dictionary<SymbolDay, TSource[]> served, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            bool bypass = cacheMode == CacheMode.Bypass;
            List<SymbolDay> unconfirmedEmpty = new List<SymbolDay>();
            foreach(string symbol in chunk)
            {
                foreach(DateTime day in daysBySymbol[symbol].OrderBy(day => day))
                {
                    SymbolDay symbolDay = new SymbolDay(symbol, day);
                    bool final = dayAvailability.IsFinal(day);
                    if(answer.Records.TryGetValue(symbolDay, out List<TJson> records))
                    {
                        served[symbolDay] = records.ToArray();
                        if(final)
                        {
                            await this.store.SaveAsync(dataset, schema.Name, symbol, day, records, bypass, cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if(bypass)
                    {
                        TSource[] cached = await this.store.ReadAsync(schema, this.store.ListMonth(dataset, schema.Name, day), symbol, day, cancellationToken).ConfigureAwait(false);
                        if(cached != null)
                        {
                            if(this.options.BypassMissAction == BypassMissAction.KeepCached || !final)
                            {
                                served[symbolDay] = cached;
                            }
                            else
                            {
                                served[symbolDay] = Array.Empty<TSource>();
                                await this.store.SaveAsync(dataset, schema.Name, symbol, day, Array.Empty<TJson>(), true, cancellationToken).ConfigureAwait(false);
                            }

                            continue;
                        }
                    }

                    served[symbolDay] = Array.Empty<TSource>();
                    if(!final || this.options.EmptyDays == EmptyDayPolicy.Never)
                    {
                        continue;
                    }

                    if(this.options.EmptyDays == EmptyDayPolicy.CacheAlways || answer.NotListed.Contains(symbol))
                    {
                        await this.store.SaveAsync(dataset, schema.Name, symbol, day, Array.Empty<TJson>(), bypass, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        unconfirmedEmpty.Add(symbolDay);
                    }
                }
            }

            if(unconfirmedEmpty.Count > 0)
            {
                await this.SaveConfirmedNotListedAsync(dataset, schema.Name, unconfirmedEmpty, bypass, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Saves as empty the empty symbol-days the symbology lookup confirms were not listed; a failed lookup leaves them unsaved
        /// </summary>
        private async Task SaveConfirmedNotListedAsync(string dataset, string schema, List<SymbolDay> emptyDays, bool overwrite, CancellationToken cancellationToken)
        {
            SymbolListing listing;
            try
            {
                listing = await SymbolListing.ResolveAsync(this.jsonClient, dataset, emptyDays.Select(symbolDay => symbolDay.Symbol).Distinct(StringComparer.Ordinal).ToList(), emptyDays.Min(symbolDay => symbolDay.Day), emptyDays.Max(symbolDay => symbolDay.Day), this.options.MaxSymbolsPerRequest, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(IsLookupFailure(ex, cancellationToken))
            {
                return;
            }

            foreach(SymbolDay symbolDay in emptyDays.Where(symbolDay => listing.IsConfirmedNotListed(symbolDay.Symbol, symbolDay.Day)))
            {
                await this.store.SaveAsync(dataset, schema, symbolDay.Symbol, symbolDay.Day, Array.Empty<IRecord>(), overwrite, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task<TJson[]> FetchAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, RangeRequest request)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
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
    }
}
