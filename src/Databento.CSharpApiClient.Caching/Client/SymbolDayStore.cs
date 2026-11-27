using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Reads symbol-days from the cache, the day file first and then the archive, and saves downloaded ones as day files. A day file that
    /// cannot be parsed is deleted, so the day is downloaded again; the archive is only ever read.
    /// </summary>
    internal sealed class SymbolDayStore
    {
        private readonly string cacheRoot;

        private readonly ArchiveCatalog catalog;

        private readonly ILogger logger;

        /// <summary>
        /// Whether the root's <c>metadata.json</c> is known to be there, so it is written before the first day file
        /// </summary>
        private volatile bool metadataWritten;

        private long monthFolderListings;

        public SymbolDayStore(string cacheRoot, string cutoff, ArchiveCatalog catalog, ILogger logger)
        {
            this.cacheRoot = cacheRoot;
            this.Cutoff = cutoff;
            this.catalog = catalog;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the day cutoff tag of the cache.
        /// </summary>
        public string Cutoff { get; }

        /// <summary>
        /// Gets how many times a month folder was listed, for tests.
        /// </summary>
        internal long MonthFolderListings => Interlocked.Read(ref this.monthFolderListings);

        /// <summary>
        /// Gets a schema's month folder for one request, which lists its archive files the first time they are asked for
        /// </summary>
        public CacheMonthFolder GetMonth(string dataset, string schema, DateTime day)
        {
            return new CacheMonthFolder(this.cacheRoot, dataset, schema, day.Year, day.Month, this.Cutoff, () => Interlocked.Increment(ref this.monthFolderListings));
        }

        /// <summary>
        /// Reads a symbol-day: its day file, opened by name, else the archive
        /// </summary>
        /// <param name="schema">Schema served</param>
        /// <param name="folder">Month folder of the day</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="logServed">Whether where the day was served from, or that it is not cached, is logged; not for a look at what a
        /// bypass would replace</param>
        /// <returns>The records, empty for a day cached as empty, or <c>null</c> when the day is not cached</returns>
        public async Task<TSource[]> ReadAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, CacheMonthFolder folder, string symbol, DateTime day, CancellationToken cancellationToken, bool logServed = true)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            string path = Path.Combine(folder.Directory, DayFileName.Format(symbol, day, this.Cutoff));
            TJson[] records = null;
            try
            {
                records = await DayFile.ReadAsync(path, schema.ReadDayFile, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                // no day file, or compacted away since: the archive is asked
            }
            catch(Exception ex) when(ex is InvalidDataException || ex is JsonException)
            {
                if(TryDelete(path, out string deleteError))
                {
                    this.logger.DayFileDeleted(path, ex.Message);
                }
                else
                {
                    this.logger.DayFileNotDeleted(path, ex.Message, deleteError);
                }
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // the disk's, not the file's: left as it is
                this.logger.DayFileNotRead(path, ex.Message);
            }

            // logged once the read is done, apart from its failures
            if(records != null)
            {
                if(logServed)
                {
                    this.logger.ServedFromDayFile(symbol, day, folder.Schema, Path.GetFileName(path), records.Length);
                }

                return records;
            }

            // listed here, not inside the archive read, so a folder that cannot be listed fails the request rather than reading as not cached
            _ = folder.ArchiveFiles;
            TRecord[] archived = await this.catalog.ReadSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day, logServed).ConfigureAwait(false);
            if(archived == null && folder.RefreshIfChanged())
            {
                // a compaction since the listing may have absorbed the day file and deleted it
                _ = folder.ArchiveFiles;
                archived = await this.catalog.ReadSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day, logServed).ConfigureAwait(false);
            }
            if(archived == null && logServed)
            {
                this.logger.NotCached(symbol, day, folder.Schema, folder.Dataset);
            }

            return archived;
        }

        /// <summary>
        /// Tells whether the cache holds a symbol-day, from its day file being there, readable or not, or the archive's index, reading no
        /// records
        /// </summary>
        /// <param name="schema">Schema served</param>
        /// <param name="folder">Month folder of the day</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="strict">Whether an archive file that cannot be read throws, rather than counting as not holding the day</param>
        /// <returns><c>true</c> when a day file or a readable archive file holds the day, empty or not</returns>
        public async Task<bool> ContainsAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, CacheMonthFolder folder, string symbol, DateTime day, bool strict = false)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            if(DayFileExists(Path.Combine(folder.Directory, DayFileName.Format(symbol, day, this.Cutoff))))
            {
                return true;
            }

            _ = folder.ArchiveFiles;
            if(await this.catalog.ContainsSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day, strict).ConfigureAwait(false))
            {
                return true;
            }

            // a compaction since the listing may have absorbed the day file and deleted it
            if(folder.RefreshIfChanged())
            {
                _ = folder.ArchiveFiles;
                return await this.catalog.ContainsSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day, strict).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Saves a symbol-day as a day file, writing the root's metadata first when it has none
        /// </summary>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="schema">Databento schema</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="records">The day's records; empty for a day saved as empty</param>
        /// <param name="overwrite">Whether a day file already there is replaced; when not, it is kept</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="emptyReason">Why a day with no records is saved as empty, for the log; <c>null</c> for a day with records</param>
        /// <returns><c>true</c> when the day file now holds these records, <c>false</c> when one already there was kept</returns>
        public async Task<bool> SaveAsync<TJson>(string dataset, string schema, string symbol, DateTime day, IReadOnlyList<TJson> records, bool overwrite, CancellationToken cancellationToken, string emptyReason = null)
        {
            if(!this.metadataWritten)
            {
                CacheMetadata.EnsureCreated(this.cacheRoot);
                this.metadataWritten = true;
            }

            string path = Path.Combine(DayFileName.GetMonthDirectory(this.cacheRoot, dataset, schema, day.Year, day.Month), DayFileName.Format(symbol, day, this.Cutoff));
            bool saved = await DayFile.WriteAsync(path, records, overwrite, cancellationToken).ConfigureAwait(false);
            if(saved && records.Count == 0 && emptyReason != null)
            {
                this.logger.EmptyDaySaved(symbol, day, schema, Path.GetFileName(path), emptyReason);
            }
            else if(saved)
            {
                this.logger.Saved(symbol, day, schema, records.Count, Path.GetFileName(path));
            }
            else
            {
                this.logger.DayFileKept(Path.GetFileName(path), records.Count, symbol, day, schema);
            }

            return saved;
        }

        /// <summary>
        /// Tells whether a day file is there; unlike <see cref="File.Exists(string)"/>, which answers false for any I/O failure, a failure
        /// other than the file or its folder not being there throws
        /// </summary>
        private static bool DayFileExists(string path)
        {
            try
            {
                File.GetAttributes(path);
                return true;
            }
            catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes a day file that cannot be parsed
        /// </summary>
        /// <returns><c>false</c> when it could not be deleted, with why: it is read again next time, and deleted then</returns>
        private static bool TryDelete(string path, out string error)
        {
            try
            {
                File.Delete(path);
                error = null;
                return true;
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
