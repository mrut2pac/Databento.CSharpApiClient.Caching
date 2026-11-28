using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Backup;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.UnitTests.Backup;
using Databento.CSharpApiClient.DataModel.Dbn;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// The whole cache in one tar checked against its manifest, put into the store and restorable, the backups the retention does not keep
    /// deleted, a failure thrown with nothing left in the store, and a cache being restored, locked, or holding too many day files refused
    /// </summary>
    [TestClass]
    public class DatabentoCacheBackupTests
    {
        private const string Name = "20261119T021530";

        private const string FullFile = "OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet";

        private const string CallDayFile = "OPRA.PILLAR/cbbo-1m/2026/10/SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz";

        private const string OhlcvDayFile = "XNAS.ITCH/ohlcv-1d/2026/10/16/SPY_2026-10-16.UTC.dbn.zst";

        private static readonly DateTime NowUtc = new DateTime(2026, 11, 19, 2, 15, 30, 500, DateTimeKind.Utc);

        private CacheFixture cache;

        private string StoreFolder => Path.Combine(this.cache.Folder, "Backups");

        private string StagingFolder => Path.Combine(this.cache.Folder, "Staging");

        private string BackupTar => Path.Combine(this.StoreFolder, Name + ".tar");

        [TestInitialize]
        public void TestInitialize()
        {
            this.cache = new CacheFixture();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.cache.Dispose();
        }

        [TestMethod]
        public async Task BackUp_Cache_IsATarOfEveryFileAndItsManifest()
        {
            this.WriteCacheFiles();

            CacheBackupReport report = await this.CreateBackup().BackUpAsync();

            string[] cacheFiles = this.RelativeCacheFiles();
            CollectionAssert.Contains(cacheFiles, "metadata.json");
            List<KeyValuePair<string, byte[]>> entries = BackupTarTestFiles.ReadEntries(this.BackupTar);
            CollectionAssert.AreEqual(cacheFiles.Append("manifest.json").ToArray(), entries.Select(entry => entry.Key).ToArray(), "every file, then the manifest");
            foreach(string file in cacheFiles)
            {
                CollectionAssert.AreEqual(File.ReadAllBytes(this.CachePath(file)), entries.Single(entry => entry.Key == file).Value, file);
            }

            BackupManifest manifest = JsonSerializer.Deserialize<BackupManifest>(entries[^1].Value);
            Assert.AreEqual(new DateTime(2026, 11, 19, 2, 15, 30, DateTimeKind.Utc), manifest.RunStartUtc, "to the second, as the name");
            CollectionAssert.AreEqual(cacheFiles, manifest.Files.Select(file => file.Path).ToArray());
            foreach(BackupManifestFile file in manifest.Files)
            {
                byte[] bytes = File.ReadAllBytes(this.CachePath(file.Path));
                Assert.AreEqual(bytes.Length, file.Size, file.Path);
                Assert.AreEqual(BackupTarTestFiles.Sha256(bytes), file.Sha256, file.Path);
            }

            Assert.AreEqual(BackupTarTestFiles.Sha256(File.ReadAllBytes(this.BackupTar)) + "  " + Name + ".tar\n", File.ReadAllText(this.BackupTar + ".sha256"));
            Assert.AreEqual(Name, report.Name);
            Assert.AreEqual(cacheFiles.Length, report.FileCount);
            Assert.AreEqual(manifest.TotalBytes, report.TotalBytes);
            Assert.AreEqual(2, report.DayFiles, "the legacy day file and the day file; the archive file and metadata.json are no day files");
            Assert.AreEqual(0, report.OldBackupsDeleted);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(this.StagingFolder).Length, "the staging folder is emptied by the move");
            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Root, CacheRootLock.FileName)), "the lock is released");
        }

        [TestMethod]
        public async Task BackUp_CompactedCacheRestoredLater_ServesTheBackedUpDays()
        {
            DateTime october16 = new DateTime(2026, 10, 16);
            this.cache.SaveCbbo("SPXW  261016C06600000", october16, 6600.5, 6601.5);
            FakeTimeProvider clock = new FakeTimeProvider(NowUtc);
            await new DatabentoArchiveCompactor(this.cache.CreateOptions(clock)).CompactAsync();
            CbboRecordDbn[] served = await this.cache.ReadServedCbboAsync("SPXW  261016C06600000", october16);
            Assert.AreEqual(2, served.Length);
            await this.CreateBackup().BackUpAsync();

            foreach(string archiveFile in Directory.GetFiles(this.cache.Root, "*.parquet", SearchOption.AllDirectories))
            {
                File.Delete(archiveFile);
            }

            Assert.IsNull(await this.cache.ReadServedCbboAsync("SPXW  261016C06600000", october16), "the archive is gone");
            await new DatabentoCacheRestore(new CacheRestoreOptions
            {
                CacheRoot = this.cache.Root,
                Store = new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = this.StagingFolder,
            }).RestoreAsync(Name);

            CollectionAssert.AreEqual(served.Select(record => record.Price).ToArray(), (await this.cache.ReadServedCbboAsync("SPXW  261016C06600000", october16)).Select(record => record.Price).ToArray());
        }

        [TestMethod]
        public async Task BackUp_CacheOfDayFilesAndLegacyDayFilesRestoredLater_ServesBoth()
        {
            DateTime october16 = new DateTime(2026, 10, 16);
            string legacy = this.cache.SaveLegacyCbbo("SPXW  261016C06600000", october16, 6600.5);
            string dayFile = this.cache.SaveCbbo("SPXW  261016P06600000", october16, 12.5);
            await this.CreateBackup().BackUpAsync();
            File.Delete(legacy);
            File.Delete(dayFile);

            await new DatabentoCacheRestore(new CacheRestoreOptions
            {
                CacheRoot = this.cache.Root,
                Store = new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = this.StagingFolder,
            }).RestoreAsync(Name);

            Assert.AreEqual(6600.5, (await DayFile.ReadAsync(legacy, DayFileFormats.Cbbo1Min, CancellationToken.None)).Single().Price, "back in its month folder");
            Assert.AreEqual(12.5, (await DayFile.ReadAsync(dayFile, DayFileFormats.Cbbo1Min, CancellationToken.None)).Single().Price, "back in its day folder");
        }

        [TestMethod]
        public async Task BackUp_Version1Cache_LeavesItsMetadataAsItIs()
        {
            // a backup reads the cache and changes nothing in it
            this.WriteCacheFiles();
            byte[] version1 = BackupTarTestFiles.Version1Metadata.Value;
            File.WriteAllBytes(this.CachePath(CacheMetadata.FileName), version1);

            await this.CreateBackup().BackUpAsync();

            CollectionAssert.AreEqual(version1, File.ReadAllBytes(this.CachePath(CacheMetadata.FileName)));
            CollectionAssert.AreEqual(version1, BackupTarTestFiles.ReadEntries(this.BackupTar).Single(entry => entry.Key == CacheMetadata.FileName).Value, "backed up as it is");
        }

        [TestMethod]
        public async Task BackUp_StoreRefusingThePut_ThrowsAndDeletesNoBackup()
        {
            this.WriteCacheFiles();
            this.WriteFakeBackup("20261101T000000");
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                FailPuts = true,
            };

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1), store: store).BackUpAsync());

            this.AssertStoreHolds("20261101T000000");
            Assert.AreEqual(0, Directory.GetFileSystemEntries(this.StagingFolder).Length, "the staged tar is deleted");
        }

        [TestMethod]
        public async Task BackUp_TarAlreadyStaged_IsReplacedByTheNewOne()
        {
            // a tar a stopped backup of the same second left
            Directory.CreateDirectory(this.StagingFolder);
            File.WriteAllBytes(Path.Combine(this.StagingFolder, Name + ".tar.staging"), new byte[] { 1 });
            this.WriteCacheFiles();

            await this.CreateBackup().BackUpAsync();

            Assert.AreEqual(this.RelativeCacheFiles().Length + 1, BackupTarTestFiles.ReadEntries(this.BackupTar).Count);
        }

        [TestMethod]
        public async Task BackUp_LeftStagingTar_IsDeletedAndOtherFilesKept()
        {
            Directory.CreateDirectory(this.StagingFolder);
            File.WriteAllBytes(Path.Combine(this.StagingFolder, "20261118T021530.tar.staging"), new byte[] { 1 });
            string other = Path.Combine(this.StagingFolder, "Notes", "keep.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(other));
            File.WriteAllBytes(other, new byte[] { 1 });

            // a backup another store keeps here is no staged tar
            File.WriteAllBytes(Path.Combine(this.StagingFolder, "20261118T021530.tar"), new byte[] { 1 });
            this.WriteCacheFiles();

            await this.CreateBackup().BackUpAsync();

            Assert.IsFalse(File.Exists(Path.Combine(this.StagingFolder, "20261118T021530.tar.staging")));
            Assert.IsTrue(File.Exists(other));
            Assert.IsTrue(File.Exists(Path.Combine(this.StagingFolder, "20261118T021530.tar")));
        }

        [TestMethod]
        public async Task BackUp_MoreBackupsThanKept_DeletesTheOldestAndNothingElseOfTheFolder()
        {
            string[] older = Enumerable.Range(1, 3).Select(day => Invariant($"202611{day:00}T000000")).ToArray();
            foreach(string backup in older)
            {
                this.WriteFakeBackup(backup);
            }

            File.WriteAllBytes(Path.Combine(this.StoreFolder, "notes.txt"), new byte[] { 1 });
            this.WriteCacheFiles();

            CacheBackupReport report = await this.CreateBackup(retention: BackupRetention.KeepLast(2)).BackUpAsync();

            Assert.AreEqual(2, report.OldBackupsDeleted);
            this.AssertStoreHolds(older[2], Name);
            Assert.IsTrue(File.Exists(Path.Combine(this.StoreFolder, "notes.txt")));
        }

        [TestMethod]
        public async Task BackUp_CalendarRetention_KeepsTheMonthAndTheLastOfTheMonthBefore()
        {
            foreach(string backup in new[] { "20260915T000000", "20261001T000000", "20261031T000000", "20261101T000000" })
            {
                this.WriteFakeBackup(backup);
            }

            this.WriteCacheFiles();

            CacheBackupReport report = await this.CreateBackup(retention: BackupRetention.Calendar(1)).BackUpAsync();

            Assert.AreEqual(2, report.OldBackupsDeleted);
            this.AssertStoreHolds("20261031T000000", "20261101T000000", Name);
        }

        [TestMethod]
        public async Task BackUp_RootWithoutMetadataButFiles_IsRefusedAndKeepsTheOldBackups()
        {
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();
            File.Delete(Path.Combine(this.cache.Root, CacheMetadata.FileName));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1)).BackUpAsync());

            this.AssertStoreHolds("20261101T000000");
        }

        [TestMethod]
        public async Task BackUp_EmptyCacheRoot_FailsAndKeepsTheOldBackups()
        {
            // a wrongly mapped or recreated cache root must not push the good backups out one run at a time
            this.WriteFakeBackup("20261101T000000");
            File.Delete(Path.Combine(this.cache.Root, CacheMetadata.FileName));

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1)).BackUpAsync());

            this.AssertStoreHolds("20261101T000000");
        }

        [TestMethod]
        public async Task BackUp_NoCacheRoot_Throws()
        {
            Directory.Delete(this.cache.Root, recursive: true);

            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => this.CreateBackup().BackUpAsync());
        }

        [TestMethod]
        public async Task BackUp_MetadataOfAnotherVersion_IsRefused()
        {
            File.WriteAllText(Path.Combine(this.cache.Root, CacheMetadata.FileName), "{\"format_version\": 3, \"day_cutoff\": \"UTC\"}");

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateBackup().BackUpAsync());
        }

        [TestMethod]
        public async Task BackUp_DayFilesUpToTheMax_IsTaken()
        {
            this.WriteCacheFiles();

            CacheBackupReport report = await this.CreateBackup(maxBackupDayFiles: 2).BackUpAsync();

            Assert.AreEqual(2, report.DayFiles);
            Assert.IsTrue(File.Exists(this.BackupTar));
        }

        [TestMethod]
        public async Task BackUp_MoreDayFilesThanTheMax_IsRefusedAndTouchesNothing()
        {
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1), maxBackupDayFiles: 1).BackUpAsync());

            StringAssert.Contains(exception.Message, "MaxBackupDayFiles");
            this.AssertStoreHolds("20261101T000000");
            Assert.IsFalse(Directory.Exists(this.StagingFolder), "nothing written into staging");
        }

        [TestMethod]
        public async Task BackUp_MoreDayFilesThanTheMaxForced_TakesTheCacheAsItIs()
        {
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();

            CacheBackupReport report = await this.CreateBackup(retention: BackupRetention.KeepLast(1), maxBackupDayFiles: 1).BackUpAsync(force: true);

            CollectionAssert.AreEqual(this.RelativeCacheFiles().Append("manifest.json").ToArray(), BackupTarTestFiles.ReadEntries(this.BackupTar).Select(entry => entry.Key).ToArray(), "every file, day files included");
            Assert.AreEqual(2, report.DayFiles);
            Assert.AreEqual(1, report.OldBackupsDeleted, "a forced backup keeps the same retention");
            this.AssertStoreHolds(Name);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "not forced")]
        [DataRow(true, DisplayName = "forced")]
        public async Task BackUp_RestoreMarkerInTheCacheRoot_IsRefusedAndTouchesNothing(bool force)
        {
            // a half-restored cache must not become the newest backup and push a good one out
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();
            File.WriteAllText(Path.Combine(this.cache.Root, CacheRestoreMarker.FileName), "backup 20261101T000000");

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1)).BackUpAsync(force));

            StringAssert.Contains(exception.Message, "20261101T000000");
            this.AssertStoreHolds("20261101T000000");
        }

        [TestMethod]
        public async Task BackUp_LockHeldByACompaction_IsRefusedAndTouchesNothing()
        {
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();

            using(CacheRootLock.Take(this.cache.Root, "the compaction"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(retention: BackupRetention.KeepLast(1)).BackUpAsync());
            }

            this.AssertStoreHolds("20261101T000000");
        }

        [TestMethod]
        public async Task Compact_WhileABackupHoldsTheLock_IsRefused()
        {
            this.cache.SaveCbbo("SPXW  261016C06600000", new DateTime(2026, 10, 16), 6600.5);
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder));
            DatabentoArchiveCompactor compactor = new DatabentoArchiveCompactor(this.cache.CreateOptions(new FakeTimeProvider(NowUtc)));
            Exception refused = null;
            store.Putting = () => refused = Assert.ThrowsExceptionAsync<InvalidOperationException>(() => compactor.CompactAsync()).GetAwaiter().GetResult();

            await this.CreateBackup(store: store).BackUpAsync();

            Assert.IsNotNull(refused);
            StringAssert.Contains(refused.Message, CacheRootLock.FileName);
        }

        [TestMethod]
        [DataRow("io", DisplayName = "an I/O failure")]
        [DataRow("other", DisplayName = "any other failure, as a remote store's")]
        [DataRow("canceled", DisplayName = "a cancellation")]
        public async Task BackUp_RetentionFailing_LeavesTheOldBackupsAndTheBackupStands(string failure)
        {
            this.WriteFakeBackup("20261101T000000");
            this.WriteCacheFiles();
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                RetentionFailure = failure switch
                {
                    "io" => new IOException("The store is offline."),
                    "canceled" => new OperationCanceledException(),
                    _ => new InvalidOperationException("The store refuses."),
                },
            };

            CacheBackupReport report = await this.CreateBackup(retention: BackupRetention.KeepLast(1), store: store).BackUpAsync();

            Assert.AreEqual(0, report.OldBackupsDeleted);
            this.AssertStoreHolds("20261101T000000", Name);
        }

        [TestMethod]
        public async Task BackUp_StoreThatCopiesTheTar_LeavesNothingInStaging()
        {
            this.WriteCacheFiles();
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                CopyPuts = true,
            };

            await this.CreateBackup(store: store).BackUpAsync();

            this.AssertStoreHolds(Name);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(this.StagingFolder).Length);
        }

        [TestMethod]
        public void Constructor_FoldersHoldingOneAnother_Throw()
        {
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(staging: Path.Combine(this.cache.Root, "Staging")));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(staging: this.cache.Folder));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(store: new LocalDirectoryBackupStore(Path.Combine(this.cache.Root, "Backups"))));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(store: new LocalDirectoryBackupStore(Path.Combine(this.StagingFolder, "Backups"))));
        }

        [TestMethod]
        public void Constructor_MissingOrOutOfRangeSettings_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DatabentoCacheBackup(null));
            Assert.ThrowsException<ArgumentNullException>(() => this.CreateBackup(change: options => options.CacheRoot = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(change: options => options.StagingFolder = string.Empty));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(change: options => options.Store = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(change: options => options.Retention = null));
            Assert.ThrowsException<ArgumentException>(() => this.CreateBackup(change: options => options.TimeProvider = null));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => this.CreateBackup(change: options => options.MaxBackupDayFiles = -1));
        }

        [TestMethod]
        public async Task Constructor_OptionsChangedAfter_AreNotSeen()
        {
            this.WriteCacheFiles();
            CacheBackupOptions options = this.CreateOptions(BackupRetention.KeepLast(5), 10_000, null, this.StagingFolder);
            DatabentoCacheBackup backup = new DatabentoCacheBackup(options);
            options.MaxBackupDayFiles = 0;

            CacheBackupReport report = await backup.BackUpAsync();

            Assert.AreEqual(Name, report.Name);
        }

        private DatabentoCacheBackup CreateBackup(BackupRetention retention = null, int maxBackupDayFiles = 10_000, IBackupStore store = null, string staging = null, Action<CacheBackupOptions> change = null)
        {
            CacheBackupOptions options = this.CreateOptions(retention ?? BackupRetention.KeepLast(5), maxBackupDayFiles, store, staging ?? this.StagingFolder);
            change?.Invoke(options);
            return new DatabentoCacheBackup(options);
        }

        private CacheBackupOptions CreateOptions(BackupRetention retention, int maxBackupDayFiles, IBackupStore store, string staging)
        {
            return new CacheBackupOptions
            {
                CacheRoot = this.cache.Root,
                Store = store ?? new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = staging,
                Retention = retention,
                MaxBackupDayFiles = maxBackupDayFiles,
                TimeProvider = new FakeTimeProvider(NowUtc),
            };
        }

        private string CachePath(string relativePath)
        {
            return Path.Combine(this.cache.Root, Path.Combine(relativePath.Split('/')));
        }

        private string[] RelativeCacheFiles()
        {
            return Directory.GetFiles(this.cache.Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(this.cache.Root, path).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Fills the cache with an archive file and two day files, beside its metadata.json
        /// </summary>
        private void WriteCacheFiles()
        {
            // an archive file, a legacy day file in its month folder and a day file in its day folder
            this.WriteCacheFile(FullFile, 1, 2, 3);
            this.WriteCacheFile(CallDayFile, 4);
            this.WriteCacheFile(OhlcvDayFile, 5);
        }

        private void WriteCacheFile(string relativePath, params byte[] bytes)
        {
            string path = this.CachePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Puts a stand-in backup into the store's folder, as the local store keeps one
        /// </summary>
        private void WriteFakeBackup(string name)
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, name + ".tar"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(this.StoreFolder, name + ".tar.sha256"), "00");
        }

        private void AssertStoreHolds(params string[] backups)
        {
            CollectionAssert.AreEqual(backups, new LocalDirectoryBackupStore(this.StoreFolder).ListAsync().GetAwaiter().GetResult().ToArray());
        }
    }
}
