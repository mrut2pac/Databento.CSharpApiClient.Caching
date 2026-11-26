using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// Reads and writes day files: one symbol's records of one UTC day as a JSON array of Databento's JSON records, gzip-compressed and
    /// readable by hand. An empty array is a day cached as empty and final.
    /// </summary>
    internal static class DayFile
    {
        /// <summary>
        /// Reads a day file's records
        /// </summary>
        /// <typeparam name="TJson">JSON record type of the file's schema</typeparam>
        /// <param name="path">File path</param>
        /// <param name="readRecords">The schema's JSON record reader, e.g. <see cref="JsonRecordReader.ReadCbboAsync"/></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The records, in the order they were written</returns>
        /// <exception cref="InvalidDataException">The file is not gzip, holds no JSON array (an empty file or payload, whitespace, JSON lines),
        /// or holds JSON that is no records of the schema</exception>
        /// <exception cref="IOException">The file cannot be read</exception>
        public static async Task<TJson[]> ReadAsync<TJson>(string path, Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> readRecords, CancellationToken cancellationToken)
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            await using(file.ConfigureAwait(false))
            {
                GZipStream gzip = new GZipStream(file, CompressionMode.Decompress);
                await using(gzip.ConfigureAwait(false))
                {
                    // read one record at a time as it is decompressed, never the whole day at once
                    JsonArrayStartStream json = new JsonArrayStartStream(gzip);
                    await using(json.ConfigureAwait(false))
                    {
                        List<TJson> records = new List<TJson>();
                        await foreach(TJson record in readRecords(json, cancellationToken).ConfigureAwait(false))
                        {
                            records.Add(record);
                        }

                        return records.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Writes a day file under a temporary name, then renames it into place, so a reader never sees half a file
        /// </summary>
        /// <typeparam name="TJson">JSON record type of the file's schema</typeparam>
        /// <param name="path">File path; its folder is created when missing</param>
        /// <param name="records">The day's records, in Databento's order; empty for a day cached as empty</param>
        /// <param name="overwrite">Whether a file already there is replaced; when not, the file there is kept and this write dropped</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns><c>true</c> when the file now holds these records, <c>false</c> when a file already there was kept</returns>
        public static async Task<bool> WriteAsync<TJson>(string path, IReadOnlyList<TJson> records, bool overwrite, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(records);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = Invariant($"{path}.{Guid.NewGuid():N}.tmp");
            try
            {
                FileStream file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await using(file.ConfigureAwait(false))
                {
                    GZipStream json = new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true);
                    await using(json.ConfigureAwait(false))
                    {
                        await JsonSerializer.SerializeAsync(json, records, cancellationToken: cancellationToken).ConfigureAwait(false);
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
    }
}
