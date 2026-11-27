// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A read opens a day file by its name and lists a month folder's archive files only when the archive is needed, once per call, so the
    /// time a read takes does not grow with the number of day files in its folder.
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientListingTests
    {
        private static readonly DateTime NovemberBuild = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public async Task GetCbbo1mAsync_DaysInDayFiles_ListsNoMonthFolder()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 2));

                ICbboRecord[] call = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call }, DayStart, DayEnd, CacheMode.CacheOnly);
                ICbboRecord[] put = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Put }, DayStart, DayEnd, CacheMode.CacheOnly);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1)), ReceivedTimes(call));
                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Put, Day, 2)), ReceivedTimes(put));
                Assert.AreEqual(0, client.MonthFolderListings);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TwoArchivedSymbolsInOneCall_ListsTheMonthFolderOnce()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }), new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2) }));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd, CacheMode.CacheOnly);

                Assert.AreEqual(2, served.Length);
                Assert.AreEqual(1, client.MonthFolderListings);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ArchiveFileAddedAfterTheFolderWasListed_ServesTheDayFromIt()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call }, DayStart, DayEnd, CacheMode.CacheOnly);

                fixture.Cache.WriteCbboFile(NovemberBuild, 1, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2) }));
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Put }, DayStart, DayEnd, CacheMode.CacheOnly);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Put, Day, 2)), ReceivedTimes(served));
                Assert.AreEqual(2, client.MonthFolderListings);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFileCompactedBetweenTwoReads_IsServedFromTheArchive()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call }, DayStart, DayEnd, CacheMode.CacheOnly);

                ArchiveCompactionReport report = await new DatabentoArchiveCompactor(fixture.Cache.CreateOptions(fixture.Clock)).CompactAsync();
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call }, DayStart, DayEnd, CacheMode.CacheOnly);

                Assert.AreEqual(1, report.DayFilesDeleted);
                Assert.IsFalse(File.Exists(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)));
                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1)), ReceivedTimes(served));
                Assert.AreEqual(0, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DaysInDayFiles_FindsThemWithoutListingTheMonthFolder()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 2));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart, DayEnd);

                Assert.AreEqual(2, report.AlreadyCached);
                Assert.AreEqual(0, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(0, client.MonthFolderListings);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayNeitherInADayFileNorTheArchive_IsNotCached()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                Directory.SetLastWriteTimeUtc(fixture.Cache.CbboDirectory, DateTime.UtcNow.AddHours(-1));

                await Assert.ThrowsExceptionAsync<NotCachedException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Put }, DayStart, DayEnd, CacheMode.CacheOnly));
                Assert.AreEqual(1, client.MonthFolderListings);
            }
        }
    }
}
