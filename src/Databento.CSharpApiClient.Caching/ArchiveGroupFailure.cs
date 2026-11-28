using System;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// A group-month a compaction run could not compact, or a month folder it could not list, or a group-month an unpack run could not
    /// unpack. Its folder was left as it was, or with the new file published and what it replaces not yet retired, or with its archive and
    /// the day files written so far; the next run tries again.
    /// </summary>
    public sealed class ArchiveGroupFailure
    {
        internal ArchiveGroupFailure(string group, Exception exception)
        {
            this.Group = group;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets what failed: the month folder under the cache root and the group, e.g. <c>OPRA.PILLAR\cbbo-1m\2026\10 SPXW.OPT</c>.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Gets why it failed.
        /// </summary>
        public Exception Exception { get; }
    }
}
