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
| archive | random | 1 | 62.7 ms | 1,131.8 ms | 16 | 7,668 MB |
| archive | sequential | 1 | 2.0 ms | 3.4 ms | 494 | 6,872 MB |
| archive | random | 8 | 291.2 ms | 2,809.1 ms | 27 | 11,891 MB |
| archive | sequential | 8 | 28.6 ms | 708.6 ms | 280 | 8,740 MB |

After a backup and a restore, the restored copy was read with the same queries: 8,000 reads checked against the generated records, 0 mismatched, and pass means from 89% to 135% of those on the archive it was backed up from.

## Steps

*Peak working set* is the process's memory held in RAM, shared pages such as the runtime's included; *peak committed* is the private memory it committed, which Server GC sets well above it, one heap per core.

| Step | What | Time | Rate | Peak working set | Peak committed | Cores busy |
|---|---|--:|--:|--:|--:|--:|
| seed | 2,074,220 day files, 18.16 GB | 1:03:20 | 546 files/s | — | 0.4 GB | 2.2 |
| compact | 2,074,220 day files into 102 Parquet files, 18.16 GB to 12.99 GB | 32:52 | 1,052 files/s, 0.4 M rows/s | — | 3.9 GB | 6.5 |
| backup | 103 files, 12.99 GB | 0:42 | 298 MB/s | 0.1 GB | 0.1 GB | 1.1 |
| restore | 103 files, 12.99 GB | 0:32 | 392 MB/s | 0.1 GB | 0.1 GB | 1.0 |

\* — is a value the run behind this report did not measure; running the tool again measures it.

## Reading a contract's day

One call per symbol-day, `CacheMode.CacheOnly`. *Random* picks symbol-days anywhere in the data; *sequential* reads runs of up to 200 neighboring contracts of one day, calls and puts in strike order, expiration by expiration. *First* is a new client; *repeat* is the same reads again on it. *Allocated a read* is the garbage each read leaves for the collector; *heap kept* is the managed heap after the pass and a full collection, with the client still open.

