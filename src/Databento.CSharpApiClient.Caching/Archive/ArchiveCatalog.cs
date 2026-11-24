using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Archive
{
    /// <summary>
    /// Answers symbol-days from the current version of their group and month: its newest full file overlaid by the highest generation of
    /// that file's delta, the delta winning a day both list. Recent versions' indexes are kept in memory, since a published file never
    /// changes; a file that cannot be read is reported once a day and left out, never touched.
    /// </summary>
    internal sealed class ArchiveCatalog
    {
        /// <summary>
        /// How many group-months' indexes are kept; the least recently used is dropped beyond it, since an options month's index is large
        /// </summary>
        private const int MaxCachedVersions = 32;

        /// <summary>
        /// How long an unreadable file is left out without reading it again, so a run asking for its days does not parse it each time, while
        /// one repaired in place is served again soon after
        /// </summary>
        private static readonly TimeSpan UnreadableRecheckInterval = TimeSpan.FromMinutes(10);

        private readonly TimeProvider clock;

        /// <summary>
        /// Told of each unreadable file once per UTC day, as the file stood when it failed
        /// </summary>
        private readonly Action<string, Exception> reportUnreadable;

        /// <summary>
        /// The indexes of the version last found for each folder and file name prefix; a version with other files, a new delta generation
        /// among them, replaces it
        /// </summary>
        private readonly ConcurrentDictionary<string, LoadedVersion> versionsByPrefix = new ConcurrentDictionary<string, LoadedVersion>(StringComparer.Ordinal);

        /// <summary>
        /// Each unreadable file as it was found, when it was last found so, and the UTC day it was reported on
        /// </summary>
        private readonly ConcurrentDictionary<string, UnreadableFile> unreadableFiles = new ConcurrentDictionary<string, UnreadableFile>(StringComparer.Ordinal);

        /// <summary>
        /// Counts version uses, so the least recently used one can be told apart
        /// </summary>
        private long useCounter;

        public ArchiveCatalog(TimeProvider clock, Action<string, Exception> reportUnreadable)
        {
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(reportUnreadable);

            this.clock = clock;
            this.reportUnreadable = reportUnreadable;
        }

        /// <summary>
        /// Reads a symbol-day from the current version in its month folder
        /// </summary>
        /// <typeparam name="TSource">Record interface</typeparam>
        /// <typeparam name="TRecord">Record type a read returns</typeparam>
        /// <param name="schema">Archive schema of the Databento schema</param>
        /// <param name="folder">Month folder of the day, as listed; listed again when a file of it is gone</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="date">UTC day, in the folder's month</param>
        /// <returns>The day's records, empty for a day archived as empty, or <c>null</c> when no readable archive file lists the day</returns>
        public async Task<TRecord[]> ReadSymbolDayAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, CacheMonthFolder folder, string symbol, DateTime date)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(folder);

            string prefix = ArchiveFileName.GetSearchPrefix(folder.Dataset, folder.Schema, ArchiveGroup.Of(folder.Dataset, symbol), folder.Year, folder.Month, folder.Cutoff);

            // the compactor retires a version after publishing the next, so a file gone between listing and opening has a successor
            for(int attempt = 0; attempt < 2; ++attempt)
            {
                LoadedIndex file = null;
                try
                {
                    LoadedVersion version = await this.FindVersionAsync(schema, folder, prefix).ConfigureAwait(false);
                    if(version == null)
                    {
                        return null;
                    }

                    file = version.Locate(symbol, date.Day, out ArchiveDayLocation location);
                    if(file == null)
                    {
                        return null;
                    }

                    if(location.RecordCount == 0)
                    {
                        return Array.Empty<TRecord>();
                    }

                    ArchiveFileReader<TSource, TRecord> reader = await ArchiveFile.ReopenAsync(file.Path, schema, file.Index, file.RowGroupStarts).ConfigureAwait(false);
                    await using(reader.ConfigureAwait(false))
                    {
                        return await reader.ReadSymbolDayAsync(symbol, date).ConfigureAwait(false);
                    }
                }
                catch(Exception ex) when(IsRetired(ex))
                {
                    folder.Refresh();
                }
                catch(Exception ex) when(file != null && IsUnreadable(ex))
                {
                    this.MarkUnreadable(folder.Directory, prefix, file.Path, file.Stamp, ex);
                    return null;
                }
                catch(Exception ex) when(IsUnreadable(ex))
                {
                    // a file of the version could not even be looked at, such as a share gone: not cached for this read
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Tells whether the current version in a symbol-day's month folder lists the day, from its index alone
        /// </summary>
        /// <typeparam name="TSource">Record interface</typeparam>
        /// <typeparam name="TRecord">Record type a read returns</typeparam>
        /// <param name="schema">Archive schema of the Databento schema</param>
        /// <param name="folder">Month folder of the day, as listed; listed again when a file of it is gone</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="date">UTC day, in the folder's month</param>
        /// <param name="strict">Whether a file of the version that cannot be read throws, rather than counting as not listing the day</param>
        /// <returns><c>true</c> when a readable archive file lists the day, empty or not</returns>
        /// <exception cref="InvalidDataException">Strict, and a file of the version cannot be read</exception>
        public async Task<bool> ContainsSymbolDayAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, CacheMonthFolder folder, string symbol, DateTime date, bool strict)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(folder);

            string prefix = ArchiveFileName.GetSearchPrefix(folder.Dataset, folder.Schema, ArchiveGroup.Of(folder.Dataset, symbol), folder.Year, folder.Month, folder.Cutoff);
            for(int attempt = 0; attempt < 2; ++attempt)
            {
                try
                {
                    LoadedVersion version = await this.FindVersionAsync(schema, folder, prefix).ConfigureAwait(false);
                    if(version?.Locate(symbol, date.Day, out _) != null)
                    {
                        return true;
                    }

                    ArchiveVersion current = ArchiveFileName.FindCurrentVersion(folder.Files, prefix);
                    if(strict && current != null && (version == null || version.Files.Count != current.Paths.Count))
                    {
                        throw new InvalidDataException(Invariant($"An archive file of {Path.Combine(folder.Directory, prefix)}* cannot be read, so whether it holds {symbol} {date:yyyy-MM-dd} is unknown."));
                    }

                    return false;
                }
                catch(Exception ex) when(IsRetired(ex))
                {
                    folder.Refresh();
                }
                catch(Exception ex) when(!strict && IsUnreadable(ex))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsRetired(Exception ex)
        {
            return ex is FileNotFoundException || ex is DirectoryNotFoundException;
        }

        /// <summary>
        /// Tells whether a failure is the file's: its contents, or the disk under it. Anything else is a fault in this code and reaches the
        /// caller, rather than having a good file reported as bad and its days paid for again.
        /// </summary>
        private static bool IsUnreadable(Exception ex)
        {
            return ex is InvalidDataException || (ex is IOException && !IsRetired(ex)) || ex is UnauthorizedAccessException;
        }

        private static async Task<LoadedIndex> ReadIndexAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, string path, FileStamp stamp)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArchiveFileReader<TSource, TRecord> reader = await ArchiveFile.OpenAsync(path, schema).ConfigureAwait(false);
            await using(reader.ConfigureAwait(false))
            {
                return new LoadedIndex(path, stamp, reader.Index, reader.RowGroupStarts);
            }
        }

        /// <summary>
        /// Gets the size and last write time of a file, which tell a file restored under the same name from the one read before
        /// </summary>
        /// <exception cref="FileNotFoundException">The file is gone</exception>
        private static FileStamp GetStamp(string path)
        {
            FileInfo file = new FileInfo(path);
            return new FileStamp(file.Length, file.LastWriteTimeUtc);
        }

        /// <summary>
        /// Finds the current version of a group-month, its full file and current delta, and the indexes of those that can be read, from
        /// memory for every file unchanged since it was read. An older delta generation is never read.
        /// </summary>
        /// <returns>The version, or <c>null</c> when there is none or its full file cannot be read</returns>
        /// <exception cref="FileNotFoundException">A file of the version was retired while it was being read</exception>
        private async Task<LoadedVersion> FindVersionAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, CacheMonthFolder folder, string prefix)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArchiveVersion version = ArchiveFileName.FindCurrentVersion(folder.Files, prefix);
            if(version == null)
            {
                return null;
            }

            string key = Path.Combine(folder.Directory, prefix);
            this.versionsByPrefix.TryGetValue(key, out LoadedVersion cached);
            List<LoadedIndex> files = new List<LoadedIndex>();
            bool unchanged = cached != null && cached.Files.Count == version.Paths.Count;
            foreach(string path in version.Paths)
            {
                FileStamp stamp = GetStamp(path);
                LoadedIndex file = cached?.Files.FirstOrDefault(loaded => string.Equals(loaded.Path, path, StringComparison.Ordinal) && loaded.Stamp == stamp);
                if(file == null)
                {
                    unchanged = false;
                    file = await this.TryReadIndexAsync(schema, folder.Directory, prefix, path, stamp).ConfigureAwait(false);
                }

                if(file != null)
                {
                    files.Add(file);
                }
                else if(string.Equals(path, version.FullPath, StringComparison.Ordinal))
                {
                    // a delta only adds to its full file, so without the full file the group-month is not cached
                    return null;
                }
            }

            // any other set of files replaces the cached one: a new delta generation or full file, an older version restored, or one copied over a file
            LoadedVersion loadedVersion = unchanged ? cached : new LoadedVersion(files);
            loadedVersion.LastUse = Interlocked.Increment(ref this.useCounter);
            if(!unchanged)
            {
                this.versionsByPrefix[key] = loadedVersion;
                this.DropLeastRecentlyUsed();
            }

            return loadedVersion;
        }

        /// <summary>
        /// Reads a file's index
        /// </summary>
        /// <returns>The index, or <c>null</c> for a file that cannot be read, or was found so a moment ago and is unchanged since</returns>
        /// <exception cref="FileNotFoundException">The file was retired while it was being read</exception>
        private async Task<LoadedIndex> TryReadIndexAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, string directory, string prefix, string path, FileStamp stamp)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            if(this.unreadableFiles.TryGetValue(path, out UnreadableFile unreadable) && unreadable.Stamp == stamp && this.clock.GetUtcNow().UtcDateTime - unreadable.FoundUtc < UnreadableRecheckInterval)
            {
                return null;
            }

            try
            {
                return await ReadIndexAsync(schema, path, stamp).ConfigureAwait(false);
            }
            catch(Exception ex) when(IsUnreadable(ex))
            {
                this.MarkUnreadable(directory, prefix, path, stamp, ex);
                return null;
            }
        }

        private void DropLeastRecentlyUsed()
        {
            while(this.versionsByPrefix.Count > MaxCachedVersions)
            {
                KeyValuePair<string, LoadedVersion> oldest = this.versionsByPrefix.MinBy(entry => entry.Value.LastUse);
                this.versionsByPrefix.TryRemove(oldest);
            }
        }

        /// <summary>
        /// Forgets the cached version holding the file and remembers the file as unreadable, reporting it once a day per file as it stood
        /// when it failed
        /// </summary>
        private void MarkUnreadable(string directory, string prefix, string path, FileStamp stamp, Exception ex)
        {
            string key = Path.Combine(directory, prefix);
            if(this.versionsByPrefix.TryGetValue(key, out LoadedVersion cached) && cached.Files.Any(file => string.Equals(file.Path, path, StringComparison.Ordinal)))
            {
                this.versionsByPrefix.TryRemove(new KeyValuePair<string, LoadedVersion>(key, cached));
            }

            // set atomically, so two reads failing on the same file at once report it once between them
            DateTime now = this.clock.GetUtcNow().UtcDateTime;
            UnreadableFile marked = new UnreadableFile(stamp, now.Date, now);
            while(true)
            {
                if(this.unreadableFiles.TryGetValue(path, out UnreadableFile existing))
                {
                    if(this.unreadableFiles.TryUpdate(path, marked, existing))
                    {
                        if(existing.Stamp == stamp && existing.ReportedOn == now.Date)
                        {
                            return;
                        }

                        break;
                    }
                }
                else if(this.unreadableFiles.TryAdd(path, marked))
                {
                    break;
                }
            }

            this.reportUnreadable(path, ex);
        }

        /// <summary>
        /// A file's size and last write time
        /// </summary>
        private readonly record struct FileStamp(long Length, DateTime LastWriteUtc);

        /// <summary>
        /// An unreadable file as it was found, the UTC day it was reported on, and when it was last found unreadable
        /// </summary>
        private readonly record struct UnreadableFile(FileStamp Stamp, DateTime ReportedOn, DateTime FoundUtc);

        /// <summary>
        /// An archive file's path, its parsed index and row-group layout
        /// </summary>
        private sealed record LoadedIndex(string Path, FileStamp Stamp, ArchiveIndex Index, IReadOnlyList<long> RowGroupStarts);

        /// <summary>
        /// The readable files of a version, its full file and then its current delta, and when the version was last used
        /// </summary>
        private sealed class LoadedVersion
        {
            public LoadedVersion(IReadOnlyList<LoadedIndex> files)
            {
                this.Files = files;
            }

            /// <summary>
            /// Gets the readable files, the full file first, then its current delta.
            /// </summary>
            public IReadOnlyList<LoadedIndex> Files { get; }

            /// <summary>
            /// Gets or sets the use count at the last use, larger meaning more recent.
            /// </summary>
            public long LastUse { get; set; }

            /// <summary>
            /// Finds the file a symbol-day is read from: the delta when it lists the day, else the full file
            /// </summary>
            /// <returns>The file, or <c>null</c> when none lists the day</returns>
            public LoadedIndex Locate(string symbol, int day, out ArchiveDayLocation location)
            {
                for(int i = this.Files.Count - 1; i >= 0; --i)
                {
                    if(this.Files[i].Index.TryLocate(symbol, day, out location))
                    {
                        return this.Files[i];
                    }
                }

                location = default;
                return null;
            }
        }
    }
}
