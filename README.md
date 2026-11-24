# Databento.CSharpApiClient.Caching

A disk cache and Parquet archive for [Databento.CSharpApiClient](https://github.com/mrut2pac/Databento.CSharpApiClient), so each day of Databento data is paid for once.

**Work in progress: not yet released to NuGet.** What is in so far: the cache's on-disk format and the compactor. The cached client, batch jobs, and backup and restore come next.

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

## License

MIT
