using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// What a backup tells its logger: each step at Information, a refusal, a left staging tar and a retention that cannot delete as
    /// warnings, and a failure as an error
    /// </summary>
    [TestClass]
    public class DatabentoCacheBackupLoggingTests
    {
        private const string Name = "20261119T021530";

        private static readonly DateTime NowUtc = new DateTime(2026, 11, 19, 2, 15, 30, 500, DateTimeKind.Utc);

        private CacheFixture cache;

        private FakeLogger logger;

        private string StoreFolder => Path.Combine(this.cache.Folder, "Backups");

        private string StagingFolder => Path.Combine(this.cache.Folder, "Staging");

        [TestInitialize]
        public void TestInitialize()
        {
            this.cache = new CacheFixture();
            this.logger = new FakeLogger();
            this.WriteCacheFile(Path.Combine("OPRA.PILLAR", "cbbo-1m", "2026", "10", "SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz"), 4);
            this.WriteCacheFile(Path.Combine("XNAS.ITCH", "ohlcv-1d", "2026", "10", "SPY_2026-10-16.UTC.json.gz"), 5);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.cache.Dispose();
        }

        [TestMethod]
        public async Task BackUp_Cache_LogsEachStepAtInformation()
        {
            this.WriteFakeBackup("20261101T000000");

            CacheBackupReport report = await this.CreateBackup(BackupRetention.KeepLast(1)).BackUpAsync();

            this.logger.AssertLogged(LogLevel.Information, "Backing up " + this.cache.Root, this.StoreFolder);
            this.logger.AssertLogged(LogLevel.Information, "Counted 2 day file(s)");
            this.logger.AssertLogged(LogLevel.Information, "Writing the backup tar", Name);
            this.logger.AssertLogged(LogLevel.Information, Invariant($"Wrote {report.FileCount} file(s)"), "reading the tar back");
            this.logger.AssertLogged(LogLevel.Information, "Checked the tar", "putting " + Name + " into " + this.StoreFolder);
            this.logger.AssertLogged(LogLevel.Information, "Deleted the old backup 20261101T000000");
            this.logger.AssertLogged(LogLevel.Information, "Backup " + Name + " is in " + this.StoreFolder, "1 old backup(s) deleted");
            Assert.IsTrue(this.logger.Entries.All(entry => entry.Level <= LogLevel.Information));
        }

        [TestMethod]
        public async Task BackUp_MoreDayFilesThanTheMax_LogsTheRefusalAsAWarning()
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(maxBackupDayFiles: 1).BackUpAsync());

            this.logger.AssertLogged(LogLevel.Warning, "Backing up " + this.cache.Root + " refused", "MaxBackupDayFiles");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task BackUp_LeftStagingTar_LogsItsDeleteAsAWarning()
        {
            Directory.CreateDirectory(this.StagingFolder);
            string left = Path.Combine(this.StagingFolder, "20261118T000000.tar.staging");
            File.WriteAllBytes(left, new byte[] { 1 });

            await this.CreateBackup().BackUpAsync();

            this.logger.AssertLogged(LogLevel.Warning, "Deleted " + left, "a stopped backup left");
        }

        [TestMethod]
        public async Task BackUp_RetentionCannotDelete_LogsAWarning()
        {
            this.WriteFakeBackup("20261101T000000");
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                RetentionFailure = new IOException("The store is read-only."),
            };

            await this.CreateBackup(BackupRetention.KeepLast(1), store: store).BackUpAsync();

            this.logger.AssertLogged(LogLevel.Warning, "The store is read-only.");
        }

        [TestMethod]
        public async Task BackUp_StoreRefusingThePut_LogsAnError()
        {
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                FailPuts = true,
            };

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateBackup(store: store).BackUpAsync());

            FakeLogEntry entry = this.logger.AssertLogged(LogLevel.Error, "Backing up " + this.cache.Root + " failed");
            Assert.IsInstanceOfType<IOException>(entry.Exception);
        }

        [TestMethod]
        public async Task BackUp_EmptyCacheRoot_LogsTheRefusalAsAWarning()
        {
            foreach(string entry in Directory.GetFileSystemEntries(this.cache.Root))
            {
                if(Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            await Assert.ThrowsExceptionAsync<IOException>(() => this.CreateBackup().BackUpAsync());

            this.logger.AssertLogged(LogLevel.Warning, "Backing up " + this.cache.Root + " refused", "holds no files");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task BackUp_LockHeldByACompaction_LogsTheRefusalAsAWarning()
        {
            using(CacheRootLock.Take(this.cache.Root, "the compaction"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup().BackUpAsync());
            }

            this.logger.AssertLogged(LogLevel.Warning, "Backing up " + this.cache.Root + " refused", "cannot be taken");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task BackUp_InvalidOperationThatIsNoRefusal_LogsAnError()
        {
            InvalidOperationException fault = new InvalidOperationException("The store's session was closed.");
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                Putting = () => throw fault,
            };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateBackup(store: store).BackUpAsync());

            Assert.AreSame(fault, this.logger.AssertLogged(LogLevel.Error, "Backing up " + this.cache.Root + " failed").Exception);
            this.logger.AssertNotLogged("refused");
        }

        [TestMethod]
        public async Task BackUp_Canceled_LogsTheCancelAtInformation()
        {
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => this.CreateBackup().BackUpAsync(cancellationToken: cancellation.Token));
            }

            this.logger.AssertLogged(LogLevel.Information, "Backing up " + this.cache.Root + " canceled");
            Assert.AreEqual(0, this.logger.Entries.Count(entry => entry.Level >= LogLevel.Warning));
        }

        [TestMethod]
        public async Task BackUp_StoreWhoseLocationThrows_BacksUpAsWithoutALogger()
        {
            FakeBackupStore store = new FakeBackupStore(new LocalDirectoryBackupStore(this.StoreFolder))
            {
                LocationFailure = new InvalidOperationException("The store has no location yet."),
            };

            CacheBackupReport report = await this.CreateBackup(store: store).BackUpAsync();

            Assert.AreEqual(Name, report.Name);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task BackUp_LoggerThrows_BacksUpAsWithout(int mode)
        {
            this.logger = FakeLogger.Failing(mode);

            CacheBackupReport report = await this.CreateBackup().BackUpAsync();

            Assert.AreEqual(Name, report.Name);
            Assert.AreEqual(2, report.DayFiles);
            Assert.IsTrue(File.Exists(Path.Combine(this.StoreFolder, Name + ".tar")));
        }

        private DatabentoCacheBackup CreateBackup(BackupRetention retention = null, int maxBackupDayFiles = 10_000, IBackupStore store = null)
        {
            return new DatabentoCacheBackup(new CacheBackupOptions
            {
                CacheRoot = this.cache.Root,
                Store = store ?? new LocalDirectoryBackupStore(this.StoreFolder),
                StagingFolder = this.StagingFolder,
                Retention = retention ?? BackupRetention.KeepLast(5),
                MaxBackupDayFiles = maxBackupDayFiles,
                TimeProvider = new FakeTimeProvider(NowUtc),
                Logger = this.logger,
            });
        }

        private void WriteCacheFile(string relativePath, params byte[] bytes)
        {
            string path = Path.Combine(this.cache.Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        private void WriteFakeBackup(string name)
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, name + ".tar"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(this.StoreFolder, name + ".tar.sha256"), "00");
        }
    }
}
