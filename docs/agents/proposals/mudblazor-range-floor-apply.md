# The Wrappers' MudBlazor floor at 9.6.0: every ADR and Definition of Done edit

Branch `claude/mudblazor-range-floor`, 2026-10-12. The user decided
`docs/agents/proposals/mudblazor-range.md` on 2026-10-12:

1. **The floor is raised to 9.6.0**, option (B) in full.
2. **ADR-0022's rule covers the Wrappers' MudBlazor floor**, and ADR-0022 gains a dated note that a `0.x`
   prerelease line may raise it. The Wrappers keep citing ADR-0022, with ADR-0205.
3. **The floor run goes into the Definition of Done** as the proposal's §6(a) Step 4 paragraph and §6(b)
   WR-10 row, with F = 9.6.0. A record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are
   unchanged up to X (ADR-0208's shape), and ADR-0205's "once" gains a note saying so.
4. **Step 4's floor paragraph also runs each Wrapper's layer-2 suite** with MudBlazor pinned to every
   published version of the declared range, recorded beside the floor run, and ADR-0205 says so.

Later the same day the user decided three more, numbered after the five on the release workflow. Two
bear on this list:

6. **A verified commit's tag is `verified/` followed by the name of the record's directory under
   `verification/`, and nothing else**, one tag per record directory; neither the platform nor the run's
   purpose is part of the rule. Step 4's floor paragraph tags the floor run's commit by it (edit 5).
7. **A MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that
   version only.** The floor run, layer 3 at 9.6.0, holds while its paths are unchanged. Edits 2, 5 and 6.

Decision (8), that the orchestrator pushes the tags of the runs already made once the release-mechanics
pull request merges, asks nothing of this list: no floor run has been made, so no floor tag exists.

Then two more, the same day:

9. **A layer-2 run at a 9.x published after the floor record is a run of its own.** Its record goes in its own
   `verification/<date>-<name>/` directory. That run tags the commit it verified `verified/<that directory's
   name>`, as the release-mechanics list's decisions (4) and (6) say for every run. WR-10's entry in
   `verification/by-hand.json` names that record (commit, tag, path, verdict, the version it covered) besides
   the floor record. The release check reads every record WR-10's entry names: the floor record's paths hold
   since its commit, and each later record's tag names its commit. Edits 2, 5 and 6; and edits 2, 5 and 8 name
   the later records among what a record carries forward, in notes credited to this decision.
10. **An unlisted version is not asked for.** The release check reports it as out of the check because it is
    unlisted. Edits 2, 5 and 6, each in a dated note beside decision (7)'s.

Besides, as the rule every record checked by hand already lives under (ADR-0208, and decision (3) for the floor
record), not a new decision: **a later record holds only while the Wrappers' and the demo pages' paths are
unchanged between its commit and X**, exactly as the floor record is held. Edit 5 says it, not credited to the
user.

What a background agent may edit is on the branch, in the commit that adds this file: the three Wrapper
projects at `[9.6.0,10.0.0)`, `check.sh`, the three Wrapper readmes, the Docs Site's Installation page,
the three Change Notes as Breaking, the comments naming the floor, and the layer-2 tests named for
WR-10. This file holds the rest: the edits to `docs/adr/` and `docs/definition-of-done.md`. Nothing here
edits `CONTEXT.md`; no new term was needed.

Each edit is an exact old text and its new text, in whole lines with the target's own indentation,
checked against the branch tip (generated and checked by script): every old text occurs once in its
file, and the list applies in order without one edit spoiling another's match. Edits 4, 6 and 7 replace
a whole table row, which is one line.

Two things the edits rely on, found while writing them:

- **The path check needs the glob spelled out.** `git diff --quiet A B -- 'src/*.MudBlazor/'` and
  `-- 'src/*.MudBlazor'` exit 0 at this branch over `origin/main`, though the branch changed six files
  there: a pathspec with a wildcard is matched against each file's whole path, never as a directory
  whose files it holds. `':(glob)src/*.MudBlazor/**'` matches all 62 tracked files below the three
  Wrappers at the branch tip, and is what Step 4's command uses (edit 5).
- **Step 4's layer-2 commands were run as written** (edit 5), but for `nix develop` given the worktree's
  flake, at `6a868089`: nuget.org listed 9.6.0 to 9.11.0, and the three Wrappers' suites passed whole at
  each, every run reading back the version it was pinned to. The new WR-10 tests fail below the floor.
  Both are in the floor record's §7 (`verification/2026-10-12-linux-mudblazor-floor/results.md`); they
  check the commands and the tests, and are not the record Step 4 asks for. What edit 5 now adds around
  them was not run: the record's directory `R`, the commit `C`, a log per version naming the commit, and
  the tagging step of decision (6). Nor was the later layer-2 run's procedure of decision (9): its own `C`
  and `R`, its record named in `laterRecords`, and its tag. No floor run has been made, so there is nothing
  to tag.

## Not settled by the decisions, for the orchestrator

- **The floor run itself is not made.** It needs installed Chrome and Edge, `chrome-150`'s Wrapper tests and
  both hosts, at the floor; this container has neither browser. ED-30's intermittent third composition
  (the proposal's §2.3 and §7) still stands between any floor run and a pass under WR-10, unless it is
  fixed or shown to fail at the demo pages' version.
- **After the first version without a suffix**, what raising a floor needs: ADR-0022's note scopes itself
  to a `0.x` prerelease line and says it does not decide that.

## Settled since, by the user's later decisions of 2026-10-12

- **Where a later layer-2 run's commit must be.** By ADR-0208's rule, under which every record checked by hand
  lives, and as the floor record is held: its record holds only while the Wrappers' and the demo pages' paths are
  unchanged between its commit and X. Not a new decision, so edit 5 says it uncredited; the release-mechanics
  list's check refuses a later record whose paths changed.
- **"Carried forward" names the later records too** (decision (9)): edits 2, 5 and 8 gain a dated note credited
  to it, beside the notes of decisions (3) and (4), which stand as written.
- **The release workflow's path check reads the floor record** (that day's decision (5) on the release
  workflow): WR-10's floor record is indexed in `verification/by-hand.json` with its paths,
  `':(glob)src/*.MudBlazor/**'` and `samples/ExGrid.DemoPages/`, and the workflow refuses a tag whose
  Wrappers or demo pages changed since the floor run. The index and the check are the release-workflow
  branch's to build, and its edit 24 states the check in ADR-0208. Edit 8 here does not restate it: it
  only points from ADR-0208's rule to ADR-0205's exception.
- **Which paths hold the layer-2 runs' record.** Decision (5) indexes the floor record with those two
  paths alone. The layer-2 runs are recorded in it (this file's decision (4)), so the edits give them the
  same paths; `tests/*.MudBlazor.Tests/` does not join.
- **A 9.x published after the record and before X** (decision (7)): it asks for the layer-2 runs at that
  version only, and the floor run, layer 3 at 9.6.0, holds while its paths are unchanged. Edits 2, 5 and 6
  say it, each in a dated note. That the release workflow refuses a tag whose records lack such a version is
  the release-workflow branch's check (its edit 24), which that branch writes in notes of the check's own,
  not credited to the user; edit 2 points to it in a note of that kind.
- **Where a later 9.x's layer-2 run is written, and which commit it names** (decision (9)). It was open here:
  edit 5 had read decision (7) as putting the run in the floor record's own directory, one log per version,
  and WR-10's entry adding the version to those it names, so that the later run's commit had no tag and the
  release check did not ask for it. Decision (9) settles it otherwise: the run is a run of its own, in a
  directory of its own, tagged `verified/` and that directory's name, and WR-10's entry names its record
  besides the floor record; the release check reads every record the entry names. Edits 2, 5 and 6 say it in
  dated notes, and edit 5's procedure makes the run that way. How the entry names it (`laterRecords`) is the
  release-workflow branch's index.
- **The floor run's tag** (decision (6)): the run's record goes in a directory of its own under
  `verification/`, and the run tags the commit it verified `verified/` followed by that directory's name
  and pushes it. WR-10's entry in `verification/by-hand.json` names the commit, the tag, the record's
  path, the verdict and the MudBlazor versions its layer-2 runs covered. Edit 5.
- **ADR-0208's "every automated layer still runs in full".** The floor run and its layer-2 runs are
  automated runs a record carries forward. ADR-0205's "once" note now says that this is an exception to
  ADR-0205's own note of 2026-10-11 under "After `0.1.0`" and to ADR-0208's rule (edit 2), and ADR-0208
  gains a pointer to it (edit 8).

## The edits, in the order they apply

1. **ADR-0022: a note on "Raising the floor is a breaking change".** Decision (2): ADR-0022's rule covers the Wrappers' MudBlazor floor, and a `0.x` prerelease line may raise it. Its precedent is the ADR's rewrite of 2026-09-25, which raised the runtime floor under a published prerelease, as the correction of that day records.
2. **ADR-0205, "The Wrappers' MudBlazor range": the floor raised to 9.6.0, what "once" means, and a later 9.x.** Decisions (1), (3), (4), (7), (9) and (10). The bullets of 2026-10-11 change only the range and the version the floor is run at; what restore does below the floor (NU1605) is said in the dated note that follows, with the raise. A floor record holds while the Wrappers and the demo pages are unchanged up to X, an exception to the note of 2026-10-11 under "After `0.1.0`" and to ADR-0208's "every automated layer still runs in full"; layer 2 runs across the declared range beside the floor run; a 9.x published after the record and before X asks for the layer-2 runs at that version only, the floor run holding while its paths are unchanged; and that layer-2 run is a run of its own, with a record and a tag of its own, which WR-10's entry names besides the floor record. That the release workflow refuses a tag without those runs is a dated note of its own, not credited to the user, as the release-mechanics list writes its check: a pointer to ADR-0208's check of WR-10's records, which that list's edit 24 states. Edit 2 does not state the check.
3. **ADR-0205, Consequences: the range the package check, PV-1 and the readmes state.** Decision (1), and, in a dated note of 2026-10-12, decisions (3) and (4): where the floor run and layer 2 across the range are run and judged.
4. **PRE-3: the range named, and `ExPivot.MudBlazor`'s entry.** Decision (1); the proposal's §5 wording with F = 9.6.0. The row is one line, so the block is the whole row.
5. **Step 4: the Wrappers' MudBlazor floor, and layer 2 across the range.** Decisions (3) and (4): the proposal's §6(a) paragraph with F = 9.6.0, the layer-2 runs of §6(c), and the rule that a record holds while its code is unchanged. Decision (6): the record in a directory of its own, and the verified commit tagged `verified/` followed by that directory's name and pushed, with WR-10's entry in `verification/by-hand.json`. Decision (7): a later 9.x asks for the layer-2 runs at that version only. Decision (9): that run is a run of its own, recorded in a directory of its own and tagged by it, and WR-10's entry names its record besides the floor record, carried forward as the floor record is; ADR-0208's rule, uncredited: it holds while its paths are unchanged since its commit. Decision (10): an unlisted version is not asked for. Inserted after the Server-host commands' Discharges paragraph, which stands as written; a dated note after it leaves WR-10 to the floor run.
6. **§23: WR-10, the floor every Wrapper declares.** Decision (3): the proposal's §6(b) row with F = 9.6.0, the layer-2 runs of decision (4), the record's holding rule, and decisions (7), (9) and (10) in dated notes.
7. **PV-1: MudBlazor's range.** Decision (1); the proposal's §5 wording with F = 9.6.0. The row is one line, so the block is the whole row.
8. **ADR-0208: a pointer to the one automated run a record carries forward.** Decision (3): the floor run and its layer-2 runs are the exception ADR-0205's notes of 2026-10-12 state; decision (9), in a dated note: a later layer-2 run's record is carried forward too. Only the pointer: the release workflow's check of WR-10's records is the release-mechanics list's edit 24, whose old text this edit's does not touch.

### 1. docs/adr/0022-packages-target-net10-and-run-on-everything-newer.md

Old:

`````text
- **Raising the floor is a breaking change.** It happens only as a deliberate decision — a
  major version bump and a rewrite of this ADR, as this one was — never as a side effect of
  wanting a newer API.
`````

New:

`````text
- **Raising the floor is a breaking change.** It happens only as a deliberate decision — a
  major version bump and a rewrite of this ADR, as this one was — never as a side effect of
  wanting a newer API.
  *(2026-10-12, decided with the user, with [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
  note of the same day. **This rule covers the MudBlazor floor of the three Wrappers**, which take
  the one dependency each adds by this ADR's rule: its lowest version, resolved upward by the
  application. **A `0.x` prerelease line may raise a floor without a major version bump**, as a
  deliberate decision written in an ADR and told to Consumers by a Breaking Change Note. No version
  without a suffix has been published, so a raise reaches only `0.1.0-beta.N` Consumers, whom
  [ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)
  already tells that a beta may break between numbers; SemVer promises nothing across `0.x`; and
  this ADR's rewrite of 2026-09-25 raised the runtime floor under a published prerelease, as its
  correction of that day records. The first raise made under this note is the Wrappers' MudBlazor
  floor, from 9.0.0 to 9.6.0, because below 9.6.0 they are wrong in ways the screen does not show
  (ADR-0205). This note does not decide what a raise needs once a version without a suffix has been
  published.)*
`````

### 2. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

`````text
- **Each Wrapper declares MudBlazor `[9.0.0,10.0.0)`.** A Consumer on MudBlazor 10 is told at restore
  (NU1608), as `Apache.Arrow`'s `[23,24)` already tells (ADR-0065).
- **The floor is run once before the sign-off**: the Wrappers' layer-3 specs against a DemoHost pinned to
  MudBlazor 9.0.0, recorded under `verification/`. Every other run stays on the version the DemoPages
  pin.
`````

New:

`````text
- **Each Wrapper declares MudBlazor `[9.6.0,10.0.0)`.** A Consumer on MudBlazor 10 is told at restore
  (NU1608), as `Apache.Arrow`'s `[23,24)` already tells (ADR-0065).
  *(2026-10-12, decided with the user: the floor was 9.0.0, and is raised to 9.6.0. The Wrappers'
  layer-3 tests, run that day on Playwright's Chromium against a DemoHost at 9.0.0, failed on both
  hosts; bisected over 9.0.0 to 9.9.0, the lowest version at which they passed, but for two failures
  that are not MudBlazor's, was 9.6.0. Below 9.6.0 the filter panel's two operators have no role and
  no name on the element that takes the keyboard (MudBlazor #13080 put them there), and below 9.2.0
  ExPivot's Field Settings… opens with the keyboard on `<body>` whenever its focus request comes before
  MudSelect's second render (#12837 ended it). The Wrappers' layer-2 suites, run at every published
  9.x, found a third inside the old range and above its floor: from 9.3.0 to 9.5.0 a refused filter
  operand keeps Apply unavailable and loses its words (#12892, undone by #13312). ExPivot's and
  ExSheet's own selects, which show their text through `ToStringFunc`, are named from 9.4.0. Each is a
  Wrapper wrong in a way the screen does not show. Holding the Wrappers at 9.0.0 instead would have
  copied MudBlazor's ARIA wiring into them, still partial below 9.4.0, and bound every later Wrapper
  change to 9.0.0 as well as to the latest 9.x. The raise costs a Consumer on 9.0 to 9.5 one minor
  upgrade within 9: one that references an earlier 9.x is stopped at restore (NU1605, an error by
  default), where one on MudBlazor 10 is told (NU1608, a warning). The three Change Notes say so as
  Breaking. The Wrappers take their floor by ADR-0022's rule; ADR-0022's note of this date says why a
  `0.x` prerelease line may raise it. A layer-2 test in each Wrapper, named for WR-10, pins the role and
  name on the focusable element of every MudSelect it draws, and another pins a refused operand's words,
  so CI holds the floor on every push. The record is `verification/2026-10-12-linux-mudblazor-floor/`;
  it is not the floor run below. The options weighed are in `docs/agents/proposals/mudblazor-range.md`.)*
- **The floor is run once before the sign-off**: the Wrappers' layer-3 specs against a DemoHost pinned to
  the floor every Wrapper declares, recorded under `verification/`. Every other run stays on the version
  the DemoPages pin.
  *(2026-10-12, decided with the user: "once" is a record that still holds at X. A floor run's record
  holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from the commit it was
  made at up to X, as a row checked by hand holds while its code is unchanged (ADR-0208); a change to
  either asks for the run again, at the floor then declared. The run is the Definition of Done's Step 4
  floor paragraph, on both hosts in installed Chrome and Edge, and WR-10 judges it. The record of
  2026-10-12 is a bisection on Playwright's bundled Chromium and does not discharge it. **The floor run,
  and the layer-2 runs recorded with it (the next notes), are the one automated run a record carries
  forward**: an exception to this ADR's note of 2026-10-11 under "After `0.1.0`", that every automated
  step of §22 still runs on every such version, and to ADR-0208's "every automated layer still runs in
  full on every candidate and every stable tag". The release workflow refuses a tag whose Wrappers or
  demo pages changed since their record, as it refuses one for a row checked by hand. Every other
  automated step still runs on every candidate and every version without a suffix.)*
  *(2026-10-12, decided with the user: a layer-2 run at a 9.x published after the floor record is a run
  of its own, and its record, which WR-10's entry names besides the floor record, is carried forward
  too.)*
  *(2026-10-12, decided with the user: beside the floor run, each Wrapper's layer-2 suite runs with
  MudBlazor pinned in its test project to every published version of the declared range, and is
  recorded with the floor run. A run at the floor alone cannot see what breaks between the ends of the
  range: the refused operand of 9.3.0 to 9.5.0 lay inside the old one. Layer 2 across the range can,
  for markup and a component's state. It sees no focus and no timing, so it adds to the floor run and
  replaces nothing.)*
  *(2026-10-12, decided with the user: **a MudBlazor 9.x published after the floor record and before X
  asks for the layer-2 runs at that version only.** The floor run (layer 3 at 9.6.0) holds while its paths
  are unchanged.)*
  *(2026-10-12, decided with the user: **an unlisted version is not asked for.** The release check reports
  it as out of the check because it is unlisted.)*
  *(2026-10-12, decided with the user: **a layer-2 run at a 9.x published after the floor record is a run
  of its own.** Its record goes in its own `verification/<date>-<name>/` directory, and the run tags the
  commit it verified `verified/` followed by that directory's name, as every run does. WR-10's entry in
  `verification/by-hand.json` names that record (commit, tag, path, verdict, the version it covered)
  besides the floor record. The release check reads every record WR-10's entry names: the floor record's
  paths hold since its commit, and each later record's tag names its commit.)*
  *(2026-10-12, how the release workflow holds the notes above: it refuses a tag when no record WR-10's
  entry names covers such a version, by
  [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)'s
  check of WR-10's records.)*
`````

### 3. docs/adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md

Old:

`````text
- The package readmes are reworded for any version before `rc.1`. `check.sh`, PV-1 and the three Wrapper
  readmes state `[9.0.0,10.0.0)`.
`````

New:

`````text
- The package readmes are reworded for any version before `rc.1`. `check.sh`, PV-1 and the three Wrapper
  readmes state `[9.6.0,10.0.0)` *(`[9.0.0,10.0.0)` until the note of 2026-10-12 above)*.
  *(2026-10-12, decided with the user: the Definition of Done's Step 4 runs the floor and layer 2 across
  the range, and WR-10 judges both.)*
`````

### 4. docs/definition-of-done.md

Old:

`````text
| **PRE-3** | MUST | No shipped package takes a package dependency its rows do not name, and `ExGrid` takes no third-party one: its `.nuspec` declares exactly `Microsoft.AspNetCore.Components.Web` at ADR-0022's floor (10.0.0) and `ExGrid.Data` at exactly the same version (ADR-0019; ADR-0130, DA-18). Every dependency on a package of the family is on exactly the same version (ADR-0042), and the other nine declare what these rows state: `ExGrid.MudBlazor` — `ExGrid` and MudBlazor (PRE-4, SH-47, DA-1); `ExGrid.Data` — nothing (DA-1); `ExGrid.Data.Arrow` — `ExGrid.Data` and `Apache.Arrow` within a stated range (DA-1); `ExSheet.Engine` — nothing (SH-1); `ExSheet` — `ExSheet.Engine` and `ExGrid` (SH-1); `ExSheet.MudBlazor` — `ExSheet`, `ExGrid.MudBlazor` and MudBlazor (SH-47); `ExPivot.Engine` — `ExGrid.Data` (PV-1); `ExPivot` — `ExPivot.Engine` and `ExGrid` (PV-1); `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor from 9.0.0 (PV-1). Each Wrapper takes MudBlazor in the one range [ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md) sets, `[9.0.0,10.0.0)`. *(Restated 2026-10-11, decided with the user. It said "`ExGrid` has no package dependency", checked with `dotnet list … package` for "no top-level package other than framework references", which a correct tree fails twice: `Microsoft.AspNetCore.Components.Web` is a package reference, at the floor ADR-0022 fixes, and `ExGrid.Data` has been one since ADR-0130. What it guarded — no third-party dependency in the core — is unchanged. Widened the same day to the ten packages, ADR-0200, with the Wrappers' MudBlazor range ADR-0205 decided.)* | `nix develop -c tests/ExGrid.PackageSmoke/check.sh`, which packs the ten packages and reads each `.nuspec`'s dependencies against these lists | it ends `package check: <version> passed` |
`````

New:

`````text
| **PRE-3** | MUST | No shipped package takes a package dependency its rows do not name, and `ExGrid` takes no third-party one: its `.nuspec` declares exactly `Microsoft.AspNetCore.Components.Web` at ADR-0022's floor (10.0.0) and `ExGrid.Data` at exactly the same version (ADR-0019; ADR-0130, DA-18). Every dependency on a package of the family is on exactly the same version (ADR-0042), and the other nine declare what these rows state: `ExGrid.MudBlazor` — `ExGrid` and MudBlazor (PRE-4, SH-47, DA-1); `ExGrid.Data` — nothing (DA-1); `ExGrid.Data.Arrow` — `ExGrid.Data` and `Apache.Arrow` within a stated range (DA-1); `ExSheet.Engine` — nothing (SH-1); `ExSheet` — `ExSheet.Engine` and `ExGrid` (SH-1); `ExSheet.MudBlazor` — `ExSheet`, `ExGrid.MudBlazor` and MudBlazor (SH-47); `ExPivot.Engine` — `ExGrid.Data` (PV-1); `ExPivot` — `ExPivot.Engine` and `ExGrid` (PV-1); `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor (PV-1). Each Wrapper takes MudBlazor in the one range [ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md) sets, `[9.6.0,10.0.0)`. *(Restated 2026-10-11, decided with the user. It said "`ExGrid` has no package dependency", checked with `dotnet list … package` for "no top-level package other than framework references", which a correct tree fails twice: `Microsoft.AspNetCore.Components.Web` is a package reference, at the floor ADR-0022 fixes, and `ExGrid.Data` has been one since ADR-0130. What it guarded — no third-party dependency in the core — is unchanged. Widened the same day to the ten packages, ADR-0200, with the Wrappers' MudBlazor range ADR-0205 decided.)* *(2026-10-12, decided with the user: the range's floor is 9.6.0, raised from 9.0.0 (ADR-0205's note of that date), and `ExPivot.MudBlazor`'s entry no longer says "from 9.0.0", which read as a floor with no ceiling.)* | `nix develop -c tests/ExGrid.PackageSmoke/check.sh`, which packs the ten packages and reads each `.nuspec`'s dependencies against these lists | it ends `package check: <version> passed` |
`````

### 5. docs/definition-of-done.md

Old:

`````text
Discharges UX-2..11/15/16, VZ-1/10, ED-2/3/4/9/11, KB-1/8/11/12/28..32, CP-4/5/6/10/14, BIG,
PST-3/5, CON-1..6/8, DOM, ST-3, FN-21, and §23's layer-3 rows — the Wrapper's specs run in the
same command, against the proof-of-concept page.
`````

New:

`````text
Discharges UX-2..11/15/16, VZ-1/10, ED-2/3/4/9/11, KB-1/8/11/12/28..32, CP-4/5/6/10/14, BIG,
PST-3/5, CON-1..6/8, DOM, ST-3, FN-21, and §23's layer-3 rows — the Wrapper's specs run in the
same command, against the proof-of-concept page.
*(2026-10-12, decided with the user: §23's layer-3 rows but WR-10, which the floor run below
discharges.)*

**The Wrappers' MudBlazor floor** ([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md), WR-10).
*(Added 2026-10-12, decided with the user, when the floor was raised to 9.6.0.)* Once before the
sign-off, the layer-3 tests that run under a Wrapper run against a DemoHost built with the MudBlazor
every Wrapper declares as its floor, 9.6.0, on both hosts, in Chrome and in Edge, and at 150 % where
`chrome-150` runs some of them. Every other run stays on the version the demo pages pin. The run is made
at a commit, `C` below, from a clean tree, and its record goes in a directory of its own under
`verification/`, `R` below. The demo pages are pinned for the run only, both hosts are built from
clean, and the version each will load is read back before the run:

```sh
C=$(git rev-parse HEAD)            # the commit the run verifies
R=verification/<date>-<name>       # the record's own directory
mkdir -p "$R"
sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\19.6.0"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
rm -rf samples/ExGrid.DemoHost/bin samples/ExGrid.DemoHost/obj/Debug \
       samples/ExGrid.DemoHost.Server/bin samples/ExGrid.DemoHost.Server/obj/Debug
nix develop -c dotnet build samples/ExGrid.DemoHost
nix develop -c dotnet build samples/ExGrid.DemoHost.Server
nix develop -c dotnet build samples/ExGrid.DemoApi
# both name 9.6.0
strings -el samples/ExGrid.DemoHost/bin/Debug/net10.0/wwwroot/_framework/MudBlazor.*.wasm | grep -A1 '^FileVersion'
strings -el samples/ExGrid.DemoHost.Server/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'

cd tests/ExGrid.Browser
G="[Mm]ud|is not the grid's \(KB-35|apply the condition \(KB-31, SRV-5\)|Escape, Escape straight after an Inner Popup|a menu taking the keyboard a round trip late|with and without a Wrapper|SH-46/DC-59: the Selection's outline|the palette reaches the pane|RI-3: every page opens|inspectors\.spec\.mjs|WR-7a|DC-58: a bold number that fits"
# every project: chrome, msedge, and chrome-150's Wrapper tests
npx playwright test --grep "$G" 2>&1 | tee "../../$R/mudblazor-floor.log"
EXGRID_HOSTING=server npx playwright test --grep "$G" 2>&1 \
  | tee "../../$R/mudblazor-floor-server.log"
cd ../..
git checkout -- samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
```

`G` is the tests that run under a Wrapper: those whose file, `describe` or title names MudBlazor or
`mud` (the `?chrome=mud` variants among them), and, by name, those that open a page under a Wrapper
without saying so. A test of the second kind joins `G` when it is written. On a machine without GNU
`strings`, each `MudBlazor` file's version is read back another way; it is never assumed.

Beside it, each Wrapper's layer-2 suite runs with MudBlazor pinned in its test project to every
published version of the declared range: each 9.x from 9.6.0 on nuget.org at the time of the run, which
the record names. The pin is the nearest reference, so NuGet takes it over the Wrapper's range, and the
version each run loaded is read back. Layer 2 sees markup and a component's state, not focus or timing:
it adds to the floor run and replaces nothing. Each version's output goes to a log of its own, which
names the commit it ran at.

```sh
versions=$(curl -s https://api.nuget.org/v3-flatcontainer/mudblazor/index.json \
  | grep -oE '"9\.[0-9]+\.[0-9]+"' | tr -d '"' | awk -F. '$2 >= 6' | sort -V)
for v in $versions; do
  for t in ExGrid.MudBlazor.Tests ExSheet.MudBlazor.Tests ExPivot.MudBlazor.Tests; do
    sed -i "s|</Project>|  <ItemGroup><PackageReference Include=\"MudBlazor\" Version=\"$v\" /></ItemGroup>\n</Project>|" \
      tests/$t/$t.csproj
    echo "== $t, MudBlazor $v, at $(git rev-parse HEAD)"
    nix develop -c dotnet test tests/$t
    strings -el tests/$t/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'   # names $v
    git checkout -- tests/$t/$t.csproj
  done 2>&1 | tee "$R/mudblazor-layer2-$v.log"
done
```

**The run tags the commit it verified.** *(2026-10-12, decided with the user: a verified commit's tag
is `verified/` followed by the name of the record's directory under `verification/`, and nothing else,
one tag per record directory.)* The record names `C` in full. In the same commit as the record, WR-10's
entry in `verification/by-hand.json` names the commit, the tag, the record's path, the verdict and the
MudBlazor versions its layer-2 runs covered, and empties `laterRecords`: a new floor run's layer-2 runs
cover every published version of the range, and earlier later records were made on the Wrappers it
replaces. The session that made the run then pushes the tag:

```sh
git add "$R" verification/by-hand.json
git commit -m "WR-10: the Wrappers' MudBlazor floor run"
git tag -a "verified/${R#verification/}" "$C" -m "The commit the Wrappers' MudBlazor floor run verified"
git push origin "verified/${R#verification/}"
```

**The floor run's record, with the layer-2 runs beside it, holds while `src/*.MudBlazor/` and
`samples/ExGrid.DemoPages/` are unchanged from the commit it was made at up to X** (ADR-0205's note of
2026-10-12, the shape of ADR-0208's rows checked by hand). It is the one automated run of this step that
a record carries forward, an exception to the note of 2026-10-11 above. *(2026-10-12, decided with the
user: a later layer-2 run's record, a run of its own, is carried forward too.)* Whether a record still holds is
read with git, from the commit its tag names, the glob spelled out so that it reaches every file below
each Wrapper:

```sh
git diff --quiet <the record's tag> X -- ':(glob)src/*.MudBlazor/**' samples/ExGrid.DemoPages/ \
  && echo "the floor record holds"
```

A change to either path asks for the floor run and the layer-2 runs again, at the floor then declared.
*(2026-10-12, decided with the user: **a MudBlazor 9.x published after the floor record and before X
asks for the layer-2 runs at that version only.** The floor run (layer 3 at 9.6.0) holds while its paths
are unchanged.)* *(2026-10-12, decided with the user: **an unlisted version is not asked for.** The
release check reports it as out of the check because it is unlisted.)*

**A layer-2 run at a later 9.x is a run of its own.** *(2026-10-12, decided with the user: its record
goes in its own `verification/<date>-<name>/` directory, and the run tags the commit it verified
`verified/` followed by that directory's name, as every run does. WR-10's entry in
`verification/by-hand.json` names that record (commit, tag, path, verdict, the version it covered)
besides the floor record. The release check reads every record WR-10's entry names: the floor record's
paths hold since its commit, and each later record's tag names its commit.)* The run is the loop above,
with `C`, `R` and `versions` set for it first:

```sh
C=$(git rev-parse HEAD)                  # the commit the run verifies
R=verification/<date>-<name>             # a directory of its own
mkdir -p "$R"
versions=<the new version>
```

In the same commit as its record, WR-10's entry names the record in `laterRecords`, and the floor record
stays in `record`; the session that made the run then pushes its tag. The later record holds, as every
record checked by hand does (ADR-0208) and as the floor record does, while `src/*.MudBlazor/` and
`samples/ExGrid.DemoPages/` are unchanged between its commit and X; a run made where they differ from X's
does not count.

```sh
git add "$R" verification/by-hand.json
git commit -m "WR-10: the Wrappers' layer-2 runs at MudBlazor $versions"
git tag -a "verified/${R#verification/}" "$C" -m "The commit the Wrappers' layer-2 runs at MudBlazor $versions verified"
git push origin "verified/${R#verification/}"
```
`````

### 6. docs/definition-of-done.md

Old:

`````text
| **WR-9** | MUST | The Wrapper's own suites hold the core's console rules: zero errors, zero page errors, zero warnings from ExGrid's or the Wrapper's code, no unhandled exception (CON-1/2/3/6) | the shared layer-3 fixture; layer 2's unobserved-exception handler | as the core's |
`````

New:

`````text
| **WR-9** | MUST | The Wrapper's own suites hold the core's console rules: zero errors, zero page errors, zero warnings from ExGrid's or the Wrapper's code, no unhandled exception (CON-1/2/3/6) | the shared layer-3 fixture; layer 2's unobserved-exception handler | as the core's |
| **WR-10** | MUST | Each MudBlazor Wrapper works at the floor it declares, 9.6.0, and across its declared range: the layer-3 tests run under `ExGrid.MudBlazor`, `ExSheet.MudBlazor` and `ExPivot.MudBlazor` pass against a DemoHost built with the floor's MudBlazor, on both hosts, in Chrome and in Edge; and each Wrapper's layer-2 suite passes with MudBlazor pinned to every published version of its declared range ([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)). *(Added 2026-10-12, decided with the user, when the floor was raised to 9.6.0: the Wrappers' layer-3 tests, run that day on Playwright's Chromium against a DemoHost at 9.0.0, failed on both hosts, and bisected over 9.0.0 to 9.9.0, the lowest version at which they passed, but for two failures that are not MudBlazor's, was 9.6.0. That record is not this row's floor run.)* | Step 4's floor run and its layer-2 runs, recorded under `verification/`. The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from the commit it was made at up to X (ADR-0205's note of 2026-10-12, the shape of ADR-0208). *(2026-10-12, decided with the user: a MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version only. The floor run (layer 3 at 9.6.0) holds while its paths are unchanged.)* *(2026-10-12, decided with the user: an unlisted version is not asked for; the release check reports it as out of the check because it is unlisted.)* *(2026-10-12, decided with the user: a layer-2 run at such a version is a run of its own, whose record goes in its own `verification/<date>-<name>/` directory, and the run tags the commit it verified `verified/` followed by that directory's name; WR-10's entry in `verification/by-hand.json` names that record besides the floor record; the release check reads every record the entry names.)* Between floor runs, Step 1 holds the floor on every run: the Wrappers' suites resolve 9.6.0, and their WR-10 tests pin the role and name on the focusable element of every MudSelect a Wrapper draws, and a refused operand's words | zero failures; a layer-3 failure that also fails at the demo pages' version is that test's, and is judged by the run at that version |
`````

### 7. docs/definition-of-done.md

Old:

`````text
| **PV-1** | MUST | `ExPivot.Engine` references `ExGrid.Data` exactly and no package. `ExPivot` references it and `ExGrid`, both exactly. `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor from 9.0.0. Nothing references ExPivot but its Wrapper, the demo pages and the tests (ADR-0059/0062/0064) | the package check (`tests/ExGrid.PackageSmoke/check.sh`) | as stated |
`````

New:

`````text
| **PV-1** | MUST | `ExPivot.Engine` references `ExGrid.Data` exactly and no package. `ExPivot` references it and `ExGrid`, both exactly. `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor `[9.6.0,10.0.0)` (ADR-0205). Nothing references ExPivot but its Wrapper, the demo pages and the tests (ADR-0059/0062/0064/0205) *(restated 2026-10-12, decided with the user: it said "MudBlazor from 9.0.0", a floor with no ceiling, which ADR-0205 replaced with a range whose floor is now 9.6.0)* | the package check (`tests/ExGrid.PackageSmoke/check.sh`) | as stated |
`````

### 8. docs/adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md

Old:

`````text
- **Every automated layer still runs in full on every candidate and every stable tag**: layers 1 to 3 on
  both hosts and both browsers, the soak, the package check. Only the rows a person runs are judged by
  their code. "A criterion that could not be verified is not passed" holds: a row whose code is
  unchanged since its record was verified on that code.
`````

New:

`````text
- **Every automated layer still runs in full on every candidate and every stable tag**: layers 1 to 3 on
  both hosts and both browsers, the soak, the package check. Only the rows a person runs are judged by
  their code. "A criterion that could not be verified is not passed" holds: a row whose code is
  unchanged since its record was verified on that code.
  *(2026-10-12, decided with the user: **one automated run is an exception**: the Wrappers' MudBlazor
  floor run, with the layer-2 runs recorded beside it, is carried forward by its record, as
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
  notes of that date under "The Wrappers' MudBlazor range" say.)*
  *(2026-10-12, decided with the user: a layer-2 run at a 9.x published after the floor record is a run
  of its own, and its record is carried forward too.)*
`````
