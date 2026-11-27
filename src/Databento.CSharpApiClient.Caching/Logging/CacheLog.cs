using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Databento.CSharpApiClient.Caching.Logging
{
    /// <summary>
    /// Every line the package logs, written through the <see cref="LoggerMessageAttribute"/> source generator, so nothing is formatted
    /// while its level is off. Event ids: 1000s the cached client, 2000s its batch jobs, 3000s the compactor, 4000s the backup, 5000s the
    /// restore. Market data values (prices, sizes) are never logged.
    /// </summary>
    /// <remarks>
    /// Each method keeps at most 6 template arguments, in the order the template names them, so the generator writes it with
    /// <see cref="LoggerMessage.Define{T1, T2, T3, T4, T5, T6}(LogLevel, EventId, string)"/>, which renders the message with the invariant
    /// culture; past 6 it formats with the current one. <c>CacheLogTests</c> fails when a line renders differently in another culture.
    /// Structured sinks get the raw values.
    /// </remarks>
    internal static partial class CacheLog
    {
        /// <summary>
        /// How many suspect days a group's warning names, the rest counted only
        /// </summary>
        private const int SuspectsNamed = 3;

        // the cached client

        [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Serving {Schema} of {Dataset} for {Symbols}, {StartUtc:yyyy-MM-ddTHH:mm:ssZ} to {EndUtc:yyyy-MM-ddTHH:mm:ssZ}, cache mode {CacheMode} ...")]
        private static partial void TimeseriesRequested(ILogger logger, string schema, string dataset, string symbols, DateTime startUtc, DateTime endUtc, CacheMode cacheMode);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} served from {File}, {RecordCount} records")]
        public static partial void ServedFromDayFile(this ILogger logger, string symbol, DateTime day, string schema, string file, int recordCount);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} served from {File}, {RecordCount} records")]
        public static partial void ServedFromArchive(this ILogger logger, string symbol, DateTime day, string schema, string file, int recordCount);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} of {Dataset} is not cached")]
        public static partial void NotCached(this ILogger logger, string symbol, DateTime day, string schema, string dataset);

        [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "The day file {Path} cannot be parsed and is deleted; its day is read from the archive, or downloaded as the cache mode allows: {Error}")]
        public static partial void DayFileDeleted(this ILogger logger, string path, string error);

        [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "The day file {Path} cannot be read and is left as it is; its day is read from the archive, or downloaded as the cache mode allows: {Error}")]
        public static partial void DayFileNotRead(this ILogger logger, string path, string error);

        [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "Waiting for another request of the process downloading {Count} symbol-day(s) of {Schema} of {Dataset} ...")]
        public static partial void WaitingForDownload(this ILogger logger, int count, string schema, string dataset);

        [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Requesting {Schema} of {Dataset} for {Symbols}, {Days} {Window} ...")]
        private static partial void Requesting(ILogger logger, string schema, string dataset, string symbols, string days, string window);

        [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Got {RecordCount} {Schema} records of {Dataset} from Databento, for {SymbolsWithRecords} of the {SymbolCount} symbol(s) requested. Took {Elapsed}")]
        public static partial void Downloaded(this ILogger logger, int recordCount, string schema, string dataset, int symbolsWithRecords, int symbolCount, TimeSpan elapsed);

        [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "{Schema} of {Dataset} for {Symbols} from {FirstDay:yyyy-MM-dd} is not available yet (the available end is {AvailableEndUtc:yyyy-MM-ddTHH:mm:ssZ}): served empty, never saved")]
        private static partial void NotAvailableYet(ILogger logger, string schema, string dataset, string symbols, DateTime firstDay, DateTime availableEndUtc);

        [LoggerMessage(EventId = 1011, Level = LogLevel.Warning, Message = "Databento refused {Schema} of {Dataset} for {SymbolCount} symbol(s): {Error} Asking again with only the {ListedCount} listed symbol(s)")]
        public static partial void RefusedAskingAgain(this ILogger logger, string schema, string dataset, int symbolCount, string error, int listedCount);

        [LoggerMessage(EventId = 1012, Level = LogLevel.Warning, Message = "Databento refused {Schema} of {Dataset} for {SymbolCount} symbol(s) and the symbology lookup cannot tell which symbol it is about, so nothing is saved: {Error}")]
        public static partial void RefusalUnresolved(this ILogger logger, string schema, string dataset, int symbolCount, string error);

        [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "Databento refused {Schema} of {Dataset} for {SymbolCount} symbol(s): {Error} None of them was listed, so they are served with no records")]
        public static partial void RefusedNoneListed(this ILogger logger, string schema, string dataset, int symbolCount, string error);

        [LoggerMessage(EventId = 1014, Level = LogLevel.Debug, Message = "Saved {Symbol} {Day:yyyy-MM-dd} {Schema} with {RecordCount} records as {File}")]
        public static partial void Saved(this ILogger logger, string symbol, DateTime day, string schema, int recordCount, string file);

        [LoggerMessage(EventId = 1015, Level = LogLevel.Debug, Message = "Kept the day file {File} already there; the {RecordCount} records of {Symbol} {Day:yyyy-MM-dd} {Schema} are not saved over it")]
        public static partial void DayFileKept(this ILogger logger, string file, int recordCount, string symbol, DateTime day, string schema);

        [LoggerMessage(EventId = 1016, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} is served but not saved: the day is not final ({Reason})")]
        public static partial void NotFinal(this ILogger logger, string symbol, DateTime day, string schema, string reason);

        [LoggerMessage(EventId = 1017, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} answered no records and is saved as empty as {File}: {Reason}")]
        public static partial void EmptyDaySaved(this ILogger logger, string symbol, DateTime day, string schema, string file, string reason);

        [LoggerMessage(EventId = 1018, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} answered no records and is not saved: {Reason}")]
        public static partial void EmptyDayNotSaved(this ILogger logger, string symbol, DateTime day, string schema, string reason);

        [LoggerMessage(EventId = 1019, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} answered no records on a bypass; the cached day is kept and served")]
        public static partial void BypassKeptCached(this ILogger logger, string symbol, DateTime day, string schema);

        [LoggerMessage(EventId = 1021, Level = LogLevel.Warning, Message = "The symbology lookup for {Count} empty symbol-day(s) of {Schema} of {Dataset} failed, so they are not saved: {Error}")]
        public static partial void EmptyDaysLookupFailed(this ILogger logger, int count, string schema, string dataset, string error);

        [LoggerMessage(EventId = 1022, Level = LogLevel.Error, Message = "The archive file {Path} cannot be read; its days are served as not cached, and it is read again after {RecheckInterval}")]
        public static partial void ArchiveFileUnreadable(this ILogger logger, string path, TimeSpan recheckInterval, Exception exception);

        [LoggerMessage(EventId = 1023, Level = LogLevel.Warning, Message = "The day file {Path} cannot be parsed and could not be deleted, so it is left; its day is read from the archive, or downloaded as the cache mode allows: {Error} {DeleteError}")]
        public static partial void DayFileNotDeleted(this ILogger logger, string path, string error, string deleteError);

        [LoggerMessage(EventId = 1024, Level = LogLevel.Warning, Message = "Downloading {Schema} of {Dataset} for {Symbols} from Databento failed after {Elapsed}")]
        private static partial void DownloadFailed(ILogger logger, string schema, string dataset, string symbols, TimeSpan elapsed, Exception exception);

        [LoggerMessage(EventId = 1025, Level = LogLevel.Debug, Message = "Read the available end of {Schema} of {Dataset}, {AvailableEndUtc:yyyy-MM-ddTHH:mm:ssZ}: requests are cut at it until it is read again from {RefreshUtc:yyyy-MM-ddTHH:mm:ssZ}")]
        public static partial void AvailableEndRemembered(this ILogger logger, string schema, string dataset, DateTime availableEndUtc, DateTime refreshUtc);

        [LoggerMessage(EventId = 1026, Level = LogLevel.Warning, Message = "Reading the available end of {Schema} of {Dataset} from Databento's dataset range failed, so Databento's refusal past it propagates")]
        public static partial void AvailableEndReadFailed(this ILogger logger, string schema, string dataset, Exception exception);

        [LoggerMessage(EventId = 1027, Level = LogLevel.Information, Message = "A bypass replaced the cached {Symbol} {Day:yyyy-MM-dd} {Schema}: cached {CachedCount} records, now {RecordCount}")]
        public static partial void BypassReplaced(this ILogger logger, string symbol, DateTime day, string schema, int cachedCount, int recordCount);

        [LoggerMessage(EventId = 1028, Level = LogLevel.Debug, Message = "{Symbol} {Day:yyyy-MM-dd} {Schema} is served as downloaded on a bypass and the cached day is kept: {Reason}")]
        public static partial void BypassNotReplaced(this ILogger logger, string symbol, DateTime day, string schema, string reason);

        [LoggerMessage(EventId = 1029, Level = LogLevel.Warning, Message = "Reading Databento's conditions for {Count} symbol-day(s) of {Schema} of {Dataset} failed, so they are served and none is saved or replaced")]
        public static partial void ConditionsReadFailed(this ILogger logger, int count, string schema, string dataset, Exception exception);

        [LoggerMessage(EventId = 1030, Level = LogLevel.Information, Message = "{Count} settled symbol-day(s) of {Schema} of {Dataset}, UTC days {FirstDay:yyyy-MM-dd} to {LastDay:yyyy-MM-dd}, are served only up to the available end {AvailableEndUtc:yyyy-MM-ddTHH:mm:ssZ} and not saved")]
        public static partial void SettledDaysPastAvailableEnd(this ILogger logger, int count, string schema, string dataset, DateTime firstDay, DateTime lastDay, DateTime availableEndUtc);

        // batch jobs

        [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "{Missing} final symbol-day(s) of {Schema} of {Dataset} are not cached; {AlreadyCached} are, and {LeftOut} day(s) not final are left out")]
        public static partial void BatchDaysMissing(this ILogger logger, int missing, string schema, string dataset, int alreadyCached, int leftOut);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Reusing batch job {JobId} for {Schema} of {Dataset}: it covers a query, so its files are downloaded again at no cost")]
        public static partial void BatchJobReused(this ILogger logger, string jobId, string schema, string dataset);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Priced {NewJobs} new batch job(s) at {EstimateUsd} USD, within the ceiling of {MaxCostUsd} USD")]
        public static partial void BatchJobsPriced(this ILogger logger, int newJobs, decimal estimateUsd, decimal maxCostUsd);

        [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "{NewJobs} new batch job(s) are priced at {EstimateUsd} USD, above the ceiling of {MaxCostUsd} USD, so none is submitted")]
        public static partial void BatchJobsTooExpensive(this ILogger logger, int newJobs, decimal estimateUsd, decimal maxCostUsd);

        [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Submitted batch job {JobId} for {Schema} of {Dataset}, {Symbols}, UTC days {Days}, priced at {EstimateUsd} USD")]
        private static partial void BatchJobSubmitted(ILogger logger, string jobId, string schema, string dataset, string symbols, string days, decimal estimateUsd);

        [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Batch job {JobId} is done; downloading its files ...")]
        public static partial void BatchJobDone(this ILogger logger, string jobId);

        [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Downloaded batch job {JobId}: {FileCount} file(s); {Saved} symbol-day(s) saved, {SavedEmpty} saved as empty, {AlreadyCached} already cached, {LeftOut} day(s) not final left out")]
        public static partial void BatchJobIngested(this ILogger logger, string jobId, int fileCount, int saved, int savedEmpty, int alreadyCached, int leftOut);

        [LoggerMessage(EventId = 2008, Level = LogLevel.Warning, Message = "Batch job {JobId} could not be saved; the other jobs go on, and a later call downloads it again at no cost while it lasts")]
        public static partial void BatchJobFailed(this ILogger logger, string jobId, Exception exception);

        [LoggerMessage(EventId = 2009, Level = LogLevel.Information, Message = "Ingested the batch folder {Path}: {Saved} symbol-day(s) saved, {SavedEmpty} saved as empty, {AlreadyCached} already cached, {LeftOut} day(s) not final left out")]
        public static partial void BatchFolderIngested(this ILogger logger, string path, int saved, int savedEmpty, int alreadyCached, int leftOut);

        [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "The symbology lookup of a batch folder's {SymbolCount} symbol(s) failed, so its symbol-days with no records are not saved: {Error}")]
        public static partial void BatchEmptyDaysLookupFailed(this ILogger logger, int symbolCount, string error);

        [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Every final symbol-day of {Schema} of {Dataset} asked for is cached, so no batch job is needed")]
        public static partial void BatchNothingMissing(this ILogger logger, string schema, string dataset);

        // the compactor

        [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Compacting {CacheRoot} in {CompactionMode} mode{DryRun}: {Groups:N0} group(s) with {DayFiles:N0} day file(s) planned ...")]
        private static partial void CompactionStarted(ILogger logger, string cacheRoot, CompactionMode compactionMode, string dryRun, int groups, long dayFiles);

        [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Compaction of {CacheRoot} done: {GroupsCompacted:N0} group(s) compacted, {FailedGroups:N0} failed, {DayFilesAbsorbed:N0} day file(s) absorbed, {DayFilesDeleted:N0} deleted, {FilesLeft:N0} file(s) left for the next run")]
        public static partial void CompactionFinished(this ILogger logger, string cacheRoot, int groupsCompacted, int failedGroups, long dayFilesAbsorbed, long dayFilesDeleted, int filesLeft);

        [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Compaction of {CacheRoot} canceled after {GroupsCompacted:N0} group(s) compacted")]
        public static partial void CompactionCanceled(this ILogger logger, string cacheRoot, int groupsCompacted);

        [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "Compacting {Directory} {Group}: {DayFiles:N0} day file(s) ...")]
        public static partial void GroupStarted(this ILogger logger, string directory, string group, int dayFiles);

        [LoggerMessage(EventId = 3005, Level = LogLevel.Debug, Message = "Wrote {Path}; {Checking}")]
        private static partial void ArchiveFileWritten(ILogger logger, string path, string checking);

        [LoggerMessage(EventId = 3006, Level = LogLevel.Debug, Message = "Verified {Path}, {Verification}")]
        public static partial void ArchiveFileVerified(this ILogger logger, string path, ArchiveVerification verification);

        [LoggerMessage(EventId = 3007, Level = LogLevel.Debug, Message = "Published {Path}: {SymbolDays:N0} symbol-day(s), {DayFilesAbsorbed:N0} day file(s) absorbed{DeltaFolded}")]
        private static partial void ArchiveFilePublished(ILogger logger, string path, int symbolDays, int dayFilesAbsorbed, string deltaFolded);

        [LoggerMessage(EventId = 3008, Level = LogLevel.Debug, Message = "Could not delete {Path} (attempt {Attempt} of {Attempts}), trying again in {Delay}: {Error}")]
        public static partial void DeleteRetried(this ILogger logger, string path, int attempt, int attempts, TimeSpan delay, string error);

        [LoggerMessage(EventId = 3009, Level = LogLevel.Warning, Message = "Left {Path} for the next run: it could not be deleted in {Attempts} attempt(s): {Error}")]
        public static partial void FileLeft(this ILogger logger, string path, int attempts, string error);

        [LoggerMessage(EventId = 3010, Level = LogLevel.Warning, Message = "The day file {Path} cannot be read and is left out of the archive, and on disk: {Error}")]
        public static partial void DayFileLeftOut(this ILogger logger, string path, string error);

        [LoggerMessage(EventId = 3011, Level = LogLevel.Information, Message = "Deleted the temporary file {Path} a stopped run left")]
        public static partial void TemporaryFileDeleted(this ILogger logger, string path);

        [LoggerMessage(EventId = 3012, Level = LogLevel.Warning, Message = "{Count} suspect day(s) in {Group}, archived all the same: {Suspects}")]
        private static partial void SuspectDaysFound(ILogger logger, int count, string group, string suspects);

        [LoggerMessage(EventId = 3013, Level = LogLevel.Error, Message = "Compacting {Group} failed, so it is left as it was: {Error}")]
        public static partial void GroupFailed(this ILogger logger, string group, string error, Exception exception);

        [LoggerMessage(EventId = 3015, Level = LogLevel.Warning, Message = "Kept {Path}: it is outside the current version and may hold a day's only copy")]
        public static partial void FileOrphaned(this ILogger logger, string path);

        [LoggerMessage(EventId = 3016, Level = LogLevel.Debug, Message = "Kept the day file {Path}: it changed during the run, so the next run absorbs it")]
        public static partial void DayFileChangedDuringRun(this ILogger logger, string path);

        [LoggerMessage(EventId = 3017, Level = LogLevel.Debug, Message = "Nothing to publish for {Directory} {Group}: no day file could be read or kept, so the version stays as it was")]
        public static partial void NothingToPublish(this ILogger logger, string directory, string group);

        [LoggerMessage(EventId = 3018, Level = LogLevel.Warning, Message = "Compacting {CacheRoot} refused: {Reason}")]
        public static partial void CompactionRefused(this ILogger logger, string cacheRoot, string reason);

        [LoggerMessage(EventId = 3019, Level = LogLevel.Error, Message = "Compacting {CacheRoot} failed; the groups not yet compacted are left as they were")]
        public static partial void CompactionFailed(this ILogger logger, string cacheRoot, Exception exception);

        [LoggerMessage(EventId = 3020, Level = LogLevel.Information, Message = "Listing {CacheRoot} to plan the compaction ...")]
        public static partial void CompactionPlanning(this ILogger logger, string cacheRoot);

        // the backup

        [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Backing up {CacheRoot} into {Store} ...")]
        private static partial void BackupStarted(ILogger logger, string cacheRoot, string store);

        [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Counted {DayFiles} day file(s) in {CacheRoot}")]
        public static partial void DayFilesCounted(this ILogger logger, int dayFiles, string cacheRoot);

        [LoggerMessage(EventId = 4003, Level = LogLevel.Warning, Message = "Deleted {Path}, a tar a stopped backup left in the staging folder")]
        public static partial void StagingTarDeleted(this ILogger logger, string path);

        [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Writing the backup tar {Path} ...")]
        public static partial void WritingTar(this ILogger logger, string path);

        [LoggerMessage(EventId = 4005, Level = LogLevel.Information, Message = "Wrote {FileCount} file(s), {TotalBytes} bytes, into the tar; reading the tar back to check it ...")]
        public static partial void TarWritten(this ILogger logger, int fileCount, long totalBytes);

        [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "Checked the tar, SHA-256 {Sha256}; putting {Name} into {Store} ...")]
        private static partial void StoringBackup(ILogger logger, string sha256, string name, string store);

        [LoggerMessage(EventId = 4007, Level = LogLevel.Information, Message = "Deleted the old backup {Name}, which the retention does not keep")]
        public static partial void OldBackupDeleted(this ILogger logger, string name);

        [LoggerMessage(EventId = 4008, Level = LogLevel.Warning, Message = "Could not list the backups in {Store}, so no old backup is deleted this run: {Error}")]
        private static partial void RetentionListFailed(ILogger logger, string store, string error);

        [LoggerMessage(EventId = 4009, Level = LogLevel.Warning, Message = "Could not delete the old backup {Name}; it is left for the next run: {Error}")]
        public static partial void RetentionDeleteFailed(this ILogger logger, string name, string error);

        [LoggerMessage(EventId = 4010, Level = LogLevel.Information, Message = "Backup {Name} is in {Store}: {FileCount} file(s), {TotalBytes} bytes, {DayFiles} day file(s); {OldBackupsDeleted} old backup(s) deleted")]
        private static partial void BackupStored(ILogger logger, string name, string store, int fileCount, long totalBytes, int dayFiles, int oldBackupsDeleted);

        [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "Backing up {CacheRoot} refused: {Reason}")]
        public static partial void BackupRefused(this ILogger logger, string cacheRoot, string reason);

        [LoggerMessage(EventId = 4012, Level = LogLevel.Error, Message = "Backing up {CacheRoot} failed; the store is left as it was")]
        public static partial void BackupFailed(this ILogger logger, string cacheRoot, Exception exception);

        [LoggerMessage(EventId = 4013, Level = LogLevel.Information, Message = "Backing up {CacheRoot} canceled; the store is left as it was")]
        public static partial void BackupCanceled(this ILogger logger, string cacheRoot);

        // the restore

        [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Restoring {CacheRoot} from backup {Name} in {Store}: existing files {ExistingFiles}, dry run {DryRun} ...")]
        private static partial void RestoreStarted(ILogger logger, string cacheRoot, string name, string store, ExistingCacheFiles existingFiles, bool dryRun);

        [LoggerMessage(EventId = 5002, Level = LogLevel.Warning, Message = "Deleted {Path}, which a restore that did not finish left in the staging folder")]
        public static partial void RestoreFolderDeleted(this ILogger logger, string path);

        [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Unpacking {Name} into {Directory} and checking it ...")]
        public static partial void Unpacking(this ILogger logger, string name, string directory);

        [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "Checked {Name}: {FileCount} file(s), {TotalBytes} bytes, each as its manifest lists, the tar's SHA-256 and its metadata.json")]
        public static partial void BackupChecked(this ILogger logger, string name, int fileCount, long totalBytes);

        [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "Swapping {Name} into {CacheRoot}, existing files {ExistingFiles} ...")]
        public static partial void SwappingIn(this ILogger logger, string name, string cacheRoot, ExistingCacheFiles existingFiles);

        [LoggerMessage(EventId = 5006, Level = LogLevel.Information, Message = "Restored {CacheRoot} from {Name}: {FileCount} file(s), {FilesDeleted} cache file(s) deleted, {DayFilesKept} day file(s) kept, {FilesMovedAside} moved aside")]
        public static partial void Restored(this ILogger logger, string cacheRoot, string name, int fileCount, int filesDeleted, int dayFilesKept, int filesMovedAside);

        [LoggerMessage(EventId = 5007, Level = LogLevel.Information, Message = "Dry run of {Name} into {Folder}: a restore would delete {FilesDeleted} cache file(s), keep {DayFilesKept} day file(s) and move {FilesMovedAside} aside")]
        public static partial void DryRunDone(this ILogger logger, string name, string folder, int filesDeleted, int dayFilesKept, int filesMovedAside);

        [LoggerMessage(EventId = 5008, Level = LogLevel.Warning, Message = "Restoring {CacheRoot} from {Name} refused: {Reason}")]
        public static partial void RestoreRefused(this ILogger logger, string cacheRoot, string name, string reason);

        [LoggerMessage(EventId = 5009, Level = LogLevel.Error, Message = "Restoring {CacheRoot} from {Name} failed")]
        public static partial void RestoreFailed(this ILogger logger, string cacheRoot, string name, Exception exception);

        [LoggerMessage(EventId = 5010, Level = LogLevel.Information, Message = "Deleted the earlier dry run {Directory}")]
        public static partial void EarlierDryRunDeleted(this ILogger logger, string directory);

        [LoggerMessage(EventId = 5011, Level = LogLevel.Information, Message = "Restoring {CacheRoot} from {Name} canceled")]
        public static partial void RestoreCanceled(this ILogger logger, string cacheRoot, string name);

        /// <summary>
        /// Logs the start of a time-series request
        /// </summary>
        public static void TimeseriesRequested(this ILogger logger, string schema, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            if(logger.IsEnabled(LogLevel.Debug))
            {
                TimeseriesRequested(logger, schema, dataset, DescribeSymbols(symbols), startUtc.UtcDateTime, endUtc.UtcDateTime, cacheMode);
            }
        }

        /// <summary>
        /// Logs a download request about to be sent to Databento: its UTC days, inclusive, and its window, whose end is exclusive as Databento's is
        /// </summary>
        public static void Requesting(this ILogger logger, string schema, string dataset, IReadOnlyList<string> symbols, DateTime firstDay, DateTime lastDay, DateTime startUtc, DateTime endUtc)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                string days = firstDay == lastDay ? Invariant($"UTC day {firstDay:yyyy-MM-dd}") : Invariant($"UTC days {firstDay:yyyy-MM-dd} to {lastDay:yyyy-MM-dd}");
                Requesting(logger, schema, dataset, DescribeSymbols(symbols), days, Invariant($"[{startUtc:yyyy-MM-ddTHH:mm:ssZ} to {endUtc:yyyy-MM-ddTHH:mm:ssZ})"));
            }
        }

        /// <summary>
        /// Logs a download that failed
        /// </summary>
        public static void DownloadFailed(this ILogger logger, string schema, string dataset, IReadOnlyList<string> symbols, TimeSpan elapsed, Exception exception)
        {
            if(logger.IsEnabled(LogLevel.Warning))
            {
                DownloadFailed(logger, schema, dataset, DescribeSymbols(symbols), elapsed, exception);
            }
        }

        /// <summary>
        /// Logs a new archive file written, and how it is checked before it is published
        /// </summary>
        public static void ArchiveFileWritten(this ILogger logger, string path, ArchiveVerification verification)
        {
            if(logger.IsEnabled(LogLevel.Debug))
            {
                ArchiveFileWritten(logger, path, verification switch
                {
                    ArchiveVerification.EveryRecord => "checking it record by record against its sources",
                    ArchiveVerification.CountsOnly => "checking its index and symbol-days against its sources",
                    _ => "not checked: Verification is None",
                });
            }
        }

        /// <summary>
        /// Logs the start of a compaction, once its groups are counted
        /// </summary>
        public static void CompactionStarted(this ILogger logger, string cacheRoot, CompactionMode compactionMode, bool dryRun, int groups, long dayFiles)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                CompactionStarted(logger, cacheRoot, compactionMode, dryRun ? ", as a dry run" : string.Empty, groups, dayFiles);
            }
        }

        /// <summary>
        /// Logs an archive file published, and whether the month's delta was folded into it
        /// </summary>
        public static void ArchiveFilePublished(this ILogger logger, string path, int symbolDays, int dayFilesAbsorbed, bool deltaFolded)
        {
            if(logger.IsEnabled(LogLevel.Debug))
            {
                ArchiveFilePublished(logger, path, symbolDays, dayFilesAbsorbed, deltaFolded ? ", the delta folded in" : string.Empty);
            }
        }

        /// <summary>
        /// Logs the start of a backup
        /// </summary>
        public static void BackupStarted(this ILogger logger, string cacheRoot, IBackupStore store)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                BackupStarted(logger, cacheRoot, DescribeStore(store));
            }
        }

        /// <summary>
        /// Logs a checked tar about to be put into the store
        /// </summary>
        public static void StoringBackup(this ILogger logger, string sha256, string name, IBackupStore store)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                StoringBackup(logger, sha256, name, DescribeStore(store));
            }
        }

        /// <summary>
        /// Logs a store whose backups could not be listed for the retention
        /// </summary>
        public static void RetentionListFailed(this ILogger logger, IBackupStore store, string error)
        {
            if(logger.IsEnabled(LogLevel.Warning))
            {
                RetentionListFailed(logger, DescribeStore(store), error);
            }
        }

        /// <summary>
        /// Logs a backup in the store
        /// </summary>
        public static void BackupStored(this ILogger logger, string name, IBackupStore store, int fileCount, long totalBytes, int dayFiles, int oldBackupsDeleted)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                BackupStored(logger, name, DescribeStore(store), fileCount, totalBytes, dayFiles, oldBackupsDeleted);
            }
        }

        /// <summary>
        /// Logs the start of a restore
        /// </summary>
        public static void RestoreStarted(this ILogger logger, string cacheRoot, string name, IBackupStore store, ExistingCacheFiles existingFiles, bool dryRun)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                RestoreStarted(logger, cacheRoot, name, DescribeStore(store), existingFiles, dryRun);
            }
        }

        /// <summary>
        /// Logs a span of days served empty because none of it is available yet
        /// </summary>
        public static void NotAvailableYet(this ILogger logger, string schema, string dataset, IReadOnlyList<string> symbols, DateTime firstDay, DateTime availableEndUtc)
        {
            if(logger.IsEnabled(LogLevel.Debug))
            {
                NotAvailableYet(logger, schema, dataset, DescribeSymbols(symbols), firstDay, availableEndUtc);
            }
        }

        /// <summary>
        /// Logs a batch job submitted
        /// </summary>
        public static void BatchJobSubmitted(this ILogger logger, string jobId, string schema, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal estimateUsd)
        {
            if(logger.IsEnabled(LogLevel.Information))
            {
                BatchJobSubmitted(logger, jobId, schema, dataset, DescribeSymbols(symbols), Invariant($"{startUtc.UtcDateTime:yyyy-MM-dd} to {endUtc.UtcDateTime.AddTicks(-1):yyyy-MM-dd}"), estimateUsd);
            }
        }

        /// <summary>
        /// Logs the suspect days a group's checks found, naming the first few day files
        /// </summary>
        public static void SuspectDaysFound(this ILogger logger, string directory, string group, IReadOnlyList<SuspectDayFile> suspects)
        {
            if(suspects.Count > 0 && logger.IsEnabled(LogLevel.Warning))
            {
                string named = string.Join(", ", suspects.Take(SuspectsNamed).Select(suspect => Invariant($"{suspect.RelativePath} ({suspect.Reason})")));
                SuspectDaysFound(logger, suspects.Count, Invariant($"{directory} {group}"), suspects.Count > SuspectsNamed ? named + Invariant($" and {suspects.Count - SuspectsNamed} more") : named);
            }
        }

        /// <summary>
        /// Gets a store's location, or its type when the location cannot be read, so logging never fails a backup or restore the store
        /// would serve
        /// </summary>
        private static string DescribeStore(IBackupStore store)
        {
            try
            {
                return store.Location ?? store.GetType().Name;
            }
            catch(Exception)
            {
                return store.GetType().Name;
            }
        }

        /// <summary>
        /// Gets the symbol when there is one, else how many there are
        /// </summary>
        private static string DescribeSymbols(IReadOnlyList<string> symbols)
        {
            return symbols.Count == 1 ? symbols[0] : Invariant($"{symbols.Count} symbols");
        }
    }
}
