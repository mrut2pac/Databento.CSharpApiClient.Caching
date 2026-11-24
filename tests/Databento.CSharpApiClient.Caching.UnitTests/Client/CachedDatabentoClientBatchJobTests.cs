// Ignore Spelling: Cbbo dbn zstd

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// <see cref="CachedDatabentoClient.RunBatchJobAsync"/> against the fake's batch jobs, whose files are a sample folder with invented market values: the
    /// 2026-10-16 SPXW 6600 call and put over 2026-10-15 and 10-16, DBN, zstd, split by day
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientBatchJobTests
    {
        private static readonly DateTime First = Day.AddDays(-1);

        private static readonly DateTimeOffset RangeStart = new DateTimeOffset(First);

        private static readonly DateTimeOffset RangeEnd = DayEnd;

        [TestMethod]
        public async Task RunBatchJobAsync_NothingCached_SubmitsOneDbnJobOfTheWholeDaysAndSavesItsFiles()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart.AddHours(14), RangeEnd.AddHours(-3), 1m);

                FakeBatchJob job = fixture.Databento.Jobs.Single();
                CollectionAssert.AreEqual(new[] { Call, Put }, job.Symbols);
                Assert.AreEqual("cbbo-1m", job.Fields["schema"]);
                Assert.AreEqual(RangeStart, ParseTime(job.Fields["start"]));
                Assert.AreEqual(RangeEnd, ParseTime(job.Fields["end"]));
                Assert.AreEqual("dbn", job.Fields["encoding"]);
                Assert.AreEqual("zstd", job.Fields["compression"]);
                Assert.AreEqual("day", job.Fields["split_duration"]);
                Assert.AreEqual("true", job.Fields["split_symbols"]);
                CollectionAssert.AreEqual(new[] { job.Id }, report.JobIds.ToArray());
                Assert.AreEqual(0, report.ReusedJobIds.Count);
                Assert.AreEqual(0.000235m, report.CostUsd);
                Assert.AreEqual(4, report.SymbolDaysSaved);
                Assert.AreEqual(429, fixture.ReadCbboDayFile(Call, First).Length);
                Assert.AreEqual(331, fixture.ReadCbboDayFile(Put, Day).Length);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobQueuedThenProcessing_AsksForItsStateAtThePollInterval()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.BatchPollInterval = TimeSpan.FromSeconds(7)))
            {
                await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7) }, fixture.Clock.Waits.ToArray());
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_SomeSymbolDaysCached_AsksOnlyForTheSymbolsMissingThem()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, First, Cbbo(Call, First, 1));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                CollectionAssert.AreEqual(new[] { Put }, fixture.Databento.Jobs.Single().Symbols);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, First).Length);
                Assert.AreEqual(2, report.SymbolDaysSaved);
                Assert.AreEqual(4, report.SymbolDaysAlreadyCached);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_EverythingCached_SubmitsNothingAndPricesNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                foreach(string symbol in new[] { Call, Put })
                {
                    foreach(DateTime day in new[] { First, Day })
                    {
                        fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, symbol, day, Cbbo(symbol, day, 1));
                    }
                }

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(0, fixture.Databento.Jobs.Count);
                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_cost").Count);
                Assert.AreEqual(4, report.SymbolDaysAlreadyCached);
                Assert.AreEqual(0, report.JobIds.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_PricedAboveTheCeiling_ThrowsAndSubmitsNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Cost = 7.40m;

                BatchJobTooExpensiveException thrown = await Assert.ThrowsExceptionAsync<BatchJobTooExpensiveException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 5m));

                Assert.AreEqual(7.40m, thrown.EstimatedCostUsd);
                Assert.AreEqual(5m, thrown.MaxCostUsd);
                Assert.AreEqual(0, fixture.Databento.Jobs.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_SameQuerySubmittedBefore_DownloadsThatJobAgainWithoutPaying()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Jobs.Add(EarlierJob("EARLIER-1", fixture.Clock.GetUtcNow().UtcDateTime.AddDays(20)));

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Put, Call }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(1, fixture.Databento.Jobs.Count);
                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_cost").Count);
                CollectionAssert.AreEqual(new[] { "EARLIER-1" }, report.ReusedJobIds.ToArray());
                Assert.AreEqual(0, report.JobIds.Count);
                Assert.AreEqual(0m, report.CostUsd);
                Assert.AreEqual(4, report.SymbolDaysSaved);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobSubmittedBeforeExpiresWithinTheHour_IsNotReused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Jobs.Add(EarlierJob("EARLIER-1", fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(30)));

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(2, fixture.Databento.Jobs.Count);
                Assert.AreEqual(0, report.ReusedJobIds.Count);
                Assert.AreEqual(1, report.JobIds.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobSubmittedBeforeNotCoveringTheQuery_IsNotReused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                DateTime expires = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(20);
                FakeBatchJob otherSchema = EarlierJob("EARLIER-TCBBO", expires);
                otherSchema.Fields["schema"] = "tcbbo";
                FakeBatchJob shorter = EarlierJob("EARLIER-SHORTER", expires);
                shorter.Fields["end"] = "2026-10-16T00:00:00Z";
                FakeBatchJob fewerSymbols = EarlierJob("EARLIER-CALL", expires);
                fewerSymbols.Fields["symbols"] = Call;
                FakeBatchJob csv = EarlierJob("EARLIER-CSV", expires);
                csv.Fields["encoding"] = "csv";
                fixture.Databento.Jobs.AddRange(new[] { otherSchema, shorter, fewerSymbols, csv });

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(0, report.ReusedJobIds.Count);
                Assert.AreEqual(1, report.JobIds.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobSubmittedBeforeCoveringMore_IsReused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                FakeBatchJob wider = EarlierJob("EARLIER-WIDER", fixture.Clock.GetUtcNow().UtcDateTime.AddDays(20));
                wider.Fields["start"] = "2026-10-01T00:00:00Z";
                wider.Fields["end"] = "2026-10-20T00:00:00Z";
                wider.Fields["symbols"] = Put + ",SPXW  261016C06700000," + Call;
                wider.Fields["encoding"] = "json";
                wider.Fields["split_symbols"] = "true";
                fixture.Databento.Jobs.Add(wider);

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                CollectionAssert.AreEqual(new[] { "EARLIER-WIDER" }, report.ReusedJobIds.ToArray());
                Assert.AreEqual(1, fixture.Databento.Jobs.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_CachedDayBetweenMissingOnes_AsksForEachRunApart()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, First, Cbbo(Call, First, 1));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, First, Cbbo(Put, First, 1));

                await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart.AddDays(-1), RangeEnd, 1m);

                CollectionAssert.AreEqual(new[] { RangeStart.AddDays(-1), RangeStart.AddDays(1) }, fixture.Databento.Jobs.Select(job => ParseTime(job.Fields["start"])).ToArray());
                CollectionAssert.AreEqual(new[] { RangeStart, RangeEnd }, fixture.Databento.Jobs.Select(job => ParseTime(job.Fields["end"])).ToArray());
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_SymbolsInAnyOrder_AreChunkedInOrdinalOrder()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.MaxSymbolsPerRequest = 1))
            {
                await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Put, Call }, RangeStart, RangeEnd, 1m);

                CollectionAssert.AreEqual(new[] { Call, Put }, fixture.Databento.Jobs.SelectMany(job => job.Symbols).ToArray());
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_ArchiveFileUnreadable_ThrowsRatherThanBuyingItsMonth()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string archive = fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new Caching.Archive.ArchiveSymbolDay<DataModel.ICbboRecord>(Call, First, new DataModel.ICbboRecord[] { Cbbo(Call, First, 1) }));
                File.WriteAllBytes(archive, new byte[] { 1, 2, 3 });

                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m));

                Assert.AreEqual(0, fixture.Databento.Jobs.Count);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_OneJobFails_TheOthersAreStillSaved()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.MaxSymbolsPerRequest = 1))
            {
                fixture.Databento.WrongHashes.Add("opra-pillar-20261016.cbbo-1m.dbn.zst");
                fixture.Databento.WrongHashJob = "TEST-1";

                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m));

                Assert.AreEqual(2, fixture.Databento.Jobs.Count);
                Assert.AreEqual(331, fixture.ReadCbboDayFile(Put, Day).Length);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_DownloadFolderGiven_DownloadsThereAndClearsIt()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                string downloads = Path.Combine(fixture.Cache.Folder, "Downloads");
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.BatchDownloadFolder = downloads))
                {
                    BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                    Assert.AreEqual(4, report.SymbolDaysSaved);
                    Assert.IsTrue(Directory.Exists(downloads));
                    Assert.AreEqual(0, Directory.GetFileSystemEntries(downloads).Length);
                }
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_DayNotFinal_IsLeftOutOfTheJob()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Conditions[Day] = "pending";

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(DayStart, ParseTime(fixture.Databento.Jobs.Single().Fields["end"]));
                CollectionAssert.AreEqual(new[] { Day }, report.DaysLeftOut.ToArray());
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_NoDayFinal_SubmitsNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.AvailableEndUtc = First;

                BatchReport report = await client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(0, fixture.Databento.Jobs.Count);
                CollectionAssert.AreEqual(new[] { First, Day }, report.DaysLeftOut.ToArray());
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_JobExpires_Throws()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.JobStates = new[] { "queued", "expired" };

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m));

                Assert.IsNull(fixture.ReadCbboDayFile(Call, First));
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_FileNotMatchingItsHash_ThrowsAndSavesNothing()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.WrongHashes.Add("opra-pillar-20261016.cbbo-1m.dbn.zst");

                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m));

                Assert.IsNull(fixture.ReadCbboDayFile(Call, First));
            }
        }

        [TestMethod]
        public void RunBatchJob_Sync_SavesWhatTheAsyncCallSaves()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = client.RunBatchJob("OPRA.PILLAR", "cbbo-1m", new[] { Call, Put }, RangeStart, RangeEnd, 1m);

                Assert.AreEqual(4, report.SymbolDaysSaved);
            }
        }

        [TestMethod]
        public async Task RunBatchJobAsync_ArgumentsOutOfRange_AreRefusedBeforeAnyRequest()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "mbo", new[] { Call }, RangeStart, RangeEnd, 1m));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { "ALL_SYMBOLS" }, RangeStart, RangeEnd, 1m));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new string[0], RangeStart, RangeEnd, 1m));
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call }, RangeEnd, RangeEnd, 1m));
                await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => client.RunBatchJobAsync("OPRA.PILLAR", "cbbo-1m", new[] { Call }, RangeStart, RangeEnd, 0m));
                Assert.ThrowsException<ArgumentException>(() => fixture.CreateClient(options => options.BatchPollInterval = TimeSpan.Zero));

                Assert.AreEqual(0, fixture.Databento.RequestsTo("metadata.get_dataset_range").Count + fixture.Databento.Jobs.Count);
            }
        }

        private static FakeBatchJob EarlierJob(string id, DateTime expiresUtc)
        {
            return new FakeBatchJob
            {
                Id = id,
                ExpiresUtc = expiresUtc,
                Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dataset"] = "OPRA.PILLAR",
                    ["schema"] = "cbbo-1m",
                    ["symbols"] = Call + "," + Put,
                    ["start"] = "2026-10-15T00:00:00Z",
                    ["end"] = "2026-10-17T00:00:00Z",
                    ["encoding"] = "dbn",
                    ["compression"] = "zstd",
                    ["split_duration"] = "day",
                },
            };
        }

        private static DateTimeOffset ParseTime(string value)
        {
            return DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
        }
    }
}
