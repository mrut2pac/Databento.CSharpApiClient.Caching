// Ignore Spelling: Cbbo Bbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    [TestClass]
    public sealed class CachedDatabentoClientCallTests
    {
        private static readonly string[] Symbols = { Call, Put };

        /// <summary>
        /// Each call, async and sync, for one symbol and for several, by the schema it serves
        /// </summary>
        private static readonly Dictionary<string, Func<CachedDatabentoClient, Task>[]> Calls = new Dictionary<string, Func<CachedDatabentoClient, Task>[]>
        {
            [Schema.ConsolidatedBBO1Sec] = Of(c => c.GetCbbo1sAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetCbbo1sAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetCbbo1s(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetCbbo1s(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.ConsolidatedBBO1Min] = Of(c => c.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetCbbo1m(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetCbbo1m(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Bbo1Sec] = Of(c => c.GetBbo1sAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetBbo1sAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetBbo1s(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetBbo1s(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Bbo1Min] = Of(c => c.GetBbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetBbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetBbo1m(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetBbo1m(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Tcbbo] = Of(c => c.GetTcbboAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetTcbboAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetTcbbo(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetTcbbo(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Trades] = Of(c => c.GetTradesAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetTradesAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetTrades(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetTrades(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Ohlcv1Sec] = Of(c => c.GetOhlcv1sAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1sAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetOhlcv1s(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1s(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Ohlcv1Min] = Of(c => c.GetOhlcv1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetOhlcv1m(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1m(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Ohlcv1Hour] = Of(c => c.GetOhlcv1hAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1hAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetOhlcv1h(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1h(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Ohlcv1Day] = Of(c => c.GetOhlcv1dAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1dAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetOhlcv1d(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetOhlcv1d(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
            [Schema.Statistics] = Of(c => c.GetStatisticsAsync(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetStatisticsAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd), c => c.GetStatistics(Datasets.OpraPillar, Call, DayStart, DayEnd), c => c.GetStatistics(Datasets.OpraPillar, Symbols, DayStart, DayEnd)),
        };

        [TestMethod]
        public async Task EveryCall_AsksForItsSchemaAndSavesIntoItsFolder()
        {
            foreach(KeyValuePair<string, Func<CachedDatabentoClient, Task>[]> schema in Calls)
            {
                foreach(Func<CachedDatabentoClient, Task> call in schema.Value)
                {
                    using(CachedClientFixture fixture = new CachedClientFixture())
                    using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
                    {
                        await call(client);

                        Assert.AreEqual(schema.Key, fixture.Databento.TimeseriesRequests.Single().Fields["schema"], schema.Key);
                        Assert.IsTrue(System.IO.File.Exists(fixture.DayFilePath(schema.Key, Call, Day)), schema.Key);
                    }
                }
            }
        }

        [TestMethod]
        public void EveryPublicCall_IsCovered()
        {
            int covered = Calls.Values.Sum(calls => calls.Length);
            int calls = typeof(CachedDatabentoClient).GetMethods().Count(method => method.DeclaringType == typeof(CachedDatabentoClient) && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CacheMode)));

            Assert.AreEqual(calls, covered);
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SymbolsMissingOtherDays_AskForEachSpanApart()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day.AddDays(1), 1), Cbbo(Put, Day, 1), Cbbo(Put, Day.AddDays(1), 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayStart.AddDays(2));

                Assert.AreEqual(4, served.Length);
                IReadOnlyList<FakeRequest> requests = fixture.Databento.TimeseriesRequests;
                Assert.AreEqual(2, requests.Count);
                FakeRequest callRequest = requests.Single(request => request.Symbols.SequenceEqual(new[] { Call }));
                FakeRequest putRequest = requests.Single(request => request.Symbols.SequenceEqual(new[] { Put }));
                Assert.AreEqual(DayStart.AddDays(1), callRequest.StartUtc);
                Assert.AreEqual(DayStart, putRequest.StartUtc);
            }
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_AnswerHoldsASymbolNotAskedFor_ItsRecordsAreLeftOut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string other = "SPXW  261016C06700000";
                fixture.Databento.List(other);
                string lines = JsonSerializer.Serialize(Cbbo(Call, Day, 1)) + "\n" + JsonSerializer.Serialize(Cbbo(Put, Day, 2)) + "\n";
                fixture.Databento.Respond = request => request.Path == "timeseries.get_range" ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(lines) } : null;

                ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, new[] { Call, other }, DayStart, DayEnd);

                CollectionAssert.AreEqual(new[] { Call }, served.Select(record => record.Symbol).ToArray());
                Assert.IsNull(fixture.ReadCbboDayFile(Put, Day));
            }
        }

        private static Func<CachedDatabentoClient, Task>[] Of(Func<CachedDatabentoClient, Task> oneAsync, Func<CachedDatabentoClient, Task> severalAsync, Func<CachedDatabentoClient, object> one, Func<CachedDatabentoClient, object> several)
        {
            return new Func<CachedDatabentoClient, Task>[] { oneAsync, severalAsync, client => Task.FromResult(one(client)), client => Task.FromResult(several(client)) };
        }
    }
}
