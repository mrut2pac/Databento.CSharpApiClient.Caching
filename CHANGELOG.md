# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `SettleDelay` on `CachedDatabentoClientOptions` (default 15 minutes, zero to 24 hours inclusive, anything else refused with an `ArgumentException`): how long after its UTC day ends a downloaded day is first saved.
- `CheckAvailabilityFirst` on `CachedDatabentoClientOptions` (default `false`): when `true`, each time-series download reads the dataset range and the conditions of the days not yet known final first, as 1.4.1 did: the request is cut at the available end, days past it are served empty, and only days whose condition is `available`, or with no condition listed, are saved (once settled, by `SettleDelay`).
- `AvailableEndRefreshInterval` on `CachedDatabentoClientOptions` (default 15 minutes; above zero and dividing a day evenly, anything else refused with an `ArgumentException`): how long an available end read after a refusal is used to cut requests before they are sent, up to the next multiple of the interval on the UTC clock (read at 00:20, used until 00:30). Debug line 1025 tells the end read and when it is due.
- `AvailableEndRefusal` on `CachedDatabentoClientOptions`, a new enum (default `ServeEmpty`): what a call does with the days past the available end it cut a request at. `ServeEmpty` serves them up to the end, empty past it, and does not save them, the call returning normally; a settled day among them is logged at Information (1030), so a backtest sees it. `Throw` throws the `DatabentoHttpException` once the days before the end are saved: Databento's refusal, or for a request cut at a remembered end before it was sent, a 422 of the same case naming that end.
- A condition guard on the two saves an outage could make wrong: an empty settled day (under any `EmptyDayPolicy`, and a bypass with `BypassMissAction.Replace`) is saved, and a `CacheMode.Bypass` download replaces a cached day file, only when Databento's condition for the day is `available`, or it lists none for the day but does for a later one (a weekend; a trailing day with none is not saved, and asked about again next time). Otherwise the download is served, nothing saved and a cached file kept (Debug lines 1018 and 1028 say why). The conditions are read once per download request, only when such a save is to be made, and a day found `available` is not asked about again; a failed read saves none of them (Warning 1029) and never throws. A replacement logs Information line 1027 with the cached and the new record counts.

### Changed
- A time-series download goes straight to Databento with no metadata call before it (no `get_dataset_range`, no `list_conditions`), which under a burst of downloads were rate-limited and held a download up for about a minute. A download that answers records for days not cached reads no range or conditions at all.
- A downloaded day with records is saved once its UTC day has ended plus `SettleDelay`, by the options' `TimeProvider`, and it ends by the available end the request was cut at, whatever Databento's condition for it; a newer day is served and not saved. A `degraded` day with records is now saved like any other, and a later `CacheMode.Bypass` download replaces it once the day is `available`.
- A request Databento refuses as past the available end (`DataEndAfterAvailableEnd`, or `DataStartAfterAvailableEnd`; a request for today ends at tomorrow 00:00, so it is refused) makes the cache read the dataset range once, remember that schema's end, cut the request at it and send it once more (not at all when the cut request is empty). If that is refused too, or the range read fails (Warning 1026), the refusal propagates whatever `AvailableEndRefusal` says; a call cancelled during the read throws `OperationCanceledException`. Every other refusal (a schema not fully available, no license) propagates as the client throws it, on the JSON transport as on DBN, and nothing of the refused request is saved.
- The debug line for a day served but not saved (1016) says why: its UTC day has not ended `SettleDelay` ago, ends past the available end, or Databento has not found it final. Its structured property `AvailableEndUtc` is now `Reason`.
- Depends on `Databento.CSharpApiClient` 2.8.0 (it was 2.7.2), whose JSON transport throws a `DatabentoHttpException` (422, `DataEndAfterAvailableEnd`) for a time-series request ending past the available end, as the DBN transport does, instead of answering it with no records.
- The compactor logs an Information line (3020) as it starts listing the cache, before the groups are counted. Its first line reads "in Auto mode" and says ", as a dry run" only on one, instead of "dry run False"; the published line (3007) says ", the delta folded in" only when it was, instead of "delta folded False". Their structured properties `DryRun` (3001) and `DeltaFolded` (3007) are now strings (the text added to the message, empty when not), no longer booleans. The compactor's counts carry thousands separators.

