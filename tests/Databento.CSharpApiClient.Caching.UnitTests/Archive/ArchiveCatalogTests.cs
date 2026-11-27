// Ignore Spelling: Cbbo

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Archive
{
    /// <summary>
    /// The catalog keeps each archive file it reads open for the next read, and closes it once no version it holds lists the file: a
    /// new full file or delta generation, the least recently used version beyond the cap, a file found unreadable, or the catalog disposed.
    /// </summary>
    [TestClass]
    public sealed class ArchiveCatalogTests
    {
        private const string Call = "SPXW  261016C06600000";

        private const string Put = "SPXW  261016P06600000";

        private static readonly DateTime October16 = new DateTime(2026, 10, 16);

        private static readonly DateTime FullBuiltUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly CbboArchiveSchema Schema = new CbboArchiveSchema(RType.Cbbo1M);

        private CacheFixture cache;

        private ArchiveCatalog catalog;

        [TestInitialize]
        public void Initialize()
        {
            this.cache = new CacheFixture();
            this.catalog = new ArchiveCatalog(new FakeTimeProvider(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc)), (path, ex) => { }, NullLogger.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.catalog.Dispose();
            this.cache.Dispose();
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_FileRead_IsKeptOpenForTheNextRead()
        {
            string full = this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2), Day(Put, 3));

            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);
            Assert.AreEqual(3, (await this.ReadAsync(Put)).Length);

            Assert.AreEqual(1, this.catalog.OpenFiles);
            AssertHeldOpen(full, true);
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_NewDeltaGeneration_ClosesTheDeltaItReplacesAndKeepsTheFullFile()
        {
            string full = this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            string firstDelta = this.cache.WriteCbboFile(FullBuiltUtc, 1, Day(Put, 3));
            Assert.AreEqual(3, (await this.ReadAsync(Put)).Length);
            Assert.AreEqual(2, this.catalog.OpenFiles);

            this.cache.WriteCbboFile(FullBuiltUtc, 2, Day(Put, 4));

            Assert.AreEqual(4, (await this.ReadAsync(Put)).Length);
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);
            Assert.AreEqual(2, this.catalog.OpenFiles, "the full file and the new delta");
            AssertHeldOpen(firstDelta, false);
            AssertHeldOpen(full, true);
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_NewFullFile_ClosesTheOldOne()
        {
            string old = this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);

            this.cache.WriteCbboFile(FullBuiltUtc.AddDays(1), 0, Day(Call, 5));

            Assert.AreEqual(5, (await this.ReadAsync(Call)).Length);
            Assert.AreEqual(1, this.catalog.OpenFiles);
            AssertHeldOpen(old, false);
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_MoreGroupMonthsThanKept_ClosesTheLeastRecentlyUsed()
        {
            List<string> files = new List<string>();
            for(int i = 0; i <= ArchiveCatalog.MaxCachedVersions; ++i)
            {
                DateTime date = new DateTime(2020 + (i / 12), (i % 12) + 1, 16, 0, 0, 0, DateTimeKind.Utc);
                ArchiveFileIdentity identity = new ArchiveFileIdentity(CacheFixture.Opra, CacheFixture.Cbbo, "SPXW.OPT", date.Year, date.Month, "UTC", date.AddMonths(1));
                files.Add(ArchiveTestRecords.WriteFile(Schema, this.cache.MonthDirectory(CacheFixture.Opra, CacheFixture.Cbbo, date.Year, date.Month), identity, new ArchiveSymbolDay<ICbboRecord>(Call, date, CacheFixture.CreateCbboRecords(1))));
                Assert.AreEqual(1, (await this.ReadAsync(Call, date)).Length);
            }

            Assert.AreEqual(ArchiveCatalog.MaxCachedVersions, this.catalog.OpenFiles);
            AssertHeldOpen(files[0], false);
            AssertHeldOpen(files[^1], true);
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_FileReplacedByOneThatCannotBeRead_ClosesIt()
        {
            string full = this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);

            File.WriteAllBytes(full, new byte[] { 1, 2, 3 });

            Assert.IsNull(await this.ReadAsync(Call));
            Assert.AreEqual(0, this.catalog.OpenFiles);
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_DeltaReplacedByOneThatCannotBeRead_OpensTheFullFileAgain()
        {
            this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            string delta = this.cache.WriteCbboFile(FullBuiltUtc, 1, Day(Put, 3));
            Assert.AreEqual(3, (await this.ReadAsync(Put)).Length);

            // the delta found unreadable drops the version the full file was taken from, closing it
            File.WriteAllBytes(delta, new byte[] { 1, 2, 3 });

            // the first read is served through a handle of its own; the next finds the full file closed and opens it again
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);
            Assert.AreEqual(1, this.catalog.OpenFiles);
        }

        [TestMethod]
        public async Task ContainsSymbolDayAsync_FileListed_KeepsItOpenForTheRead()
        {
            this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));

            Assert.IsTrue(await this.catalog.ContainsSymbolDayAsync(Schema, this.GetFolder(October16), Call, October16, strict: true));
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);

            Assert.AreEqual(1, this.catalog.OpenFiles);
        }

        [TestMethod]
        public async Task Dispose_FilesOpen_ClosesThemAndRefusesReads()
        {
            string full = this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            Assert.AreEqual(2, (await this.ReadAsync(Call)).Length);

            this.catalog.Dispose();

            Assert.AreEqual(0, this.catalog.OpenFiles);
            AssertHeldOpen(full, false);
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => this.ReadAsync(Call));
        }

        [TestMethod]
        public async Task ReadSymbolDayAsync_VersionsReplacedDuringReads_ServesEveryRead()
        {
            const int Generations = 50;
            this.cache.WriteCbboFile(FullBuiltUtc, 0, Day(Call, 2));
            int published = 0;
            Task<int>[] readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                int reads = 0;
                while(Volatile.Read(ref published) < Generations)
                {
                    Assert.IsNotNull(await this.ReadAsync(Call));
                    ++reads;
                }

                return reads;
            })).ToArray();

            // each delta generation replaces the version the reads hold, closing the delta before it; published whole, as a compaction does
            for(int generation = 1; generation <= Generations; ++generation)
            {
                string written = ArchiveTestRecords.WriteFile(Schema, this.cache.Folder, new ArchiveFileIdentity(CacheFixture.Opra, CacheFixture.Cbbo, "SPXW.OPT", 2026, 10, "UTC", FullBuiltUtc, generation), Day(Call, 3));
                File.Move(written, Path.Combine(this.cache.CbboDirectory, Path.GetFileName(written)));
                Interlocked.Increment(ref published);
                await Task.Delay(1);
            }

            Assert.IsTrue((await Task.WhenAll(readers)).All(reads => reads > 0), "every reader read while versions were replaced");
        }

        private static ArchiveSymbolDay<ICbboRecord> Day(string symbol, int records)
        {
            return new ArchiveSymbolDay<ICbboRecord>(symbol, October16, CacheFixture.CreateCbboRecords(records));
        }

        /// <summary>
        /// Asserts whether a handle holds the file open, which only Windows tells: it refuses an open that shares nothing
        /// </summary>
        private static void AssertHeldOpen(string path, bool expected)
        {
            if(!OperatingSystem.IsWindows())
            {
                return;
            }

            bool held = false;
            try
            {
                using(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }
            }
            catch(IOException)
            {
                held = true;
            }

            Assert.AreEqual(expected, held, path);
        }

        private CacheMonthFolder GetFolder(DateTime date)
        {
            return new CacheMonthFolder(this.cache.Root, CacheFixture.Opra, CacheFixture.Cbbo, date.Year, date.Month, CacheMetadata.UtcCutoff);
        }

        private Task<CbboRecordDbn[]> ReadAsync(string symbol, DateTime? date = null)
        {
            DateTime day = date ?? October16;
            return this.catalog.ReadSymbolDayAsync(Schema, this.GetFolder(day), symbol, day, logServed: false);
        }
    }
}
