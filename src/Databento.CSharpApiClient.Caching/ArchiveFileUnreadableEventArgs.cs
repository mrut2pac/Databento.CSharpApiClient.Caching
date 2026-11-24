using System;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// An archive file <see cref="CachedDatabentoClient"/> cannot read. Without its full file a group-month is served as not cached, so its
    /// days are downloaded again; without its delta, the days only the delta holds are, and the rest come from the full file. The file is
    /// left in place: restore the cache from a backup, or remove the file.
    /// </summary>
    public sealed class ArchiveFileUnreadableEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes the event's data
        /// </summary>
        /// <param name="path">Full path of the file</param>
        /// <param name="exception">What reading it threw</param>
        public ArchiveFileUnreadableEventArgs(string path, Exception exception)
        {
            this.Path = path;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets what reading the file threw.
        /// </summary>
        public Exception Exception { get; }
    }
}
