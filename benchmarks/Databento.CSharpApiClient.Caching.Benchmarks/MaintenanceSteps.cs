using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// The compaction, backup and restore steps, each through the package's public API with its defaults except where the settings say
    /// </summary>
    internal static class MaintenanceSteps
    {
        public static async Task CompactAsync(BenchmarkSettings settings, CancellationToken cancellationToken)
        {
            Console.WriteLine(Invariant($"Compacting {settings.CacheRoot} with MaxParallelGroups {settings.MaxParallelGroups}"));
            long bytesBefore = FolderBytes(settings.CacheRoot);
            using ResourceSampler sampler = new ResourceSampler();
            ArchiveCompactionProgress last = null;
            using Timer progress = new Timer(
                _ =>
                {
                    ArchiveCompactionProgress seen = Volatile.Read(ref last);
                    if(seen != null)
                    {
                        ProgressLine.Report("compact", seen.DayFilesDone, seen.PlannedDayFiles, sampler);
                    }
                },
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
            DatabentoArchiveCompactor compactor = new DatabentoArchiveCompactor(new ArchiveCompactorOptions
            {
                CacheRoot = settings.CacheRoot,
                MaxParallelGroups = settings.MaxParallelGroups,
                Progress = new SynchronousProgress<ArchiveCompactionProgress>(report => Volatile.Write(ref last, report)),
            });
            ArchiveCompactionReport report = await compactor.CompactAsync(cancellationToken).ConfigureAwait(false);

            ResourceUse use = sampler.Stop();
            StepResult result = new StepResult { Name = "compact", Resources = use };
            result.Values["GroupsCompacted"] = report.GroupsCompacted;
            result.Values["FullFilesWritten"] = report.FullFilesWritten;
            result.Values["DayFilesAbsorbed"] = report.DayFilesAbsorbed;
            result.Values["SymbolDaysWritten"] = report.SymbolDaysWritten;
            result.Values["RowsWritten"] = report.RowsWritten;
            result.Values["BytesBefore"] = bytesBefore;
            result.Values["BytesAfter"] = FolderBytes(settings.CacheRoot);
            result.Values["FailedGroups"] = report.FailedGroups.Count;
            result.Values["DayFilesPerSecond"] = report.DayFilesAbsorbed / Math.Max(0.001, use.ElapsedSeconds);
            result.Values["RowsPerSecond"] = report.RowsWritten / Math.Max(0.001, use.ElapsedSeconds);
            result.Save(settings.ResultsFolder);
            Console.WriteLine(Invariant($"Compaction done: {report.DayFilesAbsorbed:N0} day files into {report.FullFilesWritten:N0} archive files, {report.RowsWritten:N0} rows, {bytesBefore / 1e9:N2} GB to {result.Values["BytesAfter"] / 1e9:N2} GB in {use.ElapsedSeconds:N0} s, peak {use.PeakWorkingSetMB:N0} MB working set, {use.PeakPrivateMemoryMB:N0} MB committed, {use.AverageCoresBusy:N1} cores, GC paused {use.GcPausePercent:N1}%"));
            if(report.FailedGroups.Count > 0)
            {
                throw new InvalidDataException(Invariant($"{report.FailedGroups.Count} groups failed to compact; the first: {report.FailedGroups[0]}"));
            }
        }

        public static async Task BackUpAsync(BenchmarkSettings settings, CancellationToken cancellationToken)
        {
            Console.WriteLine(Invariant($"Backing up {settings.CacheRoot} to {settings.BackupFolder}"));
            using ResourceSampler sampler = new ResourceSampler();
            DatabentoCacheBackup backup = new DatabentoCacheBackup(new CacheBackupOptions
            {
                CacheRoot = settings.CacheRoot,
                Store = new LocalDirectoryBackupStore(settings.BackupFolder),
                StagingFolder = settings.StagingFolder,
                Retention = BackupRetention.KeepLast(1),
            });
            CacheBackupReport report = await backup.BackUpAsync(force: true, cancellationToken).ConfigureAwait(false);

            ResourceUse use = sampler.Stop();
            StepResult result = new StepResult { Name = "backup", Resources = use };
            result.Values["Files"] = report.FileCount;
            result.Values["Bytes"] = report.TotalBytes;
            result.Values["BackupBytes"] = FolderBytes(settings.BackupFolder);
            result.Values["MBPerSecond"] = report.TotalBytes / 1024.0 / 1024.0 / Math.Max(0.001, use.ElapsedSeconds);
            result.Save(settings.ResultsFolder);
            File.WriteAllText(Path.Combine(settings.ResultsFolder, "backup-name.txt"), report.Name);
            Console.WriteLine(Invariant($"Backup {report.Name} done: {report.FileCount:N0} files, {report.TotalBytes / 1e9:N2} GB in {use.ElapsedSeconds:N0} s ({result.Values["MBPerSecond"]:N0} MB/s), peak {use.PeakWorkingSetMB:N0} MB working set, {use.PeakPrivateMemoryMB:N0} MB committed"));
        }

        public static async Task RestoreAsync(BenchmarkSettings settings, CancellationToken cancellationToken)
        {
            string backupName = File.ReadAllText(Path.Combine(settings.ResultsFolder, "backup-name.txt")).Trim();
            if(Directory.Exists(settings.RestoredRoot) && Directory.EnumerateFileSystemEntries(settings.RestoredRoot).Any())
            {
                throw new IOException(Invariant($"{settings.RestoredRoot} is not empty: delete it before a restore, so the restore is timed into an empty folder."));
            }

            Console.WriteLine(Invariant($"Restoring {backupName} into {settings.RestoredRoot}"));
            using ResourceSampler sampler = new ResourceSampler();
            DatabentoCacheRestore restore = new DatabentoCacheRestore(new CacheRestoreOptions
            {
                CacheRoot = settings.RestoredRoot,
                Store = new LocalDirectoryBackupStore(settings.BackupFolder),
                StagingFolder = settings.StagingFolder,
                InUseChecker = new NotInUseChecker(),
            });
            CacheRestoreReport report = await restore.RestoreAsync(backupName, cancellationToken).ConfigureAwait(false);

            ResourceUse use = sampler.Stop();
            StepResult result = new StepResult { Name = "restore", Resources = use };
            result.Values["Files"] = report.FileCount;
            result.Values["Bytes"] = report.TotalBytes;
            result.Values["MBPerSecond"] = report.TotalBytes / 1024.0 / 1024.0 / Math.Max(0.001, use.ElapsedSeconds);
            result.Save(settings.ResultsFolder);
            Console.WriteLine(Invariant($"Restore done: {report.FileCount:N0} files, {report.TotalBytes / 1e9:N2} GB in {use.ElapsedSeconds:N0} s ({result.Values["MBPerSecond"]:N0} MB/s), peak {use.PeakWorkingSetMB:N0} MB working set, {use.PeakPrivateMemoryMB:N0} MB committed"));
        }

        private static long FolderBytes(string folder)
        {
            return Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) : 0;
        }

        /// <summary>
        /// Nothing else uses the benchmark's restored cache
        /// </summary>
        private sealed class NotInUseChecker : ICacheInUseChecker
        {
            public bool IsInUse()
            {
                return false;
            }
        }

        /// <summary>
        /// Reports on the caller's thread, unlike <see cref="Progress{T}"/>, which posts to the thread pool
        /// </summary>
        private sealed class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> report;

            public SynchronousProgress(Action<T> report)
            {
                this.report = report;
            }

            public void Report(T value)
            {
                this.report(value);
            }
        }
    }
}
