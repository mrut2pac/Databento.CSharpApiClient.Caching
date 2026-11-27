using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

using Databento.CSharpApiClient.Caching.Archive;

using Microsoft.Win32;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Renders the saved step results as a Markdown report: the machine, the data set, a summary, each step's time and memory, the query
    /// latencies and what to keep in mind reading them
    /// </summary>
    internal static class ReportWriter
    {
        private const string NotMeasured = "\u2014";

        public static string Write(BenchmarkSettings settings, SyntheticMarket market, IReadOnlyDictionary<string, StepResult> results)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Databento.CSharpApiClient.Caching benchmark");
            text.AppendLine();
            text.AppendLine("Made by the benchmark app in this repository's `benchmarks/` folder, on synthetic data it generates from `benchmark.json`: no market data is downloaded and Databento is never called. Run it to get this report for your own machine and settings.");
            text.AppendLine();
            text.AppendLine(Invariant($"Machine: {CpuName()}, {Environment.ProcessorCount} logical cores, {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0:N0} GB RAM, {RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}, {(GCSettings.IsServerGC ? "Server" : "Workstation")} GC."));
            text.AppendLine();
            text.AppendLine(Invariant($"Disk: {DiskLine(settings)}; the cache, the backup and the restored copy are all on it."));
            text.AppendLine();
            text.AppendLine(Invariant($"Data: synthetic, {market.TradingDays.Count:N0} trading days from {settings.StartDate:yyyy-MM-dd} to {settings.EndDate:yyyy-MM-dd}, seed {settings.Seed}."));
            text.AppendLine();
            text.AppendLine("| Root | Schema | Contracts a day | Records a contract-day | Symbol-days |");
            text.AppendLine("|---|---|--:|--:|--:|");
            foreach(InstrumentSettings instrument in settings.Instruments)
            {
                text.AppendLine(Invariant($"| {instrument.Root} | {SyntheticMarket.GetDataset(instrument)} {SyntheticMarket.GetSchema(instrument)} | {instrument.ContractsPerDay:N0} | {instrument.RecordsPerDay:N0} | {(long)instrument.ContractsPerDay * market.TradingDays.Count:N0} |"));
            }

            AppendSummary(text, results);
            text.AppendLine();
            text.AppendLine("## Steps");
            text.AppendLine();
            text.AppendLine("*Peak working set* is the process's memory held in RAM, shared pages such as the runtime's included; *peak committed* is the private memory it committed, which Server GC sets well above it, one heap per core.");
            text.AppendLine();
            int stepsTable = text.Length;
            text.AppendLine("| Step | What | Time | Rate | Peak working set | Peak committed | Cores busy |");
            text.AppendLine("|---|---|--:|--:|--:|--:|--:|");
            AppendStep(text, results, "seed", step => Invariant($"{step.Values["DayFilesWritten"]:N0} day files, {step.Values["BytesWritten"] / 1e9:N2} GB"), step => Invariant($"{step.Values["DayFilesPerSecond"]:N0} files/s"));
            AppendStep(text, results, "compact", step => Invariant($"{step.Values["DayFilesAbsorbed"]:N0} day files into {step.Values["FullFilesWritten"]:N0} Parquet files, {step.Values["BytesBefore"] / 1e9:N2} GB to {step.Values["BytesAfter"] / 1e9:N2} GB"), step => Invariant($"{step.Values["DayFilesPerSecond"]:N0} files/s, {step.Values["RowsPerSecond"] / 1e6:N1} M rows/s"));
            AppendStep(text, results, "backup", step => Invariant($"{step.Values["Files"]:N0} files, {step.Values["Bytes"] / 1e9:N2} GB"), step => Invariant($"{step.Values["MBPerSecond"]:N0} MB/s"));
            AppendStep(text, results, "restore", step => Invariant($"{step.Values["Files"]:N0} files, {step.Values["Bytes"] / 1e9:N2} GB"), step => Invariant($"{step.Values["MBPerSecond"]:N0} MB/s"));
            AppendNotMeasuredNote(text, stepsTable);

            text.AppendLine();
            text.AppendLine("## Reading a contract's day");
            text.AppendLine();
            text.AppendLine("One call per symbol-day, `CacheMode.CacheOnly`. *Random* picks symbol-days anywhere in the data; *sequential* reads runs of up to 200 neighboring contracts of one day, calls and puts in strike order, expiration by expiration. *First* is a new client; *repeat* is the same reads again on it. *Allocated a read* is the garbage each read leaves for the collector; *heap kept* is the managed heap after the pass and a full collection, with the client still open.");
            text.AppendLine();
            int queriesTable = text.Length;
            text.AppendLine("| Cache | Order | Readers | Pass | Mean | p50 | p95 | p99 | Reads/s | Records/s | Allocated a read | Heap kept | Peak working set | Peak committed |");
            text.AppendLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach(StepResult step in results.Values.Where(step => step.Name.StartsWith("query-", StringComparison.Ordinal)).OrderBy(step => step.FinishedUtc))
            {
                foreach(QueryPassResult pass in step.Queries)
                {
                    text.AppendLine(Invariant($"| {CacheName(step.Name.Substring(6))} | {pass.Order} | {pass.Readers} | {(pass.Repeat ? "repeat" : "first")} | {pass.MeanMs:N2} ms | {pass.P50Ms:N2} ms | {pass.P95Ms:N2} ms | {pass.P99Ms:N2} ms | {pass.QueriesPerSecond:N0} | {Records(pass.RecordsPerSecond)} | {pass.AllocatedBytesPerRead / 1024:N0} KB | {pass.RetainedHeapMB:N0} MB | {Megabytes(pass.PeakWorkingSetMB)} | {pass.PeakPrivateMemoryMB:N0} MB |"));
                }
            }

            AppendNotMeasuredNote(text, queriesTable);
            AppendCaveats(text, results);
            return text.ToString();
        }

        private static void AppendSummary(StringBuilder text, IReadOnlyDictionary<string, StepResult> results)
        {
            List<(string Cache, QueryPassResult Pass)> passes = new List<(string, QueryPassResult)>();
            foreach(string cache in new[] { "dayfiles", "archive" })
            {
                if(results.TryGetValue("query-" + cache, out StepResult step))
                {
                    passes.AddRange(step.Queries.Where(pass => !pass.Repeat).Select(pass => (cache, pass)));
                }
            }

            if(passes.Count == 0)
            {
                return;
            }

            text.AppendLine();
            text.AppendLine("## Summary");
            text.AppendLine();
            text.AppendLine("Reading one contract's day, first pass on a new client. *Day files* is the cache as downloads leave it; *archive* is after compaction into monthly Parquet files.");
            text.AppendLine();
            int summaryTable = text.Length;
            text.AppendLine("| Cache | Order | Readers | Mean | p99 | Reads/s | Peak working set |");
            text.AppendLine("|---|---|--:|--:|--:|--:|--:|");
            foreach((string cache, QueryPassResult pass) in passes)
            {
                text.AppendLine(Invariant($"| {CacheName(cache)} | {pass.Order} | {pass.Readers} | {pass.MeanMs:N1} ms | {pass.P99Ms:N1} ms | {pass.QueriesPerSecond:N0} | {Megabytes(pass.PeakWorkingSetMB)} |"));
            }

            AppendNotMeasuredNote(text, summaryTable);

            if(results.TryGetValue("query-archive", out StepResult archive) && results.TryGetValue("query-restored", out StepResult restored))
            {
                double[] ratios = restored.Queries
                    .Select(pass => (Restored: pass, Original: archive.Queries.FirstOrDefault(original => original.Order == pass.Order && original.Readers == pass.Readers && original.Repeat == pass.Repeat)))
                    .Where(pair => pair.Original != null && pair.Original.MeanMs > 0)
                    .Select(pair => pair.Restored.MeanMs / pair.Original.MeanMs)
                    .ToArray();
                if(ratios.Length > 0)
                {
                    // the check runs once for each first and repeat pair, over the same reads
                    long checkedReads = restored.Queries.Where(pass => !pass.Repeat).Sum(pass => (long)pass.Queries);
                    string matched = restored.Values.TryGetValue("Mismatches", out double mismatches) ? Invariant($", {mismatches:N0} mismatched") : string.Empty;
                    text.AppendLine();
                    text.AppendLine(Invariant($"After a backup and a restore, the restored archive was read with the same queries: {checkedReads:N0} reads checked against the generated records{matched}, and pass means from {ratios.Min() * 100:N0}% to {ratios.Max() * 100:N0}% of the original archive's."));
                }
            }
        }

        private static void AppendCaveats(StringBuilder text, IReadOnlyDictionary<string, StepResult> results)
        {
            text.AppendLine();
            text.AppendLine("## Reading these numbers");
            text.AppendLine();
            text.AppendLine("- *First* passes are not cold reads: the operating system's file cache keeps what earlier passes and steps read, and a machine with more RAM than the archive serves most of it from memory.");
            if(results.TryGetValue("compact", out StepResult compact) && compact.Values.TryGetValue("BytesAfter", out double bytes) && compact.Values.TryGetValue("RowsWritten", out double rows) && rows > 0)
            {
                text.AppendLine(Invariant($"- The synthetic quotes are random walks, and the archive holds {bytes / rows:N1} bytes a record here. Real quotes compress differently, so an archive of real data has another size for the same records."));
            }

            if(!results.TryGetValue("query-archive", out StepResult archive))
            {
                return;
            }

            QueryPassResult random = FindPass(archive, "random", 1);
            QueryPassResult sequential = FindPass(archive, "sequential", 1);
            if(random != null && sequential != null && random.MeanMs > 5 * sequential.MeanMs)
            {
                string files = compact != null && compact.Values.TryGetValue("FullFilesWritten", out double written) ? Invariant($"; the archive here has {written:N0} full files") : string.Empty;
                text.AppendLine(Invariant($"- Random reads over the archive took {random.MeanMs:N1} ms on average with 1 reader (p99 {random.P99Ms:N0} ms), sequential ones {sequential.MeanMs:N1} ms. The client keeps at most {ArchiveCatalog.MaxCachedVersions} archive versions open with their indexes, one for each dataset, schema, root and month, a full file with its delta{files}. A read beyond them closes the least recently used and opens its file again, parsing its footer, while sequential reads stay in one file and reuse its index."));
            }

            QueryPassResult sequentialMany = archive.Queries.Where(pass => pass.Order == "sequential" && !pass.Repeat && pass.Readers > 1).OrderByDescending(pass => pass.Readers).FirstOrDefault();
            if(sequential != null && sequentialMany != null && sequentialMany.QueriesPerSecond < 1.5 * sequential.QueriesPerSecond)
            {
                text.AppendLine(Invariant($"- {sequentialMany.Readers} readers read the archive sequentially at {sequentialMany.QueriesPerSecond:N0} reads a second, against {sequential.QueriesPerSecond:N0} for 1: reads of one archive file take turns on its single open reader. Each reader count reads its own set of symbol-days, picked from the same seed."));
            }
        }

        private static QueryPassResult FindPass(StepResult step, string order, int readers)
        {
            return step.Queries.FirstOrDefault(pass => pass.Order == order && pass.Readers == readers && !pass.Repeat);
        }

        private static void AppendNotMeasuredNote(StringBuilder text, int tableStart)
        {
            if(text.ToString(tableStart, text.Length - tableStart).Contains(NotMeasured, StringComparison.Ordinal))
            {
                text.AppendLine();
                text.AppendLine(Invariant($"\\* {NotMeasured} is a value the run behind this report did not measure; running the tool again measures it."));
            }
        }

        private static string CacheName(string cache)
        {
            return cache == "dayfiles" ? "day files" : cache;
        }

        private static string Records(double perSecond)
        {
            return perSecond >= 1e6 ? Invariant($"{perSecond / 1e6:N1} M") : Invariant($"{perSecond / 1e3:N0} K");
        }

        private static string Megabytes(double megabytes)
        {
            return megabytes > 0 ? Invariant($"{megabytes:N0} MB") : NotMeasured;
        }

        private static string Gigabytes(double megabytes)
        {
            return megabytes > 0 ? Invariant($"{megabytes / 1024.0:N1} GB") : NotMeasured;
        }

        /// <summary>
        /// Describes the disk the work folder is on: the configured description, and the volume it is mounted as, its format and size
        /// </summary>
        private static string DiskLine(BenchmarkSettings settings)
        {
            string volume = DescribeVolume(Path.GetFullPath(settings.WorkFolder));
            string described = string.IsNullOrWhiteSpace(settings.Disk) ? null : settings.Disk.Trim();
            return described != null && volume != null ? Invariant($"{described}, {volume}") : described ?? volume ?? "not known";
        }

        /// <summary>
        /// Describes the volume a path is on: the mount whose root is the longest one the path starts with, as a Linux path's root is
        /// always <c>/</c>; <c>null</c> when none can be read, as for a network share
        /// </summary>
        private static string DescribeVolume(string path)
        {
            try
            {
                StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                DriveInfo drive = DriveInfo.GetDrives()
                    .Where(candidate => path.StartsWith(candidate.RootDirectory.FullName, comparison))
                    .OrderByDescending(candidate => candidate.RootDirectory.FullName.Length)
                    .FirstOrDefault();
                if(drive == null || !drive.IsReady)
                {
                    return null;
                }

                return Invariant($"{drive.RootDirectory.FullName} ({drive.DriveFormat}, {drive.TotalSize / 1e9:N0} GB)");
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return null;
            }
        }

        private static void AppendStep(StringBuilder text, IReadOnlyDictionary<string, StepResult> results, string name, Func<StepResult, string> what, Func<StepResult, string> rate)
        {
            if(results.TryGetValue(name, out StepResult step))
            {
                TimeSpan time = TimeSpan.FromSeconds(step.Resources.ElapsedSeconds);
                text.AppendLine(Invariant($"| {name} | {what(step)} | {(time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : time.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture))} | {rate(step)} | {Gigabytes(step.Resources.PeakWorkingSetMB)} | {step.Resources.PeakPrivateMemoryMB / 1024.0:N1} GB | {step.Resources.AverageCoresBusy:N1} |"));
            }
        }

        private static string CpuName()
        {
            if(OperatingSystem.IsWindows())
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if(key?.GetValue("ProcessorNameString") is string name)
                {
                    return name.Trim();
                }
            }

            if(OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                string model = File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal));
                if(model != null && model.Contains(':', StringComparison.Ordinal))
                {
                    return model.Substring(model.IndexOf(':', StringComparison.Ordinal) + 1).Trim();
                }
            }

            return RuntimeInformation.ProcessArchitecture.ToString();
        }
    }
}
