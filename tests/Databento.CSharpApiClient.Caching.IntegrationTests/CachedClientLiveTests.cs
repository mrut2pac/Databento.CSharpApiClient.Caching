using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Symbology;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Live checks of the cached client beyond one day of one schema: a range across UTC midnight, empty days, batch jobs, the parent
    /// symbols the archive groups by, and a cache that goes through compaction, backup and restore
    /// </summary>
    [TestClass]
    public sealed class CachedClientLiveTests : LiveTestBase
    {
        private const string MicroNasdaqDecember = "MNQZ5";

        [TestMethod]
        public async Task RangeAcrossUtcMidnight_DownloadsBothDaysWhole_ServesOnlyTheRange()
        {
            DateTimeOffset start = Day.AddHours(-2);
            DateTimeOffset end = Day.AddHours(2);
            string[] symbols = { MicroNasdaqDecember };
            await LiveBudget.ChargeAsync(this.Reference, Datasets.GlbxMdp3, "ohlcv-1m", symbols, Day.AddDays(-1), Day.AddDays(1)).ConfigureAwait(false);
            string root = this.NewFolder();
            CachedDatabentoClient client = this.OpenClient(root, DatabentoTransport.Json, out CountingTransport requests);

            IOhlcvRecord[] served = await client.GetOhlcv1mAsync(Datasets.GlbxMdp3, MicroNasdaqDecember, start, end).ConfigureAwait(false);
            int sentBefore = requests.Total;
            requests.ForbidDownloads();
            IOhlcvRecord[] wholeDays = await client.GetOhlcv1mAsync(Datasets.GlbxMdp3, MicroNasdaqDecember, Day.AddDays(-1), Day.AddDays(1)).ConfigureAwait(false);
            long countedRange = await this.Reference.GetRecordCountAsync(Datasets.GlbxMdp3, symbols, "ohlcv-1m", start, end).ConfigureAwait(false);
            long countedDays = await this.Reference.GetRecordCountAsync(Datasets.GlbxMdp3, symbols, "ohlcv-1m", Day.AddDays(-1), Day.AddDays(1)).ConfigureAwait(false);

            Assert.AreEqual(countedRange, served.Length, "Databento's count for the range");
            Assert.IsTrue(served.Any(record => record.TsEventUtc < Day.UtcDateTime) && served.Any(record => record.TsEventUtc >= Day.UtcDateTime), "The range must have records on both days.");
            Assert.AreEqual(sentBefore, requests.Total, "Both whole days are served from the cache.");
            Assert.AreEqual(countedDays, wholeDays.Length, "Both days were saved whole.");
        }

        [TestMethod]
        public async Task EnsureCached_TwoContracts_SavesThemOnceAndThenFindsThemWithoutAsking()
        {
            const string SpxwPut = "SPXW  250908P06475000";
            string[] symbols = { SpxwCall, SpxwPut };
            await LiveBudget.ChargeAsync(this.Reference, Datasets.OpraPillar, "cbbo-1m", symbols, Day, Day.AddDays(1)).ConfigureAwait(false);
            string root = this.NewFolder();
            CachedDatabentoClient client = this.OpenClient(root, DatabentoTransport.Json, out CountingTransport requests);

            EnsureCachedReport first = await client.EnsureCachedAsync(Datasets.OpraPillar, "cbbo-1m", symbols, Day, Day.AddDays(1)).ConfigureAwait(false);
            requests.ForbidDownloads();
            int sentBefore = requests.Total;
            EnsureCachedReport again = await client.EnsureCachedAsync(Datasets.OpraPillar, "cbbo-1m", symbols, Day, Day.AddDays(1)).ConfigureAwait(false);
            ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, symbols, Day, Day.AddDays(1), CacheMode.CacheOnly).ConfigureAwait(false);
            long counted = await this.Reference.GetRecordCountAsync(Datasets.OpraPillar, symbols, "cbbo-1m", Day, Day.AddDays(1)).ConfigureAwait(false);

            Assert.AreEqual(2, first.Saved);
            Assert.AreEqual(0, first.NotCached.Count);
            Assert.AreEqual(1, requests.Count("timeseries.get_range"), "Both contracts in one request.");
            Assert.AreEqual(2, again.AlreadyCached);
            Assert.AreEqual(sentBefore, requests.Total, "The second call asks Databento nothing.");
            Assert.AreEqual(counted, served.Length, "Databento's count for the day");
        }

        [TestMethod]
        public async Task DayBeforeListing_IsSavedEmpty_AndNotAskedForAgain()
        {
            DateTimeOffset beforeListing = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
            await LiveBudget.ChargeAsync(this.Reference, Datasets.OpraPillar, "cbbo-1m", new[] { SpxwCall }, beforeListing, beforeListing.AddDays(1)).ConfigureAwait(false);
            string root = this.NewFolder();
            CachedDatabentoClient client = this.OpenClient(root, DatabentoTransport.Json, out CountingTransport requests);

            ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, beforeListing, beforeListing.AddDays(1)).ConfigureAwait(false);
            int sentBefore = requests.Total;
            requests.ForbidDownloads();
            ICbboRecord[] again = await client.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, beforeListing, beforeListing.AddDays(1)).ConfigureAwait(false);

            Assert.AreEqual(0, served.Length);
            Assert.AreEqual(0, again.Length);
            Assert.IsTrue(File.Exists(DayFilePath(root, Datasets.OpraPillar, "cbbo-1m", SpxwCall, beforeListing)), "Symbology confirms the contract was not listed, so the empty day is saved.");
            Assert.AreEqual(sentBefore, requests.Total, "A saved empty day is not asked for again.");
        }

        [TestMethod]
        public async Task WeekendWhileListed_IsServedEmpty_ButNotSaved()
        {
            DateTimeOffset saturday = Day.AddDays(1);
            await LiveBudget.ChargeAsync(this.Reference, Datasets.OpraPillar, "cbbo-1m", new[] { SpxwCall }, saturday, saturday.AddDays(1), times: 2).ConfigureAwait(false);
            string root = this.NewFolder();
            CachedDatabentoClient client = this.OpenClient(root, DatabentoTransport.Json, out CountingTransport requests);

            ICbboRecord[] served = await client.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, saturday, saturday.AddDays(1)).ConfigureAwait(false);
            int sentBefore = requests.Count("timeseries.get_range");
            await client.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, saturday, saturday.AddDays(1)).ConfigureAwait(false);

            Assert.AreEqual(0, served.Length);
            Assert.IsFalse(File.Exists(DayFilePath(root, Datasets.OpraPillar, "cbbo-1m", SpxwCall, saturday)), "The contract was listed over the weekend, so the empty answer is not saved.");
            Assert.AreEqual(sentBefore + 1, requests.Count("timeseries.get_range"), "An empty day that is not saved is asked for again.");
        }

        [TestMethod]
        [DataRow(Datasets.OpraPillar, "SPXW.OPT", SpxwCall)]
        [DataRow(Datasets.GlbxMdp3, "MNQ.FUT", MicroNasdaqDecember)]
        public async Task ParentSymbol_EveryOutrightDatabentoListsUnderIt_IsInTheParentsArchiveGroup(string dataset, string parent, string member)
        {
            SymbologyRequest request = new SymbologyRequest
            {
                Dataset = dataset,
                Symbols = new[] { parent },
                StypeIn = SymbolTypes.Parent,
                StypeOut = SymbolTypes.InstrumentId,
                StartDate = Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = Day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };

            // a parent resolves to its children's instrument ids, keyed by each child's raw symbol
            SymbologyResolution resolution = await this.Reference.ResolveSymbolsAsync(request).ConfigureAwait(false);

            // calendar spreads such as MNQZ5-MNQH6 sit under the parent at Databento but are not contract months, so they keep their own group
            string[] outrights = resolution.Result.Keys.Where(symbol => !symbol.Contains('-', StringComparison.Ordinal)).ToArray();
            CollectionAssert.Contains(outrights, member);
            string[] elsewhere = outrights.Where(symbol => ArchiveGroup.Of(dataset, symbol) != parent).ToArray();
            Assert.AreEqual(0, elsewhere.Length, Invariant($"Outrights outside {parent}: {string.Join(", ", elsewhere.Take(5))}"));
        }

        [TestMethod]
        public async Task BatchJob_SavesTheSameDaysTheTimeseriesCallsDo()
        {
            string[] symbols = { "SPY", "QQQM" };
            DateTimeOffset start = new DateTimeOffset(2025, 9, 2, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset end = Day.AddDays(1);
            await LiveBudget.ChargeAsync(this.Reference, Datasets.XnasItch, "ohlcv-1d", symbols, start, end, times: 2).ConfigureAwait(false);
            CachedDatabentoClient batched = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out CountingTransport batchRequests, options =>
            {
                options.BatchDownloadFolder = this.NewFolder();
                options.BatchPollInterval = TimeSpan.FromSeconds(10);
            });
            CachedDatabentoClient direct = this.OpenClient(this.NewFolder(), DatabentoTransport.Json, out _);

            BatchReport report;
            using(CancellationTokenSource timeLimit = new CancellationTokenSource(TimeSpan.FromMinutes(15)))
            {
                report = await batched.RunBatchJobAsync(Datasets.XnasItch, "ohlcv-1d", symbols, start, end, maxCostUsd: 0.01m, timeLimit.Token).ConfigureAwait(false);
            }

            int sentAfterBatch = batchRequests.Total;
            batchRequests.ForbidDownloads();
            IOhlcvRecord[] fromBatch = await batched.GetOhlcv1dAsync(Datasets.XnasItch, symbols, start, end).ConfigureAwait(false);
            IOhlcvRecord[] fromTimeseries = await direct.GetOhlcv1dAsync(Datasets.XnasItch, symbols, start, end).ConfigureAwait(false);

            // a job from an earlier run on the account that covers the range is downloaded again for free instead of submitted
            Assert.AreEqual(1, report.JobIds.Count + report.ReusedJobIds.Count, "One job, new or reused, covers the four days.");
            Assert.AreEqual(8, report.SymbolDaysSaved + report.SymbolDaysSavedEmpty, "Four days of two symbols");
            Assert.AreEqual(sentAfterBatch, batchRequests.Total, "The batch job's days are served from the cache.");
            Assert.AreEqual(8, fromBatch.Length);
            CollectionAssert.AreEqual(AsText(fromTimeseries), AsText(fromBatch));
        }

        [TestMethod]
        public async Task DownloadedCache_ThroughCompactionBackupAndRestore_ServesTheSameRecordsWithoutDownloading()
        {
            DateTimeOffset end = Day.AddDays(1);
            await LiveBudget.ChargeAsync(this.Reference, Datasets.OpraPillar, "cbbo-1m", new[] { SpxwCall }, Day, end).ConfigureAwait(false);
            await LiveBudget.ChargeAsync(this.Reference, Datasets.XnasItch, "ohlcv-1h", new[] { "SPY" }, Day, end).ConfigureAwait(false);
            string root = this.NewFolder();
            CachedDatabentoClient client = this.OpenClient(root, DatabentoTransport.Json, out _);
            IRecord[] downloaded = (await client.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, Day, end).ConfigureAwait(false)).Cast<IRecord>()
                .Concat(await client.GetOhlcv1hAsync(Datasets.XnasItch, "SPY", Day, end).ConfigureAwait(false))
                .ToArray();

            ArchiveCompactionReport compaction = await new DatabentoArchiveCompactor(new ArchiveCompactorOptions { CacheRoot = root }).CompactAsync().ConfigureAwait(false);
            LocalDirectoryBackupStore store = new LocalDirectoryBackupStore(this.NewFolder());
            CacheBackupReport backup = await new DatabentoCacheBackup(new CacheBackupOptions
            {
                CacheRoot = root,
                Store = store,
                StagingFolder = this.NewFolder(),
            }).BackUpAsync().ConfigureAwait(false);
            string restoredRoot = this.NewFolder();
            CacheRestoreReport restore = await new DatabentoCacheRestore(new CacheRestoreOptions
            {
                CacheRoot = restoredRoot,
                Store = store,
                StagingFolder = this.NewFolder(),
            }).RestoreAsync(backup.Name).ConfigureAwait(false);

            CachedDatabentoClient restored = this.OpenClient(restoredRoot, DatabentoTransport.Json, out CountingTransport requests);
            requests.ForbidDownloads();
            IRecord[] served = (await restored.GetCbbo1mAsync(Datasets.OpraPillar, SpxwCall, Day, end).ConfigureAwait(false)).Cast<IRecord>()
                .Concat(await restored.GetOhlcv1hAsync(Datasets.XnasItch, "SPY", Day, end).ConfigureAwait(false))
                .ToArray();

            Assert.AreEqual(2, compaction.DayFilesAbsorbed, "Both day files go into the archive.");
            Assert.AreEqual(0, compaction.FailedGroups.Count);
            Assert.AreEqual(backup.FileCount, restore.FileCount);
            Assert.AreEqual(0, requests.Total, "The restored cache serves both days from its archive.");
            CollectionAssert.AreEqual(AsText(downloaded), AsText(served));
        }

        private static string DayFilePath(string root, string dataset, string schema, string symbol, DateTimeOffset day)
        {
            return DayFileName.GetPath(DayFileName.GetMonthDirectory(root, dataset, schema, day.Year, day.Month), symbol, day.UtcDateTime, CacheMetadata.UtcCutoff);
        }
    }
}
