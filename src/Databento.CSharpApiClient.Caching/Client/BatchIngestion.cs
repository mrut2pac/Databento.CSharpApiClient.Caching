using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

using ZstdSharp;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Saves a batch folder as day files: its records split by symbol and by the UTC day the schema's filter timestamp falls on, for the
    /// final days its range holds whole, then its symbol-days without records as <see cref="EmptyDayPolicy"/> says. A symbol-day the cache
    /// already holds is left as it is. Each data file is read whole before its days are saved, all files together when split by size.
    /// </summary>
    internal sealed class BatchIngestion
    {
        private readonly CachedDatabentoClientOptions options;

        private readonly DatabentoJsonClient jsonClient;

        private readonly SymbolDayStore store;

        public BatchIngestion(CachedDatabentoClientOptions options, DatabentoJsonClient jsonClient, SymbolDayStore store)
        {
            this.options = options;
            this.jsonClient = jsonClient;
            this.store = store;
        }

        public async Task<BatchCounts> IngestAsync<TSource, TRecord>(CachedSchema<TSource, TRecord> schema, BatchFolder folder, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            BatchCounts counts = new BatchCounts();
            List<DateTime> finalDays = folder.GetFinalDays(out List<DateTime> leftOut);
            counts.DaysLeftOut.UnionWith(leftOut);
            if(finalDays.Count == 0)
            {
                return counts;
            }

            SymbolDayRun run = new SymbolDayRun(folder, finalDays);
            IEnumerable<IReadOnlyList<string>> groups = folder.SplitBySize ? new[] { folder.DataFiles } : folder.DataFiles.Select(file => (IReadOnlyList<string>)new[] { file });
            foreach(IReadOnlyList<string> group in groups)
            {
                Dictionary<SymbolDay, List<TRecord>> buckets = new Dictionary<SymbolDay, List<TRecord>>();
                foreach(string file in group)
                {
                    await this.ReadFileAsync(schema, run, file, buckets, cancellationToken).ConfigureAwait(false);
                }

                foreach(KeyValuePair<SymbolDay, List<TRecord>> bucket in buckets.OrderBy(bucket => bucket.Key.Day).ThenBy(bucket => bucket.Key.Symbol, StringComparer.Ordinal))
                {
                    run.Handled.Add(bucket.Key);
                    if(await this.SaveNewAsync(schema, run, bucket.Key, bucket.Value, cancellationToken).ConfigureAwait(false))
                    {
                        ++counts.Saved;
                    }
                    else
                    {
                        ++counts.AlreadyCached;
                    }
                }
            }

            await this.SaveEmptyDaysAsync(schema, run, counts, cancellationToken).ConfigureAwait(false);
            return counts;
        }

        /// <summary>
        /// Tells whether a symbology lookup failed in a way that only leaves days unconfirmed
        /// </summary>
        private static bool IsLookupFailure(Exception ex, CancellationToken cancellationToken)
        {
            return ex is DatabentoHttpException || ex is HttpRequestException || ex is JsonException || ex is InvalidDataException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);
        }

        /// <summary>
        /// Reads a data file's records into the buckets of the final days, each under its symbol: the record's own, else the one the
        /// symbology lookup gives its instrument id that day
        /// </summary>
        private async Task ReadFileAsync<TSource, TRecord>(CachedSchema<TSource, TRecord> schema, SymbolDayRun run, string path, Dictionary<SymbolDay, List<TRecord>> buckets, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using(file.ConfigureAwait(false))
            {
                Stream data = BatchFolder.IsZstd(path) ? new DecompressionStream(file) : file;
                try
                {
                    Func<Stream, CancellationToken, IAsyncEnumerable<TRecord>> read = BatchFolder.IsDbn(path) ? schema.ReadDbn : schema.ReadJson;
                    await foreach(TRecord record in read(data, cancellationToken).ConfigureAwait(false))
                    {
                        DateTime? time = schema.GetFilterTime(record);
                        if(!time.HasValue)
                        {
                            continue;
                        }

                        DateTime day = DateTime.SpecifyKind(time.Value.Date, DateTimeKind.Utc);
                        if(!run.FinalDays.Contains(day))
                        {
                            continue;
                        }

                        string symbol = record.Symbol;
                        if(symbol == null)
                        {
                            SymbolListing listing = await this.GetListingAsync(run, cancellationToken).ConfigureAwait(false);
                            symbol = listing.FindSymbol(record.InstrumentId, day);
                            if(symbol == null)
                            {
                                continue;
                            }

                            schema.SetSymbol(record, symbol);
                        }

                        if(!run.Symbols.Contains(symbol))
                        {
                            continue;
                        }

                        SymbolDay symbolDay = new SymbolDay(symbol, day);
                        if(!buckets.TryGetValue(symbolDay, out List<TRecord> records))
                        {
                            records = new List<TRecord>();
                            buckets.Add(symbolDay, records);
                        }

                        records.Add(record);
                    }
                }
                finally
                {
                    if(data != file)
                    {
                        await data.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Saves a symbol-day unless the cache already holds it
        /// </summary>
        /// <returns><c>true</c> when it was saved</returns>
        private async Task<bool> SaveNewAsync<TSource, TRecord>(CachedSchema<TSource, TRecord> schema, SymbolDayRun run, SymbolDay symbolDay, IReadOnlyList<TRecord> records, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            if(await this.store.ContainsAsync(schema, run.GetMonth(this.store, schema.Name, symbolDay.Day), symbolDay.Symbol, symbolDay.Day).ConfigureAwait(false))
            {
                return false;
            }

            return await this.store.SaveAsync(schema.Format, run.Folder.Dataset, symbolDay.Symbol, symbolDay.Day, records, false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves as empty, as <see cref="EmptyDayPolicy"/> says, the final symbol-days the folder holds no records for and the cache does not
        /// hold; under <see cref="EmptyDayPolicy.CacheWhenNotListed"/> only those the symbology lookup confirms were not listed
        /// </summary>
        private async Task SaveEmptyDaysAsync<TSource, TRecord>(CachedSchema<TSource, TRecord> schema, SymbolDayRun run, BatchCounts counts, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            SymbolListing listing = null;
            bool lookupFailed = false;
            foreach(DateTime day in run.FinalDays.OrderBy(day => day))
            {
                foreach(string symbol in run.Folder.Symbols)
                {
                    SymbolDay symbolDay = new SymbolDay(symbol, day);
                    if(run.Handled.Contains(symbolDay))
                    {
                        continue;
                    }

                    if(await this.store.ContainsAsync(schema, run.GetMonth(this.store, schema.Name, day), symbol, day).ConfigureAwait(false))
                    {
                        ++counts.AlreadyCached;
                        continue;
                    }

                    if(this.options.EmptyDays == EmptyDayPolicy.Never)
                    {
                        this.options.Logger.EmptyDayNotSaved(symbol, day, schema.Name, "EmptyDays is Never");
                        continue;
                    }

                    if(this.options.EmptyDays == EmptyDayPolicy.CacheWhenNotListed)
                    {
                        if(listing == null && !lookupFailed)
                        {
                            try
                            {
                                listing = await this.GetListingAsync(run, cancellationToken).ConfigureAwait(false);
                            }
                            catch(Exception ex) when(IsLookupFailure(ex, cancellationToken))
                            {
                                this.options.Logger.BatchEmptyDaysLookupFailed(run.Folder.Symbols.Count, ex.Message);
                                lookupFailed = true;
                            }
                        }

                        if(listing == null || !listing.IsConfirmedNotListed(symbol, day))
                        {
                            this.options.Logger.EmptyDayNotSaved(symbol, day, schema.Name, lookupFailed ? "the symbology lookup failed" : "the symbology lookup has not confirmed it was not listed");
                            continue;
                        }
                    }

                    if(await this.store.SaveAsync(schema.Format, run.Folder.Dataset, symbol, day, Array.Empty<TRecord>(), false, cancellationToken, this.options.EmptyDays == EmptyDayPolicy.CacheAlways ? "EmptyDays is CacheAlways" : "it was not listed").ConfigureAwait(false))
                    {
                        ++counts.SavedEmpty;
                    }
                    else
                    {
                        ++counts.AlreadyCached;
                    }
                }
            }
        }

        /// <summary>
        /// Looks the folder's symbols up over its final days, once per folder
        /// </summary>
        private async Task<SymbolListing> GetListingAsync(SymbolDayRun run, CancellationToken cancellationToken)
        {
            run.Listing ??= await SymbolListing.ResolveAsync(this.jsonClient, run.Folder.Dataset, run.Folder.Symbols.ToList(), run.FinalDays.Min(), run.FinalDays.Max(), this.options.MaxSymbolsPerRequest, cancellationToken).ConfigureAwait(false);
            return run.Listing;
        }

        /// <summary>
        /// A symbol and a UTC day
        /// </summary>
        private readonly record struct SymbolDay(string Symbol, DateTime Day);

        /// <summary>
        /// What one folder's ingestion keeps as it goes: its final days and symbols, the symbol-days it has handled, the month folders it
        /// has looked in, and the symbology lookup once made
        /// </summary>
        private sealed class SymbolDayRun
        {
            private readonly Dictionary<DateTime, CacheMonthFolder> months = new Dictionary<DateTime, CacheMonthFolder>();

            public SymbolDayRun(BatchFolder folder, List<DateTime> finalDays)
            {
                this.Folder = folder;
                this.FinalDays = new HashSet<DateTime>(finalDays);
                this.Symbols = new HashSet<string>(folder.Symbols, StringComparer.Ordinal);
            }

            public BatchFolder Folder { get; }

            public HashSet<DateTime> FinalDays { get; }

            public HashSet<string> Symbols { get; }

            public HashSet<SymbolDay> Handled { get; } = new HashSet<SymbolDay>();

            public SymbolListing Listing { get; set; }

            public CacheMonthFolder GetMonth(SymbolDayStore store, string schema, DateTime day)
            {
                DateTime month = new DateTime(day.Year, day.Month, 1);
                if(!this.months.TryGetValue(month, out CacheMonthFolder folder))
                {
                    folder = store.GetMonth(this.Folder.Dataset, schema, day);
                    this.months.Add(month, folder);
                }

                return folder;
            }
        }
    }
}
