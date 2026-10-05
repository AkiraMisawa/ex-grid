# CI runs every layer, and layer 3 gates there, on Linux with the installed Chrome and Edge

[ADR-0026](./0026-layer-three-runs-on-playwright-against-the-installed-chrome.md) put layer 3 on
Playwright against the installed browsers and recorded, as a consequence, that it "does not gate
automatically, because there is no CI". Layers 1 and 2 gated only in the sense that a person ran
`dotnet test` before a push. [ADR-0019](./0019-one-repository-many-packages.md) had assumed
"CI verifies everything in one run" when it put every package in one repository, and nothing
ever built that CI.

What that cost was visible by 2026-09-24. Every layer-3 test written that day had run only on a
cloud container's bundled Chromium, headless, with two settings that are not the project's, and
`docs/implementation-status.md` carried a growing list of runs still owed on `chrome` and
`msedge`. A GitHub-hosted Ubuntu runner has both browsers installed, and on Linux the native
scrollbars take their width out of the layout, which is the property ADR-0026 needed.

## Decision

**GitHub Actions runs all three layers on every push to `main`, and on every pull request. All
three gate.** *(Until 2026-09-25 this also named `dev/claude-code`, the branch work was gathered
on before `main` took the merges. It was retired then, with the user's agreement: it held
nothing `main` did not, and a second integration branch would only fall behind.)*

- **Build, then layers 1 and 2**, on `ubuntu-latest`. The SDK comes from `global.json`, and the
  .NET 8 runtime is installed beside it, because the gating tests execute on `net8.0`
  ([ADR-0022](./0022-packages-target-net10-and-run-on-everything-newer.md)). *(Since 2026-09-25, when ADR-0022 moved the target to `net10.0`,
  the SDK's own runtime is the one they execute on, and no second runtime is installed.)* This is
  `dotnet test ExGrid.slnx`, the command the Definition of Done gates on, run with coverage on.
- **Layer 3 on the installed Chrome and Edge, headed**, under `xvfb`: the project's own
  `playwright.config.mjs`, both projects, unchanged. Headed matters for the reason ADR-0026
  gives: headless keeps overlay scrollbars on some platforms. A failure turns the run red.
  *(Since 2026-09-25 there are two such jobs. The second runs the same suite against the Blazor
  Server host (`EXGRID_HOSTING=server`, ADR-0019), and it gates too. That run is the Definition
  of Done's SRV-2, on both browsers. It is a job of its own, not a matrix entry, so the
  WebAssembly job keeps its check name.)*
  *(Since 2026-09-28 each host's run is split by browser and into two shards, each shard on a
  runner of its own. A job under each old name passes only when all of its shards did, so the
  checks keep their names. The suite is the same: this configuration, both projects, every spec.
  What changed is only which runner runs which files, `--project` and `--shard` choosing them. The
  reason was the time: the WebAssembly job took 24.3 minutes, Chrome and then Edge on one worker,
  and it was the wall clock of every push. A shard boundary is a file boundary, so nothing the
  suite shares within a run — the OS clipboard, the records, a spec file's booted app
  ([ADR-0056](./0056-layer-three-boots-once-per-spec-file.md)) — is split between runners.)*
  *(Since 2026-10-04, decided with the user, Chrome and Edge each take four shards and chrome-150
  two. At two each, those four shards were the wall clock: on run 37153502205 the WebAssembly
  ones took 20.4 to 22.0 minutes and the Server ones 12.7 to 17.0, while chrome-150's took 0.7
  to 4.0. The suite and the file boundary are unchanged.)*
  *(Since 2026-10-05, decided with the user, layer 3 drives the hosts as `dotnet publish -c
  Release` writes them, published once by a job of their own and taken by every runner, rather
  than each runner building them and starting them with `dotnet run`. Their environment stays
  Development, so what changed is how they are built. It was not done for time: on runs
  37244202854 (Debug) and 37244249352 (Release) the suite took 143.5 and 149.2 runner minutes
  and the slowest shard 12.9 and 12.7, the suite's time being the browser's and not the app's.)*
  - The **observational** specs still only record: each asserts that it measured something and
    never gates on the number (ADR-0026, "performance never gates").
  - The records a run writes — `console.json`, `metrics.json` — and any failure's trace are
    kept as the run's artifacts. They are not filed into `verification/` automatically, which
    stays a deliberate act. *(Since 2026-09-28 there is one artifact per shard, each holding the
    records of the files that shard ran.)*
- **The long run is weekly, and on demand**: the ten-minute soak (`EXGRID_SOAK=1`, MEM-5/6) on
  both browsers, and ST-1 at 10⁶ rows (`EXGRID_ST1_MILLION=1`). Too slow for every push, and
  what they catch — a leak, a drift at scale — does not arrive in a single commit.
- **Coverage is reported, never gated.** Microsoft's Testing Platform extension collects it from
  the shipped assemblies only — `ExGrid` and `ExGrid.MudBlazor` (the family's ten since
  2026-10-03, below) — and ReportGenerator merges the suites. Each run writes the table into its summary. Each push to `main` refreshes line
  and branch badges, and appends to a history file, on a `badges` branch that the README reads.
  No threshold: a number the build must reach invites tests written to reach it, and the
  criteria in the Definition of Done are what "tested" means here.
  *(Since 2026-10-03, decided with the user: "the shipped assemblies" are the family's ten
  packages, since ADR-0042 ships them together (#53). Naming only `ExGrid` and
  `ExGrid.MudBlazor` had left eight shipped assemblies uncounted. The figure is also reported
  per product: ExGrid (`ExGrid`, `ExGrid.MudBlazor`), ExSheet (`ExSheet`, `ExSheet.Engine`,
  `ExSheet.MudBlazor`), ExPivot (`ExPivot`, `ExPivot.Engine`, `ExPivot.MudBlazor`) and Data
  (`ExGrid.Data`, `ExGrid.Data.Arrow`), each by the exact assembly names, beside the total of
  all ten. The README shows five badges, the total and one per product, each giving line
  coverage alone (branch coverage is in the table they link to: on a badge it doubled the
  width, decided with the user the same day); they are shields.io endpoint files on the `badges` branch, because a generated
  badge cannot carry the product's name. The history gained a column pair per product, so
  the total's line steps where the eight were first counted.)*

## What CI does not replace

- **Windows.** VZ-14 — a native scrollbar that is a non-integer number of CSS pixels at 125% —
  exists only on a Windows desktop, and its test skips itself anywhere else. That stays a run by
  hand.
- **A real IME.** Nothing drives a Japanese IME's candidate window from a runner, so the claim
  that Enter confirming a candidate does not apply a filter stays a manual check.
- **The filed record.** Step 4's `verification/<date>-<platform>/` is still written by a
  person, from a run they watched; a CI artifact is evidence, not the record.

## Consequences

- **ADR-0026's "there is no CI" is replaced here**, and it points here, keeping what it said.
- **A red layer 3 is a real answer about Linux, not a flake.** The suite has no retries by
  design (ADR-0026), and CI keeps it that way.
- **Layer 3 now runs on a second machine's timing.** Tests that waited on an animation or a
  settle already wait by condition, not by sleep; one that does not will show itself here.
- **`AGENTS.md`, the README and `tests/ExGrid.Browser/README.md` say CI exists**, and the
  README carries the CI and coverage badges.
