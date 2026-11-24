using System;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Downloads one whole UTC day of every schema the cache holds over both transports, and checks the day against Databento: the
    /// cache files a record under the UTC date of the timestamp Databento filters a range on, so a day must hold exactly the records
    /// Databento counts for it.
    /// </summary>
    [TestClass]
    public sealed class CachedSchemaLiveTests : LiveTestBase
    {
        [TestMethod]
        [DataRow(Datasets.OpraPillar, "cbbo-1s", SpxwCall)]
        [DataRow(Datasets.OpraPillar, "cbbo-1m", SpxwCall)]
        [DataRow(Datasets.OpraPillar, "tcbbo", SpxwCall)]
        [DataRow(Datasets.OpraPillar, "statistics", SpxwCall)]
        [DataRow(Datasets.XnasItch, "trades", "QQQM")]
        [DataRow(Datasets.XnasItch, "bbo-1s", "SPY")]
        [DataRow(Datasets.XnasItch, "bbo-1m", "SPY")]
        [DataRow(Datasets.XnasItch, "ohlcv-1s", "SPY")]
        [DataRow(Datasets.XnasItch, "ohlcv-1m", "SPY")]
        [DataRow(Datasets.XnasItch, "ohlcv-1h", "SPY")]
        [DataRow(Datasets.XnasItch, "ohlcv-1d", "SPY")]
        public async Task WholeDay_BothTransports_HoldExactlyTheRecordsDatabentoCountsForTheDay(string dataset, string schema, string symbol)
        {
            DateTimeOffset end = Day.AddDays(1);
            await LiveBudget.ChargeAsync(this.Reference, dataset, schema, new[] { symbol }, Day, end, times: 2).ConfigureAwait(false);

            CachedDatabentoClient json = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out CountingTransport jsonRequests);
            CachedDatabentoClient dbn = this.OpenClient(this.NewFolder(), DatabentoTransport.Dbn, out CountingTransport dbnRequests);

            IRecord[] fromJson = await LiveSchemas.GetAsync(json, schema, dataset, symbol, Day, end).ConfigureAwait(false);
            IRecord[] fromDbn = await LiveSchemas.GetAsync(dbn, schema, dataset, symbol, Day, end).ConfigureAwait(false);
            long counted = await this.Reference.GetRecordCountAsync(dataset, new[] { symbol }, schema, Day, end).ConfigureAwait(false);

            // the cache trims a day by its own day stamp, so a stamp other than Databento's range filter shows up as a different count
            Assert.IsTrue(fromJson.Length > 0, "The day must have records, or the test proves nothing.");
            Assert.AreEqual(counted, fromJson.Length, "Databento's count for the day");
            CollectionAssert.AreEqual(AsText(fromJson), AsText(fromDbn), "The DBN transport must serve the records the JSON transport serves.");
            Assert.AreEqual(1, jsonRequests.Count("timeseries.get_range"), "The JSON transport downloads the day once.");
            Assert.AreEqual(1, dbnRequests.Count("timeseries.get_range"), "The DBN transport downloads the day once.");

            int sentBefore = jsonRequests.Total;
            jsonRequests.ForbidDownloads();
            IRecord[] again = await LiveSchemas.GetAsync(json, schema, dataset, symbol, Day, end).ConfigureAwait(false);

            Assert.AreEqual(sentBefore, jsonRequests.Total, "A day the cache holds is served without asking Databento.");
            CollectionAssert.AreEqual(AsText(fromJson), AsText(again));
        }
    }
}
