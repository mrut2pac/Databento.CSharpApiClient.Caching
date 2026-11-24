# CLAUDE.md — Databento.CSharpApiClient.Caching

Guidelines for working on this repository with Claude Code or any AI coding assistant.

---

## Repo overview

A disk cache and Parquet archive on top of [Databento.CSharpApiClient](https://github.com/mrut2pac/Databento.CSharpApiClient) (.NET 8), so each day of Databento data is paid for once. It depends on the client's NuGet package and Parquet.Net, nothing else.

```
src/
  Databento.CSharpApiClient.Caching/            ← the library (not packaged yet)
tests/
  Databento.CSharpApiClient.Caching.UnitTests/  ← deterministic, no network
.github/workflows/ci.yml                        ← CI: build + unit tests on every push/PR
```

- **The client comes from NuGet**, as for any of its users (`PackageReference`, not a project reference). A client feature the package needs ships in a client release first, and the package then moves to that version.
- **Only the client's public API**: no `InternalsVisibleTo` from the client, no copies of its internals.
- **Not packable yet** (`IsPackable` false): the first release, 1.0.0, comes once the cached client, backup and restore, and live tests are in.

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

---

## Market data

- **Never commit or share real market data.** Databento's terms forbid redistributing the data it sells, and this repo will be public. No file Databento (or any vendor) delivered goes into the repo, a PR, an issue or a test as it came: not DBN, JSON, CSV or Parquet records, not responses pasted into tests.
- **Fixtures are invented.** Write records by hand, or keep a real file's layout and replace every price, size, count, sequence, statistic and trade time with invented values before it is committed, and say so in a `README.md` next to it. Symbols, instrument ids, record types and the folder files are format, not data, and may stay.

## Coding conventions

- **Dependencies:** the client and Parquet.Net only. No logging library: results come back as report objects, and progress through `IProgress<T>`.
- **Public surface stays small:** the cached client and its options, modes and event, the compactor, its options and report, and the day-check API are public; the archive format, naming, file handling and the client's download machinery (`Client/`) are `internal`.
- **The cached client's calls are the client's**, async and sync, same names and arguments plus a `CacheMode`, one set per schema the archive has columns for (`Client/CachedSchemas.cs`). A new schema is an archive schema, a row there and its calls.
- **Its unit tests talk to `FakeDatabento`**, an in-memory Databento behind the client's `IHttpTransport` (time series as JSON lines or a CBBO DBN stream, dataset range and conditions, symbology), so they exercise the real client. `CachedDatabentoClientCallTests.EveryPublicCall_IsCovered` fails when a call is added without its row in that test.
- **Batch fixtures mimic real folders:** `tests/.../Batch/Samples/` holds the layout of folders Databento made for small jobs (DBN and JSON, zstd or not, split by day, week or symbol, GLBX DBN v3, a never-listed symbol), with every market value invented (see its `README.md`) and the account id in the manifests' URLs replaced. The fake serves them as its jobs' files. What the real folders showed: no `symbology.json` (symbols come from each DBN file's metadata, or each JSON record with `map_symbols`), files cut by `ts_recv`'s UTC date, `split_size` of 1 to 10 GB only, and a submit naming an unlisted symbol accepted.
- **Time comes from `TimeProvider`** (in the options), never `DateTime.Now`, so tests drive it.
- **Invariant formatting:** `Invariant($"...")`, from the global `using static System.FormattableString`.
- **Async:** `ConfigureAwait(false)` on every await, as in the client.
- **C# style:** Allman braces, 4-space indent, `this.` on instance members, no `var`. Test names read `Method_Scenario_Expectation`, and test doubles are named `Fake...`.
- **LF line endings** (`.gitattributes`); CI runs on Linux.
