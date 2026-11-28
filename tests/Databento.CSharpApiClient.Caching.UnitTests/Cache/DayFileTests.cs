// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv dbn

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.UnitTests.Archive;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ZstdSharp;

namespace Databento.CSharpApiClient.Caching.UnitTests.Cache
{
    /// <summary>
    /// A day file must give back every value of the records written into it, for every schema the archive holds, as zstd-compressed DBN
    /// that Databento's tools read; a legacy day file, gzipped JSON, must still be read, as the same DBN records
    /// </summary>
    [TestClass]
    public class DayFileTests
    {
        private const string Option = "SPXW  261016C06600000";

        private static readonly DateTime Day = new DateTime(2026, 10, 16);

        private static readonly DateTime TsEvent = new DateTime(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc);

        private string directory;

        private string DbnPath => Path.Combine(this.directory, "day.dbn.zst");

        private string LegacyPath => Path.Combine(this.directory, "day.json.gz");

        [TestInitialize]
        public void TestInitialize()
        {
            this.directory = Path.Combine(Path.GetTempPath(), "DayFileTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.directory);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if(Directory.Exists(this.directory))
            {
                Directory.Delete(this.directory, recursive: true);
            }
        }

        [TestMethod]
        public async Task WriteThenRead_CbboJsonRecords_KeepEveryArchivedValue()
        {
            await this.AssertRoundTripsAsync(DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, CbboRecords());
        }

        [TestMethod]
        public async Task WriteThenRead_BboJsonRecords_KeepEveryArchivedValue()
        {
            BboRecordJson[] records =
            {
                new BboRecordJson
                {
                    Header = Header(RType.Bbo1M, 0),
                    Price = 21000.25,
                    Size = 3,
                    Side = TradeAggressor.Buyer,
                    Flags = MessageInfoBits.Last,
                    TsReceivedUtc = TsEvent.AddTicks(1234567),
                    Sequence = 77,
                    Levels = new[] { new Mbp1LevelJson { BidPrice = 21000.0, AskPrice = 21000.5, BidSize = 10, AskSize = 12, BidCount = 4, AskCount = 5 } },
                    Symbol = "MNQZ6",
                },
            };

            await this.AssertRoundTripsAsync(DayFileFormats.Bbo1Min, Datasets.GlbxMdp3, "MNQZ6", records);
        }

        [TestMethod]
        public async Task WriteThenRead_TcbboJsonRecords_KeepEveryArchivedValue()
        {
            TcbboRecordJson[] records =
            {
                new TcbboRecordJson
                {
                    Header = Header(RType.Tcbbo, 0),
                    Price = 4.05,
                    Size = 2,
                    Action = OrderBookAction.Trade,
                    Side = TradeAggressor.Seller,
                    Flags = MessageInfoBits.Last,
                    TsReceivedUtc = TsEvent.AddTicks(1234567),
                    TsInDelta = 5150,
                    Levels = new[] { new CbboLevel1RecordJson { BidPrice = 4.0, AskPrice = double.NaN, BidSize = 20, AskSize = 30, BidPublisherId = 21, AskPublisherId = 22 } },
                    Symbol = Option,
                },
            };

            await this.AssertRoundTripsAsync(DayFileFormats.Tcbbo, Datasets.OpraPillar, Option, records);
        }

        [TestMethod]
        public async Task WriteThenRead_TradeJsonRecords_KeepEveryArchivedValue()
        {
            TradeRecordJson[] records =
            {
                new TradeRecordJson
                {
                    Header = Header(RType.Mbp0, 0),
                    Price = 6600.25,
                    Size = 1,
                    Action = OrderBookAction.Trade,
                    Side = TradeAggressor.None,
                    Flags = MessageInfoBits.Last,
                    Depth = 3,
                    TsReceivedUtc = TsEvent.AddTicks(1234567),
                    TsInDelta = 950,
                    Sequence = 123456,
                    Symbol = "SPX",
                },
            };

            await this.AssertRoundTripsAsync(DayFileFormats.Trades, Datasets.DbixCore, "SPX", records);
        }

        [TestMethod]
        public async Task WriteThenRead_OhlcvJsonRecords_KeepEveryArchivedValue()
        {
            OhlcvRecordJson[] records =
            {
                new OhlcvRecordJson
                {
                    Header = Header(RType.Ohlcv1D, 0),
                    Open = 600.1,
                    High = 601.2,
                    Low = 599.3,
                    Close = 600.4,
                    Volume = 125000,
                    Symbol = "QQQ",
                },
            };

            await this.AssertRoundTripsAsync(DayFileFormats.Ohlcv1Day, Datasets.XnasItch, "QQQ", records);
        }

        [TestMethod]
        public async Task WriteThenRead_StatisticsJsonRecords_KeepEveryArchivedValue()
        {
            StatisticsRecordJson[] records =
            {
                new StatisticsRecordJson
                {
                    Header = Header(RType.Statistics, 0),
                    TsReceivedUtc = TsEvent.AddTicks(1234567),
                    TsRefUtc = TsEvent.AddDays(-1),
                    Price = double.NaN,
                    Quantity = 4200,
                    Sequence = 77,
                    TsInDelta = 5150,
                    StatType = 9,
                    ChannelId = 3,
                    UpdateAction = 1,
                    StatFlags = MessageInfoBits.Last,
                    Symbol = Option,
                },
                new StatisticsRecordJson
                {
                    // a price statistic, with no quantity and no reference time
                    Header = Header(RType.Statistics, 1),
                    TsReceivedUtc = TsEvent.AddMinutes(1),
                    TsRefUtc = null,
                    Price = 4.25,
                    Quantity = long.MaxValue,
                    StatType = 3,
                    Symbol = Option,
                },
            };

            await this.AssertRoundTripsAsync(DayFileFormats.Statistics, Datasets.OpraPillar, Option, records);
        }

        [TestMethod]
        public async Task WriteThenRead_FullyPopulatedDbnRecords_KeepEveryValue()
        {
            // every property set by reflection, so a field DBN or the conversion drops fails here
            await this.AssertFullyPopulatedRoundTripAsync<ICbboRecord, CbboRecordDbn>(DayFileFormats.Cbbo1Min);
            await this.AssertFullyPopulatedRoundTripAsync<IBboRecord, BboRecordDbn>(DayFileFormats.Bbo1Sec);
            await this.AssertFullyPopulatedRoundTripAsync<ITcbboRecord, TcbboRecordDbn>(DayFileFormats.Tcbbo);
            await this.AssertFullyPopulatedRoundTripAsync<ITradeRecord, TradeRecordDbn>(DayFileFormats.Trades);
            await this.AssertFullyPopulatedRoundTripAsync<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Min);
            await this.AssertFullyPopulatedRoundTripAsync<IStatisticsRecord, StatisticsRecordDbn>(DayFileFormats.Statistics);
        }

