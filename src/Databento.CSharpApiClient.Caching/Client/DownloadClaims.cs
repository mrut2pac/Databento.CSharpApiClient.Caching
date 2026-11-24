using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Lets one request at a time download a symbol-day within the process: a request finding a day claimed waits for the claim to end,
    /// then reads the day from the cache
    /// </summary>
    internal sealed class DownloadClaims
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource> claims = new ConcurrentDictionary<string, TaskCompletionSource>(StringComparer.Ordinal);

        /// <summary>
        /// Claims a key
        /// </summary>
        /// <param name="key">Key of the symbol-day</param>
        /// <param name="released">When another request holds the key, a task that completes once it releases it</param>
        /// <returns><c>true</c> when the key is now this caller's, to <see cref="Release"/> once done</returns>
        public bool TryClaim(string key, out Task released)
        {
            TaskCompletionSource claim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if(this.claims.TryAdd(key, claim))
            {
                released = null;
                return true;
            }

            released = this.claims.TryGetValue(key, out TaskCompletionSource held) ? held.Task : Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// Releases a key this caller claimed
        /// </summary>
        public void Release(string key)
        {
            if(this.claims.TryRemove(key, out TaskCompletionSource claim))
            {
                claim.TrySetResult();
            }
        }
    }
}
