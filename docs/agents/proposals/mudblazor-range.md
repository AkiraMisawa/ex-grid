# Proposal: the Definition of Done's rows for ADR-0205's MudBlazor range and floor

Written by the background agent on `claude/mudblazor-range-floor`, which implemented ADR-0205's
"The Wrappers' MudBlazor range". A background agent does not edit `docs/definition-of-done.md`, so
the text below is for the orchestrator to commit, or to put in front of the user where it is more
than wording.

What the branch changed, so the rows can name it:

- `src/ExGrid.MudBlazor`, `src/ExSheet.MudBlazor` and `src/ExPivot.MudBlazor` reference MudBlazor
  as `[9.0.0,10.0.0)`; each packed `.nuspec` declares `version="[9.0.0, 10.0.0)"` (NuGet writes the
  space).
- `tests/ExGrid.PackageSmoke/check.sh` reads each Wrapper's dependency list back as an exact list,
  with that range. `ExGrid.MudBlazor`'s list was read with two `grep`s before (the core exactly, and
  MudBlazor 9.0.0 present), which let a third dependency through; it is now read as the other
  packages' lists are, which is what PRE-3 already says it does.
- The floor run is recorded in `verification/2026-10-11-linux-mudblazor-floor/`.

## 1. Wording only: PV-1 and PRE-3

PRE-3 already states the range (restated 2026-10-11 with ADR-0205), but its list still says "from
9.0.0" for `ExPivot.MudBlazor`, and PV-1 says it outright. Both read as a floor with no ceiling, which
is what ADR-0205 replaced.

**PV-1**, the statement — replace

> `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor from 9.0.0.

with

> `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor
> `[9.0.0,10.0.0)` (ADR-0205).

and add `0205` to the row's closing ADR list: "(ADR-0059/0062/0064/0205)".

**PRE-3**, the statement — in the list, replace

> `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor from 9.0.0 (PV-1).

with

> `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor (PV-1).

The sentence after the list ("Each Wrapper takes MudBlazor in the one range ADR-0205 sets,
`[9.0.0,10.0.0)`") then carries the version for all three, as it already does for `ExGrid.MudBlazor`
and `ExSheet.MudBlazor`.

No other row names a MudBlazor version. SH-47 names "MudBlazor" with no version, and §23 none.

## 2. A decision, not wording: where the floor run lives in §22

ADR-0205 says "The floor is run once before the sign-off: the Wrappers' layer-3 specs against a
DemoHost pinned to MudBlazor 9.0.0, recorded under `verification/`." The Definition of Done has no
row and no step for it, so Step 7 would not ask for it, and nothing says which record counts or how
the DemoHost is pinned. Proposed, for the user to decide:

**(a) Step 4 gains a paragraph**, after the Server-host commands:

> **The Wrappers' MudBlazor floor** (ADR-0205). Once before the sign-off, the layer-3 tests that run
> under a Wrapper run against a DemoHost built with MudBlazor 9.0.0, the floor every Wrapper
> declares, on both hosts. Every other run stays on the version the demo pages pin. The demo pages
> are pinned for the run only and put back after it:
>
> ```sh
> sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\19.0.0"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> # the commands of verification/2026-10-11-linux-mudblazor-floor/results.md, on each host
> git checkout -- samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
> ```
>
> The tests are those whose file, `describe` or title names MudBlazor or `mud` (the `?chrome=mud`
> variants among them), and those that open a page under a Wrapper without saying so in their title;
> the record lists the latter by name.

**(b) §23 gains a row** (it is the Wrapper section; ExSheet's and ExPivot's Wrappers have no section
of their own, and their floor is the same run):

> | **WR-10** | MUST | Each MudBlazor Wrapper works at the floor it declares: the layer-3 tests run
> under `ExGrid.MudBlazor`, `ExSheet.MudBlazor` and `ExPivot.MudBlazor` pass against a DemoHost
> built with MudBlazor 9.0.0, on both hosts (ADR-0205) | Step 4's floor run, recorded under
> `verification/` | zero failures; a failure that also fails at the demo pages' version is that
> test's, and is judged by the run at that version |

The open point for the user: **which commit the floor run must be made at.** ADR-0205 says "once
before the sign-off", not "at X". Read strictly, a run on an earlier commit satisfies it, however much
the Wrappers change after it. Two readings:

- **Once, at any commit before X** — what ADR-0205's words say. Cheap, and it catches a Wrapper
  written against an API MudBlazor added after 9.0.0, which is what a floor run is for.
- **At X, or on a commit whose Wrappers and demo pages are unchanged by X** — the ADR-0208 shape
  (a record holds while its code is unchanged), with the paths `src/*.MudBlazor/` and
  `samples/ExGrid.DemoPages/`. It would need ADR-0205 noted, since it narrows "once".

This branch's run is at the commit named in its `results.md`; under the first reading it already
discharges the step, under the second it is a dry run.

## 3. Found while doing it, for the orchestrator

- **The Wrappers' layer-2 suites already run at the floor.** `ExGrid.MudBlazor.Tests`,
  `ExSheet.MudBlazor.Tests` and `ExPivot.MudBlazor.Tests` reference the Wrapper projects and nothing
  else that brings MudBlazor, so NuGet resolves the lowest version the range allows, 9.0.0, as it did
  for the old `9.0.0` reference. The same holds for `tests/ExGrid.PackageSmoke`. Only the DemoPages
  (and the Docs Site) pin a later 9.x. The floor run is therefore a layer-3 question only, which is
  what ADR-0205 asks of it.
- **The root `README.md` still says "Beta"** and `0.1.0-beta.N` with `--prerelease` in its Install and
  Status sections. ADR-0205 asks the package readmes to be worded for any version, and this branch
  did that; the root readme is not packed, and describes the repository as it is today. It will be
  false at the `0.1.0` tag (Y may change only `verification/` and `docs/releases/`), so it has to be
  reworded on the release branch before X, or on `main` before the cut.
- **The Docs Site's Installation and Packages pages** say "Every version so far is a prerelease,
  `0.1.0-beta.N`" and show `--prerelease` in `Examples/Setup/install.sh`. True today; ADR-0205's "the
  site describes the latest stable version" makes them false once `0.1.0` is published, so they
  belong to the same rewording.
