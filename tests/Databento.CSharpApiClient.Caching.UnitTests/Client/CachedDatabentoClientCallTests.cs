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
            ["cbbo-1s"] = Of(c => c.GetCbbo1sAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetCbbo1sAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetCbbo1s("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetCbbo1s("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["cbbo-1m"] = Of(c => c.GetCbbo1mAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetCbbo1mAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetCbbo1m("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetCbbo1m("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["bbo-1s"] = Of(c => c.GetBbo1sAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetBbo1sAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetBbo1s("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetBbo1s("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["bbo-1m"] = Of(c => c.GetBbo1mAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetBbo1mAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetBbo1m("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetBbo1m("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["tcbbo"] = Of(c => c.GetTcbboAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetTcbboAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetTcbbo("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetTcbbo("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["trades"] = Of(c => c.GetTradesAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetTradesAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetTrades("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetTrades("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["ohlcv-1s"] = Of(c => c.GetOhlcv1sAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1sAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetOhlcv1s("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1s("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["ohlcv-1m"] = Of(c => c.GetOhlcv1mAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1mAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetOhlcv1m("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1m("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["ohlcv-1h"] = Of(c => c.GetOhlcv1hAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1hAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetOhlcv1h("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1h("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["ohlcv-1d"] = Of(c => c.GetOhlcv1dAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1dAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetOhlcv1d("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetOhlcv1d("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
            ["statistics"] = Of(c => c.GetStatisticsAsync("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetStatisticsAsync("OPRA.PILLAR", Symbols, DayStart, DayEnd), c => c.GetStatistics("OPRA.PILLAR", Call, DayStart, DayEnd), c => c.GetStatistics("OPRA.PILLAR", Symbols, DayStart, DayEnd)),
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
            int calls = typeof(CachedDatabentoClient).GetMethods().Count(method => method.DeclaringType == typeof(CachedDatabentoClient) && method.Name.StartsWith("Get", StringComparison.Ordinal));

            Assert.AreEqual(calls, covered);
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_SymbolsMissingOtherDays_AskForEachSpanApart()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                fixture.Databento.Add("cbbo-1m", Cbbo(Call, Day.AddDays(1), 1), Cbbo(Put, Day, 1), Cbbo(Put, Day.AddDays(1), 1));

                ICbboRecord[] served = await client.GetCbbo1mAsync("OPRA.PILLAR", Symbols, DayStart, DayStart.AddDays(2));

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

                ICbboRecord[] served = await client.GetCbbo1mAsync("OPRA.PILLAR", new[] { Call, other }, DayStart, DayEnd);

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
