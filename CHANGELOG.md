# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `DatabentoArchiveUnpacker`, with `ArchiveUnpackerOptions` and `ArchiveUnpackReport`: the compactor's inverse, writing a compacted archive back as DBN day files in their day folders ([#47](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/issues/47)). The scope is the whole cache or a `Dataset`, `Schema`, `Group` and month range, a month at a time. For each group-month it writes every symbol-day the current version serves (the delta's copy where it lists the day), empty days included, reads each back and checks it column by column, and only then deletes the version's files, so a stop or failure leaves both and a rerun keeps the day files written. A readable day file or legacy day file already there is kept; one that cannot be read is replaced by the archive's copy, which the cache would have served. An older full file or delta of the group that a stopped compaction left is deleted with the version when every day it holds with records has a day file; otherwise the group fails and keeps its archive, so that file never becomes the version served. The delta is deleted before its full file, and once deletes start a stop no longer interrupts them. `DryRun` counts the day files and their bytes, with the free space of the cache root's drive, changing nothing. It holds `cache.lock`, refuses a cache being restored, works `MaxParallelGroups` group-months at once, and logs events 6001 to 6008. Run it with no cached client open.
- A cached client's read that misses the archive in a month folder changed since its listing looks for the day's day file once more, so a day an unpack wrote just then is found.
- The benchmark app's `unpack` step unpacks the restored copy, and `unpacked` reads it, both part of `all`. `benchmark-mini.json` is a smaller suite, about 25% of the full one's symbol-days over 36 group-months, for a run of about an hour.

