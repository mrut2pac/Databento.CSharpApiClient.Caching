using System;
using System.IO;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Cache
{
    [TestClass]
    public class DayFileNameTests
    {
        [TestMethod]
        [DataRow("SPXW  261016C06600000", "SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst", DisplayName = "an OSI symbol")]
        [DataRow("MNQZ6", "MNQZ6_2026-10-16.UTC.dbn.zst", DisplayName = "a contract month")]
        [DataRow("A_B", "A%5FB_2026-10-16.UTC.dbn.zst", DisplayName = "a symbol holding the name's separator")]
        public void Format_Symbol_NamesTheEscapedSymbolDayAndCutoff(string symbol, string expectedFileName)
        {
            Assert.AreEqual(expectedFileName, DayFileName.Format(symbol, new DateTime(2026, 10, 16), "UTC"));
        }

        [TestMethod]
        public void Format_DayOfASingleDigitMonth_IsPadded()
        {
            Assert.AreEqual("QQQ_2026-03-05.UTC.dbn.zst", DayFileName.Format("QQQ", new DateTime(2026, 3, 5, 23, 59, 59), "UTC"));
        }

        [TestMethod]
        public void FormatLegacy_Symbol_NamesItAsFormatVersion1Did()
        {
            Assert.AreEqual("SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz", DayFileName.FormatLegacy("SPXW  261016C06600000", new DateTime(2026, 10, 16), "UTC"));
        }

        [TestMethod]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst", "SPXW  261016C06600000", DisplayName = "an OSI symbol")]
        [DataRow("A%5FB_2026-10-16.UTC.dbn.zst", "A_B", DisplayName = "a symbol holding the name's separator")]
        public void TryParse_DayFileName_GivesItsSymbolAndDay(string fileName, string expectedSymbol)
        {
            Assert.IsTrue(DayFileName.TryParse(fileName, "UTC", out string symbol, out DateTime date));

            Assert.AreEqual(expectedSymbol, symbol);
            Assert.AreEqual(new DateTime(2026, 10, 16), date);
        }

        [TestMethod]
        public void TryParseLegacy_LegacyDayFileName_GivesItsSymbolAndDay()
        {
            Assert.IsTrue(DayFileName.TryParseLegacy("SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz", "UTC", out string symbol, out DateTime date));

            Assert.AreEqual("SPXW  261016C06600000", symbol);
            Assert.AreEqual(new DateTime(2026, 10, 16), date);
        }

        [TestMethod]
        public void TryParseAndTryParseLegacy_NameOfTheOtherKind_ReturnFalse()
        {
            Assert.IsFalse(DayFileName.TryParse("QQQ_2026-10-16.UTC.json.gz", "UTC", out _, out _));
            Assert.IsFalse(DayFileName.TryParseLegacy("QQQ_2026-10-16.UTC.dbn.zst", "UTC", out _, out _));
        }

        [TestMethod]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.ET.dbn.zst", DisplayName = "another cutoff")]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.EST.dbn.zst", DisplayName = "another cutoff as long as UTC")]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.dbn.zst", DisplayName = "no cutoff")]
        [DataRow("SPXW%20%20261016C06600000_2026-02-30.UTC.dbn.zst", DisplayName = "a date that is none")]
        [DataRow("SPXW%20%20261016C06600000_2026-10-6.UTC.dbn.zst", DisplayName = "a day not written as two digits")]
        [DataRow("SPXW%20%20261016C06600000_20261016.UTC.dbn.zst", DisplayName = "a date of another format")]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst.0123456789abcdef0123456789abcdef.tmp", DisplayName = "a day file's temporary file")]
        [DataRow("SPXW%20%20261016C06600000_2026-10-16.UTC.dbn", DisplayName = "not compressed")]
        [DataRow("_2026-10-16.UTC.dbn.zst", DisplayName = "no symbol")]
        [DataRow("2026-10-16.UTC.dbn.zst", DisplayName = "no separator")]
        [DataRow("SPXW  261016C06600000_2026-10-16.UTC.dbn.zst", DisplayName = "a symbol not escaped")]
        [DataRow("%41_2026-10-16.UTC.dbn.zst", DisplayName = "a symbol escaped where it needs no escape")]
        [DataRow("OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", DisplayName = "an archive file")]
        public void TryParse_NameThatIsNoDayFileOfTheCutoff_ReturnsFalse(string fileName)
        {
            Assert.IsFalse(DayFileName.TryParse(fileName, "UTC", out string symbol, out DateTime date));
            Assert.IsNull(symbol);
            Assert.AreEqual(default(DateTime), date);
        }

        [TestMethod]
        public void TryParse_NoNameOrCutoff_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => DayFileName.TryParse(null, "UTC", out _, out _));
            Assert.ThrowsException<ArgumentException>(() => DayFileName.TryParse("QQQ_2026-10-16.UTC.dbn.zst", string.Empty, out _, out _));
            Assert.ThrowsException<ArgumentException>(() => DayFileName.Format("QQQ", new DateTime(2026, 10, 16), string.Empty));
        }

        [TestMethod]
        [DataRow("QQQ_2026-10-16.UTC.dbn.zst", true, false, DisplayName = "a day file")]
        [DataRow("QQQ_2026-10-16.UTC.json.gz", true, true, DisplayName = "a legacy day file")]
        [DataRow("OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", false, false, DisplayName = "an archive file")]
        [DataRow("metadata.json", false, false, DisplayName = "the metadata")]
        public void IsDayFileAndIsLegacy_Path_TellTheKind(string fileName, bool dayFile, bool legacy)
        {
            Assert.AreEqual(dayFile, DayFileName.IsDayFile(Path.Combine("10", fileName)));
            Assert.AreEqual(legacy, DayFileName.IsLegacy(Path.Combine("10", fileName)));
        }

        [TestMethod]
        public void GetMonthDirectory_EarlyMonth_IsTheDatasetSchemaYearAndPaddedMonthUnderTheRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "cache");

            Assert.AreEqual(Path.Combine(root, Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "2026", "07"), DayFileName.GetMonthDirectory(root, Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, 2026, 7));
        }

        [TestMethod]
        public void GetPathAndGetLegacyPath_Day_AreInTheDayFolderAndTheMonthFolder()
        {
            string month = Path.Combine(Path.GetTempPath(), "cache", Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "2026", "07");

            Assert.AreEqual(Path.Combine(month, "05"), DayFileName.GetDayDirectory(month, 5));
            Assert.AreEqual(Path.Combine(month, "05", "QQQ_2026-07-05.UTC.dbn.zst"), DayFileName.GetPath(month, "QQQ", new DateTime(2026, 7, 5), "UTC"));
            Assert.AreEqual(Path.Combine(month, "QQQ_2026-07-05.UTC.json.gz"), DayFileName.GetLegacyPath(month, "QQQ", new DateTime(2026, 7, 5), "UTC"));
        }
    }
}
