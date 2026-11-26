# Databento.CSharpApiClient.Caching

[![NuGet](https://img.shields.io/nuget/v/Databento.CSharpApiClient.Caching.svg)](https://www.nuget.org/packages/Databento.CSharpApiClient.Caching)

A disk cache and Parquet archive for [Databento.CSharpApiClient](https://github.com/mrut2pac/Databento.CSharpApiClient), so each day of Databento data is paid for once.

- **Cached client:** the client's time-series calls, served per symbol and UTC day from the cache, and downloaded only for the days it lacks.
- **Batch jobs:** ranges too large for the time-series calls, priced first and kept under a cost cap.
- **Archive:** a compactor folds the day files into Parquet that DuckDB, pandas or any Parquet reader can query.
- **Backup and restore:** the whole cache as one checked tar.

See [CHANGELOG.md](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/blob/main/CHANGELOG.md) for the version history.

## Installation

```
dotnet add package Databento.CSharpApiClient.Caching
```

It brings in `Databento.CSharpApiClient`, Parquet.Net and `Microsoft.Extensions.Logging.Abstractions`, and targets .NET 8.

## The cached client

```csharp
using Databento.CSharpApiClient;
using Databento.CSharpApiClient.Caching;
using Databento.CSharpApiClient.DataModel;

using CachedDatabentoClient client = new CachedDatabentoClient(new CachedDatabentoClientOptions
{
    Databento = new DatabentoOptions { ApiKey = "YOUR_API_KEY" },
    CacheRoot = @"D:\MarketData\Databento",
});

ICbboRecord[] quotes = await client.GetCbbo1mAsync(
    Datasets.OpraPillar,
    "SPXW  261016C06600000",
    new DateTimeOffset(2026, 10, 15, 13, 30, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 10, 15, 20, 15, 0, TimeSpan.Zero));
```

The first call downloads the whole UTC day of 2026-10-15 for that symbol, saves it and returns the records in the range. Any later call that touches that day, from this process or another one on the same cache root, reads it from disk.

- **Calls:** cbbo-1s, cbbo-1m, bbo-1s, bbo-1m, tcbbo, trades, ohlcv-1s, ohlcv-1m, ohlcv-1h, ohlcv-1d and statistics, async and sync, for one symbol or several. They take the client's arguments plus a `CacheMode`, and return the client's record interfaces (`ICbboRecord`, `ITradeRecord`, ...) in time order. For several symbols, only the symbol-days the cache lacks are downloaded, at most 2,000 symbols a download request (`MaxSymbolsPerRequest`).
- **Days.** A record's day is the UTC date of the timestamp Databento filters a range on: `ts_recv` where the schema has one, else `ts_event`. A day is saved once Databento says it is final; until then it is served but downloaded again next time. A symbol's download covers its first to its last missing day, so ask for runs of days rather than scattered ones.
- **Empty days.** By default (`EmptyDayPolicy.CacheWhenNotListed`) a day with no records is saved as empty only when Databento's free symbology lookup confirms the symbol was not listed that day. An empty answer for a listed symbol, such as a weekend, is served but not saved.
- **Cache only.** `CacheMode.CacheOnly` serves what the cache holds and never asks Databento anything: when a symbol-day is missing, the call throws `NotCachedException` naming them. Use it to work from a restored backup at no cost, or while Databento should not be reached.
- **Downloading again.** `CacheMode.Bypass` downloads the days again and replaces their day files; `BypassMissAction` says what happens to a cached day Databento now answers with no records.
- **Transport.** `DatabentoTransport.Json` (the default) or `Dbn`. The cache saves the same JSON records either way, so you can switch at any time.
- **Interfaces.** `CachedDatabentoClient` is an `ICachedDatabentoClient`, whose calls take a `CacheMode`, and an `IDatabentoTimeseriesClient`, the Databento client's own interface, whose calls read the cache (`CacheMode.Use`). Code that takes an `IDatabentoTimeseriesClient` takes the cache or either Databento client, chosen by configuration; code that needs `Bypass` or batch jobs takes an `ICachedDatabentoClient`, and tests can hand either a fake. An implementation of either writes only the 11 async calls for several symbols, with a `CacheMode` for `ICachedDatabentoClient`: the client's own calls are bridged to those with `Use`, and extension methods add the one-symbol and synchronous forms. Two differences from the Databento clients: the cache refuses `ALL_SYMBOLS` (it needs the raw symbols), and its own methods name the cancellation token `ct` where the interfaces say `cancellationToken`.
- **Unreadable archive files** raise `ArchiveFileUnreadable`, for the host to alarm on. Meanwhile the days only that file holds are downloaded again, and the file is left in place for you to restore or remove.

## Batch jobs

```csharp
string[] symbols = { "SPXW  261016C06600000", "SPXW  261016P06600000" };

BatchReport batch = await client.RunBatchJobAsync(
    Datasets.OpraPillar,
    "cbbo-1m",
    symbols,
    new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
    maxCostUsd: 5m);
```

For ranges too large for the time-series calls: it saves the final symbol-days the cache lacks through Databento batch jobs and returns once they are saved, and the time-series calls then serve them. Databento prices the jobs first; past `maxCostUsd` it throws `BatchJobTooExpensiveException` and submits nothing. A job submitted before that covers the range, and is not expired, is downloaded again at no cost. `IngestBatchFolderAsync` saves a batch download folder you already have, made by any tool.

## The cache

```
<cache root>\
  metadata.json                                       format version and day cutoff (UTC)
  OPRA.PILLAR\cbbo-1m\2026\10\
    SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz  day file: one symbol, one UTC day
    OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.full.parquet
    OPRA.PILLAR_cbbo-1m_SPXW.OPT_2026-10.UTC_20261118T021500.delta.3.parquet
```

- **Day files.** Each holds one symbol's records for one UTC day as Databento's JSON records, gzip-compressed. An empty array means the day was asked for and holds no data.
- **Archive files.** The compactor folds day files into Parquet, one archive per dataset, schema, group and month: a full file and at most one delta. The group is the parent symbol for OPRA options (`SPXW.OPT`) and CME futures (`MNQ.FUT`), and the symbol itself otherwise.
- **Reading the archive.** The Parquet files have one column per record field, plus `raw_symbol` and `date`, so DuckDB, pandas or any Parquet reader can query them directly.

## Compaction

```csharp
DatabentoArchiveCompactor compactor = new DatabentoArchiveCompactor(new ArchiveCompactorOptions
{
    CacheRoot = @"D:\MarketData\Databento",
    CompactionMode = CompactionMode.Auto,   // a new delta generation, or a rebuild past the thresholds
});

ArchiveCompactionReport report = await compactor.CompactAsync(cancellationToken);
```

Each new file is written under a temporary name, checked against its sources (record by record, by default) and published by a rename. Only then are the files it replaces and its day files deleted, so a reader always finds the previous version or the new one. A run holds `cache.lock` in the cache root, so two runs never overlap.

The options also choose how empty days are kept, how new files are verified, and how old a day must be before it is archived. Optional `IArchiveDayCheck`s flag suspect days in the run's report.

## Backup and restore

```csharp
IBackupStore store = new LocalDirectoryBackupStore(@"E:\Backups\Databento");

CacheBackupReport backup = await new DatabentoCacheBackup(new CacheBackupOptions
{
    CacheRoot = @"D:\MarketData\Databento",
    Store = store,
    StagingFolder = @"E:\Backups\Staging",
    Retention = BackupRetention.KeepLast(5),     // or BackupRetention.Calendar(12): this month's, then one a month
}).BackUpAsync(cancellationToken: cancellationToken);

CacheRestoreReport restore = await new DatabentoCacheRestore(new CacheRestoreOptions
{
    CacheRoot = @"D:\MarketData\Databento",
    Store = store,
    StagingFolder = @"D:\Restore",
    InUseChecker = new MyServiceIsRunningChecker(),   // your ICacheInUseChecker, or null
}).RestoreAsync(backup.Name, cancellationToken);
```

A backup is one uncompressed tar of the whole cache root with a manifest of every file's size and SHA-256, read back before it goes into the store. A cache with more day files than `MaxBackupDayFiles` (10,000) is refused unless forced: compact it first.

A restore unpacks the backup into the staging folder and checks every file and the tar's hash before it touches the cache. Then it clears the cache as `ExistingFiles` says and moves the backup's files in. By default (`Keep`) it deletes the archive files and keeps the cache's own day files, since they are served first and may be downloads made after the backup. For a cache whose files can't be trusted, such as ones a faulty release wrote, `MoveAside` moves everything into `<root>.bak_<yyyyMMddTHHmmss>` beside it and `Delete` deletes everything; either way the backup lands in a clean root, and day files downloaded since the backup are bought again when next asked for. While it runs, `RESTORE_IN_PROGRESS` in the cache root keeps new cached clients, compactions and backups from starting. A client that is already running is not seen: stop it first, or give the restore an `ICacheInUseChecker` that reports it, and the restore refuses with an `InvalidOperationException` while it says the cache is in use. `DryRun` unpacks and checks a backup into `DryRunFolder` without touching the cache.

Backups go wherever an `IBackupStore` keeps them; `LocalDirectoryBackupStore` is a local or synced folder, holding each backup's tar beside its hash in `sha256sum`'s format, so `sha256sum -c 20261119T021530.tar.sha256` checks one without the package.

## Logging

The client's, the compactor's, the backup's and the restore's options each take a `Microsoft.Extensions.Logging` `ILogger`, so the package's decisions show in the host's own logs. Without one (the default, and `null`) nothing is logged; a line below the logger's level is never formatted.

```csharp
ILogger logger = loggerFactory.CreateLogger("Databento.Cache");

using CachedDatabentoClient client = new CachedDatabentoClient(new CachedDatabentoClientOptions
{
    Databento = new DatabentoOptions { ApiKey = "YOUR_API_KEY" },
    CacheRoot = @"D:\MarketData\Databento",
    Logger = logger,
});
```

- **Information:** each download request to Databento (schema, dataset, the symbol or how many, the UTC days and window) and what came back (records, how many of the symbols had any, how long it took); batch jobs reused, priced, submitted, done and saved; a compaction's start and end, and the temporary files a stopped run left that it deleted; each step of a backup and a restore; a compaction, backup or restore the caller canceled.
- **Debug:** each request served (symbols, range, cache mode); each symbol-day served from its day file or the archive (the file, the record count) or not cached; each save, a day file kept, a day served but not saved (not final), empty days saved or not and why; a wait for another request downloading the same days; each compacted group's files written, verified and published, or nothing to publish; a compaction delete tried again.
- **Warning:** a download from Databento that failed (with the exception), a symbology refusal and the request sent again with the listed symbols, or a refusal that cannot be resolved; a failed symbology lookup for empty days; a day file that cannot be parsed (deleted, or not deleted) or read; a batch job that could not be saved; a compaction refused (a restore in progress, `cache.lock` held, no `metadata.json`), a compaction file left for the next run, an orphan archive file kept, an unreadable day file, suspect days; a backup or restore refused, left staging files deleted, old backups that cannot be deleted.
- **Error:** an unreadable archive file (also raised as `ArchiveFileUnreadable`), a compacted group that failed or did not verify, a compaction, backup or restore that failed (with the exception).

Event ids are stable: 1000s the cached client, 2000s its batch jobs, 3000s the compactor, 4000s the backup, 5000s the restore. Counts, symbols, days and paths are logged, never prices or sizes. The rendered message formats days, times and amounts with the invariant culture; structured sinks get the raw values.

**A logger's methods shall not throw** (`Log`, `IsEnabled`, `BeginScope`). The package nevertheless catches and drops anything a logger throws, so a failing logger loses its lines but never changes what the cache serves or writes. A logger is called from several threads at once (concurrent client requests, parallel compaction groups), so it must be thread-safe, as the `Microsoft.Extensions.Logging` loggers are.

## License

MIT