        [TestMethod]
        public async Task Write_DayOfRecords_IsZstdDbnNamingItsDatasetSchemaDayAndSymbol()
        {
            // what Databento's own tools need to read the file on its own
            CbboRecordJson second = CbboRecords()[1];
            second.Header.InstrumentId = 7654321;
            CbboRecordJson[] records = { CbboRecords()[0], second };

            Assert.IsTrue(await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, ToDbn(DayFileFormats.Cbbo1Min, records), overwrite: true, CancellationToken.None));

            DbnMetadata metadata = ReadMetadata(this.DbnPath);
            Assert.AreEqual(3, metadata.Version);
            Assert.AreEqual(Datasets.OpraPillar, metadata.Dataset);
            Assert.AreEqual(DbnSchema.Cbbo1M, metadata.Schema);
            Assert.AreEqual(new DateTimeOffset(Day, TimeSpan.Zero), metadata.Start);
            Assert.AreEqual(new DateTimeOffset(Day.AddDays(1), TimeSpan.Zero), metadata.End);
            Assert.AreEqual(DbnSType.RawSymbol, metadata.STypeIn);
            Assert.AreEqual(DbnSType.InstrumentId, metadata.STypeOut);
            CollectionAssert.AreEqual(new[] { Option }, metadata.Symbols);
            Assert.AreEqual(Option, metadata.Mappings.Single().RawSymbol);
            CollectionAssert.AreEqual(new[] { "1234567", "7654321" }, metadata.Mappings.Single().Intervals.Select(interval => interval.Symbol).ToArray(), "each instrument id of the day");
            Assert.IsTrue(metadata.Mappings.Single().Intervals.All(interval => interval.StartDate == 20261016 && interval.EndDate == 20261017));

