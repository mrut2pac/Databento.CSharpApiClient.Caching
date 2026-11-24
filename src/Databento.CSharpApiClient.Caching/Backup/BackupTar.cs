using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// Writes and reads a backup tar: an uncompressed PAX tar of a cache root's files under their relative paths, with <c>manifest.json</c>
    /// (each file's path, size and SHA-256) as its last entry, since the hashes are taken from the bytes as they go into the tar.
    /// </summary>
    internal static class BackupTar
    {
        /// <summary>
        /// Name of the manifest's entry, the tar's last
        /// </summary>
        public const string ManifestEntryName = "manifest.json";

        /// <summary>
        /// Layout version of the manifest this build writes and reads
        /// </summary>
        public const int ManifestFormatVersion = 1;

        private const int BufferSize = 1024 * 1024;

        private static readonly JsonSerializerOptions ManifestJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        /// <summary>
        /// Names Windows gives to devices, which no file can have, with or without an extension
        /// </summary>
        private static readonly HashSet<string> DeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// Writes every file under the cache root into a new tar, then its manifest. Temporary files, the root's lock and a restore's
        /// marker are left out, and a file deleted between the listing and the copy is skipped.
        /// </summary>
        /// <param name="cacheRoot">The cache root to back up</param>
        /// <param name="tarPath">The tar to create; one already there is not overwritten</param>
        /// <param name="startUtc">When the backup started, recorded in the manifest</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The manifest written</returns>
        public static async Task<BackupManifest> WriteAsync(string cacheRoot, string tarPath, DateTime startUtc, CancellationToken cancellationToken)
        {
            BackupManifest manifest = new BackupManifest
            {
                FormatVersion = ManifestFormatVersion,
                RunStartUtc = startUtc,
            };

            FileStream output = new FileStream(tarPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, BufferSize, useAsync: true);
            await using(output.ConfigureAwait(false))
            {
                TarWriter writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: true);
                await using(writer.ConfigureAwait(false))
                {
                    foreach(string sourcePath in Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryName = Path.GetRelativePath(cacheRoot, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
                        if(IsLeftOut(sourcePath, entryName))
                        {
                            continue;
                        }

                        if(entryName == ManifestEntryName)
                        {
                            throw new IOException(Invariant($"{sourcePath} has the name of the backup's manifest, so the cache root cannot be backed up."));
                        }

                        BackupManifestFile file = await WriteEntryAsync(writer, sourcePath, entryName, cancellationToken).ConfigureAwait(false);
                        if(file != null)
                        {
                            manifest.Files.Add(file);
                            ++manifest.FileCount;
                            manifest.TotalBytes += file.Size;
                        }
                    }

                    using(MemoryStream manifestStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonOptions)))
                    {
                        PaxTarEntry manifestEntry = new PaxTarEntry(TarEntryType.RegularFile, ManifestEntryName)
                        {
                            DataStream = manifestStream,
                        };

                        await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
                    }
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            return manifest;
        }

        /// <summary>
        /// Reads a backup tar front to back and checks it holds exactly the files its manifest lists, each of the listed size and SHA-256,
        /// under paths that stay inside the cache root, and nothing else; the stream is read to its end.
        /// </summary>
        /// <param name="tar">The tar's bytes</param>
        /// <param name="extractFolder">The folder the files are written into under their relative paths, or <c>null</c> to only check them</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The tar's manifest</returns>
        /// <exception cref="InvalidDataException">The tar is damaged, or differs from its manifest</exception>
        public static async Task<BackupManifest> ReadAsync(Stream tar, string extractFolder, CancellationToken cancellationToken)
        {
            // paths match exactly, as a case-sensitive disk reads them; two differing only in case would collide on a case-insensitive one
            Dictionary<string, BackupManifestFile> found = new Dictionary<string, BackupManifestFile>(StringComparer.Ordinal);
            HashSet<string> foundIgnoringCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            BackupManifest manifest = null;
            try
            {
                TarReader reader = new TarReader(tar, leaveOpen: true);
                await using(reader.ConfigureAwait(false))
                {
                    TarEntry entry;
                    while((entry = await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false)) != null)
                    {
                        if(manifest != null)
                        {
                            throw new InvalidDataException(Invariant($"The backup holds {entry.Name} after its manifest."));
                        }

                        if(entry.EntryType != TarEntryType.RegularFile)
                        {
                            throw new InvalidDataException(Invariant($"The backup's entry {entry.Name} is a {entry.EntryType}, not a file."));
                        }

                        if(entry.Name == ManifestEntryName)
                        {
                            manifest = await ReadManifestAsync(entry, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        string relativePath = ToRelativePath(entry.Name);
                        if(!foundIgnoringCase.Add(entry.Name))
                        {
                            throw new InvalidDataException(Invariant($"The backup holds {entry.Name} twice."));
                        }

                        string extractPath = extractFolder == null ? null : ToExtractPath(extractFolder, relativePath);
                        found.Add(entry.Name, await ReadEntryAsync(entry, extractPath, cancellationToken).ConfigureAwait(false));
                    }
                }

                // the end-of-archive blocks, so a hash of the stream covers the whole tar
                await tar.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(ex is FormatException || ex is EndOfStreamException || ex is JsonException)
            {
                throw new InvalidDataException(Invariant($"The backup is damaged: {ex.Message}"), ex);
            }

            CheckAgainstManifest(manifest, found);
            return manifest;
        }

        /// <summary>
        /// Gets whether a cache root's file stays out of its backup: a temporary file still being written, the root's lock, or a restore's marker
        /// </summary>
        private static bool IsLeftOut(string sourcePath, string entryName)
        {
            return sourcePath.EndsWith(ArchiveFileName.TemporaryFileExtension, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entryName, CacheRootLock.FileName, StringComparison.Ordinal)
                || string.Equals(entryName, CacheRestoreMarker.FileName, StringComparison.Ordinal);
        }

        private static async Task<BackupManifestFile> WriteEntryAsync(TarWriter writer, string sourcePath, string entryName, CancellationToken cancellationToken)
        {
            FileStream source;
            try
            {
                source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, useAsync: true);
            }
            catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                // a day file replaced, or an archive file a run retired, between the listing and the copy
                return null;
            }

            HashingReadStream hashing = new HashingReadStream(source, length: source.Length);
            await using(hashing.ConfigureAwait(false))
            {
                PaxTarEntry entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = hashing,
                    ModificationTime = File.GetLastWriteTimeUtc(source.SafeFileHandle),
                };

                await writer.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
                return new BackupManifestFile
                {
                    Path = entryName,
                    Size = hashing.BytesRead,
                    Sha256 = hashing.GetHashHex(),
                };
            }
        }

        private static async Task<BackupManifest> ReadManifestAsync(TarEntry entry, CancellationToken cancellationToken)
        {
            if(entry.DataStream == null)
            {
                throw new InvalidDataException("The backup's manifest is empty.");
            }

            BackupManifest manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(entry.DataStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if(manifest?.Files == null)
            {
                throw new InvalidDataException("The backup's manifest lists no files.");
            }

            if(manifest.FormatVersion != ManifestFormatVersion)
            {
                throw new InvalidDataException(Invariant($"The backup's manifest is of format version {manifest.FormatVersion}; this build reads version {ManifestFormatVersion}."));
            }

            return manifest;
        }

        /// <summary>
        /// Hashes an entry's bytes, writing them to <paramref name="extractPath"/> unless it is <c>null</c>
        /// </summary>
        private static async Task<BackupManifestFile> ReadEntryAsync(TarEntry entry, string extractPath, CancellationToken cancellationToken)
        {
            // the entry's data belongs to the reader, which moves past it on the next entry
            HashingReadStream hashing = new HashingReadStream(entry.DataStream ?? Stream.Null, leaveOpen: true);
            await using(hashing.ConfigureAwait(false))
            {
                if(extractPath == null)
                {
                    await hashing.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(extractPath));
                    FileStream target = new FileStream(extractPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                    await using(target.ConfigureAwait(false))
                    {
                        await hashing.CopyToAsync(target, BufferSize, cancellationToken).ConfigureAwait(false);
                        target.Flush(flushToDisk: true);
                    }

                    File.SetLastWriteTimeUtc(extractPath, entry.ModificationTime.UtcDateTime);
                }

                return new BackupManifestFile
                {
                    Path = entry.Name,
                    Size = hashing.BytesRead,
                    Sha256 = hashing.GetHashHex(),
                };
            }
        }

        /// <summary>
        /// Turns an entry's name into a path below the cache root: <c>/</c>-separated names, none empty, <c>.</c>, <c>..</c> or holding a
        /// character a file name cannot, so no entry can be written outside the folder it is restored into
        /// </summary>
        private static string ToRelativePath(string entryName)
        {
            string[] names = entryName.Split('/');
            if(names.Any(name => name.Length == 0 || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(new[] { '\\', ':' }) >= 0
                || name.EndsWith('.') || name.EndsWith(' ') || DeviceNames.Contains(name.Split('.')[0])))
            {
                throw new InvalidDataException(Invariant($"The backup's entry {entryName} is not a path inside the cache root."));
            }

            if(names.Length == 1 && (string.Equals(names[0], CacheRootLock.FileName, StringComparison.OrdinalIgnoreCase) || string.Equals(names[0], CacheRestoreMarker.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(Invariant($"The backup holds {entryName}, a file only a running compaction, backup or restore writes."));
            }

            return Path.Combine(names);
        }

        /// <summary>
        /// Gets where an entry is extracted to, checked once more to be inside the folder
        /// </summary>
        private static string ToExtractPath(string extractFolder, string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(extractFolder, relativePath));
            if(!CacheFolders.IsInside(path, extractFolder))
            {
                throw new InvalidDataException(Invariant($"The backup's entry {relativePath} is not a path inside the cache root."));
            }

            return path;
        }

        private static void CheckAgainstManifest(BackupManifest manifest, Dictionary<string, BackupManifestFile> found)
        {
            if(manifest == null)
            {
                throw new InvalidDataException("The backup has no manifest.");
            }

            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach(BackupManifestFile file in manifest.Files)
            {
                if(!listed.Add(file.Path ?? string.Empty))
                {
                    throw new InvalidDataException(Invariant($"The backup's manifest lists {file.Path} twice."));
                }

                if(!found.TryGetValue(file.Path ?? string.Empty, out BackupManifestFile held))
                {
                    throw new InvalidDataException(Invariant($"The backup's manifest lists {file.Path}, which the backup does not hold."));
                }

                if(held.Size != file.Size || !string.Equals(held.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(Invariant($"The backup's {file.Path} differs from its manifest: {held.Size:N0} bytes, SHA-256 {held.Sha256}, listed as {file.Size:N0} bytes, {file.Sha256}."));
                }
            }

            string extra = found.Keys.FirstOrDefault(path => !listed.Contains(path));
            if(extra != null)
            {
                throw new InvalidDataException(Invariant($"The backup holds {extra}, which its manifest does not list."));
            }

            if(manifest.FileCount != manifest.Files.Count || manifest.TotalBytes != manifest.Files.Sum(file => file.Size))
            {
                throw new InvalidDataException(Invariant($"The backup's manifest counts {manifest.FileCount:N0} files, {manifest.TotalBytes:N0} bytes, but lists {manifest.Files.Count:N0}."));
            }
        }
    }
}
