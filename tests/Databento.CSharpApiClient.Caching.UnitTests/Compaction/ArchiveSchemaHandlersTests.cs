using Databento.CSharpApiClient.Caching.Compaction;
using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Compaction
{
    [TestClass]
    public class ArchiveSchemaHandlersTests
    {
        [TestMethod]
        [DataRow(Schema.ConsolidatedBBO1Sec)]
        [DataRow(Schema.ConsolidatedBBO1Min)]
        [DataRow(Schema.Bbo1Sec)]
        [DataRow(Schema.Bbo1Min)]
        [DataRow(Schema.Tcbbo)]
        [DataRow(Schema.Trades)]
        [DataRow(Schema.Ohlcv1Sec)]
        [DataRow(Schema.Ohlcv1Min)]
        [DataRow(Schema.Ohlcv1Hour)]
        [DataRow(Schema.Ohlcv1Day)]
        [DataRow(Schema.OhlcvEod)]
        [DataRow(Schema.Statistics)]
        public void TryGet_ArchivedSchema_IsHandledUnderItsDatabentoName(string schema)
        {
            Assert.IsTrue(ArchiveSchemaHandlers.TryGet(schema, out IArchiveSchemaHandler handler));

            Assert.AreEqual(schema, handler.Schema);
        }

        [TestMethod]
        [DataRow(Schema.Mbo)]
        [DataRow(Schema.Mbp1)]
        [DataRow(Schema.Definition)]
        [DataRow("CBBO-1M")]
        [DataRow("CBBO1-Min")]
        [DataRow("")]
        public void TryGet_SchemaTheArchiveHasNoColumnsFor_ReturnsFalse(string schema)
        {
            Assert.IsFalse(ArchiveSchemaHandlers.TryGet(schema, out IArchiveSchemaHandler handler));
            Assert.IsNull(handler);
        }
    }
}
