// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// Where the store reads and saves a symbol-day: its day file in the day folder, then a legacy day file in the month folder, then the
    /// archive. A month folder's archive listing taken earlier in a call, and a compaction that absorbs one of the call's day files before
    /// the call reaches it: the day is still found, since the folder changed under the miss.
    /// </summary>
    [TestClass]
    public sealed class SymbolDayStoreTests
    {
        private static readonly DateTime NovemberBuild = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public async Task ReadAsync_DayFileCompactedIntoADeltaAfterTheCallListedTheArchive_ServesTheDayFromTheDelta()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                string putDayFile = cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 2));
                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);
                Assert.IsNotNull(await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Call, Day, CancellationToken.None));

                // what a compaction does: the delta first, then the day file it absorbed goes
                cache.WriteCbboFile(NovemberBuild, 1, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2) }));
                File.Delete(putDayFile);
                ICbboRecord[] served = await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Put, Day, CancellationToken.None);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Put, Day, 2)), ReceivedTimes(served));
            }
        }

        [TestMethod]
        public async Task ReadAsync_DayUnpackedAfterTheCallMissedItsDayFile_ServesItsNewDayFile()
        {
            // what an unpack does: the day file first, then the archive it came from goes
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                string archive = cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2) }));
                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);

                // the read finds no day file; the unpack then writes it and retires the archive before the read lists the folder
                store.DayFileMissed = () =>
                {
                    store.DayFileMissed = null;
                    cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 2));
                    File.Delete(archive);
                };
                ICbboRecord[] served = await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Put, Day, CancellationToken.None);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Put, Day, 2)), ReceivedTimes(served));
            }
        }

        [TestMethod]
        public async Task ContainsAsync_DayFileCompactedIntoADeltaAfterTheCallListedTheArchive_FindsTheDay()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                string putDayFile = cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Put, Day, Cbbo(Put, Day, 2));
                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);
                Assert.IsTrue(await store.ContainsAsync(CachedSchemas.Cbbo1Min, folder, Call, Day));

                cache.WriteCbboFile(NovemberBuild, 1, new ArchiveSymbolDay<ICbboRecord>(Put, Day, new ICbboRecord[] { Cbbo(Put, Day, 2) }));
                File.Delete(putDayFile);

                Assert.IsTrue(await store.ContainsAsync(CachedSchemas.Cbbo1Min, folder, Put, Day, strict: true));
            }
        }

        [TestMethod]
        public async Task ReadAsync_DayNotCachedInAFolderUnchangedSinceItWasListed_ListsItOnce()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.WriteCbboFile(NovemberBuild, 0, new ArchiveSymbolDay<ICbboRecord>(Call, Day, new ICbboRecord[] { Cbbo(Call, Day, 1) }));
                Directory.SetLastWriteTimeUtc(cache.CbboDirectory, DateTime.UtcNow.AddHours(-1));
                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);

                Assert.IsNull(await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Put, Day, CancellationToken.None));
                Assert.IsNull(await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Put, Day.AddDays(1), CancellationToken.None));
                Assert.AreEqual(1, store.MonthFolderListings);
            }
        }

        [TestMethod]
        public async Task ReadAsync_MonthFolderCannotBeListed_Throws()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                // Windows: a file where the month folder should be fails its listing; elsewhere that reads as no folder, so the folder's
                // permissions are taken away instead, as for a folder this process is denied
                if(OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cache.CbboDirectory));
                    File.WriteAllText(cache.CbboDirectory, string.Empty);
                }
                else
                {
                    Directory.CreateDirectory(cache.CbboDirectory);
                    File.SetUnixFileMode(cache.CbboDirectory, UnixFileMode.None);
                }

                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);
                Exception thrown = null;
                try
                {
                    if(!OperatingSystem.IsWindows() && Directory.GetFiles(cache.CbboDirectory) != null)
                    {
                        Assert.Inconclusive("The process can list a folder with no permissions, as root can: the denied folder cannot be made here.");
                    }
                }
                catch(UnauthorizedAccessException)
                {
                    // the folder is denied, as the test needs
                }

                try
                {
                    await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Call, Day, CancellationToken.None);
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                {
                    thrown = ex;
                }
                finally
                {
                    if(!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(cache.CbboDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }
                }

                Assert.IsNotNull(thrown, "a month folder that cannot be listed fails the read rather than reading as not cached");
                Assert.IsNotInstanceOfType(thrown, typeof(DirectoryNotFoundException));
            }
        }

        [TestMethod]
        public async Task SaveAsync_Day_WritesADbnDayFileInItsDayFolder()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);

                Assert.IsTrue(await store.SaveAsync(DayFileFormats.Cbbo1Min, CacheFixture.Opra, Call, Day, ToDbn(Cbbo(Call, Day, 1)), false, CancellationToken.None));

                string path = Path.Combine(cache.CbboDirectory, "16", "SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst");
                CollectionAssert.AreEqual(new[] { path }, cache.DayFiles(cache.CbboDirectory));
                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1)), ReceivedTimes(await DayFile.ReadAsync(path, DayFileFormats.Cbbo1Min, CancellationToken.None)));
            }
        }

        [TestMethod]
        public async Task ReadAsync_LegacyDayFileOnly_ServesIt()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.SaveLegacyDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                CacheMonthFolder folder = store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day);

                ICbboRecord[] served = await store.ReadAsync(CachedSchemas.Cbbo1Min, folder, Call, Day, CancellationToken.None);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1)), ReceivedTimes(served));
                Assert.IsInstanceOfType<CbboRecordDbn>(served[0]);
                Assert.IsTrue(await store.ContainsAsync(CachedSchemas.Cbbo1Min, folder, Call, Day));
                Assert.AreEqual(0, store.MonthFolderListings, "opened by name, as a day file is");
            }
        }

        [TestMethod]
        public async Task ReadAsync_DayFileAndLegacyDayFile_ServesTheDayFile()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.SaveLegacyDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                cache.SaveDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 2), Cbbo(Call, Day, 3));

                ICbboRecord[] served = await store.ReadAsync(CachedSchemas.Cbbo1Min, store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day), Call, Day, CancellationToken.None);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 2), Cbbo(Call, Day, 3)), ReceivedTimes(served));
            }
        }

        [TestMethod]
        public async Task ReadAsync_UnreadableDayFileOverALegacyDayFile_DeletesItAndServesTheLegacyOne()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.SaveLegacyDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));
                string unreadable = cache.WriteUnreadableCbbo(Call, Day);

                ICbboRecord[] served = await store.ReadAsync(CachedSchemas.Cbbo1Min, store.GetMonth(CacheFixture.Opra, CacheFixture.Cbbo, Day), Call, Day, CancellationToken.None);

                CollectionAssert.AreEqual(ReceivedTimes(Cbbo(Call, Day, 1)), ReceivedTimes(served));
                Assert.IsFalse(File.Exists(unreadable));
            }
        }

        [TestMethod]
        public async Task SaveAsync_LegacyDayFileThereWithoutOverwrite_KeepsItAndWritesNothing()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                string legacy = cache.SaveLegacyDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                Assert.IsFalse(await store.SaveAsync(DayFileFormats.Cbbo1Min, CacheFixture.Opra, Call, Day, ToDbn(Cbbo(Call, Day, 2)), false, CancellationToken.None));

                CollectionAssert.AreEqual(new[] { legacy }, cache.DayFiles(cache.CbboDirectory));
            }
        }

        [TestMethod]
        public async Task SaveAsync_LegacyDayFileThereWithOverwrite_WritesTheDayFileAndDeletesTheLegacyOne()
        {
            // a bypass replacing a day of a version 1 cache
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, NullLogger.Instance);
                cache.SaveLegacyDayFile(CacheFixture.Opra, CacheFixture.Cbbo, Call, Day, Cbbo(Call, Day, 1));

                Assert.IsTrue(await store.SaveAsync(DayFileFormats.Cbbo1Min, CacheFixture.Opra, Call, Day, ToDbn(Cbbo(Call, Day, 2)), true, CancellationToken.None));

                string[] dayFiles = cache.DayFiles(cache.CbboDirectory);
                Assert.AreEqual(1, dayFiles.Length);
                StringAssert.EndsWith(dayFiles[0], DayFileName.Extension);
            }
        }

        [TestMethod]
        public async Task SaveAsync_RecordDbnCannotHold_SavesNothingAndLogsAWarning()
        {
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                FakeLogger logger = new FakeLogger();
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, logger);
                CbboRecordDbn record = ToDbn(Cbbo(Call, Day, 1))[0];
                record.Price = double.PositiveInfinity;

                Assert.IsFalse(await store.SaveAsync(DayFileFormats.Cbbo1Min, CacheFixture.Opra, Call, Day, new[] { record }, false, CancellationToken.None));

                Assert.AreEqual(0, Directory.Exists(cache.CbboDirectory) ? Directory.GetFiles(cache.CbboDirectory, "*", SearchOption.AllDirectories).Length : 0, "neither a day file nor its temporary file");
                StringAssert.Contains(logger.Find(LogLevel.Warning, "DBN cannot hold").Single().Message, "SPXW  261016C06600000 2026-10-16 cbbo-1m is served but not saved");
            }
        }

        [TestMethod]
        public async Task ReadAsync_LegacyDayFileOfAValueTheRecordInterfaceCannotGive_KeepsItAndReadsTheDayAsNotCached()
        {
            // whole, only not convertible to DBN records: kept, as the compactor keeps it, rather than deleted as unreadable
            using(CacheFixture cache = new CacheFixture())
            using(ArchiveCatalog catalog = new ArchiveCatalog(TimeProvider.System, (_, _) => { }, NullLogger.Instance))
            {
                FakeLogger logger = new FakeLogger();
                SymbolDayStore store = new SymbolDayStore(cache.Root, CacheMetadata.UtcCutoff, catalog, logger);
                TcbboRecordJson overflowing = new TcbboRecordJson
                {
                    Header = new RecordHeaderJson { RecordType = RType.Tcbbo, PublisherId = 30, InstrumentId = 1234567, TsEventUtc = Day.AddHours(14) },
                    TsReceivedUtc = Day.AddHours(14),
                    Price = 12.5,
                    Side = TradeAggressor.Buyer,
                    TsInDelta = (long)int.MaxValue + 1,
                    Levels = new[] { new CbboLevel1RecordJson() },
                    Symbol = Call,
                };
                string legacy = cache.SaveLegacyDayFile(CacheFixture.Opra, Schema.Tcbbo, Call, Day, overflowing);

                ITcbboRecord[] served = await store.ReadAsync(CachedSchemas.Tcbbo, store.GetMonth(CacheFixture.Opra, Schema.Tcbbo, Day), Call, Day, CancellationToken.None);

                Assert.IsNull(served);
                Assert.IsTrue(File.Exists(legacy));
                logger.AssertLogged(LogLevel.Warning, "cannot be read and is left as it is", "ts_in_delta");
            }
        }

        private static CbboRecordDbn[] ToDbn(params ICbboRecord[] records)
        {
            return records.Select(DayFileFormats.Cbbo1Min.RecordSchema.ToRecord).ToArray();
        }
    }
}
