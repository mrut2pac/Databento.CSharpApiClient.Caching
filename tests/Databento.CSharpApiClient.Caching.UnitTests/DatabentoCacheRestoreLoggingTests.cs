using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.UnitTests.Backup;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// What a restore tells its logger: each step and a dry run's outcome at Information, a refusal and a left restore folder as warnings,
    /// and a failure as an error
    /// </summary>
    [TestClass]
    public class DatabentoCacheRestoreLoggingTests
    {
        private const string Name = "20261119T021530";

        private static readonly KeyValuePair<string, byte[]>[] BackupFiles =
        {
            BackupTarTestFiles.Metadata,
            BackupTarTestFiles.Entry("OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", 1, 2, 3),
            BackupTarTestFiles.Entry("OPRA.PILLAR/cbbo-1m/2026/10/SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz", 4),
        };

        private CacheFixture cache;

        private FakeLogger logger;

        private string StoreFolder => Path.Combine(this.cache.Folder, "Backups");

        private string StagingFolder => Path.Combine(this.cache.Folder, "Staging");

        [TestInitialize]
        public void TestInitialize()
        {
            this.cache = new CacheFixture();
            this.logger = new FakeLogger();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.cache.Dispose();
        }

        [TestMethod]
        public async Task Restore_GoodBackup_LogsEachStepAtInformation()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            CacheRestoreReport report = await this.CreateRestore().RestoreAsync(Name);

            this.logger.AssertLogged(LogLevel.Information, "Restoring " + this.cache.Root + " from backup " + Name, this.StoreFolder, "existing files Keep", "dry run False");
            this.logger.AssertLogged(LogLevel.Information, "Unpacking " + Name + " into");
            this.logger.AssertLogged(LogLevel.Information, Invariant($"Checked {Name}: {report.FileCount} file(s)"));
            this.logger.AssertLogged(LogLevel.Information, "Swapping " + Name + " into " + this.cache.Root);
            this.logger.AssertLogged(LogLevel.Information, "Restored " + this.cache.Root + " from " + Name, Invariant($"{report.DayFilesKept} day file(s) kept"));
            Assert.IsTrue(this.logger.Entries.All(entry => entry.Level <= LogLevel.Information));
        }

        [TestMethod]
        public async Task Restore_CacheInUse_LogsTheRefusalAsAWarning()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateRestore(new FakeCacheInUseChecker(_ => true)).RestoreAsync(Name));

            this.logger.AssertLogged(LogLevel.Warning, "Restoring " + this.cache.Root + " from " + Name + " refused", "in use");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task Restore_DamagedBackup_LogsAnError()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles), sha256: new string('0', 64));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => this.CreateRestore().RestoreAsync(Name));

            FakeLogEntry entry = this.logger.AssertLogged(LogLevel.Error, "Restoring " + this.cache.Root + " from " + Name + " failed");
            Assert.IsInstanceOfType<InvalidDataException>(entry.Exception);
        }

        [TestMethod]
        public async Task Restore_LeftRestoreFolder_LogsItsDeleteAsAWarning()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            string left = Path.Combine(this.StagingFolder, "20261118T000000.restore");
            Directory.CreateDirectory(left);

            await this.CreateRestore().RestoreAsync(Name);

            this.logger.AssertLogged(LogLevel.Warning, "Deleted " + left, "did not finish");
        }

        [TestMethod]
        public async Task DryRun_GoodBackup_LogsTheOutcomeAtInformation()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await this.CreateRestore(dryRun: true).RestoreAsync(Name);

            this.logger.AssertLogged(LogLevel.Information, "Dry run of " + Name + " into " + Path.Combine(this.cache.DryRunFolder, Name), "a restore would delete");
            this.logger.AssertNotLogged("Swapping");
        }

        [TestMethod]
        public async Task Restore_NameThatIsNoBackups_LogsTheRefusalAsAWarning()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.CreateRestore().RestoreAsync("notes"));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => this.CreateRestore().RestoreAsync("20261101T000000"));

            this.logger.AssertLogged(LogLevel.Warning, "Restoring " + this.cache.Root + " from notes refused", "is not a backup's name");
            this.logger.AssertLogged(LogLevel.Warning, "Restoring " + this.cache.Root + " from 20261101T000000 refused", "holds no backup named");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task Restore_MoveAsideFolderAlreadyThere_LogsTheRefusalAsAWarning()
        {
            Directory.CreateDirectory(this.cache.Root + ".bak_20261124T120000");
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateRestore(existingFiles: ExistingCacheFiles.MoveAside).RestoreAsync(Name));

            this.logger.AssertLogged(LogLevel.Warning, "Restoring " + this.cache.Root + " from " + Name + " refused", "already exists");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task Restore_CheckerThrowingOnceTheMarkerIsIn_LogsAnError()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            FakeCacheInUseChecker checker = new FakeCacheInUseChecker(check => check == 2 ? throw new TimeoutException("The service did not answer.") : false);

            await Assert.ThrowsExceptionAsync<TimeoutException>(() => this.CreateRestore(checker).RestoreAsync(Name));

            Assert.IsInstanceOfType<TimeoutException>(this.logger.AssertLogged(LogLevel.Error, "Restoring " + this.cache.Root + " from " + Name + " failed").Exception);
            this.logger.AssertNotLogged("refused");
        }

        [TestMethod]
        public async Task Restore_Canceled_LogsTheCancelAtInformation()
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                try
                {
                    await this.CreateRestore().RestoreAsync(Name, cancellation.Token);
                    Assert.Fail("The restore was not canceled.");
                }
                catch(OperationCanceledException)
                {
                }
            }

            this.logger.AssertLogged(LogLevel.Information, "Restoring " + this.cache.Root + " from " + Name + " canceled");
            Assert.AreEqual(0, this.logger.Entries.Count(entry => entry.Level >= LogLevel.Warning));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task Restore_LoggerThrows_RestoresAsWithout(int mode)
        {
            this.PutBackup(BackupTarTestFiles.CreateTar(BackupFiles));
            this.logger = FakeLogger.Failing(mode);

            CacheRestoreReport report = await this.CreateRestore().RestoreAsync(Name);

            Assert.AreEqual(BackupFiles.Length, report.FileCount);
            Assert.IsTrue(File.Exists(Path.Combine(this.cache.Root, "OPRA.PILLAR", "cbbo-1m", "2026", "10", "SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet")));
            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Root, "RESTORE_IN_PROGRESS")));
        }

        private DatabentoCacheRestore CreateRestore(ICacheInUseChecker checker = null, bool dryRun = false, ExistingCacheFiles existingFiles = ExistingCacheFiles.Keep)
        {
            return new DatabentoCacheRestore(new CacheRestoreOptions
            {
                CacheRoot = this.cache.Root,
                Store = new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = this.StagingFolder,
                InUseChecker = checker,
                ExistingFiles = existingFiles,
                DryRun = dryRun,
                DryRunFolder = this.cache.DryRunFolder,
                TimeProvider = new FakeTimeProvider(new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc)),
                Logger = this.logger,
            });
        }

        private void PutBackup(byte[] tar, string sha256 = null)
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, Name + ".tar"), tar);
            File.WriteAllText(Path.Combine(this.StoreFolder, Name + ".tar.sha256"), sha256 ?? BackupTarTestFiles.Sha256(tar));
        }
    }
}
