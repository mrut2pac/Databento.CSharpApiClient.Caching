# CLAUDE.md — Databento.CSharpApiClient.Caching

Guidelines for working on this repository with Claude Code or any AI coding assistant.

---

## Repo overview

A disk cache and Parquet archive on top of [Databento.CSharpApiClient](https://github.com/mrut2pac/Databento.CSharpApiClient) (.NET 8), so each day of Databento data is paid for once. It depends on the client's NuGet package, Parquet.Net and Microsoft.Extensions.Logging.Abstractions, nothing else.

```
src/
  Databento.CSharpApiClient.Caching/            ← the library, the NuGet package
tests/
  Databento.CSharpApiClient.Caching.UnitTests/  ← deterministic, no network
  Databento.CSharpApiClient.Caching.IntegrationTests/  ← live Databento, costs money; Inconclusive without DATABENTO_API_KEY
.github/workflows/ci.yml                        ← CI: build + unit tests on every push/PR
.github/workflows/publish.yml                   ← packs and pushes to NuGet.org on a v* tag, then creates the tag's GitHub Release from its CHANGELOG section
```

- **The client comes from NuGet**, as for any of its users (`PackageReference`, not a project reference). A client feature the package needs ships in a client release first, and the package then moves to that version.
- **Only the client's public API**: no `InternalsVisibleTo` from the client, no copies of its internals.

---

## Contribution model — PRs only, no direct commits

Every change goes through a Pull Request, squash-merged. Create a feature branch off `main`, build and test locally, push it and open a PR.

---

## Building

```bash
dotnet build Databento.CSharpApiClient.Caching.slnx              # Debug by default
dotnet build Databento.CSharpApiClient.Caching.slnx -c Release
```

Zero warnings required. `GenerateDocumentationFile=true` is enabled and `CS1591` is an error: every public member needs an XML doc comment.

---

## Tests

```bash
dotnet test tests/Databento.CSharpApiClient.Caching.UnitTests
```

The unit tests write into temporary folders and never touch the network. A test that needs Windows file locking (an open handle that blocks a delete) is `Inconclusive` elsewhere; the rest run on every OS, the delete retries through a test hook.

**Live tests cost money.** `tests/Databento.CSharpApiClient.Caching.IntegrationTests` downloads whole UTC days from Databento with the key in `DATABENTO_API_KEY` (without it every test is `Inconclusive`; CI builds the project but never runs it). A full run costs about $0.14. `LiveBudget` prices every download first with the free cost endpoint and fails a test, without buying, when the run would pass its cap; calls the cache must serve run after `CountingTransport.ForbidDownloads()`, which refuses any paid request ($0.25, or `DATABENTO_LIVE_TEST_CAP_USD`). Never run them unfiltered without agreeing the cost first; add a test's downloads to `LiveBudget` before it buys anything, and keep the symbols and days cheap. What they check: every cached schema's day against Databento's own record count on both transports, a range across UTC midnight, empty days (not listed: saved; listed but empty: not saved), a batch job, parent-symbol groups against Databento's symbology, a downloaded cache through compaction, backup and restore, and the metadata and symbology pass-throughs against the plain client's answers (free calls).

---

## Releasing to NuGet.org

**Code changes and version bumps are separate PRs**, so the history shows each release as its own step. A feature or fix PR adds its entry under CHANGELOG `[Unreleased]` and never touches `<Version>` or `<PackageReleaseNotes>`; the version bump is a `chore:` PR of its own, opened once every PR of the release is merged.

1. Decide whether the live tests must run first. Diff from the last `v*` tag to the commit that will be tagged: a run is needed when anything touches downloads, the day files, the archive or a restore, or when the last full run predates such a change. Price it and agree the cost first (see "Live tests cost money"), run it on that commit, and record the passed count, or "not needed" and why, in the bump PR's Testing section.
2. Bump `<Version>` in the library's csproj, add its `<PackageReleaseNotes>` entry, and cut CHANGELOG `[Unreleased]` into a dated section with its compare link.
3. Merge the bump PR, then tag `main` and push the tag:
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```
4. `publish.yml` builds Release, packs the `.nupkg` and `.snupkg`, and pushes them to NuGet.org through NuGet Trusted Publishing: no API key is stored, but nuget.org needs a Trusted Publishing policy for this repository and `publish.yml`. `--skip-duplicate` makes a tag for a version already on NuGet.org a no-op.

---

## Market data

- **Never commit or share real market data.** Databento's terms forbid redistributing the data it sells, and this repo will be public. No file Databento (or any vendor) delivered goes into the repo, a PR, an issue or a test as it came: not DBN, JSON, CSV or Parquet records, not responses pasted into tests.
- **Fixtures are invented.** Write records by hand, or keep a real file's layout and replace every price, size, count, sequence, statistic and timestamp (`ts_event`, `ts_recv` and the rest) with invented values before it is committed, and say so in a `README.md` next to it. Symbols, instrument ids, record types and the folder files are format, not data, and may stay.

## Coding conventions

- **Dependencies:** the client, Parquet.Net and Microsoft.Extensions.Logging.Abstractions only. Logging goes through the options' `ILogger` (`NullLogger.Instance` by default), with `[LoggerMessage]` methods in `Logging/CacheLog.cs` (stable event ids by component, at most 6 template arguments so the rendered message stays invariant, counts, symbols, days and paths but never prices or sizes, and never inside a per-record loop); every caller's logger is wrapped once in `NonThrowingLogger` where its options are copied, so a logger that throws loses its line and changes nothing: never call the caller's logger directly; results still come back as report objects, and progress through `IProgress<T>`.
- **Public surface stays small:** the cached client, `ICachedDatabentoClient` and its extensions, and the client's options, modes and event, the compactor, its options and report, the day-check API, and backup and restore (their options and reports, `IBackupStore`, `LocalDirectoryBackupStore`, `BackupRetention`, `ICacheInUseChecker`, `ExistingCacheFiles`) and `NotCachedException` are public; the archive format, naming, file handling, the backup tar (`Backup/`) and the client's download machinery (`Client/`) are `internal`.
- **The cached client's calls are the client's**, async and sync, same names and arguments plus a `CacheMode`, one set per schema the archive has columns for (`Client/CachedSchemas.cs`). A new schema is an archive schema, a row there and its calls: its async call for several symbols also goes on `ICachedDatabentoClient` (with a default implementation that throws `NotSupportedException`, so outside implementations keep compiling), its one-symbol and sync forms in `CachedDatabentoClientExtensions`, and, when the client's `IDatabentoTimeseriesClient` has it, a default implementation in `ICachedDatabentoClient` that serves it with `CacheMode.Use`. `CachedClientInterfaceTests` fails until they are there.
- **New calls get their synchronous forms as extensions only.** A call added to the cached client goes on the class and on `ICachedDatabentoClient` as async, and its sync form goes in `CachedDatabentoClientExtensions`, which serves the class too; the class gets no sync method of its own. The time-series and batch calls keep the sync methods they shipped with on the class: removing them would break compiled callers, so it waits for a major version.
- **Databento's metadata and symbology calls are passed through uncached** (`CachedDatabentoClient.Metadata.cs`), with the client's names, arguments and defaults, so one client serves the whole Historical API. A metadata call added to the client gets its pass-through here the same way.
- **Its unit tests talk to `FakeDatabento`**, an in-memory Databento behind the client's `IHttpTransport` (time series as JSON lines or a CBBO DBN stream, dataset range and conditions, symbology), so they exercise the real client. `CachedDatabentoClientCallTests.EveryPublicCall_IsCovered` fails when a call is added without its row in that test.
- **Batch fixtures mimic real folders:** `tests/.../Batch/Samples/` holds the layout of folders Databento made for small jobs (DBN and JSON, zstd or not, split by day, week or symbol, GLBX DBN v3, a never-listed symbol), with every market value invented (see its `README.md`) and the account id in the manifests' URLs replaced. The fake serves them as its jobs' files. What the real folders showed: no `symbology.json` (symbols come from each DBN file's metadata, or each JSON record with `map_symbols`), files cut by `ts_recv`'s UTC date, `split_size` of 1 to 10 GB only, and a submit naming an unlisted symbol accepted.
- **One run at a time per cache root:** a compaction, a backup and a restore each hold `cache.lock` (`Cache/CacheRootLock.cs`) while they run. A restore also writes `RESTORE_IN_PROGRESS`, which the cached client, the compactor and the backup refuse to start on; a client already running is the caller's `ICacheInUseChecker` to report, since the package cannot see other processes; on Windows the restore also refuses while an archive file is open, as a running client keeps them (best effort: Linux does not lock files).
- **Time comes from `TimeProvider`** (in the options), never `DateTime.Now`, so tests drive it.
- **Invariant formatting:** `Invariant($"...")`, from the global `using static System.FormattableString`.
- **Async:** `ConfigureAwait(false)` on every await, as in the client.
- **C# style:** Allman braces, 4-space indent, `this.` on instance members, no `var`. Test names read `Method_Scenario_Expectation`, and test doubles are named `Fake...`.
- **LF line endings** (`.gitattributes`); CI runs on Linux.