## [1.4.1] - 2026-11-26

### Changed
- Log messages end without a period, which read as part of a path or a name before it; a step under way ends with ` ...` instead (`Writing the backup tar <path> ...`).

## [1.4.0] - 2026-11-26

### Added
- Diagnostic logging through `Microsoft.Extensions.Logging`: a `Logger` (`ILogger`, default `NullLogger.Instance`, which `null` also stands for) on `CachedDatabentoClientOptions`, `ArchiveCompactorOptions`, `CacheBackupOptions` and `CacheRestoreOptions`, so the host sees the package's decisions in its own logs.
  - Information: each download request to Databento and what it brought back (records, symbols with records, time taken), batch jobs reused, priced, submitted, done and saved, a compaction's start and end, and each step of a backup and a restore.
  - Debug: each symbol-day served from its day file or the archive or not cached, each save and why a day was or was not saved, and each step of a compacted group.
  - Warning: failed downloads, symbology refusals and their retry, failed lookups, day files that cannot be read, files left for the next compaction, orphan archive files, suspect days, refusals of a compaction, backup or restore. Error: an unreadable archive file, a failed or unverified group, a failed compaction, backup or restore.
  - Written with the `[LoggerMessage]` source generator, with stable event ids (1000s client, 2000s batch jobs, 3000s compactor, 4000s backup, 5000s restore), so nothing is formatted below the logger's level; the rendered message is formatted with the invariant culture (structured sinks get the raw values), and no prices or sizes are logged.
  - A logger's methods shall not throw; the package nevertheless drops anything a logger throws, so a failing logger loses its lines but never changes what the cache serves or writes. It is called from several threads at once, so it must be thread-safe.

### Changed
- Depends on `Microsoft.Extensions.Logging.Abstractions` 8.0.3, besides the client and Parquet.Net.
- Built on `Databento.CSharpApiClient` 2.7.2, which reads a JSON array one record at a time as it arrives.

### Fixed
- A day file's records are parsed one at a time as it is decompressed, instead of decompressing the whole day into memory and having the client read the JSON array as one document, which held the day several times over on the large object heap. A compaction of many groups at once (`MaxParallelGroups`) reached gigabytes, mostly LOH garbage and fragmentation: on invented cbbo-1s day files sized like an SPXW month, 12 groups at once peaked at a 4.4 GB GC heap (3.4 GB LOH) before and 1.0 GB (72 MB LOH) after, allocated 101 GB instead of 342 GB, and finished in 186 s instead of 204 s. Reads served from day files gain the same. A day file that is not one JSON array of records is still refused as unreadable.

## [1.3.0] - 2026-11-25

### Added
- Databento's metadata and symbology calls on the cached client, passed through uncached: `ListDatasets`, `ListSchemas`, `ListPublishers`, `ListFields`, `ListUnitPrices`, `GetDatasetCondition`, `GetDatasetRange`, `ListConditions`, `GetRecordCount`, `GetBillableSize`, `GetCost` and `ResolveSymbols`, so one client serves both the cached time series and the rest of the API.
  - On `ICachedDatabentoClient` as async calls with default implementations that throw `NotSupportedException`, so outside implementations keep compiling; their synchronous forms are in `CachedDatabentoClientExtensions`.

### Changed
- Built on `Databento.CSharpApiClient` 2.7.1.
- The cached client creates its DBN client only when `Transport` is `Dbn`; with the default `Json` it held an unused one, with its own `HttpClient`.

## [1.2.0] - 2026-11-25

### Added
- `CacheMode.CacheOnly`: serves what the cache holds and downloads nothing, so a call never costs anything. When the cache lacks any symbol-day of the range, the call throws `NotCachedException`, which names them (`MissingDays`, by raw symbol) and asks Databento nothing, not even the free metadata and symbology calls. For a restored backup used as it is, or a host that must not reach Databento for a while.

