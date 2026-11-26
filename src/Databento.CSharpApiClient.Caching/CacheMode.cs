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
        /// Every symbol-day downloaded again and served as downloaded, the day file and the archive both skipped; each is a paid request. A
        /// cached settled day is replaced only when Databento's condition for it is <c>available</c>, whatever the record counts, else the
        /// cached file is kept. An empty answer for a cached day follows <see cref="CachedDatabentoClientOptions.BypassMissAction"/>.
        /// </summary>
        Bypass = 1,

        /// <summary>
        /// Each symbol-day from its day file, else from the archive, and nothing downloaded: when the cache lacks any symbol-day the call
        /// throws <see cref="NotCachedException"/> naming them, so it never costs anything.
        /// </summary>
        CacheOnly = 2,
    }
}
