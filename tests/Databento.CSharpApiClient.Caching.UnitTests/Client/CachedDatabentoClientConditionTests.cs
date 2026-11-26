// Ignore Spelling: Cbbo

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// The condition guard on the two suspect saves of a download made without <see cref="CachedDatabentoClientOptions.CheckAvailabilityFirst"/>:
    /// an empty day is saved, and a bypass replaces a cached day, only when Databento's condition for the day is <c>available</c>. The
    /// conditions are read once per request, only when there is such a save to make.
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientConditionTests
    {
        private const string ConditionPath = "metadata.get_dataset_condition";

        private const string Unlisted = "SPXW  261016C09999000";

        private static int ConditionReads(CachedClientFixture fixture)
        {
            return fixture.Databento.RequestsTo(ConditionPath).Count;
        }

        private static void SaveCached(CachedClientFixture fixture, string symbol, DateTime day, params CbboRecordJson[] records)
        {
            fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, symbol, day, records);
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CacheAlwaysEmptyDayAvailable_IsSavedAfterOneConditionRead()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(1, ConditionReads(fixture));
                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count, "no range read");
            }
        }

        [TestMethod]
        [DataRow("degraded")]
        [DataRow("pending")]
        [DataRow("missing")]
        public async Task GetCbbo1mAsync_CacheAlwaysEmptyDayNotAvailable_IsServedEmptyAndNotSaved(string condition)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.EmptyDays = EmptyDayPolicy.CacheAlways;
                    options.Logger = logger;
                }))
                {
                    fixture.Databento.Conditions[Day] = condition;

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    Assert.AreEqual(0, served.Length);
                    Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                    FakeLogEntry notSaved = logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records and is not saved: Databento's condition for the day is " + condition);
                    Assert.AreEqual(1018, notSaved.EventId.Id);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_UnlistedSymbolEmptyDayAvailable_IsSavedAfterTheLookupAndTheConditionRead()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Listings[Call] = new[] { Day.AddDays(1), Day.AddDays(10) };

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(1, fixture.Databento.SymbologyRequests.Count, "the listing still needs confirming");
                Assert.AreEqual(1, ConditionReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_UnlistedSymbolEmptyDayDegraded_IsNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Listings[Call] = new[] { Day.AddDays(1), Day.AddDays(10) };
                fixture.Databento.Conditions[Day] = "degraded";

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusedUnlistedSymbolDegraded_IsServedEmptyAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted }, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "a day with records is saved whatever its condition");
                Assert.IsNull(fixture.ReadCbboDayFile(Unlisted, Day), "the empty one only when available");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayOfAListedSymbol_ReadsNoCondition()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                Assert.AreEqual(0, ConditionReads(fixture), "nothing to save, so nothing to check");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDaysNever_ReadsNoCondition()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.Never))
            {
                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted }, DayStart, DayEnd);

                Assert.AreEqual(0, ConditionReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ConditionReadFails_ServesTheEmptyDayAndSavesNothingWithAWarning()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.EmptyDays = EmptyDayPolicy.CacheAlways;
                    options.Logger = logger;
                }))
                {
                    fixture.Databento.Respond = request => request.Path == ConditionPath ? FakeDatabento.Error(HttpStatusCode.InternalServerError, "internal_error") : null;

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                    Assert.AreEqual(0, served.Length);
                    Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                    Assert.IsNull(fixture.ReadCbboDayFile(Put, Day));
                    FakeLogEntry warning = logger.AssertLogged(LogLevel.Warning, "Reading Databento's conditions for 2 symbol-day(s) of cbbo-1m of OPRA.PILLAR failed, so they are served and none is saved or replaced");
                    Assert.AreEqual(1029, warning.EventId.Id);
                    Assert.IsNotNull(warning.Exception);
                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records and is not saved: Databento's condition for the day could not be read");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SeveralEmptyDaysAndSymbols_ReadsTheConditionsOnceForTheRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                fixture.Databento.Conditions[Day.AddDays(1)] = "degraded";

                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayStart.AddDays(3));

                Assert.AreEqual(1, ConditionReads(fixture));
                Assert.AreEqual("2026-10-16", fixture.Databento.RequestsTo(ConditionPath)[0].Fields["start_date"]);
                Assert.IsFalse(fixture.Databento.RequestsTo(ConditionPath)[0].Fields.ContainsKey("end_date"), "up to the latest listed day");
                foreach(string symbol in new[] { Call, Put })
                {
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(symbol, Day).Length);
                    Assert.IsNull(fixture.ReadCbboDayFile(symbol, Day.AddDays(1)), "degraded");
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(symbol, Day.AddDays(2)).Length);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFoundAvailableBefore_IsNotAskedAboutAgain()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Put, DayStart, DayEnd);

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Put, Day).Length);
                Assert.AreEqual(1, ConditionReads(fixture), "the day was found available, and that is kept");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_WeekendOmittedInsideTheListedDays_IsSavedAndKeptAsFinal()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                // Friday to Monday, Databento listing no condition for the weekend between
                fixture.Databento.OmittedConditions.Add(Day.AddDays(1));
                fixture.Databento.OmittedConditions.Add(Day.AddDays(2));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(4));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Put, DayStart.AddDays(1), DayStart.AddDays(3));

                foreach(DateTime day in Enumerable.Range(0, 4).Select(offset => Day.AddDays(offset)))
                {
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, day).Length, Invariant($"{day:yyyy-MM-dd}"));
                }

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Put, Day.AddDays(1)).Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Put, Day.AddDays(2)).Length);
                Assert.AreEqual(1, ConditionReads(fixture), "the weekend was kept as final");
            }
        }

        [TestMethod]
        [DataRow(-4, DisplayName = "Monday to Sunday")]
        [DataRow(1, DisplayName = "Saturday to Sunday")]
        public async Task GetCbbo1mAsync_TrailingWeekendOmittedWithALaterDayListed_IsSavedAndKeptAsFinal(int firstOffset)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                // the request ends with the weekend; Databento lists the Monday after it
                fixture.Databento.OmittedConditions.Add(Day.AddDays(1));
                fixture.Databento.OmittedConditions.Add(Day.AddDays(2));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart.AddDays(firstOffset), DayStart.AddDays(3));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Put, DayStart.AddDays(1), DayStart.AddDays(3));

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day.AddDays(2)).Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Put, Day.AddDays(2)).Length);
                Assert.AreEqual(1, ConditionReads(fixture), "the weekend was kept as final");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TrailingDayOmittedWithNoLaterDayListed_IsNotSavedAndAskedAboutAgain()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.EmptyDays = EmptyDayPolicy.CacheAlways;
                    options.Logger = logger;
                }))
                {
                    // Databento has not reached the second day yet: its conditions answer stops at the first
                    fixture.Databento.LastConditionDay = Day;

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));

                    Assert.AreEqual(0, served.Length);
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                    Assert.IsNull(fixture.ReadCbboDayFile(Call, Day.AddDays(1)));
                    FakeLogEntry notSaved = logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-17 cbbo-1m answered no records and is not saved: Databento lists no condition for the day, nor for a later one");
                    Assert.AreEqual(1018, notSaved.EventId.Id);

                    fixture.Databento.LastConditionDay = null;
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));

                    Assert.AreEqual(2, ConditionReads(fixture), "not kept as final, so asked about again");
                    Assert.AreEqual("2026-10-17", fixture.Databento.RequestsTo(ConditionPath)[1].Fields["start_date"]);
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length, "saved once listed available");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstTrailingDayOmitted_IsStillFinal()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options =>
            {
                options.CheckAvailabilityFirst = true;
                options.EmptyDays = EmptyDayPolicy.CacheAlways;
            }))
            {
                fixture.Databento.OmittedConditions.Add(Day.AddDays(1));

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));

                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length, "unchanged: a day with no condition listed is final there");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RecordsOnlyOverSeveralDaysAndSymbols_ReadsNoMetadata()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
            {
                fixture.Databento.Conditions[Day] = "degraded";
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 1), Cbbo(Call, Day.AddDays(1), 1), Cbbo(Put, Day.AddDays(1), 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayStart.AddDays(2));

                Assert.AreEqual(4, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Put, Day).Length, "a day with records is saved whatever its condition");
                Assert.AreEqual(0, ConditionReads(fixture));
                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count);
                Assert.AreEqual(0, fixture.Databento.SymbologyRequests.Count);
            }
        }

        [TestMethod]
        [DataRow("degraded")]
        [DataRow("pending")]
        [DataRow("missing")]
        public async Task GetCbbo1mAsync_BypassCachedDayNotAvailable_ServesTheDownloadAndKeepsTheCachedDay(string condition)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));
                    fixture.Databento.Conditions[Day] = condition;

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    Assert.AreEqual(2, served.Length, "the download is served");
                    Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "the cached day is kept");
                    FakeLogEntry kept = logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m is served as downloaded on a bypass and the cached day is kept: Databento's condition for the day is " + condition);
                    Assert.AreEqual(1028, kept.EventId.Id);
                    Assert.AreEqual(0, logger.Find(LogLevel.Information, "A bypass replaced").Length);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassCachedDayAvailableWithFewerRecords_ReplacesItAndLogsTheCounts()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    // a cached day holding duplicates, which Databento now answers corrected
                    SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1), Cbbo(Call, Day, 1), Cbbo(Call, Day, 2));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day, 2));

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    Assert.AreEqual(2, served.Length);
                    Assert.AreEqual(2, fixture.ReadCbboDayFile(Call, Day).Length, "replaced, though with fewer records");
                    Assert.AreEqual(1, ConditionReads(fixture));
                    FakeLogEntry replaced = logger.AssertLogged(LogLevel.Information, "A bypass replaced the cached " + Call + " 2026-10-16 cbbo-1m: cached 3 records, now 2");
                    Assert.AreEqual(1027, replaced.EventId.Id);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassWithReplaceCachedDayAnsweredEmptyDegraded_ServesEmptyAndKeepsTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.BypassMissAction = BypassMissAction.Replace))
            {
                SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(0, served.Length, "the download is served");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "an outage's empty answer never replaces the cached day");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassWithReplaceCachedDayAnsweredEmptyAvailable_ReplacesItAndLogsTheCounts()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.BypassMissAction = BypassMissAction.Replace;
                    options.Logger = logger;
                }))
                {
                    SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
                    logger.AssertLogged(LogLevel.Information, "A bypass replaced the cached " + Call + " 2026-10-16 cbbo-1m: cached 1 records, now 0");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassCachedEmptyDayAnsweredWithRecordsDegraded_KeepsTheEmptyDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                SaveCached(fixture, Call, Day);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassConditionReadFails_ServesTheDownloadAndKeepsTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));
                    fixture.Databento.Respond = request => request.Path == ConditionPath ? FakeDatabento.Error(HttpStatusCode.InternalServerError, "internal_error") : null;

                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    Assert.AreEqual(2, served.Length);
                    Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                    logger.AssertLogged(LogLevel.Warning, "Reading Databento's conditions for 1 symbol-day(s) of cbbo-1m of OPRA.PILLAR failed");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassOfDaysNotCached_SavesThemWithoutAConditionRead()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 1));
                fixture.Databento.Conditions[Day] = "degraded";

                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Put, Day).Length);
                Assert.AreEqual(0, ConditionReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassKeepCachedAnsweredEmpty_ReadsNoCondition()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(1, served.Length, "the cached day is served");
                Assert.AreEqual(0, ConditionReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassOfSeveralCachedDays_ReadsTheConditionsOnce()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                SaveCached(fixture, Call, Day.AddDays(1), Cbbo(Call, Day.AddDays(1), 1));
                SaveCached(fixture, Put, Day, Cbbo(Put, Day, 1));
                SaveCached(fixture, Put, Day.AddDays(1), Cbbo(Put, Day.AddDays(1), 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day.AddDays(1), 5), Cbbo(Call, Day.AddDays(1), 6), Cbbo(Put, Day, 5), Cbbo(Put, Day, 6), Cbbo(Put, Day.AddDays(1), 5), Cbbo(Put, Day.AddDays(1), 6));
                fixture.Databento.Conditions[Day.AddDays(1)] = "degraded";

                await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayStart.AddDays(2), CacheMode.Bypass);

                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count, "one request for both symbols");
                Assert.AreEqual(1, ConditionReads(fixture), "and one condition read for it");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length, "replaced with the one record the call has that day");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length, "degraded: kept");
                Assert.AreEqual(2, fixture.ReadCbboDayFile(Put, Day).Length, "replaced");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Put, Day.AddDays(1)).Length, "degraded: kept");
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstBypass_ReadsTheConditionsOnlyBeforeTheDownload()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.CheckAvailabilityFirst = true;
                    options.EmptyDays = EmptyDayPolicy.CacheAlways;
                    options.Logger = logger;
                }))
                {
                    SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd, CacheMode.Bypass);

                    Assert.AreEqual(1, ConditionReads(fixture), "the check before the download, not a second read");
                    Assert.AreEqual(2, fixture.ReadCbboDayFile(Call, Day).Length);
                    Assert.AreEqual(0, fixture.ReadCbboDayFile(Put, Day).Length);
                    logger.AssertLogged(LogLevel.Information, "A bypass replaced the cached " + Call + " 2026-10-16 cbbo-1m: cached 1 records, now 2");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstBypassCachedDayDegraded_IsNotReplaced()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.CheckAvailabilityFirst = true))
            {
                SaveCached(fixture, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 5), Cbbo(Call, Day, 6));
                fixture.Databento.Conditions[Day] = "degraded";

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                Assert.AreEqual(2, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
                Assert.AreEqual(1, ConditionReads(fixture));
            }
        }
    }
}
