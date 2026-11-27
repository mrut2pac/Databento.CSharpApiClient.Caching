using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching.Archive
{
    /// <summary>
    /// Answers symbol-days from the current version of their group and month: its newest full file overlaid by the highest generation of
    /// that file's delta, the delta winning a day both list. Recent versions' files are kept open with their indexes, since a published
    /// file never changes, so a read parses no footer; a file that cannot be read is reported once a day and left out, never touched.
    /// </summary>
    internal sealed class ArchiveCatalog : IDisposable
    {
        /// <summary>
        /// How many group-months' files are kept open; the least recently used is closed beyond it, since an options month's index is large
        /// </summary>
        internal const int MaxCachedVersions = 32;

        /// <summary>
        /// How many times a read lists a month folder's archive files: once, and once more when a listed file is gone. The compactor publishes the next version
        /// before it retires a file, so the second listing finds it; only another compaction finished between that listing and the open would
        /// need a third, and compactions take the root's lock and seconds each, against the milliseconds between the two.
        /// </summary>
        private const int ListingAttempts = 2;

        /// <summary>
        /// How long an unreadable file is left out without reading it again, so a run asking for its days does not parse it each time, while
        /// one repaired in place is served again soon after
        /// </summary>
        public static readonly TimeSpan UnreadableRecheckInterval = TimeSpan.FromMinutes(10);

        private readonly TimeProvider clock;

        /// <summary>
        /// Told of each unreadable file once per UTC day, as the file stood when it failed
        /// </summary>
        private readonly Action<string, Exception> reportUnreadable;

        private readonly ILogger logger;

        /// <summary>
        /// The indexes of the version last found for each folder and file name prefix; a version with other files, a new delta generation
        /// among them, replaces it
        /// </summary>
        private readonly ConcurrentDictionary<string, LoadedVersion> versionsByPrefix = new ConcurrentDictionary<string, LoadedVersion>(StringComparer.Ordinal);

        /// <summary>
        /// Taken to change <see cref="versionsByPrefix"/>, so each file a change drops is closed once
        /// </summary>
        private readonly object versionsLock = new object();

        /// <summary>
        /// Each unreadable file as it was found, when it was last found so, and the UTC day it was reported on
        /// </summary>
        private readonly ConcurrentDictionary<string, UnreadableFile> unreadableFiles = new ConcurrentDictionary<string, UnreadableFile>(StringComparer.Ordinal);

        /// <summary>
        /// Counts version uses, so the least recently used one can be told apart
        /// </summary>
        private long useCounter;

        /// <summary>
        /// Set once the catalog is disposed: no file is opened for a version after it
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Files opened and not closed yet, for <see cref="OpenFiles"/>
        /// </summary>
        private int openFiles;

        public ArchiveCatalog(TimeProvider clock, Action<string, Exception> reportUnreadable, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(reportUnreadable);
            ArgumentNullException.ThrowIfNull(logger);

            this.clock = clock;
            this.reportUnreadable = reportUnreadable;
            this.logger = logger;
        }

        /// <summary>
        /// Gets how many archive files are open, so a test can tell a file is closed once no version holds it.
        /// </summary>
        internal int OpenFiles => Volatile.Read(ref this.openFiles);

        /// <summary>
        /// Reads a symbol-day from the current version in its month folder
        /// </summary>
        /// <typeparam name="TSource">Record interface</typeparam>
        /// <typeparam name="TRecord">Record type a read returns</typeparam>
        /// <param name="schema">Archive schema of the Databento schema</param>
        /// <param name="folder">Month folder of the day, its archive files as listed; listed again when one of them is gone</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="date">UTC day, in the folder's month</param>
        /// <param name="logServed">Whether the file the day was served from is logged</param>
        /// <returns>The day's records, empty for a day archived as empty, or <c>null</c> when no readable archive file lists the day</returns>
        public async Task<TRecord[]> ReadSymbolDayAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, CacheMonthFolder folder, string symbol, DateTime date, bool logServed = true)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(folder);

            string prefix = ArchiveFileName.GetSearchPrefix(folder.Dataset, folder.Schema, ArchiveGroup.Of(folder.Dataset, symbol), folder.Year, folder.Month, folder.Cutoff);

            for(int attempt = 0; attempt < ListingAttempts; ++attempt)
            {
                LoadedIndex file = null;
                TRecord[] records;
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
                        records = Array.Empty<TRecord>();
                    }
                    else
                    {
                        records = await ReadAsync(schema, file, symbol, date).ConfigureAwait(false);
                    }
                }
                catch(Exception ex) when(IsRetired(ex))
                {
                    // listed here, in the catch, so a folder that cannot be listed fails the read rather than reading as not cached
                    folder.Refresh();
                    _ = folder.ArchiveFiles;
                    continue;
                }
                catch(Exception ex) when(file != null && IsUnreadable(ex))
                {
                    await this.MarkUnreadableAsync(folder.Directory, prefix, file.Path, file.Stamp, ex).ConfigureAwait(false);
                    return null;
                }
                catch(Exception ex) when(IsUnreadable(ex))
                {
                    // a file of the version could not even be looked at, such as a share gone: not cached for this read
                    return null;
                }

                // logged once the read is done, apart from its failures
                if(records != null && logServed)
                {
                    this.logger.ServedFromArchive(symbol, date, folder.Schema, Path.GetFileName(file.Path), records.Length);
                }

                return records;
            }

            return null;
        }

        /// <summary>
        /// Tells whether the current version in a symbol-day's month folder lists the day, from its index alone
        /// </summary>
        /// <typeparam name="TSource">Record interface</typeparam>
        /// <typeparam name="TRecord">Record type a read returns</typeparam>
        /// <param name="schema">Archive schema of the Databento schema</param>
        /// <param name="folder">Month folder of the day, its archive files as listed; listed again when one of them is gone</param>
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
            for(int attempt = 0; attempt < ListingAttempts; ++attempt)
            {
                try
                {
                    LoadedVersion version = await this.FindVersionAsync(schema, folder, prefix).ConfigureAwait(false);
                    if(version?.Locate(symbol, date.Day, out _) != null)
                    {
                        return true;
                    }

                    ArchiveVersion current = ArchiveFileName.FindCurrentVersion(folder.ArchiveFiles, prefix);
                    if(strict && current != null && (version == null || version.Files.Count != current.Paths.Count))
                    {
                        throw new InvalidDataException(Invariant($"An archive file of {Path.Combine(folder.Directory, prefix)}* cannot be read, so whether it holds {symbol} {date:yyyy-MM-dd} is unknown."));
                    }

                    return false;
                }
                catch(Exception ex) when(IsRetired(ex))
                {
                    folder.Refresh();
                    _ = folder.ArchiveFiles;
                }
                catch(Exception ex) when(!strict && IsUnreadable(ex))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Closes every archive file kept open; a read under way finishes first
        /// </summary>
        public void Dispose()
        {
            List<LoadedIndex> files;
            lock(this.versionsLock)
            {
                this.disposed = true;
                files = this.versionsByPrefix.Values.SelectMany(version => version.Files).ToList();
                this.versionsByPrefix.Clear();
            }

            this.CloseAsync(files).AsTask().GetAwaiter().GetResult();
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

        /// <summary>
        /// Reads a symbol-day from a file's open reader, or from a handle of its own when another request closed the reader since this one
        /// found it, its version replaced or dropped
        /// </summary>
        private static async Task<TRecord[]> ReadAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, LoadedIndex file, string symbol, DateTime date)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            try
            {
                return await ((ArchiveFileReader<TSource, TRecord>)file.Reader).ReadSymbolDayAsync(symbol, date).ConfigureAwait(false);
            }
            catch(ObjectDisposedException)
            {
                ArchiveFileReader<TSource, TRecord> reader = await ArchiveFile.OpenAsync(file.Path, schema).ConfigureAwait(false);
                await using(reader.ConfigureAwait(false))
                {
                    return await reader.ReadSymbolDayAsync(symbol, date).ConfigureAwait(false);
                }
            }
        }

        private async ValueTask CloseAsync(IEnumerable<LoadedIndex> files)
        {
            foreach(LoadedIndex file in files)
            {
                if(await file.CloseAsync().ConfigureAwait(false))
                {
                    Interlocked.Decrement(ref this.openFiles);
                }
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
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArchiveVersion version = ArchiveFileName.FindCurrentVersion(folder.ArchiveFiles, prefix);
            if(version == null)
            {
                return null;
            }

            string key = Path.Combine(folder.Directory, prefix);
            this.versionsByPrefix.TryGetValue(key, out LoadedVersion cached);
            List<LoadedIndex> files = new List<LoadedIndex>();
            List<LoadedIndex> opened = new List<LoadedIndex>();
            bool unchanged = cached != null && cached.Files.Count == version.Paths.Count;
            try
            {
                foreach(string path in version.Paths)
                {
                    FileStamp stamp = GetStamp(path);
                    // a file closed since, by a request that dropped the version it was taken from, is opened again
                    LoadedIndex file = cached?.Files.FirstOrDefault(loaded => string.Equals(loaded.Path, path, StringComparison.Ordinal) && loaded.Stamp == stamp && loaded.IsOpen);
                    if(file == null)
                    {
                        unchanged = false;
                        file = await this.TryOpenAsync(schema, folder.Directory, prefix, path, stamp).ConfigureAwait(false);
                        if(file != null)
                        {
                            opened.Add(file);
                        }
                    }

                    if(file != null)
                    {
                        files.Add(file);
                    }
                    else if(string.Equals(path, version.FullPath, StringComparison.Ordinal))
                    {
                        // a delta only adds to its full file, so without the full file the group-month is not cached
                        await this.CloseAsync(opened).ConfigureAwait(false);
                        return null;
                    }
                }
            }
            catch
            {
                // a file retired while the version was found: the files opened for it are no version's
                await this.CloseAsync(opened).ConfigureAwait(false);
                throw;
            }

            // any other set of files replaces the cached one: a new delta generation or full file, an older version restored, or one copied over a file
            LoadedVersion loadedVersion = unchanged ? cached : new LoadedVersion(files);
            loadedVersion.LastUse = Interlocked.Increment(ref this.useCounter);
            if(!unchanged)
            {
                await this.CloseAsync(this.Store(key, loadedVersion, opened)).ConfigureAwait(false);
            }

            return loadedVersion;
        }

        /// <summary>
        /// Opens a file and reads its index
        /// </summary>
        /// <returns>The open file, or <c>null</c> for a file that cannot be read, or was found so a moment ago and is unchanged since</returns>
        /// <exception cref="FileNotFoundException">The file was retired while it was being read</exception>
        private async Task<LoadedIndex> TryOpenAsync<TSource, TRecord>(ArchiveRecordSchema<TSource, TRecord> schema, string directory, string prefix, string path, FileStamp stamp)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            if(this.unreadableFiles.TryGetValue(path, out UnreadableFile unreadable) && unreadable.Stamp == stamp && this.clock.GetUtcNow().UtcDateTime - unreadable.FoundUtc < UnreadableRecheckInterval)
            {
                return null;
            }

            try
            {
                ArchiveFileReader<TSource, TRecord> reader = await ArchiveFile.OpenAsync(path, schema).ConfigureAwait(false);
                Interlocked.Increment(ref this.openFiles);
                return new LoadedIndex(path, stamp, reader.Index, reader);
            }
            catch(Exception ex) when(IsUnreadable(ex))
            {
                await this.MarkUnreadableAsync(directory, prefix, path, stamp, ex).ConfigureAwait(false);
                return null;
            }
        }

        /// <summary>
        /// Holds a version in place of the one held for its prefix, and drops the least recently used beyond <see cref="MaxCachedVersions"/>
        /// </summary>
        /// <returns>The files no longer held, to close</returns>
        private List<LoadedIndex> Store(string key, LoadedVersion version, IReadOnlyList<LoadedIndex> opened)
        {
            List<LoadedIndex> dropped = new List<LoadedIndex>();
            lock(this.versionsLock)
            {
                if(this.disposed)
                {
                    dropped.AddRange(opened);
                    return dropped;
                }

                if(this.versionsByPrefix.TryGetValue(key, out LoadedVersion replaced))
                {
                    dropped.AddRange(replaced.Files.Except(version.Files));
                }

                this.versionsByPrefix[key] = version;
                while(this.versionsByPrefix.Count > MaxCachedVersions)
                {
                    KeyValuePair<string, LoadedVersion> oldest = this.versionsByPrefix.MinBy(entry => entry.Value.LastUse);
                    this.versionsByPrefix.TryRemove(oldest);
                    dropped.AddRange(oldest.Value.Files);
                }
            }

            return dropped;
        }

        /// <summary>
        /// Forgets and closes the version held for a prefix when it holds the file, so the next read finds the version again
        /// </summary>
        private async ValueTask DropVersionAsync(string directory, string prefix, string path)
        {
            string key = Path.Combine(directory, prefix);
            LoadedVersion dropped = null;
            lock(this.versionsLock)
            {
                if(this.versionsByPrefix.TryGetValue(key, out LoadedVersion cached) && cached.Files.Any(file => string.Equals(file.Path, path, StringComparison.Ordinal)))
                {
                    this.versionsByPrefix.TryRemove(key, out dropped);
                }
            }

            if(dropped != null)
            {
                await this.CloseAsync(dropped.Files).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Forgets the cached version holding the file and remembers the file as unreadable, reporting it once a day per file as it stood
        /// when it failed
        /// </summary>
        private async ValueTask MarkUnreadableAsync(string directory, string prefix, string path, FileStamp stamp, Exception ex)
        {
            await this.DropVersionAsync(directory, prefix, path).ConfigureAwait(false);

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
        /// An archive file's path, its parsed index, and its reader, open while a version holds it
        /// </summary>
        private sealed class LoadedIndex
        {
            /// <summary>
            /// 1 once the file is closed, so a file two versions held in a race is closed once
            /// </summary>
            private int closed;

            public LoadedIndex(string path, FileStamp stamp, ArchiveIndex index, IAsyncDisposable reader)
            {
                this.Path = path;
                this.Stamp = stamp;
                this.Index = index;
                this.Reader = reader;
            }

            public string Path { get; }

            public FileStamp Stamp { get; }

            public ArchiveIndex Index { get; }

            public IAsyncDisposable Reader { get; }

            /// <summary>
            /// Gets whether the file is still open; one closed by an evicted or replaced version is opened again when found.
            /// </summary>
            public bool IsOpen => Volatile.Read(ref this.closed) == 0;

            /// <summary>
            /// Closes the file, unless it is closed already
            /// </summary>
            /// <returns>Whether this call closed it</returns>
            public async ValueTask<bool> CloseAsync()
            {
                if(Interlocked.Exchange(ref this.closed, 1) != 0)
                {
                    return false;
                }

                await this.Reader.DisposeAsync().ConfigureAwait(false);
                return true;
            }
        }

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
