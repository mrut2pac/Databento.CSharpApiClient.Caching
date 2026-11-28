using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Backup;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// A full backup of a cache root: every file in one uncompressed tar, named by the backup's start, with a manifest of each file's size and
    /// SHA-256 as its last entry. The tar is written and read back in the staging folder, then put into the store; the older backups
    /// <see cref="CacheBackupOptions.Retention"/> does not keep are deleted after. A backup holds the root's lock, so no compaction or
    /// restore of the root runs alongside it; a cached client may keep saving day files, which are taken as they are found.
    /// </summary>
    public sealed class DatabentoCacheBackup
    {
        private const int BufferSize = 1024 * 1024;

        /// <summary>
        /// Extension of a tar being staged, so the staging folder's clean-up never takes a backup some other store keeps there
        /// </summary>
        private const string StagingExtension = ".tar.staging";

        private readonly CacheBackupOptions options;

        /// <summary>
        /// Creates a backup
        /// </summary>
        /// <param name="options">Backup settings, copied: a later change to them does not reach the backup</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c></exception>
        /// <exception cref="ArgumentException">A folder, the store, the retention or the clock is not given, a setting is out of range, or
        /// the staging folder holds the cache root or is inside it</exception>
        public DatabentoCacheBackup(CacheBackupOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = new CacheBackupOptions
            {
                CacheRoot = options.CacheRoot,
                Store = options.Store,
                StagingFolder = options.StagingFolder,
                Retention = options.Retention,
                MaxBackupDayFiles = options.MaxBackupDayFiles,
                TimeProvider = options.TimeProvider,
                Logger = NonThrowingLogger.Wrap(options.Logger),
            };

            ArgumentException.ThrowIfNullOrEmpty(this.options.CacheRoot, nameof(options));
            ArgumentException.ThrowIfNullOrEmpty(this.options.StagingFolder, nameof(options));
            ArgumentOutOfRangeException.ThrowIfNegative(this.options.MaxBackupDayFiles, nameof(options));
            if(this.options.Store == null || this.options.Retention == null || this.options.TimeProvider == null)
            {
                throw new ArgumentException("Store, Retention and TimeProvider are required.", nameof(options));
            }

            BackupFolders.ThrowIfOverlapping(nameof(options), this.options.Store, this.options.CacheRoot, this.options.StagingFolder);
        }

        /// <summary>
        /// Backs the cache root up into a new tar in the store, then deletes the older backups the retention does not keep. A failure leaves
        /// nothing in the store and deletes no backup; an old backup that cannot be deleted is left for the next run.
        /// </summary>
        /// <param name="force">Backs the cache up however many day files it holds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>What the backup put into the store</returns>
        /// <exception cref="DirectoryNotFoundException">The cache root does not exist</exception>
        /// <exception cref="InvalidOperationException">A restore of the cache is in progress or was stopped before it finished; a compaction,
        /// backup or restore holds the root's lock; the root holds files but no <c>metadata.json</c>; or it holds more day files than
        /// <see cref="CacheBackupOptions.MaxBackupDayFiles"/> and the backup is not forced</exception>
        /// <exception cref="InvalidDataException">The root's <c>metadata.json</c> is not one this package accepts, or the tar does not read back as written</exception>
        /// <exception cref="IOException">The cache root holds no files, or the tar could not be written or put into the store</exception>
        /// <remarks>
        /// Once the backup is in the store it is reported, whatever the retention after it meets: an old backup the store cannot list or
        /// delete is left for the next run.
        /// </remarks>
        public async Task<CacheBackupReport> BackUpAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            LoggedRefusal refusal = new LoggedRefusal();
            try
            {
                this.options.Logger.BackupStarted(this.options.CacheRoot, this.options.Store);
                return await this.RunBackupAsync(force, refusal, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                this.options.Logger.BackupCanceled(this.options.CacheRoot);
                throw;
            }
            catch(Exception ex) when(!refusal.Is(ex))
            {
                this.options.Logger.BackupFailed(this.options.CacheRoot, ex);
                throw;
            }
        }

        /// <summary>
        /// Reads the tar back and checks every file against its manifest
        /// </summary>
        /// <returns>The tar's SHA-256</returns>
        private static async Task<string> CheckAsync(string tarPath, CancellationToken cancellationToken)
        {
            FileStream tar = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            HashingReadStream hashing = new HashingReadStream(tar);
            await using(hashing.ConfigureAwait(false))
            {
                await BackupTar.ReadAsync(hashing, extractFolder: null, cancellationToken).ConfigureAwait(false);
                return hashing.GetHashHex();
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // the next backup deletes what is left in the staging folder
            }
        }

        private static bool IsLeftBackupFile(string path)
        {
            string fileName = Path.GetFileName(path);
            return fileName.EndsWith(StagingExtension, StringComparison.Ordinal) && BackupName.IsValid(fileName[..^StagingExtension.Length]);
        }

        /// <summary>
        /// Backs the cache root up, as <see cref="BackUpAsync"/> says, holding the root's lock
        /// </summary>
        /// <param name="force">Backs the cache up however many day files it holds</param>
        /// <param name="refusal">Notes the refusal thrown, logged where it is thrown</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task<CacheBackupReport> RunBackupAsync(bool force, LoggedRefusal refusal, CancellationToken cancellationToken)
        {
            // a half-restored cache must not become the newest backup and push a good one out
            this.ThrowIfRestoring(refusal);
            if(!Directory.Exists(this.options.CacheRoot))
            {
                throw this.Refuse(refusal, new DirectoryNotFoundException(Invariant($"The cache root {this.options.CacheRoot} does not exist.")));
            }

            if(CacheMetadata.Read(this.options.CacheRoot, mark: false) == null)
            {
                // an empty or wrongly mapped cache root would otherwise push the good backups out one run at a time
                bool holdsFiles = Directory.EnumerateFileSystemEntries(this.options.CacheRoot).Any(entry => !CacheRootLock.IsLockFile(entry));
                throw this.Refuse(refusal, holdsFiles
                    ? new InvalidOperationException(Invariant($"{this.options.CacheRoot} holds files but no {CacheMetadata.FileName}, so it is no cache this package wrote."))
                    : new IOException(Invariant($"{this.options.CacheRoot} holds no files to back up.")));
            }

            using(this.TakeLock(refusal))
            {
                // a restore that started before the lock was taken
                this.ThrowIfRestoring(refusal);

                // unforced, the count stops past the limit, so a refusal does not walk a cache of millions of day files
                int dayFiles = this.CountDayFiles(force ? int.MaxValue : (int)Math.Min((long)this.options.MaxBackupDayFiles + 1, int.MaxValue), cancellationToken);
                if(dayFiles > this.options.MaxBackupDayFiles && !force)
                {
                    throw this.Refuse(refusal, new InvalidOperationException(Invariant($"{this.options.CacheRoot} holds more than MaxBackupDayFiles ({this.options.MaxBackupDayFiles:N0}) day files, so it was not backed up: compact it first, or back it up as it is with force.")));
                }

                this.options.Logger.DayFilesCounted(dayFiles, this.options.CacheRoot);

                DateTime startUtc = this.options.TimeProvider.GetUtcNow().UtcDateTime;
                string name = BackupName.Format(startUtc);
                BackupName.TryParse(name, out startUtc);
                string tarPath = Path.Combine(this.options.StagingFolder, name + StagingExtension);
                BackupManifest manifest;
                try
                {
                    this.DeleteLeftStagingFiles();
                    this.options.Logger.WritingTar(tarPath);
                    manifest = await BackupTar.WriteAsync(this.options.CacheRoot, tarPath, startUtc, cancellationToken).ConfigureAwait(false);
                    this.options.Logger.TarWritten(manifest.FileCount, manifest.TotalBytes);
                    string sha256 = await CheckAsync(tarPath, cancellationToken).ConfigureAwait(false);
                    this.options.Logger.StoringBackup(sha256, name, this.options.Store);
                    await this.options.Store.PutAsync(name, tarPath, sha256, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // a tar that does not read back as written included: the store is left as it was
                    TryDeleteFile(tarPath);
                    throw;
                }

                // a store that copies rather than moves leaves the staged tar behind
                TryDeleteFile(tarPath);

                // the backup is in the store, so nothing after this fails it, a cancellation included
                int oldBackupsDeleted = await this.DeleteOldBackupsAsync(name).ConfigureAwait(false);
                this.options.Logger.BackupStored(name, this.options.Store, manifest.FileCount, manifest.TotalBytes, dayFiles, oldBackupsDeleted);
                return new CacheBackupReport(name, manifest.FileCount, manifest.TotalBytes, dayFiles, oldBackupsDeleted);
            }
        }

        /// <summary>
        /// Logs a refusal about to be thrown and notes it, so it is not logged again as a failure
        /// </summary>
        /// <returns>The refusal, to throw</returns>
        private Exception Refuse(LoggedRefusal refusal, Exception exception)
        {
            this.options.Logger.BackupRefused(this.options.CacheRoot, exception.Message);
            return refusal.Note(exception);
        }

        /// <summary>
        /// Refuses a cache being restored, or one a restore stopped before it finished
        /// </summary>
        private void ThrowIfRestoring(LoggedRefusal refusal)
        {
            try
            {
                CacheRestoreMarker.ThrowIfPresent(this.options.CacheRoot, "the backup");
            }
            catch(InvalidOperationException ex)
            {
                this.Refuse(refusal, ex);
                throw;
            }
        }

        /// <summary>
        /// Takes the cache root's lock, refusing when another run holds it
        /// </summary>
        private CacheRootLock TakeLock(LoggedRefusal refusal)
        {
            try
            {
                return CacheRootLock.Take(this.options.CacheRoot, "the backup");
            }
            catch(InvalidOperationException ex)
            {
                this.Refuse(refusal, ex);
                throw;
            }
        }

        /// <summary>
        /// Counts the day files and legacy day files anywhere under the cache root, stopping once the count reaches <paramref name="limit"/>
        /// </summary>
        private int CountDayFiles(int limit, CancellationToken cancellationToken)
        {
            int count = 0;
            foreach(string path in Directory.EnumerateFiles(this.options.CacheRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(DayFileName.IsDayFile(path) && ++count >= limit)
                {
                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// Deletes the backup tars a stopped backup left in the staging folder, and nothing else there; backups of the root take turns, so
        /// none is still being written
        /// </summary>
        private void DeleteLeftStagingFiles()
        {
            Directory.CreateDirectory(this.options.StagingFolder);
            foreach(string file in Directory.EnumerateFiles(this.options.StagingFolder).Where(IsLeftBackupFile).ToList())
            {
                File.Delete(file);
                this.options.Logger.StagingTarDeleted(file);
            }
        }

        /// <summary>
        /// Deletes the backups the retention does not keep, never the one just written. A store that fails to list or delete leaves the
        /// backups as they are for the next run: the new backup is in the store, so this never fails it.
        /// </summary>
        /// <returns>The number of old backups deleted</returns>
        private async Task<int> DeleteOldBackupsAsync(string newBackup)
        {
            IReadOnlyList<string> backups;
            try
            {
                backups = await this.options.Store.ListAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                // the backup is in the store; the next run deletes what this one could not
                this.options.Logger.RetentionListFailed(this.options.Store, ex.Message);
                return 0;
            }

            int deleted = 0;
            foreach(string backup in this.options.Retention.SelectExpired(backups, newBackup))
            {
                try
                {
                    await this.options.Store.DeleteAsync(backup, CancellationToken.None).ConfigureAwait(false);
                    ++deleted;
                    this.options.Logger.OldBackupDeleted(backup);
                }
                catch(Exception ex)
                {
                    // left for the next run
                    this.options.Logger.RetentionDeleteFailed(backup, ex.Message);
                }
            }

            return deleted;
        }
    }
}
