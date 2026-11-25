// Ignore Spelling: Cbbo Ohlcv

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    [TestClass]
    public sealed class CachedDatabentoClientReadTests
    {
        [TestMethod]
        public async Task GetCbbo1mAsync_DayNotCached_DownloadsTheWholeDayAndServesTheRange()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, CbboAt(Call, Day.AddHours(10)), CbboAt(Call, Day.AddHours(14)), CbboAt(Call, Day.AddHours(14.5)), CbboAt(Call, Day.AddHours(15)), CbboAt(Call, Day.AddHours(20)));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart.AddHours(14), DayStart.AddHours(15));

                CollectionAssert.AreEqual(new[] { Day.AddHours(14), Day.AddHours(14.5) }, ReceivedTimes(served));
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(DayStart, fixture.Databento.TimeseriesRequests[0].StartUtc);
                Assert.AreEqual(DayEnd, fixture.Databento.TimeseriesRequests[0].EndUtc);
                CollectionAssert.AreEqual(new[] { Day.AddHours(10), Day.AddHours(14), Day.AddHours(14.5), Day.AddHours(15), Day.AddHours(20) }, ReceivedTimes(fixture.ReadCbboDayFile(Call, Day)));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DaySavedBefore_IsServedFromTheDayFile()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, CbboAt(Call, Day.AddHours(14.5)));
                using(CachedDatabentoClient client = fixture.CreateClient())
                {
                    await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                }

                using(CachedDatabentoClient client = fixture.CreateClient())
                {
                    ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                    CollectionAssert.AreEqual(new[] { Day.AddHours(14.5) }, ReceivedTimes(served));
                    Assert.AreEqual(Call, served[0].Symbol);
                    Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                }
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayArchived_IsServedFromTheArchiveWithoutDownloading()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1), Cbbo(Call, Day, 2) }));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1), Cbbo(Call, Day, 2)), ReceivedTimes(served));
                Assert.AreEqual(0, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayInTheDayFileAndTheArchive_IsServedFromTheDayFile()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 7));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 7)), ReceivedTimes(served));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_OneOfTwoSymbolsCached_DownloadsOnlyTheOther()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Put, Day, 2));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                CollectionAssert.AreEqual(new[] { Call, Put }, served.Select(record => record.Symbol).ToArray());
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                CollectionAssert.AreEqual(new[] { Put }, fixture.Databento.TimeseriesRequests[0].Symbols);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SeveralSymbols_ServedInTimeOrderAcrossSymbols()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 0), Cbbo(Put, Day, 2), Cbbo(Call, Day, 3));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                CollectionAssert.AreEqual(new[] { Put, Call, Put, Call }, served.Select(record => record.Symbol).ToArray());
                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1), Cbbo(Call, Day, 3)), ReceivedTimes(fixture.ReadCbboDayFile(Call, Day)));
                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Put, Day, 0), Cbbo(Put, Day, 2)), ReceivedTimes(fixture.ReadCbboDayFile(Put, Day)));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_MoreSymbolsThanARequestTakes_DownloadsInChunks()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.MaxSymbolsPerRequest = 2))
            {
                string[] symbols = Enumerable.Range(1, 5).Select(strike => Invariant($"SPXW  261016C0{6600 + strike}000")).ToArray();
                fixture.Databento.List(symbols);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, symbols.Select((symbol, i) => (IRecord)Cbbo(symbol, Day, i)).ToArray());

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, symbols, DayStart, DayEnd);

                Assert.AreEqual(5, served.Length);
                CollectionAssert.AreEqual(new[] { 2, 2, 1 }, fixture.Databento.TimeseriesRequests.Select(request => request.Symbols.Length).ToArray());
                Assert.IsTrue(symbols.All(symbol => fixture.ReadCbboDayFile(symbol, Day).Length == 1));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RangeOverThreeDays_SplitsTheAnswerByTheUtcDateOfTsRecv()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                DateTime second = Day.AddDays(1);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, CbboAt(Call, Day.AddHours(23.99)), CbboAt(Call, second), CbboAt(Call, Day.AddDays(2).AddHours(1)));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart.AddHours(12), DayStart.AddDays(2).AddHours(12));

                Assert.AreEqual(3, served.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(DayStart.AddDays(3), fixture.Databento.TimeseriesRequests[0].EndUtc);
                CollectionAssert.AreEqual(new[] { Day.AddHours(23.99) }, ReceivedTimes(fixture.ReadCbboDayFile(Call, Day)));
                CollectionAssert.AreEqual(new[] { second }, ReceivedTimes(fixture.ReadCbboDayFile(Call, second)));
                CollectionAssert.AreEqual(new[] { Day.AddDays(2).AddHours(1) }, ReceivedTimes(fixture.ReadCbboDayFile(Call, Day.AddDays(2))));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_RecordReceivedAfterTheRangeOfItsEvent_BelongsToTheDayItWasReceived()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                CbboRecordJson record = CbboAt(Call, Day.AddDays(1).AddSeconds(1));
                record.Header.TsEventUtc = Day.AddHours(23.99);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, record);

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayStart.AddDays(2));

                Assert.AreEqual(1, served.Length);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Day));
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day.AddDays(1)).Length);
            }
        }

        [TestMethod]
        public async Task GetOhlcv1mAsync_Bars_AreDayedByTsEvent()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.List("AAPL");
                fixture.Databento.Add(Schema.Ohlcv1Min, Bar("AAPL", Day.AddHours(23).AddMinutes(59)), Bar("AAPL", Day.AddDays(1)));

                IOhlcvRecord[] served = await client.GetOhlcv1mAsync(Datasets.XnasItch, "AAPL", DayStart, DayStart.AddDays(2));

                Assert.AreEqual(2, served.Length);
                Assert.IsTrue(File.Exists(fixture.DayFilePath(Schema.Ohlcv1Min, "AAPL", Day, Datasets.XnasItch)));
                Assert.IsTrue(File.Exists(fixture.DayFilePath(Schema.Ohlcv1Min, "AAPL", Day.AddDays(1), Datasets.XnasItch)));
                CollectionAssert.AreEqual(new[] { Day.AddHours(23).AddMinutes(59) }, served.Take(1).Select(bar => bar.TsEventUtc.Value).ToArray());
            }
        }

        [TestMethod]
        public void GetCbbo1m_Sync_ServesWhatTheAsyncCallServes()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2));

                ICbboRecord[] one = client.GetCbbo1m(Datasets.OpraPillar, Call, DayStart, DayEnd);
                ICbboRecord[] both = client.GetCbbo1m(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);

                Assert.AreEqual(1, one.Length);
                Assert.AreEqual(2, both.Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_JsonTransport_DownloadsWithNoDbnClient()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Sec, OneSecond(Call, 1));

                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, new[] { Call }, DayStart, DayEnd);

                Assert.IsNull(client.DbnClient, "a client that downloads JSON never needs one");
                Assert.AreEqual(1, served.Length);
                Assert.AreNotEqual("dbn", fixture.Databento.TimeseriesRequests[0].Fields.GetValueOrDefault("encoding"));
            }
        }

        [TestMethod]
        public void Constructor_DbnTransport_CreatesTheDbnClient()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.Transport = DatabentoTransport.Dbn))
            {
                Assert.IsNotNull(client.DbnClient);
            }
        }

        [TestMethod]
        public async Task GetCbbo1sAsync_DbnTransport_SavesTheSameJsonRecordsAsTheJsonTransport()
        {
            CbboRecordJson[] viaJson;
            CbboRecordJson[] viaDbn;
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Sec, OneSecond(Call, 1), OneSecond(Put, 2));
                await client.GetCbbo1sAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);
                viaJson = fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec);
            }

            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.Transport = DatabentoTransport.Dbn))
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Sec, OneSecond(Call, 1), OneSecond(Put, 2));
                ICbboRecord[] served = await client.GetCbbo1sAsync(Datasets.OpraPillar, new[] { Call, Put }, DayStart, DayEnd);
                viaDbn = fixture.ReadCbboDayFile(Call, Day, Schema.ConsolidatedBBO1Sec);

                Assert.AreEqual("dbn", fixture.Databento.TimeseriesRequests[0].Fields["encoding"]);
                CollectionAssert.AreEqual(new[] { Call, Put }, served.Select(record => record.Symbol).ToArray());
                Assert.IsInstanceOfType<CbboRecordJson>(served[0]);
            }

            Assert.AreEqual(1, viaDbn.Length);
            Assert.AreEqual(Call, viaDbn[0].Symbol);
            Assert.AreEqual(viaJson[0].TsReceivedUtc, viaDbn[0].TsReceivedUtc);
            Assert.AreEqual(((ICbboRecord)viaJson[0]).BidPrice, ((ICbboRecord)viaDbn[0]).BidPrice);
            Assert.AreEqual(((ICbboRecord)viaJson[0]).AskSize, ((ICbboRecord)viaDbn[0]).AskSize);
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_DayFileCannotBeParsed_IsDeletedAndDownloadedAgain()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteUnreadableCbbo(Call, Day);
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 4));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, fixture.ReadCbboDayFile(Call, Day).Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_ArchiveFileUnreadable_RaisesTheEventOnceADayAndDownloads()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string archive = fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                File.WriteAllBytes(archive, new byte[] { 1, 2, 3 });
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 2));
                fixture.Databento.Conditions[Day] = "pending";
                List<ArchiveFileUnreadableEventArgs> raised = new List<ArchiveFileUnreadableEventArgs>();
                client.ArchiveFileUnreadable += (sender, args) => raised.Add(args);

                // a pending day is not saved, so each call reads the archive again: the second past the 10 minutes, the file read again
                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                fixture.Clock.Advance(TimeSpan.FromMinutes(11));
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
                Assert.AreEqual(1, raised.Count);
                Assert.AreEqual(archive, raised[0].Path);
                Assert.IsInstanceOfType<InvalidDataException>(raised[0].Exception);
                Assert.IsTrue(File.Exists(archive));
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_EventHandlerThrows_TheReadStillDownloads()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string archive = fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                File.WriteAllBytes(archive, new byte[] { 1, 2, 3 });
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 2));
                client.ArchiveFileUnreadable += (sender, args) => throw new InvalidOperationException("alarm down");

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);

                Assert.AreEqual(1, served.Length);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TwoCallsForTheSameDayAtOnce_DownloadOnce()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Databento.BeforeTimeseries = () => gate.Task;

                Task<ICbboRecord[]> first = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await WaitForAsync(() => fixture.Databento.TimeseriesRequests.Count == 1);
                Task<ICbboRecord[]> second = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await Task.Delay(50);
                gate.SetResult();

                Assert.AreEqual(1, (await first).Length);
                Assert.AreEqual(1, (await second).Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TwoClientsOnOneRootAtOnce_DownloadOnce()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient firstClient = fixture.CreateClient())
            using(CachedDatabentoClient secondClient = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Databento.BeforeTimeseries = () => gate.Task;

                Task<ICbboRecord[]> first = firstClient.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await WaitForAsync(() => fixture.Databento.TimeseriesRequests.Count == 1);
                Task<ICbboRecord[]> second = secondClient.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await Task.Delay(50);
                gate.SetResult();

                Assert.AreEqual(1, (await first).Length);
                Assert.AreEqual(1, (await second).Length);
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_TheDownloadWaitedOnFails_TheWaitingCallDownloadsItself()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1));
                TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                int timeseriesCalls = 0;

                // the first download fails, but only once the second call is waiting on it
                fixture.Databento.BeforeTimeseries = async () =>
                {
                    if(System.Threading.Interlocked.Increment(ref timeseriesCalls) == 1)
                    {
                        await gate.Task;
                        throw new System.Net.Http.HttpRequestException("connection reset");
                    }
                };

                Task<ICbboRecord[]> first = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await WaitForAsync(() => fixture.Databento.TimeseriesRequests.Count == 1);
                Task<ICbboRecord[]> second = client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await Task.Delay(50);
                gate.SetResult();

                await Assert.ThrowsExceptionAsync<System.Net.Http.HttpRequestException>(() => first);
                Assert.AreEqual(1, (await second).Length);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            for(int i = 0; i < 200 && !condition(); ++i)
            {
                await Task.Delay(10);
            }

            Assert.IsTrue(condition());
        }

        private static CbboRecordJson OneSecond(string symbol, int second)
        {
            CbboRecordJson record = CbboAt(symbol, Day.AddHours(14).AddSeconds(second));
            record.Header.RecordType = RType.Cbbo1S;
            return record;
        }

        private static OhlcvRecordJson Bar(string symbol, DateTime eventUtc)
        {
            return new OhlcvRecordJson
            {
                Header = new RecordHeaderJson
                {
                    RecordType = RType.Ohlcv1M,
                    PublisherId = 2,
                    InstrumentId = 38,
                    TsEventUtc = eventUtc,
                },
                Open = 230,
                High = 231,
                Low = 229,
                Close = 230.5,
                Volume = 1000,
                Symbol = symbol,
            };
        }
    }
}
