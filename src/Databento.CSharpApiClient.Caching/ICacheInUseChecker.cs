namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Tells a <see cref="DatabentoCacheRestore"/> whether something is using the cache it is about to restore, such as a service holding a
    /// <see cref="CachedDatabentoClient"/> on it. The restore asks before it writes its marker and again after, and refuses while the cache is in use.
    /// </summary>
    public interface ICacheInUseChecker
    {
        /// <summary>
        /// Gets whether something is using the cache now. An exception it throws stops the restore before anything in the cache changes.
        /// </summary>
        /// <returns><c>true</c> when the cache is in use and must not be restored</returns>
        bool IsInUse();
    }
}
