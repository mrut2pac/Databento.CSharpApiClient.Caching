using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.Caching.Logging;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Compaction
{
    /// <summary>
    /// Unpacks one group-month: writes every symbol-day its current version serves (the delta's copy where the delta lists the day, else
    /// the full file's) as a day file in its day folder, empty days included, each read back and checked column by column, then retires
    /// the group's archive files. A symbol-day whose day file or legacy day file is already there and readable keeps it, since that is
    /// what the cache serves; one that cannot be read is replaced by the archive's copy, as the cache would serve the archive's. The
    /// archive is retired only once no file of the group holds a day without a day file: an older file holding one fails the group and
    /// keeps everything, so it never becomes the version served. A failure or a stop leaves the archive with the day files written so far,
    /// which a rerun keeps.
    /// </summary>
    /// <typeparam name="TSource">Record interface</typeparam>
    /// <typeparam name="TRecord">Record type day files and archive reads give</typeparam>
    internal sealed class ArchiveGroupUnpacking<TSource, TRecord>
        where TSource : class, IRecord
        where TRecord : class, TSource
    {
        private readonly ArchiveSchemaHandler<TSource, TRecord> handler;

        private readonly ArchiveUnpackRequest request;

        private readonly UnpackRun run;

        public ArchiveGroupUnpacking(ArchiveSchemaHandler<TSource, TRecord> handler, ArchiveUnpackRequest request, UnpackRun run)
        {
            this.handler = handler;
            this.request = request;
            this.run = run;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            int written = 0;
            int kept = 0;
            ArchiveFileReader<TSource, TRecord> full = await ArchiveFile.OpenAsync(this.request.Version.FullPath, this.handler.RecordSchema).ConfigureAwait(false);
            await using(full.ConfigureAwait(false))
            {
                ArchiveFileReader<TSource, TRecord> delta = this.request.Version.DeltaPath != null ? await ArchiveFile.OpenAsync(this.request.Version.DeltaPath, this.handler.RecordSchema).ConfigureAwait(false) : null;
                try
                {
                    // the delta's days, then the full file's it does not list, each file in its index's order, so each row group is decoded once
                    List<(ArchiveFileReader<TSource, TRecord> Reader, ArchiveIndexEntry Entry)> served = new List<(ArchiveFileReader<TSource, TRecord> Reader, ArchiveIndexEntry Entry)>();
                    HashSet<(string Symbol, int Day)> inDelta = new HashSet<(string Symbol, int Day)>();
                    if(delta != null)
                    {
                        foreach(ArchiveIndexEntry entry in delta.Index.Entries)
                        {
                            served.Add((delta, entry));
                            inDelta.Add((entry.Symbol, entry.Day));
                        }
                    }

                    served.AddRange(full.Index.Entries.Where(entry => !inDelta.Contains((entry.Symbol, entry.Day))).Select(entry => (full, entry)));
                    foreach((ArchiveFileReader<TSource, TRecord> reader, ArchiveIndexEntry entry) in served)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if(await this.UnpackDayAsync(reader, entry, cancellationToken).ConfigureAwait(false))
                        {
                            ++written;
                        }
                        else
                        {
                            ++kept;
                        }
                    }
                }
                finally
                {
                    if(delta != null)
                    {
                        await delta.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }

            // every day file is written and flushed, and the readers closed: only now is the archive retired, whole, the stop no longer heeded
            if(!this.run.Options.DryRun)
            {
                await this.ThrowIfOtherFilesHoldOtherDaysAsync().ConfigureAwait(false);
                foreach(string temporary in this.request.TemporaryFiles)
                {
                    ArchiveFileOperations.TryDeleteOnce(temporary);
                }

                // the older files first and the full file last, so a stop leaves no delta without its full file
                IEnumerable<string> retired = this.request.OtherFiles.Concat(this.request.Version.Paths.Reverse());
                foreach(string path in retired)
                {
                    if(await this.run.FileOperations.DeleteAsync(path, null, CancellationToken.None).ConfigureAwait(false) == DeleteResult.Deleted)
                    {
                        this.run.Report.ArchiveFileDeleted();
                    }
                    else
                    {
                        this.run.Report.FileLeft(Path.GetRelativePath(this.run.Options.CacheRoot, path));
                    }
                }
            }

            this.run.Report.GroupUnpacked();
            this.run.Options.Logger.GroupUnpacked(this.request.DisplayName, written, kept);
        }

        /// <summary>
        /// Tells whether a day file reads, as the cache would serve it; one that cannot be parsed is served by the cache from the archive,
        /// so the archive's copy must replace it before the archive goes
        /// </summary>
        /// <exception cref="IOException">The file cannot be read for a reason of the disk's, which fails the group and keeps the archive</exception>
        private async Task<bool> IsReadableAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                await DayFile.ReadAsync(path, this.handler.Format, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch(Exception ex) when(ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is InvalidDataException || ex is JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes one symbol-day as a day file, unless a readable day file or legacy day file of it is already there
        /// </summary>
        /// <returns><c>true</c> when written, or for a dry run measured; <c>false</c> when one already there was kept</returns>
        private async Task<bool> UnpackDayAsync(ArchiveFileReader<TSource, TRecord> reader, ArchiveIndexEntry entry, CancellationToken cancellationToken)
        {
            DateTime date = new DateTime(this.request.Year, this.request.Month, entry.Day);
            string path = DayFileName.GetPath(this.request.Directory, entry.Symbol, date, this.run.Cutoff);
            string legacyPath = DayFileName.GetLegacyPath(this.request.Directory, entry.Symbol, date, this.run.Cutoff);
            bool dayFileThere = File.Exists(path);
            string existing = dayFileThere ? path : File.Exists(legacyPath) ? legacyPath : null;
            if(existing != null && await this.IsReadableAsync(existing, cancellationToken).ConfigureAwait(false))
            {
                this.run.Report.DayFileKept();
                return false;
            }

            TRecord[] records = await reader.ReadSymbolDayAsync(entry.Symbol, date).ConfigureAwait(false);
            if(this.run.Options.DryRun)
            {
                this.run.Report.DayFileWritten(await DayFile.MeasureAsync(this.handler.Format, this.request.Dataset, entry.Symbol, date, records, cancellationToken).ConfigureAwait(false));
                return true;
            }

            // an unreadable day file is replaced; an unreadable legacy one stays, the day file being read before it
            if(!await DayFile.WriteAsync(path, this.handler.Format, this.request.Dataset, entry.Symbol, date, records, overwrite: dayFileThere, cancellationToken).ConfigureAwait(false))
            {
                // saved by a client just now
                this.run.Report.DayFileKept();
                return false;
            }

            FileInfo writtenFile = new FileInfo(path);
            long length = writtenFile.Length;
            DateTime lastWriteUtc = writtenFile.LastWriteTimeUtc;
            string problem;
            try
            {
                problem = this.FindReadBackProblem(records, await DayFile.ReadAsync(path, this.handler.Format, cancellationToken).ConfigureAwait(false));
            }
            catch(Exception ex) when(ex is InvalidDataException || ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                problem = ex.Message;
            }

            if(problem != null)
            {
                // deleted only as this run wrote it: a client's bypass may have replaced it since with its own download
                if(await this.run.FileOperations.DeleteAsync(path, () => IsAsWritten(path, length, lastWriteUtc), CancellationToken.None).ConfigureAwait(false) == DeleteResult.Changed)
                {
                    this.run.Report.DayFileKept();
                    return false;
                }

                throw new InvalidDataException(Invariant($"{Path.GetFileName(path)} reads back {problem}, so it is deleted and the archive kept."));
            }

            this.run.Report.DayFileWritten(length);
            this.run.DayFileWritten?.Invoke(path);
            return true;
        }

        private static bool IsAsWritten(string path, long length, DateTime lastWriteUtc)
        {
            FileInfo file = new FileInfo(path);
            return file.Exists && file.Length == length && file.LastWriteTimeUtc == lastWriteUtc;
        }

        /// <summary>
        /// Finds how a day file read back differs from the records written, in any value the archive stores
        /// </summary>
        /// <returns>What differs, or <c>null</c> when nothing does</returns>
        private string FindReadBackProblem(TRecord[] expected, TRecord[] actual)
        {
            if(expected.Length != actual.Length)
            {
                return Invariant($"{actual.Length} records, where {expected.Length} were written");
            }

            for(int i = 0; i < expected.Length; ++i)
            {
                string column = this.handler.RecordSchema.FindDifferentColumn(expected[i], actual[i]);
                if(column != null)
                {
                    return Invariant($"a different {column} in record {i}");
                }
            }

            return null;
        }

        /// <summary>
        /// Refuses to retire the group while one of its files outside the current version, which a stopped or failed compaction left,
        /// holds a day with records that has no day file: once the current version went, that file would be the one served
        /// </summary>
        /// <exception cref="InvalidDataException">Such a file, or one that cannot be opened</exception>
        private async Task ThrowIfOtherFilesHoldOtherDaysAsync()
        {
            foreach(string path in this.request.OtherFiles)
            {
                ArchiveIndex index;
                try
                {
                    ArchiveFileReader<TSource, TRecord> reader = await ArchiveFile.OpenAsync(path, this.handler.RecordSchema).ConfigureAwait(false);
                    await using(reader.ConfigureAwait(false))
                    {
                        index = reader.Index;
                    }
                }
                catch(Exception ex) when(ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw new InvalidDataException(Invariant($"{Path.GetFileName(path)}, outside the current version, cannot be opened: {ex.Message} Its days may have no other copy, so the archive is kept."), ex);
                }

                ArchiveIndexEntry uncovered = index.Entries.FirstOrDefault(entry => entry.RecordCount > 0 && !this.HasDayFile(entry));
                if(uncovered != null)
                {
                    throw new InvalidDataException(Invariant($"{Path.GetFileName(path)}, outside the current version, holds {uncovered.Symbol} {this.request.Year:0000}-{this.request.Month:00}-{uncovered.Day:00}, which has no day file, so the archive is kept; compacting the group first retires that file or keeps it as an orphan."));
                }
            }
        }

        private bool HasDayFile(ArchiveIndexEntry entry)
        {
            DateTime date = new DateTime(this.request.Year, this.request.Month, entry.Day);
            return File.Exists(DayFileName.GetPath(this.request.Directory, entry.Symbol, date, this.run.Cutoff))
                || File.Exists(DayFileName.GetLegacyPath(this.request.Directory, entry.Symbol, date, this.run.Cutoff));
        }
    }
}
