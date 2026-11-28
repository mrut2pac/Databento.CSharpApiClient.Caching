using System;
using System.Diagnostics;
using System.Globalization;
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
        private const string Usage = "Usage: Databento.CSharpApiClient.Caching.Benchmarks <seed|query|compact|backup|restore|verify|unpack|unpacked|report|all> [--settings benchmark.json] [--run <name>] [--clean]";

        /// <summary>
        /// Name of a run started without <c>--run</c>: its UTC start time, so runs sort in the order they started
        /// </summary>
        private const string RunNameFormat = "yyyyMMdd'T'HHmmss";

        public static async Task<int> Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine(Usage);
                return 1;
            }

            int settingsAt = Array.IndexOf(args, "--settings");
            string settingsPath = settingsAt >= 0 && settingsAt + 1 < args.Length ? args[settingsAt + 1] : Path.Combine(AppContext.BaseDirectory, "benchmark.json");
            string command = args[0].ToLowerInvariant();
            int runAt = Array.IndexOf(args, "--run");
            BenchmarkSettings settings;
            try
            {
                settings = BenchmarkSettings.Read(settingsPath);
                settings.RunName = ChooseRun(settings, command, runAt >= 0 && runAt + 1 < args.Length ? args[runAt + 1] : null);
                if(command == "all")
                {
                    PrepareDataFolders(settings, args.Contains("--clean"));
                }
            }
            catch(InvalidDataException ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }

            Console.WriteLine(Invariant($"Run {settings.RunName}: results in {settings.ResultsFolder}"));
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

        /// <summary>
        /// Chooses the run a command's results go to: the one named, else a new one for <c>all</c> and <c>seed</c>, which start a benchmark,
        /// else the latest run by name, so steps run one at a time add to the run they continue
        /// </summary>
        /// <exception cref="InvalidDataException">The name is no plain folder name, or a step continues a run when there is none</exception>
        private static string ChooseRun(BenchmarkSettings settings, string command, string named)
        {
            if(named != null)
            {
                if(named.Length == 0 || named.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || named == "." || named == "..")
                {
                    throw new InvalidDataException(Invariant($"--run {named} is no folder name."));
                }

                return named;
            }

            if(command == "all" || command == "seed")
            {
                return DateTime.UtcNow.ToString(RunNameFormat, CultureInfo.InvariantCulture);
            }

            string latest = Directory.Exists(settings.ResultsRoot)
                ? Directory.EnumerateDirectories(settings.ResultsRoot).Select(Path.GetFileName).Order(StringComparer.Ordinal).LastOrDefault()
                : null;
            return latest ?? throw new InvalidDataException(Invariant($"{settings.ResultsRoot} holds no run for {command} to continue: start one with seed or all, or name one with --run."));
        }

        /// <summary>
        /// Makes sure <c>all</c> starts on no data of an earlier run: the cache, backups, staging and restored copy it writes are deleted
        /// with <c>--clean</c>, and refused without it. Earlier runs' results are kept.
        /// </summary>
        /// <exception cref="InvalidDataException">A data folder holds files and <c>--clean</c> is not given</exception>
        private static void PrepareDataFolders(BenchmarkSettings settings, bool clean)
        {
            string[] used = new[] { settings.CacheRoot, settings.BackupFolder, settings.StagingFolder, settings.RestoredRoot }
                .Where(folder => Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                .ToArray();
            if(used.Length == 0)
            {
                return;
            }

            if(!clean)
            {
                throw new InvalidDataException(Invariant($"{string.Join(", ", used)} hold an earlier run's data: pass --clean to delete them (results are kept), or use another WorkFolder."));
            }

            foreach(string folder in used)
            {
                Console.WriteLine(Invariant($"Deleting {folder}, an earlier run's data"));
                Directory.Delete(folder, recursive: true);
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
                    string path = Path.Combine(settings.ResultsFolder, "report.md");
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
