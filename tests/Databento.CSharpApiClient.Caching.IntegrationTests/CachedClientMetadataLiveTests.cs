using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Metadata;
using Databento.CSharpApiClient.DataModel.Symbology;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Live checks of the metadata and symbology calls the cached client passes through: each one reaches Databento and answers what the
    /// plain client answers. These calls are free, and downloads are forbidden throughout, so the tests cost nothing.
    /// </summary>
    [TestClass]
    public sealed class CachedClientMetadataLiveTests : LiveTestBase
    {
        private static readonly string DayText = Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static readonly string[] Symbols = { SpxwCall };

        [TestMethod]
        public async Task ListCalls_AnswerAsThePlainClient()
        {
            CachedDatabentoClient client = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out CountingTransport requests);
            requests.ForbidDownloads();

            CollectionAssert.AreEquivalent(await this.Reference.ListDatasetsAsync().ConfigureAwait(false), await client.ListDatasetsAsync().ConfigureAwait(false));
            CollectionAssert.AreEquivalent(await this.Reference.ListSchemasAsync(Datasets.OpraPillar).ConfigureAwait(false), await client.ListSchemasAsync(Datasets.OpraPillar).ConfigureAwait(false));
            CollectionAssert.AreEquivalent(
                (await this.Reference.ListPublishersAsync().ConfigureAwait(false)).Select(publisher => publisher.PublisherId).ToArray(),
                (await client.ListPublishersAsync().ConfigureAwait(false)).Select(publisher => publisher.PublisherId).ToArray());
            CollectionAssert.AreEqual(
                (await this.Reference.ListFieldsAsync(Schema.ConsolidatedBBO1Min).ConfigureAwait(false)).Select(field => field.Name).ToArray(),
                (await client.ListFieldsAsync(Schema.ConsolidatedBBO1Min).ConfigureAwait(false)).Select(field => field.Name).ToArray());
            CollectionAssert.AreEquivalent(
                (await this.Reference.ListUnitPricesAsync(Datasets.OpraPillar).ConfigureAwait(false)).Select(price => price.Mode).ToArray(),
                (await client.ListUnitPricesAsync(Datasets.OpraPillar).ConfigureAwait(false)).Select(price => price.Mode).ToArray());

            foreach(string path in new[] { "metadata.list_datasets", "metadata.list_schemas", "metadata.list_publishers", "metadata.list_fields", "metadata.list_unit_prices" })
            {
                Assert.AreEqual(1, requests.Count(path), path);
            }
        }

        [TestMethod]
        public async Task DatasetCalls_AnswerAsThePlainClient()
        {
            CachedDatabentoClient client = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out CountingTransport requests);
            requests.ForbidDownloads();

            DateRange expectedRange = await this.Reference.GetDatasetRangeAsync(Datasets.OpraPillar).ConfigureAwait(false);
            DateRange range = await client.GetDatasetRangeAsync(Datasets.OpraPillar).ConfigureAwait(false);
            DatasetCondition expectedCondition = await this.Reference.GetDatasetConditionAsync(Datasets.OpraPillar, DayText).ConfigureAwait(false);
            DatasetCondition condition = await client.GetDatasetConditionAsync(Datasets.OpraPillar, DayText).ConfigureAwait(false);
            DatasetCondition[] expectedConditions = await this.Reference.ListConditionsAsync(Datasets.OpraPillar, "2025-09-01", DayText).ConfigureAwait(false);
            DatasetCondition[] conditions = await client.ListConditionsAsync(Datasets.OpraPillar, "2025-09-01", DayText).ConfigureAwait(false);

            Assert.AreEqual(expectedRange.Start, range.Start);
            Assert.IsTrue(range.End >= expectedRange.End, "asked after the plain client, so its end is the same or later");
            Assert.AreEqual(expectedCondition.Date, condition.Date);
            Assert.AreEqual(expectedCondition.Condition, condition.Condition);
            CollectionAssert.AreEqual(expectedConditions.Select(day => day.Date + " " + day.Condition).ToArray(), conditions.Select(day => day.Date + " " + day.Condition).ToArray());
            Assert.AreEqual(1, requests.Count("metadata.get_dataset_range"));
            Assert.AreEqual(2, requests.Count("metadata.get_dataset_condition"));
        }

        [TestMethod]
        public async Task EstimatesAndSymbology_AnswerAsThePlainClient()
        {
            CachedDatabentoClient client = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out CountingTransport requests);
            requests.ForbidDownloads();
            DateTimeOffset end = Day.AddDays(1);
            SymbologyRequest request = new SymbologyRequest
            {
                Dataset = Datasets.OpraPillar,
                Symbols = Symbols,
                StartDate = DayText,
                EndDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };

            Assert.AreEqual(
                await this.Reference.GetRecordCountAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false),
                await client.GetRecordCountAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false));
            Assert.AreEqual(
                await this.Reference.GetBillableSizeAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false),
                await client.GetBillableSizeAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false));
            Assert.AreEqual(
                await this.Reference.GetCostAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false),
                await client.GetCostAsync(Datasets.OpraPillar, Symbols, Schema.ConsolidatedBBO1Min, Day, end).ConfigureAwait(false),
                1e-9);
            SymbologyResolution expected = await this.Reference.ResolveSymbolsAsync(request).ConfigureAwait(false);
            SymbologyResolution resolution = await client.ResolveSymbolsAsync(request).ConfigureAwait(false);

            CollectionAssert.AreEquivalent(expected.Result.Keys.ToArray(), resolution.Result.Keys.ToArray());
            CollectionAssert.Contains(resolution.Result.Keys.ToArray(), SpxwCall);
            Assert.AreEqual(0, requests.Count("timeseries.get_range"), "nothing was downloaded");
            Assert.AreEqual(1, requests.Count("symbology.resolve"));
        }
    }
}
