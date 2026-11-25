using System;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Archive
{
    [TestClass]
    public class ArchiveFileIdentityTests
    {
        private static readonly DateTime BuiltUtc = new DateTime(2026, 11, 18, 2, 15, 0, DateTimeKind.Utc);

        [TestMethod]
        public void FileName_FullFile_NamesDatasetSchemaGroupMonthCutoffAndBuildTime()
        {
            ArchiveFileIdentity identity = new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", 2026, 10, "UTC", BuiltUtc);

            Assert.AreEqual("OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", identity.FileName);
            Assert.AreEqual(ArchiveFileKind.Full, identity.Kind);
            Assert.AreEqual(0, identity.Generation);
        }

        [TestMethod]
        public void FileName_Delta_EndsInItsGeneration()
        {
            ArchiveFileIdentity identity = new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", 2026, 10, "UTC", BuiltUtc, 12);

            Assert.AreEqual("OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.delta.12.parquet", identity.FileName);
            Assert.AreEqual(ArchiveFileKind.Delta, identity.Kind);
            Assert.AreEqual(12, identity.Generation);
        }

        [TestMethod]
        public void FileName_EarlyMonth_IsPaddedToTwoDigits()
        {
            ArchiveFileIdentity identity = new ArchiveFileIdentity(Datasets.GlbxMdp3, Schema.Bbo1Min, "MNQ.FUT", 2026, 3, "UTC", BuiltUtc);

            Assert.AreEqual("GLBX.MDP3_bbo-1m_MNQ.FUT_2026-03.UTC_20261118T021500.full.parquet", identity.FileName);
        }

        [TestMethod]
        [DataRow("BRK B", "XNAS.ITCH_ohlcv-1d_BRK%20B_2026-10.UTC_20261118T021500.full.parquet", DisplayName = "a space")]
        [DataRow("A_B", "XNAS.ITCH_ohlcv-1d_A%5FB_2026-10.UTC_20261118T021500.full.parquet", DisplayName = "the name's separator")]
        [DataRow("ES/NQ", "XNAS.ITCH_ohlcv-1d_ES%2FNQ_2026-10.UTC_20261118T021500.full.parquet", DisplayName = "a path separator")]
        public void FileName_GroupOutsideANamePart_IsEscaped(string group, string expectedFileName)
        {
            // a group is the symbol itself for a stock, so it may hold anything a symbol does
            ArchiveFileIdentity identity = new ArchiveFileIdentity(Datasets.XnasItch, Schema.Ohlcv1Day, group, 2026, 10, "UTC", BuiltUtc);

            Assert.AreEqual(expectedFileName, identity.FileName);
            Assert.AreEqual(group, identity.Group);
        }

        [TestMethod]
        public void Constructor_NegativeGeneration_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", 2026, 10, "UTC", BuiltUtc, -1));
        }

        [TestMethod]
        public void BuiltUtc_TimeWithFractionsOfASecond_IsCutToWholeSeconds()
        {
            // the file name keeps whole seconds, and the index must name the same version as the file it sits in
            ArchiveFileIdentity identity = new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", 2026, 10, "UTC", BuiltUtc.AddTicks(9_999_999));

            Assert.AreEqual(BuiltUtc, identity.BuiltUtc);
            Assert.AreEqual(DateTimeKind.Utc, identity.BuiltUtc.Kind);
            Assert.AreEqual("OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", identity.FileName);
        }

        [TestMethod]
        [DataRow(DateTimeKind.Local)]
        [DataRow(DateTimeKind.Unspecified)]
        public void Constructor_BuildTimeNotUtc_Throws(DateTimeKind kind)
        {
            Assert.ThrowsException<ArgumentException>(() => new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", 2026, 10, "UTC", DateTime.SpecifyKind(BuiltUtc, kind)));
        }

        [TestMethod]
        [DataRow("OPRA_PILLAR", Schema.ConsolidatedBBO1Min, "UTC", "dataset")]
        [DataRow("OPRA/PILLAR", Schema.ConsolidatedBBO1Min, "UTC", "dataset")]
        [DataRow(Datasets.OpraPillar, "cbbo_1m", "UTC", "schema")]
        [DataRow(Datasets.OpraPillar, "cbbo\\1m", "UTC", "schema")]
        [DataRow(Datasets.OpraPillar, "cbbo*", "UTC", "schema")]
        [DataRow(Datasets.OpraPillar, "cbbo 1m", "UTC", "schema")]
        [DataRow(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "UT?", "cutoff")]
        [DataRow(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "U_TC", "cutoff")]
        [DataRow(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "", "cutoff")]
        public void Constructor_PartThatCannotSitInTheFileName_Throws(string dataset, string schema, string cutoff, string expectedParamName)
        {
            // '_' separates the name's parts, and a path separator or wildcard would turn the name into a path or a search pattern
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new ArchiveFileIdentity(dataset, schema, "SPXW.OPT", 2026, 10, cutoff, BuiltUtc));

            Assert.AreEqual(expectedParamName, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_NoGroup_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, string.Empty, 2026, 10, "UTC", BuiltUtc));
            Assert.ThrowsException<ArgumentNullException>(() => new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, null, 2026, 10, "UTC", BuiltUtc));
        }

        [TestMethod]
        [DataRow(2026, 0)]
        [DataRow(2026, 13)]
        [DataRow(0, 10)]
        [DataRow(10000, 1)]
        public void Constructor_NotAMonth_Throws(int year, int month)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ArchiveFileIdentity(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "SPXW.OPT", year, month, "UTC", BuiltUtc));
        }
    }
}
