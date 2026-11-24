# Databento.CSharpApiClient.Caching

A disk cache and Parquet archive for [Databento.CSharpApiClient](https://github.com/mrut2pac/Databento.CSharpApiClient), so each day of Databento data is paid for once.

**Work in progress: not yet released to NuGet.** What is in so far: the cache's on-disk format, the compactor, the cached client with batch jobs, and backup and restore.

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

Backups go wherever an `IBackupStore` keeps them; `LocalDirectoryBackupStore` is a local or synced folder.

## License

MIT
