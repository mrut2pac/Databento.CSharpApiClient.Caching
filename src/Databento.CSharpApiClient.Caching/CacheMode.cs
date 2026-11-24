namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Which caches a request of <see cref="CachedDatabentoClient"/> reads
    /// </summary>
    public enum CacheMode
    {
        /// <summary>
        /// Each symbol-day from its day file, else from the archive, else downloaded and saved.
        /// </summary>
        // stays 0: a call given the default literal where a cache mode or a token may go takes it as the cache mode
        Use = 0,

        /// <summary>
        /// Every symbol-day downloaded again, the day file and the archive both skipped, and the day file replaced. Each such symbol-day is
        /// a paid request; an empty answer for a cached day follows <see cref="CachedDatabentoClientOptions.BypassMissAction"/>.
        /// </summary>
        Bypass = 1,
    }
}