            CbboRecordDbn[] read = await DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None);
            CollectionAssert.AreEqual(new[] { Option, Option }, read.Select(record => record.Symbol).ToArray(), "both instrument ids map to the symbol");
        }

        [TestMethod]
        public async Task WriteThenRead_EmptyDay_IsDbnOfNoRecords()
        {
            // a day cached as empty is final, and its file still names its symbol
            Assert.IsTrue(await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Ohlcv1Day, Datasets.XnasItch, "QQQ", Day, Array.Empty<OhlcvRecordDbn>(), overwrite: true, CancellationToken.None));

            Assert.AreEqual(0, (await DayFile.ReadAsync(this.DbnPath, DayFileFormats.Ohlcv1Day, CancellationToken.None)).Length);
            DbnMetadata metadata = ReadMetadata(this.DbnPath);
            CollectionAssert.AreEqual(new[] { "QQQ" }, metadata.Symbols);
            Assert.AreEqual(0, metadata.Mappings.Single().Intervals.Count);
        }

        [TestMethod]
        public async Task Write_FolderMissing_IsCreatedAndNoTemporaryFileIsLeft()
        {
            string path = Path.Combine(this.directory, Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, "2026", "10", "16", "day.dbn.zst");

            Assert.IsTrue(await DayFile.WriteAsync(path, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(4.05) }, overwrite: true, CancellationToken.None));

            CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)));
        }

        [TestMethod]
        public async Task Write_FileThereWithoutOverwrite_KeepsTheFileThere()
        {
            await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(4.05) }, overwrite: true, CancellationToken.None);

            bool written = await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(5.05), Cbbo(5.10) }, overwrite: false, CancellationToken.None);

            Assert.IsFalse(written);
            CbboRecordDbn[] read = await DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None);
            Assert.AreEqual(1, read.Length);
            Assert.AreEqual(4.05, read[0].Price);
            CollectionAssert.AreEqual(new[] { this.DbnPath }, Directory.GetFiles(this.directory), "the dropped write leaves no temporary file");
        }

        [TestMethod]
        public async Task Write_FileThereWithOverwrite_ReplacesIt()
        {
            await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(4.05) }, overwrite: true, CancellationToken.None);

            bool written = await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(5.05), Cbbo(5.10) }, overwrite: true, CancellationToken.None);

            Assert.IsTrue(written);
            CbboRecordDbn[] read = await DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None);
            CollectionAssert.AreEqual(new double?[] { 5.05, 5.10 }, read.Select(record => record.Price).ToArray());
        }

        [TestMethod]
        public async Task Write_RecordDbnCannotHold_ThrowsAndLeavesNoFile()
        {
            CbboRecordDbn record = Cbbo(4.05);
            record.Price = double.PositiveInfinity;

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { record }, overwrite: true, CancellationToken.None));

            Assert.AreEqual(0, Directory.GetFiles(this.directory).Length, "neither the day file nor its temporary file");
        }

        [TestMethod]
        public async Task Write_DatasetDbnMetadataCannotHold_ThrowsAnArgumentExceptionAndLeavesNoFile()
        {
            // the caller's mistake, not a record's: it is not taken for a day DBN cannot hold
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, "A.DATASET.NAME.TOO.LONG", Option, Day, new[] { Cbbo(4.05) }, overwrite: true, CancellationToken.None));

            Assert.AreEqual(0, Directory.GetFiles(this.directory).Length);
        }

        [TestMethod]
        public async Task Read_DbnFileThatIsNotZstd_Throws()
        {
            File.WriteAllBytes(this.DbnPath, new byte[] { 1, 2, 3 });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_ZstdOfSomethingElseThanDbn_Throws()
        {
            WriteZstd(this.DbnPath, Encoding.UTF8.GetBytes("not dbn"));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_DbnCutShort_Throws()
        {
            await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Cbbo1Min, Datasets.OpraPillar, Option, Day, new[] { Cbbo(4.05), Cbbo(4.10) }, overwrite: true, CancellationToken.None);
            byte[] dbn = ReadDecompressed(this.DbnPath);
            WriteZstd(this.DbnPath, dbn.AsSpan(0, dbn.Length - 10).ToArray());

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_DbnRecordsOfAnotherSchema_Throws()
        {
            // a TCBBO day file read as CBBO
            TcbboRecordDbn tcbbo = DayFileFormats.Tcbbo.RecordSchema.ToRecord(new TcbboRecordJson { Header = Header(RType.Tcbbo, 0), TsReceivedUtc = TsEvent, Price = 4.05, Side = TradeAggressor.Buyer, Levels = new[] { new CbboLevel1RecordJson() } });
            await DayFile.WriteAsync(this.DbnPath, DayFileFormats.Tcbbo, Datasets.OpraPillar, Option, Day, new[] { tcbbo }, overwrite: true, CancellationToken.None);

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_MissingFile_Throws()
        {
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => DayFile.ReadAsync(this.DbnPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_LegacyFile_GivesTheDbnRecordsOfItsJsonRecords()
        {
            CbboRecordJson[] records = CbboRecords();
            TestDayFiles.WriteLegacy(this.LegacyPath, records);

            CbboRecordDbn[] read = await DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None);

            AssertSameArchivedValues(DayFileFormats.Cbbo1Min.RecordSchema, records, read);
        }

        [TestMethod]
        public async Task Read_LegacyEmptyArray_IsAnEmptyDay()
        {
            WriteGzip(this.LegacyPath, " \n[]");

            Assert.AreEqual(0, (await DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None)).Length);
        }

        [TestMethod]
        public async Task Read_LegacyFileThatIsNotGzip_Throws()
        {
            File.WriteAllBytes(this.LegacyPath, new byte[] { 1, 2, 3 });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_LegacyGzipOfSomethingElseThanJsonRecords_Throws()
        {
            WriteGzip(this.LegacyPath, "not json");

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        [DataRow("an empty file")]
        [DataRow("an empty gzip payload")]
        [DataRow("whitespace only")]
        [DataRow("text before an array")]
        [DataRow("JSON lines")]
        public async Task Read_LegacyFileHoldingNoJsonArray_Throws(string holding)
        {
            // an empty array is a day cached as empty and final, so a file holding no array must not read as one, nor as records
            File.WriteAllBytes(this.LegacyPath, CacheFixture.NoArrayDayFile(holding));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_LegacyFile_HandsTheReaderTheArrayAsItIsDecompressed()
        {
            // handed over as it is decompressed, from the array's start on, rather than as the whole day decompressed into memory
            WriteGzip(this.LegacyPath, " \r\n" + JsonSerializer.Serialize(new[] { CbboJson(4.05), CbboJson(4.10), CbboJson(4.15) }));
            List<string> seen = new List<string>();
            DayFileFormat<ICbboRecord, CbboRecordDbn> noting = new DayFileFormat<ICbboRecord, CbboRecordDbn>(
                Schema.ConsolidatedBBO1Min,
                DbnSchema.Cbbo1M,
                new CbboArchiveSchema(RType.Cbbo1M),
                (stream, cancellationToken) => NoteStreamThenReadRecords(stream, seen, cancellationToken),
                DbnReader.ReadCbboAsync,
                DbnWriter.WriteCbboAsync);

            CbboRecordDbn[] read = await DayFile.ReadAsync(this.LegacyPath, noting, CancellationToken.None);

            Assert.AreEqual(3, read.Length);
            Assert.AreEqual(4.15, read[2].Price);
            CollectionAssert.AreEqual(new[] { "forward only", "starts with [" }, seen);
        }

        [TestMethod]
        public async Task Read_LegacyArrayWrittenByHand_IsReadAsWritten()
        {
            // indented over many lines, with strings holding brackets, commas, quotes, a backslash and a newline, as the serializer escapes them
            const string odd = "SPXW  ],{\"x\\\"}\n[";
            CbboRecordJson first = CbboJson(4.05);
            first.Symbol = odd;
            CbboRecordJson second = CbboJson(4.10);
            second.Symbol = "QQQ";
            WriteGzip(this.LegacyPath, "\r\n  " + JsonSerializer.Serialize(new[] { first, second }, new JsonSerializerOptions { WriteIndented = true }).Replace("\n", "\r\n") + " \r\n");

            CbboRecordDbn[] read = await DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None);

            Assert.AreEqual(2, read.Length);
            Assert.AreEqual(odd, read[0].Symbol);
            Assert.AreEqual(4.05, read[0].Price);
            Assert.AreEqual("QQQ", read[1].Symbol);
            Assert.AreEqual(4.10, read[1].Price);
        }

        [TestMethod]
        [DataRow("[", DisplayName = "an array not closed")]
        [DataRow("[{record}", DisplayName = "a record and no end")]
        [DataRow("[{record}] x", DisplayName = "text after the array")]
        [DataRow("[{record}][]", DisplayName = "a second array")]
        [DataRow("[,]", DisplayName = "a comma alone")]
        [DataRow("[{record},]", DisplayName = "a trailing comma")]
        [DataRow("[{record},,{record}]", DisplayName = "two commas")]
        [DataRow("[[{record}]]", DisplayName = "an array of arrays")]
        [DataRow("[null]", DisplayName = "a null")]
        [DataRow("[{record}{record}]", DisplayName = "records without a comma")]
        public async Task Read_LegacyArrayThatIsNotOneOfRecords_Throws(string json)
        {
            WriteGzip(this.LegacyPath, json.Replace("{record}", JsonSerializer.Serialize(CbboJson(4.05))));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_LegacyRecordsOfAnotherSchema_Throws()
        {
            // a TCBBO day file read as CBBO: the record type tells them apart
            TestDayFiles.WriteLegacy(this.LegacyPath, new[] { new TcbboRecordJson { Header = Header(RType.Tcbbo, 0), Price = 4.05, Side = TradeAggressor.Buyer } });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => DayFile.ReadAsync(this.LegacyPath, DayFileFormats.Cbbo1Min, CancellationToken.None));
        }

        private static RecordHeaderJson Header(RType recordType, int minute)
        {
            return new RecordHeaderJson
            {
                RecordType = recordType,
                PublisherId = 30,
                InstrumentId = 1234567,
                TsEventUtc = TsEvent.AddMinutes(minute),
            };
        }

        private static CbboRecordJson[] CbboRecords()
        {
            return new[]
            {
                new CbboRecordJson
                {
                    Header = Header(RType.Cbbo1M, 0),
                    TsReceivedUtc = TsEvent.AddTicks(1234567),
                    Side = TradeAggressor.Seller,
                    Price = 4.05,
                    Size = 7,
                    Flags = MessageInfoBits.Last,
                    Levels = new[] { new CbboLevel1RecordJson { BidPrice = 4.0, AskPrice = 4.1, BidSize = 120, AskSize = 95, BidPublisherId = 31, AskPublisherId = 32 } },
                    Symbol = Option,
                },
                new CbboRecordJson
                {
                    // a minute without a trade, quoted on one side only
                    Header = Header(RType.Cbbo1M, 1),
                    TsReceivedUtc = TsEvent.AddMinutes(1).AddTicks(7),
                    Side = TradeAggressor.None,
                    Price = null,
                    Levels = new[] { new CbboLevel1RecordJson { BidPrice = double.NaN, AskPrice = 0.05, AskSize = 3, AskPublisherId = 40 } },
                    Symbol = Option,
                },
            };
        }

        private static CbboRecordJson CbboJson(double price)
        {
            return new CbboRecordJson
            {
                Header = Header(RType.Cbbo1M, 0),
                Price = price,
                Side = TradeAggressor.None,
                TsReceivedUtc = TsEvent,
                Levels = new[] { new CbboLevel1RecordJson { BidPrice = price, AskPrice = price + 0.05 } },
            };
        }

        private static CbboRecordDbn Cbbo(double price)
        {
            return DayFileFormats.Cbbo1Min.RecordSchema.ToRecord(CbboJson(price));
        }

        private static TRecord[] ToDbn<TSource, TRecord>(DayFileFormat<TSource, TRecord> format, IEnumerable<TSource> records)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            return records.Select(format.RecordSchema.ToRecord).ToArray();
        }

        private static void AssertSameArchivedValues<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, TSource[] expected, TRecord[] actual)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for(int i = 0; i < expected.Length; ++i)
            {
                Assert.IsNull(schema.FindDifferentColumn(expected[i], actual[i]), Invariant($"records[{i}]"));
                Assert.AreEqual(expected[i].Symbol, actual[i].Symbol, Invariant($"records[{i}].Symbol"));
            }
        }

        /// <summary>
        /// Notes whether the stream a legacy day file hands its reader can seek and where it starts, then reads it as CBBO records
        /// </summary>
        private static async IAsyncEnumerable<CbboRecordJson> NoteStreamThenReadRecords(Stream stream, List<string> seen, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            seen.Add(stream.CanSeek ? "seekable" : "forward only");
            byte[] first = new byte[1];
            int read = await stream.ReadAsync(first, cancellationToken);
            seen.Add(read == 1 && first[0] == '[' ? "starts with [" : "starts elsewhere");

            using(MemoryStream rest = new MemoryStream())
            {
                rest.Write(first, 0, read);
                await stream.CopyToAsync(rest, cancellationToken);
                rest.Position = 0;
                await foreach(CbboRecordJson record in JsonRecordReader.ReadCbboAsync(rest, cancellationToken))
                {
                    yield return record;
                }
            }
        }

        private static DbnMetadata ReadMetadata(string path)
        {
            using(MemoryStream dbn = new MemoryStream(ReadDecompressed(path)))
            using(BinaryReader reader = new BinaryReader(dbn))
            {
                return DbnMetadata.FromBinaryReader(reader);
            }
        }

        private static byte[] ReadDecompressed(string path)
        {
            using(FileStream file = File.OpenRead(path))
            using(DecompressionStream zstd = new DecompressionStream(file))
            using(MemoryStream bytes = new MemoryStream())
            {
                zstd.CopyTo(bytes);
                return bytes.ToArray();
            }
        }

        private static void WriteZstd(string path, byte[] bytes)
        {
            using(FileStream file = File.Create(path))
            using(CompressionStream zstd = new CompressionStream(file))
            {
                zstd.Write(bytes, 0, bytes.Length);
            }
        }

        private static void WriteGzip(string path, string text)
        {
            using(FileStream file = File.Create(path))
            using(GZipStream json = new GZipStream(file, CompressionLevel.Optimal))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                json.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Writes JSON records as a day file and reads them back, then as a legacy day file and reads that back: both give DBN records of
        /// every archived value, under the day file's symbol
        /// </summary>
        private async Task AssertRoundTripsAsync<TSource, TRecord, TJson>(DayFileFormat<TSource, TRecord> format, string dataset, string symbol, TJson[] records)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            Assert.IsTrue(await DayFile.WriteAsync(this.DbnPath, format, dataset, symbol, Day, ToDbn<TSource, TRecord>(format, records), overwrite: true, CancellationToken.None));
            AssertSameArchivedValues(format.RecordSchema, records, await DayFile.ReadAsync(this.DbnPath, format, CancellationToken.None));

            TestDayFiles.WriteLegacy(this.LegacyPath, records);
            AssertSameArchivedValues(format.RecordSchema, records, await DayFile.ReadAsync(this.LegacyPath, format, CancellationToken.None));
        }

        private async Task AssertFullyPopulatedRoundTripAsync<TSource, TRecord>(DayFileFormat<TSource, TRecord> format)
            where TSource : class, IRecord
            where TRecord : class, TSource, new()
        {
            TRecord[] records = Enumerable.Range(1, 3).Select(ArchiveTestRecords.CreateFullyPopulated<TRecord>).ToArray();

            Assert.IsTrue(await DayFile.WriteAsync(this.DbnPath, format, Datasets.OpraPillar, Option, Day, records, overwrite: true, CancellationToken.None), format.Schema);
            TRecord[] read = await DayFile.ReadAsync(this.DbnPath, format, CancellationToken.None);

            ArchiveTestRecords.AssertSameValues<TSource>(records, read, Option);
        }
    }
}
