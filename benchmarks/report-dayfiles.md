# Databento.CSharpApiClient.Caching benchmark

Made by the benchmark app in this repository's `benchmarks/` folder, on synthetic data it generates from `benchmark.json`: no market data is downloaded and Databento is never called. Run it to get this report for your own machine and settings.

Machine: AMD Ryzen 9 3900X 12-Core Processor, 24 logical cores, 64 GB RAM, Microsoft Windows 10.0.19045, .NET 8.0.34, Server GC.

Disk: Corsair MP600 PRO LPX NVMe SSD, D:\ (NTFS, 2,000 GB); the cache, the backup and the restored copy are all on it.

Data: synthetic, 740 trading days from 2024-01-01 to 2026-10-30, seed 20261127.

| Root | Schema | Contracts a day | Records a contract-day | Symbol-days |
|---|---|--:|--:|--:|
| SPXW | OPRA.PILLAR cbbo-1m | 2,000 | 390 | 1,480,000 |
| SPY | OPRA.PILLAR cbbo-1m | 800 | 390 | 592,000 |
| MES | GLBX.MDP3 bbo-1m | 3 | 1,380 | 2,220 |

## Summary

Reading one contract's day, first pass on a new client. *Day files* is the cache as downloads leave it; *archive* is after compaction into monthly Parquet files.

| Cache | Order | Readers | Mean | p99 | Reads/s | Peak working set |
|---|---|--:|--:|--:|--:|--:|
| day files | random | 1 | 2.6 ms | 7.2 ms | 378 | 314 MB |
| day files | sequential | 1 | 1.8 ms | 2.9 ms | 540 | 335 MB |
| day files | random | 8 | 2.2 ms | 4.0 ms | 3,578 | 345 MB |
| day files | sequential | 8 | 2.3 ms | 5.0 ms | 3,429 | 351 MB |

After a backup and a restore, the restored copy was read with the same queries: 8,000 reads checked against the generated records, 0 mismatched, and pass means from 84% to 104% of those on the day files it was backed up from.

## Steps

*Peak working set* is the process's memory held in RAM, shared pages such as the runtime's included; *peak committed* is the private memory it committed, which Server GC sets well above it, one heap per core.

| Step | What | Time | Rate | Peak working set | Peak committed | Cores busy |
|---|---|--:|--:|--:|--:|--:|
| seed | 2,074,220 day files, 18.16 GB | 39:14 | 881 files/s | 0.4 GB | 0.4 GB | 3.8 |
| backup | 2,074,221 files, 18.16 GB | 49:55 | 5.8 MB/s | 7.5 GB | 7.9 GB | 2.7 |
| restore | 2,074,221 files, 18.16 GB | 1:51:14 | 2.6 MB/s | 5.5 GB | 6.0 GB | 1.4 |

## Reading a contract's day

One call per symbol-day, `CacheMode.CacheOnly`. *Random* picks symbol-days anywhere in the data; *sequential* reads runs of up to 200 neighboring contracts of one day, calls and puts in strike order, expiration by expiration. *First* is a new client; *repeat* is the same reads again on it. *Allocated a read* is the garbage each read leaves for the collector; *heap kept* is the managed heap after the pass and a full collection, with the client still open.

| Cache | Order | Readers | Pass | Mean | p50 | p95 | p99 | Reads/s | Records/s | Allocated a read | Heap kept | Peak working set | Peak committed |
|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| day files | random | 1 | first | 2.64 ms | 1.94 ms | 5.53 ms | 7.20 ms | 378 | 147 K | 177 KB | 1 MB | 314 MB | 329 MB |
| day files | random | 1 | repeat | 1.44 ms | 1.40 ms | 1.69 ms | 1.91 ms | 695 | 271 K | 177 KB | 1 MB | 338 MB | 353 MB |
| day files | sequential | 1 | first | 1.85 ms | 1.75 ms | 2.54 ms | 2.94 ms | 540 | 211 K | 177 KB | 1 MB | 335 MB | 350 MB |
| day files | sequential | 1 | repeat | 1.42 ms | 1.39 ms | 1.68 ms | 1.96 ms | 703 | 274 K | 177 KB | 1 MB | 338 MB | 352 MB |
| day files | random | 8 | first | 2.23 ms | 2.08 ms | 3.06 ms | 4.03 ms | 3,578 | 1.4 M | 177 KB | 2 MB | 345 MB | 359 MB |
| day files | random | 8 | repeat | 1.91 ms | 1.77 ms | 2.60 ms | 3.52 ms | 4,170 | 1.6 M | 177 KB | 2 MB | 350 MB | 364 MB |
| day files | sequential | 8 | first | 2.33 ms | 2.17 ms | 3.19 ms | 5.04 ms | 3,429 | 1.3 M | 177 KB | 2 MB | 351 MB | 364 MB |
| day files | sequential | 8 | repeat | 1.94 ms | 1.73 ms | 2.47 ms | 3.19 ms | 4,111 | 1.6 M | 177 KB | 2 MB | 355 MB | 369 MB |
| restored | random | 1 | first | 2.75 ms | 1.97 ms | 5.89 ms | 8.67 ms | 363 | 142 K | 177 KB | 1 MB | 320 MB | 335 MB |
| restored | random | 1 | repeat | 1.40 ms | 1.37 ms | 1.67 ms | 1.92 ms | 714 | 279 K | 177 KB | 1 MB | 338 MB | 352 MB |
| restored | sequential | 1 | first | 1.86 ms | 1.76 ms | 2.52 ms | 2.93 ms | 537 | 209 K | 177 KB | 1 MB | 331 MB | 345 MB |
| restored | sequential | 1 | repeat | 1.40 ms | 1.36 ms | 1.67 ms | 1.94 ms | 714 | 278 K | 177 KB | 1 MB | 338 MB | 352 MB |
| restored | random | 8 | first | 2.26 ms | 2.11 ms | 3.04 ms | 4.48 ms | 3,532 | 1.4 M | 177 KB | 2 MB | 351 MB | 365 MB |
| restored | random | 8 | repeat | 1.81 ms | 1.69 ms | 2.52 ms | 3.32 ms | 4,408 | 1.7 M | 177 KB | 2 MB | 347 MB | 361 MB |
| restored | sequential | 8 | first | 2.06 ms | 1.94 ms | 2.77 ms | 3.80 ms | 3,874 | 1.5 M | 177 KB | 2 MB | 347 MB | 359 MB |
| restored | sequential | 8 | repeat | 1.63 ms | 1.53 ms | 2.27 ms | 2.63 ms | 4,894 | 1.9 M | 177 KB | 2 MB | 353 MB | 366 MB |

## Reading these numbers

- *First* passes are not cold reads: the operating system's file cache keeps what earlier passes and steps read, and a machine with more RAM than the archive serves most of it from memory.
