using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Databento.CSharpApiClient.Caching.Backup;

namespace Databento.CSharpApiClient.Caching.UnitTests.Backup
{
    /// <summary>
    /// Builds backup tars by hand, so a test can give one a manifest that differs from what it holds
    /// </summary>
    internal static class BackupTarTestFiles
    {
        /// <summary>
        /// The start of the backups these tars stand for, which names them
        /// </summary>
        public static readonly DateTime StartUtc = new DateTime(2026, 11, 19, 2, 15, 30, DateTimeKind.Utc);

        /// <summary>
        /// The <c>metadata.json</c> a cache this package wrote holds
        /// </summary>
        public static readonly KeyValuePair<string, byte[]> Metadata = Entry("metadata.json", Encoding.UTF8.GetBytes("{\n  \"format_version\": 1,\n  \"day_cutoff\": \"UTC\"\n}"));

        /// <summary>
        /// Gets the SHA-256 of some bytes, lowercase hex
        /// </summary>
        public static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        /// <summary>
        /// Gets a manifest that lists exactly the given files
        /// </summary>
        public static BackupManifest CreateManifest(IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            BackupManifest manifest = new BackupManifest
            {
                FormatVersion = 1,
                RunStartUtc = StartUtc,
            };

            foreach(KeyValuePair<string, byte[]> file in files)
            {
                manifest.Files.Add(new BackupManifestFile
                {
                    Path = file.Key,
                    Size = file.Value.Length,
                    Sha256 = Sha256(file.Value),
                });
            }

            manifest.FileCount = manifest.Files.Count;
            manifest.TotalBytes = manifest.Files.Sum(file => file.Size);
            return manifest;
        }

        /// <summary>
        /// Writes a tar of the given entries, in order, then the manifest as its last entry unless it is <c>null</c>
        /// </summary>
        public static byte[] CreateTar(IEnumerable<KeyValuePair<string, byte[]>> entries, BackupManifest manifest)
        {
            using(MemoryStream tar = new MemoryStream())
            {
                using(TarWriter writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
                {
                    foreach(KeyValuePair<string, byte[]> entry in entries)
                    {
                        WriteEntry(writer, entry.Key, entry.Value);
                    }

                    if(manifest != null)
                    {
                        WriteEntry(writer, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest));
                    }
                }

                return tar.ToArray();
            }
        }

        /// <summary>
        /// Writes a tar of the given files with a manifest that lists exactly them
        /// </summary>
        public static byte[] CreateTar(IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            List<KeyValuePair<string, byte[]>> list = files.ToList();
            return CreateTar(list, CreateManifest(list));
        }

        /// <summary>
        /// Gets a tar entry of the given name and bytes
        /// </summary>
        public static KeyValuePair<string, byte[]> Entry(string path, params byte[] bytes)
        {
            return new KeyValuePair<string, byte[]>(path, bytes);
        }

        /// <summary>
        /// Gets a tar's entries, in order, with their bytes
        /// </summary>
        public static List<KeyValuePair<string, byte[]>> ReadEntries(string tarPath)
        {
            List<KeyValuePair<string, byte[]>> entries = new List<KeyValuePair<string, byte[]>>();
            using(TarReader reader = new TarReader(File.OpenRead(tarPath)))
            {
                TarEntry entry;
                while((entry = reader.GetNextEntry()) != null)
                {
                    using(MemoryStream bytes = new MemoryStream())
                    {
                        entry.DataStream?.CopyTo(bytes);
                        entries.Add(new KeyValuePair<string, byte[]>(entry.Name, bytes.ToArray()));
                    }
                }
            }

            return entries;
        }

        private static void WriteEntry(TarWriter writer, string name, byte[] bytes)
        {
            using(MemoryStream data = new MemoryStream(bytes))
            {
                PaxTarEntry entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = data,
                };

                writer.WriteEntry(entry);
            }
        }
    }
}
