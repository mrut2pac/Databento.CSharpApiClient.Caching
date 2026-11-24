using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

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

        /// <summary>
        /// Whether the root's <c>metadata.json</c> is known to be there, so it is written before the first day file
        /// </summary>
        private volatile bool metadataWritten;

        public SymbolDayStore(string cacheRoot, string cutoff, ArchiveCatalog catalog)
        {
            this.cacheRoot = cacheRoot;
            this.Cutoff = cutoff;
            this.catalog = catalog;
        }

        /// <summary>
        /// Gets the day cutoff tag of the cache.
        /// </summary>
        public string Cutoff { get; }

        /// <summary>
        /// Lists a schema's month folder
        /// </summary>
        public CacheMonthFolder ListMonth(string dataset, string schema, DateTime day)
        {
            return new CacheMonthFolder(this.cacheRoot, dataset, schema, day.Year, day.Month, this.Cutoff);
        }

        /// <summary>
        /// Reads a symbol-day: its day file when the folder lists one, else the archive
        /// </summary>
        /// <param name="schema">Schema served</param>
        /// <param name="folder">Month folder of the day, as listed</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The records, empty for a day cached as empty, or <c>null</c> when the day is not cached</returns>
        public async Task<TSource[]> ReadAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, CacheMonthFolder folder, string symbol, DateTime day, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            string fileName = DayFileName.Format(symbol, day, this.Cutoff);
            if(folder.Contains(fileName))
            {
                string path = Path.Combine(folder.Directory, fileName);
                try
                {
                    return await DayFile.ReadAsync(path, schema.ReadDayFile, cancellationToken).ConfigureAwait(false);
                }
                catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    // compacted since the folder was listed: the archive holds it now
                    folder.Refresh();
                }
                catch(Exception ex) when(ex is InvalidDataException || ex is JsonException)
                {
                    TryDelete(path);
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                {
                    // the disk's, not the file's: left as it is
                }
            }

            return await this.catalog.ReadSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day).ConfigureAwait(false);
        }

        /// <summary>
        /// Tells whether the cache holds a symbol-day, from the day file's name in the folder's listing or the archive's index, reading no
        /// records
        /// </summary>
        /// <param name="schema">Schema served</param>
        /// <param name="folder">Month folder of the day, as listed</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="strict">Whether an archive file that cannot be read throws, rather than counting as not holding the day</param>
        /// <returns><c>true</c> when a day file or a readable archive file holds the day, empty or not</returns>
        public Task<bool> ContainsAsync<TSource, TRecord, TJson>(CachedSchema<TSource, TRecord, TJson> schema, CacheMonthFolder folder, string symbol, DateTime day, bool strict = false)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            if(folder.Contains(DayFileName.Format(symbol, day, this.Cutoff)))
            {
                return Task.FromResult(true);
            }

            return this.catalog.ContainsSymbolDayAsync(schema.ArchiveSchema, folder, symbol, day, strict);
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
        /// <returns><c>true</c> when the day file now holds these records, <c>false</c> when one already there was kept</returns>
        public async Task<bool> SaveAsync<TJson>(string dataset, string schema, string symbol, DateTime day, IReadOnlyList<TJson> records, bool overwrite, CancellationToken cancellationToken)
        {
            if(!this.metadataWritten)
            {
                CacheMetadata.EnsureCreated(this.cacheRoot);
                this.metadataWritten = true;
            }

            string path = Path.Combine(DayFileName.GetMonthDirectory(this.cacheRoot, dataset, schema, day.Year, day.Month), DayFileName.Format(symbol, day, this.Cutoff));
            return await DayFile.WriteAsync(path, records, overwrite, cancellationToken).ConfigureAwait(false);
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            {
                // read again next time, and deleted then
            }
        }
    }
}
