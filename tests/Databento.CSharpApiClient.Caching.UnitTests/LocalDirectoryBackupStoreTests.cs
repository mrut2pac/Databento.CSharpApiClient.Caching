using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// The local store: a backup put whole beside its hash, listed oldest first, read back and deleted, and nothing else of its folder touched
    /// </summary>
    [TestClass]
    public class LocalDirectoryBackupStoreTests
    {
        private const string Name = "20261119T021530";

        private string folder;

        private string StoreFolder => Path.Combine(this.folder, "Backups");

        private string StagingFolder => Path.Combine(this.folder, "Staging");

        [TestInitialize]
        public void TestInitialize()
        {
            this.folder = Path.Combine(Path.GetTempPath(), "LocalDirectoryBackupStoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.StagingFolder);
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
        public async Task Put_FinishedTar_IsMovedInBesideItsHashAndReadsBack()
        {
            LocalDirectoryBackupStore store = this.CreateStore();
            string tarPath = this.WriteStagedTar(1, 2, 3);

            await store.PutAsync(Name, tarPath, "abc123");

            Assert.IsFalse(File.Exists(tarPath), "moved, not copied");
            CollectionAssert.AreEquivalent(new[] { Name + ".tar", Name + ".tar.sha256" }, Directory.GetFiles(this.StoreFolder).Select(Path.GetFileName).ToArray(), "no partial file left");
            CollectionAssert.AreEqual(new[] { Name }, (await store.ListAsync()).ToArray());
            Assert.AreEqual("abc123", await store.GetSha256Async(Name));
            Assert.AreEqual("abc123  " + Name + ".tar\n", File.ReadAllText(Path.Combine(this.StoreFolder, Name + ".tar.sha256")), "as sha256sum writes it");
            using(Stream tar = await store.OpenReadAsync(Name))
            using(MemoryStream bytes = new MemoryStream())
            {
                await tar.CopyToAsync(bytes);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, bytes.ToArray());
            }

            Assert.AreEqual(this.StoreFolder, store.Location);
        }

        [TestMethod]
        public async Task Put_NameAlreadyInTheStore_ThrowsAndLeavesTheOldOne()
        {
            LocalDirectoryBackupStore store = this.CreateStore();
            await store.PutAsync(Name, this.WriteStagedTar(1), "old");

            await Assert.ThrowsExceptionAsync<IOException>(() => store.PutAsync(Name, this.WriteStagedTar(2), "new"));

            Assert.AreEqual("old", await store.GetSha256Async(Name));
        }

        [TestMethod]
        public async Task Put_TarThatCannotBeMoved_LeavesNothingInTheStore()
        {
            LocalDirectoryBackupStore store = this.CreateStore();

            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => store.PutAsync(Name, Path.Combine(this.StagingFolder, "missing.tar"), "abc"));

            Assert.AreEqual(0, Directory.GetFiles(this.StoreFolder).Length);
        }

        [TestMethod]
        public async Task Put_PartialFileLeftByAStoppedPut_IsReplacedAndNeverListed()
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, Name + ".tar.partial"), new byte[] { 9 });
            LocalDirectoryBackupStore store = this.CreateStore();
            Assert.AreEqual(0, (await store.ListAsync()).Count, "a partial file is no backup");

            await store.PutAsync(Name, this.WriteStagedTar(1), "abc");

            CollectionAssert.AreEquivalent(new[] { Name + ".tar", Name + ".tar.sha256" }, Directory.GetFiles(this.StoreFolder).Select(Path.GetFileName).ToArray());
        }

        [TestMethod]
        [DataRow("abc123", DisplayName = "the hash alone")]
        [DataRow("abc123  20261119T021530.tar\n", DisplayName = "as sha256sum writes it")]
        [DataRow("abc123 *20261119T021530.tar\r\n", DisplayName = "sha256sum's binary mode, CRLF")]
        public async Task GetSha256_HashFileInEitherFormat_GivesTheHash(string content)
        {
            Directory.CreateDirectory(this.StoreFolder);
            File.WriteAllBytes(Path.Combine(this.StoreFolder, Name + ".tar"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(this.StoreFolder, Name + ".tar.sha256"), content);

            Assert.AreEqual("abc123", await this.CreateStore().GetSha256Async(Name));
        }

        [TestMethod]
        public async Task List_FolderHoldingOtherFiles_ListsOnlyBackupsOldestFirst()
        {
            Directory.CreateDirectory(this.StoreFolder);
            foreach(string file in new[] { Name + ".tar", "20261101T000000.tar", "notes.tar", "20261102T000000.tar.sha256", "20261103T000000.TAR", "20261132T000000.tar" })
            {
                File.WriteAllBytes(Path.Combine(this.StoreFolder, file), new byte[] { 1 });
            }

            CollectionAssert.AreEqual(new[] { "20261101T000000", Name }, (await this.CreateStore().ListAsync()).ToArray());
        }

        [TestMethod]
        public async Task List_NoFolder_ListsNone()
        {
            Assert.AreEqual(0, (await this.CreateStore().ListAsync()).Count);
        }

        [TestMethod]
        public async Task Delete_Backup_RemovesTheTarAndItsHashAndNothingElse()
        {
            LocalDirectoryBackupStore store = this.CreateStore();
            await store.PutAsync(Name, this.WriteStagedTar(1), "abc");
            File.WriteAllBytes(Path.Combine(this.StoreFolder, "keep.txt"), new byte[] { 1 });

            await store.DeleteAsync(Name);

            CollectionAssert.AreEqual(new[] { "keep.txt" }, Directory.GetFiles(this.StoreFolder).Select(Path.GetFileName).ToArray());
        }

        [TestMethod]
        [DataRow("notes", DisplayName = "not a time")]
        [DataRow("../20261119T021530", DisplayName = "a path")]
        [DataRow("20261119T02153", DisplayName = "too short")]
        [DataRow(null, DisplayName = "null")]
        public async Task Calls_NotABackupsName_Throw(string name)
        {
            LocalDirectoryBackupStore store = this.CreateStore();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.DeleteAsync(name));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.OpenReadAsync(name));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.GetSha256Async(name));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.PutAsync(name, this.WriteStagedTar(1), "abc"));
        }

        [TestMethod]
        public void Constructor_NoFolder_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new LocalDirectoryBackupStore(string.Empty));
        }

        private LocalDirectoryBackupStore CreateStore()
        {
            return new LocalDirectoryBackupStore(this.StoreFolder);
        }

        private string WriteStagedTar(params byte[] bytes)
        {
            string path = Path.Combine(this.StagingFolder, Guid.NewGuid().ToString("N") + ".tar");
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
