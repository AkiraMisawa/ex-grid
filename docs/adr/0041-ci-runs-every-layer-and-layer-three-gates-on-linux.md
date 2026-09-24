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

**GitHub Actions runs all three layers on every push to `main` and `dev/claude-code`, and on
every pull request. All three gate.**

- **Build, then layers 1 and 2**, on `ubuntu-latest`. The SDK comes from `global.json`, and the
  .NET 8 runtime is installed beside it, because the gating tests execute on `net8.0`
  ([ADR-0022](./0022-packages-target-net8-and-run-on-everything-newer.md)). This is
  `dotnet test ExGrid.slnx`, the command the Definition of Done gates on, run with coverage on.
- **Layer 3 on the installed Chrome and Edge, headed**, under `xvfb`: the project's own
  `playwright.config.mjs`, both projects, unchanged. Headed matters for the reason ADR-0026
  gives: headless keeps overlay scrollbars on some platforms. A failure turns the run red.
  - The **observational** specs still only record: each asserts that it measured something and
    never gates on the number (ADR-0026, "performance never gates").
  - The records a run writes — `console.json`, `metrics.json` — and any failure's trace are
    kept as the run's artifacts. They are not filed into `verification/` automatically, which
    stays a deliberate act.
- **The long run is weekly, and on demand**: the ten-minute soak (`EXGRID_SOAK=1`, MEM-5/6) on
  both browsers, and ST-1 at 10⁶ rows (`EXGRID_ST1_MILLION=1`). Too slow for every push, and
  what they catch — a leak, a drift at scale — does not arrive in a single commit.
- **Coverage is reported, never gated.** Microsoft's Testing Platform extension collects it from
  the shipped assemblies only — `ExGrid` and `ExGrid.MudBlazor` — and ReportGenerator merges the
  three suites. Each run writes the table into its summary. Each push to `main` refreshes line
  and branch badges, and appends to a history file, on a `badges` branch that the README reads.
  No threshold: a number the build must reach invites tests written to reach it, and the
  criteria in the Definition of Done are what "tested" means here.

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