### Format
- Day files are DBN, zstd-compressed (`.dbn.zst`), written with the client's `DbnWriter` and read with its `DbnReader`, instead of a gzipped JSON array (`.json.gz`). Each file's metadata (DBN version 3) names its dataset, schema, UTC day and symbol, and maps each instrument id of its records to the symbol, so Databento's `dbn` CLI and Python `databento` package read it as it is. Parsing JSON was most of a day file's read: a probe of 5,000 synthetic cbbo-1m days read DBN about 12 times faster than gzipped JSON, in files 9% smaller ([#49](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/issues/49)).
- Day files go into a folder per UTC day, `dataset/schema/yyyy/MM/dd/`, so a month folder holds at most 31 day folders, its archive files and any legacy day files. A read that misses its day file lists that folder for its archive files, which no longer means walking every day file of the month once the legacy ones are compacted, and Explorer, antivirus and sync tools no longer meet folders of 60,000 files, past FAT32's limit of about 65,000 entries ([#43](https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/issues/43)).
- Caches written before keep working with no rewrite. A read takes the day file in the day folder first, then a `.json.gz` day file in the month folder, then the archive, so a day served from the archive pays two failed opens first. The compactor absorbs both kinds; a legacy day file of a day that also has a DBN day file is retired with it, the DBN file being the one archived. A `CacheMode.Bypass` replacing a legacy day writes the DBN day file and deletes the legacy one. Backups hold both kinds, and a backup of an earlier version restores as it is. The `.json.gz` fallback is to be retired in 2.0.0.
- The format version in `metadata.json` goes from 1 to 2. A version 1 cache is marked version 2, with no day file rewritten, when the cached client opens it, a compaction that is no dry run starts on it, or a version 1 backup is restored; a dry run and a backup leave it as it is. From then on an older build refuses it rather than taking its DBN day files for missing days. A version 1 cache whose `metadata.json` cannot be rewritten is read, but nothing is saved into it: the save throws an `IOException`. The mark writes the file anew, so any field another build added to it is dropped. Going back to an older build is not supported.
- `ArchiveCompactorOptions.DeleteEmptyDayDirectories` (default `true`): a compaction deletes each day folder it empties of its day files. The delete refuses a folder that holds anything, so a day file a client saves during the run keeps its folder, and the run moves on. A client that finds its day folder gone between creating it and writing the day file creates it once more; a second loss fails the save, as no compaction causes it.

### Changed
- `DatabentoTransport.Dbn` is the default `Transport`, so downloads arrive as the DBN records day files hold. With `Json`, records are converted to DBN records as they are downloaded.
- The time-series calls return the client's DBN record types (`CbboRecordDbn` and so on) behind the record interfaces, whichever the transport and whether served from a day file, a legacy day file or the archive. Breaking for code that type-tests or casts: a returned `ICbboRecord` cast to `CbboRecordJson` must use the interface or the DBN type instead.
- A day whose records hold a value DBN cannot hold, such as an undefined side, is served but not saved, with a Warning (1031); a bypass over a cached day then keeps the cached day.
- A JSON record is converted to the DBN type through the values the archive stores. One whose value the record interface cannot give (a JSON number past the interface's range) fails the JSON download, or JSON batch file, it is in. A legacy day file holding one is left on disk, as the compactor leaves it, and its day is read as not cached.
- Depends on Databento.CSharpApiClient 2.9.0, for `DbnWriter`.

## [1.6.2] - 2026-11-28

### Fixed
- A time-series call no longer lists every file of each month folder it reads. A day file is opened by name, and a day without one goes to the archive. The folder's archive files are listed only when a day is not in a day file, once per call and month folder, and again when such a day misses the archive in a folder that changed since the listing, as a compaction absorbing that day file during the call does. On a folder of 58,800 day files, each call used to list the whole folder first: 51 to 66 ms and 25 MB allocated per symbol-day with one reader, 118 to 173 ms with eight; it now takes 1.6 to 5.6 ms and allocates 177 KB, about the day's records. A day served from the archive now pays one failed open of its day file first, about 50 microseconds, against about 2 ms to read the day from the archive. `EnsureCachedAsync` and batch jobs find a day file by its name too.

## [1.6.1] - 2026-11-27

### Changed
- The download request line (1008) writes its window half-open, as Databento reads it, names a single day once and no longer ends with ", from Databento": "Requesting cbbo-1m of OPRA.PILLAR for SPXW  260223C07035000, UTC day 2026-02-20 [2026-02-20T00:00:00Z to 2026-02-21T00:00:00Z) ...", or "UTC days 2026-02-20 to 2026-02-23" for several. Its structured `FirstDay` and `LastDay` properties become one `Days` string. The request itself is unchanged: its end was already exclusive.

## [1.6.0] - 2026-11-27

### Added
- `EnsureCachedAsync` on `ICachedDatabentoClient` and `CachedDatabentoClient`, with `EnsureCached` in `CachedDatabentoClientExtensions`: makes sure the cache holds a range of symbol-days of a schema (named as Databento names it, e.g. `cbbo-1m`) and returns no records. It finds the cached days by day file name and archive index, reading no record, and downloads only the others as a `CacheMode.Use` time-series call would: chunked by `MaxSymbolsPerRequest`, saved under `SettleDelay`, the empty-day policy and condition guard, cut at the available end with `AvailableEndRefusal` as set, nothing kept in memory once saved. It returns an `EnsureCachedReport`: `AlreadyCached`, `Saved`, `PastAvailableEnd`, `NotSettled`, and `NotCached`, the symbol-days the cache still lacks. On `ICachedDatabentoClient` it has a default implementation that throws `NotSupportedException`. A batch request built from a `CacheMode.Use` call and a `CacheMode.CacheOnly` call read every cached record twice: 51 to 88 s per 34 to 44 option contracts on a fully archived cache, downloading nothing.

### Changed
- The per-day Debug lines name the file without its folder: a day served from its day file or the archive (1002 and 1003, now both "{Symbol} {Day} {Schema} served from {File}, {RecordCount} records"), a day saved (1014), a day file kept (1015) and an empty day saved (1017). The structured property is `File` instead of `Path`. Warnings and errors about a file still give its full path.

### Fixed
- A symbol-day read from the archive no longer opens its file again and parses its whole Parquet footer: the cached client keeps each archive file it reads open, with its index, until a new full file or delta generation replaces it, it is found unreadable, enough other group-months are read, or the client is disposed. On a file of 4,100 row groups (an SPXW cbbo-1m month), one symbol-day read took 0.8 s and allocated 85 MB, with a full GC every read or so; it now takes 2 to 3 ms and allocates under 2 MB. An archive file is opened sharing read, write and delete, so a compaction retires it while the client holds it open; a read whose file was closed under it by another request reads from a handle of its own. A file a compaction retired keeps its disk space until the client next reads that group-month or is disposed. Shut the application down before a restore: a running client now holds archive files open. As a best-effort guard, a restore refuses with an `InvalidOperationException` naming the open archive files before it touches anything; this works on Windows, not on Linux, which does not lock files, so there an `ICacheInUseChecker` is the guard.

## [1.5.0] - 2026-11-27

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

[Unreleased]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.6.2...HEAD
[1.6.2]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.6.1...v1.6.2
[1.6.1]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.6.0...v1.6.1
[1.6.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.4.1...v1.5.0
[1.4.1]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/mrut2pac/Databento.CSharpApiClient.Caching/releases/tag/v1.0.0