| Cache | Order | Readers | Pass | Mean | p50 | p95 | p99 | Reads/s | Records/s | Allocated a read | Heap kept | Peak working set | Peak committed |
|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| archive | random | 1 | first | 62.66 ms | 6.26 ms | 133.25 ms | 1,131.76 ms | 16 | 6 K | 35,563 KB | 1,625 MB | 7,668 MB | 7,962 MB |
| archive | random | 1 | repeat | 58.08 ms | 3.18 ms | 111.67 ms | 1,197.70 ms | 17 | 7 K | 35,127 KB | 1,625 MB | 7,381 MB | 7,646 MB |
| archive | sequential | 1 | first | 2.02 ms | 1.37 ms | 2.53 ms | 3.39 ms | 494 | 193 K | 2,216 KB | 564 MB | 6,872 MB | 7,090 MB |
| archive | sequential | 1 | repeat | 1.63 ms | 1.45 ms | 2.76 ms | 3.78 ms | 615 | 240 K | 1,861 KB | 553 MB | 6,290 MB | 6,504 MB |
| archive | random | 8 | first | 291.16 ms | 22.43 ms | 2,159.00 ms | 2,809.07 ms | 27 | 11 K | 36,744 KB | 1,711 MB | 11,891 MB | 12,102 MB |
| archive | random | 8 | repeat | 268.71 ms | 9.26 ms | 2,046.52 ms | 2,836.88 ms | 30 | 12 K | 35,943 KB | 1,705 MB | 11,964 MB | 12,173 MB |
| archive | sequential | 8 | first | 28.56 ms | 10.64 ms | 17.55 ms | 708.61 ms | 280 | 109 K | 6,070 KB | 669 MB | 8,740 MB | 8,933 MB |
| archive | sequential | 8 | repeat | 11.31 ms | 11.24 ms | 14.37 ms | 20.27 ms | 705 | 275 K | 1,843 KB | 669 MB | 8,132 MB | 8,326 MB |
| restored | random | 1 | first | 63.91 ms | 5.41 ms | 150.93 ms | 1,160.89 ms | 16 | 6 K | 35,563 KB | 1,617 MB | 7,320 MB | 7,590 MB |
| restored | random | 1 | repeat | 78.61 ms | 6.71 ms | 240.12 ms | 1,185.49 ms | 13 | 5 K | 35,136 KB | 1,617 MB | 8,201 MB | 8,451 MB |
| restored | sequential | 1 | first | 1.92 ms | 1.35 ms | 2.49 ms | 3.21 ms | 520 | 203 K | 2,216 KB | 556 MB | 7,026 MB | 7,221 MB |
| restored | sequential | 1 | repeat | 1.46 ms | 1.34 ms | 2.39 ms | 3.06 ms | 685 | 267 K | 1,861 KB | 553 MB | 6,501 MB | 6,693 MB |
| restored | random | 8 | first | 259.99 ms | 11.53 ms | 1,971.44 ms | 2,517.64 ms | 31 | 12 K | 36,349 KB | 1,699 MB | 11,067 MB | 11,278 MB |
| restored | random | 8 | repeat | 248.52 ms | 8.99 ms | 1,821.70 ms | 3,335.63 ms | 32 | 13 K | 36,229 KB | 1,708 MB | 11,453 MB | 11,664 MB |
| restored | sequential | 8 | first | 28.72 ms | 9.93 ms | 38.42 ms | 547.63 ms | 278 | 109 K | 6,222 KB | 668 MB | 11,317 MB | 11,515 MB |
| restored | sequential | 8 | repeat | 10.38 ms | 10.55 ms | 13.06 ms | 14.45 ms | 768 | 300 K | 1,852 KB | 668 MB | 10,442 MB | 10,641 MB |
| day files | random | 1 | first | 2.64 ms | 1.94 ms | 5.53 ms | 7.20 ms | 378 | 147 K | 177 KB | 1 MB | 314 MB | 329 MB |
| day files | random | 1 | repeat | 1.44 ms | 1.40 ms | 1.69 ms | 1.91 ms | 695 | 271 K | 177 KB | 1 MB | 338 MB | 353 MB |
| day files | sequential | 1 | first | 1.85 ms | 1.75 ms | 2.54 ms | 2.94 ms | 540 | 211 K | 177 KB | 1 MB | 335 MB | 350 MB |
| day files | sequential | 1 | repeat | 1.42 ms | 1.39 ms | 1.68 ms | 1.96 ms | 703 | 274 K | 177 KB | 1 MB | 338 MB | 352 MB |
| day files | random | 8 | first | 2.23 ms | 2.08 ms | 3.06 ms | 4.03 ms | 3,578 | 1.4 M | 177 KB | 2 MB | 345 MB | 359 MB |
| day files | random | 8 | repeat | 1.91 ms | 1.77 ms | 2.60 ms | 3.52 ms | 4,170 | 1.6 M | 177 KB | 2 MB | 350 MB | 364 MB |
| day files | sequential | 8 | first | 2.33 ms | 2.17 ms | 3.19 ms | 5.04 ms | 3,429 | 1.3 M | 177 KB | 2 MB | 351 MB | 364 MB |
| day files | sequential | 8 | repeat | 1.94 ms | 1.73 ms | 2.47 ms | 3.19 ms | 4,111 | 1.6 M | 177 KB | 2 MB | 355 MB | 369 MB |

## Reading these numbers

- *First* passes are not cold reads: the operating system's file cache keeps what earlier passes and steps read, and a machine with more RAM than the archive serves most of it from memory.
- The synthetic quotes are random walks, and the archive holds 16.0 bytes a record here. Real quotes compress differently, so an archive of real data has another size for the same records.
- Random reads over the archive took 62.7 ms on average with 1 reader (p99 1,132 ms), sequential ones 2.0 ms. The client keeps at most 32 archive versions open with their indexes, one for each dataset, schema, root and month, a full file with its delta; the archive here has 102 full files. A read beyond them closes the least recently used and opens its file again, parsing its footer, while sequential reads stay in one file and reuse its index.
- 8 readers read the archive sequentially at 280 reads a second, against 494 for 1: reads of one archive file take turns on its single open reader. Each reader count reads its own set of symbol-days, picked from the same seed.
