// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.UnitTests.Archive;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// A compacted archive unpacked back into day files: every symbol-day its current version serves written as a DBN day file in its day
    /// folder, empty days included, and only then the version retired; a day file already there kept; a stop leaving both, and a rerun
    /// finishing
    /// </summary>
    [TestClass]
    public class DatabentoArchiveUnpackerTests
    {
        private const string Call = "SPXW  261016C06600000";

        private const string Put = "SPXW  261016P06600000";

        private const string SpyCall = "SPY   261016C00600000";

        private static readonly DateTime October15 = new DateTime(2026, 10, 15);

        private static readonly DateTime October16 = new DateTime(2026, 10, 16);

        private static readonly DateTime NowUtc = new DateTime(2026, 11, 19, 2, 15, 30, DateTimeKind.Utc);

        private CacheFixture cache;

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
        public async Task Unpack_CompactedCache_WritesBackEveryDayItAbsorbedAndRetiresTheArchive()
        {
            CbboRecordDbn[] callRecords = CacheFixture.CreateCbboRecords(3);
            this.cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, October16, callRecords);
            this.cache.SaveCbbo(Put, October16, 12.5);
            this.cache.SaveCbbo(Call, October15);
            await this.CompactAsync();
            Assert.AreEqual(0, this.cache.DayFiles(this.cache.CbboDirectory).Length);

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(1, report.GroupsUnpacked);
            Assert.AreEqual(0, report.FailedGroups.Count);
            Assert.AreEqual(3L, report.DayFilesWritten);
            Assert.AreEqual(0L, report.DayFilesKept);
            Assert.AreEqual(1L, report.ArchiveFilesDeleted);
            Assert.IsFalse(report.DryRun);
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length, "the archive is retired");
            Assert.AreEqual(this.cache.DayFiles(this.cache.CbboDirectory).Sum(path => new FileInfo(path).Length), report.DayFileBytes);
            ArchiveTestRecords.AssertSameValues<ICbboRecord>(callRecords, this.ReadDayFile(Call, October16), Call);
            Assert.AreEqual(12.5, this.ReadDayFile(Put, October16).Single().Price);
            Assert.AreEqual(0, this.ReadDayFile(Call, October15).Length, "an empty day stays an empty, final day");
        }

        [TestMethod]
        public async Task Unpack_DeltaOverAFullFile_WritesTheCopyTheArchiveServes()
        {
            this.cache.WriteCbboFile(NowUtc.AddDays(-1), 0, CbboDay(Call, 1), CbboDay(Put, 1));
            this.cache.WriteCbboFile(NowUtc.AddDays(-1), 1, CbboDay(Call, 2));

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(2L, report.DayFilesWritten);
            Assert.AreEqual(0L, report.DayFilesKept, "each symbol-day once, the full file's copy of a day the delta lists not even looked at");
            Assert.AreEqual(2L, report.ArchiveFilesDeleted, "the full file and its delta");
            Assert.AreEqual(2, this.ReadDayFile(Call, October16).Length, "the delta's copy");
            Assert.AreEqual(1, this.ReadDayFile(Put, October16).Length, "the full file's copy");
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_DayFileOrLegacyDayFileAlreadyThere_KeepsItAndRetiresTheArchive()
        {
            // a day downloaded again since the compaction, which is what the cache serves
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(Put, October16, 12.5);
            this.cache.SaveCbbo(Call, October15, 6601.5);
            await this.CompactAsync();
            this.cache.SaveCbbo(Call, October16, 7000.5);
            this.cache.SaveLegacyCbbo(Put, October16, 13.5);

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(1L, report.DayFilesWritten);
            Assert.AreEqual(2L, report.DayFilesKept);
            Assert.AreEqual(7000.5, this.ReadDayFile(Call, October16).Single().Price);
            Assert.AreEqual(13.5, this.ReadLegacyDayFile(Put, October16).Single().Price);
            Assert.IsFalse(File.Exists(this.DayFilePath(Put, October16)), "the legacy day file is what the cache serves");
            Assert.AreEqual(6601.5, this.ReadDayFile(Call, October15).Single().Price);
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_StoppedPartway_LeavesTheArchiveAndARerunFinishes()
        {
            this.cache.SaveCbbo(Call, October15, 6601.5);
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(Put, October16, 12.5);
            await this.CompactAsync();
            string[] archive = this.cache.CbboArchiveFileNames();
            using(CancellationTokenSource stop = new CancellationTokenSource())
            {
                DatabentoArchiveUnpacker stopped = this.CreateUnpacker();
                stopped.DayFileWritten = _ => stop.Cancel();

                ArchiveUnpackReport first = await stopped.UnpackAsync(stop.Token);

                Assert.IsTrue(first.Canceled);
                Assert.AreEqual(1L, first.DayFilesWritten);
                Assert.AreEqual(0L, first.ArchiveFilesDeleted);
                CollectionAssert.AreEqual(archive, this.cache.CbboArchiveFileNames(), "the archive is kept until every day file is written");
            }

            Assert.AreEqual(1, this.cache.DayFiles(this.cache.CbboDirectory).Length);
            Assert.IsFalse(File.Exists(Path.Combine(this.cache.Root, CacheRootLock.FileName)), "the lock is released");

            ArchiveUnpackReport rerun = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(2L, rerun.DayFilesWritten);
            Assert.AreEqual(1L, rerun.DayFilesKept, "the day file the stopped run wrote");
            Assert.AreEqual(1L, rerun.ArchiveFilesDeleted);
            Assert.AreEqual(6601.5, this.ReadDayFile(Call, October15).Single().Price);
            Assert.AreEqual(6600.5, this.ReadDayFile(Call, October16).Single().Price);
            Assert.AreEqual(12.5, this.ReadDayFile(Put, October16).Single().Price);
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_DryRun_ChangesNothingAndCountsTheBytesARealRunWrites()
        {
            this.cache.SaveCbbo(Call, October16, 6600.25, 6600.5);
            this.cache.SaveCbbo(Put, October16, 12.5);
            await this.CompactAsync();
            string[] archive = this.cache.CbboArchiveFileNames();

            ArchiveUnpackReport dryRun = await this.CreateUnpacker(options => options.DryRun = true).UnpackAsync();

            Assert.IsTrue(dryRun.DryRun);
            Assert.AreEqual(2L, dryRun.DayFilesWritten, "the day files it would write");
            Assert.AreEqual(0L, dryRun.ArchiveFilesDeleted);
            CollectionAssert.AreEqual(archive, this.cache.CbboArchiveFileNames());
            Assert.AreEqual(0, this.cache.DayFiles(this.cache.CbboDirectory).Length);
            Assert.IsTrue(dryRun.FreeBytes > 0, "the free space of the cache root's drive");

            ArchiveUnpackReport real = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(real.DayFileBytes, dryRun.DayFileBytes);
        }

        [TestMethod]
        public async Task Unpack_Scope_UnpacksOnlyTheGroupMonthsItSelects()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(SpyCall, October16, 6.5);
            this.cache.SaveCbbo(Call, new DateTime(2026, 9, 16), 6500.5);
            await this.CompactAsync();
            string september = this.cache.MonthDirectory(CacheFixture.Opra, CacheFixture.Cbbo, 2026, 9);

            ArchiveUnpackReport report = await this.CreateUnpacker(options =>
            {
                options.Dataset = CacheFixture.Opra;
                options.Schema = CacheFixture.Cbbo;
                options.Group = "SPXW.OPT";
                options.FirstMonth = new DateOnly(2026, 10, 1);
                options.LastMonth = new DateOnly(2026, 10, 31);
            }).UnpackAsync();

            Assert.AreEqual(1, report.GroupsUnpacked);
            Assert.AreEqual(6600.5, this.ReadDayFile(Call, October16).Single().Price);
            CollectionAssert.AreEqual(new[] { "OPRA.PILLAR_cbbo-1m_SPY.OPT_2026-10.UTC_20261119T021530.full.parquet" }, this.cache.CbboArchiveFileNames(), "another group of the month");
            Assert.AreEqual(1, this.cache.ArchiveFileNames(september).Length, "another month");
            Assert.AreEqual(1, this.cache.DayFiles(this.cache.Root).Length);
        }

        [TestMethod]
        public async Task Unpack_ScopeOfAnotherSchema_UnpacksNothing()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            await this.CompactAsync();

            ArchiveUnpackReport report = await this.CreateUnpacker(options => options.Schema = Schema.Tcbbo).UnpackAsync();

            Assert.AreEqual(0, report.GroupsUnpacked);
            Assert.AreEqual(1, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_LockHeldByAnotherRun_IsRefused()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            await this.CompactAsync();

            using(CacheRootLock.Take(this.cache.Root, "a test"))
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateUnpacker().UnpackAsync());
            }

            Assert.AreEqual(1, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_UnreadableDayFile_IsReplacedByTheArchivesCopy()
        {
            // the cache serves the archive's copy of a day whose day file cannot be parsed, so that copy must not go with the archive
            this.cache.SaveCbbo(Call, October16, 6600.5);
            await this.CompactAsync();
            this.cache.WriteUnreadableCbbo(Call, October16);

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(1L, report.DayFilesWritten);
            Assert.AreEqual(0L, report.DayFilesKept);
            Assert.AreEqual(6600.5, this.ReadDayFile(Call, October16).Single().Price);
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_UnreadableLegacyDayFile_GetsADayFileOfTheArchivesCopyReadBeforeIt()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            await this.CompactAsync();
            string legacy = this.cache.WriteLegacyCbboBytes(Call, October16, new byte[] { 1, 2, 3 });

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(1L, report.DayFilesWritten);
            Assert.AreEqual(6600.5, this.ReadDayFile(Call, October16).Single().Price);
            Assert.IsTrue(File.Exists(legacy), "left for the next compaction, which supersedes it");
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
        }

        [TestMethod]
        public async Task Unpack_DeltaOnlyDayAndEmptyDeltaCopyOverAFullOne_AreWrittenAsTheDeltaHasThem()
        {
            this.cache.WriteCbboFile(NowUtc.AddDays(-1), 0, CbboDay(Call, 2));
            this.cache.WriteCbboFile(NowUtc.AddDays(-1), 1, CbboDay(Call, 0), CbboDay(Put, 1));

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(2L, report.DayFilesWritten);
            Assert.AreEqual(0, this.ReadDayFile(Call, October16).Length, "the delta's empty copy");
            Assert.AreEqual(1, this.ReadDayFile(Put, October16).Length, "a day only the delta lists");
        }

        [TestMethod]
        public async Task Unpack_OlderFileOfTheGroupWhoseDaysAreAllDayFilesNow_IsRetiredWithTheVersion()
        {
            // a full file an earlier compaction could not delete, and a temporary file a stopped one left
            this.cache.WriteCbboFile(NowUtc.AddDays(-3), 0, CbboDay(Call, 1));
            string current = this.cache.WriteCbboFile(NowUtc.AddDays(-1), 0, CbboDay(Call, 2), CbboDay(Put, 1));
            string temporary = Path.Combine(this.cache.CbboDirectory, ArchiveFileName.GetTemporaryFileName(Path.GetFileName(current)));
            File.WriteAllBytes(temporary, new byte[] { 1 });

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(0, report.FailedGroups.Count);
            Assert.AreEqual(2L, report.ArchiveFilesDeleted, "the current full file and the older one");
            Assert.AreEqual(0, this.cache.CbboArchiveFileNames().Length);
            Assert.IsFalse(File.Exists(temporary));
            Assert.AreEqual(2, this.ReadDayFile(Call, October16).Length, "the current version's copy");
        }

        [TestMethod]
        public async Task Unpack_OlderFileHoldingADayWithoutADayFile_FailsTheGroupAndKeepsTheArchive()
        {
            // once the current version went, that older file would be the version served, with a day the current one no longer had
            const string OtherCall = "SPXW  261016C06700000";
            this.cache.WriteCbboFile(NowUtc.AddDays(-3), 0, CbboDay(Call, 1), CbboDay(OtherCall, 1));
            this.cache.WriteCbboFile(NowUtc.AddDays(-1), 0, CbboDay(Call, 2));
            string[] archive = this.cache.CbboArchiveFileNames();

            ArchiveUnpackReport report = await this.CreateUnpacker().UnpackAsync();

            Assert.AreEqual(1, report.FailedGroups.Count);
            StringAssert.Contains(report.FailedGroups[0].Exception.Message, OtherCall);
            Assert.AreEqual(0, report.GroupsUnpacked);
            CollectionAssert.AreEqual(archive, this.cache.CbboArchiveFileNames());
        }

        [TestMethod]
        public async Task Unpack_ArchiveFileThatCannotBeDeleted_IsLeftReportedAndWarned()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            await this.CompactAsync();
            FakeLogger logger = new FakeLogger();
            DatabentoArchiveUnpacker unpacker = this.CreateUnpacker(options => options.Logger = logger);
            unpacker.TimeProvider = new FakeTimeProvider(NowUtc);
            unpacker.DeleteFile = _ => throw new IOException("The file is in use.");

            ArchiveUnpackReport report = await unpacker.UnpackAsync();

            Assert.AreEqual(1, report.GroupsUnpacked);
            Assert.AreEqual(0L, report.ArchiveFilesDeleted);
            CollectionAssert.AreEqual(new[] { Path.Combine(CacheFixture.Opra, CacheFixture.Cbbo, "2026", "10", this.cache.CbboArchiveFileNames().Single()) }, report.FilesLeft.ToArray());
            Assert.AreEqual(6600.5, this.ReadDayFile(Call, October16).Single().Price);
            Assert.AreEqual(6008, logger.AssertLogged(Microsoft.Extensions.Logging.LogLevel.Warning, "left 1 archive file(s)").EventId.Id);
        }

        [TestMethod]
        public async Task Unpack_SeveralGroupsAtOnce_UnpacksEach()
        {
            this.cache.SaveCbbo(Call, October16, 6600.5);
            this.cache.SaveCbbo(SpyCall, October16, 6.5);
            this.cache.SaveCbbo(Call, new DateTime(2026, 9, 16), 6500.5);
            await this.CompactAsync();

            ArchiveUnpackReport report = await this.CreateUnpacker(options => options.MaxParallelGroups = 3).UnpackAsync();

            Assert.AreEqual(3, report.GroupsUnpacked);
            Assert.AreEqual(3L, report.DayFilesWritten);
            Assert.AreEqual(3, this.cache.DayFiles(this.cache.Root).Length);
            Assert.AreEqual(0, Directory.GetFiles(this.cache.Root, "*" + ArchiveFileName.Extension, SearchOption.AllDirectories).Length);
        }

        [TestMethod]
        public async Task Unpack_RestoreInProgress_IsRefused()
        {
            CacheRestoreMarker.Write(this.cache.Root, "20261119T021530", NowUtc);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateUnpacker().UnpackAsync());
        }

        [TestMethod]
        public async Task Unpack_RootWithoutMetadataButFiles_IsRefused()
        {
            File.Delete(Path.Combine(this.cache.Root, CacheMetadata.FileName));
            File.WriteAllText(Path.Combine(this.cache.Root, "notes.txt"), "x");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => this.CreateUnpacker().UnpackAsync());
        }

        [TestMethod]
        public async Task Unpack_NoCacheRoot_Throws()
        {
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => new DatabentoArchiveUnpacker(new ArchiveUnpackerOptions { CacheRoot = Path.Combine(this.cache.Folder, "missing") }).UnpackAsync());
        }

        [TestMethod]
        public void Constructor_OptionsOutOfRange_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DatabentoArchiveUnpacker(null));
            Assert.ThrowsException<ArgumentNullException>(() => new DatabentoArchiveUnpacker(new ArchiveUnpackerOptions()));
            Assert.ThrowsException<ArgumentException>(() => new DatabentoArchiveUnpacker(new ArchiveUnpackerOptions { CacheRoot = string.Empty }));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => this.CreateUnpacker(options => options.MaxParallelGroups = 0));
            Assert.ThrowsException<ArgumentException>(() => this.CreateUnpacker(options =>
            {
                options.FirstMonth = new DateOnly(2026, 11, 1);
                options.LastMonth = new DateOnly(2026, 10, 1);
            }));
        }

        private static ArchiveSymbolDay<ICbboRecord> CbboDay(string symbol, int records)
        {
            return new ArchiveSymbolDay<ICbboRecord>(symbol, October16, Enumerable.Range(1, records).Select(minute => (ICbboRecord)DayFileFormats.Cbbo1Min.RecordSchema.ToRecord(CacheFixture.CbboRecord(October16, 6600.5, minute))).ToArray());
        }

        private string DayFilePath(string symbol, DateTime date)
        {
            return DayFileName.GetPath(this.cache.MonthDirectory(CacheFixture.Opra, CacheFixture.Cbbo, date.Year, date.Month), symbol, date, CacheMetadata.UtcCutoff);
        }

        private CbboRecordDbn[] ReadDayFile(string symbol, DateTime date)
        {
            return DayFile.ReadAsync(this.DayFilePath(symbol, date), DayFileFormats.Cbbo1Min, CancellationToken.None).GetAwaiter().GetResult();
        }

        private CbboRecordDbn[] ReadLegacyDayFile(string symbol, DateTime date)
        {
            string path = DayFileName.GetLegacyPath(this.cache.MonthDirectory(CacheFixture.Opra, CacheFixture.Cbbo, date.Year, date.Month), symbol, date, CacheMetadata.UtcCutoff);
            return DayFile.ReadAsync(path, DayFileFormats.Cbbo1Min, CancellationToken.None).GetAwaiter().GetResult();
        }

        private Task<ArchiveCompactionReport> CompactAsync()
        {
            return new DatabentoArchiveCompactor(this.cache.CreateOptions(new FakeTimeProvider(NowUtc))).CompactAsync();
        }

        private DatabentoArchiveUnpacker CreateUnpacker(Action<ArchiveUnpackerOptions> configure = null)
        {
            ArchiveUnpackerOptions options = new ArchiveUnpackerOptions
            {
                CacheRoot = this.cache.Root,
            };
            configure?.Invoke(options);
            return new DatabentoArchiveUnpacker(options);
        }
    }
}
