// Ignore Spelling: Cbbo Tcbbo Glbx dbn

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ZstdSharp;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// Folders kept under <c>Batch\Samples</c> in the layout of Databento batch jobs, with invented market values: the 2026-10-16
    /// SPXW 6600 call and put over 2026-10-15 and 10-16 in several encodings and splits, MNQZ6 on GLBX, and one job naming a
    /// contract that was never listed
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientBatchFolderTests
    {
        private const string Unlisted = "SPXW  261016C09999000";

        private static readonly DateTime First = Day.AddDays(-1);

        private static readonly DateTime Second = Day;

        [TestMethod]
        public async Task IngestBatchFolderAsync_DbnSplitByDay_SavesEachSymbolDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("A_dbn_zstd_day"));

                Assert.AreEqual(4, report.SymbolDaysSaved);
                Assert.AreEqual(0, report.SymbolDaysSavedEmpty);
                Assert.AreEqual(0, report.SymbolDaysAlreadyCached);
                Assert.AreEqual(0, report.DaysLeftOut.Count);
                CollectionAssert.AreEqual(new[] { 429, 429, 390, 331 }, new[] { Count(fixture, Call, First), Count(fixture, Put, First), Count(fixture, Call, Second), Count(fixture, Put, Second) });
                Assert.IsTrue(fixture.ReadCbboDayFile(Call, First).All(record => record.Symbol == Call && ((ICbboRecord)record).TsReceivedUtc.Value.Date == First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_JsonSplitBySymbol_SavesTheSameRecordsAsDbn()
        {
            using(CachedClientFixture dbn = new CachedClientFixture())
            using(CachedDatabentoClient dbnClient = dbn.CreateClient())
            using(CachedClientFixture json = new CachedClientFixture())
            using(CachedDatabentoClient jsonClient = json.CreateClient())
            {
                await dbnClient.IngestBatchFolderAsync(Sample("A_dbn_zstd_day"));
                BatchReport report = await jsonClient.IngestBatchFolderAsync(Sample("B_json_zstd_day_symbols"));

                Assert.AreEqual(4, report.SymbolDaysSaved);
                foreach(string symbol in new[] { Call, Put })
                {
                    foreach(DateTime day in new[] { First, Second })
                    {
                        AssertSameRecords(dbn.ReadCbboDayFile(symbol, day), json.ReadCbboDayFile(symbol, day));
                    }
                }
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_JsonWithoutSymbols_MapsInstrumentIdsThroughTheSymbologyLookup()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string folder = CopySample(fixture, "B_json_zstd_day_symbols");
                StripSymbols(folder);
                fixture.Databento.Mappings[Call] = new[] { (First, Day, 1342194942u), (Second, Second.AddDays(1), 1342200492u) };
                fixture.Databento.Mappings[Put] = new[] { (First, Day, 1342186460u), (Second, Second.AddDays(1), 1342186947u) };

                BatchReport report = await client.IngestBatchFolderAsync(folder);

                Assert.AreEqual(4, report.SymbolDaysSaved);
                Assert.AreEqual(331, Count(fixture, Put, Second));
                Assert.IsTrue(fixture.ReadCbboDayFile(Put, Second).All(record => record.Symbol == Put));
                Assert.AreEqual(1, fixture.Databento.SymbologyRequests.Count);
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_RangeStartingAndEndingInsideDays_LeavesThoseDaysOut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("C_dbn_none_partial_nosplit"));

                Assert.AreEqual(0, report.SymbolDaysSaved + report.SymbolDaysSavedEmpty);
                CollectionAssert.AreEqual(new[] { First, Second }, report.DaysLeftOut.ToArray());
                Assert.IsNull(fixture.ReadCbboDayFile(Call, First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_JsonOfAWeekUnsplit_SplitsByDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                int lines = File.ReadAllLines(Path.Combine(Sample("D_json_none_week_tcbbo"), "opra-pillar-20261015-20261016.tcbbo.json")).Length;

                BatchReport report = await client.IngestBatchFolderAsync(Sample("D_json_none_week_tcbbo"));

                int saved = new[] { Call, Put }.SelectMany(symbol => new[] { First, Second }.Select(day => ReadTcbbo(fixture, symbol, day))).Where(records => records != null).Sum(records => records.Length);
                Assert.AreEqual(lines, saved);
                Assert.IsTrue(report.SymbolDaysSaved > 0);
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_GlbxDbnVersion3_SavesTheFutureUnderItsDataset()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("E_dbn_zstd_day_glbx"));

                Assert.AreEqual(2, report.SymbolDaysSaved);
                string path = fixture.DayFilePath(Schema.Bbo1Min, "MNQZ6", First, Datasets.GlbxMdp3);
                BboRecordJson[] records = await DayFile.ReadAsync(path, JsonRecordReader.ReadBboAsync, CancellationToken.None);
                Assert.AreEqual(1390, records.Length);
                Assert.IsTrue(records.All(record => ((IBboRecord)record).TsReceivedUtc.Value.Date == First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_Statistics_SavesEachSymbolDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("F_dbn_zstd_day_statistics"));

                Assert.AreEqual(4, report.SymbolDaysSaved);
                StatisticsRecordJson[] records = await DayFile.ReadAsync(fixture.DayFilePath(Schema.Statistics, Put, Second), JsonRecordReader.ReadStatisticsAsync, CancellationToken.None);
                Assert.AreEqual(9, records.Length);
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_SymbolNeverListed_IsSavedAsEmptyOnceTheLookupConfirmsIt()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("H_unlisted_symbol"));

                Assert.AreEqual(2, report.SymbolDaysSaved);
                Assert.AreEqual(2, report.SymbolDaysSavedEmpty);
                Assert.AreEqual(0, fixture.ReadCbboDayFile(Unlisted, First).Length);
                Assert.AreEqual(429, Count(fixture, Call, First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_Never_SavesNoEmptyDay()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.Never))
            {
                BatchReport report = await client.IngestBatchFolderAsync(Sample("H_unlisted_symbol"));

                Assert.AreEqual(0, report.SymbolDaysSavedEmpty);
                Assert.IsNull(fixture.ReadCbboDayFile(Unlisted, First));
                Assert.AreEqual(0, fixture.Databento.SymbologyRequests.Count);
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_DayAlreadyCached_IsLeftAsItWas()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, First, Cbbo(Call, First, 1));

                BatchReport report = await client.IngestBatchFolderAsync(Sample("A_dbn_zstd_day"));

                Assert.AreEqual(3, report.SymbolDaysSaved);
                Assert.AreEqual(1, report.SymbolDaysAlreadyCached);
                Assert.AreEqual(1, Count(fixture, Call, First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_DayTheArchiveHolds_GetsNoDayFile()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Cache.WriteCbboFile(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 0, new Caching.Archive.ArchiveSymbolDay<ICbboRecord>(Call, First, new ICbboRecord[] { Cbbo(Call, First, 1) }));

                BatchReport report = await client.IngestBatchFolderAsync(Sample("A_dbn_zstd_day"));

                Assert.AreEqual(1, report.SymbolDaysAlreadyCached);
                Assert.IsNull(fixture.ReadCbboDayFile(Call, First));
                Assert.AreEqual(429, Count(fixture, Put, First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_ListedSymbolWithoutRecords_IsNotSavedAsEmpty()
        {
            FakeLogger logger = new FakeLogger();
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient(options => options.Logger = logger))
            {
                string quiet = "SPXW  261016C06700000";
                fixture.Databento.List(quiet);
                string folder = CopySample(fixture, "A_dbn_zstd_day");
                Rewrite(Path.Combine(folder, "metadata.json"), "\"SPXW  261016P06600000\"", "\"SPXW  261016P06600000\",\n            \"" + quiet + "\"");

                BatchReport report = await client.IngestBatchFolderAsync(folder);

                Assert.AreEqual(0, report.SymbolDaysSavedEmpty);
                Assert.IsNull(fixture.ReadCbboDayFile(quiet, First));
                logger.AssertLogged(Microsoft.Extensions.Logging.LogLevel.Debug, quiet + " 2026-10-15 cbbo-1m answered no records and is not saved", "has not confirmed it was not listed");
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_RecordsOfASymbolTheJobDidNotAskFor_AreLeftOut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string folder = CopySample(fixture, "A_dbn_zstd_day");
                Rewrite(Path.Combine(folder, "metadata.json"), "\"SPXW  261016C06600000\",\n            \"SPXW  261016P06600000\"", "\"SPXW  261016C06600000\"");

                BatchReport report = await client.IngestBatchFolderAsync(folder);

                Assert.AreEqual(2, report.SymbolDaysSaved);
                Assert.IsNull(fixture.ReadCbboDayFile(Put, First));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_SplitBySize_ReadsADaySpreadOverTwoFilesWhole()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string folder = CopySample(fixture, "B_json_zstd_day_symbols");
                Rewrite(Path.Combine(folder, "metadata.json"), "\"split_size\": null", "\"split_size\": 1000000000");
                File.Delete(Path.Combine(folder, "manifest.json"));
                foreach(string file in Directory.GetFiles(folder, "*.json.zst"))
                {
                    string[] lines = Decompress(file).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    string name = file.Substring(0, file.Length - ".json.zst".Length);
                    File.WriteAllText(name + ".a.json", string.Join("\n", lines.Take(lines.Length / 2)) + "\n");
                    File.WriteAllText(name + ".b.json", string.Join("\n", lines.Skip(lines.Length / 2)) + "\n");
                    File.Delete(file);
                }

                BatchReport report = await client.IngestBatchFolderAsync(folder);

                Assert.AreEqual(4, report.SymbolDaysSaved);
                Assert.AreEqual(429, Count(fixture, Call, First));
                Assert.AreEqual(331, Count(fixture, Put, Second));
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_DayNotAvailable_IsLeftOut()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string folder = CopySample(fixture, "A_dbn_zstd_day");
                string condition = Path.Combine(folder, "condition.json");
                File.WriteAllText(condition, File.ReadAllText(condition).Replace("\"date\": \"2026-10-16\",\n        \"condition\": \"available\"", "\"date\": \"2026-10-16\",\n        \"condition\": \"degraded\"").Replace("\"date\": \"2026-10-16\",\r\n        \"condition\": \"available\"", "\"date\": \"2026-10-16\",\r\n        \"condition\": \"degraded\""));
                Assert.IsTrue(File.ReadAllText(condition).Contains("degraded", StringComparison.Ordinal));

                BatchReport report = await client.IngestBatchFolderAsync(folder);

                Assert.AreEqual(2, report.SymbolDaysSaved);
                CollectionAssert.AreEqual(new[] { Second }, report.DaysLeftOut.ToArray());
                Assert.IsNull(fixture.ReadCbboDayFile(Call, Second));
            }
        }

        [TestMethod]
        public void IngestBatchFolder_Sync_SavesWhatTheAsyncCallSaves()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                BatchReport report = client.IngestBatchFolder(Sample("F_dbn_zstd_day_statistics"));

                Assert.AreEqual(4, report.SymbolDaysSaved);
            }
        }

        [TestMethod]
        public async Task IngestBatchFolderAsync_FoldersItCannotTake_AreRefused()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => client.IngestBatchFolderAsync(Path.Combine(fixture.Cache.Folder, "none")));

                string noMetadata = CopySample(fixture, "A_dbn_zstd_day", "noMetadata");
                File.Delete(Path.Combine(noMetadata, "metadata.json"));
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(noMetadata));

                string csv = CopySample(fixture, "A_dbn_zstd_day", "csv");
                Rewrite(Path.Combine(csv, "metadata.json"), "\"encoding\": \"dbn\"", "\"encoding\": \"csv\"");
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(csv));

                string parent = CopySample(fixture, "A_dbn_zstd_day", "parent");
                Rewrite(Path.Combine(parent, "metadata.json"), "\"stype_in\": \"raw_symbol\"", "\"stype_in\": \"parent\"");
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(parent));

                string mbo = CopySample(fixture, "A_dbn_zstd_day", Schema.Mbo);
                Rewrite(Path.Combine(mbo, "metadata.json"), "\"schema\": \"cbbo-1m\"", "\"schema\": \"mbo\"");
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(mbo));

                string missing = CopySample(fixture, "A_dbn_zstd_day", "missing");
                File.Delete(Path.Combine(missing, "opra-pillar-20261016.cbbo-1m.dbn.zst"));
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(missing));

                string cut = CopySample(fixture, "A_dbn_zstd_day", "cut");
                string cutFile = Path.Combine(cut, "opra-pillar-20261015.cbbo-1m.dbn.zst");
                File.WriteAllBytes(cutFile, File.ReadAllBytes(cutFile).Take(100).ToArray());
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(cut));

                string mixed = CopySample(fixture, "A_dbn_zstd_day", "mixed");
                File.WriteAllText(Path.Combine(mixed, "opra-pillar-20261015.cbbo-1m.csv"), "ts_recv\n");
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.IngestBatchFolderAsync(mixed));

                Assert.AreEqual(0, Directory.Exists(fixture.Cache.CbboDirectory) ? Directory.GetFiles(fixture.Cache.CbboDirectory).Length : 0);
            }
        }

        internal static string Sample(string name)
        {
            return Path.Combine(FakeDatabento.SamplesFolder, name);
        }

        private static string CopySample(CachedClientFixture fixture, string name, string copy = null)
        {
            string folder = Path.Combine(fixture.Cache.Folder, "Batch", copy ?? name);
            Directory.CreateDirectory(folder);
            foreach(string file in Directory.GetFiles(Sample(name)))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            return folder;
        }

        private static void Rewrite(string path, string from, string to)
        {
            string text = File.ReadAllText(path);
            Assert.IsTrue(text.Contains(from, StringComparison.Ordinal), from);
            File.WriteAllText(path, text.Replace(from, to));
        }

        /// <summary>
        /// Rewrites a JSON folder as one made without <c>map_symbols</c>: its files decompressed, and no record naming its symbol
        /// </summary>
        private static void StripSymbols(string folder)
        {
            Rewrite(Path.Combine(folder, "metadata.json"), "\"map_symbols\": true", "\"map_symbols\": false");
            File.Delete(Path.Combine(folder, "manifest.json"));
            foreach(string file in Directory.GetFiles(folder, "*.json.zst"))
            {
                File.WriteAllText(file.Substring(0, file.Length - ".zst".Length), Regex.Replace(Decompress(file), ",\"symbol\":\"[^\"]*\"", string.Empty));
                File.Delete(file);
            }
        }

        private static string Decompress(string file)
        {
            using(FileStream input = File.OpenRead(file))
            using(DecompressionStream zstd = new DecompressionStream(input))
            using(StreamReader reader = new StreamReader(zstd, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static int Count(CachedClientFixture fixture, string symbol, DateTime day)
        {
            return fixture.ReadCbboDayFile(symbol, day).Length;
        }

        private static TcbboRecordJson[] ReadTcbbo(CachedClientFixture fixture, string symbol, DateTime day)
        {
            string path = fixture.DayFilePath(Schema.Tcbbo, symbol, day);
            return File.Exists(path) ? DayFile.ReadAsync(path, JsonRecordReader.ReadTcbboAsync, CancellationToken.None).GetAwaiter().GetResult() : null;
        }

        private static void AssertSameRecords(CbboRecordJson[] expected, CbboRecordJson[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for(int i = 0; i < expected.Length; ++i)
            {
                ICbboRecord left = expected[i];
                ICbboRecord right = actual[i];
                Assert.AreEqual(left.TsReceivedUtc, right.TsReceivedUtc);
                Assert.AreEqual(left.TsEventUtc, right.TsEventUtc);
                Assert.AreEqual(left.InstrumentId, right.InstrumentId);
                Assert.AreEqual(left.RecordType, right.RecordType);
                Assert.AreEqual(left.Price, right.Price);
                Assert.AreEqual(left.BidPrice, right.BidPrice);
                Assert.AreEqual(left.AskPrice, right.AskPrice);
                Assert.AreEqual(left.BidSize, right.BidSize);
                Assert.AreEqual(left.AskSize, right.AskSize);
                Assert.AreEqual(left.Symbol, right.Symbol);
            }
        }
    }
}
