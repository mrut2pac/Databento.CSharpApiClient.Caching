using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Backup;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.UnitTests.Backup;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// A good backup replaces the cache's archive files and keeps its day files, a cache in use or locked is refused, the marker keeps new
    /// clients off throughout, a backup failing any check leaves the cache as it was, and a dry run unpacks into its own folder only
    /// </summary>
    [TestClass]
    public class DatabentoCacheRestoreTests
    {
        private const string Name = "20261119T021530";

        private const string FullFile = "OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet";

        private const string DayFile = "OPRA.PILLAR/cbbo-1m/2026/10/SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz";

        private const string LaterDayFile = "OPRA.PILLAR/tcbbo/2026/11/SPXW%20%20261120C06600000_2026-11-20.UTC.json.gz";

        private static readonly KeyValuePair<string, byte[]>[] BackupFiles =
        {
            BackupTarTestFiles.Metadata,
            BackupTarTestFiles.Entry(FullFile, 1, 2, 3),
            BackupTarTestFiles.Entry(DayFile, 4),
            BackupTarTestFiles.Entry("XNAS.ITCH/ohlcv-1d/2026/10/SPY_2026-10-16.UTC.json.gz", 5),
        };

        private CacheFixture cache;

        private string StoreFolder => Path.Combine(this.cache.Folder, "Backups");

        private string StagingFolder => Path.Combine(this.cache.Folder, "Staging");

        private string DryRunRestoreFolder => Path.Combine(this.cache.DryRunFolder, Name);

        private string MarkerPath => Path.Combine(this.cache.Root, CacheRestoreMarker.FileName);

        private string MovedAsideFolder => this.cache.Root + ".bak_20261124T120000";

        [TestInitialize]
        public void TestInitialize()
        {
            this.cache = new CacheFixture();
            this.WriteCacheFile("OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261120T021500.full.parquet", 9, 9);
            this.WriteCacheFile("OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261120T021500.delta.1.parquet", 9);
            this.WriteCacheFile("OPRA.PILLAR/tcbbo/2026/11/being-written.json.gz.0123456789abcdef.tmp", 7);
            this.WriteCacheFile(LaterDayFile, 8);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.cache.Dispose();
        }

        [TestMethod]
        public async Task Restore_GoodBackup_ReplacesEveryArchiveFileAndKeepsTheCachesDayFiles()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore().RestoreAsync(Name);

            Assert.AreEqual(Name, report.BackupName);
            Assert.AreEqual(4, report.FileCount);
            Assert.AreEqual(BackupFiles.Sum(file => file.Value.Length), report.TotalBytes);
            Assert.IsFalse(report.DryRun);
            Assert.AreEqual(this.cache.Root, report.RestoredFolder);
            Assert.AreEqual(3, report.FilesDeleted, "two archive files and the temporary file");
            Assert.AreEqual(0, report.DayFilesKept);
            string[] expected = BackupFiles.Select(file => file.Key).Append(LaterDayFile).Order(StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, this.CacheFiles(), "the later archive files and the temporary file deleted, the later day file kept");
            foreach(KeyValuePair<string, byte[]> file in BackupFiles)
            {
                CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(this.CachePath(file.Key)), file.Key);
            }

            Assert.AreEqual(0, Directory.GetFileSystemEntries(this.StagingFolder).Length, "the unpacked backup is moved out");
            Assert.IsTrue(File.Exists(Path.Combine(this.StoreFolder, Name + ".tar")), "the backup stays in the store");
        }

        [TestMethod]
        public async Task Restore_DayFileInTheCacheAndTheBackup_KeepsTheCachesCopy()
        {
            // a later download of the day; the next compaction folds it in
            this.WriteCacheFile(DayFile, 6, 6);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore().RestoreAsync(Name);

            Assert.AreEqual(1, report.DayFilesKept);
            CollectionAssert.AreEqual(new byte[] { 6, 6 }, File.ReadAllBytes(this.CachePath(DayFile)));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(this.CachePath(FullFile)));
        }

        [TestMethod]
        public async Task Restore_NoCacheRoot_CreatesItFromTheBackup()
        {
            Directory.Delete(this.cache.Root, recursive: true);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore().RestoreAsync(Name);

            CollectionAssert.AreEqual(BackupFiles.Select(file => file.Key).Order(StringComparer.Ordinal).ToArray(), this.CacheFiles());
        }

        [TestMethod]
        public async Task Restore_CacheMetadataDiffering_IsReplacedByTheBackups()
        {
            this.WriteCacheFile(CacheMetadata.FileName, 1);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore().RestoreAsync(Name);

            CollectionAssert.AreEqual(BackupTarTestFiles.Metadata.Value, File.ReadAllBytes(this.CachePath(CacheMetadata.FileName)));
        }

        [TestMethod]
        public async Task Restore_Delete_RestoresIntoACleanRootReplacingABadDayFile()
        {
            // a release that wrote bad day files: the backup's copies must win
            this.WriteCacheFile(DayFile, 6, 6);
            this.WriteCacheFile("notes.txt", 1);
            int cacheFiles = this.CacheFiles().Length;
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore(change: options => options.ExistingFiles = ExistingCacheFiles.Delete).RestoreAsync(Name);

            CollectionAssert.AreEqual(BackupFiles.Select(file => file.Key).Order(StringComparer.Ordinal).ToArray(), this.CacheFiles(), "the backup's files alone");
            CollectionAssert.AreEqual(new byte[] { 4 }, File.ReadAllBytes(this.CachePath(DayFile)));
            Assert.AreEqual(cacheFiles, report.FilesDeleted);
            Assert.AreEqual(0, report.DayFilesKept);
            Assert.AreEqual(0, report.FilesMovedAside);
            Assert.IsNull(report.MovedAsideFolder);
        }

        [TestMethod]
        public async Task Restore_MoveAside_MovesTheCacheBesideTheRootAndRestoresIntoACleanRoot()
        {
            this.WriteCacheFile(DayFile, 6, 6);
            this.WriteCacheFile("notes.txt", 1);
            Dictionary<string, byte[]> before = this.CacheContents();

            // a marker a stopped restore left is the restore's, never the cache's, so it is not moved
            File.WriteAllText(this.MarkerPath, "backup 20261118T021530");
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore(change: options => options.ExistingFiles = ExistingCacheFiles.MoveAside).RestoreAsync(Name);

            CollectionAssert.AreEqual(BackupFiles.Select(file => file.Key).Order(StringComparer.Ordinal).ToArray(), this.CacheFiles(), "the backup's files alone, the marker gone");
            CollectionAssert.AreEqual(new byte[] { 4 }, File.ReadAllBytes(this.CachePath(DayFile)));
            Assert.AreEqual(this.MovedAsideFolder, report.MovedAsideFolder);
            Assert.AreEqual(before.Count, report.FilesMovedAside);
            Assert.AreEqual(0, report.FilesDeleted);
            CollectionAssert.AreEqual(before.Keys.ToArray(), Files(this.MovedAsideFolder), "every file of the cache, and neither the lock nor the marker");
            foreach(KeyValuePair<string, byte[]> file in before)
            {
                CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(Path.Combine(this.MovedAsideFolder, Path.Combine(file.Key.Split('/')))), file.Key);
            }

            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Root, CacheRootLock.FileName)), "the lock is released");
        }

        [TestMethod]
        public async Task Restore_MoveAsideFolderAlreadyThere_ThrowsAndLeavesTheCacheAsItWas()
        {
            Directory.CreateDirectory(this.MovedAsideFolder);
            File.WriteAllBytes(Path.Combine(this.MovedAsideFolder, "earlier.json.gz"), new byte[] { 1 });
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateRestore(change: options => options.ExistingFiles = ExistingCacheFiles.MoveAside).RestoreAsync(Name));

            this.AssertCacheIs(before);
            CollectionAssert.AreEqual(new[] { "earlier.json.gz" }, Files(this.MovedAsideFolder));
        }

        [TestMethod]
        [DataRow(ExistingCacheFiles.Keep)]
        [DataRow(ExistingCacheFiles.MoveAside)]
        [DataRow(ExistingCacheFiles.Delete)]
        public async Task Restore_ArchiveFileHeldOpenAsTheCachedClientHoldsIt_ThrowsNamingItAndLeavesTheCacheAsItWas(ExistingCacheFiles existing)
        {
            if(!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Only Windows refuses an open that shares nothing while another process's handle holds the file.");
            }

            const string HeldFile = "OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261117T021500.full.parquet";
            this.WriteCacheFile(HeldFile, 7);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            using(new FileStream(this.CachePath(HeldFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore(change: options => options.ExistingFiles = existing).RestoreAsync(Name));

                StringAssert.Contains(ex.Message, Path.Combine(HeldFile.Split('/')));
            }

            this.AssertCacheIs(before);
            Assert.IsFalse(File.Exists(this.MarkerPath), "refused before the marker is written");
            Assert.IsFalse(Directory.Exists(this.MovedAsideFolder));
        }

        [TestMethod]
        [DataRow(ExistingCacheFiles.Keep)]
        [DataRow(ExistingCacheFiles.MoveAside)]
        [DataRow(ExistingCacheFiles.Delete)]
        public async Task DryRun_EachWayOfClearing_CountsWhatTheRealRestoreDoes(ExistingCacheFiles existing)
        {
            this.WriteCacheFile(DayFile, 6, 6);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            CacheRestoreReport dryRun = await this.CreateRestore(dryRun: true, change: options => options.ExistingFiles = existing).RestoreAsync(Name);
            this.AssertCacheIs(before);
            Assert.IsFalse(Directory.Exists(this.MovedAsideFolder));
            CacheRestoreReport real = await this.CreateRestore(change: options => options.ExistingFiles = existing).RestoreAsync(Name);

            Assert.AreEqual(real.FilesDeleted, dryRun.FilesDeleted);
            Assert.AreEqual(real.DayFilesKept, dryRun.DayFilesKept);
            Assert.AreEqual(real.FilesMovedAside, dryRun.FilesMovedAside);
            Assert.AreEqual(real.MovedAsideFolder, dryRun.MovedAsideFolder);
            Assert.AreEqual(existing == ExistingCacheFiles.Keep ? 1 : 0, real.DayFilesKept);
        }

        [TestMethod]
        [DataRow("damaged", DisplayName = "a byte of a file changed")]
        [DataRow("differs", DisplayName = "a file differing from the manifest")]
        [DataRow("missing", DisplayName = "a file the manifest lists missing")]
        [DataRow("extra", DisplayName = "a file the manifest does not list")]
        [DataRow("hash", DisplayName = "a tar differing from its stored hash")]
        [DataRow("metadata", DisplayName = "no metadata.json")]
        [DataRow("version", DisplayName = "metadata.json of another format version")]
        public async Task Restore_BackupFailingACheck_ThrowsAndLeavesTheCacheAsItWas(string fault)
        {
            KeyValuePair<string, byte[]>[] withoutMetadata = BackupFiles.Skip(1).ToArray();
            byte[] tar = fault switch
            {
                "differs" => BackupTarTestFiles.CreateTar(BackupFiles, BackupTarTestFiles.CreateManifest(BackupFiles.Take(3).Append(BackupTarTestFiles.Entry(BackupFiles[3].Key, 9)))),
                "missing" => BackupTarTestFiles.CreateTar(BackupFiles.Take(3), BackupTarTestFiles.CreateManifest(BackupFiles)),
                "extra" => BackupTarTestFiles.CreateTar(BackupFiles, BackupTarTestFiles.CreateManifest(BackupFiles.Take(3))),
                "metadata" => BackupTarTestFiles.CreateTar(withoutMetadata),
                "version" => BackupTarTestFiles.CreateTar(withoutMetadata.Prepend(BackupTarTestFiles.Entry("metadata.json", "{\"format_version\": 2, \"day_cutoff\": \"UTC\"}"u8.ToArray()))),
                _ => BackupTarTestFiles.CreateTar(BackupFiles),
            };

            string sha256 = BackupTarTestFiles.Sha256(tar);
            if(fault == "damaged")
            {
                tar[FindBytes(tar, BackupFiles[1].Value)] ^= 0xFF;
                sha256 = BackupTarTestFiles.Sha256(tar);
            }
            else if(fault == "hash")
            {
                sha256 = new string('0', 64);
            }

            this.PutBackup(tar, sha256);
            Dictionary<string, byte[]> before = this.CacheContents();

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore().RestoreAsync(Name));

            this.AssertCacheIs(before);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(this.StagingFolder).Length, "the unpacked files are deleted");
        }

        [TestMethod]
        public async Task Restore_CacheInUse_ThrowsBeforeWritingTheMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            bool markerSeen = false;
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(_ =>
            {
                markerSeen |= File.Exists(this.MarkerPath);
                return true;
            });

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore(checker).RestoreAsync(Name));

            StringAssert.Contains(exception.Message, "in use");
            Assert.AreEqual(1, checker.Checks);
            Assert.IsFalse(markerSeen);
            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_CacheUsedOnceTheMarkerWasWritten_ThrowsAndDeletesTheMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            // free at the first check, in use at the second, made once the marker is in
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check => check == 2 && File.Exists(this.MarkerPath));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore(checker).RestoreAsync(Name));

            Assert.AreEqual(2, checker.Checks);
            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_CacheUsedWhileTheBackupWasUnpacked_ThrowsBeforeTheSwapAndDeletesTheMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            // free at the first two checks, in use at the last, made once the backup is unpacked
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check => check == 3 && File.Exists(this.MarkerPath));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore(checker).RestoreAsync(Name));

            Assert.AreEqual(3, checker.Checks);
            this.AssertCacheIs(before);
            Assert.IsFalse(File.Exists(this.MarkerPath));
        }

        [TestMethod]
        public async Task Restore_WhileRunning_KeepsNewClientsOffWithTheMarkerAndDeletesItOnceDone()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            bool markerAtSecondCheck = false;
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check =>
            {
                if(check == 2)
                {
                    markerAtSecondCheck = File.Exists(this.MarkerPath);
                    StringAssert.Contains(File.ReadAllText(this.MarkerPath), Name);
                    InvalidOperationException refused = Assert.ThrowsException<InvalidOperationException>(() => CacheRestoreMarker.ThrowIfPresent(this.cache.Root, "the cached client"));
                    StringAssert.Contains(refused.Message, Name);
                }

                return false;
            });

            await this.CreateRestore(checker).RestoreAsync(Name);

            Assert.IsTrue(markerAtSecondCheck);
            Assert.IsFalse(File.Exists(this.MarkerPath));
            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Root, CacheRootLock.FileName)), "the lock is released");
        }

        [TestMethod]
        public async Task Restore_LockHeldByACompaction_IsRefusedWithoutAMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            using(CacheRootLock.Take(this.cache.Root, "the compaction"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore().RestoreAsync(Name));
            }

            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_MarkerLeftByAStoppedRestoreAndTheBackupFailing_KeepsTheMarker()
        {
            // the cache that marker guards may be half swapped, so it stays closed until a restore finishes
            File.WriteAllText(this.MarkerPath, "backup 20261118T021530");
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles), new string('0', 64));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore().RestoreAsync(Name));

            Assert.IsTrue(File.Exists(this.MarkerPath));
        }

        [TestMethod]
        public async Task Restore_MarkerLeftByAStoppedRestore_IsTakenOverAndDeletedOnceDone()
        {
            File.WriteAllText(this.MarkerPath, "backup 20261118T021530");
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore().RestoreAsync(Name);

            Assert.IsFalse(File.Exists(this.MarkerPath));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(this.CachePath(FullFile)));
        }

        [TestMethod]
        public async Task Restore_FailingDuringTheSwap_KeepsTheMarkerSoNothingStartsOnTheCache()
        {
            // a directory where a backup file goes makes moving it in fail once the cache's archive files are gone
            Directory.CreateDirectory(this.CachePath(BackupFiles[3].Key));
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await AssertThrowsIOAsync(() => this.CreateRestore().RestoreAsync(Name));

            Assert.IsTrue(File.Exists(this.MarkerPath));
            Assert.ThrowsException<InvalidOperationException>(() => CacheRestoreMarker.ThrowIfPresent(this.cache.Root, "the cached client"));
        }

        [TestMethod]
        public async Task Restore_EntryOutsideTheCacheRoot_ThrowsAndLeavesTheCacheAsItWas()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles.Append(BackupTarTestFiles.Entry("../outside.json.gz", 1))));
            Dictionary<string, byte[]> before = this.CacheContents();

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore().RestoreAsync(Name));

            this.AssertCacheIs(before);
            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Folder, "outside.json.gz")));
            Assert.IsFalse(File.Exists(Path.Combine(this.StagingFolder, "outside.json.gz")));
        }

        [TestMethod]
        public async Task Restore_ManifestOfAnotherBackup_ThrowsAndLeavesTheCacheAsItWas()
        {
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(BackupFiles);
            manifest.RunStartUtc = manifest.RunStartUtc.AddDays(-1);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles, manifest));
            Dictionary<string, byte[]> before = this.CacheContents();

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore().RestoreAsync(Name));

            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_Canceled_LeavesTheCacheAsItWasWithoutAMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                await AssertCanceledAsync(() => this.CreateRestore().RestoreAsync(Name, cancellation.Token));
            }

            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_CanceledOnceTheMarkerIsIn_LeavesTheCacheAsItWasWithoutAMarker()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                // canceled at the second check, once the marker is in and before the backup is read
                FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check =>
                {
                    if(check == 2)
                    {
                        cancellation.Cancel();
                    }

                    return false;
                });

                await AssertCanceledAsync(() => this.CreateRestore(checker).RestoreAsync(Name, cancellation.Token));
            }

            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_CheckerThrowingOnceTheMarkerIsIn_DeletesTheMarkerAndLeavesTheCache()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check => check == 2 ? throw new TimeoutException("The service did not answer.") : false);

            await Assert.ThrowsExceptionAsync<TimeoutException>(() => this.CreateRestore(checker).RestoreAsync(Name));

            this.AssertCacheIs(before);
        }

        [TestMethod]
        [DataRow("..", DisplayName = "a parent folder")]
        [DataRow("notes", DisplayName = "not a time")]
        [DataRow(null, DisplayName = "null")]
        public async Task Restore_NameThatIsNoBackupsListedByTheStore_IsRefused(string name)
        {
            // the name would become a folder name in the staging and dry run folders
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                ExtraNames = new[] { name },
            };

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.CreateRestore(change: options => options.Store = store).RestoreAsync(name));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.CreateRestore(dryRun: true, change: options => options.Store = store).RestoreAsync(name));

            this.AssertCacheIs(before);
            Assert.IsTrue(Directory.Exists(this.StoreFolder));
            Assert.IsFalse(Directory.Exists(this.cache.DryRunFolder));
            Assert.IsFalse(Directory.Exists(this.StagingFolder));
        }

        [TestMethod]
        public async Task Restore_BackupNotInTheStore_ThrowsNamingTheBackupsThere()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.CreateRestore().RestoreAsync("20261118T021530"));

            StringAssert.Contains(exception.Message, Name);
            this.AssertCacheIs(before);
        }

        [TestMethod]
        public async Task Restore_LeftRestoreFolder_IsDeletedAndOtherFoldersKept()
        {
            string left = Path.Combine(this.StagingFolder, "20261118T021530.restore", Datasets.OpraPillar);
            Directory.CreateDirectory(left);
            File.WriteAllBytes(Path.Combine(left, "half.json.gz"), new byte[] { 1 });
            string other = Path.Combine(this.StagingFolder, "Notes");
            Directory.CreateDirectory(other);
            string notes = Path.Combine(this.StagingFolder, "notes.restore");
            Directory.CreateDirectory(notes);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore().RestoreAsync(Name);

            CollectionAssert.AreEquivalent(new[] { other, notes }, Directory.GetDirectories(this.StagingFolder));
        }

        [TestMethod]
        public async Task DryRun_GoodBackupWhileInUse_UnpacksIntoTheDryRunFolderAndLeavesTheCacheAsItWas()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            Dictionary<string, byte[]> before = this.CacheContents();
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(_ => true);

            // a dry run changes nothing in the cache, so it may be in use and no marker is written
            CacheRestoreReport report = await this.CreateRestore(checker, dryRun: true).RestoreAsync(Name);

            this.AssertCacheIs(before);
            Assert.AreEqual(0, checker.Checks);
            Assert.IsTrue(report.DryRun);
            Assert.AreEqual(4, report.FileCount);
            Assert.AreEqual(this.DryRunRestoreFolder, report.RestoredFolder);
            Assert.AreEqual(3, report.FilesDeleted, "what a real restore would delete: two archive files and the temporary file");
            Assert.AreEqual(0, report.DayFilesKept);
            CollectionAssert.AreEqual(BackupFiles.Select(file => file.Key).Order(StringComparer.Ordinal).ToArray(), Files(this.DryRunRestoreFolder), "the backup's files alone");
            Assert.IsFalse(Directory.Exists(this.StagingFolder), "the staging folder is a real restore's");
        }

        [TestMethod]
        public async Task DryRun_SameCacheAsARealRestore_CountsWhatTheRealRestoreDoes()
        {
            this.WriteCacheFile(FullFile, 9);
            this.WriteCacheFile(DayFile, 6, 6);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport dryRun = await this.CreateRestore(dryRun: true).RestoreAsync(Name);
            CacheRestoreReport real = await this.CreateRestore().RestoreAsync(Name);

            Assert.AreEqual(4, real.FilesDeleted, "the three archive files and the temporary file");
            Assert.AreEqual(1, real.DayFilesKept);
            Assert.AreEqual(real.FilesDeleted, dryRun.FilesDeleted);
            Assert.AreEqual(real.DayFilesKept, dryRun.DayFilesKept);
        }

        [TestMethod]
        public async Task DryRun_MarkerLeftByAStoppedRestore_KeepsIt()
        {
            File.WriteAllText(this.MarkerPath, "backup 20261118T021530");
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore(dryRun: true).RestoreAsync(Name);

            Assert.AreEqual("backup 20261118T021530", File.ReadAllText(this.MarkerPath), "the half swapped cache stays guarded");
        }

        [TestMethod]
        public async Task DryRun_NoCacheRoot_CountsNothingAndCreatesNone()
        {
            Directory.Delete(this.cache.Root, recursive: true);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore(dryRun: true).RestoreAsync(Name);

            Assert.AreEqual(0, report.FilesDeleted);
            Assert.AreEqual(0, report.DayFilesKept);
            Assert.IsFalse(Directory.Exists(this.cache.Root));
        }

        [TestMethod]
        public async Task DryRun_BackupFailingACheck_ThrowsAndLeavesNothingBehind()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles), new string('0', 64));
            Dictionary<string, byte[]> before = this.CacheContents();

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore(dryRun: true).RestoreAsync(Name));

            this.AssertCacheIs(before);
            Assert.IsFalse(Directory.Exists(this.DryRunRestoreFolder));
        }

        [TestMethod]
        public async Task DryRun_EarlierDryRunOfTheSameBackup_IsReplacedAndOtherFoldersKept()
        {
            Directory.CreateDirectory(this.DryRunRestoreFolder);
            File.WriteAllBytes(Path.Combine(this.DryRunRestoreFolder, "stale.json.gz"), new byte[] { 1 });
            string compactionOutput = Path.Combine(this.cache.DryRunFolder, Datasets.OpraPillar);
            Directory.CreateDirectory(compactionOutput);
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore(dryRun: true).RestoreAsync(Name);

            Assert.IsFalse(File.Exists(Path.Combine(this.DryRunRestoreFolder, "stale.json.gz")));
            Assert.IsTrue(File.Exists(Path.Combine(this.DryRunRestoreFolder, Path.Combine(FullFile.Split('/')))));
            Assert.IsTrue(Directory.Exists(compactionOutput), "a compaction's dry run output stays");
        }

        [TestMethod]
        [DataRow(null, DisplayName = "no dry run folder")]
        [DataRow("Cache", DisplayName = "the dry run folder inside the cache")]
        [DataRow("Staging", DisplayName = "the dry run folder inside the staging folder")]
        [DataRow("Backups", DisplayName = "the dry run folder inside the backup folder")]
        [DataRow("", DisplayName = "the dry run folder holding the cache")]
        public void Constructor_DryRunFolderMissingOrOverlappingAnotherFolder_Throws(string parent)
        {
            string dryRunFolder = parent == null ? null : parent.Length == 0 ? this.cache.Folder : Path.Combine(this.cache.Folder, parent, "DryRun");

            if(dryRunFolder == null)
            {
                Assert.ThrowsException<ArgumentNullException>(() => this.CreateRestore(dryRun: true, change: options => options.DryRunFolder = dryRunFolder));
                return;
            }

            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(dryRun: true, change: options => options.DryRunFolder = dryRunFolder));
        }

        [TestMethod]
        public void Constructor_FoldersHoldingOneAnotherOrMissingSettings_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DatabentoCacheRestore(null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.StagingFolder = Path.Combine(this.cache.Root, "Staging")));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.Store = new LocalDirectoryBackupStore(Path.Combine(this.cache.Root, "Backups"))));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.CacheRoot = string.Empty));
            Assert.ThrowsException<ArgumentNullException>(() => this.CreateRestore(change: options => options.StagingFolder = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.Store = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.TimeProvider = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateRestore(change: options => options.ExistingFiles = (ExistingCacheFiles)7));
        }

        private static int FindBytes(byte[] bytes, byte[] pattern)
        {
            for(int i = 0; i + pattern.Length <= bytes.Length; ++i)
            {
                if(bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }

            throw new InvalidOperationException("Pattern not found.");
        }

        private static string[] Files(string folder)
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Asserts an I/O failure: a file or folder in the way fails a move with an <see cref="IOException"/> or, on Windows for a folder,
        /// an <see cref="UnauthorizedAccessException"/>
        /// </summary>
        private static async Task AssertThrowsIOAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            Assert.Fail("No I/O failure was thrown.");
        }

        private static async Task AssertCanceledAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch(OperationCanceledException)
            {
                return;
            }

            Assert.Fail("The restore was not canceled.");
        }

        private DatabentoCacheRestore CreateRestore(ICacheInUseChecker checker = null, bool dryRun = false, Action<CacheRestoreOptions> change = null)
        {
            CacheRestoreOptions options = new CacheRestoreOptions
            {
                CacheRoot = this.cache.Root,
                Store = new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = this.StagingFolder,
                InUseChecker = checker,
                DryRun = dryRun,
                DryRunFolder = this.cache.DryRunFolder,
                TimeProvider = new FakeTimeProvider(new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc)),
            };

            change?.Invoke(options);
            return new DatabentoCacheRestore(options);
        }

        /// <summary>
        /// Puts a tar into the store's folder as the store keeps one, with the given hash or else its own
        /// </summary>
        private void PutBackup(byte[] tar, string sha256 = null)
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, Name + ".tar"), tar);
            File.WriteAllText(Path.Combine(this.StoreFolder, Name + ".tar.sha256"), sha256 ?? BackupTarTestFiles.Sha256(tar));
        }

        private string CachePath(string relativePath)
        {
            return Path.Combine(this.cache.Root, Path.Combine(relativePath.Split('/')));
        }

        private string[] CacheFiles()
        {
            return Directory.Exists(this.cache.Root) ? Files(this.cache.Root) : Array.Empty<string>();
        }

        private Dictionary<string, byte[]> CacheContents()
        {
            return this.CacheFiles().ToDictionary(file => file, file => File.ReadAllBytes(this.CachePath(file)));
        }

        private void AssertCacheIs(Dictionary<string, byte[]> expected)
        {
            CollectionAssert.AreEqual(expected.Keys.ToArray(), this.CacheFiles(), "no file added or deleted, the marker included");
            foreach(KeyValuePair<string, byte[]> file in expected)
            {
                CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(this.CachePath(file.Key)), file.Key);
            }
        }

        private void WriteCacheFile(string relativePath, params byte[] bytes)
        {
            string path = this.CachePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
    }
}
