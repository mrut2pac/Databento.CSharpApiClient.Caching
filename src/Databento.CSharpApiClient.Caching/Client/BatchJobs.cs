using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel.Batch;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Runs Databento batch jobs for the symbol-days a range lacks and saves their files as day files. Only final days are asked for, each
    /// run of consecutive ones apart. A job submitted before whose symbols and range cover a query, and not expired, is downloaded again
    /// rather than paid for twice; the new jobs are priced first and refused together above the caller's ceiling. Runs on one cache root
    /// take turns within the process.
    /// </summary>
    internal sealed class BatchJobs
    {
        private const string AllSymbols = "ALL_SYMBOLS";

        private const string HashPrefix = "sha256:";

        /// <summary>
        /// How long past now a job must stay downloadable to be reused, so its files are not withdrawn while they download
        /// </summary>
        private static readonly TimeSpan ReuseMargin = TimeSpan.FromHours(1);

        /// <summary>
        /// How far back the account's jobs are looked through: Databento keeps a job's files about 30 days
        /// </summary>
        private static readonly TimeSpan ReuseLookBack = TimeSpan.FromDays(31);

        private static readonly string[] LiveStates = { BatchJobStates.Queued, BatchJobStates.Processing, BatchJobStates.Done };

        /// <summary>
        /// One turn per cache root, so two runs never both find nothing to reuse and both pay for the same job
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> RootTurns = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The details of jobs found done, which never change again, by job id
        /// </summary>
        private readonly ConcurrentDictionary<string, BatchJob> doneJobs = new ConcurrentDictionary<string, BatchJob>(StringComparer.Ordinal);

        private readonly CachedDatabentoClientOptions options;

        private readonly DatabentoJsonClient jsonClient;

        private readonly SymbolDayStore store;

        private readonly DatasetAvailability availability;

        private readonly BatchIngestion ingestion;

        private readonly ILogger logger;

        public BatchJobs(CachedDatabentoClientOptions options, DatabentoJsonClient jsonClient, SymbolDayStore store, DatasetAvailability availability, BatchIngestion ingestion)
        {
            this.options = options;
            this.jsonClient = jsonClient;
            this.store = store;
            this.availability = availability;
            this.ingestion = ingestion;
            this.logger = options.Logger;
        }

        public async Task<BatchReport> RunAsync(string dataset, string schemaName, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(dataset);
            ArgumentNullException.ThrowIfNull(symbols);
            if(!CachedSchemas.TryGet(schemaName, out ICachedSchema schema))
            {
                throw new ArgumentException(Invariant($"The cache holds no {schemaName}; it holds {string.Join(", ", CachedSchemas.Names)}."), nameof(schemaName));
            }

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

            if(maxCostUsd <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCostUsd), maxCostUsd, "A batch job needs a cost ceiling above 0.");
            }

            SemaphoreSlim turn = RootTurns.GetOrAdd(Path.TrimEndingDirectorySeparator(Path.GetFullPath(this.options.CacheRoot)), _ => new SemaphoreSlim(1, 1));
            await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await this.RunInTurnAsync(schema, dataset, symbols, startUtc, endUtc, maxCostUsd, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                turn.Release();
            }
        }

        /// <summary>
        /// Saves a batch folder made by any tool as day files
        /// </summary>
        /// <exception cref="InvalidDataException">The folder is not one the cache takes, or holds a schema it does not hold</exception>
        public async Task<BatchReport> IngestFolderAsync(string path, CancellationToken cancellationToken)
        {
            BatchFolder folder = BatchFolder.Read(path);
            if(!CachedSchemas.TryGet(folder.Schema, out ICachedSchema schema))
            {
                throw new InvalidDataException(Invariant($"The batch folder holds {folder.Schema}; the cache holds {string.Join(", ", CachedSchemas.Names)}."));
            }

            BatchCounts counts = await schema.IngestAsync(this.ingestion, folder, cancellationToken).ConfigureAwait(false);
            this.logger.BatchFolderIngested(path, counts.Saved, counts.SavedEmpty, counts.AlreadyCached, counts.DaysLeftOut.Count);
            return ToReport(counts, Array.Empty<string>(), Array.Empty<string>(), 0m);
        }

        /// <summary>
        /// Splits a symbol's days into runs of consecutive days
        /// </summary>
        private static IEnumerable<DaySpan> ToRuns(IEnumerable<DateTime> days)
        {
            DateTime? first = null;
            DateTime last = default;
            foreach(DateTime day in days.Distinct().OrderBy(day => day))
            {
                if(first.HasValue && day == last.AddDays(1))
                {
                    last = day;
                    continue;
                }

                if(first.HasValue)
                {
                    yield return new DaySpan(first.Value, last);
                }

                first = day;
                last = day;
            }

            if(first.HasValue)
            {
                yield return new DaySpan(first.Value, last);
            }
        }

        private async Task<BatchReport> RunInTurnAsync(ICachedSchema schema, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd, CancellationToken cancellationToken)
        {
            BatchCounts counts = new BatchCounts();
            Dictionary<string, List<DateTime>> missing = await this.FindMissingAsync(schema, dataset, symbols.Distinct(StringComparer.Ordinal).ToList(), startUtc, endUtc, counts, cancellationToken).ConfigureAwait(false);
            if(missing.Count == 0)
            {
                this.logger.BatchNothingMissing(schema.Name, dataset);
                return ToReport(counts, Array.Empty<string>(), Array.Empty<string>(), 0m);
            }

            // only final days are asked for: the others are left to the time-series calls once Databento finishes them
            DateTime firstMissing = missing.Values.SelectMany(days => days).Min();
            DateTime lastMissing = missing.Values.SelectMany(days => days).Max();
            DayAvailability dayAvailability = await this.availability.GetAsync(this.jsonClient, dataset, schema.Name, firstMissing, lastMissing, cancellationToken).ConfigureAwait(false);
            foreach(KeyValuePair<string, List<DateTime>> symbol in missing)
            {
                counts.DaysLeftOut.UnionWith(symbol.Value.Where(day => !dayAvailability.IsFinal(day)));
                symbol.Value.RemoveAll(day => !dayAvailability.IsFinal(day));
            }

            if(this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.BatchDaysMissing(missing.Values.Sum(days => days.Count), schema.Name, dataset, counts.AlreadyCached, counts.DaysLeftOut.Count);
            }

            // a run of consecutive days per query, so no cached or unfinished day between two missing ones is bought; symbols in ordinal
            // order, so the same call chunks them the same way again
            List<BatchJobRequest> queries = missing
                .SelectMany(symbol => ToRuns(symbol.Value).Select(run => new KeyValuePair<DaySpan, string>(run, symbol.Key)))
                .GroupBy(pair => pair.Key, pair => pair.Value)
                .OrderBy(span => span.Key.First)
                .SelectMany(span => span.OrderBy(symbol => symbol, StringComparer.Ordinal).Chunk(this.options.MaxSymbolsPerRequest).Select(chunk => CreateRequest(dataset, schema.Name, chunk, span.Key)))
                .ToList();
            if(queries.Count == 0)
            {
                this.logger.BatchNothingMissing(schema.Name, dataset);
                return ToReport(counts, Array.Empty<string>(), Array.Empty<string>(), 0m);
            }

            List<BatchJob> reusable = await this.ListReusableJobsAsync(cancellationToken).ConfigureAwait(false);
            List<string> reusedJobIds = new List<string>();
            List<BatchJobRequest> newQueries = new List<BatchJobRequest>();
            foreach(BatchJobRequest query in queries)
            {
                BatchJob match = reusable.FirstOrDefault(job => Covers(job, query));
                if(match != null)
                {
                    if(!reusedJobIds.Contains(match.JobId, StringComparer.Ordinal))
                    {
                        reusedJobIds.Add(match.JobId);
                        this.logger.BatchJobReused(match.JobId, schema.Name, dataset);
                    }
                }
                else
                {
                    newQueries.Add(query);
                }
            }

            // every new job is priced, and all refused together above the ceiling, before any is submitted
            decimal estimate = 0m;
            List<decimal> estimates = new List<decimal>();
            foreach(BatchJobRequest query in newQueries)
            {
                estimates.Add((decimal)await this.jsonClient.GetCostAsync(query.Dataset, query.Symbols, query.Schema, query.StartUtc, query.EndUtc, cancellationToken).ConfigureAwait(false));
                estimate += estimates[^1];
            }

            if(estimate > maxCostUsd)
            {
                this.logger.BatchJobsTooExpensive(newQueries.Count, estimate, maxCostUsd);
                throw new BatchJobTooExpensiveException(estimate, maxCostUsd);
            }

            if(newQueries.Count > 0)
            {
                this.logger.BatchJobsPriced(newQueries.Count, estimate, maxCostUsd);
            }

            List<string> newJobIds = new List<string>();
            for(int i = 0; i < newQueries.Count; ++i)
            {
                BatchJobRequest query = newQueries[i];
                BatchJob submitted = await this.jsonClient.SubmitBatchJobAsync(query, cancellationToken).ConfigureAwait(false);
                newJobIds.Add(submitted.JobId);
                this.logger.BatchJobSubmitted(submitted.JobId, schema.Name, dataset, query.Symbols, query.StartUtc, query.EndUtc, estimates[i]);
            }

            // every job is downloaded even when one fails, so no paid job is left behind; the failures are thrown together after
            decimal cost = 0m;
            List<Exception> failures = new List<Exception>();
            foreach(string jobId in reusedJobIds.Concat(newJobIds))
            {
                try
                {
                    BatchJob done = await this.WaitAsync(jobId, cancellationToken).ConfigureAwait(false);
                    if(newJobIds.Contains(jobId, StringComparer.Ordinal))
                    {
                        cost += done.CostUsd ?? 0m;
                    }

                    counts.Add(await this.DownloadAndIngestAsync(schema, jobId, cancellationToken).ConfigureAwait(false));
                }
                catch(Exception ex) when(ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    this.logger.BatchJobFailed(jobId, ex);
                    failures.Add(ex);
                }
            }

            if(failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            if(failures.Count > 1)
            {
                throw new AggregateException(Invariant($"{failures.Count} batch jobs could not be saved; the others were. A next call downloads them again at no cost while they last."), failures);
            }

            return ToReport(counts, newJobIds, reusedJobIds, cost);
        }

        private static BatchJobRequest CreateRequest(string dataset, string schema, string[] symbols, DaySpan span)
        {
            return new BatchJobRequest
            {
                Dataset = dataset,
                Symbols = symbols,
                Schema = schema,
                StartUtc = new DateTimeOffset(span.First, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(span.Last.AddDays(1), TimeSpan.Zero),
                Encoding = BatchEncoding.Dbn,
                Compression = BatchCompression.Zstd,
                SplitDuration = BatchSplitDuration.Day,
                SplitSymbols = true,
                MapSymbols = false,
            };
        }

        /// <summary>
        /// Tells whether a job submitted before holds a query's data: the same dataset and schema, raw symbols including the query's, a
        /// range covering its days, no record limit, and files the cache reads (DBN or JSON, any compression or split)
        /// </summary>
        private static bool Covers(BatchJob job, BatchJobRequest query)
        {
            return string.Equals(job.Dataset, query.Dataset, StringComparison.Ordinal)
                && string.Equals(job.Schema, query.Schema, StringComparison.Ordinal)
                && string.Equals(job.StypeIn, "raw_symbol", StringComparison.Ordinal)
                && job.Symbols != null && job.Symbols.ToHashSet(StringComparer.Ordinal).IsSupersetOf(query.Symbols)
                && job.Start <= query.StartUtc && job.End >= query.EndUtc
                && (string.Equals(job.Encoding, "dbn", StringComparison.Ordinal) || string.Equals(job.Encoding, "json", StringComparison.Ordinal))
                && job.Limit == null;
        }

        private static BatchReport ToReport(BatchCounts counts, IReadOnlyList<string> jobIds, IReadOnlyList<string> reusedJobIds, decimal costUsd)
        {
            return new BatchReport(jobIds, reusedJobIds, costUsd, counts.Saved, counts.SavedEmpty, counts.AlreadyCached, counts.DaysLeftOut.ToList());
        }

        private static string ToHex(byte[] hash)
        {
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Finds each symbol's UTC days in the range the cache does not hold, from their day files and archive indexes alone
        /// </summary>
        /// <exception cref="InvalidDataException">An archive file that may hold a day cannot be read: buying its month again is left to the caller</exception>
        private async Task<Dictionary<string, List<DateTime>>> FindMissingAsync(ICachedSchema schema, string dataset, List<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, BatchCounts counts, CancellationToken cancellationToken)
        {
            Dictionary<string, List<DateTime>> missing = new Dictionary<string, List<DateTime>>(StringComparer.Ordinal);
            DateTime last = endUtc.UtcDateTime.AddTicks(-1).Date;
            for(DateTime day = startUtc.UtcDateTime.Date; day <= last; day = day.AddDays(1))
            {
                DateTime utcDay = DateTime.SpecifyKind(day, DateTimeKind.Utc);
                CacheMonthFolder month = this.store.GetMonth(dataset, schema.Name, utcDay);
                foreach(string symbol in symbols)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(await schema.ContainsAsync(this.store, month, symbol, utcDay, true, cancellationToken).ConfigureAwait(false))
                    {
                        ++counts.AlreadyCached;
                        continue;
                    }

                    if(!missing.TryGetValue(symbol, out List<DateTime> days))
                    {
                        days = new List<DateTime>();
                        missing.Add(symbol, days);
                    }

                    days.Add(utcDay);
                }
            }

            return missing;
        }

        /// <summary>
        /// Lists the account's jobs that are queued, processing or done and stay downloadable a while yet, with their queries
        /// </summary>
        private async Task<List<BatchJob>> ListReusableJobsAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset now = this.options.TimeProvider.GetUtcNow();
            BatchJob[] listed = await this.jsonClient.ListBatchJobsAsync(LiveStates, now - ReuseLookBack, cancellationToken).ConfigureAwait(false);
            List<BatchJob> reusable = new List<BatchJob>();
            foreach(BatchJob job in listed)
            {
                // the list carries only each job's id and state: its query comes with its details, which a done job keeps
                if(!this.doneJobs.TryGetValue(job.JobId, out BatchJob details))
                {
                    details = await this.jsonClient.GetBatchJobDetailsAsync(job.JobId, cancellationToken).ConfigureAwait(false);
                    if(string.Equals(details.State, BatchJobStates.Done, StringComparison.Ordinal))
                    {
                        this.doneJobs[job.JobId] = details;
                    }
                }

                if(LiveStates.Contains(details.State, StringComparer.Ordinal) && (details.TsExpiration == null || details.TsExpiration > now + ReuseMargin))
                {
                    reusable.Add(details);
                }
            }

            return reusable;
        }

        /// <summary>
        /// Waits for a job to be done, asking for its state every <see cref="CachedDatabentoClientOptions.BatchPollInterval"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">The job expired before it was downloaded</exception>
        private async Task<BatchJob> WaitAsync(string jobId, CancellationToken cancellationToken)
        {
            while(true)
            {
                BatchJob job = await this.jsonClient.GetBatchJobDetailsAsync(jobId, cancellationToken).ConfigureAwait(false);
                if(string.Equals(job.State, BatchJobStates.Done, StringComparison.Ordinal))
                {
                    this.logger.BatchJobDone(jobId);
                    return job;
                }

                if(!string.Equals(job.State, BatchJobStates.Queued, StringComparison.Ordinal) && !string.Equals(job.State, BatchJobStates.Processing, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(Invariant($"Batch job {jobId} is {job.State}: its files can no longer be downloaded."));
                }

                await Task.Delay(this.options.BatchPollInterval, this.options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Downloads a done job's files into a folder of their own, checks each against its SHA-256, and saves them as day files
        /// </summary>
        /// <exception cref="InvalidDataException">A file does not match its hash, or the folder is not one the cache takes</exception>
        private async Task<BatchCounts> DownloadAndIngestAsync(ICachedSchema schema, string jobId, CancellationToken cancellationToken)
        {
            string folder = Path.Combine(this.options.BatchDownloadFolder ?? Path.Combine(Path.GetTempPath(), "Databento.CSharpApiClient.Caching"), Invariant($"{jobId}_{Guid.NewGuid():N}"));
            Directory.CreateDirectory(folder);
            try
            {
                BatchFile[] files = await this.jsonClient.ListBatchFilesAsync(jobId, cancellationToken).ConfigureAwait(false);
                foreach(BatchFile file in files)
                {
                    string path = Path.Combine(folder, Path.GetFileName(file.Filename));
                    using(IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        Stream download = await this.jsonClient.DownloadBatchFileAsync(file.HttpsUrl, cancellationToken).ConfigureAwait(false);
                        await using(download.ConfigureAwait(false))
                        {
                            FileStream target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                            await using(target.ConfigureAwait(false))
                            {
                                byte[] buffer = new byte[81920];
                                int read;
                                while((read = await download.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                                {
                                    sha256.AppendData(buffer, 0, read);
                                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                                }
                            }
                        }

                        string hash = ToHex(sha256.GetHashAndReset());
                        if(file.Hash != null && file.Hash.StartsWith(HashPrefix, StringComparison.Ordinal) && !string.Equals(file.Hash.Substring(HashPrefix.Length), hash, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(Invariant($"{file.Filename} of batch job {jobId} downloaded with SHA-256 {hash}, where Databento lists {file.Hash}."));
                        }
                    }
                }

                BatchFolder batch = BatchFolder.Read(folder);
                if(!string.Equals(batch.Schema, schema.Name, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(Invariant($"Batch job {jobId} holds {batch.Schema}, not {schema.Name}."));
                }

                BatchCounts counts = await schema.IngestAsync(this.ingestion, batch, cancellationToken).ConfigureAwait(false);
                this.logger.BatchJobIngested(jobId, files.Length, counts.Saved, counts.SavedEmpty, counts.AlreadyCached, counts.DaysLeftOut.Count);
                return counts;
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                {
                    // a temporary folder; the system clears it
                }
            }
        }

        /// <summary>
        /// A run of consecutive UTC days a symbol lacks, first and last
        /// </summary>
        private readonly record struct DaySpan(DateTime First, DateTime Last);
    }
}
