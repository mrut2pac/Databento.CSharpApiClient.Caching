using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// A backup store over another that runs a test's code as a backup is put, and can fail puts and deletes
    /// </summary>
    internal sealed class FakeBackupStore : IBackupStore
    {
        private readonly IBackupStore inner;

        public FakeBackupStore(IBackupStore inner)
        {
            this.inner = inner;
        }

        /// <summary>
        /// Gets or sets what runs at the start of each put, while the backup holds the cache root's lock.
        /// </summary>
        public Action Putting { get; set; }

        /// <summary>
        /// Gets or sets whether each put fails with an <see cref="IOException"/>, leaving the store as it was.
        /// </summary>
        public bool FailPuts { get; set; }

        /// <summary>
        /// Gets or sets what each list and delete throws, or <c>null</c> for none.
        /// </summary>
        public Exception RetentionFailure { get; set; }

        /// <summary>
        /// Gets or sets whether a put copies the staged tar in and leaves it, as a remote store would.
        /// </summary>
        public bool CopyPuts { get; set; }

        /// <summary>
        /// Gets or sets names the store lists beside its backups, as a store that lists more than backups would.
        /// </summary>
        public IReadOnlyList<string> ExtraNames { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets what reading <see cref="Location"/> throws, or <c>null</c> for nothing.
        /// </summary>
        public Exception LocationFailure { get; set; }

        public string Location => this.LocationFailure == null ? this.inner.Location : throw this.LocationFailure;

        public Task PutAsync(string name, string tarPath, string sha256, CancellationToken cancellationToken = default)
        {
            this.Putting?.Invoke();
            if(this.FailPuts)
            {
                throw new IOException("The fake store refuses the put.");
            }

            if(this.CopyPuts)
            {
                string copy = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tar");
                File.Copy(tarPath, copy);
                return this.inner.PutAsync(name, copy, sha256, cancellationToken);
            }

            return this.inner.PutAsync(name, tarPath, sha256, cancellationToken);
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        {
            if(this.RetentionFailure != null)
            {
                throw this.RetentionFailure;
            }

            IReadOnlyList<string> names = this.ExtraNames.Concat(this.inner.ListAsync(cancellationToken).GetAwaiter().GetResult()).ToList();
            return Task.FromResult(names);
        }

        public Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default)
        {
            return this.inner.OpenReadAsync(name, cancellationToken);
        }

        public Task<string> GetSha256Async(string name, CancellationToken cancellationToken = default)
        {
            return this.inner.GetSha256Async(name, cancellationToken);
        }

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            if(this.RetentionFailure != null)
            {
                throw this.RetentionFailure;
            }

            return this.inner.DeleteAsync(name, cancellationToken);
        }
    }
}
