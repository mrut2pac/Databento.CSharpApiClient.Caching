using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Backup;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Restores a cache root from a backup in the store. The tar is unpacked into the staging folder and every file, the tar's SHA-256 and its
    /// <c>metadata.json</c> checked there; only then is the cache cleared as <see cref="CacheRestoreOptions.ExistingFiles"/> says and the
    /// backup's files moved in: by default its archive and temporary files are deleted and its day files kept. Throughout, the root's lock keeps
    /// compactions and backups off and the marker <c>RESTORE_IN_PROGRESS</c> keeps new cached clients off; a client already running is
    /// what <see cref="CacheRestoreOptions.InUseChecker"/> is for. A dry run unpacks and checks the backup into its own folder instead.
    /// </summary>
    public sealed class DatabentoCacheRestore
    {
        private const string RestoreDirectorySuffix = ".restore";

        private readonly CacheRestoreOptions options;

        /// <summary>
        /// Creates a restore
        /// </summary>
        /// <param name="options">Restore settings, copied: a later change to them does not reach the restore</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c></exception>
        /// <exception cref="ArgumentException">A folder, the store or the clock is not given, the folders hold one another, or a dry run's
        /// folder is not given or overlaps another folder</exception>
        public DatabentoCacheRestore(CacheRestoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = new CacheRestoreOptions
            {
                CacheRoot = options.CacheRoot,
                Store = options.Store,
                StagingFolder = options.StagingFolder,
                InUseChecker = options.InUseChecker,
                ExistingFiles = options.ExistingFiles,
                DryRun = options.DryRun,
                DryRunFolder = options.DryRunFolder,
                TimeProvider = options.TimeProvider,
                Logger = NonThrowingLogger.Wrap(options.Logger),
            };

            ArgumentException.ThrowIfNullOrEmpty(this.options.CacheRoot, nameof(options));
            ArgumentException.ThrowIfNullOrEmpty(this.options.StagingFolder, nameof(options));
            if(this.options.Store == null || this.options.TimeProvider == null)
            {
                throw new ArgumentException("Store and TimeProvider are required.", nameof(options));
            }

            BackupFolders.ThrowIfOverlapping(nameof(options), this.options.Store, this.options.CacheRoot, this.options.StagingFolder);
            if(!Enum.IsDefined(this.options.ExistingFiles))
            {
                throw new ArgumentException("ExistingFiles takes one of its enum's values.", nameof(options));
            }

            if(this.options.ExistingFiles == ExistingCacheFiles.MoveAside && Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(this.options.CacheRoot))) == null)
            {
                throw new ArgumentException(Invariant($"The cache root {this.options.CacheRoot} has no parent folder to move its files aside into."), nameof(options));
            }
            if(this.options.DryRun)
            {
                string storeFolder = (this.options.Store as LocalDirectoryBackupStore)?.Location;
                CacheFolders.ThrowIfDryRunFolderInvalid(this.options.DryRunFolder, this.options.CacheRoot, this.options.StagingFolder, storeFolder);
            }
        }

        /// <summary>
        /// Restores the cache root from a backup. A backup that fails a check, a cache in use, or a cancellation before the files are swapped
        /// leaves the cache as it was; a failure while they are swapped leaves the marker, so nothing starts on the cache until a restore
        /// finishes. A dry run neither asks whether the cache is in use nor touches it.
        /// </summary>
        /// <param name="backupName">The backup's name, one the store lists</param>
        /// <param name="cancellationToken">Cancellation token; once the files are being swapped the restore runs to its end</param>
        /// <returns>What the restore did, or for a dry run what a real one would do to the cache</returns>
        /// <exception cref="ArgumentException">The name is no backup's, or the store holds no backup of that name</exception>
        /// <exception cref="InvalidOperationException">The in-use checker reports the cache in use, an archive file is open (seen on Windows only), or a compaction, backup or restore holds the root's lock</exception>
        /// <exception cref="InvalidDataException">The backup is damaged, differs from its manifest, or holds no <c>metadata.json</c> this package accepts</exception>
        /// <exception cref="IOException">The store cannot be read, or a file cannot be moved into the cache; during the swap the marker stays</exception>
        public async Task<CacheRestoreReport> RestoreAsync(string backupName, CancellationToken cancellationToken = default)
        {
            LoggedRefusal refusal = new LoggedRefusal();
            try
            {
                this.options.Logger.RestoreStarted(this.options.CacheRoot, backupName, this.options.Store, this.options.ExistingFiles, this.options.DryRun);
                return await this.RunRestoreAsync(backupName, refusal, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                this.options.Logger.RestoreCanceled(this.options.CacheRoot, backupName);
                throw;
            }
            catch(Exception ex) when(!refusal.Is(ex))
            {
                this.options.Logger.RestoreFailed(this.options.CacheRoot, backupName, ex);
                throw;
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if(Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // the next restore deletes what is left in the staging folder
            }
        }

        /// <summary>
        /// Restores the cache root from a backup, as <see cref="RestoreAsync"/> says
        /// </summary>
        /// <param name="backupName">The backup's name</param>
        /// <param name="refusal">Notes the refusal thrown, logged where it is thrown</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task<CacheRestoreReport> RunRestoreAsync(string backupName, LoggedRefusal refusal, CancellationToken cancellationToken)
        {
            // the name becomes a folder name in the staging and dry run folders
            if(!BackupName.IsValid(backupName))
            {
                throw this.Refuse(refusal, backupName, new ArgumentException(Invariant($"{backupName} is not a backup's name (yyyyMMddTHHmmss, UTC)."), nameof(backupName)));
            }

            IReadOnlyList<string> backups = await this.options.Store.ListAsync(cancellationToken).ConfigureAwait(false);
            if(!backups.Contains(backupName, StringComparer.Ordinal))
            {
                throw this.Refuse(refusal, backupName, new ArgumentException(Invariant($"{this.options.Store.Location} holds no backup named {backupName}; it holds {(backups.Count == 0 ? "none" : string.Join(", ", backups))}."), nameof(backupName)));
            }

            if(this.options.DryRun)
            {
                return await this.DryRunAsync(backupName, cancellationToken).ConfigureAwait(false);
            }

            this.ThrowIfInUse(refusal, backupName);
            Directory.CreateDirectory(this.options.CacheRoot);
            using(this.TakeLock(refusal, backupName))
            {
                // a marker left by a stopped restore stays until a restore finishes, since the cache it guards may be half swapped
                bool markerWasLeft = CacheRestoreMarker.Exists(this.options.CacheRoot);
                string restoreDirectory = Path.Combine(this.options.StagingFolder, backupName + RestoreDirectorySuffix);
                DateTime startUtc = this.options.TimeProvider.GetUtcNow().UtcDateTime;
                string movedAsideFolder = this.GetMovedAsideFolder(startUtc);
                BackupManifest manifest;
                try
                {
                    CacheRestoreMarker.Write(this.options.CacheRoot, backupName, startUtc);

                    // a client started before the marker was written would not have seen it
                    this.ThrowIfInUse(refusal, backupName);
                    this.DeleteLeftRestoreDirectories();
                    Directory.CreateDirectory(restoreDirectory);
                    manifest = await this.UnpackCheckedAsync(backupName, restoreDirectory, cancellationToken).ConfigureAwait(false);
                    if(movedAsideFolder != null && Path.Exists(movedAsideFolder))
                    {
                        throw this.Refuse(refusal, backupName, new IOException(Invariant($"{movedAsideFolder} already exists, so the cache's files cannot be moved aside into it; restore again a second later.")));
                    }
                }
                catch
                {
                    TryDeleteDirectory(restoreDirectory);
                    if(!markerWasLeft)
                    {
                        TryDeleteMarker(this.options.CacheRoot);
                    }

                    throw;
                }

                this.options.Logger.SwappingIn(backupName, this.options.CacheRoot, this.options.ExistingFiles);
                CacheRestoreReport report = this.SwapIn(backupName, restoreDirectory, manifest, movedAsideFolder);

                // the cache is whole, so it may be used again; what is left in staging is the next restore's to delete
                CacheRestoreMarker.Delete(this.options.CacheRoot);
                TryDeleteDirectory(restoreDirectory);
                this.options.Logger.Restored(this.options.CacheRoot, backupName, report.FileCount, report.FilesDeleted, report.DayFilesKept, report.FilesMovedAside);
                return report;
            }
        }

        /// <summary>
        /// Deletes the marker after a restore that left the cache as it was; the restore's own failure is the one thrown
        /// </summary>
        private static void TryDeleteMarker(string cacheRoot)
        {
            try
            {
                CacheRestoreMarker.Delete(cacheRoot);
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // the cache is as it was; the marker keeps it closed until it is deleted or a restore finishes
            }
        }

        /// <summary>
        /// Gets whether a cache file is an archive file or a temporary one, which a restore deletes whatever its month: the backup's archive
        /// files must be the only ones read
        /// </summary>
        private static bool IsArchiveOrTemporaryFile(string path)
        {
            return path.EndsWith(ArchiveFileName.Extension, StringComparison.Ordinal) || path.EndsWith(ArchiveFileName.TemporaryFileExtension, StringComparison.Ordinal);
        }

        private static bool IsDayFile(string path)
        {
            return path.EndsWith(DayFileName.Extension, StringComparison.Ordinal);
        }

        private static int CountFiles(string entry)
        {
            return Directory.Exists(entry) ? Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories).Count() : 1;
        }

        private static string ToPath(string folder, BackupManifestFile file)
        {
            return Path.Combine(folder, Path.Combine(file.Path.Split('/')));
        }

        /// <summary>
        /// Unpacks and checks the backup into its folder under the dry run folder, replacing an earlier dry run of it, and counts what a real
        /// restore would delete from the cache and keep there. Nothing in the cache changes, so it may be in use.
        /// </summary>
        private async Task<CacheRestoreReport> DryRunAsync(string backupName, CancellationToken cancellationToken)
        {
            string restoreDirectory = Path.Combine(this.options.DryRunFolder, backupName);
            if(Directory.Exists(restoreDirectory))
            {
                Directory.Delete(restoreDirectory, recursive: true);
                this.options.Logger.EarlierDryRunDeleted(restoreDirectory);
            }

            Directory.CreateDirectory(restoreDirectory);
            try
            {
                BackupManifest manifest = await this.UnpackCheckedAsync(backupName, restoreDirectory, cancellationToken).ConfigureAwait(false);

                // the cache may change under the count, since it may be in use
                string movedAsideFolder = this.GetMovedAsideFolder(this.options.TimeProvider.GetUtcNow().UtcDateTime);
                int cleared = Directory.Exists(this.options.CacheRoot) ? this.ListClearedEntries().Sum(CountFiles) : 0;
                CacheRestoreReport report;
                if(this.options.ExistingFiles != ExistingCacheFiles.Keep)
                {
                    report = new CacheRestoreReport(backupName, manifest.FileCount, manifest.TotalBytes, dryRun: true, restoreDirectory,
                        this.options.ExistingFiles == ExistingCacheFiles.Delete ? cleared : 0, 0, this.options.ExistingFiles == ExistingCacheFiles.MoveAside ? cleared : 0, movedAsideFolder);
                }
                else
                {
                    int deleted = Directory.Exists(this.options.CacheRoot) ? Directory.EnumerateFiles(this.options.CacheRoot, "*", SearchOption.AllDirectories).Count(IsArchiveOrTemporaryFile) : 0;
                    int kept = manifest.Files.Select(file => ToPath(this.options.CacheRoot, file)).Count(path => IsDayFile(path) && File.Exists(path));
                    report = new CacheRestoreReport(backupName, manifest.FileCount, manifest.TotalBytes, dryRun: true, restoreDirectory, deleted, kept, 0, null);
                }

                this.options.Logger.DryRunDone(backupName, restoreDirectory, report.FilesDeleted, report.DayFilesKept, report.FilesMovedAside);
                return report;
            }
            catch
            {
                TryDeleteDirectory(restoreDirectory);
                throw;
            }
        }

        private void ThrowIfInUse(LoggedRefusal refusal, string backupName)
        {
            if(this.options.InUseChecker?.IsInUse() == true)
            {
                throw this.Refuse(refusal, backupName, new InvalidOperationException(Invariant($"{this.options.CacheRoot} is in use, as its in-use checker reports, so it was not restored; stop what uses it and restore again.")));
            }

            List<string> held = this.ListHeldArchiveFiles();
            if(held.Count > 0)
            {
                throw this.Refuse(refusal, backupName, new InvalidOperationException(Invariant($"{this.options.CacheRoot} is in use: {held.Count} archive file(s) are open, as a running cached client keeps them, so it was not restored; stop what uses it and restore again. Open: {string.Join(", ", held.Take(5))}{(held.Count > 5 ? ", ..." : string.Empty)}")));
            }
        }

        /// <summary>
        /// Lists the cache's archive files something holds open, each found by an open that shares nothing. A running cached client keeps
        /// the archive files it reads open; where the system does not lock files for other processes, none is found.
        /// </summary>
        private List<string> ListHeldArchiveFiles()
        {
            List<string> held = new List<string>();
            if(!Directory.Exists(this.options.CacheRoot))
            {
                return held;
            }

            foreach(string file in Directory.EnumerateFiles(this.options.CacheRoot, "*" + ArchiveFileName.Extension, SearchOption.AllDirectories))
            {
                try
                {
                    using(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                }
                catch(IOException ex) when(ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
                {
                    held.Add(Path.GetRelativePath(this.options.CacheRoot, file));
                }
            }

            return held;
        }

        /// <summary>
        /// Logs a refusal about to be thrown and notes it, so it is not logged again as a failure
        /// </summary>
        /// <returns>The refusal, to throw</returns>
        private Exception Refuse(LoggedRefusal refusal, string backupName, Exception exception)
        {
            this.options.Logger.RestoreRefused(this.options.CacheRoot, backupName, exception.Message);
            return refusal.Note(exception);
        }

        /// <summary>
        /// Takes the cache root's lock, refusing when another run holds it
        /// </summary>
        private CacheRootLock TakeLock(LoggedRefusal refusal, string backupName)
        {
            try
            {
                return CacheRootLock.Take(this.options.CacheRoot, "the restore");
            }
            catch(InvalidOperationException ex)
            {
                this.Refuse(refusal, backupName, ex);
                throw;
            }
        }

        /// <summary>
        /// Unpacks the backup into the restore directory, checking every file against the manifest, the tar against its stored SHA-256 and
        /// its <c>metadata.json</c> against what this package accepts
        /// </summary>
        private async Task<BackupManifest> UnpackCheckedAsync(string backupName, string restoreDirectory, CancellationToken cancellationToken)
        {
            this.options.Logger.Unpacking(backupName, restoreDirectory);
            string expectedSha256 = await this.options.Store.GetSha256Async(backupName, cancellationToken).ConfigureAwait(false);
            Stream tar = await this.options.Store.OpenReadAsync(backupName, cancellationToken).ConfigureAwait(false);
            BackupManifest manifest;
            HashingReadStream hashing = new HashingReadStream(tar);
            await using(hashing.ConfigureAwait(false))
            {
                manifest = await BackupTar.ReadAsync(hashing, restoreDirectory, cancellationToken).ConfigureAwait(false);
                if(BackupName.Format(manifest.RunStartUtc) != backupName)
                {
                    throw new InvalidDataException(Invariant($"The backup {backupName} holds the manifest of the backup started {manifest.RunStartUtc:yyyy-MM-dd HH:mm:ss} UTC."));
                }

                string sha256 = hashing.GetHashHex();
                if(!string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(Invariant($"The backup {backupName} has SHA-256 {sha256}, but was stored with {expectedSha256}."));
                }
            }

            if(CacheMetadata.Read(restoreDirectory) == null)
            {
                throw new InvalidDataException(Invariant($"The backup {backupName} holds no {CacheMetadata.FileName}, so it is no backup of a cache this package wrote."));
            }

            this.options.Logger.BackupChecked(backupName, manifest.FileCount, manifest.TotalBytes);
            return manifest;
        }

        /// <summary>
        /// Clears the cache root as <see cref="CacheRestoreOptions.ExistingFiles"/> says, then moves the backup's files in; with
        /// <see cref="ExistingCacheFiles.Keep"/> a day file the cache already holds is kept, being the same file or a later download of the day
        /// </summary>
        private CacheRestoreReport SwapIn(string backupName, string restoreDirectory, BackupManifest manifest, string movedAsideFolder)
        {
            int deleted = 0;
            int movedAside = 0;
            if(this.options.ExistingFiles == ExistingCacheFiles.Keep)
            {
                foreach(string file in Directory.EnumerateFiles(this.options.CacheRoot, "*", SearchOption.AllDirectories).Where(IsArchiveOrTemporaryFile).ToList())
                {
                    File.Delete(file);
                    ++deleted;
                }
            }
            else
            {
                if(movedAsideFolder != null)
                {
                    Directory.CreateDirectory(movedAsideFolder);
                }

                foreach(string entry in this.ListClearedEntries())
                {
                    int files = CountFiles(entry);
                    string target = movedAsideFolder == null ? null : Path.Combine(movedAsideFolder, Path.GetFileName(entry));
                    if(Directory.Exists(entry))
                    {
                        if(target == null)
                        {
                            Directory.Delete(entry, recursive: true);
                        }
                        else
                        {
                            Directory.Move(entry, target);
                        }
                    }
                    else if(target == null)
                    {
                        File.Delete(entry);
                    }
                    else
                    {
                        File.Move(entry, target);
                    }

                    if(target == null)
                    {
                        deleted += files;
                    }
                    else
                    {
                        movedAside += files;
                    }
                }
            }

            int kept = 0;
            foreach(BackupManifestFile file in manifest.Files)
            {
                string target = ToPath(this.options.CacheRoot, file);
                if(IsDayFile(target) && File.Exists(target))
                {
                    ++kept;
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(ToPath(restoreDirectory, file), target, overwrite: true);
            }

            return new CacheRestoreReport(backupName, manifest.FileCount, manifest.TotalBytes, dryRun: false, this.options.CacheRoot, deleted, kept, movedAside, movedAsideFolder);
        }

        /// <summary>
        /// Gets the folder the cache's files are moved aside into, beside the cache root and named by the restore's start, or <c>null</c>
        /// unless they are moved aside
        /// </summary>
        private string GetMovedAsideFolder(DateTime startUtc)
        {
            if(this.options.ExistingFiles != ExistingCacheFiles.MoveAside)
            {
                return null;
            }

            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(this.options.CacheRoot));
            return root + ".bak_" + BackupName.Format(startUtc);
        }

        /// <summary>
        /// Lists the cache root's files and folders a clean restore clears: everything but the root's lock and the restore's marker
        /// </summary>
        private List<string> ListClearedEntries()
        {
            return Directory.EnumerateFileSystemEntries(this.options.CacheRoot)
                .Where(entry => !string.Equals(Path.GetFileName(entry), CacheRootLock.FileName, StringComparison.Ordinal) && !string.Equals(Path.GetFileName(entry), CacheRestoreMarker.FileName, StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>
        /// Deletes what a restore that did not finish left in the staging folder, and nothing else there
        /// </summary>
        private void DeleteLeftRestoreDirectories()
        {
            Directory.CreateDirectory(this.options.StagingFolder);
            foreach(string directory in Directory.EnumerateDirectories(this.options.StagingFolder, "*" + RestoreDirectorySuffix).ToList())
            {
                if(BackupName.IsValid(Path.GetFileName(directory)[..^RestoreDirectorySuffix.Length]))
                {
                    Directory.Delete(directory, recursive: true);
                    this.options.Logger.RestoreFolderDeleted(directory);
                }
            }
        }
    }
}
