# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/releases/tag/v1.0.0
