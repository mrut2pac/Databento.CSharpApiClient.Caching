// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// What a compaction run tells its logger: the run at Information, each group's files at Debug, retried and left deletes, unreadable
    /// day files and suspect days as warnings, and a failed group as an error
    /// </summary>
    [TestClass]
    public class DatabentoArchiveCompactorLoggingTests
    {
        private const string Call = "SPXW  261016C06600000";

        private const string Put = "SPXW  261016P06600000";

        private const string NewFullFile = "OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261119T021530.full.parquet";

        private static readonly DateTime October16 = new DateTime(2026, 10, 16);

        private static readonly DateTime NowUtc = new DateTime(2026, 11, 19, 2, 15, 30, DateTimeKind.Utc);

        private static readonly string GroupName = Path.Combine(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "2026", "10") + " SPXW.OPT";

        private CacheFixture cache;

        private FakeLogger logger;

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
        public async Task Compact_DayFiles_LogsTheRunAtInformationAndThePublishedFileAtDebug()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(Put, October16, 12.5);

            await this.CreateCompactor().CompactAsync();

            this.logger.AssertLogged(LogLevel.Information, "Compacting " + this.cache.Root + " in Delta mode: 1 group(s) with 2 day file(s) planned ...");
            this.logger.AssertLogged(LogLevel.Information, "Listing " + this.cache.Root + " to plan the compaction ...");
            this.logger.AssertLogged(LogLevel.Debug, "Compacting " + GroupName, "2 day file(s)");
            this.logger.AssertLogged(LogLevel.Debug, "Wrote ", NewFullFile);
            this.logger.AssertLogged(LogLevel.Debug, "Verified ", NewFullFile);
            this.logger.AssertLogged(LogLevel.Debug, "Published " + Path.Combine(this.cache.CbboDirectory, NewFullFile), "2 symbol-day(s)", "2 day file(s) absorbed");
            this.logger.AssertLogged(LogLevel.Information, "Compaction of " + this.cache.Root + " done", "1 group(s) compacted", "0 failed", "2 day file(s) absorbed");
        }

        [TestMethod]
        public async Task CompactAsync_DryRun_SaysSoInItsFirstLine()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);

            await this.CreateCompactor(options => options.DryRun = true).CompactAsync();

            this.logger.AssertLogged(LogLevel.Information, "Compacting " + this.cache.Root + " in Delta mode, as a dry run: 1 group(s) with 1 day file(s) planned ...");
        }

        [TestMethod]
        public async Task Compact_FileReadBackDiffersFromItsSource_LogsTheVerificationAndTheGroupAsErrors()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            DatabentoArchiveCompactor compactor = this.CreateCompactor();
            compactor.TemporaryFileWritten = _ => this.cache.SaveCbbo(Call, October16, 6601);

            await compactor.CompactAsync();

            FakeLogEntry[] errors = this.logger.Find(LogLevel.Error);
            Assert.AreEqual(1, errors.Length, "one line per failed group");
            StringAssert.Contains(errors[0].Message, "Compacting " + GroupName + " failed");
            StringAssert.Contains(errors[0].Message, "price");
            Assert.IsInstanceOfType<InvalidDataException>(errors[0].Exception);
            this.logger.AssertLogged(LogLevel.Information, "1 failed");
        }

        [TestMethod]
        public async Task Compact_DayFileWhoseFirstDeleteFails_LogsTheRetryAtDebug()
        {
            string dayFile = this.cache.SaveCbbo(Call, October16, 6600.5);
            DatabentoArchiveCompactor compactor = this.CreateCompactor();
            int deletes = 0;
            compactor.DeleteFile = path =>
            {
                if(++deletes == 1)
                {
                    throw new UnauthorizedAccessException("Access is denied.");
                }

                File.Delete(path);
            };

            await compactor.CompactAsync();

            this.logger.AssertLogged(LogLevel.Debug, "Could not delete " + dayFile, "attempt 1 of 2", "Access is denied.");
            Assert.AreEqual(0, this.logger.Entries.Count(entry => entry.Level >= LogLevel.Warning), "a delete the retry makes is no warning");
        }

        [TestMethod]
        public async Task Compact_DayFileNeverDeleted_LogsItLeftForTheNextRun()
        {
            string dayFile = this.cache.SaveCbbo(Call, October16, 6600.5);
            DatabentoArchiveCompactor compactor = this.CreateCompactor();
            compactor.DeleteFile = _ => throw new IOException("The file is in use.");

            await compactor.CompactAsync();

            this.logger.AssertLogged(LogLevel.Debug, "Could not delete " + dayFile, "attempt 1 of 2");
            this.logger.AssertLogged(LogLevel.Warning, "Left " + dayFile + " for the next run", "2 attempt(s)", "The file is in use.");
        }

        [TestMethod]
        public async Task Compact_UnreadableDayFile_LogsAWarning()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            string unreadable = this.cache.WriteUnreadableCbbo(Put, October16);

            await this.CreateCompactor().CompactAsync();

            this.logger.AssertLogged(LogLevel.Warning, "The day file " + unreadable, "cannot be read and is left out of the archive");
        }

        [TestMethod]
        public async Task Compact_SuspectDays_LogsOneWarningForTheGroup()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(Put, October16, 12.5);
            FakeArchiveDayCheck check = new FakeArchiveDayCheck
            {
                Suspects = days => days.DayFileDays.Select(day => new ArchiveDaySuspect(day, "any", 0, null)),
            };

            await this.CreateCompactor(options => options.DayChecks = new[] { check }).CompactAsync();

            FakeLogEntry[] warnings = this.logger.Find(LogLevel.Warning, "suspect day(s)");
            Assert.AreEqual(1, warnings.Length);
            StringAssert.Contains(warnings[0].Message, "2 suspect day(s) in " + GroupName);
            StringAssert.Contains(warnings[0].Message, "SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst");
        }

        [TestMethod]
        public async Task Compact_LeftTemporaryFile_LogsItsDelete()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            string temporary = Path.Combine(this.cache.CbboDirectory, NewFullFile.Replace("20261119T021530", "20261118T000000", StringComparison.Ordinal) + ".0123456789abcdef0123456789abcdef.tmp");
            File.WriteAllBytes(temporary, new byte[] { 1 });

            await this.CreateCompactor().CompactAsync();

            Assert.IsFalse(File.Exists(temporary));
            this.logger.AssertLogged(LogLevel.Information, "Deleted the temporary file " + temporary);
        }

        [TestMethod]
        public async Task Compact_LoggerAtInformation_FormatsNoDebugLine()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.logger.MinimumLevel = LogLevel.Information;

            await this.CreateCompactor().CompactAsync();

            Assert.AreNotEqual(0, this.logger.Entries.Count);
            Assert.IsTrue(this.logger.Entries.All(entry => entry.Level >= LogLevel.Information));
        }

        [TestMethod]
        public async Task Compact_Canceled_LogsTheCancelAtInformation()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            DatabentoArchiveCompactor compactor = this.CreateCompactor();
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                compactor.TemporaryFileWritten = _ => cancellation.Cancel();
                await compactor.CompactAsync(cancellation.Token);
            }

            this.logger.AssertLogged(LogLevel.Information, "Compaction of " + this.cache.Root + " canceled after 0 group(s) compacted");
            Assert.AreEqual(0, this.logger.Entries.Count(entry => entry.Level >= LogLevel.Warning));
        }

        [TestMethod]
        public async Task Compact_NoVerification_LogsTheFilePublishedUnchecked()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);

            await this.CreateCompactor(options => options.Verification = ArchiveVerification.None).CompactAsync();

            this.logger.AssertLogged(LogLevel.Debug, "Wrote ", NewFullFile, "not checked: Verification is None");
            this.logger.AssertNotLogged("Verified ");
        }

        [TestMethod]
        public async Task Compact_OnlyUnreadableDayFiles_LogsNothingPublished()
        {
            this.cache.WriteCbboFile(new DateTime(2026, 11, 18, 2, 15, 0, DateTimeKind.Utc), 0, CacheFixture.CbboDay(Put, 1));
            this.cache.WriteUnreadableCbbo(Put, October16);

            await this.CreateCompactor().CompactAsync();

            this.logger.AssertLogged(LogLevel.Debug, "Nothing to publish for " + GroupName);
            this.logger.AssertNotLogged("Published ");
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task Compact_LoggerThrows_CompactsAsWithout(int mode)
        {
            string dayFile = this.cache.SaveCbbo(Call, October16, 6600.5);
            this.logger = FakeLogger.Failing(mode);

            ArchiveCompactionReport report = await this.CreateCompactor().CompactAsync();

            Assert.AreEqual(1, report.GroupsCompacted);
            Assert.AreEqual(0, report.FailedGroups.Count);
            Assert.IsFalse(File.Exists(dayFile));
            Assert.AreEqual(6600.5, (await this.cache.ReadServedCbboAsync(Call, October16)).Single().Price);
        }

        [TestMethod]
        public async Task Compact_LockHeldByABackup_LogsTheRefusalAsAWarning()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);

            using(CacheRootLock.Take(this.cache.Root, "the backup"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateCompactor().CompactAsync());
            }

            this.logger.AssertLogged(LogLevel.Warning, "Compacting " + this.cache.Root + " refused", "cannot be taken");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task Compact_RestoreInProgress_LogsTheRefusalAsAWarning()
        {
            CacheRestoreMarker.Write(this.cache.Root, "20261119T021530", NowUtc);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateCompactor().CompactAsync());

            this.logger.AssertLogged(LogLevel.Warning, "Compacting " + this.cache.Root + " refused", "is being restored");
            Assert.AreEqual(0, this.logger.Find(LogLevel.Error).Length);
        }

        [TestMethod]
        public async Task Compact_RunFailingOutsideAGroup_LogsAnError()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            FakeTimeProvider clock = new FakeTimeProvider(NowUtc)
            {
                TimestampFailure = new InvalidOperationException("The clock stopped."),
            };

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateCompactor(options => options.TimeProvider = clock).CompactAsync());

            Assert.AreSame(thrown, this.logger.AssertLogged(LogLevel.Error, "Compacting " + this.cache.Root + " failed").Exception);
            this.logger.AssertNotLogged("refused");
        }

        private DatabentoArchiveCompactor CreateCompactor(Action<ArchiveCompactorOptions> configure = null)
        {
            ArchiveCompactorOptions options = this.cache.CreateOptions(new FakeTimeProvider(NowUtc));
            options.Logger = this.logger;
            configure?.Invoke(options);
            return new DatabentoArchiveCompactor(options);
        }
    }
}
