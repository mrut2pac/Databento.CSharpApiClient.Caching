// Ignore Spelling: Cbbo

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// EnsureCachedAsync finds the cached symbol-days by day file name and archive index, reading no record, downloads the others in as
    /// few requests as the time-series calls would, saves them under the same rules, and reports what it found, saved and left out
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientEnsureCachedTests
    {
        private const string Third = "SPXW  261016C06605000";

        private static readonly string[] Symbols = { Call, Put, Third };

        [TestMethod]
        public async Task EnsureCachedAsync_DaysCached_ReadsNoRecordAndAsksNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    // a read of either would fail: the day file holds no gzip, the archive's would be logged as served
                    string dayFile = fixture.Cache.WriteUnreadableCbbo(Call, Day);
                    fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 1) }));

                    EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart, DayEnd);

                    Assert.AreEqual(2, report.AlreadyCached);
                    Assert.AreEqual(0, report.Saved);
                    Assert.AreEqual(0, report.NotCached.Count);
                    Assert.AreEqual(0, Asked(fixture), "neither downloaded nor any metadata asked");
                    Assert.IsTrue(File.Exists(dayFile), "a read would have deleted it as unreadable");
                    Assert.AreEqual(0, logger.Entries.Count(entry => entry.EventId.Id == 1002 || entry.EventId.Id == 1003), "no day served from a day file or the archive");
                }
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DaysMissing_DownloadsThemInChunksAndATimeseriesCallThenServesThemFromTheCache()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.MaxSymbolsPerRequest = 2))
            {
                fixture.Databento.List(Third);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2), Cbbo(Third, Day, 3));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayStart, DayEnd);

                Assert.AreEqual(0, report.AlreadyCached);
                Assert.AreEqual(3, report.Saved);
                Assert.AreEqual(0, report.NotCached.Count);
                CollectionAssert.AreEqual(new[] { 2, 1 }, fixture.Databento.TimeseriesRequests.Select(request => request.Symbols.Length).ToArray());

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd, CacheMode.CacheOnly);

                Assert.AreEqual(3, served.Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_SomeDaysCached_AsksOnlyForTheOthers()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Put, Day, 2));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart, DayEnd);

                Assert.AreEqual(1, report.AlreadyCached);
                Assert.AreEqual(1, report.Saved);
                CollectionAssert.AreEqual(new[] { Put }, fixture.Databento.TimeseriesRequests.Single().Symbols.ToArray());
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DayAnotherRequestIsDownloading_WaitsForItAndReadsNoRecord()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                    TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    fixture.Databento.BeforeTimeseries = () => gate.Task;

                    Task<ICbboRecord[]> downloading = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                    await WaitForAsync(() => fixture.Databento.TimeseriesRequests.Count == 1);
                    Task<EnsureCachedReport> ensuring = client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call }, DayStart, DayEnd);
                    await WaitForAsync(() => logger.Find(LogLevel.Debug, "Waiting for another request").Length == 1);
                    logger.Clear();
                    gate.SetResult();
                    await downloading;
                    EnsureCachedReport report = await ensuring;

                    Assert.AreEqual(1, report.Saved, "saved by the other request");
                    Assert.AreEqual(0, report.NotCached.Count);
                    Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                    Assert.AreEqual(0, logger.Entries.Count(entry => entry.EventId.Id == 1002 || entry.EventId.Id == 1003), "no day served from a day file or the archive");
                }
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DayNotSettled_IsReportedNotCachedAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Clock.SetUtcNow(Day.AddDays(1).AddMinutes(5));
                fixture.Databento.AvailableEndUtc = Day.AddDays(1);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call }, DayStart, DayEnd);

                Assert.AreEqual(0, report.Saved);
                Assert.AreEqual(1, report.NotSettled);
                Assert.AreEqual(0, report.PastAvailableEnd);
                CollectionAssert.AreEqual(new[] { new KeyValuePair<string, DateTime>(Call, Day) }, report.NotCached.ToArray());
                Assert.IsFalse(File.Exists(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)));
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DaysPastTheAvailableEnd_SavesTheDaysBeforeAndReportsThem()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                DateTime today = Day.AddDays(2);
                fixture.Clock.SetUtcNow(today.AddHours(12));
                fixture.Databento.AvailableEndUtc = today.AddHours(6);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day.AddDays(1), 1));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call }, DayStart, new DateTimeOffset(today.AddDays(1)));

                Assert.AreEqual(2, report.Saved);
                Assert.AreEqual(1, report.PastAvailableEnd);
                Assert.AreEqual(0, report.NotSettled, "a day past the end is counted there, though it has not settled either");
                CollectionAssert.AreEqual(new[] { new KeyValuePair<string, DateTime>(Call, today) }, report.NotCached.ToArray());
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_DaysPastTheAvailableEndWithThrow_SavesTheDaysBeforeAndThrows()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.AvailableEndRefusal = AvailableEndRefusal.Throw))
            {
                DateTime today = Day.AddDays(2);
                fixture.Clock.SetUtcNow(today.AddHours(12));
                fixture.Databento.AvailableEndUtc = today.AddHours(6);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day.AddDays(1), 1));

                await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call }, DayStart, new DateTimeOffset(today.AddDays(1))));

                Assert.IsTrue(File.Exists(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)));
                Assert.IsTrue(File.Exists(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day.AddDays(1))));
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_EmptyDays_SavesTheNotListedOneAndReportsTheListedOne()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                // no records for Put or Third: Put is listed, so its empty day is not saved; Third is not listed, so its empty day is
                fixture.Databento.List(Call, Put);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                EnsureCachedReport report = await client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayStart, DayEnd);

                Assert.AreEqual(2, report.Saved, "the call's day, and the empty day of the symbol not listed");
                Assert.AreEqual(0, report.NotSettled);
                Assert.AreEqual(0, report.PastAvailableEnd);
                CollectionAssert.AreEqual(new[] { new KeyValuePair<string, DateTime>(Put, Day) }, report.NotCached.ToArray());
            }
        }

        [TestMethod]
        public async Task EnsureCachedAsync_SchemaNotCached_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                ArgumentException refused = await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.EnsureCachedAsync(Datasets.OpraPillar, "mbo", new[] { Call }, DayStart, DayEnd));

                Assert.AreEqual("schema", refused.ParamName);
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.EnsureCachedAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { "ALL_SYMBOLS" }, DayStart, DayEnd));
                Assert.AreEqual(0, Asked(fixture));
            }
        }

        /// <summary>
        /// Counts the requests to Databento: time series, symbology, dataset range and conditions
        /// </summary>
        private static int Asked(CachedClientFixture fixture)
        {
            return new[] { "timeseries.get_range", "symbology.resolve", "metadata.get_dataset_range", "metadata.list_conditions" }.Sum(path => fixture.Databento.RequestsTo(path).Count);
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            // generous, so a slow build machine does not fail it
            for(int i = 0; i < 1000 && !condition(); ++i)
            {
                await Task.Delay(10);
            }

            Assert.IsTrue(condition());
        }

        [TestMethod]
        public void EnsureCached_ThroughTheExtension_IsTheAsyncCallsAnswer()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                EnsureCachedReport report = ((ICachedDatabentoClient)client).EnsureCached(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call }, DayStart, DayEnd);

                Assert.AreEqual(1, report.AlreadyCached);
            }
        }
    }
}
