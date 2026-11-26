// Ignore Spelling: Cbbo

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;
using Databento.CSharpApiClient.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A download Databento refuses as past the available end: the dataset range read once, the request cut at the end and sent once
    /// more, the end remembered so later requests are cut before they are sent until the next line of the
    /// <see cref="CachedDatabentoClientOptions.AvailableEndRefreshInterval"/> grid, and the days past it served empty or thrown, as
    /// <see cref="CachedDatabentoClientOptions.AvailableEndRefusal"/> says
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientAvailableEndTests
    {
        private const string RangePath = "metadata.get_dataset_range";

        private const string TimeseriesPath = "timeseries.get_range";

        /// <summary>
        /// The day the clock stands on: two days after the fixture's, at noon, with the data available up to 06:00 of it
        /// </summary>
        private static readonly DateTime Today = Day.AddDays(2);

        private static readonly DateTime AvailableEnd = Today.AddHours(6);

        private static DateTimeOffset TodayStart => new DateTimeOffset(Today);

        private static DateTimeOffset TodayEnd => new DateTimeOffset(Today.AddDays(1));

        private static CbboRecordJson OneSecond(string symbol, DateTime receivedUtc)
        {
            CbboRecordJson record = CbboAt(symbol, receivedUtc);
            record.Header.RecordType = RType.Cbbo1S;
            return record;
        }

        /// <summary>
        /// Creates a DBN client over the fixture, its clock at noon of <see cref="Today"/> and the data available up to 06:00 of it, with a
        /// record of the call at 14:00 of the fixture's day and of the day after, and at 03:00 of today
        /// </summary>
        private static CachedDatabentoClient CreateClient(CachedClientFixture fixture, AvailableEndRefusal refusal = AvailableEndRefusal.ServeEmpty, Action<CachedDatabentoClientOptions> change = null)
        {
            fixture.Clock.SetUtcNow(Today.AddHours(12));
            fixture.Databento.AvailableEndUtc = AvailableEnd;
            fixture.Databento.Add(Schema.ConsolidatedBBO1Sec, OneSecond(Call, Day.AddHours(14)), OneSecond(Call, Day.AddDays(1).AddHours(14)), OneSecond(Call, Today.AddHours(3)));
            return fixture.CreateClient(options =>
            {
                options.Transport = DatabentoTransport.Dbn;
                options.AvailableEndRefusal = refusal;
                change?.Invoke(options);
            });
        }

        private static int RangeReads(CachedClientFixture fixture)
        {
            return fixture.Databento.RequestsTo(RangePath).Count;
        }

        private static Task<DatabentoHttpException> AssertRefusedAsync(Func<Task> call)
        {
            return Assert.ThrowsExceptionAsync<DatabentoHttpException>(call);
        }

        private static void AssertRefusedByDatabento(DatabentoHttpException thrown, DatabentoErrorCase code = DatabentoErrorCase.DataEndAfterAvailableEnd)
        {
            Assert.AreEqual(code, thrown.Code);
            StringAssert.Contains(thrown.ResponseBody, "\"message\":\"refused\"", "the fake's refusal, as it came");
        }

        [TestMethod]
        [DataRow(DatabentoTransport.Dbn)]
        [DataRow(DatabentoTransport.Json)]
        public async Task GetCbbo1sAsync_RefusedPastTheAvailableEnd_ReadsTheRangeOnceAndAsksOnceMoreCutAtIt(DatabentoTransport transport)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, change: options => options.Transport = transport))
            {
                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd);

                Assert.AreEqual(3, served.Length, "today's record before the end too");
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "refused, then once more");
                Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[0].EndUtc, "the first goes uncut");
                Assert.AreEqual(DayStart, fixture.Databento.TimeseriesRequests[1].StartUtc);
                Assert.AreEqual(new DateTimeOffset(AvailableEnd), fixture.Databento.TimeseriesRequests[1].EndUtc);
                Assert.AreEqual(1, RangeReads(fixture));
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec).Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1), Schema.ConsolidatedBBO1Sec).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Today, Schema.ConsolidatedBBO1Sec), "today has not settled");
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ServeEmptySettledDayPastTheEnd_IsServedEmptyNotSavedAndLoggedAtInformation()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, change: options =>
                {
                    options.EmptyDays = EmptyDayPolicy.CacheAlways;
                    options.Logger = logger;
                }))
                {
                    // the data lags a day behind: it ends at 06:00 of yesterday, before yesterday's record
                    fixture.Databento.AvailableEndUtc = Day.AddDays(1).AddHours(6);

                    ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayStart);

                    Assert.AreEqual(1, served.Length, "the first day's record");
                    Assert.AreEqual(new DateTimeOffset(Day.AddDays(1).AddHours(6)), fixture.Databento.TimeseriesRequests[1].EndUtc);
                    Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec).Length);
                    Assert.IsNull(fixture.ReadCbboDayFile(Call, Day.AddDays(1), Schema.ConsolidatedBBO1Sec), "not even as empty, though CacheAlways");
                    FakeLogEntry line = logger.AssertLogged(LogLevel.Information, "1 settled symbol-day(s) of cbbo-1s of OPRA.PILLAR, UTC days 2026-10-17 to 2026-10-17, are served only up to the available end 2026-10-17T06:00:00Z and not saved");
                    Assert.AreEqual(1030, line.EventId.Id);
                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-17 cbbo-1s is served but not saved", "it ends past the available end 2026-10-17T06:00:00Z");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ServeEmptyTodayPastTheEnd_IsNotLoggedAtInformation()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, change: options => options.Logger = logger))
                {
                    ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                    Assert.AreEqual(1, served.Length);
                    Assert.AreEqual(0, logger.Find(LogLevel.Information, "settled symbol-day").Length, "today is no settled day");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_TodayStartingAtOrPastTheEnd_IsServedEmptyWithoutAnotherRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                // 00:20, and the data still ends at 23:50 of yesterday: today's request starts past it, so the cut one is empty
                fixture.Clock.SetUtcNow(Today.AddMinutes(20));
                fixture.Databento.AvailableEndUtc = Today.AddMinutes(-10);

                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                ICbboRecord[] again = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                Assert.AreEqual(0, served.Length);
                Assert.AreEqual(0, again.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count, "refused once; nothing is left to send");
                Assert.AreEqual(1, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ThrowRefusedPastTheEnd_SavesTheDaysBeforeItAndThrowsDatabentosRefusal()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, AvailableEndRefusal.Throw))
            {
                DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));

                AssertRefusedByDatabento(thrown);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "refused, then once more cut at the end");
                Assert.AreEqual(1, RangeReads(fixture));
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec).Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1), Schema.ConsolidatedBBO1Sec).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Today, Schema.ConsolidatedBBO1Sec));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ThrowOneSpanRefused_SavesTheOtherSpanAndThrowsOnceAllAreServed()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.AvailableEndRefusal = AvailableEndRefusal.Throw))
            {
                // 00:20 of the tenth day, the data ending at 23:50 of yesterday; the call misses only yesterday, the put only the first day
                DateTime today = Day.AddDays(10);
                DateTime yesterday = today.AddDays(-1);
                fixture.Clock.SetUtcNow(today.AddMinutes(20));
                fixture.Databento.AvailableEndUtc = today.AddMinutes(-10);
                for(DateTime day = Day; day < yesterday; day = day.AddDays(1))
                {
                    fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, day, Cbbo(Call, day, 1));
                    fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, day.AddDays(1), Cbbo(Put, day.AddDays(1), 1));
                }

                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Put, Day, 5), Cbbo(Call, yesterday, 5));

                DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, new DateTimeOffset(today)));

                AssertRefusedByDatabento(thrown);
                Assert.AreEqual(3, fixture.Databento.TimeseriesRequests.Count, "the put's span, and the call's refused then cut");
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Put, Day).Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, yesterday));
                Assert.AreEqual(1, RangeReads(fixture));
            }
        }

        [TestMethod]
        [DataRow(AvailableEndRefusal.ServeEmpty)]
        [DataRow(AvailableEndRefusal.Throw)]
        public async Task GetCbbo1sAsync_RetryRefusedAgain_PropagatesThatRefusalWithoutAnotherRead(AvailableEndRefusal refusal)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, refusal))
            {
                fixture.Databento.Respond = request => request.Path == TimeseriesPath && fixture.Databento.TimeseriesRequests.Count == 2 ? FakeDatabento.Error(HttpStatusCode.UnprocessableEntity, "data_start_after_available_end") : null;

                DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));

                AssertRefusedByDatabento(thrown, DatabentoErrorCase.DataStartAfterAvailableEnd);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, RangeReads(fixture));
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec));
            }
        }

        [TestMethod]
        [DataRow(AvailableEndRefusal.ServeEmpty)]
        [DataRow(AvailableEndRefusal.Throw)]
        public async Task GetCbbo1sAsync_RangeReadFails_PropagatesDatabentosRefusalAndRemembersNothing(AvailableEndRefusal refusal)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, refusal, options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request => request.Path == RangePath ? FakeDatabento.Error(HttpStatusCode.InternalServerError, "internal_error") : null;

                    DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));
                    await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));

                    AssertRefusedByDatabento(thrown);
                    Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "nothing remembered, so the next goes uncut");
                    Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[1].EndUtc);
                    Assert.IsNull(fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec));
                    FakeLogEntry warning = logger.AssertLogged(LogLevel.Warning, "Reading the available end of cbbo-1s of OPRA.PILLAR from Databento's dataset range failed");
                    Assert.AreEqual(1026, warning.EventId.Id);
                    Assert.IsInstanceOfType(warning.Exception, typeof(DatabentoHttpException));
                }
            }
        }

        [TestMethod]
        [DataRow(AvailableEndRefusal.ServeEmpty)]
        [DataRow(AvailableEndRefusal.Throw)]
        public async Task GetCbbo1sAsync_CancelledDuringTheRangeRead_ThrowsOperationCanceled(AvailableEndRefusal refusal)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, refusal, options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request =>
                    {
                        if(request.Path != RangePath)
                        {
                            return null;
                        }

                        // as HttpClient does when the caller cancels while the request is under way
                        cancellation.Cancel();
                        throw new OperationCanceledException(cancellation.Token);
                    };

                    Exception thrown = null;
                    try
                    {
                        await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd, CacheMode.Use, cancellation.Token);
                    }
                    catch(Exception ex)
                    {
                        thrown = ex;
                    }

                    Assert.IsInstanceOfType(thrown, typeof(OperationCanceledException));
                    Assert.AreEqual(1, RangeReads(fixture));
                    Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                    Assert.AreEqual(0, logger.Find(LogLevel.Warning, "Reading the available end").Length);
                }
            }
        }

        [TestMethod]
        [DataRow(AvailableEndRefusal.ServeEmpty, HttpStatusCode.Forbidden, "license_not_found_unauthorized", DatabentoErrorCase.LicenseNotFoundUnauthorized)]
        [DataRow(AvailableEndRefusal.Throw, HttpStatusCode.Forbidden, "license_not_found_unauthorized", DatabentoErrorCase.LicenseNotFoundUnauthorized)]
        [DataRow(AvailableEndRefusal.ServeEmpty, HttpStatusCode.UnprocessableEntity, "data_schema_not_fully_available", DatabentoErrorCase.DataSchemaNotFullyAvailable)]
        public async Task GetCbbo1sAsync_OtherRefusals_ThrowReadNoRangeAndAreAskedEachTime(AvailableEndRefusal refusal, HttpStatusCode status, string errorCase, DatabentoErrorCase code)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, refusal))
            {
                fixture.Databento.Respond = request => request.Path == TimeseriesPath ? FakeDatabento.Error(status, errorCase) : null;

                DatabentoHttpException first = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd));
                DatabentoHttpException second = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd));

                Assert.AreEqual(code, first.Code);
                Assert.AreEqual(code, second.Code);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(0, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_RememberedEndWithinTheInterval_CutsTheRequestBeforeItIsSent()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                fixture.Clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromTicks(1)));

                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, new[] { Call, Put }, TodayStart, TodayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(3, fixture.Databento.TimeseriesRequests.Count, "the first call's two, then one cut request");
                Assert.AreEqual(new DateTimeOffset(AvailableEnd), fixture.Databento.TimeseriesRequests[2].EndUtc);
                Assert.IsTrue(fixture.Databento.TimeseriesRequests.Skip(1).All(request => request.EndUtc <= new DateTimeOffset(AvailableEnd)), "none past the end reaches Databento");
                Assert.AreEqual(1, RangeReads(fixture), "the range is not read again");
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ThrowRememberedEndWithinTheInterval_ThrowsARefusalNamingIt()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, AvailableEndRefusal.Throw))
            {
                await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd));

                DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));

                Assert.AreEqual(DatabentoErrorCase.DataEndAfterAvailableEnd, thrown.Code);
                Assert.AreEqual(422, thrown.StatusCode);
                StringAssert.Contains(thrown.ResponseBody, "ends past the available end 2026-10-18T06:00:00.0000000Z the cache remembers");
                Assert.AreEqual(3, fixture.Databento.TimeseriesRequests.Count, "the first call's two, then one cut request");
                Assert.AreEqual(new DateTimeOffset(AvailableEnd), fixture.Databento.TimeseriesRequests[2].EndUtc);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec).Length, "the days before the end are saved first");
                Assert.AreEqual(1, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ThrowRememberedEndAtOrBeforeTheStart_ThrowsARefusalForTheStartWithoutARequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, AvailableEndRefusal.Throw))
            {
                fixture.Clock.SetUtcNow(Today.AddMinutes(20));
                fixture.Databento.AvailableEndUtc = Today.AddMinutes(-10);
                await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd));

                DatabentoHttpException thrown = await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd));

                Assert.AreEqual(DatabentoErrorCase.DataStartAfterAvailableEnd, thrown.Code);
                StringAssert.Contains(thrown.ResponseBody, "starts at or past the available end 2026-10-17T23:50:00.0000000Z");
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_RememberedEndDueByTheGrid_SendsTheRequestUncut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                fixture.Clock.Advance(TimeSpan.FromMinutes(15));

                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                Assert.AreEqual(4, fixture.Databento.TimeseriesRequests.Count, "each call refused once and cut");
                Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[2].EndUtc, "due: uncut");
                Assert.AreEqual(2, RangeReads(fixture), "read again after the refusal");
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_EndReadOffTheGrid_IsUsedUntilTheNextGridLineOnly()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                // read at 00:20 with the 15-minute default: used until 00:30, not 00:35
                fixture.Clock.SetUtcNow(Today.AddMinutes(20));
                fixture.Databento.AvailableEndUtc = Today.AddMinutes(-10);
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                fixture.Clock.SetUtcNow(Today.AddMinutes(30).AddTicks(-1));
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                int requestsBeforeTheLine = fixture.Databento.TimeseriesRequests.Count;
                fixture.Clock.SetUtcNow(Today.AddMinutes(30));

                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                Assert.AreEqual(1, requestsBeforeTheLine, "nothing sent up to the grid line");
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "sent uncut on it");
                Assert.AreEqual(2, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ShorterRefreshInterval_SendsUncutSooner()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture, change: options => options.AvailableEndRefreshInterval = TimeSpan.FromMinutes(5)))
            {
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                fixture.Clock.Advance(TimeSpan.FromMinutes(5));

                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[2].EndUtc);
                Assert.AreEqual(2, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_RequestEndingExactlyAtTheRememberedEnd_GoesUncut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                // the data ends at midnight, so the end of yesterday's request is the remembered end itself
                fixture.Databento.AvailableEndUtc = Today;
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayStart);

                Assert.AreEqual(2, served.Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "today refused, then the two days");
                Assert.AreEqual(new DateTimeOffset(Today), fixture.Databento.TimeseriesRequests[1].EndUtc);
                Assert.AreEqual(1, RangeReads(fixture));
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_AnotherSchemaOrDataset_IsNotCutByTheRememberedEnd()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);
                await client.GetCbbo1sAsync(Datasets.XnasItch, Call, TodayStart, TodayEnd);

                Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[2].EndUtc);
                Assert.AreEqual(TodayEnd, fixture.Databento.TimeseriesRequests[4].EndUtc);
                Assert.AreEqual(3, RangeReads(fixture), "each read its own end");
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_ConcurrentCallsWithinTheInterval_AreAllCutWithoutAnotherRead()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = CreateClient(fixture))
            {
                await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                Task[] calls = Enumerable.Range(0, 8)
                    .Select(index => Task.Run(() => client.GetCbbo1sAsync(Datasets.OpraPillar, index % 2 == 0 ? Call : Put, TodayStart, TodayEnd)))
                    .ToArray();
                await Task.WhenAll(calls);

                Assert.IsTrue(fixture.Databento.TimeseriesRequests.Skip(1).All(request => request.EndUtc == new DateTimeOffset(AvailableEnd)));
                Assert.AreEqual(1, RangeReads(fixture));
            }
        }

        [TestMethod]
        [DataRow("00:20:00", 15, "2026-10-18T00:30:00Z")]
        [DataRow("00:30:00", 15, "2026-10-18T00:45:00Z")]
        [DataRow("23:59:59", 15, "2026-10-19T00:00:00Z")]
        [DataRow("00:20:00", 60, "2026-10-18T01:00:00Z")]
        [DataRow("00:20:00", 1440, "2026-10-19T00:00:00Z")]
        public async Task GetCbbo1sAsync_RefusedPastTheEnd_LogsTheEndReadAndTheNextGridLineAtDebug(string readTime, int intervalMinutes, string refreshUtc)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, change: options =>
                {
                    options.Logger = logger;
                    options.AvailableEndRefreshInterval = TimeSpan.FromMinutes(intervalMinutes);
                }))
                {
                    fixture.Clock.SetUtcNow(Today.Add(TimeSpan.Parse(readTime, CultureInfo.InvariantCulture)));

                    await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, TodayStart, TodayEnd);

                    FakeLogEntry read = logger.AssertLogged(LogLevel.Debug, "Read the available end of cbbo-1s of OPRA.PILLAR, 2026-10-18T06:00:00Z", "until it is read again from " + refreshUtc);
                    Assert.AreEqual(1025, read.EventId.Id);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_RefusedPastTheEnd_LogsNoWarningForTheRefusal()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, change: options => options.Logger = logger))
                {
                    await client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd);

                    Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count, "refused, then once more");
                    Assert.AreEqual(0, logger.Find(LogLevel.Warning).Length, "the refusal is handled by the cut");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_RetryRefusedAgain_LogsTheFailedDownloadOnce()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = CreateClient(fixture, change: options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request => request.Path == TimeseriesPath && fixture.Databento.TimeseriesRequests.Count == 2 ? FakeDatabento.Error(HttpStatusCode.UnprocessableEntity, "data_start_after_available_end") : null;

                    await AssertRefusedAsync(() => client.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, TodayEnd));

                    FakeLogEntry failed = logger.AssertLogged(LogLevel.Warning, "Downloading cbbo-1s of OPRA.PILLAR");
                    Assert.AreEqual(1024, failed.EventId.Id);
                    Assert.AreEqual(1, logger.Find(LogLevel.Warning).Length, "the retry's refusal only");
                }
            }
        }

        [TestMethod]
        public void AvailableEndRefreshInterval_ByDefault_Is15Minutes()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(15), new CachedDatabentoClientOptions().AvailableEndRefreshInterval);
        }

        [TestMethod]
        public void Constructor_AvailableEndRefreshIntervalZeroOrNegative_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                ArgumentException zero = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.Zero));
                ArgumentException negative = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromTicks(-1)));
                fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromTicks(1)).Dispose();

                StringAssert.Contains(zero.Message, "AvailableEndRefreshInterval 00:00:00 must be above zero.");
                StringAssert.Contains(negative.Message, "AvailableEndRefreshInterval -00:00:00.0000001 must be above zero.");
            }
        }

        [TestMethod]
        public void Constructor_AvailableEndRefreshIntervalNotDividingADay_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                ArgumentException sevenMinutes = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromMinutes(7)));
                ArgumentException twoDays = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromDays(2)));
                fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromHours(8)).Dispose();
                fixture.CreateClient(options => options.AvailableEndRefreshInterval = TimeSpan.FromDays(1)).Dispose();

                StringAssert.Contains(sevenMinutes.Message, "AvailableEndRefreshInterval 00:07:00 must divide a day evenly.");
                StringAssert.Contains(twoDays.Message, "AvailableEndRefreshInterval 2.00:00:00 must divide a day evenly.");
            }
        }

        [TestMethod]
        public void AvailableEndRefusal_ByDefault_IsServeEmpty()
        {
            Assert.AreEqual(AvailableEndRefusal.ServeEmpty, new CachedDatabentoClientOptions().AvailableEndRefusal);
        }

        [TestMethod]
        public void Constructor_AvailableEndRefusalUndefined_IsRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                ArgumentException thrown = Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.AvailableEndRefusal = (AvailableEndRefusal)5));

                StringAssert.Contains(thrown.Message, "AvailableEndRefusal 5");
            }
        }
    }
}
