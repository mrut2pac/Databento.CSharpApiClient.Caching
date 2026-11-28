using System;
using System.IO;
using System.Text.Json;

using Databento.CSharpApiClient.Caching.Cache;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Cache
{
    [TestClass]
    public class CacheMetadataTests
    {
        private string directory;

        private string Root => Path.Combine(this.directory, "Cache");

        private string MetadataPath => Path.Combine(this.Root, "metadata.json");

        [TestInitialize]
        public void TestInitialize()
        {
            this.directory = Path.Combine(Path.GetTempPath(), "CacheMetadataTests_" + Guid.NewGuid().ToString("N"));
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
        public void Read_RootWithoutMetadata_ReturnsNull()
        {
            Assert.IsNull(CacheMetadata.Read(this.Root), "a missing root");

            Directory.CreateDirectory(this.Root);
            Assert.IsNull(CacheMetadata.Read(this.Root), "an empty root");
        }

        [TestMethod]
        public void EnsureCreated_MissingRoot_CreatesItWithTheMetadataOfThisBuild()
        {
            CacheMetadata metadata = CacheMetadata.EnsureCreated(this.Root);

            Assert.AreEqual(2, metadata.FormatVersion);
            Assert.AreEqual("UTC", metadata.DayCutoff);
            CollectionAssert.AreEqual(new[] { this.MetadataPath }, Directory.GetFiles(this.Root), "no temporary file is left");
            using(JsonDocument document = JsonDocument.Parse(File.ReadAllText(this.MetadataPath)))
            {
                Assert.AreEqual(2, document.RootElement.GetProperty("format_version").GetInt32());
                Assert.AreEqual("UTC", document.RootElement.GetProperty("day_cutoff").GetString());
            }
        }

        [TestMethod]
        public void Read_MetadataEnsureCreatedWrote_IsRead()
        {
            CacheMetadata.EnsureCreated(this.Root);

            CacheMetadata metadata = CacheMetadata.Read(this.Root);

            Assert.AreEqual(2, metadata.FormatVersion);
            Assert.AreEqual("UTC", metadata.DayCutoff);
        }

        [TestMethod]
        public void EnsureCreated_RootWithMetadata_LeavesItAsItIs()
        {
            Directory.CreateDirectory(this.Root);
            const string Json = "{ \"day_cutoff\": \"UTC\", \"format_version\": 2, \"written_by\": \"another build\" }";
            File.WriteAllText(this.MetadataPath, Json);

            CacheMetadata metadata = CacheMetadata.EnsureCreated(this.Root);

            Assert.AreEqual("UTC", metadata.DayCutoff);
            Assert.AreEqual(Json, File.ReadAllText(this.MetadataPath));
        }

        [TestMethod]
        [DataRow("{\"format_version\":3,\"day_cutoff\":\"UTC\"}", DisplayName = "another format version")]
        [DataRow("{\"format_version\":1,\"day_cutoff\":\"ET\"}", DisplayName = "another cutoff")]
        [DataRow("{\"format_version\":1,\"day_cutoff\":\"utc\"}", DisplayName = "the cutoff in another case")]
        [DataRow("{\"format_version\":1}", DisplayName = "no cutoff")]
        [DataRow("{\"day_cutoff\":\"UTC\"}", DisplayName = "no format version")]
        [DataRow("{\"format_version\":\"1\",\"day_cutoff\":\"UTC\"}", DisplayName = "a format version that is no number")]
        [DataRow("{\"format_version\":1.5,\"day_cutoff\":\"UTC\"}", DisplayName = "a format version that is no integer")]
        [DataRow("[]", DisplayName = "no object")]
        [DataRow("not json", DisplayName = "not JSON")]
        [DataRow("", DisplayName = "empty")]
        public void ReadAndEnsureCreated_MetadataThisBuildDoesNotAccept_Throw(string json)
        {
            Directory.CreateDirectory(this.Root);
            File.WriteAllText(this.MetadataPath, json);

            Assert.ThrowsException<InvalidDataException>(() => CacheMetadata.Read(this.Root));
            Assert.ThrowsException<InvalidDataException>(() => CacheMetadata.EnsureCreated(this.Root));
            Assert.AreEqual(json, File.ReadAllText(this.MetadataPath), "a file this build does not accept is never replaced");
        }

        [TestMethod]
        public void ReadAndEnsureCreated_Version1Cache_MarkItVersion2AndReadIt()
        {
            // a cache of JSON day files only, which this build reads as they are; an older build refuses it once it is marked
            foreach(Func<string, CacheMetadata> open in new Func<string, CacheMetadata>[] { root => CacheMetadata.Read(root), CacheMetadata.EnsureCreated })
            {
                Directory.CreateDirectory(this.Root);
                File.WriteAllText(this.MetadataPath, "{\"format_version\":1,\"day_cutoff\":\"UTC\"}");

                CacheMetadata metadata = open(this.Root);

                Assert.AreEqual(2, metadata.FormatVersion);
                Assert.AreEqual("UTC", metadata.DayCutoff);
                using(JsonDocument document = JsonDocument.Parse(File.ReadAllText(this.MetadataPath)))
                {
                    Assert.AreEqual(2, document.RootElement.GetProperty("format_version").GetInt32());
                    Assert.AreEqual("UTC", document.RootElement.GetProperty("day_cutoff").GetString());
                }

                CollectionAssert.AreEqual(new[] { this.MetadataPath }, Directory.GetFiles(this.Root), "no temporary file is left");
            }
        }

        [TestMethod]
        public void Read_Version1CacheThatCannotBeMarked_IsReadAsVersion2()
        {
            // a read-only copy, such as a restored backup used as it is: nothing can be saved into it either
            if(!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("A read-only file blocks its replacement on Windows only.");
            }

            Directory.CreateDirectory(this.Root);
            const string Json = "{\"format_version\":1,\"day_cutoff\":\"UTC\"}";
            File.WriteAllText(this.MetadataPath, Json);
            File.SetAttributes(this.MetadataPath, FileAttributes.ReadOnly);
            try
            {
                CacheMetadata metadata = CacheMetadata.Read(this.Root);

                Assert.AreEqual(2, metadata.FormatVersion);
                Assert.AreEqual(Json, File.ReadAllText(this.MetadataPath));
                CollectionAssert.AreEqual(new[] { this.MetadataPath }, Directory.GetFiles(this.Root), "no temporary file is left");
            }
            finally
            {
                File.SetAttributes(this.MetadataPath, FileAttributes.Normal);
            }
        }

        [TestMethod]
        public void Read_Version1CacheNotToBeMarked_IsReadAsVersion2AndLeftAsItIs()
        {
            // a dry run or a backup, which change nothing in the cache
            Directory.CreateDirectory(this.Root);
            const string Json = "{\"format_version\":1,\"day_cutoff\":\"UTC\"}";
            File.WriteAllText(this.MetadataPath, Json);

            CacheMetadata metadata = CacheMetadata.Read(this.Root, mark: false);

            Assert.AreEqual(2, metadata.FormatVersion);
            Assert.AreEqual(Json, File.ReadAllText(this.MetadataPath));
        }

        [TestMethod]
        public void EnsureCreated_Version1CacheThatCannotBeMarked_ThrowsAndLeavesItAsItIs()
        {
            // nothing is saved into a cache an older build would still open: it would take the DBN day files for missing days
            if(!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("A read-only file blocks its replacement on Windows only.");
            }

            Directory.CreateDirectory(this.Root);
            const string Json = "{\"format_version\":1,\"day_cutoff\":\"UTC\"}";
            File.WriteAllText(this.MetadataPath, Json);
            File.SetAttributes(this.MetadataPath, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsException<IOException>(() => CacheMetadata.EnsureCreated(this.Root));

                Assert.AreEqual(Json, File.ReadAllText(this.MetadataPath));
                CollectionAssert.AreEqual(new[] { this.MetadataPath }, Directory.GetFiles(this.Root), "no temporary file is left");
            }
            finally
            {
                File.SetAttributes(this.MetadataPath, FileAttributes.Normal);
            }
        }

        [TestMethod]
        public void Read_NoRoot_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => CacheMetadata.Read(string.Empty));
            Assert.ThrowsException<ArgumentNullException>(() => CacheMetadata.Read(null));
        }
    }
}
