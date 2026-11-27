# Databento.CSharpApiClient.Caching

[![NuGet](https://img.shields.io/nuget/v/Databento.CSharpApiClient.Caching.svg)](https://www.nuget.org/packages/Databento.CSharpApiClient.Caching)
[![CI](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/blob/main/LICENSE)

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
    Databento = new DatabentoOptions { ApiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY") },
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
- **Days.** A record's day is the UTC date of the timestamp Databento filters a range on: `ts_recv` where the schema has one, else `ts_event`. A downloaded day with records is saved once its UTC day has ended plus `SettleDelay` (15 minutes by default, at most 24 hours); a newer day is served but not saved. A symbol's download covers its first to its last missing day in one request, so ask for runs of days rather than scattered ones.
- **No metadata calls before a download.** A download goes straight to Databento, with no dataset range or conditions read before it, and one that answers records for every day it asks for reads neither after it, except a Bypass over cached days (a symbology refusal, or an empty day under `CacheWhenNotListed`, still makes the free symbology lookup). The cache asks for whole UTC days, so a request for today ends at tomorrow 00:00, past the available end, and Databento refuses it (`DataEndAfterAvailableEnd`, or `DataStartAfterAvailableEnd` for a request starting at or past the end). The cache then reads the dataset range once, cuts the request at that schema's end and sends it once more; if that is refused too, or the range cannot be read, the refusal propagates. The end is remembered, and until the next line of the `AvailableEndRefreshInterval` grid on the UTC clock (15 minutes by default, dividing a day evenly: an end read at 00:20 is used until 00:30) every request past it is cut before it is sent; after that the next request goes uncut. The days a cut leaves out are, by default (`AvailableEndRefusal.ServeEmpty`), served up to the end, empty past it, and not saved (a settled day among them is logged at Information, so a backtest sees it); with `AvailableEndRefusal.Throw` the call throws the client's `DatabentoHttpException` once the days before the end are saved (for a request cut at a remembered end, a 422 of the same case naming that end). Every other refusal (a schema not fully available, no license, ...) throws each time, and nothing of the refused request is saved. `CheckAvailabilityFirst = true` reads the dataset range and the conditions of the days not yet known final before each download instead: the request is cut at the available end, days past it are served empty, and only days whose condition is `available`, or with no condition listed, are saved.
- **Condition guard on the suspect saves.** Databento serves only the days it has published, so a day with records is saved whatever its condition. The two saves an outage could make wrong are guarded: an empty day is saved (under any `EmptyDayPolicy`, and as a `BypassMissAction.Replace`) and a `CacheMode.Bypass` download replaces a cached day only when Databento's condition for the day is `available` (or it lists none for the day but does for a later one, as for a weekend). Otherwise the day is served as downloaded and not saved, and a cached file is kept. The conditions are read once per download request, only for the days about to be saved this way, and a day found `available` is never asked about again; a failed read saves none of them and is logged, never thrown.
- **Empty days.** By default (`EmptyDayPolicy.CacheWhenNotListed`) a day with no records is saved as empty only when Databento's free symbology lookup confirms the symbol was not listed that day, and its condition is `available`. An empty answer for a listed symbol, such as a weekend, is served but not saved.
- **Cache only.** `CacheMode.CacheOnly` serves what the cache holds and never asks Databento anything: when a symbol-day is missing, the call throws `NotCachedException` naming them. Use it to work from a restored backup at no cost, or while Databento should not be reached.
- **Downloading again.** `CacheMode.Bypass` downloads the days again, serves the download, and replaces a cached day once it is settled and its condition is `available`, whatever the record counts (so a cached day holding duplicates is corrected); `BypassMissAction` says what happens to a cached day Databento now answers with no records.
- **Transport.** `DatabentoTransport.Json` (the default) or `Dbn`. The cache saves the same JSON records either way, so you can switch at any time.
- **Interfaces.** `CachedDatabentoClient` is an `ICachedDatabentoClient`, whose calls take a `CacheMode`, and an `IDatabentoTimeseriesClient`, the Databento client's own interface, whose calls read the cache (`CacheMode.Use`). Code that takes an `IDatabentoTimeseriesClient` takes the cache or either Databento client, chosen by configuration; code that needs `Bypass` or batch jobs takes an `ICachedDatabentoClient`, and tests can hand either a fake. An implementation of either writes only the 11 async calls for several symbols, with a `CacheMode` for `ICachedDatabentoClient`: the client's own calls are bridged to those with `Use`, and extension methods add the one-symbol and synchronous forms. Two differences from the Databento clients: the cache refuses `ALL_SYMBOLS` (it needs the raw symbols), and its own methods name the cancellation token `ct` where the interfaces say `cancellationToken`.
- **Unreadable archive files** raise `ArchiveFileUnreadable`, for the host to alarm on. Meanwhile the days only that file holds are downloaded again, and the file is left in place for you to restore or remove.

### Choosing the options

The defaults suit a backtest or research host. Change one when its row describes you:

| Option | Default | Change it when |
|---|---|---|
| `SettleDelay` | 15 minutes | Your license serves a UTC day right after midnight and you want more margin for Databento to finalize it before it is saved for good (up to 24 hours). A day with records is saved without a condition check, so this delay is what keeps a just-ended day from being locked in incomplete. |
| `AvailableEndRefusal` | `ServeEmpty` | Your code must tell "not published yet" from "no data": `Throw` makes a range past the available end throw instead of coming back empty. With `ServeEmpty`, watch the Information log, which names any settled day served empty. |
| `AvailableEndRefreshInterval` | 15 minutes | You need a newly published day sooner after Databento publishes it (shorter), or want fewer range reads (longer). It must divide a day evenly. |
| `CheckAvailabilityFirst` | `false` | You prefer a metadata read before every download over an occasional refused request, or want only `available` days saved. It costs up to two metadata calls per download, which a burst of downloads can find rate-limited. |
| `EmptyDays` | `CacheWhenNotListed` | `CacheAlways` saves every settled empty day whose condition is `available`, so weekends and quiet contracts are never asked for again; `Never` saves no empty day. |
| `MaxSymbolsPerRequest` | 2,000 | You want smaller requests, for example to spread a large download over more, shorter calls. |
| `Transport` | `Json` | You prefer DBN on the wire; the cache saves the same JSON records either way. |

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

**Run the compactor with Server GC.** With several groups compacted at once (`MaxParallelGroups`) the compactor allocates fast, and the default Workstation GC stops every thread for each collection, so more groups can end up slower, not faster. Set `<ServerGarbageCollection>true</ServerGarbageCollection>` in the host's project file and check that its `.runtimeconfig.json` has `"System.GC.Server": true`; a misspelled property name is ignored without a warning. Compacting about 4 million day files with 16 groups at once, Server GC took the estimate from about 15 hours to 37 minutes and CPU use from about 20% to 64%, with memory staying under 2.5 GB.

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

A restore unpacks the backup into the staging folder and checks every file and the tar's hash before it touches the cache. Then it clears the cache as `ExistingFiles` says and moves the backup's files in. By default (`Keep`) it deletes the archive files and keeps the cache's own day files, since they are served first and may be downloads made after the backup. For a cache whose files can't be trusted, such as ones a faulty release wrote, `MoveAside` moves everything into `<root>.bak_<yyyyMMddTHHmmss>` beside it and `Delete` deletes everything; either way the backup lands in a clean root, and day files downloaded since the backup are bought again when next asked for. While it runs, `RESTORE_IN_PROGRESS` in the cache root keeps new cached clients, compactions and backups from starting. Shut down the application that uses the cache before a restore: a running client is not seen, and it holds archive files open, which keeps the restore from moving folders on Windows. The restore refuses with an `InvalidOperationException`, before it touches anything, while an `ICacheInUseChecker` reports the application running, or while any archive file is open (it names them), as a running client keeps those it reads. That second check is best effort: it works on Windows, which refuses an exclusive open of a file another process holds, but not on Linux, which does not lock files, so there shut the application down or give the restore an `ICacheInUseChecker`. `DryRun` unpacks and checks a backup into `DryRunFolder` without touching the cache.

Backups go wherever an `IBackupStore` keeps them; `LocalDirectoryBackupStore` is a local or synced folder, holding each backup's tar beside its hash in `sha256sum`'s format, so `sha256sum -c 20261119T021530.tar.sha256` checks one without the package.

## Logging

The client's, the compactor's, the backup's and the restore's options each take a `Microsoft.Extensions.Logging` `ILogger`, so the package's decisions show in the host's own logs. Without one (the default, and `null`) nothing is logged; a line below the logger's level is never formatted.

```csharp
ILogger logger = loggerFactory.CreateLogger("Databento.Cache");

using CachedDatabentoClient client = new CachedDatabentoClient(new CachedDatabentoClientOptions
{
    Databento = new DatabentoOptions { ApiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY") },
    CacheRoot = @"D:\MarketData\Databento",
    Logger = logger,
});
```

- **Information:** each download request to Databento (schema, dataset, the symbol or how many, the UTC days and window) and what came back (records, how many of the symbols had any, how long it took); a cached day a bypass replaced, with the cached and the new record counts; settled days served only up to the available end; batch jobs reused, priced, submitted, done and saved; a compaction's start and end, and the temporary files a stopped run left that it deleted; each step of a backup and a restore; a compaction, backup or restore the caller canceled.
- **Debug:** each request served (symbols, range, cache mode); each symbol-day served from its day file or the archive (the file, the record count) or not cached; each save, a day file kept, a day served but not saved (not final, and why), empty days saved or not and why; a cached day a bypass kept, and why; an available end read after a refusal past it, and when it is read again; a wait for another request downloading the same days; each compacted group's files written, verified and published, or nothing to publish; a compaction delete tried again.
- **Warning:** a download from Databento that failed (with the exception), a symbology refusal and the request sent again with the listed symbols, or a refusal that cannot be resolved; a failed dataset range read for the available end; a failed condition read for the days about to be saved empty or replaced; a failed symbology lookup for empty days; a day file that cannot be parsed (deleted, or not deleted) or read; a batch job that could not be saved; a compaction refused (a restore in progress, `cache.lock` held, no `metadata.json`), a compaction file left for the next run, an orphan archive file kept, an unreadable day file, suspect days; a backup or restore refused, left staging files deleted, old backups that cannot be deleted.
- **Error:** an unreadable archive file (also raised as `ArchiveFileUnreadable`), a compacted group that failed or did not verify, a compaction, backup or restore that failed (with the exception).

Event ids are stable: 1000s the cached client, 2000s its batch jobs, 3000s the compactor, 4000s the backup, 5000s the restore. Counts, symbols, days and paths are logged, never prices or sizes. The rendered message formats days, times and amounts with the invariant culture; structured sinks get the raw values.

**A logger's methods shall not throw** (`Log`, `IsEnabled`, `BeginScope`). The package nevertheless catches and drops anything a logger throws, so a failing logger loses its lines but never changes what the cache serves or writes. A logger is called from several threads at once (concurrent client requests, parallel compaction groups), so it must be thread-safe, as the `Microsoft.Extensions.Logging` loggers are.

## License

MIT
