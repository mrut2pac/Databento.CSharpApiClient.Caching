using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Backup;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Keeps the backups in a local folder as <c>&lt;name&gt;.tar</c> with its hash in <c>&lt;name&gt;.tar.sha256</c>. A backup comes in
    /// under a <c>.partial</c> name, which is never listed, and is renamed once whole, so the folder (which a sync tool may watch) only ever
    /// lists a finished tar beside its hash. The staging folder may be on another volume, where the tar is copied in rather than moved.
    /// </summary>
    public sealed class LocalDirectoryBackupStore : IBackupStore
    {
        private const string TarExtension = ".tar";

        private const string HashExtension = ".sha256";

        private const string PartialExtension = ".partial";

        private readonly string folder;

        /// <summary>
        /// Creates a store over a folder, which is created when the first backup is put
        /// </summary>
        /// <param name="folder">The folder the backups are kept in</param>
        /// <exception cref="ArgumentException"><paramref name="folder"/> is <c>null</c> or empty</exception>
        public LocalDirectoryBackupStore(string folder)
        {
            ArgumentException.ThrowIfNullOrEmpty(folder);
            this.folder = folder;
        }

        /// <summary>
        /// Gets the folder the backups are kept in.
        /// </summary>
        public string Location => this.folder;

        /// <inheritdoc/>
        public async Task PutAsync(string name, string tarPath, string sha256, CancellationToken cancellationToken = default)
        {
            ThrowIfNotBackupName(name);
            ArgumentException.ThrowIfNullOrEmpty(tarPath);
            ArgumentException.ThrowIfNullOrEmpty(sha256);
            string tarTarget = this.GetTarPath(name);
            string hashTarget = tarTarget + HashExtension;
            if(File.Exists(tarTarget) || File.Exists(hashTarget))
            {
                throw new IOException(Invariant($"{this.folder} already holds a backup named {name}."));
            }

            Directory.CreateDirectory(this.folder);
            string partial = tarTarget + PartialExtension;
            bool hashWritten = false;
            try
            {
                // a partial file a stopped put left is this name's, so it is replaced
                bool sameVolume = string.Equals(Path.GetPathRoot(Path.GetFullPath(tarPath)), Path.GetPathRoot(Path.GetFullPath(this.folder)), StringComparison.OrdinalIgnoreCase);
                File.Move(tarPath, partial, overwrite: true);
                if(!sameVolume)
                {
                    // the move was a copy: it is read back and flushed before older backups may be deleted on the strength of it
                    await ThrowIfCopyDiffersAsync(partial, sha256, cancellationToken).ConfigureAwait(false);
                }

                hashWritten = true;
                await WriteHashAsync(hashTarget, sha256, cancellationToken).ConfigureAwait(false);
                File.Move(partial, tarTarget);
            }
            catch
            {
                TryDelete(partial);
                if(hashWritten)
                {
                    TryDelete(hashTarget);
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> names = !Directory.Exists(this.folder) ? Array.Empty<string>()
                : Directory.EnumerateFiles(this.folder, "*" + TarExtension)
                    .Where(path => path.EndsWith(TarExtension, StringComparison.Ordinal))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(BackupName.IsValid)
                    .Order(StringComparer.Ordinal)
                    .ToList();

            return Task.FromResult(names);
        }

        /// <inheritdoc/>
        public Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfNotBackupName(name);
            Stream stream = new FileStream(this.GetTarPath(name), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(stream);
        }

        /// <inheritdoc/>
        public async Task<string> GetSha256Async(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfNotBackupName(name);
            return (await File.ReadAllTextAsync(this.GetTarPath(name) + HashExtension, Encoding.ASCII, cancellationToken).ConfigureAwait(false)).Trim();
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfNotBackupName(name);

            // the tar first: a hash left without it is not listed
            string tarPath = this.GetTarPath(name);
            File.Delete(tarPath);
            File.Delete(tarPath + HashExtension);
            return Task.CompletedTask;
        }

        private static void ThrowIfNotBackupName(string name)
        {
            if(!BackupName.IsValid(name))
            {
                throw new ArgumentException(Invariant($"{name} is not a backup's name (yyyyMMddTHHmmss, UTC)."), nameof(name));
            }
        }

        private static async Task ThrowIfCopyDiffersAsync(string path, string sha256, CancellationToken cancellationToken)
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using(file.ConfigureAwait(false))
            {
                string copied;
                using(HashingReadStream hashing = new HashingReadStream(file, leaveOpen: true))
                {
                    await hashing.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
                    copied = hashing.GetHashHex();
                }

                if(!string.Equals(copied, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(Invariant($"The copy of the backup in {path} has SHA-256 {copied}, not {sha256}."));
                }

                file.Flush(flushToDisk: true);
            }
        }

        private static async Task WriteHashAsync(string path, string sha256, CancellationToken cancellationToken)
        {
            FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await using(file.ConfigureAwait(false))
            {
                await file.WriteAsync(Encoding.ASCII.GetBytes(sha256), cancellationToken).ConfigureAwait(false);
                file.Flush(flushToDisk: true);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // the put's own failure is the one reported; a partial file is never listed, and the next put of the name replaces it
            }
        }

        private string GetTarPath(string name)
        {
            return Path.Combine(this.folder, name + TarExtension);
        }
    }
}