## [1.1.0] - 2026-11-25

### Added
- `ICachedDatabentoClient`, which `CachedDatabentoClient` implements: the 11 time-series calls for several symbols with a `CacheMode`, `RunBatchJobAsync`, `IngestBatchFolderAsync` and the `ArchiveFileUnreadable` event, so code written against it can be handed a fake in tests. An implementation writes only those 11 calls: default implementations serve the client's own calls through them with `CacheMode.Use`. `CachedDatabentoClientExtensions` adds the one-symbol and synchronous forms of each call; on `CachedDatabentoClient` its own methods are called instead.
- `CachedDatabentoClient` is an `IDatabentoTimeseriesClient`, the client's own interface from 2.7.0, and serves its calls with `CacheMode.Use`. So code that takes either Databento client takes the cache too, chosen by configuration.
  - Unlike the Databento clients, the cache refuses `ALL_SYMBOLS` with an `ArgumentException`, as it always has: it needs the raw symbols.

### Changed
- Built on `Databento.CSharpApiClient` 2.7.0 (it was 2.6.0).

## [1.0.0] - 2026-11-24

The first release, built on `Databento.CSharpApiClient` 2.6.0.

### Added
- `CachedDatabentoClient`: the client's time-series calls served from a disk cache, so each day of Databento data is paid for once.
  - Calls for cbbo-1s, cbbo-1m, bbo-1s, bbo-1m, tcbbo, trades, ohlcv-1s, ohlcv-1m, ohlcv-1h, ohlcv-1d and statistics, async and sync, for one symbol or several. They take the client's arguments plus a `CacheMode`, and return the client's record interfaces (`ICbboRecord`, ...) in time order, trimmed to the range.
  - Each UTC day a range touches is served per symbol from its day file, else from the archive, else downloaded whole. A downloaded day is saved before the call returns once Databento says it is final; a day that is not final yet is served but not saved.
  - Records download as JSON or DBN (`DatabentoTransport`); the cache saves the same JSON records either way.
  - `CacheMode.Bypass` downloads the days again and replaces their day files; `BypassMissAction` says what happens to a cached day Databento now answers with no records.
  - By default a final day with no records is saved as empty only when the free symbology lookup confirms the symbol was not listed that day, so a gap in Databento's data is asked for again. `EmptyDayPolicy` can save every empty day, or none.
  - `ArchiveFileUnreadable` is raised when an archive file can't be read, for the host to alarm on.
- Batch jobs: `RunBatchJobAsync` saves the final symbol-days a range lacks through Databento batch jobs, for ranges too large for the time-series calls. Databento prices the jobs first, and `BatchJobTooExpensiveException` is thrown, with nothing submitted, when they pass the cap you give. A job submitted before that covers the range, and is not expired, is downloaded again at no cost. `IngestBatchFolderAsync` saves a batch download folder made by any tool.
- `DatabentoArchiveCompactor`: folds day files into Parquet, one archive per dataset, schema, group and month, as a full file and at most one delta. OPRA options are grouped by their parent (`SPXW.OPT`) and CME futures by theirs (`MNQ.FUT`). Each new file is checked against its sources, record by record by default (`ArchiveVerification`), before it is published and the files it replaces are deleted. Optional `IArchiveDayCheck`s flag suspect days in the report.
- `DatabentoCacheBackup` and `DatabentoCacheRestore`: the whole cache as one tar with a SHA-256 manifest, kept in an `IBackupStore` (`LocalDirectoryBackupStore` for a local or synced folder) under a `BackupRetention`. A restore checks every file before it touches the cache, says what happens to the cache's own files (`ExistingCacheFiles`), and refuses while an `ICacheInUseChecker` reports the cache in use. A dry run of a restore unpacks and checks a backup without touching the cache.

[Unreleased]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.4.1...HEAD
[1.4.1]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/releases/tag/v1.0.0
