using System;
using System.Collections.Generic;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a <see cref="DatabentoArchiveUnpacker"/> run did, or for a dry run would do
    /// </summary>
    public sealed class ArchiveUnpackReport
    {
        private readonly object updateLock = new object();

        private readonly List<ArchiveGroupFailure> failedGroups = new List<ArchiveGroupFailure>();

        private readonly List<string> filesLeft = new List<string>();

        internal ArchiveUnpackReport(bool dryRun, long? freeBytes)
        {
            this.DryRun = dryRun;
            this.FreeBytes = freeBytes;
        }

        /// <summary>
        /// Gets whether the run was a dry run, which changed nothing in the cache.
        /// </summary>
        public bool DryRun { get; }

        /// <summary>
        /// Gets whether the run was canceled; the group-months in flight keep their archive, with the day files written so far.
        /// </summary>
        public bool Canceled { get; private set; }

        /// <summary>
        /// Gets the free space of the cache root's drive when the run started, in bytes, or <c>null</c> when it cannot be read.
        /// </summary>
        public long? FreeBytes { get; }

        /// <summary>
        /// Gets the number of group-months whose every symbol-day is now a day file, and, unless a dry run, whose archive is retired.
        /// </summary>
        public int GroupsUnpacked { get; private set; }

        /// <summary>
        /// Gets the group-months that failed, each left with its archive in place.
        /// </summary>
        public IReadOnlyList<ArchiveGroupFailure> FailedGroups
        {
            get
            {
                lock(this.updateLock)
                {
                    return this.failedGroups.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the number of day files written, or for a dry run that would be written.
        /// </summary>
        public long DayFilesWritten { get; private set; }

        /// <summary>
        /// Gets the number of symbol-days whose day file or legacy day file was already there and kept, being what the cache serves.
        /// </summary>
        public long DayFilesKept { get; private set; }

        /// <summary>
        /// Gets the size of the day files written, or for a dry run that would be written, in bytes; a file takes up to a cluster more on disk.
        /// </summary>
        public long DayFileBytes { get; private set; }

        /// <summary>
        /// Gets the number of archive files retired, the full files and deltas of the versions unpacked.
        /// </summary>
        public long ArchiveFilesDeleted { get; private set; }

        /// <summary>
        /// Gets the archive files that could not be deleted, under the cache root; their days are day files, which are served first.
        /// </summary>
        public IReadOnlyList<string> FilesLeft
        {
            get
            {
                lock(this.updateLock)
                {
                    return this.filesLeft.ToArray();
                }
            }
        }

        internal void RunCanceled()
        {
            lock(this.updateLock)
            {
                this.Canceled = true;
            }
        }

        internal void GroupUnpacked()
        {
            lock(this.updateLock)
            {
                ++this.GroupsUnpacked;
            }
        }

        internal void GroupFailed(string group, Exception exception)
        {
            lock(this.updateLock)
            {
                this.failedGroups.Add(new ArchiveGroupFailure(group, exception));
            }
        }

        internal void DayFileWritten(long bytes)
        {
            lock(this.updateLock)
            {
                ++this.DayFilesWritten;
                this.DayFileBytes += bytes;
            }
        }

        internal void DayFileKept()
        {
            lock(this.updateLock)
            {
                ++this.DayFilesKept;
            }
        }

        internal void ArchiveFileDeleted()
        {
            lock(this.updateLock)
            {
                ++this.ArchiveFilesDeleted;
            }
        }

        internal void FileLeft(string relativePath)
        {
            lock(this.updateLock)
            {
                this.filesLeft.Add(relativePath);
            }
        }
    }
}
