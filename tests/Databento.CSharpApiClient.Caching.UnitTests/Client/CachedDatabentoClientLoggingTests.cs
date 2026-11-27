// Ignore Spelling: Cbbo

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// What the cached client tells its logger: downloads at Information, cache reads and saves at Debug, refusals and unreadable day files
    /// as warnings, an unreadable archive file as an error, and nothing formatted below the logger's level
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientLoggingTests
    {
        private const string Unlisted = "SPXW  261016C09999000";

        [TestMethod]
        public async Task GetCbbo1mAsync_DayNotCached_LogsTheDownloadBeforeAndAfterAtInformation()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day, 2));
                    fixture.Databento.BeforeTimeseries = () =>
                    {
                        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
                        return Task.CompletedTask;
                    };

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, "Serving cbbo-1m of OPRA.PILLAR for " + Call, "2026-10-16T00:00:00Z to 2026-10-17T00:00:00Z", "cache mode Use");
                    logger.AssertLogged(LogLevel.Information, "Requesting cbbo-1m of OPRA.PILLAR for " + Call + ", UTC day 2026-10-16 [2026-10-16T00:00:00Z to 2026-10-17T00:00:00Z) ...");
                    logger.AssertLogged(LogLevel.Information, "Got 2 cbbo-1m records of OPRA.PILLAR from Databento", "1 of the 1 symbol(s)", "Took 00:00:03");
                    logger.AssertLogged(LogLevel.Debug, "Saved " + Call + " 2026-10-16 cbbo-1m with 2 records as " + Path.GetFileName(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)));
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TwoDaysNotCached_LogsTheFirstAndLastDayAndTheHalfOpenWindow()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd.AddDays(1));

                    logger.AssertLogged(LogLevel.Information, "Requesting cbbo-1m of OPRA.PILLAR for " + Call + ", UTC days 2026-10-16 to 2026-10-17 [2026-10-16T00:00:00Z to 2026-10-18T00:00:00Z) ...");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayCachedByAnEarlierCall_LogsTheDayFileAndNoDownload()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                    logger.Clear();

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m served from " + Path.GetFileName(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)) + ", 1 records");
                    logger.AssertNotLogged(Path.GetDirectoryName(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)));
                    Assert.AreEqual(0, logger.Entries.Count(entry => entry.Level >= LogLevel.Information));
                    logger.AssertNotLogged("Requesting");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayInTheArchive_LogsTheArchiveFileItWasServedFrom()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string archive = fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1), Cbbo(Call, Day, 2) }));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.CacheOnly);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m served from " + Path.GetFileName(archive) + ", 2 records");
                    logger.AssertNotLogged(Path.GetDirectoryName(archive));
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CacheOnlyMiss_LogsTheDayNotCached()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    await Assert.ThrowsExceptionAsync<NotCachedException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.CacheOnly));

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m of OPRA.PILLAR is not cached");
                    logger.AssertNotLogged("Requesting");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayNotFinal_LogsItServedAndNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Clock.SetUtcNow(Day.AddHours(20));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m is served but not saved", "not final", "its UTC day has not ended 00:15:00 ago)");
                    logger.AssertNotLogged("Saved ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayWithinTheSettleDelay_LogsTheDelay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.Logger = logger;
                    options.SettleDelay = TimeSpan.FromHours(1);
                }))
                {
                    fixture.Clock.SetUtcNow(Day.AddDays(1).AddMinutes(30));
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m is served but not saved", "its UTC day has not ended 01:00:00 ago");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CheckAvailabilityFirstDayNotFinal_LogsTheAvailableEnd()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.Logger = logger;
                    options.CheckAvailabilityFirst = true;
                }))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                    fixture.Databento.Conditions[Day] = "degraded";

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m is served but not saved", "Databento has not found it final", "2026-11-01T00:00:00Z");
                    logger.AssertNotLogged("Saved ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_NothingAvailableYet_LogsTheSpanServedEmpty()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.Logger = logger;
                    options.CheckAvailabilityFirst = true;
                }))
                {
                    fixture.Databento.AvailableEndUtc = Day;

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, "not available yet", "2026-10-16T00:00:00Z");
                    logger.AssertNotLogged("Requesting");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayOfAnUnlistedSymbol_LogsItSavedAsEmpty()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Unlisted, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Unlisted + " 2026-10-16 cbbo-1m answered no records and is saved as empty as " + Path.GetFileName(fixture.DayFilePath(CacheFixture.Cbbo, Unlisted, Day)) + ":", "it was not listed");
                    Assert.AreEqual(1, logger.Find(LogLevel.Debug, Unlisted + " 2026-10-16 cbbo-1m answered no records").Length, "one line per empty day saved");
                    logger.AssertNotLogged("Saved ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDayOfAListedSymbol_LogsItNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records and is not saved", "not confirmed");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EmptyDaysNever_LogsTheDayNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.Logger = logger;
                    options.EmptyDays = EmptyDayPolicy.Never;
                }))
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records and is not saved", "EmptyDays is Never");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SymbologyLookupFails_LogsAWarningAndTheEmptyDayNotSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request => request.Path == "symbology.resolve" ? FakeDatabento.Error(HttpStatusCode.InternalServerError, "internal_error") : null;

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Warning, "symbology lookup for 1 empty symbol-day(s) of cbbo-1m", "not saved");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusedForAnUnlistedSymbol_LogsTheRetryWithTheListedOnesAsAWarning()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2));
                    fixture.Databento.BeforeTimeseries = () =>
                    {
                        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
                        return Task.CompletedTask;
                    };

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted, Put }, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Warning, "Databento refused cbbo-1m of OPRA.PILLAR for 3 symbol(s)", "Asking again with only the 2 listed symbol(s)");
                    Assert.AreEqual(2, logger.Find(LogLevel.Information, "Requesting cbbo-1m").Length);
                    // the retry alone, timed alone: the refused request took 3 s more
                    logger.AssertLogged(LogLevel.Information, "Got 2 cbbo-1m records", "2 of the 2 symbol(s) requested", "Took 00:00:03");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RefusalTheLookupCannotExplain_LogsAWarningThatNothingIsSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.BadRequest, "symbology_invalid_request") : null;

                    await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd));

                    logger.AssertLogged(LogLevel.Warning, "Databento refused cbbo-1m of OPRA.PILLAR for 2 symbol(s)", "nothing is saved");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassAnsweredEmpty_LogsTheCachedDayKept()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records on a bypass", "cached day is kept");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_BypassAnsweredEmptyWithReplace_LogsTheCachedDayReplaced()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options =>
                {
                    options.Logger = logger;
                    options.BypassMissAction = BypassMissAction.Replace;
                }))
                {
                    fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records and is saved as empty", "a bypass answered no records, so it replaces the cached day");
                    logger.AssertNotLogged("Saved ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFileCannotBeParsed_LogsAWarningThatItIsDeleted()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string dayFile = fixture.Cache.WriteUnreadableCbbo(Call, Day);
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 4));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Warning, "The day file " + dayFile, "cannot be parsed and is deleted");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ArchiveFileUnreadableAndTheHandlerThrows_LogsAnError()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string archive = fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                    File.WriteAllBytes(archive, new byte[] { 1, 2, 3 });
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 2));
                    client.ArchiveFileUnreadable += (sender, args) => throw new InvalidOperationException("alarm down");

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    FakeLogEntry entry = logger.AssertLogged(LogLevel.Error, "The archive file " + archive, "cannot be read", "read again after 00:10:00");
                    Assert.IsInstanceOfType<InvalidDataException>(entry.Exception);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TwoCallsForTheSameDayAtOnce_LogsTheWaitForTheOther()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                    TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    fixture.Databento.BeforeTimeseries = () => gate.Task;

                    Task<ICbboRecord[]> first = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                    await WaitForAsync(() => fixture.Databento.TimeseriesRequests.Count == 1);
                    Task<ICbboRecord[]> second = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                    await WaitForAsync(() => logger.Find(LogLevel.Debug, "Waiting for another request").Length == 1);
                    gate.SetResult();
                    await Task.WhenAll(first, second);

                    logger.AssertLogged(LogLevel.Debug, "Waiting for another request of the process downloading 1 symbol-day(s) of cbbo-1m");
                    Assert.AreEqual(1, logger.Find(LogLevel.Information, "Requesting").Length);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_LoggerAtInformation_FormatsNoDebugLine()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger { MinimumLevel = LogLevel.Information };
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                    Assert.AreNotEqual(0, logger.Entries.Count);
                    Assert.IsTrue(logger.Entries.All(entry => entry.Level >= LogLevel.Information), "Log is never called below the logger's level");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_LoggerSetToNull_ServesAsWithNoLogger()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = null))
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CultureOfAnotherCalendar_LogsDaysAndTimesInvariant()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                // the Thai Buddhist calendar would write 2026 as 2569
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                using(CachedClientFixture fixture = new CachedClientFixture())
                {
                    FakeLogger logger = new FakeLogger();
                    using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                    {
                        fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));

                        await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                        logger.AssertLogged(LogLevel.Information, "UTC day 2026-10-16", "[2026-10-16T00:00:00Z to 2026-10-17T00:00:00Z)");
                        logger.AssertNotLogged("2569");
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestMethod]
        public void Logger_SetToNull_ReadsAsTheNullLogger()
        {
            Assert.AreSame(NullLogger.Instance, new CachedDatabentoClientOptions().Logger);
            Assert.AreSame(NullLogger.Instance, new CachedDatabentoClientOptions { Logger = null }.Logger);
            Assert.AreSame(NullLogger.Instance, new ArchiveCompactorOptions { Logger = null }.Logger);
            Assert.AreSame(NullLogger.Instance, new CacheBackupOptions { Logger = null }.Logger);
            Assert.AreSame(NullLogger.Instance, new CacheRestoreOptions { Logger = null }.Logger);
        }

        [TestMethod]
        public async Task RunBatchJobAsync_NothingCached_LogsTheSubmittedJobAndWhatItSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    BatchReport report = await client.RunBatchJobAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart.AddDays(-1), DayEnd, 1m);

                    string jobId = fixture.Databento.Jobs.Single().Id;
                    logger.AssertLogged(LogLevel.Information, "Submitted batch job " + jobId, "cbbo-1m of OPRA.PILLAR");
                    logger.AssertLogged(LogLevel.Information, "Batch job " + jobId + " is done");
                    logger.AssertLogged(LogLevel.Information, "Downloaded batch job " + jobId, Invariant($"{report.SymbolDaysSaved} symbol-day(s) saved"));
                }
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_SameQuerySubmittedBefore_LogsTheReusedJob()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Jobs.Add(new FakeBatchJob
                    {
                        Id = "EARLIER-1",
                        ExpiresUtc = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(20),
                        Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["dataset"] = Datasets.OpraPillar,
                            ["schema"] = Schema.ConsolidatedBBO1Min,
                            ["symbols"] = Call + "," + Put,
                            ["start"] = "2026-10-15T00:00:00Z",
                            ["end"] = "2026-10-17T00:00:00Z",
                            ["encoding"] = "dbn",
                            ["compression"] = "zstd",
                            ["split_duration"] = "day",
                        },
                    });

                    await client.RunBatchJobAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Put, Call }, DayStart.AddDays(-1), DayEnd, 1m);

                    logger.AssertLogged(LogLevel.Information, "Reusing batch job EARLIER-1");
                    logger.AssertNotLogged("Submitted batch job");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_OneUnlistedSymbol_LogsTheRefusalWithNoneListedAndNoDownloadResult()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Unlisted, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Warning, "Databento refused cbbo-1m of OPRA.PILLAR for 1 symbol(s)", "None of them was listed");
                    Assert.AreEqual(1, logger.Find(LogLevel.Information, "Requesting").Length);
                    logger.AssertNotLogged("Got ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_Bypass_LogsNoCacheReadForItsCheckOfTheCachedDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd, CacheMode.Bypass);

                    logger.AssertLogged(LogLevel.Debug, Call + " 2026-10-16 cbbo-1m answered no records on a bypass");
                    logger.AssertNotLogged(" served from ");
                    logger.AssertNotLogged("is not cached");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DownloadFails_LogsAWarningWithTheException()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? FakeDatabento.Error(HttpStatusCode.Forbidden, "license_not_found_unauthorized") : null;

                    DatabentoHttpException thrown = await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd));

                    FakeLogEntry entry = logger.AssertLogged(LogLevel.Warning, "Downloading cbbo-1m of OPRA.PILLAR for " + Call + " from Databento failed");
                    Assert.AreSame(thrown, entry.Exception);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RetryWithTheListedSymbolsFails_LogsAWarningWithTheException()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    int requests = 0;
                    fixture.Databento.Respond = request => request.Path == "timeseries.get_range" && ++requests == 2 ? FakeDatabento.Error(HttpStatusCode.Forbidden, "license_not_found_unauthorized") : null;

                    await Assert.ThrowsExceptionAsync<DatabentoHttpException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Unlisted }, DayStart, DayEnd));

                    logger.AssertLogged(LogLevel.Warning, "Asking again with only the 1 listed symbol(s)");
                    Assert.IsInstanceOfType<DatabentoHttpException>(logger.AssertLogged(LogLevel.Warning, "Downloading cbbo-1m of OPRA.PILLAR for " + Call + " from Databento failed").Exception);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_CanceledByTheCaller_LogsNoFailure()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.BeforeTimeseries = () =>
                    {
                        cancellation.Cancel();
                        return Task.FromCanceled(cancellation.Token);
                    };

                    await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, ct: cancellation.Token));

                    logger.AssertNotLogged("failed");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFileSavedByAnotherWriterDuringTheDownload_LogsItKept()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Call, Day, 2));
                    fixture.Databento.BeforeTimeseries = () =>
                    {
                        fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                        return Task.CompletedTask;
                    };

                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    logger.AssertLogged(LogLevel.Debug, "Kept the day file " + Path.GetFileName(fixture.DayFilePath(CacheFixture.Cbbo, Call, Day)) + " already there", "the 2 records of " + Call);
                    logger.AssertNotLogged("Saved ");
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFileTheDiskCannotRead_LogsAWarningThatItIsLeft()
        {
            if(!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Only Windows refuses to open a file another handle holds open.");
            }

            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string dayFile = fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                    using(new FileStream(dayFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        await Assert.ThrowsExceptionAsync<NotCachedException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.CacheOnly));
                    }

                    logger.AssertLogged(LogLevel.Warning, "The day file " + dayFile, "cannot be read and is left as it is", "read from the archive, or downloaded as the cache mode allows");
                    Assert.IsTrue(File.Exists(dayFile));
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_UnparsableDayFileThatCannotBeDeleted_LogsAWarningThatItIsKept()
        {
            if(!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Only Windows refuses to delete a file another handle holds open.");
            }

            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string dayFile = fixture.Cache.WriteUnreadableCbbo(Call, Day);
                    using(new FileStream(dayFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await Assert.ThrowsExceptionAsync<NotCachedException>(() => client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.CacheOnly));
                    }

                    logger.AssertLogged(LogLevel.Warning, "The day file " + dayFile, "cannot be parsed and could not be deleted");
                    logger.AssertNotLogged("cannot be parsed and is deleted");
                    Assert.IsTrue(File.Exists(dayFile));
                }
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task GetCbbo1mAsync_LoggerThrows_ServesTheDayFileAndTheArchiveAndDownloadsAsWithout(int mode)
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = FakeLogger.Failing(mode);
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    string dayFile = fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                    fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2), Cbbo(Put, Day, 3) }));
                    List<ArchiveFileUnreadableEventArgs> raised = new List<ArchiveFileUnreadableEventArgs>();
                    client.ArchiveFileUnreadable += (sender, args) => raised.Add(args);
                    fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day.AddDays(1), 4));

                    ICbboRecord[] cached = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd, CacheMode.CacheOnly);
                    ICbboRecord[] downloaded = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayEnd, DayEnd.AddDays(1));

                    Assert.AreEqual(3, cached.Length);
                    Assert.AreEqual(0, raised.Count, "a good archive file is not reported unreadable");
                    Assert.IsTrue(File.Exists(dayFile), "a good day file is not deleted");
                    Assert.AreEqual(1, downloaded.Length);
                    Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length);
                    Assert.AreEqual(mode != 2, logger.Entries.Count > 0, "the lines were handed to the logger, unless IsEnabled failed");
                }
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_CultureWithACommaDecimal_LogsCostsAndTimesInvariant()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                using(CachedClientFixture fixture = new CachedClientFixture())
                {
                    FakeLogger logger = new FakeLogger();
                    using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                    {
                        fixture.Databento.Cost = 0.5m;
                        fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                        fixture.Databento.BeforeTimeseries = () =>
                        {
                            fixture.Clock.Advance(TimeSpan.FromSeconds(3.5));
                            return Task.CompletedTask;
                        };

                        await client.RunBatchJobAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart.AddDays(-1), DayEnd, 1.25m);
                        await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayEnd, DayEnd.AddDays(1));

                        logger.AssertLogged(LogLevel.Information, "Priced 1 new batch job(s) at 0.5 USD, within the ceiling of 1.25 USD");
                        logger.AssertLogged(LogLevel.Information, "priced at 0.5 USD");
                        logger.AssertLogged(LogLevel.Information, "Took 00:00:03.5000000");
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_PricedAboveTheCeiling_LogsAWarning()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.Cost = 7.40m;

                    await Assert.ThrowsExceptionAsync<BatchJobTooExpensiveException>(() => client.RunBatchJobAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart.AddDays(-1), DayEnd, 5m));

                    logger.AssertLogged(LogLevel.Warning, "1 new batch job(s) are priced at 7.4 USD, above the ceiling of 5 USD");
                    logger.AssertNotLogged("Submitted batch job");
                }
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobExpires_LogsAWarningWithTheException()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                FakeLogger logger = new FakeLogger();
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
                {
                    fixture.Databento.JobStates = new[] { "queued", "expired" };

                    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => client.RunBatchJobAsync(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, new[] { Call, Put }, DayStart.AddDays(-1), DayEnd, 1m));

                    FakeLogEntry entry = logger.AssertLogged(LogLevel.Warning, "Batch job " + fixture.Databento.Jobs.Single().Id + " could not be saved");
                    Assert.IsInstanceOfType<InvalidOperationException>(entry.Exception);
                }
            }
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
    }
}
