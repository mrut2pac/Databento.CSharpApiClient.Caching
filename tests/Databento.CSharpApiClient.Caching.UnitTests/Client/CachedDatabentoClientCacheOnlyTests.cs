using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A <see cref="CacheMode.CacheOnly"/> call serves what the cache holds and never asks Databento anything, not even the free metadata
    /// and symbology calls; a symbol-day the cache lacks fails the call with <see cref="NotCachedException"/>.
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientCacheOnlyTests
    {
        [TestMethod]
        public async Task GetCbbo1mAsync_EveryDayCached_IsServedWithoutAskingDatabento()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1), Cbbo(Call, Day, 2));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 3));
                RefuseEveryRequest(fixture);

                ICbboRecord[] served = await client.GetCbbo1mAsync("OPRA.PILLAR", new[] { Call, Put }, DayStart, DayEnd, CacheMode.CacheOnly);

                Assert.AreEqual(3, served.Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SomeDaysMissing_ThrowsNamingEachMissingSymbolDay_AndAsksNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                RefuseEveryRequest(fixture);

                NotCachedException ex = await Assert.ThrowsExceptionAsync<NotCachedException>(() => client.GetCbbo1mAsync("OPRA.PILLAR", new[] { Call, Put }, DayStart, DayStart.AddDays(2), CacheMode.CacheOnly));

                Assert.AreEqual("OPRA.PILLAR", ex.Dataset);
                Assert.AreEqual("cbbo-1m", ex.Schema);
                Assert.AreEqual(3, ex.MissingDayCount);
                CollectionAssert.AreEqual(new[] { Day.AddDays(1) }, ex.MissingDays[Call].ToArray());
                CollectionAssert.AreEqual(new[] { Day, Day.AddDays(1) }, ex.MissingDays[Put].ToArray());
                StringAssert.Contains(ex.Message, Put + " 2026-10-16");
                Assert.IsFalse(File.Exists(fixture.DayFilePath("cbbo-1m", Put, Day)), "nothing is saved");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayCached_IsServedEmpty()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile<CbboRecordJson>(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day);
                RefuseEveryRequest(fixture);

                ICbboRecord[] served = await client.GetCbbo1mAsync("OPRA.PILLAR", Call, DayStart, DayEnd, CacheMode.CacheOnly);

                Assert.AreEqual(0, served.Length);
            }
        }

        [TestMethod]
        public void GetCbbo1m_ThroughTheInterfacesSyncExtension_ThrowsTheSame()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                RefuseEveryRequest(fixture);
                ICachedDatabentoClient cached = client;

                NotCachedException ex = Assert.ThrowsException<NotCachedException>(() => cached.GetCbbo1m("OPRA.PILLAR", Call, DayStart, DayEnd, CacheMode.CacheOnly));

                Assert.AreEqual(1, ex.MissingDayCount);
            }
        }

        [TestMethod]
        public void NotCachedException_ManyDays_NamesTheFirstTenAndCountsTheRest()
        {
            NotCachedException ex = new NotCachedException("OPRA.PILLAR", "cbbo-1m", Enumerable.Range(0, 12).Select(offset => new KeyValuePair<string, DateTime>(Call, Day.AddDays(offset))));

            Assert.AreEqual(12, ex.MissingDayCount);
            StringAssert.Contains(ex.Message, "and 2 more");
            StringAssert.Contains(ex.Message, Call + " 2026-10-16");
            Assert.IsFalse(ex.Message.Contains("2026-10-27", StringComparison.Ordinal), "the 11th day is counted, not named");
        }

        private static void RefuseEveryRequest(CachedClientFixture fixture)
        {
            fixture.Databento.Respond = request => throw new HttpRequestException("A cache-only call asked Databento: " + request.Path);
        }
    }
}
