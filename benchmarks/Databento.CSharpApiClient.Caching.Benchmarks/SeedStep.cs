using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel.Json;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Writes a day file for every generated contract-day, the way the cache saves a download, so the cache starts as one-off days only.
    /// A day file already there is kept, so an interrupted seed picks up where it stopped.
    /// </summary>
    internal static class SeedStep
    {
        public static async Task RunAsync(BenchmarkSettings settings, SyntheticMarket market, CancellationToken cancellationToken)
        {
            if(Directory.Exists(settings.CacheRoot) && Directory.EnumerateFiles(settings.CacheRoot, "*.parquet", SearchOption.AllDirectories).Any())
            {
                // day files written over an archive would be served first, and the next query would time them as the archive
                throw new InvalidOperationException(Invariant($"{settings.CacheRoot} is already compacted: seed into a new WorkFolder."));
            }

            CacheMetadata.EnsureCreated(settings.CacheRoot);
            long planned = settings.Instruments.Sum(instrument => (long)instrument.ContractsPerDay) * market.TradingDays.Count;
            Console.WriteLine(Invariant($"Seeding {planned:N0} day files for {market.TradingDays.Count} trading days into {settings.CacheRoot}"));

            long written = 0;
            long kept = 0;
            long records = 0;
            long bytes = 0;
            using ResourceSampler sampler = new ResourceSampler();
            using Timer progress = new Timer(
                _ => ProgressLine.Report("seed", Interlocked.Read(ref written) + Interlocked.Read(ref kept), planned, sampler),
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));

            IEnumerable<(InstrumentSettings Instrument, DateTime Day)> instrumentDays =
                from day in market.TradingDays
                from instrument in settings.Instruments
                select (instrument, day);
            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = settings.Parallelism, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(instrumentDays, options, async (instrumentDay, ct) =>
            {
                string dataset = SyntheticMarket.GetDataset(instrumentDay.Instrument);
                string schema = SyntheticMarket.GetSchema(instrumentDay.Instrument);
                string folder = DayFileName.GetMonthDirectory(settings.CacheRoot, dataset, schema, instrumentDay.Day.Year, instrumentDay.Day.Month);
                foreach(SyntheticContract contract in market.ListContracts(instrumentDay.Instrument, instrumentDay.Day))
                {
                    string path = Path.Combine(folder, DayFileName.Format(contract.Symbol, instrumentDay.Day, CacheMetadata.UtcCutoff));
                    if(File.Exists(path))
                    {
                        Interlocked.Increment(ref kept);
                        continue;
                    }

                    object[] dayRecords = market.CreateRecords(contract, instrumentDay.Day);
                    bool saved = instrumentDay.Instrument.Kind == InstrumentKind.Option
                        ? await DayFile.WriteAsync(path, Array.ConvertAll(dayRecords, record => (CbboRecordJson)record), false, ct).ConfigureAwait(false)
                        : await DayFile.WriteAsync(path, Array.ConvertAll(dayRecords, record => (BboRecordJson)record), false, ct).ConfigureAwait(false);
                    if(saved)
                    {
                        Interlocked.Increment(ref written);
                        Interlocked.Add(ref records, dayRecords.Length);
                        Interlocked.Add(ref bytes, new FileInfo(path).Length);
                    }
                    else
                    {
                        Interlocked.Increment(ref kept);
                    }
                }
            }).ConfigureAwait(false);

            ResourceUse use = sampler.Stop();
            StepResult result = new StepResult { Name = "seed", Resources = use };
            result.Values["DayFilesPlanned"] = planned;
            result.Values["DayFilesWritten"] = written;
            result.Values["DayFilesKept"] = kept;
            result.Values["RecordsWritten"] = records;
            result.Values["BytesWritten"] = bytes;
            result.Values["TradingDays"] = market.TradingDays.Count;
            result.Values["DayFilesPerSecond"] = written / Math.Max(0.001, use.ElapsedSeconds);
            result.Save(settings.ResultsFolder);
            Console.WriteLine(Invariant($"Seed done: {written:N0} day files written, {kept:N0} already there, {records:N0} records, {bytes / 1024.0 / 1024.0 / 1024.0:N2} GB in {use.ElapsedSeconds:N0} s ({result.Values["DayFilesPerSecond"]:N0} files/s), peak {use.PeakWorkingSetMB:N0} MB working set, {use.PeakPrivateMemoryMB:N0} MB committed"));
        }
    }
}
