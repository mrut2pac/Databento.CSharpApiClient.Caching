# Benchmarks

A console app that measures the cache on your own machine: writing day files, reading a contract's day from day files and from the Parquet archive, compaction, backup and restore. It runs on **synthetic data only**. Every quote is generated from a seed in `benchmark.json`, nothing is downloaded, and no step calls Databento, so it costs nothing and needs no API key.

## What it generates

Each weekday from `StartDate` to `EndDate` (there is no holiday calendar), for each instrument:

- **Options** (`"Kind": "Option"`, OPRA `cbbo-1m`): calls and puts on `2 × StrikesPerSide` strikes around the money, for the next `Expirations` trading days, so a contract is quoted on several days, as real options are.
- **Futures** (`"Kind": "Future"`, CME `bbo-1m`): the next `Expirations` quarterly contract months, e.g. `MESZ6`.

Each contract-day has `RecordsPerDay` records, one a minute from `FirstMinuteUtc`. The prices follow a seeded random walk; they look like quotes but are not market data. The shipped `benchmark.json` generates SPXW, SPY and MES from 2024-01-01 to 2026-10-30: about 2.07 million contract-days and 810 million records.

## Running it

```bash
dotnet build benchmarks/Databento.CSharpApiClient.Caching.Benchmarks -c Release
cd benchmarks/Databento.CSharpApiClient.Caching.Benchmarks/bin/Release/net8.0
./Databento.CSharpApiClient.Caching.Benchmarks all --settings benchmark.json
```

Set `WorkFolder` first: everything is written under it. The shipped settings needed about 40 GB at the peak and about two hours before `unpack` joined `all`; the unpacked copy now adds its day files beside the compacted cache and the backup, and the unpack and its queries add to the time. Excluding the folder from antivirus scanning makes the day-file steps much faster, since every file written or read is otherwise scanned.

### The mini suite

`benchmark-mini.json` measures the same things in about an hour, for a before-and-after of a change while it is worked on; the full suite is for the numbers a release states. It keeps the three instruments and the queries but covers 12 months, with SPXW on 60 strikes a side instead of 100: about 521,000 contract-days, a quarter of the full suite's. It still has 36 group-months, more than the 32 archive versions the client keeps open, so random archive reads still reopen files. It needs about 15 GB at the peak.

```bash
./Databento.CSharpApiClient.Caching.Benchmarks all --settings benchmark-mini.json
```

Its numbers compare with another run of the mini suite on the same machine, not with the full suite's: its month files are smaller, and so are their footers.

`all` runs these steps in order. Each can also be run alone, and each saves its results under `WorkFolder/results`:

| Step | What it does |
|---|---|
| `seed` | Writes every contract-day as a day file, the way the cache saves a download. A day file already there is kept, so an interrupted seed resumes. |
| `query` | Reads symbol-days through `CachedDatabentoClient` in `CacheMode.CacheOnly`, one call per contract-day: in random order, then in runs of up to 200 neighboring contracts of one day (calls and puts in strike order, expiration by expiration), with each of `QueryParallelism` concurrent readers. Run before `compact` it reads day files; run after, the archive. |
| `compact` | Runs `DatabentoArchiveCompactor` with `MaxParallelGroups`. |
| `backup` | Backs the cache up with `DatabentoCacheBackup` to `WorkFolder/backups`. |
| `restore` | Restores that backup into `WorkFolder/restored`, which must be empty. |
| `verify` | Runs the queries against the restored cache. |
| `unpack` | Unpacks the restored cache's archive back into day files with `DatabentoArchiveUnpacker` and `MaxParallelGroups`. |
| `unpacked` | Runs the queries against the unpacked cache: day files again, as unpacking wrote them. |
| `report` | Writes `WorkFolder/report.md` from the saved results. |

To measure a cache kept as day files instead, run `seed`, `query`, `backup`, `restore`, `verify` and `report` one at a time in a new `WorkFolder`, leaving out `compact`. The shipped settings then need about 60 GB at the peak (the day files, the backup's tar and the restored copy), and the backup and restore of 2 million day files take about 2 h 40 min on the machine in [report-dayfiles.md](../report-dayfiles.md). The two reports in [`benchmarks/`](..) are a run of each on the same seed and data: [report.md](../report.md), compacted, and [report-dayfiles.md](../report-dayfiles.md), kept as day files. The day-file query rows of both reports come from the day-file run, whose results measure the working set; the compacted run's own day-file queries predate that measure.

Every query step also checks every field of each read against the records the generator makes for it, in a pass outside the timing, and fails if any differ. The data and the queries come from `Seed`, so two runs with the same settings generate and read the same records.

The app runs at below-normal priority with Server GC, so other work on the machine keeps its share. The query timings include the operating system's file cache: the first pass on a new client may find files the previous pass read.
