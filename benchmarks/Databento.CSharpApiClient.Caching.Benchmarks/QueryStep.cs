using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Reads whole symbol-days through the cached client in <see cref="CacheMode.CacheOnly"/>, as a caller asks for a contract's day, in
    /// random order and in runs of neighboring contracts, and times each read. Every field of every record read is checked against the
    /// records the generator makes for it, so a fast wrong answer cannot pass.
    /// </summary>
    internal static class QueryStep
    {
        /// <summary>
        /// The longest run of consecutive contracts a sequential pass reads, a batch download's worth
        /// </summary>
        private const int SequentialRunLength = 200;

        private static readonly PropertyInfo[] CbboFields = RecordFields(typeof(ICbboRecord));

        private static readonly PropertyInfo[] BboFields = RecordFields(typeof(IBboRecord));

        /// <summary>
        /// The first mismatch a check found, for the step's output, or <c>null</c>
        /// </summary>
        private static string firstMismatch;

        public static async Task RunAsync(BenchmarkSettings settings, SyntheticMarket market, string cacheRoot, string label, CancellationToken cancellationToken)
        {
            Console.WriteLine(Invariant($"Querying {cacheRoot} ({label})"));
            using ResourceSampler sampler = new ResourceSampler();
            StepResult result = new StepResult { Name = "query-" + label };
            long mismatches = 0;
            foreach(int readers in settings.QueryParallelism)
            {
                foreach(string order in new[] { "random", "sequential" })
                {
                    int seed = settings.Seed + (readers * 7919) + (order == "random" ? 0 : 1);
                    List<(SyntheticContract Contract, DateTime Day)> queries = order == "random"
                        ? PickRandom(settings, market, settings.RandomQueries, seed)
                        : PickSequential(settings, market, settings.SequentialQueries, seed);
                    using CachedDatabentoClient client = OpenClient(cacheRoot);
                    foreach(bool repeat in new[] { false, true })
                    {
                        (QueryPassResult pass, _) = await RunPassAsync(client, market, queries, readers, false, cancellationToken).ConfigureAwait(false);
                        pass.Order = order;
                        pass.Repeat = repeat;
                        result.Queries.Add(pass);
                        Console.WriteLine(Invariant($"  {order,-10} readers {readers,2} {(repeat ? "repeat" : "first "),-6}: mean {pass.MeanMs,7:N2} ms, p50 {pass.P50Ms,7:N2}, p95 {pass.P95Ms,7:N2}, p99 {pass.P99Ms,7:N2}, {pass.QueriesPerSecond,8:N0} reads/s, {pass.RecordsPerSecond / 1e6,6:N2} M records/s, {pass.AllocatedBytesPerRead / 1024,6:N0} KB allocated a read, {pass.RetainedHeapMB,5:N0} MB heap kept, peak {pass.PeakWorkingSetMB,5:N0} MB working set, {pass.PeakPrivateMemoryMB,5:N0} MB committed"));
                    }

                    // the check regenerates every record read, so it runs as a pass of its own, outside the timing
                    (_, long passMismatches) = await RunPassAsync(client, market, queries, readers, true, cancellationToken).ConfigureAwait(false);
                    mismatches += passMismatches;
                }
            }

            result.Resources = sampler.Stop();
            result.Values["Mismatches"] = mismatches;
            Console.WriteLine(Invariant($"  every read checked against the generated records: {mismatches} mismatched{(firstMismatch == null ? string.Empty : "; the first: " + firstMismatch)}"));
            result.Save(settings.ResultsFolder);
            if(mismatches > 0)
            {
                throw new InvalidDataException(Invariant($"{mismatches} reads did not return the records generated for them."));
            }
        }

        /// <summary>
        /// Opens a cached client that can only read the cache: <see cref="CacheMode.CacheOnly"/> never reaches Databento, so the key is a placeholder
        /// </summary>
        private static CachedDatabentoClient OpenClient(string cacheRoot)
        {
            return new CachedDatabentoClient(new CachedDatabentoClientOptions
            {
                CacheRoot = cacheRoot,
                Databento = new DatabentoOptions
                {
                    ApiKey = "db-benchmark-never-sent",
                },
            });
        }

        private static async Task<(QueryPassResult Pass, long Mismatches)> RunPassAsync(CachedDatabentoClient client, SyntheticMarket market, List<(SyntheticContract Contract, DateTime Day)> queries, int readers, bool check, CancellationToken cancellationToken)
        {
            double[] latencies = new double[queries.Count];
            long records = 0;
            long mismatches = 0;
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            using ResourceSampler sampler = new ResourceSampler();
            Stopwatch wall = Stopwatch.StartNew();
            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = readers, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(Enumerable.Range(0, queries.Count), options, async (index, ct) =>
            {
                (SyntheticContract contract, DateTime day) = queries[index];
                string dataset = SyntheticMarket.GetDataset(contract.Instrument);
                string[] symbols = { contract.Symbol };
                DateTimeOffset start = new DateTimeOffset(day, TimeSpan.Zero);
                long started = Stopwatch.GetTimestamp();
                IRecord[] read = contract.Instrument.Kind == InstrumentKind.Option
                    ? await client.GetCbbo1mAsync(dataset, symbols, start, start.AddDays(1), CacheMode.CacheOnly, ct).ConfigureAwait(false)
                    : await client.GetBbo1mAsync(dataset, symbols, start, start.AddDays(1), CacheMode.CacheOnly, ct).ConfigureAwait(false);
                latencies[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Interlocked.Add(ref records, read.Length);
                if(check && !Matches(read, market.CreateRecords(contract, day), contract.Symbol))
                {
                    Interlocked.Increment(ref mismatches);
                }
            }).ConfigureAwait(false);
            wall.Stop();
            double allocatedPerRead = (GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore) / (double)queries.Count;
            ResourceUse use = sampler.Stop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            double retainedHeapMB = GC.GetTotalMemory(forceFullCollection: true) / 1024.0 / 1024.0;

            Array.Sort(latencies);
            QueryPassResult pass = new QueryPassResult
            {
                Readers = readers,
                Queries = queries.Count,
                Records = records,
                MeanMs = latencies.Average(),
                P50Ms = Percentile(latencies, 0.50),
                P95Ms = Percentile(latencies, 0.95),
                P99Ms = Percentile(latencies, 0.99),
                QueriesPerSecond = queries.Count / wall.Elapsed.TotalSeconds,
                RecordsPerSecond = records / wall.Elapsed.TotalSeconds,
                AllocatedBytesPerRead = allocatedPerRead,
                RetainedHeapMB = retainedHeapMB,
                PeakPrivateMemoryMB = use.PeakPrivateMemoryMB,
                PeakWorkingSetMB = use.PeakWorkingSetMB,
            };
            return (pass, mismatches);
        }

        /// <summary>
        /// Checks the records read are the ones generated, field by field: every property of the record's interface but the symbol, which
        /// must be the one asked for or none, as a day file of a download without <c>map_symbols</c> holds none and the archive sets it
        /// </summary>
        private static bool Matches(IRecord[] read, object[] generated, string symbol)
        {
            if(read.Length != generated.Length)
            {
                return Mismatch(Invariant($"{symbol}: {read.Length} records read, {generated.Length} generated"));
            }

            for(int index = 0; index < read.Length; ++index)
            {
                PropertyInfo[] fields = read[index] switch
                {
                    ICbboRecord when generated[index] is ICbboRecord => CbboFields,
                    IBboRecord when generated[index] is IBboRecord => BboFields,
                    _ => null,
                };
                if(fields == null || (read[index].Symbol != null && !string.Equals(read[index].Symbol, symbol, StringComparison.Ordinal)))
                {
                    return Mismatch(Invariant($"{symbol}: record {index} is a {read[index].GetType().Name} for {read[index].Symbol}"));
                }

                foreach(PropertyInfo field in fields)
                {
                    object readValue = field.GetValue(read[index]);
                    object generatedValue = field.GetValue(generated[index]);
                    if(!Equals(readValue, generatedValue))
                    {
                        return Mismatch(Invariant($"{symbol}: record {index} {field.Name} read {readValue ?? "null"}, generated {generatedValue ?? "null"}"));
                    }
                }
            }

            return true;
        }

        private static bool Mismatch(string description)
        {
            Interlocked.CompareExchange(ref firstMismatch, description, null);
            return false;
        }

        private static PropertyInfo[] RecordFields(Type recordInterface)
        {
            return recordInterface.GetProperties()
                .Concat(recordInterface.GetInterfaces().SelectMany(baseInterface => baseInterface.GetProperties()))
                .Where(property => property.Name != nameof(IRecord.Symbol))
                .ToArray();
        }

        private static double Percentile(double[] sorted, double fraction)
        {
            return sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(fraction * sorted.Length) - 1)];
        }

        private static List<(SyntheticContract Contract, DateTime Day)> PickRandom(BenchmarkSettings settings, SyntheticMarket market, int count, int seed)
        {
            Random random = new Random(seed);
            List<(SyntheticContract Contract, DateTime Day)> queries = new List<(SyntheticContract Contract, DateTime Day)>(count);
            while(queries.Count < count)
            {
                (InstrumentSettings instrument, DateTime day) = PickInstrumentDay(settings, market, random);
                IReadOnlyList<SyntheticContract> contracts = ListSomeContracts(market, instrument, day);
                queries.Add((contracts[random.Next(contracts.Count)], day));
            }

            return queries;
        }

        private static List<(SyntheticContract Contract, DateTime Day)> PickSequential(BenchmarkSettings settings, SyntheticMarket market, int count, int seed)
        {
            Random random = new Random(seed);
            List<(SyntheticContract Contract, DateTime Day)> queries = new List<(SyntheticContract Contract, DateTime Day)>(count);
            while(queries.Count < count)
            {
                (InstrumentSettings instrument, DateTime day) = PickInstrumentDay(settings, market, random);
                IReadOnlyList<SyntheticContract> contracts = ListSomeContracts(market, instrument, day);
                int first = random.Next(contracts.Count);
                int length = Math.Min(Math.Min(SequentialRunLength, count - queries.Count), contracts.Count - first);
                queries.AddRange(contracts.Skip(first).Take(length).Select(contract => (contract, day)));
            }

            return queries;
        }

        /// <summary>
        /// Lists the contracts of an instrument's day, which a query can only pick from if there is one
        /// </summary>
        /// <exception cref="InvalidDataException">The instrument lists no contract that day: every strike is zero or below</exception>
        private static IReadOnlyList<SyntheticContract> ListSomeContracts(SyntheticMarket market, InstrumentSettings instrument, DateTime day)
        {
            IReadOnlyList<SyntheticContract> contracts = market.ListContracts(instrument, day);
            if(contracts.Count == 0)
            {
                throw new InvalidDataException(Invariant($"{instrument.Root} lists no contract on {day:yyyy-MM-dd}: every strike is zero or below. Raise its Price or lower StrikeStep or StrikesPerSide."));
            }

            return contracts;
        }

        /// <summary>
        /// Picks a trading day and an instrument, weighted by the contracts each lists, so every symbol-day is as likely as any other
        /// </summary>
        private static (InstrumentSettings Instrument, DateTime Day) PickInstrumentDay(BenchmarkSettings settings, SyntheticMarket market, Random random)
        {
            DateTime day = market.TradingDays[random.Next(market.TradingDays.Count)];
            int pick = random.Next(settings.Instruments.Sum(instrument => instrument.ContractsPerDay));
            foreach(InstrumentSettings instrument in settings.Instruments)
            {
                if(pick < instrument.ContractsPerDay)
                {
                    return (instrument, day);
                }

                pick -= instrument.ContractsPerDay;
            }

            return (settings.Instruments[^1], day);
        }
    }
}
