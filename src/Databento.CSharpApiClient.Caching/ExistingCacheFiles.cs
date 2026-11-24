namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a <see cref="DatabentoCacheRestore"/> does with the files the cache root holds before the backup's files are moved in. The
    /// root's lock and the restore's marker are never touched.
    /// </summary>
    public enum ExistingCacheFiles
    {
        /// <summary>
        /// The cache's archive and temporary files are deleted and its day files kept, since a day file is served ahead of the archive
        /// and may be a download made after the backup; <c>metadata.json</c> is replaced and any other file left. Nothing paid for since
        /// the backup is lost, but a bad day file stays.
        /// </summary>
        Keep,

        /// <summary>
        /// Everything in the cache root is moved into the sibling folder <c>&lt;root&gt;.bak_&lt;yyyyMMddTHHmmss&gt;</c>, named by the
        /// restore's start, and the backup restored into a clean root: for a cache whose files cannot be trusted, such as ones a faulty
        /// release wrote. The folder is left for the caller to delete.
        /// </summary>
        MoveAside,

        /// <summary>
        /// Everything in the cache root is deleted and the backup restored into a clean root. Every day file downloaded since the backup is
        /// lost, and is paid for again when it is next asked for.
        /// </summary>
        Delete,
    }
}
