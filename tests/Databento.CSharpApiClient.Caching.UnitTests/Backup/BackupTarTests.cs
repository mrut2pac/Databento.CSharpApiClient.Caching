using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Backup;
using Databento.CSharpApiClient.Caching.Cache;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Backup
{
    /// <summary>
    /// A cache root written into a tar and read back as it was, and every way a tar can differ from its manifest refused
    /// </summary>
    [TestClass]
    public class BackupTarTests
    {
        private string folder;

        private string CacheRoot => Path.Combine(this.folder, "Cache");

        private string TarPath => Path.Combine(this.folder, "20261119T021530.tar");

        private string ExtractFolder => Path.Combine(this.folder, "Extract");

        [TestInitialize]
        public void TestInitialize()
        {
            this.folder = Path.Combine(Path.GetTempPath(), "BackupTarTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.CacheRoot);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if(Directory.Exists(this.folder))
            {
                Directory.Delete(this.folder, recursive: true);
            }
        }

        [TestMethod]
        public async Task WriteThenRead_CacheRoot_IsExtractedAsItWasWithItsManifest()
        {
            this.WriteCacheFile("OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet", Enumerable.Range(0, 3 * 1024 * 1024).Select(i => (byte)i).ToArray());
            this.WriteCacheFile("OPRA.PILLAR/cbbo-1m/2026/10/ESZ6_2026-10-16.UTC.json.gz", 1, 2, 3);
            this.WriteCacheFile("DBIX.CORE/trades/2026/10/empty.json.gz");
            this.WriteCacheFile("OPRA.PILLAR/cbbo-1m/2026/10/being-written.json.gz.0123456789abcdef.tmp", 9);

            BackupManifest written = await BackupTar.WriteAsync(this.CacheRoot, this.TarPath, BackupTarTestFiles.StartUtc, CancellationToken.None);
            BackupManifest read = await Read(File.ReadAllBytes(this.TarPath), this.ExtractFolder);

            string[] expected = { "DBIX.CORE/trades/2026/10/empty.json.gz", "OPRA.PILLAR/cbbo-1m/2026/10/ESZ6_2026-10-16.UTC.json.gz", "OPRA.PILLAR/cbbo-1m/2026/10/SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet" };
            CollectionAssert.AreEqual(expected, written.Files.Select(file => file.Path).ToArray(), "the temporary file is left out");
            CollectionAssert.AreEqual(expected, read.Files.Select(file => file.Path).ToArray());
            Assert.AreEqual(3, read.FileCount);
            Assert.AreEqual((3L * 1024 * 1024) + 3, read.TotalBytes);
            Assert.AreEqual(BackupTarTestFiles.StartUtc, read.RunStartUtc);
            foreach(string path in expected)
            {
                string relativePath = Path.Combine(path.Split('/'));
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(this.CacheRoot, relativePath)), File.ReadAllBytes(Path.Combine(this.ExtractFolder, relativePath)), path);
            }

            Assert.AreEqual(expected.Length, Directory.GetFiles(this.ExtractFolder, "*", SearchOption.AllDirectories).Length);
        }

        [TestMethod]
        public async Task Write_RestoreMarkerAndLockInTheCacheRoot_AreLeftOut()
        {
            this.WriteCacheFile("metadata.json", 1);
            this.WriteCacheFile(CacheRestoreMarker.FileName, 2);
            this.WriteCacheFile(CacheRootLock.FileName, 3);

            BackupManifest written = await BackupTar.WriteAsync(this.CacheRoot, this.TarPath, BackupTarTestFiles.StartUtc, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "metadata.json" }, written.Files.Select(file => file.Path).ToArray());
        }

        [TestMethod]
        public async Task Write_TarAlreadyThere_Throws()
        {
            this.WriteCacheFile("metadata.json", 1);
            File.WriteAllBytes(this.TarPath, new byte[] { 1 });

            await Assert.ThrowsExceptionAsync<IOException>(() => BackupTar.WriteAsync(this.CacheRoot, this.TarPath, BackupTarTestFiles.StartUtc, CancellationToken.None));

            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(this.TarPath));
        }

        [TestMethod]
        public async Task Write_CacheFileNamedLikeTheManifest_Throws()
        {
            this.WriteCacheFile("manifest.json", 1);

            await Assert.ThrowsExceptionAsync<IOException>(() => BackupTar.WriteAsync(this.CacheRoot, this.TarPath, BackupTarTestFiles.StartUtc, CancellationToken.None));
        }

        [TestMethod]
        public async Task Read_FileDifferingFromTheManifest_Throws()
        {
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1, 2, 3);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1, 2, 4) });

            InvalidDataException exception = await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }, manifest), null));

            StringAssert.Contains(exception.Message, "OPRA.PILLAR/a.json.gz");
        }

        [TestMethod]
        public async Task Read_ManifestListingAFileTheTarLacks_Throws()
        {
            KeyValuePair<string, byte[]> held = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { held, BackupTarTestFiles.Entry("OPRA.PILLAR/b.json.gz", 2) });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { held }, manifest), null));
        }

        [TestMethod]
        public async Task Read_FileTheManifestDoesNotList_Throws()
        {
            KeyValuePair<string, byte[]> listed = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { listed });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { listed, BackupTarTestFiles.Entry("OPRA.PILLAR/b.json.gz", 2) }, manifest), null));
        }

        [TestMethod]
        public async Task Read_NoManifest_Throws()
        {
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1) }, null), null));
        }

        [TestMethod]
        public async Task Read_FileAfterTheManifest_Throws()
        {
            // the manifest lists the file, so only its place after the manifest is wrong
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(BackupTarTestFiles.CreateManifest(new[] { file }));
            byte[] tar = BackupTarTestFiles.CreateTar(new[] { BackupTarTestFiles.Entry("manifest.json", manifest), file }, null);

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(tar, null));
        }

        [TestMethod]
        public async Task Read_ManifestOfAnotherFormatVersion_Throws()
        {
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { file });
            manifest.FormatVersion = 2;

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }, manifest), null));
        }

        [TestMethod]
        public async Task Read_ManifestCountsDifferingFromItsList_Throws()
        {
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { file });
            manifest.TotalBytes = 2;

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }, manifest), null));
        }

        [TestMethod]
        [DataRow("../outside.json.gz", DisplayName = "parent folder")]
        [DataRow("OPRA.PILLAR/../../outside.json.gz", DisplayName = "parent folder inside")]
        [DataRow("/outside.json.gz", DisplayName = "rooted")]
        [DataRow("C:/outside.json.gz", DisplayName = "drive")]
        [DataRow("OPRA.PILLAR\\..\\..\\outside.json.gz", DisplayName = "backslashes")]
        [DataRow("OPRA.PILLAR//a.json.gz", DisplayName = "empty folder name")]
        [DataRow("OPRA.PILLAR/NUL", DisplayName = "device name")]
        [DataRow("CON.json.gz", DisplayName = "device name with an extension")]
        [DataRow("OPRA.PILLAR/a.json.gz.", DisplayName = "trailing dot")]
        [DataRow("OPRA.PILLAR/a.json.gz ", DisplayName = "trailing space")]
        public async Task Read_EntryNotInsideTheCacheRoot_ThrowsAndWritesNothing(string name)
        {
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry(name, 1);

            // refused by the name alone, before any path is built from it
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }), null));
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }), this.ExtractFolder));

            Assert.IsFalse(File.Exists(Path.Combine(this.folder, "outside.json.gz")));
            Assert.IsFalse(Directory.Exists(this.ExtractFolder) && Directory.GetFiles(this.ExtractFolder, "*", SearchOption.AllDirectories).Length > 0);
        }

        [TestMethod]
        public async Task Read_SamePathTwice_Throws()
        {
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file, BackupTarTestFiles.Entry("opra.pillar/A.json.gz", 1) }, BackupTarTestFiles.CreateManifest(new[] { file })), null));
        }

        [TestMethod]
        public async Task Read_ManifestPathDifferingOnlyInCase_Throws()
        {
            // a case-sensitive disk would hold the entry under one name and be asked for it under the other
            KeyValuePair<string, byte[]> file = BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", 1);
            BackupManifest manifest = BackupTarTestFiles.CreateManifest(new[] { BackupTarTestFiles.Entry("opra.pillar/a.json.gz", 1) });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { file }, manifest), null));
        }

        [TestMethod]
        [DataRow("cache.lock", DisplayName = "the lock")]
        [DataRow("RESTORE_IN_PROGRESS", DisplayName = "the marker")]
        [DataRow("restore_in_progress", DisplayName = "the marker in another case")]
        public async Task Read_EntryNamedLikeARunsFile_Throws(string name)
        {
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(BackupTarTestFiles.CreateTar(new[] { BackupTarTestFiles.Entry(name, 1) }), this.ExtractFolder));

            Assert.IsFalse(File.Exists(Path.Combine(this.ExtractFolder, name)));
        }

        [TestMethod]
        public async Task Read_TruncatedTar_Throws()
        {
            byte[] tar = BackupTarTestFiles.CreateTar(new[] { BackupTarTestFiles.Entry("OPRA.PILLAR/a.json.gz", Enumerable.Repeat((byte)7, 5000).ToArray()) });

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Read(tar[..2048], null));
        }

        private static async Task<BackupManifest> Read(byte[] tar, string extractFolder)
        {
            using(MemoryStream stream = new MemoryStream(tar))
            {
                return await BackupTar.ReadAsync(stream, extractFolder, CancellationToken.None);
            }
        }

        private void WriteCacheFile(string relativePath, params byte[] bytes)
        {
            string path = Path.Combine(this.CacheRoot, Path.Combine(relativePath.Split('/')));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
    }
}
