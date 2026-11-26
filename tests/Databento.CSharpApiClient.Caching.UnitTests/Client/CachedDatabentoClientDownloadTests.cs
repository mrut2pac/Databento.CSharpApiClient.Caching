// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    [TestClass]
    public sealed class CachedDatabentoClientDownloadTests
    {
        private const string Unlisted = "SPXW  261016C09999000";

        private static Databento.CSharpApiClient.DataModel.Json.CbboRecordJson OneSecond(string symbol, DateTime receivedUtc)
        {
            Databento.CSharpApiClient.DataModel.Json.CbboRecordJson record = CbboAt(symbol, receivedUtc);
            record.Header.RecordType = RType.Cbbo1S;
            return record;
        }

        private static void AssertNoMetadataRequest(CachedClientFixture fixture)
        {
            Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count, "no dataset range is read");
            Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_condition").Count, "no condition is read");
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstDayNotFinal_IsServedAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                ICbboRecord[] first = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                fixture.Databento.Conditions.Remove(Day);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, first.Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstDayPastTheAvailableEnd_AsksOnlyUpToItAndSavesNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                fixture.Databento.AvailableEndUtc = Day.AddHours(18);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(DayStart.AddHours(18), fixture.Databento.TimeseriesRequests[0].EndUtc);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_CheckAvailabilityFirstDayAnotherSchemaFoundFinal_IsNotSavedPastThisSchemasEnd()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options =>
            {
                options.EmptyDays = EmptyDayPolicy.CacheAlways;
                options.CheckAvailabilityFirst = true;
            }))
            {
                fixture.Databento.SchemaEnds[Schema.ConsolidatedBBO1Sec] = Day.AddHours(12);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Sec, OneSecond(Call, Day.AddHours(9)));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(DayStart.AddHours(12), fixture.Databento.TimeseriesRequests[1].EndUtc);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec));
                Assert.IsNull(fixture.ReadCbboDayFile(Put, Day, Schema.ConsolidatedBBO1Sec));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstNothingAvailableYet_ServesNothingWithoutAsking()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                fixture.Databento.AvailableEndUtc = Day;

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(0, fixture.Databento.TimeseriesRequests.Count);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirst_AsksTheRangeAndTheConditions()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count);
                Assert.AreEqual(1, fixture.Databento.RequestsTo("metadata.get_dataset_condition").Count);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstTodaysDay_IsNotSavedThoughAvailable()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                // the data ends past the day and its condition is available, but the day has not ended
                fixture.Clock.SetUtcNow(Day.AddHours(20));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EndedDay_IsSavedWithoutAMetadataRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                AssertNoMetadataRequest(fixture);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TodaysDay_IsServedAndNotSavedWithoutAMetadataRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Clock.SetUtcNow(Day.AddHours(20));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] first = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, first.Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "a day not saved is asked for again");
                Assert.AreEqual(DayEnd, fixture.Databento.TimeseriesRequests[0].EndUtc, "the whole day is asked for");
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                AssertNoMetadataRequest(fixture);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayEndedOnceTheClockPassesMidnight_IsSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.SettleDelay = TimeSpan.Zero))
            {
                fixture.Clock.SetUtcNow(Day.AddHours(20));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                fixture.Clock.SetUtcNow(Day.AddDays(1));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayEndedWithinTheSettleDelay_IsServedAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.SettleDelay = TimeSpan.FromHours(1)))
            {
                fixture.Clock.SetUtcNow(Day.AddDays(1).AddMinutes(30));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                Databento.CSharpApiClient.DataModel.Json.CbboRecordJson[] savedWithinTheDelay = fixture.ReadCbboDayFile(Call, Day);
                fixture.Clock.SetUtcNow(Day.AddDays(1).AddHours(1));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.IsNull(savedWithinTheDelay, "ended 30 minutes ago, under the 1 hour delay");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "saved once ended 1 hour ago");
                AssertNoMetadataRequest(fixture);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DegradedDay_IsSavedWithoutAConditionCheck()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                AssertNoMetadataRequest(fixture);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusedAsNotFullyAvailable_ThrowsAndSavesNothingWithoutAMetadataRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.UnprocessableEntity, "data_schema_not_fully_available") : null;

                DatabentoHttpException thrown = await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd));

                Assert.AreEqual(DatabentoErrorCase.DataSchemaNotFullyAvailable, thrown.Code);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count, "not asked again");
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                Assert.IsNull(fixture.ReadCbboDayFile(Put, Day));
                AssertNoMetadataRequest(fixture);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstEndedDaysAndToday_AsksOnceUpToTheAvailableEnd()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                DateTime today = Day.AddDays(2);
                fixture.Clock.SetUtcNow(today.AddHours(12));
                fixture.Databento.AvailableEndUtc = today.AddHours(6);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day.AddDays(1), 1), CbboAt(Call, today.AddHours(3)));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, new DateTimeOffset(today.AddDays(1)));

                Assert.AreEqual(3, served.Length, "today's records up to the available end too");
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(new DateTimeOffset(today.AddHours(6)), fixture.Databento.TimeseriesRequests[0].EndUtc);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, today));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_LaterChunkRefused_KeepsTheEarlierChunksSavedAndSavesNothingOfTheRefusedOne()
        {
            const string Third = "SPXW  261016C06700000";
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options =>
            {
                options.MaxSymbolsPerRequest = 1;
                options.EmptyDays = EmptyDayPolicy.CacheAlways;
            }))
            {
                fixture.Databento.List(Third);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 1), Cbbo(Third, Day, 1));
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" && fixture.Databento.TimeseriesRequests.Count == 3 ? FakeDatabento.Error(HttpStatusCode.Forbidden, "license_not_found_unauthorized") : null;

                DatabentoHttpException thrown = await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put, Third }, DayStart, DayEnd));

                Assert.AreEqual(DatabentoErrorCase.LicenseNotFoundUnauthorized, thrown.Code);
                Assert.AreEqual(3, fixture.Databento.TimeseriesRequests.Count);
                string[] saved = fixture.Databento.TimeseriesRequests.Take(2).Select(request => request.Symbols.Single()).ToArray();
                string refused = fixture.Databento.TimeseriesRequests[2].Symbols.Single();
                CollectionAssert.AreEquivalent(new[] { Call, Put, Third }, saved.Append(refused).ToArray());
                Assert.IsTrue(saved.All(symbol => fixture.ReadCbboDayFile(symbol, Day)?.Length == 1), "the earlier chunks are on disk");
                Assert.IsNull(fixture.ReadCbboDayFile(refused, Day), "not even as empty");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassCachedSettledDayAndToday_ReplacesTheSettledDayAndSavesNothingOfToday()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                DateTime today = Day.AddDays(1);
                fixture.Clock.SetUtcNow(today.AddHours(20));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6), Cbbo(Call, today, 5));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, new DateTimeOffset(today.AddDays(1)), CacheMode.Bypass);

                Assert.AreEqual(3, served.Length);
                Assert.AreEqual(2, fixture.ReadCbboDayFile(Call, Day).Length, "replaced by the download");
                Assert.IsNull(fixture.ReadCbboDayFile(Call, today), "today has not ended");
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count, "the settled day and today in one request");
                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count);
                Assert.AreEqual(1, fixture.Databento.RequestsTo("metadata.get_dataset_condition").Count, "the replaced day's condition only");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassWithReplaceCachedTodayAnsweredEmpty_KeepsAndServesTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.BypassMissAction = BypassMissAction.Replace))
            {
                // a day file for a day not yet ended, as one saved before the clock was set back
                fixture.Clock.SetUtcNow(Day.AddHours(20));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(1, served.Length, "the cached day is served");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "a day not settled never replaces it");
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayBeforeTheSymbolWasListed_IsSavedAsEmptyAndTheListedDayIsNot()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Listings[Call] = new[] { Day.AddDays(1), Day.AddDays(10) };

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day.AddDays(1)));
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(DayStart.AddDays(1), fixture.Databento.TimeseriesRequests[1].StartUtc);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayOfAListedSymbol_IsNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                Assert.AreEqual(1, fixture.Databento.SymbologyRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SymbologyLookupFails_EmptyDayIsServedAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Listings[Call] = new[] { Day.AddDays(1), Day.AddDays(10) };
                fixture.Databento.Respond = request => request.Path == "symbology.resolve" ? FakeDatabento.Error(HttpStatusCode.InternalServerError, "internal") : null;

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(0, served.Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CacheAlways_SavesAnEmptyDayWithoutALookup()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(0, fixture.Databento.SymbologyRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_Never_SavesNoEmptyDayEvenOfAnUnlistedSymbol()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.Never))
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted }, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Unlisted, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusedForAnUnlistedSymbol_AsksAgainWithTheListedOnesAndSavesTheUnlistedAsEmpty()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted, Put }, DayStart, DayEnd);

                CollectionAssert.AreEqual(new[] { Call, Put }, served.Select(record => record.Symbol).ToArray());
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                CollectionAssert.AreEqual(new[] { Call, Put }, fixture.Databento.TimeseriesRequests[1].Symbols);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Unlisted, Day).Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Put, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_OneUnlistedSymbol_IsSavedAsEmptyWithoutAskingAgain()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Unlisted, DayStart, DayEnd);

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Unlisted, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusalTheLookupCannotExplain_Throws()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.BadRequest, "symbology_invalid_request") : null;

                DatabentoHttpException thrown = await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd));

                Assert.AreEqual(DatabentoErrorCase.SymbologyInvalidRequest, thrown.Code);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusalWithASymbolTheLookupLeavesUnanswered_ThrowsAndSavesNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                // the lookup names Unlisted not found and says nothing of Put
                fixture.Databento.Respond = request => request.Path == "symbology.resolve"
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\":{\"" + Call + "\":[{\"d0\":\"2026-10-16\",\"d1\":\"2026-10-17\",\"s\":\"1001\"}]},\"not_found\":[\"" + Unlisted + "\"],\"partial\":[],\"status\":0}") }
                    : null;

                await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put, Unlisted }, DayStart, DayEnd));

                Assert.IsNull(fixture.ReadCbboDayFile(Put, Day));
                Assert.IsNull(fixture.ReadCbboDayFile(Unlisted, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DownloadFails_ThrowsAndSavesNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.Forbidden, "license_not_found_unauthorized") : null;

                await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd));

                Assert.IsFalse(Directory.Exists(fixture.Cache.CbboDirectory) && Directory.GetFiles(fixture.Cache.CbboDirectory).Length > 0);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_Bypass_DownloadsACachedDayAgainAndReplacesIt()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(2, served.Length);
                Assert.AreEqual(2, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassSkipsTheArchive_AndItsDayFileWinsAfterwards()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(2, served.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassAnsweredEmpty_KeepsAndServesTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassAnsweredEmptyWithReplace_ReplacesTheCachedDayWithAnEmptyOne()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.BypassMissAction = BypassMissAction.Replace))
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassFails_KeepsTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.BypassMissAction = BypassMissAction.Replace))
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.ServiceUnavailable, "unavailable") : null;

                await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass));

                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_FirstSaveIntoAnEmptyRoot_WritesItsMetadata()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                File.Delete(Path.Combine(fixture.Cache.Root, CacheMetadata.FileName));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                using(CachedDatabentoClient client = fixture.CreateClient())
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                }

                Assert.AreEqual(CacheMetadata.UtcCutoff, CacheMetadata.Read(fixture.Cache.Root).DayCutoff);
            }
        }

        [TestMethod]
        public void Constructor_RestoreInProgress_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                CacheRestoreMarker.Write(fixture.Cache.Root, "backup.tar", fixture.Clock.GetUtcNow().UtcDateTime);

                Assert.ThrowsException<InvalidOperationException>(() => fixture.CreateClient());
            }
        }

        [TestMethod]
        public void Constructor_MetadataOfAnotherFormat_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Cache.Root, CacheMetadata.FileName), "{\"format_version\":2,\"day_cutoff\":\"UTC\"}");

                Assert.ThrowsException<InvalidDataException>(() => fixture.CreateClient());
            }
        }

        [TestMethod]
        public void Constructor_SettingsOutOfRange_AreRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.MaxSymbolsPerRequest = 2001));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.MaxSymbolsPerRequest = 0));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.CacheRoot = " "));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.Databento = null));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.EmptyDays = (EmptyDayPolicy)7));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.TimeProvider = null));
                ArgumentException negative = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.SettleDelay = TimeSpan.FromTicks(-1)));
                ArgumentException overADay = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.SettleDelay = TimeSpan.FromDays(1).Add(TimeSpan.FromTicks(1))));
                StringAssert.Contains(negative.Message, "SettleDelay -00:00:00.0000001 must be 00:00:00 to 1.00:00:00, inclusive.");
                StringAssert.Contains(overADay.Message, "SettleDelay 1.00:00:00.0000001 must be 00:00:00 to 1.00:00:00, inclusive.");
            }
        }

        [TestMethod]
        public void Constructor_SettleDelayAtEitherEndOfItsRange_IsTaken()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                fixture.CreateClient(options => options.SettleDelay = TimeSpan.Zero).Dispose();
                fixture.CreateClient(options => options.SettleDelay = TimeSpan.FromDays(1)).Dispose();
            }
        }

        [TestMethod]
        public void SettleDelay_ByDefault_Is15Minutes()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(15), new CachedDatabentoClientOptions().SettleDelay);
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ArgumentsOutOfRange_AreRefusedBeforeAnyRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, "ALL_SYMBOLS", DayStart, DayEnd));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new string[0], DayStart, DayEnd));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, string.Empty }, DayStart, DayEnd));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayEnd, DayEnd));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.GetCbbo1mAsync(string.Empty, Call, DayStart, DayEnd));
                await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, (CacheMode)5));

                Assert.AreEqual(0, fixture.Databento.RequestsTo("timeseries.get_range").Count + fixture.Databento.RequestsTo("metadata.get_dataset_range").Count);
            }
        }
    }
}
