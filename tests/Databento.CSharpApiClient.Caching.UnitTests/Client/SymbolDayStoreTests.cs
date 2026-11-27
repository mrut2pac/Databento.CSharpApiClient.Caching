// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.DataModel;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A month folder's archive listing taken earlier in a call, and a compaction that absorbs one of the call's day files before the call
    /// reaches it: the day is still found, since the folder changed under the miss.
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
    }
}
