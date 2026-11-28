using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Compaction;
using Databento.CSharpApiClient.Caching.Logging;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// The compactor's inverse: writes a cache's archive back as DBN day files in their day folders, for a cache read mostly by
    /// contract-day, which day files serve far faster. For each group-month in scope it writes every symbol-day its current version serves,
    /// empty days included, checks each read back, and only then retires the version, so a stop or a failure leaves both and a rerun keeps
    /// the day files already written. A day file or legacy day file already there is kept, being what the cache serves. A run holds the
    /// cache root's lock, so a compaction, backup or restore of the root is refused while it goes on.
    /// </summary>
    public sealed class DatabentoArchiveUnpacker
    {
        private readonly ArchiveUnpackerOptions options;

        /// <summary>
        /// Creates an unpacker
        /// </summary>
        /// <param name="options">Run settings, copied: a later change to them does not reach the unpacker</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c></exception>
        /// <exception cref="ArgumentException">The cache root is not given, or the first month is after the last</exception>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="ArchiveUnpackerOptions.MaxParallelGroups"/> is below 1</exception>
        public DatabentoArchiveUnpacker(ArchiveUnpackerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = new ArchiveUnpackerOptions
            {
                CacheRoot = options.CacheRoot,
                Dataset = options.Dataset,
                Schema = options.Schema,
                Group = options.Group,
                FirstMonth = options.FirstMonth,
                LastMonth = options.LastMonth,
                DryRun = options.DryRun,
                MaxParallelGroups = options.MaxParallelGroups,
                Logger = NonThrowingLogger.Wrap(options.Logger),
            };
            ArgumentException.ThrowIfNullOrEmpty(this.options.CacheRoot, nameof(options));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.options.MaxParallelGroups, nameof(options));
            if(this.options.FirstMonth.HasValue && this.options.LastMonth.HasValue && MonthOf(this.options.FirstMonth.Value) > MonthOf(this.options.LastMonth.Value))
            {
                throw new ArgumentException(Invariant($"FirstMonth {this.options.FirstMonth:yyyy-MM} is after LastMonth {this.options.LastMonth:yyyy-MM}."), nameof(options));
            }
        }

        /// <summary>
        /// Gets or sets what is told each day file's path once it is written and checked, so a test can stop the run partway.
        /// </summary>
        internal Action<string> DayFileWritten { get; set; }

        /// <summary>
        /// Gets or sets what deletes each file the run retires, <see cref="File.Delete"/> when <c>null</c>, so a test can fail a delete.
        /// </summary>
        internal Action<string> DeleteFile { get; set; }

        /// <summary>
        /// Gets or sets the clock of the waits between delete attempts, so a test does not wait them.
        /// </summary>
        internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>
        /// Unpacks the group-months in scope, holding the root's lock file <c>cache.lock</c> while it does. An existing root with nothing
        /// in it has nothing to unpack. A canceled run returns what it did until then, with <see cref="ArchiveUnpackReport.Canceled"/> set.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token; checked between symbol-days, in every group-month in flight</param>
        /// <returns>What the run did</returns>
        /// <exception cref="DirectoryNotFoundException">The cache root does not exist</exception>
        /// <exception cref="InvalidOperationException">A restore of the cache is in progress, or was stopped before it finished; another
        /// compaction, backup, restore or unpack holds the root's lock; or the root holds files but no <c>metadata.json</c>. No cached
        /// client should run meanwhile: one reads a day being unpacked from its new day file, but a day it already looked for in the day
        /// files and found in the archive just then may miss both.</exception>
        /// <exception cref="InvalidDataException">The root's <c>metadata.json</c> is not one this package accepts</exception>
        /// <exception cref="IOException">A version 1 cache's <c>metadata.json</c> cannot be marked version 2, so no day file is written into it</exception>
        public async Task<ArchiveUnpackReport> UnpackAsync(CancellationToken cancellationToken = default)
        {
            LoggedRefusal refusal = new LoggedRefusal();
            try
            {
                return await this.RunUnpackAsync(refusal, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(!refusal.Is(ex) && !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                this.options.Logger.UnpackFailed(this.options.CacheRoot, ex);
                throw;
            }
        }

        private static DateTime MonthOf(DateOnly day)
        {
            return new DateTime(day.Year, day.Month, 1);
        }

        private static long? ReadFreeBytes(string cacheRoot)
        {
            try
            {
                // the drive or mount the root is on: the longest root that holds it, as a Linux mount under / is
                string fullPath = WithEndingSeparator(Path.GetFullPath(cacheRoot));
                DriveInfo drive = DriveInfo.GetDrives()
                    .Where(candidate => fullPath.StartsWith(WithEndingSeparator(candidate.RootDirectory.FullName), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    .OrderByDescending(candidate => candidate.RootDirectory.FullName.Length)
                    .FirstOrDefault();
                return drive?.AvailableFreeSpace;
            }
            catch(Exception ex) when(ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // a network share, for instance
                return null;
            }
        }

        private static string WithEndingSeparator(string path)
        {
            return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
        }

        private static async Task UnpackGroupAsync(IArchiveSchemaHandler handler, ArchiveUnpackRequest request, UnpackRun run, CancellationToken cancellationToken)
        {
            try
            {
                await handler.UnpackAsync(request, run, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                run.Options.Logger.UnpackGroupFailed(request.DisplayName, ex.Message, ex);
                run.Report.GroupFailed(request.DisplayName, ex);
            }
        }

        private async Task<ArchiveUnpackReport> RunUnpackAsync(LoggedRefusal refusal, CancellationToken cancellationToken)
        {
            this.ThrowIfRestoring(refusal);
            if(!Directory.Exists(this.options.CacheRoot))
            {
                throw this.Refuse(refusal, new DirectoryNotFoundException(Invariant($"The cache root {this.options.CacheRoot} does not exist.")));
            }

            ArchiveUnpackReport report = new ArchiveUnpackReport(this.options.DryRun, ReadFreeBytes(this.options.CacheRoot));
            CacheMetadata metadata = CacheMetadata.Read(this.options.CacheRoot, mark: false);
            if(metadata == null)
            {
                if(Directory.EnumerateFileSystemEntries(this.options.CacheRoot).Any(entry => !CacheRootLock.IsLockFile(entry)))
                {
                    throw this.Refuse(refusal, new InvalidOperationException(Invariant($"{this.options.CacheRoot} holds files but no {CacheMetadata.FileName}, so it is no cache this package wrote.")));
                }

                return report;
            }

            using(this.TakeLock(refusal))
            {
                this.ThrowIfRestoring(refusal);

                // DBN day files go only into a cache marked version 2, which an older build refuses
                if(!this.options.DryRun)
                {
                    CacheMetadata.EnsureCreated(this.options.CacheRoot);
                }

                this.options.Logger.UnpackStarted(this.options.CacheRoot, this.options.DryRun ? " (dry run)" : string.Empty);
                try
                {
                    await this.RunAsync(new UnpackRun(this.options, metadata.DayCutoff, report, this.DayFileWritten, this.DeleteFile, this.TimeProvider), cancellationToken).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    report.RunCanceled();
                }
            }

            if(report.Canceled)
            {
                this.options.Logger.UnpackCanceled(this.options.CacheRoot, report.GroupsUnpacked);
            }
            else if(this.options.Logger.IsEnabled(LogLevel.Information))
            {
                this.options.Logger.UnpackFinished(this.options.CacheRoot, report.GroupsUnpacked, report.FailedGroups.Count, report.DayFilesWritten, report.DayFilesKept, report.ArchiveFilesDeleted);
            }

            if(report.FilesLeft.Count > 0)
            {
                this.options.Logger.UnpackFilesLeft(this.options.CacheRoot, report.FilesLeft.Count);
            }

            return report;
        }

        private async Task RunAsync(UnpackRun run, CancellationToken cancellationToken)
        {
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = this.options.MaxParallelGroups,
                CancellationToken = cancellationToken,
            };

            await Parallel.ForEachAsync(this.EnumerateGroups(run.Cutoff, run.Report), parallelOptions, async (group, groupCancellationToken) =>
            {
                await UnpackGroupAsync(group.Handler, group.Request, run, groupCancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// The current archive version of each group-month in scope, one month folder listed at a time
        /// </summary>
        private IEnumerable<(IArchiveSchemaHandler Handler, ArchiveUnpackRequest Request)> EnumerateGroups(string cutoff, ArchiveUnpackReport report)
        {
            foreach(string datasetDirectory in Directory.EnumerateDirectories(this.options.CacheRoot).Order(StringComparer.Ordinal))
            {
                string dataset = Path.GetFileName(datasetDirectory);
                if(!DatabentoArchiveCompactor.IsDatasetName(dataset) || (this.options.Dataset != null && !string.Equals(dataset, this.options.Dataset, StringComparison.Ordinal)))
                {
                    continue;
                }

                foreach(string schemaDirectory in Directory.EnumerateDirectories(datasetDirectory).Order(StringComparer.Ordinal))
                {
                    string schema = Path.GetFileName(schemaDirectory);
                    if((this.options.Schema != null && !string.Equals(schema, this.options.Schema, StringComparison.Ordinal)) || !ArchiveSchemaHandlers.TryGet(schema, out IArchiveSchemaHandler handler))
                    {
                        continue;
                    }

                    foreach((string monthDirectory, DateTime month) in DatabentoArchiveCompactor.EnumerateMonthDirectories(schemaDirectory))
                    {
                        if((this.options.FirstMonth.HasValue && month < MonthOf(this.options.FirstMonth.Value)) || (this.options.LastMonth.HasValue && month > MonthOf(this.options.LastMonth.Value)))
                        {
                            continue;
                        }

                        foreach(ArchiveUnpackRequest request in this.TryFindVersions(dataset, schema, monthDirectory, month, cutoff, report))
                        {
                            yield return (handler, request);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lists a month folder's versions; one that cannot be listed is reported as failed, and the run goes on
        /// </summary>
        private List<ArchiveUnpackRequest> TryFindVersions(string dataset, string schema, string monthDirectory, DateTime month, string cutoff, ArchiveUnpackReport report)
        {
            try
            {
                return this.FindVersions(dataset, schema, monthDirectory, month, cutoff).ToList();
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                string relativeDirectory = Path.GetRelativePath(this.options.CacheRoot, monthDirectory);
                this.options.Logger.UnpackGroupFailed(relativeDirectory, ex.Message, ex);
                report.GroupFailed(relativeDirectory, ex);
                return new List<ArchiveUnpackRequest>();
            }
        }

        private IEnumerable<ArchiveUnpackRequest> FindVersions(string dataset, string schema, string monthDirectory, DateTime month, string cutoff)
        {
            string[] files = Directory.GetFiles(monthDirectory, "*" + ArchiveFileName.Extension);
            string[] temporaryCandidates = Directory.GetFiles(monthDirectory, "*" + ArchiveFileName.TemporaryFileExtension);
            HashSet<string> groups = new HashSet<string>(StringComparer.Ordinal);
            foreach(string file in files)
            {
                if(ArchiveFileName.TryParseGroup(Path.GetFileName(file), dataset, schema, month.Year, month.Month, cutoff, out string group) && (this.options.Group == null || string.Equals(group, this.options.Group, StringComparison.Ordinal)))
                {
                    groups.Add(group);
                }
            }

            string relativeDirectory = Path.GetRelativePath(this.options.CacheRoot, monthDirectory);
            foreach(string group in groups.Order(StringComparer.Ordinal))
            {
                string prefix = ArchiveFileName.GetSearchPrefix(dataset, schema, group, month.Year, month.Month, cutoff);
                ArchiveVersion version = ArchiveFileName.FindCurrentVersion(files, prefix);
                if(version != null)
                {
                    List<string> otherFiles = ArchiveFileName.FindFiles(files, prefix).Where(path => !version.Paths.Contains(path, StringComparer.OrdinalIgnoreCase)).ToList();
                    List<string> temporaryFiles = temporaryCandidates.Where(path => ArchiveFileName.IsTemporaryFileOf(Path.GetFileName(path), prefix)).ToList();
                    yield return new ArchiveUnpackRequest(monthDirectory, relativeDirectory, dataset, schema, group, month.Year, month.Month, version, otherFiles, temporaryFiles);
                }
            }
        }

        private Exception Refuse(LoggedRefusal refusal, Exception exception)
        {
            this.options.Logger.UnpackRefused(this.options.CacheRoot, exception.Message);
            return refusal.Note(exception);
        }

        private void ThrowIfRestoring(LoggedRefusal refusal)
        {
            try
            {
                CacheRestoreMarker.ThrowIfPresent(this.options.CacheRoot, "the unpack");
            }
            catch(InvalidOperationException ex)
            {
                this.Refuse(refusal, ex);
                throw;
            }
        }

        private CacheRootLock TakeLock(LoggedRefusal refusal)
        {
            try
            {
                return CacheRootLock.Take(this.options.CacheRoot, "the unpack");
            }
            catch(InvalidOperationException ex)
            {
                this.Refuse(refusal, ex);
                throw;
            }
        }
    }
}
