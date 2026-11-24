using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Keeps the backups of a cache, one tar each with its SHA-256, under the backup's name (its start, UTC, <c>yyyyMMddTHHmmss</c>). A backup
    /// is in the store whole or not at all: a reader never sees one half put or half deleted.
    /// </summary>
    public interface IBackupStore
    {
        /// <summary>
        /// Gets where the store keeps its backups, for messages and reports.
        /// </summary>
        string Location { get; }

        /// <summary>
        /// Puts a finished backup into the store; on return it is there whole, and on a failure nothing of it is.
        /// </summary>
        /// <param name="name">The backup's name; one the store already holds is refused</param>
        /// <param name="tarPath">The finished, checked tar in the staging folder; the store may move it, so it may be gone from there on return</param>
        /// <param name="sha256">The tar's SHA-256, lowercase hex</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task PutAsync(string name, string tarPath, string sha256, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists the names of the backups in the store, oldest first; anything else the store holds is not listed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The backups' names</returns>
        Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a backup's tar for reading, front to back.
        /// </summary>
        /// <param name="name">The backup's name, one <see cref="ListAsync"/> lists</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The tar's bytes; the caller disposes of the stream</returns>
        Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the SHA-256 stored with a backup's tar when it was put, lowercase hex.
        /// </summary>
        /// <param name="name">The backup's name, one <see cref="ListAsync"/> lists</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The tar's SHA-256</returns>
        Task<string> GetSha256Async(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a backup. Once it starts, the backup is no longer listed, even if the delete fails part way.
        /// </summary>
        /// <param name="name">The backup's name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    }
}
