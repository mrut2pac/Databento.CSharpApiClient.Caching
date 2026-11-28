using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

using ZstdSharp;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// Reads and writes day files: one symbol's records of one UTC day as zstd-compressed DBN, which Databento's own tools read too, its
    /// metadata naming the dataset, schema, day and symbol. A file of no records is a day cached as empty and final. Legacy day files,
    /// a gzipped JSON array of Databento's JSON records, are read and never written.
    /// </summary>
    internal static class DayFile
    {
        /// <summary>
        /// The DBN version day files are written in
        /// </summary>
        public const byte DbnVersion = 3;

        /// <summary>
        /// The zstd level day files are written at, zstd's default
        /// </summary>
        private const int ZstdLevel = 3;

        /// <summary>
        /// Reads a day file's records, DBN or legacy JSON by its extension
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="format">The schema's day files</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The records, in the order they were written</returns>
        /// <exception cref="InvalidDataException">The file is not zstd-compressed DBN of the schema (or, for a legacy file, gzip holding a JSON array
        /// of the schema's records)</exception>
        /// <exception cref="System.Text.Json.JsonException">A legacy file's JSON is malformed</exception>
        /// <exception cref="IOException">The file cannot be read</exception>
        public static async Task<TRecord[]> ReadAsync<TSource, TRecord>(string path, DayFileFormat<TSource, TRecord> format, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            await using(file.ConfigureAwait(false))
            {
                if(DayFileName.IsLegacy(path))
                {
                    return await ReadLegacyAsync(file, format, cancellationToken).ConfigureAwait(false);
                }

                DecompressionStream dbn = new DecompressionStream(file, leaveOpen: true);
                await using(dbn.ConfigureAwait(false))
                {
                    try
                    {
                        return await ToArrayAsync(format.ReadDbn(dbn, cancellationToken), cancellationToken).ConfigureAwait(false);
                    }
                    catch(Exception ex) when(ex is ZstdException || ex is EndOfStreamException)
                    {
                        throw new InvalidDataException(Invariant($"{Path.GetFileName(path)} is no zstd-compressed DBN: {ex.Message}"), ex);
                    }
                }
            }
        }

        /// <summary>
        /// Writes a day file under a temporary name, then renames it into place, so a reader never sees half a file
        /// </summary>
        /// <param name="path">File path; its folder is created when missing</param>
        /// <param name="format">The schema's day files</param>
        /// <param name="dataset">Databento dataset, which the metadata names</param>
        /// <param name="symbol">Databento raw symbol of the records</param>
        /// <param name="day">UTC day of the records</param>
        /// <param name="records">The day's records, in Databento's order; empty for a day cached as empty</param>
        /// <param name="overwrite">Whether a file already there is replaced; when not, the file there is kept and this write dropped</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns><c>true</c> when the file now holds these records, <c>false</c> when a file already there was kept</returns>
        /// <exception cref="InvalidDataException">A record holds a value DBN cannot hold, as <see cref="DbnWriter"/> refuses it; no file is written</exception>
        /// <exception cref="ArgumentException">The dataset or symbol cannot be written in DBN metadata</exception>
        public static async Task<bool> WriteAsync<TSource, TRecord>(string path, DayFileFormat<TSource, TRecord> format, string dataset, string symbol, DateTime day, IReadOnlyList<TRecord> records, bool overwrite, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            ArgumentNullException.ThrowIfNull(records);

            // the metadata checked on its own, so the writer's refusal below can only be a record's
            DbnMetadata metadata = CreateMetadata(format.DbnSchema, dataset, symbol, day, records);
            using(BinaryWriter check = new BinaryWriter(Stream.Null))
            {
                metadata.WriteToBinaryWriter(check);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = Invariant($"{path}.{Guid.NewGuid():N}.tmp");
            try
            {
                FileStream file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await using(file.ConfigureAwait(false))
                {
                    CompressionStream dbn = new CompressionStream(file, ZstdLevel, leaveOpen: true);
                    await using(dbn.ConfigureAwait(false))
                    {
                        try
                        {
                            await format.WriteDbn(dbn, metadata, ToAsyncEnumerable(records), cancellationToken).ConfigureAwait(false);
                        }
                        catch(ArgumentException ex)
                        {
                            throw new InvalidDataException(Invariant($"A record of {symbol} {day:yyyy-MM-dd} cannot be written as DBN: {ex.Message}"), ex);
                        }
                    }

                    file.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temporaryPath, path, overwrite);
                    return true;
                }
                catch(IOException) when(!overwrite && File.Exists(path))
                {
                    return false;
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Gets the metadata of a day file: its dataset, schema and day, its symbol, and that symbol's mapping from each instrument id of
        /// its records over the day, which a reader maps the records' symbol from
        /// </summary>
        internal static DbnMetadata CreateMetadata<TRecord>(DbnSchema schema, string dataset, string symbol, DateTime day, IReadOnlyList<TRecord> records)
            where TRecord : IRecord
        {
            ArgumentException.ThrowIfNullOrEmpty(symbol);

            DateTime start = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
            uint startDate = ToDate(start);
            uint endDate = ToDate(start.AddDays(1));
            return new DbnMetadata
            {
                Version = DbnVersion,
                Dataset = dataset,
                Schema = schema,
                Start = new DateTimeOffset(start),
                End = new DateTimeOffset(start.AddDays(1)),
                STypeIn = DbnSType.RawSymbol,
                STypeOut = DbnSType.InstrumentId,
                Symbols = new List<string> { symbol },
                Mappings = new List<DbnSymbolMapping>
                {
                    new DbnSymbolMapping
                    {
                        RawSymbol = symbol,
                        Intervals = records.Select(record => record.InstrumentId).Distinct().Order().Select(instrumentId => new DbnMappingInterval
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Symbol = instrumentId.ToString(CultureInfo.InvariantCulture),
                        }).ToList(),
                    },
                },
            };
        }

        private static uint ToDate(DateTime day)
        {
            return (uint)(day.Year * 10000 + day.Month * 100 + day.Day);
        }

        private static async Task<TRecord[]> ReadLegacyAsync<TSource, TRecord>(FileStream file, DayFileFormat<TSource, TRecord> format, CancellationToken cancellationToken)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            GZipStream gzip = new GZipStream(file, CompressionMode.Decompress, leaveOpen: true);
            await using(gzip.ConfigureAwait(false))
            {
                // read one record at a time as it is decompressed, never the whole day at once
                JsonArrayStartStream json = new JsonArrayStartStream(gzip);
                await using(json.ConfigureAwait(false))
                {
                    return await ToArrayAsync(format.ReadJsonAsync(json, cancellationToken), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<TRecord[]> ToArrayAsync<TRecord>(IAsyncEnumerable<TRecord> records, CancellationToken cancellationToken)
        {
            List<TRecord> read = new List<TRecord>();
            await foreach(TRecord record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                read.Add(record);
            }

            return read.ToArray();
        }

        private static async IAsyncEnumerable<TRecord> ToAsyncEnumerable<TRecord>(IReadOnlyList<TRecord> records, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach(TRecord record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
