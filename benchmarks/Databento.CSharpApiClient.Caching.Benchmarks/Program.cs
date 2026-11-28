using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Runs the benchmark steps on synthetic data: <c>seed</c>, <c>query</c>, <c>compact</c>, <c>query</c> again, <c>backup</c>,
    /// <c>restore</c>, <c>verify</c>, <c>unpack</c>, <c>unpacked</c> and <c>report</c>, one per call or all of them with <c>all</c>. No step
    /// calls Databento.
    /// </summary>
    internal static class Program
    {
        private const string Usage = "Usage: Databento.CSharpApiClient.Caching.Benchmarks <seed|query|compact|backup|restore|verify|unpack|unpacked|report|all> [--settings benchmark.json]";

        public static async Task<int> Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine(Usage);
                return 1;
            }

            int settingsAt = Array.IndexOf(args, "--settings");
            string settingsPath = settingsAt >= 0 && settingsAt + 1 < args.Length ? args[settingsAt + 1] : Path.Combine(AppContext.BaseDirectory, "benchmark.json");
            BenchmarkSettings settings = BenchmarkSettings.Read(settingsPath);
            SyntheticMarket market = new SyntheticMarket(settings);

            // a benchmark should not take the machine from its owner
            using(Process current = Process.GetCurrentProcess())
            {
                current.PriorityClass = ProcessPriorityClass.BelowNormal;
            }

            using CancellationTokenSource cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancel.Cancel();
            };

            try
            {
                string command = args[0].ToLowerInvariant();
                string[] steps = command == "all" ? new[] { "seed", "query", "compact", "query", "backup", "restore", "verify", "unpack", "unpacked", "report" } : new[] { command };
                foreach(string step in steps)
                {
                    await RunStepAsync(step, settings, market, cancel.Token).ConfigureAwait(false);
                }

                return 0;
            }
            catch(OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 2;
            }
        }

        private static async Task RunStepAsync(string step, BenchmarkSettings settings, SyntheticMarket market, CancellationToken cancellationToken)
        {
            switch(step)
            {
                case "seed":
                    await SeedStep.RunAsync(settings, market, cancellationToken).ConfigureAwait(false);
                    break;
                case "query":
                    bool archived = Directory.Exists(settings.CacheRoot) && Directory.EnumerateFiles(settings.CacheRoot, "*.parquet", SearchOption.AllDirectories).Any();
                    await QueryStep.RunAsync(settings, market, settings.CacheRoot, archived ? "archive" : "dayfiles", cancellationToken).ConfigureAwait(false);
                    break;
                case "compact":
                    await MaintenanceSteps.CompactAsync(settings, cancellationToken).ConfigureAwait(false);
                    break;
                case "backup":
                    await MaintenanceSteps.BackUpAsync(settings, cancellationToken).ConfigureAwait(false);
                    break;
                case "restore":
                    await MaintenanceSteps.RestoreAsync(settings, cancellationToken).ConfigureAwait(false);
                    break;
                case "verify":
                    await QueryStep.RunAsync(settings, market, settings.RestoredRoot, "restored", cancellationToken).ConfigureAwait(false);
                    break;
                case "unpack":
                    await MaintenanceSteps.UnpackAsync(settings, cancellationToken).ConfigureAwait(false);
                    break;
                case "unpacked":
                    await QueryStep.RunAsync(settings, market, settings.RestoredRoot, "unpacked", cancellationToken).ConfigureAwait(false);
                    break;
                case "report":
                    string report = ReportWriter.Write(settings, market, StepResult.ReadAll(settings.ResultsFolder));
                    string path = Path.Combine(settings.WorkFolder, "report.md");
                    File.WriteAllText(path, report);
                    Console.WriteLine(report);
                    Console.WriteLine(Invariant($"Written to {path}"));
                    break;
                default:
                    throw new ArgumentException(Usage);
            }
        }
    }
}
