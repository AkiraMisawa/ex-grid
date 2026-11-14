# The Definition of Done signed off for 0.3.0

**X = `fb7b872a841ff3dcdba72ec54a95091091c53cd3`**, the commit `v0.3.0-rc.1` is tagged on. Signed off on
2026-11-14 UTC. Steps 0 to 3, 5 and 6 ran on 2026-11-13 UTC, which names this directory; the release run of
`v0.3.0-rc.1`, Step 4, began at 23:38 UTC that day, and its layer 3 ended at 00:18 UTC on 2026-11-14 (ADR-0205,
the Definition of Done's §22 Step 7).

X is 0.2.1's commit, `85a7ea06`, with what `main` took since: a grid told the Device Pixel of the window its
root is in, so one in a window the page opened is sharp on a display of another scale (#566, ADR-0090's note of
2026-11-12, issue #565); a Consumer's placement of a Selection of several ranges (#567, ADR-0050 item 4's note
of 2026-11-12, issue #561); the rule that a pull request shows what it changes (#568, a document); 0.2.1's own
sign-off record (#564); and the cut of `0.3.0-rc.1` (#569), which gathered the two Change Notes, moved ExGrid's
one new public member into its shipped baseline, and corrected two layer-3 readings (below, "Found during the
sign-off"). Outside the tests, the samples and the documents, the diff is 12 files, all ExGrid's: the Script
Core's reports, mount, view, Selection and Selection Summary (`packages/core`, eight files with its API report),
its crossing in `packages/blazor` and its React component's ref in `packages/react`, and `ExGrid`'s handle,
component, summary and shipped baseline in C#. No file of ExSheet, ExPivot, ExChart, ExBoard or the data
packages changed. Every step below ran at X. 0.2.1's record is
[`../2026-11-12-linux-signoff`](../2026-11-12-linux-signoff/results.md); Step 6 reads its numbers.

## Verdict

- **No MUST fails, and none reads `blocked`.** Of the 688 MUST criteria, 524 pass; 109 pass with a clause
  no test asserts, accepted as it stands; 24 have no test of their own, accepted as they stand; 11 are Known
  limitations carried from 0.2.0; FD-10 and PV-47 are withdrawn, with nothing left to judge; and RT-1 to RT-18
  are outside §34's scope. Every one of these but the first is the user's decision for 0.2.0, carried to 0.2.1
  on 2026-11-11 and to 0.3.0 by the user's decision of 2026-11-13, below.
- **Every SHOULD passes**: UX-10, DIR-4 and CH-14, and FN-20 with a clause no test asserts, accepted as it
  stands. None is accepted failing.
- **Every OBSERVATIONAL number is recorded with its comparison**, or is `not run` with its reason (Step 6):
  MEM-7 as Step 6 says, and PV-48 and LV-23, whose harnesses measured the report Window path ADR-0370
  section 3 ended. BIG-6, BIG-7, PST-6, PF-6, PF-7 and DOM-5, and MEM-6 at the soak's end, were measured by CI
  in the release run and stay in its records there, which this record does not copy (Step 4, Step 6).
- **§21 lists no open question, and no criterion reads `BLOCKED`.**
- **Every reservation of §21.11 still has its trigger unfired, or was settled with the feature that fired
  it** (below).
- **The rows once checked by hand are CI's** (ADR-0450): each ran in the release run of `v0.3.0-rc.1`, at X,
  and passed; `verification/by-hand.json` indexes none.
- **ADR-0042's prerequisites hold**: the public surface is ADR-0201's, and every
  `dotnet/src/*/PublicAPI.Unshipped.txt` holds only `#nullable enable` at X; the release workflow's check
  of a stable tag (ADR-0205) is the one 0.1.0, 0.2.0 and 0.2.1 were released by.

## The user's decision of 2026-11-13

0.2.1 was signed off two days before X against decisions the user made for 0.2.0 on 2026-11-06 and 2026-11-07
and carried on 2026-11-11, written in [its record](../2026-11-12-linux-signoff/results.md) under "The user's
decision of 2026-11-11". 0.3.0 is the first version of a new minor line, and two of those decisions name the
line they are owed in: DA-18 and ED-34 are Known limitations "owed in 0.2.x". Asked on 2026-11-13 how 0.3.0
treats them, the user chose (00:44 UTC, "0.3.x で対応予定に書き換える", rewrite them as owed in 0.3.x) to carry
every decision as it is and to move that one clause:

- **The 109 clauses no test asserts, and the 24 rows with no test of their own**, are accepted as they
  stand. Each row names what is not asserted, or the code that holds it, in the table below; each is a ticket
  since #557 (`docs/specs/signoff-0-2-0-gaps/`). They read `pass, accepted as is` and `accepted as is, no test`.
- **DA-18 and ED-34** stay Known limitations, said in 0.3.0's Release Notes as in 0.2.1's but each **owed in
  0.3.x**, where 0.2.0's and 0.2.1's notes said 0.2.x. Neither was taken up in 0.2.x: 0.2.1 is its last
  version, since `main` has gone on to 0.3.0 (ADR-0205).
- **PV-50, LV-24 to LV-28, LV-30, LV-31 and LV-34** stay Known limitations: their rewrite for the Leaf
  Aggregate boundary is owed with ExPivot.Engine's removal.
- **RT-1 to RT-18** are §34's, whose MUST criteria "gate a release claiming mobile support"; 0.3.0 claims Chrome
  and Edge on the desktop (its Release Notes, Supported), as 0.2.1 did.

**What X adds to the criteria.** The Definition of Done changed in two rows between 0.2.1's commit and X.
**DC-68** is new, a MUST: a Consumer's placement of a list of ranges (ADR-0050 item 4). It was judged fresh at
X, in layers 1 and 2 here, and passes with every clause asserted (the table). **VZ-16** gained the window the
root is in: its layer-2 clause, the report read and heard from a window that is not the script's, was judged
here, and its layer-3 clause, `popout.spec.mjs`, on the React host in the release run; it passes with every
clause asserted. The other 707 rows read as at 0.2.1's commit. Every line number in the table was carried from
0.2.1's record to X by the diff between the two commits: of 2,055 references, 158 are in files the diff
touched, and 157 of those hold the same text at their new line, 93 of them moved. The one line the diff itself
changed was read again at X: RR-8's, where the Scrollbar Gutter's report is now held among the grid's stops
as it begins (#566); it still invalidates a pass and writes nothing, as the row says.

**A defect of 0.2.1's record, corrected here.** That record carried the first line of each range of lines
(`file:a-b`) to its commit and left the last where 0.2.0's record had it, so the eight ranges in the three
files its diff had moved (`grid-view.ts`, `ExGrid.cs`, `ex-grid.css`: A11Y-12, MEM-3, RR-8, ASY-3, MK-9,
DC-56, FD-14 and LV-20) read with an end that was 0.2.0's line: RR-8's read `grid-view.ts:2377-2353`. Here
each end is carried from 0.2.0's commit to X by that diff, and each range holds at X the lines it held at
0.2.0's commit, RR-8's the four of the report as it stands now. No verdict rested on an end: each row's
evidence is named by its first line.

## Toolchain, OS and browsers

- **Local steps** (0 to 3, 5 and 6): a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Ubuntu
  24.04.5 LTS, Linux 6.18; .NET SDK 10.0.203 through nix, runtime 10.0.7; Node 26.12.1; Playwright 1.62.1;
  Chrome for Testing 153.0.8010.12 as `chrome`, headed under xvfb.
- **CI** (Step 4, the soak, ST-1 at 10⁶ and the rows once checked by hand, on its long run): release run
  [43528656529](https://github.com/akiramisawa/ex-grid/actions/runs/43528656529) on the tag `v0.3.0-rc.1`,
  GitHub's `ubuntu-24.04` runner images 20261111.370.1 and 20261111.127.1 (Ubuntu 24.04.5 LTS), .NET SDK
  10.0.404, Node 26.12.1, Playwright 1.62.1, headed under xvfb; Google Chrome 157.0.8103.50 and Microsoft Edge
  157.0.4341.46, a major version on from 0.2.1's run; the Windows and macOS desktop jobs on the hosted
  `windows-latest` (image `windows-2025-vs2026` 20261103.298.1, .NET SDK 10.0.403) and `macos-latest` (image
  `macos-26-arm64` 20261102.0473.1, macOS 26.7.2) runners, Chrome 156.0.8078.80 and Edge 156.0.4314.58 on macOS.

## Step 0 — Preconditions

| Row | Result | Record |
|---|---|---|
| PRE-1 | pass: the solution builds, 0 warnings, 0 errors | [`pre-1.log`](pre-1.log) |
| PRE-2 | pass: every `dotnet/src` project `net10.0`, no `LangVersion` | [`step0-static.log`](step0-static.log) |
| PRE-3 | pass: the NuGet package check, `0.0.0-smoke.1794537906 passed`, and the npm one, 23 packages taken as a Consumer takes them | [`pre-3.log`](pre-3.log), [`pre-3-npm.log`](pre-3-npm.log) |
| PRE-4 | pass: no core project references MudBlazor or Fluxor | [`pre-4.log`](pre-4.log), [`step3-static.log`](step3-static.log) |
| PRE-5 | pass: Chrome and Edge projects | [`step0-static.log`](step0-static.log) |
| PRE-6 | pass: `git status --porcelain -- dotnet/src/` empty | [`step0-static.log`](step0-static.log) |

## Steps 1 and 2 — Layers 1 and 2

**.NET.** One run of `ExGrid.slnx` with `--timeout 5m` per project: [`layers-1-2.log`](layers-1-2.log),
and one `.trx` per project beside it. **12,767 tests in 19 projects, 0 failed.** The skips are exactly the
ten explicit measurements Step 2 allows, read by name from the `.trx` files by `ExGrid.Release skips`
([`skips.log`](skips.log)), and run in Step 6. No project was stopped by its timeout (ASY-6), and CON-7's
`UnobservedTaskWatch`, which fails a suite on any unobserved exception, failed none. The five tests more than
0.2.1's record counts are three in ExGrid.Tests and two in ExGrid.Components.

| Project | Passed | Skipped | Total |
|---|---|---|---|
| ExGrid.Tests | 1,902 | 0 | 1,902 |
| ExGrid.Components | 323 | 0 | 323 |
| ExGrid.MudBlazor.Tests | 197 | 0 | 197 |
| ExGrid.Data.Tests | 594 | 2 | 596 |
| ExGrid.Data.Arrow.Tests | 164 | 1 | 165 |
| ExGrid.Data.Xlsx.Tests | 151 | 0 | 151 |
| ExSheet.Engine.Tests | 6,302 | 3 | 6,305 |
| ExSheet.Components | 38 | 0 | 38 |
| ExSheet.MudBlazor.Tests | 21 | 0 | 21 |
| ExSheet.Xlsx.Tests | 669 | 0 | 669 |
| ExPivot.Engine.Tests | 688 | 4 | 692 |
| ExPivot.Components | 23 | 0 | 23 |
| ExPivot.MudBlazor.Tests | 11 | 0 | 11 |
| ExChart.Engine.Tests | 597 | 0 | 597 |
| ExChart.Components | 53 | 0 | 53 |
| ExBoard.Tests | 219 | 0 | 219 |
| ExGrid.DemoApi.Tests | 159 | 0 | 159 |
| ExGrid.Docs.Generator.Tests | 431 | 0 | 431 |
| ExGrid.Release.Tests | 215 | 0 | 215 |
| **All** | **12,757** | **10** | **12,767** |

**The Script Core.** `npm run typecheck && npm run lint && npm test` at the root:
[`script-core-layers-1-2.log`](script-core-layers-1-2.log), exit 0. **14,240 tests in 28 packages, 0
failed**: 14,239 passed, and the one skip is ST-1's 10⁶-row case, which only `EXGRID_ST1_MILLION=1` runs
(Step 5). That is 22 more than 0.2.1's record counts, 21 in `packages/core` and one in `packages/blazor`. The layer-1
cases under `tests/cases/` ran there and under xUnit, `selection/placed-ranges.json` (DC-68) among them.

## Step 3 — Static inspection

The greps: [`step3-static.log`](step3-static.log). PF-1 (its grep and its awk), PRE-4, FN-15 and KB-16 print
nothing; UX-1 and UX-4's tokens are listed, the 139 that 0.2.1's record lists, and each is a Visual Token the
stylesheet reads. The rows judged by reading the code are those of 0.2.0's read-through, with the verdicts at X
in the table below; their evidence lines were carried to X as said above. PF-1's grep names any reading of
layout in the packages' sources, and prints nothing at X: the Device Pixel of a second window is told by that
window's own `matchMedia` (`browser-reports.ts`), read from no layout. PF-2 was read at X over the code #566
and #567 changed: the report's listener is on the `MediaQueryList` of the window the root is in, held among the
instance's stops and taken back at its destruction (ADR-0018, ADR-0350 section 6), and the placement of
several ranges adds no listener and no observer.

## Step 4 — Layer 3

CI ran the whole of layer 3 at X in the release run of `v0.3.0-rc.1`, CI's long run: the `chrome`, `msedge`
and `chrome-150` projects, the behaviour specs on the React host and the Blazor specs on the WebAssembly host
and the Server host, the rows once checked by hand on the Windows and macOS desktops and with a real IME, and
WR-10's runs at the Wrappers' MudBlazor floor. Every job, its conclusion and its Playwright summary:
[`layer3/jobs.md`](layer3/jobs.md), with what this session could read of each job's log beside it.

- **Every job concluded `success`**, and every Playwright summary read names only passed and skipped tests:
  **6,298 passed and 501 skipped in the 26 jobs' summaries read, 0 failed, 0 flaky, 0 did not run**; no retry
  (`retries: 0`, `playwright.config.mjs:174`) and no `Test timeout of … exceeded` in any line read (ASY-6). The
  four tests each first WebAssembly shard counts as expected failures are the harness's self-tests declared with
  `test.fail`, which Playwright counts as passed.
- **It is the first release run on Chrome and Edge 157.** The cut's pull request met them first, and the two
  readings they showed wrong were corrected in the cut itself, so X holds them (below, "Found during the
  sign-off"): ticket 75's tests passed here on both browsers, the four on the React host and the three the
  WebAssembly host runs, and MEM-4 on both browsers and both Blazor hosts.
- **What was not read.** A job's log reaches this session through a tool that returns its last 5,000 lines at
  most. Ten logs are longer, the Server host's among them, which are its request log for the most part;
  each one's closing summary, at the log's end, was read, and `jobs.md` says how many lines of each were. ED-30's
  job runs Playwright several times, and of its summaries only the last is in the lines read (30 passed); the
  step that runs them all, which stops at the first failure, concluded `success`. So the totals above leave out
  ED-30's earlier runs, and are otherwise the run's.
- **Against 0.2.1's run**: each job's count of skips is the one 0.2.1's record has for it, and each count of
  passes is that record's but the React host's, which is one more on Chrome and on Edge (800 against 799, of
  826 tests against 825): VZ-16's new test, `popout.spec.mjs:18`, a grid in a window the page opened, which
  passed on both. DC-68 is judged in layers 1 and 2 and adds no layer-3 test.
- **The skips** are therefore the kinds 0.2.0's record lists. On every host: the measurements Step 6 runs by
  name (`measure*.spec.mjs`), the Windows desktop and IME specs off their runners, the keys that exist only on an
  Apple platform or only on Windows, and two `test.fixme` of `sheet-vs-excel.spec.mjs`, items 18 and 24. On the
  WebAssembly host: the tests of a circuit or a round trip, which say why by name. On the Server host: ERR-10's
  two stamp tests, which SRV-2 names as an exception (the integrity-pinned test runs instead). SRV-2 holds.
- **The soak** (MEM-5, `EXGRID_SOAK=1`): ten minutes on Chrome and on Edge, on each Blazor host, all four
  passed (`memory.spec.mjs:187`, 10.0 minutes each). MEM-6 is read at the end of each.
- **The console** (CON-1 to CON-4, CON-6, CON-8): every layer-3 test ends with the fixture's console verdict,
  which fails the test on any console error or page error, any warning from ExGrid's own code it does not name,
  any unhandled exception in the host's log, and any log line from ExGrid on a healthy path. Every test passed,
  so each test's entry holds none. CI writes the same reading per test to `console.json`, in each job's
  `layer3-records-*` artifact of the release run, kept until 2026-12-13; this container cannot reach GitHub's
  artifact storage, so the file is not copied here, and the verdict rests on the tests' own readings, which fail
  on the first such message.
- **The rest of the release run.** Its build with layers 1 and 2, the Script Core's layers 1 and 2, and the
  packages taken as a Consumer takes them all concluded `success` at X, and the run went on to publish
  `0.3.0-rc.1` to nuget.org, at 00:19 UTC on 2026-11-14. Its job that publishes to npm waits on the `npm`
  environment, which the user approves, and had not run when this record was closed at 00:26 UTC; the Docs
  Site's job follows it (`jobs.md`, "The run's other jobs"). No criterion rests on either.

### The rows once checked by hand

ADR-0450 moved each onto CI's own desktops, where it gates the commit being released whatever changed;
`verification/by-hand.json` indexes none, and VZ-18's check on the remote-isolation PC is withdrawn
(2026-11-02, decided with the user). Each ran in the release run at X and passed:

| Rows | Job | Result |
|---|---|---|
| VZ-14, VZ-16, FN-12e, SH-42, SH-51, SH-52 | The Windows desktop rows | 24 passed; VZ-16's two skips are its run at a display of 100%, and it passed in the run at 150% on Chrome and Edge |
| KB-3, FN-12e | The macOS desktop rows | 16 passed |
| ED-30 | ED-30 with a real IME (Windows, Japanese) | the job and each of its twelve steps concluded `success`; the last run's summary, the one in the lines read, is 30 passed |
| WR-10 | WR-10, layer 3 at the MudBlazor floor, ten jobs; and WR-10, the Wrappers' layer 2 at every MudBlazor of their range | 1,692 passed at the floor, 9.6.0 as each host's file says, 150 skipped as the full run skips them; layer 2 passed at each of MudBlazor 9.6.0 to 9.13.0, the eight versions nuget.org lists in `[9.6.0,10.0.0)`, 32 runs of 32 ([its log](layer3/wr-10-the-wrappers-layer-2-at-every-mudblazor-of-their-range.log)); 9.13.0 is new since 0.2.1's run |

## Step 5 — The randomised consistency run

ST-1 at 10⁶ rows, `EXGRID_ST1_MILLION=1`: [`st1.log`](st1.log), 5 of 5 passed. The seeds are **20260901**
(200, 1,000 and 1,000,000 rows) and **424242** (200 and 1,000 rows), as `grid-consistency.test.ts` names
them. Zero violations. CI's long run ran the same case at X.

## Step 6 — Observational numbers

Every number below is OBSERVATIONAL: recorded, never gated. A move more than 20% gets its sentence, as Step 6
asks.

**How the comparison is made.** The previous verification directory is 0.2.1's,
[`../2026-11-12-linux-signoff`](../2026-11-12-linux-signoff/results.md), measured two days before X in another
cloud container of the same kind. Each number was measured once at X, with nothing beside it, and is set
against that record's number for 0.2.1's commit. 0.2.1's code was not measured again in this container, so a
move is the code's or the container's, and this record tells them apart only where the code a number measures
is the same in both commits: every CoreCLR measurement, the pivot engine, the sheet engine and the data
packages, none of whose files changed. What changed is in ExGrid's Script Core, which every product here draws
through: where a grid's reports begin and whose window they ask (#566), and a Selection of several ranges with
its Selection Summary (#567). This container reads quicker than 0.2.1's throughout: the CoreCLR numbers, on
code that is byte for byte the same, are 0–27% down, and the browser's moved the same way by about as much. So
the moves below are read as the container's, and none is called the code's.

**CoreCLR**: [`measure-coreclr.log`](measure-coreclr.log), the ten explicit measurements (PV-21's four,
DA-17's two, ADR-0065's one, SH-19's three) in a Release build, exit 0. 0.2.1's record and X, median:

| Number | 0.2.1 | X |
|---|---|---|
| DA-17, a million rows read from a CSV in memory, keyed / from a file, ms | 503 / 480 | 400 / 399 |
| DA-17, a million records built from objects, keyed / no key, ms | 291 / 260 | 255 / 245 |
| ADR-0065, a million trades to Arrow and back: write / read from memory / read from a stream, ms | 133 / 249 / 283 | 116 / 209 / 266 |
| PV-21, a million records asked, three row fields and two Sums / Month in Columns / 270 dates, ms | 21.0 / 40.4 / 105.9 | 21.0 / 39.9 / 95.2 |
| PV-21, the answer laid out, the cube / 270 dates' cube, ms | 2.6 / 100.3 | 2.3 / 90.7 |
| PV-21, 1,000 changes folded, exact Sums / a double's Sum / Max, ms | 6.3 / 15.8 / 13.3 | 5.8 / 13.9 / 12.4 |
| PV-21, the Snapshot built from typed declarations, ms | 141.9 | 138.3 |
| SH-19, a full recalculation / a row inserted near the top / that insertion undone, ms | 1,027.2 / 2,045.0 / 2,547.2 | 901.5 / 1,769.1 / 2,176.8 |
| SH-19, an edit to A1 / A10000 / O5000, ms | 95.2 / 47.8 / 38.0 | 74.8 / 34.9 / 31.2 |

The code these measure is byte for byte 0.2.1's, so every move is the container. Three moved more than 20%,
all down: the CSV read from memory, 400 ms against 503; and the edits to A1 and A10000, 74.8 against 95.2 and
34.9 against 47.8. MEM-7 is `not run`, as Step 6 says: it measured the C# core's render, removed with that
core (#501), and the Script Core has no measurement of it yet.

**In the browser** (PV-21, DA-17, LV-15), on a published WebAssembly DemoHost without AOT, served by
`tests/browser/static-host.mjs`, with the demo API holding a million trades and the page's own export of a
million trades (89.7 MiB) as the CSV; **on a circuit** (SRV-6, SH-19's completion), on the Server host the suite
starts: [`measure/README.md`](measure/README.md). X's numbers are [`measure/metrics.json`](measure/metrics.json),
with the runs' logs beside it; 0.2.1's are
[`../2026-11-12-linux-signoff/measure/metrics.json`](../2026-11-12-linux-signoff/measure/metrics.json).

**PV-21 and DA-17.** Level, and where they moved, sooner. Every gesture answers within 2 ms of 0.2.1's, each
under PV-21's 0.1 s: opening a field's menu in 13.7 ms against 12.5, sorting A to Z in 14.0 against 14.3, a
change of form in 13.7 to 15.0 against 15.0 to 16.5. The questions that need a new answer read 115.7 to 147.9 ms
against 141.7 to 191.5, with first visual answers of 35 to 36 ms against 36 to 40. Below and near the cap the
medians are 11–32% sooner: 1,350 combinations in 184.4 ms against 231.7, 13,500 in 307.8 against 454.9, 27,000
in 555.2 against 625.2, 66,150 in 1,063.5 against 1,289.6, 134,730 in 2,203.3 against 2,635.1, 198,450 in
2,660.4 against 3,319.9. The longest task near the cap is 202 ms against 235 at 66,150 combinations, 368
against 429 at 134,730 and 631 against 766 at 198,450: smaller by the container's margin and still far over
the 50 ms a task may take, so what 0.2.0's record says of it stands, and it stays listed below. The readings
that moved more than 20%, every one down:

- the first question at load, answered in 341 ms against 517 with a longest task of 108 against 166, the first
  report 6,620 ms after the load began against 8,466;
- two of the questions that need a new answer, ticking Book, 147.9 ms against 191.5, and filtering to USD,
  115.7 against 151.6;
- the medians at 1,350 combinations, 184.4 ms against 231.7, and at 13,500, 307.8 against 454.9, whose first
  run held no long task where 0.2.1's held one of 84 ms;
- the first answer at 198,450 combinations, 2,677 ms against 3,479, inside a task of 670 against 892;
- the refused question at 330,750 combinations, its first answer 294.2 ms against 367.9, its median 351.5
  against 493.4 and its longest task 100 against 126;
- and the longest task while the 89.7 MiB CSV is read, 99 ms against 141, which is that read's first visual
  answer too. The read itself took 3,463 ms against 3,999.

The engine that answers these did not change. The input delays, tenths of a millisecond each, are not listed.
DA-17 reads sooner by the same margin: a million records from objects into a Snapshot in 2,653 ms against
3,168, the Arrow file's first read 8.7 s against 9.7 for the page and 2.6 s against 2.9 in the browser. All
seven tests of `measure-pivot.spec.mjs` passed as committed, the refused question's four runs among them.

**1,000 changes on /pivot-live** are applied in 95 ms against 0.2.1's 101, and reach the report's frame in 103
against 111, inside a task of 105 against 113. The report is in the batch's frame in all 12 runs, as for 0.2.1.
It stays inside PV-21's 0.2 s and over the 50 ms a task may take before the page reads as blocked, the gap the
user accepted on 2026-11-02 (ADR-0350 section 15).

**LV-15** ([`measure/lv15/run.log`](measure/lv15/run.log)): 1,000 changes to a million rows are applied in
47 ms against 0.2.1's 45, and reach the frame that shows the grid 86 ms after Apply began, against 76. The
task start to that frame reads 109 ms against 190 (−43%), between 90 and 196 over the 12 runs where 0.2.1's lay
between 103 and 238: 0.2.0's record read 110, so 0.2.1's 190 was the reading that stood apart, and this one is
back beside 0.2.0's. The longest task's median is 0 ms with a maximum of 156 against 207. The grid is painted
in the batch's own frame in 1 of the 12 runs and in a frame after the status line's in 11, where 0.2.1's
record has it after in all 12. Where the time between the two frames goes is still not established; it stays
listed below, with the LV rows' rewrite.

**SRV-6**: the hover band trails the pointer by 2 ms at every round trip (0, 50 and 150 ms), as in 0.2.1's
record, and no frame crosses the circuit while the pointer moves. The band is painted by the Script Core in
the browser, and the pointer does not cross the circuit (ADR-0350).

**SH-19** on a circuit: from the key to the frame with the completion list, =S takes 5.7 ms and =SU 6.3 at no
round trip, and 5.9 and 7.0 at 150 ms, against 7.3, 6.4, 5.8 and 4.4 in 0.2.1's record. Two moved more than
20%, opposite ways and by a millisecond or two: =S at no round trip, down 1.6 ms, and =SU at 150 ms, up 2.6 ms
(0.2.0's record read it at 5.6). The list is made in the browser by the Script Core, so the round trip does not
reach it.

**PV-48 and LV-23** are `not run`: their harnesses measured the update step by step and the heap over
redraws of the report Window path ADR-0370 section 3 ended, and nothing measures the Leaf Aggregate path that
replaced it yet; they go with the LV rows' rewrite, a Known limitation above.

**CI's layer-3 numbers** (BIG-6, BIG-7, PST-6, PF-6, PF-7, MEM-2, MEM-6, DOM-5): measured in every job of the
release run and written to its `metrics.json`, in the `layer3-records-*` artifacts. This container cannot reach
GitHub's artifact storage, so they are neither copied nor compared here; the tests that take them passed.
0.1.0's are in its record.

**PF-8**: `spikes/render-bench`'s core comparison (ADR-0350 section 15), Bench.React and a published
Bench.Client, five rounds each, each arm alone, with no console complaint: X's in
[`measure/pf8/`](measure/pf8/). [`measure/pf8/summary.txt`](measure/pf8/summary.txt) sets 0.2.1's record and X
side by side, 0.2.1 first.

- **The grid.** React is level, within 20% or half a millisecond, but two readings: a push of every row of 10,000 took 6.5 ms of DOM work against
  4.5, reaching its frame in 13.1 ms against 13.6, and the 95th-percentile frame of a tick of 25 of 100,000 is
  37.8 ms against 48.3. Blazor WebAssembly is quicker where it moved, seven readings by more than 20%: a push
  to one of 10,000 rows 14.2 ms against 21.3 and to one of 100,000 81.4 against 111.2, a tick of one of 10,000
  14.2 against 18.2 and of 5 of 100,000 81.4 against 110.0, and three 95th-percentile frames under ticks. **Its
  push of every row of 1,000**, which 0.2.1's record listed, reached its frame in 45.7 ms against 46.9, with
  DOM work of 38.2 against 36.6; 0.2.0's record read 32.2 and 32.0. The frame steps by a display frame of
  16.7 ms, so DOM work that ends a few milliseconds past the second frame is shown in the third: the step from
  32 to 46 is that, over DOM work that reads 32.0, 36.6 and 38.2 in three containers. It stays listed.
- **The template.** React is level, every reading within 20% of 0.2.1's or half a millisecond of it. Blazor WebAssembly is quicker in thirteen: its
  push of every row of 1,000 reached its frame in 30.1 ms against 46.6, a push of every row of 10,000 took
  36.3 ms against 48.8, a tick of 25 of 1,000 rows 11.5 against 15.4, the 95th-percentile frame of a tick of
  one of 100,000 119.8 against 159.7, and its scroll lag 2.5 against 4.0.
- **ExSheet and ExPivot.** ExSheet's edit of A1 with 20,000 cells recalculated took 167.6 ms against 225.6 at
  the median (−26%) and 200.2 against 250.2 at the 95th percentile, and its scroll lags by 5.7 ms against 13.5;
  ExPivot's amended trade took 85.8 ms against 98.7 at the median and 94.9 against 137.9 at the 95th
  percentile (−31%), and its swapped columns 114.7 against 123.6. The sheet engine and the pivot engine did not
  change, so these are the container's, as the CoreCLR numbers are.

## §21 and §21.11

§21 states that no open question remains, and no row reads `BLOCKED`. The reservations of §21.11, and their
triggers at X. The diff since 0.2.1 adds one sample page, `/popout`, on the React host, and the Examples of a
grid in a second window and of rows selected by a condition to the Docs Site; none declares what a trigger
below names, so each reads as in 0.2.1's record:

| Reserved | Trigger | At X |
|---|---|---|
| The Overlay application and the bundled undo stack | the first scenario-backed Consumer | unfired: no such page or Consumer is in `samples/` or `dotnet/samples/` |
| The Cell Editor's own ARIA semantics, a richer announced contract | a real screen reader | out of 0.3.0's scope, accepted in writing (its Release Notes, Out of scope, as 0.2.1's) |
| The year ▸ month ▸ day tree over a date column's value list | a Consumer declaring a value list on a date column | unfired: no page in either host's samples declares one |
| A range of columns through a Pointing Scope | the engine gaining column ranges | unfired: SH-37 pins today's refusal and passes |
| ExGrid reading a Snapshot | a screen showing one live feed in ExGrid and ExPivot, or a measurement | unfired: the Board's grid reads the Snapshot it is handed (ADR-0342), but no page shows one live feed in both, and no record measures a user's sort of 10⁶ rows in the browser |
| Which layer VZ-18's repaint answers | — | the check on that PC is withdrawn (2026-11-02, decided with the user: it was a remote browser isolation mirror); VZ-18's layer-3 half passes |
| Find All and Replace | settled | Replace by ADR-0332, Find All by ADR-0470; FD-12 to FD-17 pass, three with a clause accepted as is |
| Dragging a Reference Outline | settled | by ADR-0480; DC-67 and SH-69 pass |
| A column band | settled | `highlightFocusColumn`, ADR-0008's "The Focus column band", judged by `core/grid-bands.test.ts` |

The entries settled before 0.1.0 (the fill handle, right-click, a copy over an Action Column, the editor's
classes and tokens, `--ex-selection-outline`, the Wrapper's seam order, ExSheet as a Consumer, the keys
after pointing into a registered grid, and ExPivot keeping unchanged report rows) have their criteria,
which gate like any other. A placement of several ranges (ADR-0050 item 4) and a grid in a second window
(ADR-0090) are features, not reservations; DC-68 and VZ-16 judge them.

## Found during the sign-off

Nothing in the product. Two readings of layer 3 were wrong, found when the cut's pull request met CI's new
browser, and corrected in the cut itself (#569), so X holds them and the release run ran on them:

- **A text field's content box was read from `clientWidth`.** GitHub's `ubuntu-24.04` runner image
  20261111.370.1 brought Chrome 157, where a text `<input>`'s `clientWidth` and `scrollWidth` leave its padding
  out; `scrollLeft`, the caret and the pixels are where they were. `caretInField` (`sheet-helpers.mjs`) took the
  box from `clientWidth`, so ticket 75's four tests read a caret at the field's right edge as 16 px outside it,
  on `main` too. It takes the box from the field's own rectangle less its borders and padding now. Reproduced on
  Chrome 157.0.8103.50 before the change, and passed there and on Chrome 153 after it; the Cell Editor's pixels
  are the same in both browsers.
- **MEM-4's listener count was read before the circuit was quiet.** On the Server host the grid's disposal
  reaches the browser a message after the render that takes the grid out of the document, and a count read
  between the two is 25 listeners high: 1 run in 40 here. The test awaits `circuitQuiet()` before each count
  (ADR-0056); 120 runs of 120 passed after it. The product disposes as it did.

Three things about the record itself:

- **The release run's logs reach this session in part** (Step 4): the last 5,000 lines of each. Each job's
  conclusion is GitHub's. The whole logs are on the run's page while GitHub keeps them.
- **Step 6 has no A/B**, so a move between the two records is not told apart from the container except where
  the code is unchanged.
- **0.2.1's record left the ends of eight ranges of lines at 0.2.0's**, corrected here (above, "What X adds
  to the criteria").

**For after 0.3.0.** None of these fails a criterion as the user's decisions read it. Those 0.2.0's record
listed are tickets since #557 (`docs/specs/signoff-0-2-0-gaps/`) and stand as they were:

- **The 109 clauses no test asserts and the 24 rows with no test**, listed in the table: a test for each.
- **DA-18**: one definition of the Aggregations, in `@ex-grid/data`, owed in 0.3.x.
- **ED-34**: naming a cancel's true outcome on Blazor, a wire and a core change, owed in 0.3.x.
- **The LV rows' rewrite** for the Leaf Aggregate boundary, with ExPivot.Engine's removal, and PV-50's keys
  typed while the panel opens, checked over the Script Core.
- **A held press whose control was painted again**, retargeted by `elementFromPoint`: whether ADR-0021's
  rejection of replay-time target discovery reaches it is left for an ADR.
- **PST-6** records 400 source cells, not the 10⁵ and 10⁶ its row names; **MEM-7** has no measurement over the
  Script Core.
- **PV-21's longest task near the cap**, 202 to 631 ms at X: which step holds the page is to be measured.
- **A refused Update under Defer Layout Update** names each deferred move with the sentence for one drop.
- **LV-15's grid frame** a frame after the status line's, in 11 runs of 12 at X.

0.2.1's record added two readings to be measured against the code before in one container:

- **LV-15's task start to the grid's frame** reads 109 ms at X, beside 0.2.0's 110, where 0.2.1's record read
  190: that reading stood apart, and it is no longer listed.
- **Blazor WebAssembly's push of every row of 1,000 in PF-8** reads 45.7 ms at X, as 0.2.1's 46.9 and against
  0.2.0's 32.2 (Step 6): still to be measured against 0.2.0's code in one container before it is called the
  code's or the frame's step.

## Every criterion

`core/`, `react/`, `blazor/`, `pivot/`, `sheet/` and the other short paths name `packages/<name>/test/`;
`browser/` is `tests/browser/`, `cases/` is `tests/cases/`, and a C# test is `<Project>/<File>.cs`. Layer 1
and 2 passed in Steps 1 and 2 (their `.trx` files and logs here); layer 3 in Step 4. A count such as `(+3)`
is further tests of the same row. Every line number is X's. A verdict that reads `known limitation, carried
from 0.2.0` is one of the Known limitations 0.3.0's Release Notes carry from 0.2.0's, as 0.2.1's did.

| Row | Level | Verdict | Evidence at X |
|---|---|---|---|
| PRE-1 | MUST | pass | `pre-1.log`: 0 warnings, 0 errors |
| PRE-2 | MUST | pass | `step0-static.log`: every `dotnet/src` project `net10.0`, no `LangVersion` |
| PRE-3 | MUST | pass | `pre-3.log` and `pre-3-npm.log`: both package checks, exit 0 |
| PRE-4 | MUST | pass | `pre-4.log` and `step3-static.log`: no core project references MudBlazor or Fluxor |
| PRE-5 | MUST | pass | `step0-static.log`: the projects of `playwright.config.mjs`, `chrome` and `msedge` |
| PRE-6 | MUST | pass | `step0-static.log`: `git status --porcelain -- dotnet/src/` empty |
| FN-1 | MUST | pass, accepted as is | L2: `core/grid-dom.test.ts:410 'a position the Window does not hold is a Placeholder, and its range is asked for once'`, `core/grid-range-request-margin.test.ts:43` (windowed source). Not asserted: nothing retains a Window after it is replaced |
| FN-2 | MUST | accepted as is, no test | No test. Code refuses: `grid-view.ts:2650` (TS), `ExGrid.cs:490` (Blazor) |
| FN-3 | MUST | pass, accepted as is | L2: `core/grid-dom.test.ts:410` (asked once), `core/grid-range-request-margin.test.ts:43 'not asked again until the Window moves'`. Not asserted: a synchronous answer does not re-enter |
| FN-3a | MUST | pass, accepted as is | L2: `core/grid-range-request-margin.test.ts:43 'at 10 …'`, `:64 'at 0, the default …'`, `:82 'a total not known yet …'` (+1). Not asserted: the upper edge (scrolling up) and the clamp at the total's end |
| FN-4 | MUST | accepted as is, no test | No test runs one scenario through both `from` and `fetch`. Structurally one path (`grid-view.ts:2655`, `rows` → `InMemoryGridSource.from`) |
| FN-5 | MUST | pass | L1: `cases/source/fetch.json:7 'cold start …'`, `:68 'same range … one fetch'`, `:99 'a superseded question is called off …'` (+14); `core/fetch-boundary.test.ts:101` |
| FN-6 | MUST | pass | L2: `core/grid-dom.test.ts:293 'a fling paints Placeholders with the pinned cells alone …'`; L3: `browser/virtualisation.spec.mjs:694 'the Pinned Columns are painted … mid-fling included (FN-6)'` |
| FN-6a | MUST | pass, accepted as is | L1: `cases/columns/geometry.json:353`, `:364`, `:373` (FN-6a); L3: `browser/sizing.spec.mjs:312 'a window narrower than the pinned block suspends pinning …'`. Not asserted: no `OnColumn…`/View State notification raised (only the pinned count after widening) |
| FN-7 | MUST | pass | L1: `cases/paint/cell-classes.json:23 'every state has a class'`, `:73 'cast in from a number is refused'`; L2: `core/grid-cell-paint.test.ts:48 'a Cell State is painted as its class and nothing more'` |
| FN-7a | MUST | pass, accepted as is | L2: `core/grid-cell-paint.test.ts:68 '… tone rule paints a gain and a loss … not asked about a null'`; L3: `browser/mud.spec.mjs:212 'a loss is painted in the palette's error colour … (FN-7a)'`. Not asserted: a negative in a column with no rule; text/copy/raw forms untouched |
| FN-8 | MUST | pass | L1: `cases/paint/row-classes.json:13`, `core/paint-classes.test.ts:39`; L2: `core/grid-row-kind.test.ts:64 'each row wears the class of the role it was told'` |
| FN-9 | MUST | pass | L2: `core/grid-actions.test.ts:334 'content is mounted once its row stands in the DOM … taken away when it is not'`, `:83`, `react/ex-grid.test.ts:153`; `core/grid-dom.test.ts:118` (listeners on root) |
| FN-10 | MUST | pass | L1: `cases/columns/overflow.json:13`, `:26`, `:32` (+12); L2: `core/grid-dom.test.ts:390 '… a number too wide for a Fixed one is ####'`, `core/grid-cell-paint.test.ts:98` |
| FN-11 | MUST | pass | L1: `cases/columns/auto-width.json:21`, `:28 'do not narrow'`, `:35 'any order'`; `cases/columns/column-width-spec.json:44` (40/400); `cases/view-state/write.json:2236` (width intents); L2: `core/grid-dom.test.ts:390`; the header's part of an Auto width, which alone comes down (ADR-0500, FN-12h): `core/auto-width-header.test.ts:10`, `:23` |
| FN-12 | MUST | pass | L1: `ExGrid.Tests/GridColumnTests.cs:26 (FN-12)`, `ExGrid.Tests/ActionAndTemplateColumnTests.cs:19 Action_and_template_columns_refuse_a_width_below_min_width_by_name`, `cases/columns/column-width-spec.json:34`; L2: `core/grid-column-gestures.test.ts:115`; L3: `browser/gestures.spec.mjs:122 'a drag past MaxWidth … (FN-12)'` |
| FN-12a | MUST | pass | L1: `cases/columns/column-width-spec.json:54 'Size to fit … clamped'`; L2: `core/grid-column-gestures.test.ts:140 'FN-12a …'`; L3: `browser/sizing.spec.mjs:80 '… fits a value the Viewport has not painted (FN-12a)'` |
| FN-12b | MUST | pass | L2: `core/grid-column-gestures.test.ts:86 'a press that barely moves reports nothing …'` (4px) |
| FN-12c | MUST | pass, accepted as is | L1: `cases/selection/whole-ranges.json:113`, `:124`; L2: `core/grid-column-gestures.test.ts:103`, `:128` (FN-12c); L3: `browser/sizing.spec.mjs:270`. Not asserted: a double-click fitting each whole column to its own content |
| FN-12d | MUST | pass | L1: `cases/columns/grid-metrics.json:104 'one em of slack, the menu band and the sort room'`, `:146`, `core/text-geometry.test.ts:49`; L3: `browser/sizing.spec.mjs:50`, `:56` (FN-12d). This is the Standard Header Display's charge (the row's note of 2026-11-10); the Compact one's is FN-12g |
| FN-12e | MUST | pass | L1: `ExGrid.Tests/GlyphWidthCorpusTests.cs:73 (FN-12e)`, `cases/columns/text-metrics.json:73`, `cases/columns/overflow.json:52`; L3: `browser/sizing.spec.mjs:65`, `browser/part-c.spec.mjs:487`, run by `ci.yml` `windows-desktop` (:600) and `macos-desktop` (:735) in Chrome and Edge |
| FN-12f | MUST | pass | L2: `core/grid-header-display.test.ts:115` (no `headerDisplay`, and `standard`, draw the header an earlier version drew), `:283` (an unknown value refused by name), `:296`, `:322`, `:331` (the Header Box: ▾, a Sort, a Filter or both; named through `commandLabel`), `:356` (a click sorts, a press on the box opens the menu and sorts nothing), `:428` (the gestures mean the same), `:444` (Header Groups), `:512` (no width moves); the crossing: `blazor/bridge.test.ts:468`, `ExGrid.Components/ScriptGridWindowTests.cs:62`; L3: `browser/header-display.spec.mjs:71` (FN-12f) @behaviour |
| FN-12g | MUST | pass | L1: `cases/columns/grid-metrics.json:110` (ADR-0500), run by Vitest and by xUnit's `CallCaseTests`; L2: `core/grid-header-display.test.ts:468`, `:489`, `:528` (Size to fit); L3: `browser/header-display.spec.mjs:54` (FN-12g: 28px less where the header decides, no label cut or under its box) @behaviour |
| FN-12h | MUST | pass | L1: `core/auto-width-header.test.ts:10`, `:23`; L2: `core/grid-header-display.test.ts:546`, `:564` (the widths, either way round), `:589` (the Sort, the Filter, the Selection, the scroll and the rows), `:620` (an open edit), `:646` (an open column menu); L3: `browser/header-display.spec.mjs:79` (within FN-12f's test: a width somebody set stands and none is reported) @behaviour, `:201` (FN-12h: the parameter's crossing, and .NET hears no width) @blazor |
| FN-12i | MUST | pass | L2: `core/grid-header-display.test.ts:129`, `:154` (ADR-0501), `:296` (the compact header's box); L3: `browser/header-display.spec.mjs:114` (FN-12i) @behaviour. The row's last sentence is FN-12j's since ADR-0502 |
| FN-12j | MUST | pass | L1: `cases/columns/grid-metrics.json:118` (ADR-0502), both runners; L2: `core/grid-header-display.test.ts:167`, `:194`, `:225`, `:242`, `:267` (the standard header), `:342`, `:497` (the compact one); L3: `browser/header-display.spec.mjs:146` (FN-12j) @behaviour |
| FN-13 | MUST | pass | `ExGrid.Tests/QuerySerialisationTests.cs:32 FN13_no_public_signature_mentions_an_expression_type`, `:21 FN13_a_row_range_round_trips…`, `cases/view-state/equality.json:3196 'FN-13 …'` (+70); no `Expression<` in any `PublicAPI.Unshipped.txt` |
| FN-14 | MUST | pass | L1: `cases/query/sort.json:7`, `:17`, `cases/query/text.json:7` (+N, run by `core/query.test.ts:31`); `ExGrid.Tests/SortSemanticsTests.cs:22 Sort_collation_is_ordinal_and_ignores_current_culture` (+N) |
| FN-15 | MUST | pass | Inspection: `grep -n '\.sort(' packages/core/src/dom/*.ts` prints nothing; no `.filter(` there reads a row against a predicate (the one over rows, `grid-view.ts:6178`, matches a Row Key) |
| FN-16 | MUST | pass, accepted as is | L2: `core/grid-pager.test.ts:63 'the pager turns pages …'`, `:51`. Not asserted: a page click raises the same Range Request shape as a scroll |
| FN-17 | MUST | pass | L2: `react/chrome.test.ts:124-130` (the slot contracts against plain Chrome), `packages/mantine/test/contracts.test.ts:19`, `packages/mui/test/contracts.test.ts` (same contracts); `ExGrid.MudBlazor.Tests/MudGridChromeTests.cs`; L3: `browser/popovers.spec.mjs` (per Chrome), `browser/mud-app.spec.mjs:422` |
| FN-18 | MUST | pass | L2: `core/grid-filter-slot.test.ts:208 'FN-18: an operator the core did not offer … refused by name'`, `react/menu-slot-contract.ts:94`, `:131`; `ExGrid.MudBlazor.Tests/MudScriptFilterPanelTests.cs:66` |
| FN-19 | MUST | pass | L1: `cases/selection/queries.json:16 'count … needs no data'`, `cases/selection/range.json:19`; L2: `core/grid-off-screen.test.ts:28`, `:44` |
| FN-20 | SHOULD | pass, accepted as is | L2: `core/grid-formula-bar.test.ts:70 'the bar reads the Focus cell's text …'`. Not asserted: a `####` cell's bar shows the value, not `####` |
| FN-21 | MUST | pass, accepted as is | L2: `core/grid-consumer-popover.test.ts:115 'while a popup of the contents' own is open, Escape is the popup's'`; L3: `browser/mud-app.spec.mjs:542`, `:558`, `:572` (each for MudSelect and MudDatePicker, both Blazor hosts). Not asserted: the other grid unaffected; the Inner Popup clauses for a grid inside a MudDialog (`:620` only draws and chooses) |
| FN-22 | MUST | pass | core/grid-dom.test.ts:199; ExGrid.Components/ScriptGridWindowTests.cs:38 (fixed by #539) |
| UX-1 | MUST | pass | `ExSheet.MudBlazor.Tests/NoColourParameterTests.cs:123 UX1_no_public_component_parameter_is_a_colour`, `:145`; `core/stylesheet-lengths.test.ts:192`, `:222` (UX-1) |
| UX-2 | MUST | pass | L3: `browser/presentation.spec.mjs:20 'geometry tokens are inline and read-only … (UX-2)'` |
| UX-3 | MUST | pass | L3: `browser/presentation.spec.mjs:89 '… at every preset'`, `browser/mud.spec.mjs:24 (UX-3)`, `browser/mantine.spec.mjs:28`, `browser/mui.spec.mjs:28` |
| UX-4 | MUST | pass | L3: `browser/presentation.spec.mjs:748 'every class under the root is one ADR-0029 names (UX-4)'` |
| UX-5 | MUST | pass, accepted as is | L3: `browser/presentation.spec.mjs:169 '… recolours the paint (UX-5, the paint half)'`, `browser/mud.spec.mjs:234 'a theme switch re-renders nothing inside the grid: the rows are the same elements'`. Not asserted: the count half on the React host, and a .NET render count |
| UX-6 | MUST | pass | L3: `browser/presentation.spec.mjs:185`, `browser/mud.spec.mjs:45` (UX-6); `ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:112` |
| UX-7 | MUST | pass | L3: `browser/presentation.spec.mjs:201 'UX-7 … under forced colors every Cell State and Row Kind stays tellable'` |
| UX-8 | MUST | pass | L3: `browser/presentation.spec.mjs:380 'under a dark scheme … (UX-8)'` (4.5:1); `ex-grid.css` declares no `color-scheme` (two comment lines only) |
| UX-9 | MUST | pass | L3: `browser/gestures.spec.mjs:193 (UX-9)`, `browser/mud.spec.mjs:63 (UX-9)`, `browser/mantine.spec.mjs:57 (UX-9 / ADR-0390, #553)` (+`mui.spec.mjs:57`), each now judging the outline's 3:1 and the action button's colours too (#558) |
| UX-10 | SHOULD | pass | L3: `browser/gestures.spec.mjs:218 'narrowing the scrollbar by token … (UX-10)'`, `browser/stripes.spec.mjs:275` |
| UX-11 | MUST | pass | L2: `core/grid-column-menu.test.ts:63` (inline max-height), `core/grid-context-menu.test.ts:140`; L3: `browser/popovers.spec.mjs:201`, `:223` (UX-11), `browser/mud-app.spec.mjs:355 'WR-7: a grid in a MudDialog opens its popovers whole'` |
| UX-11a | MUST | pass | L2: `core/grid-context-menu.test.ts:243 '… a box shrunk below a row of room closes it'`; L3: `browser/stretch.spec.mjs:116 (UX-11a)` |
| UX-11b | MUST | pass | L3: `browser/sizing.spec.mjs:287 (UX-11b)` |
| UX-12 | MUST | accepted as is, no test | Inherits §4.1: A11Y-8 and A11Y-14 are gaps |
| UX-13 | MUST | pass, accepted as is | L2: `core/grid-bands.test.ts:26 'UX-13 / RR-11 …'`; L3: `browser/mud.spec.mjs:252 '… in the hovered instance only (UX-13)'` (two grids, no row class, gone after leave). Not asserted: the band's fill is `--ex-row-hover-background` |
| UX-14 | MUST | pass | L3: `browser/features.spec.mjs:1483 'the chosen action is outlined, with and without forced colors (UX-14)'` |
| UX-15 | MUST | pass, accepted as is | L2: `core/grid-row-kind.test.ts:25 '… stays with its row on scroll'`, `:45` (Placeholders); L3: `browser/stripes.spec.mjs:87 (UX-15)`, `browser/appearance.spec.mjs:433`. Not asserted: parity under a pager |
| UX-16 | MUST | pass | L3: `browser/stripes.spec.mjs:111`, `:226`, `:258` (UX-16) |
| UX-17 | MUST | pass | L3: `browser/selection-look.spec.mjs:76 'UX-17 (${chrome} Chrome) …'` |
| UX-18 | MUST | pass | L3: `browser/selection-look.spec.mjs:110`, `:131`, `:151` (UX-18); `ExGrid.Components/ShippedStylesheetTests.cs:210` |
| UX-19 | MUST | pass | L1/2: `cases/paint/selection-styles.json`, `ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:25`, `ExSheet.MudBlazor.Tests/PaperStylesheetTests.cs:91`; L3: `browser/selection-look.spec.mjs:181`, `:194`, `:226` (+5), `browser/sheet-paper.spec.mjs:209`, `browser/mud.spec.mjs:169`; 150% via `chrome-150` |
| UX-20 | MUST | pass | L3: `browser/presentation.spec.mjs:331 'the Modified mark is a triangle … (UX-20)'` |
| UX-21 | MUST | pass, accepted as is | L1: `cases/selection/heading-marks.json`, `core/heading-marks-property.test.ts:36`; L2: `core/grid-row-headings.test.ts:154`, `core/grid-selection.test.ts:275`; L3: `browser/heading-mark.spec.mjs:75`, `:320`, `:378` (+8). Not asserted: no row repainted as the Selection moves the Heading Mark (`grid-selection.test.ts:255` holds it for the pinned shade only) |
| A11Y-1 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`role="grid"`), `core/grid-actions.test.ts:92` (gridcell), `core/grid-header-groups.test.ts:36` (row/columnheader); L3: `browser/aria-rows.spec.mjs:100` (rows owning headers and cells in the tree) |
| A11Y-2 | MUST | pass | L2: `core/grid-dom.test.ts:54` (1001), `core/grid-header-groups.test.ts:36` (1002 under a band), `core/grid-row-headings.test.ts:253` (hidden header); L3: `browser/aria-rows.spec.mjs:100` (402), `:139` (21), `:161` (1048577) |
| A11Y-3 | MUST | pass | L2: `core/grid-dom.test.ts:88` (indices after a scroll), `core/grid-header-groups.test.ts:150`; L3: `browser/aria-rows.spec.mjs:100`, `:139`, `:161` (far down), `browser/virtualisation.spec.mjs:579` (header `aria-colindex` after panning) |
| A11Y-4 | MUST | pass | L2: `core/grid-edit.test.ts:114`; L3: `browser/features.spec.mjs:1086`, `browser/key-field.spec.mjs:755`, `:786`, `:830` (+1) |
| A11Y-5 | MUST | pass | L2: `core/grid-edit.test.ts:114`, `:135`; L3: `browser/mud-app.spec.mjs:247 'WR-7: the two grids … stay independent'` (own id spaces), `browser/key-field.spec.mjs:821`, `:650` |
| A11Y-6 | MUST | pass | L3: `browser/features.spec.mjs:1583 'A11Y-6 …'` |
| A11Y-7 | MUST | pass | L2: `core/grid-dom.test.ts:390` (`aria-label` the number, text `####`), `core/grid-cell-paint.test.ts:98` |
| A11Y-8 | MUST | accepted as is, no test | No test greps for `aria-selected` after a drag (the bUnit check went with #501). Code writes it only on listbox options (completion, find-all list, filter values), never on a cell |
| A11Y-9 | MUST | pass | L3: `browser/features.spec.mjs:1559 'A11Y-9: a drag across 200 rows writes the live region once …'`; L2: `core/grid-selection.test.ts:119` |
| A11Y-10 | MUST | pass | L3: `browser/features.spec.mjs:1542 'A11Y-10 …'` |
| A11Y-11 | MUST | pass | L2: `core/grid-dom.test.ts:293` (every Placeholder `aria-busy="true"`) |
| A11Y-12 | MUST | pass, accepted as is | L3: `browser/heading-mark.spec.mjs:191` (overlay layer `aria-hidden`), `browser/aria-rows.spec.mjs:100` (tree holds only rows). Not asserted: the Selection layers' `aria-hidden` (`grid-view.ts:2217-2219` sets it) |
| A11Y-13 | MUST | pass | L2: `core/grid-header-groups.test.ts:36` (`aria-colspan` = member count, 1 and 2) |
| A11Y-14 | MUST | accepted as is, no test | No test counts row paints across a drag with the live region; `core/grid-selection.test.ts:119` checks the announcement, not paints |
| A11Y-15 | MUST | pass, accepted as is | L2: `core/grid-edit.test.ts:349 'ED-15: a Reject … shows and says the Consumer's sentence'` (`aria-invalid`, `.ex-announce` verbatim), `react/editor-slot-contract.ts:107`; L3: `browser/features.spec.mjs:988`. Not asserted: one write per Reject; nothing written on Accept or Flag |
| A11Y-16 | MUST | pass | L2: `core/grid-copy.test.ts:243 'A11Y-16 …'`; L3: `browser/features.spec.mjs:403 '… announced in the root's live region … again when repeated (A11Y-16)'` (DemoHost line has no role) |
| A11Y-17 | MUST | pass | L2: `core/grid-actions.test.ts:83 '… out of the tab sequence …'`; L3: `browser/features.spec.mjs:1461 (A11Y-17)` |
| A11Y-18 | MUST | pass, accepted as is | L2: `core/grid-actions.test.ts:118`, `:155`, `:83` (label); L3: `browser/key-field.spec.mjs:885`. Not asserted: the attribute cleared while the chosen button is not painted |
| A11Y-19 | MUST | pass | L2: `core/grid-column-menu.test.ts:56` (menu named by header), `core/grid-filter-panel.test.ts:87` (dialog named by header), `core/grid-context-menu.test.ts:45`; L3: `browser/popovers.spec.mjs` (roles, per Chrome incl. MudBlazor) |
| A11Y-20 | MUST | pass | L2: `blazor/bridge.test.ts:134 'A11Y-20 …'`, `ExGrid.Components/ScriptGridHandleTests.cs:61`, `ExSheet.Components/ScriptSheetTests.cs:250` (+`ExPivot.Components/ScriptPivotTests.cs:76`); L3: `browser/circuit.spec.mjs:481 '… no grid to press or tab to until its circuit connects (A11Y-20)'` |
| A11Y-21 | MUST | pass | L3: `browser/key-field.spec.mjs:650`, `:885` (A11Y-21, per Chrome) |
| HG-1 | MUST | pass | L1: `cases/columns/header-group-layout.json:7 'HG-1 …'`; L2: `core/grid-header-groups.test.ts:36` (members declared out of order) |
| HG-2 | MUST | pass | L2: `core/grid-header-groups.test.ts:36` (width spans members), `:69 '… says its rowspan'` |
| HG-3 | MUST | pass | L1: `cases/columns/header-group-layout.json:90 'HG-3 …'`; L2: `core/grid-header-groups.test.ts:69` |
| HG-4 | MUST | pass | L1: `cases/columns/header-group-layout.json:16 'HG-4 …'` |
| HG-5 | MUST | pass | L1: `cases/columns/header-group-layout.json:22 'HG-5 …'`; L2: `core/grid-header-groups.test.ts:98 "'Apart' members are not adjacent"` |
| HG-6 | MUST | pass | L1: `cases/columns/header-group-layout.json:28`, `:34`, `:43` |
| HG-7 | MUST | pass | L1: `cases/columns/header-group-layout.json:49 '… naming the boundary'`, `:55`, `:63`; L2: `core/grid-header-groups.test.ts:98` |
| HG-8 | MUST | pass, accepted as is | L2: `core/grid-header-groups.test.ts:36` (band 2 × 28 at one tier), `:150` (rows' place moves with tiers); L1: `cases/viewport/geometry.json:83`. Not asserted: the first painted row at a scroll offset identical at 0, 1 and 2 tiers |
| HG-9 | MUST | pass | L2: `core/grid-header-groups.test.ts:98` ('must exceed the header band — 2 tiers × 28px …') |
| HG-10 | MUST | accepted as is, no test | No test scrolls a group's first members out of the DOM and reads its `left`/`width` |
| HG-11 | MUST | accepted as is, no test | No test counts member resolution across scroll steps; `core/grid-header-groups.test.ts:113` keeps the rectangle across a re-push and a Sort but scrolls nothing |
| HG-12 | MUST | accepted as is, no test | No test of a long group label over narrow columns (no `####`, ellipsis, member Auto widths kept) |
| HG-13 | MUST | pass | `ExGrid.Components/ShippedStylesheetTests.cs:32 The_shipped_stylesheet_aligns_nothing_vertically`, `ExGrid.Tests/NoVerticalAlignmentTests.cs:21 Nothing_public_offers_a_vertical_alignment`; L3: `browser/presentation.spec.mjs:496 (HG-13)`; the row's grep over `dotnet/src/ExGrid/Assets/` finds nothing |
| HG-14 | MUST | pass | L1: `cases/columns/header-reorder.json:20 'HG-14 …'`; L2: `core/grid-column-gestures.test.ts:174`; L3: `browser/gestures.spec.mjs:30`, `:68` |
| HG-15 | MUST | pass | L1: `core/header-groups.test.ts:9 'HG-15 …'`, `cases/columns/header-reorder.json:39`; L3: `browser/gestures.spec.mjs:68 'HG-14/HG-15 …'` |
| HG-16 | MUST | pass, accepted as is | L2: `core/grid-header-groups.test.ts:134 'a click on a rectangle sorts nothing …'`. Not asserted under tiers: Focus never enters the header; no tier row copied (the selection and copy code takes no tiers) |
| HG-17 | MUST | accepted as is, no test | No test counts elements per rectangle or row paints with groups (3 groups over 40 columns) |
| HG-18 | MUST | pass | L1: `cases/columns/header-group-toggle.json:61`; L2: `core/grid-header-group-toggle.test.ts:74`, `:110`; L3: `browser/group-toggle.spec.mjs:33 'HG-18 …'` |
| HG-19 | MUST | pass | L1: `cases/columns/header-group-toggle.json:26`, `:31`, `:46` (+4), `cases/columns/header-group-toggle-layout.json:7`, `:19`; L2: `core/grid-header-group-toggle.test.ts:178`, `:183` |
| HG-20 | MUST | pass | L2: `core/grid-header-group-toggle.test.ts:119 'HG-20 …'` |
| HG-21 | MUST | pass | L1: `cases/columns/header-group-toggle-layout.json:31`, `:45`, `:59`, `cases/keys/header-group-keys.json:7`, `:17`, `:24`; L2: `core/grid-header-group-toggle.test.ts:143`, `:155`, `:168` |
| HG-22 | MUST | pass | L1: `cases/columns/header-group-toggle.json:69`; L2: `core/grid-header-group-toggle.test.ts:130`; L3: `browser/group-toggle.spec.mjs:50 'HG-22 …' @blazor` (Server, answers held) |
| HG-23 | MUST | pass | L1: `cases/columns/header-group-toggle-layout.json:68`, `:76`, `:86`; L2: `core/grid-header-group-toggle.test.ts:194`, `:207`, `:225` (+4); L3: `browser/group-toggle.spec.mjs:63` |
| HG-24 | MUST | pass | L2: `core/grid-header-group-toggle.test.ts:322 'HG-24 … wears its %s mark, the reason its description'` (four states); L3: `browser/group-toggle.spec.mjs:81 'HG-24 …'` (tokens, forced colours) |
| DIR-1 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`dir: 'ltr'` on the root) |
| DIR-2 | MUST | pass | L3: `browser/presentation.spec.mjs:403 'inside an RTL ancestor the grid stays an LTR island (DIR-2)'` |
| DIR-3 | MUST | pass | L3: `browser/presentation.spec.mjs:459 'DIR-3 …'` |
| DIR-4 | SHOULD | pass | `ExGrid.Components/ShippedStylesheetTests.cs:53 DIR4_the_shipped_stylesheet_uses_no_logical_property`; the row's grep over `ex-grid.css` finds nothing |
| VZ-1 | MUST | pass | L3: `browser/virtualisation.spec.mjs:212 'the element count is the same at 10³, 10⁵ and 10⁶ rows (VZ-1 …)'` |
| VZ-2 | MUST | pass | L2: `core/grid-dom.test.ts:54` (20 rows at 1000), `:88 'a row that stays on screen is not painted again …'`; L3: `browser/virtualisation.spec.mjs:212`, `:305` |
| VZ-3 | MUST | pass | L1: `cases/viewport/geometry.json:15`, `:22`, `:37` ('fractional heights are carried') |
| VZ-4 | MUST | pass | L1: `cases/columns/geometry.json:373` (slice), `:139`; L2: `core/grid-dom.test.ts:293` (pinned cells while flung); L3: `browser/virtualisation.spec.mjs:579 (DOM-3)` |
| VZ-5 | MUST | pass, accepted as is | L2: `core/grid-dom.test.ts:293` (Placeholders until 150 ms of stillness, real cells at 150); L1: `cases/viewport/geometry.json:73`, `cases/columns/geometry.json:139`. Not asserted: a horizontal fling's Placeholders at layer 2 |
| VZ-6 | MUST | accepted as is, no test | No test sets `virtualiseColumns: false` (none in `packages/*/test`, `tests/browser`, `dotnet/tests`) |
| VZ-7 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`.ex-spacer` 24 + 1000 × 24), `core/grid-pager.test.ts:63` |
| VZ-8 | MUST | pass | core/grid-dom.test.ts:241; cases viewport/compression.json:8, :16, :23 (fixed by #539: the header band counts) |
| VZ-9 | MUST | pass | L1: `cases/viewport/box.json:7`, `:35`, `:53`; L2: `core/grid-dom.test.ts:355`; L3: `browser/scrollbar.spec.mjs:230`, `:240` |
| VZ-10 | MUST | pass | L3: `browser/scrollbar.spec.mjs:266 'at every zoom level, after moving again'` (Linux CI, classic scrollbars) |
| VZ-11 | MUST | pass | core/grid-dom.test.ts:217 (fixed by #539: the first row in view is kept) |
| VZ-12 | MUST | pass | L2: `core/grid-dom.test.ts:369` (reported 0 and band-tall heights throw nothing, paint nothing), `core/grid-header-groups.test.ts:98` (declared height too small throws) |
| VZ-12a | MUST | pass | L3: `browser/stretch.spec.mjs:35`, `:47`, `:90` (VZ-12a) |
| VZ-12b | MUST | pass | L2: `core/grid-dom.test.ts:369 'ADR-0028 (VZ-12b) … heard once'`; L3: `browser/stretch.spec.mjs:74 '… names the parent (VZ-12b)' @blazor` |
| VZ-13 | MUST | pass | core/grid-dom.test.ts:228; core/grid-pager.test.ts:51 (fixed by #539: page-local re-anchor) |
| VZ-14 | MUST | pass | L3: `browser/scrollbar.spec.mjs:345 'the Focus stays readable with the OS doing the scaling'`, run by `ci.yml` `windows-desktop` at 125% (:606) and 150% (:615), Chrome and Edge |
| VZ-15 | MUST | pass | L1: `cases/viewport/compression.json:45`, `:58`, `:79`; L3: `browser/virtualisation.spec.mjs:154 (VZ-15)` and the scrollbar/BIG/SH/DC tests in project `chrome-150` (`playwright.config.mjs:211`) |
| VZ-16 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`--ex-dp`), `core/grid-row-headings.test.ts:85` (2.25), `core/browser-reports.test.ts:95`, `:105` (a window that is not the script's: its Device Pixel read, its change heard, the script's window never asked; +2 `:126`, `:133`, a root no window shows refused, nothing left behind); L3: `browser/device-pixel-225.spec.mjs:37 'VZ-16 …'`, `browser/popout.spec.mjs:18 'VZ-16 / #565: a grid in a window the page opened is told that window's Device Pixel, at its mount and as it changes'`, `browser/windows-desktop.spec.mjs:207 'VZ-16 … a real browser zoom is told …'` (`ci.yml` `windows-desktop` :615) |
| VZ-17 | MUST | pass, accepted as is | L1: `core/column-geometry.test.ts:13`, `:25` (VZ-17), `cases/columns/geometry.json:455`, `ExGrid.Tests/ColumnGeometryTests.cs`; L2: `core/grid-row-headings.test.ts:85`; L3: `browser/part-c.spec.mjs:455 'VZ-17/DC-59 …'`. Not asserted: Selection, Cell Editor and hit test reading the rounded edges; View State, resize report and `####` keeping declared widths at a fractional ratio |
| VZ-18 | MUST | pass | L2: `core/grid-selection.test.ts:165`, `:196`; L3: `browser/virtualisation.spec.mjs:79`, `:113` (VZ-18). The Citrix check is withdrawn (row's note of 2026-11-02) |
| VZ-19 | MUST | pass | L1/2: `core/column-geometry.test.ts:38 'VZ-19 …'`, `ExGrid.Tests/ColumnGeometryTests.cs:57`; L3: `browser/sheet-paper.spec.mjs:714 'SH-41/VZ-19 (${chrome} Chrome) …'` (incl. `chrome-150`); the pixel is `.ex-cell-numeric`'s alone (`ex-grid.css:232`) |
| FL-1 | MUST | pass | react/filter-slot-contract.ts:93 `filterPanelSlotContract`, which imports only `@ex-grid/core`, run by react/chrome.test.ts:125, mui/test/contracts.test.ts:21, mantine/test/contracts.test.ts; inspected: packages/mui/src and packages/mantine/src import only `'@ex-grid/core'` (the package exports `.` alone); the ExGrid.MudBlazor panel is a separate assembly |
| FL-2 | MUST | pass | cases/source/from.json:339 'another column's Filter narrows a column's value list', :354 'a column's own Filter is left out of its value list' |
| FL-3 | MUST | pass | core/grid-filter-panel.test.ts:156 'TooMany gets the condition form'; core/filter-panel-choices.test.ts:148 'TooMany degrades to Excel's search box: contains'; browser/filter-panel.spec.mjs:91 'TooMany gets the condition form' (both Chromes) |
| FL-4 | MUST | pass, accepted as is | OK applies / Cancel discards: core/grid-filter-panel.test.ts:106, :245 'Cancel sends nothing'; core/grid-filter-slot.test.ts:179. **Not asserted:** one `distinctValues` fetch per opening, and no Consumer notification on a tick before OK (the code fetches once, in the `FilterPanelModel` constructor) |
| FL-5 | MUST | pass | cases/query/filter.json:7 'columns combine with And', :17 'clauses within a column can combine with Or', :39 |
| FL-6 | MUST | pass | core/query.test.ts:48 'FL-6: every operator on every column type is either offered, with its cases, or refused'; cases/query/operators.json (108 cases); cases/query/text.json, numbers.json, dates.json; C# ExGrid.Tests/Cases/QueryCaseTests.cs:46 |
| FL-7 | MUST | pass, accepted as is | cases/query/filter.json:48 'the Opaque Filter's meaning is the Consumer's'; cases/view-state/equality.json:3760 'a Query that differs in an Opaque Filter'; core/source-boundary.test.ts:107. **Not asserted:** the Opaque Filter kept through a panel's OK or Clear filter, and absent from the panel, at layer 2 (grid-view.ts:8602 and :8645 keep it) |
| FL-8 | MUST | pass | cases/source/from.json:93 'Row Sequence Version moves when a Filter changes the rows'; cases/selection/held.json:12 'dropped when the sort order changes'; core/grid-selection.test.ts:213 'new rows drop the Selection, and the Consumer hears it empty' |
| FL-9 | MUST | pass | core/filter-panel-choices.test.ts:22 'applied list starts chosen, intersected with the domain just fetched', :33 'set membership, never a count', :43 'nothing chosen is never silently no filter' |
| FL-10 | MUST | pass | core/filter-panel-choices.test.ts:77, :84 'a checked value the search hides is not applied'; browser/popovers.spec.mjs:621 'S sorts, E … Enter applies the search's matches' (both Chromes) |
| FL-11 | MUST | pass | core/filter-panel-choices.test.ts:110, :117; core/grid-filter-panel.test.ts:88 (shows '(Select All Search Results)'), :106; browser/filter-panel.spec.mjs:67 (both Chromes) |
| FL-12 | MUST | pass | core/grid-filter-panel.test.ts:325 'Tab … wraps back to the first command', :357; core/grid-column-menu.test.ts:51; cases/chrome/menu-keys.json:129; browser/popovers.spec.mjs:558, :591 (both Chromes) |
| FL-13 | MUST | pass | core/filter-panel-choices.test.ts:57, :88, :93; core/grid-filter-panel.test.ts:141 'offers to add to the filter in force'; browser/popovers.spec.mjs:720 (both Chromes draw the checkbox) |
| FL-14 | MUST | pass | core/filter-panel-choices.test.ts:173, :179, :184; core/grid-filter-panel.test.ts:245 'two conditions joined by Or apply as one spec' |
| FL-15 | MUST | pass | cases/chrome/menu-keys.json:99, :108, :115 (+4); core/grid-column-menu.test.ts:51 (letters underlined `[S]ort ascending`); browser/popovers.spec.mjs:621, :668, :703 (both Chromes) |
| FL-16 | MUST | pass | core/grid-column-menu.test.ts:51 '[C]lear filter (disabled)', :103 'C clears its filter'; cases/chrome/menu-keys.json:108; browser/popovers.spec.mjs:621 |
| SR-1 | MUST | pass | browser/features.spec.mjs:1141 'a header click sorts and the selection is untouched by it (SR-1)'; browser/sizing.spec.mjs:113; core/grid-selection.test.ts:317 |
| SR-2 | MUST | pass | cases/selection/keyboard.json:80 'Ctrl+Shift+Down from the first row is a whole-column selection', :88 'Ctrl+Space'; core/grid-row-headings.test.ts:229 'a click on the corner selects all' |
| SR-2a | MUST | pass | cases/selection/whole-ranges.json:63, :79, :87; browser/sizing.spec.mjs:96 'Shift+click on a header selects whole columns and does not sort'; core/grid-selection.test.ts:334 |
| SR-2b | MUST | pass | cases/selection/whole-ranges.json:7 'Ctrl+Space then Shift+Right gives two whole columns', :16, :27 (+2) |
| SR-2c | MUST | pass, accepted as is | browser/sizing.spec.mjs:237, :251; browser/headings.spec.mjs:48, :71, :92; core/grid-selection.test.ts:98; core/grid-pager.test.ts:82 (Ctrl+Shift+↓ from the first row turns no page). **Not asserted:** under a pager, Shift+→ over whole columns turns no page |
| SR-2d | MUST | pass | browser/sizing.spec.mjs:113, :125, :149, :164; core/grid-selection.test.ts:357, :382 |
| SR-2e | MUST | pass | C# ExGrid.Tests/HeadingToggleTests.cs:8 (10 Facts); cases/selection/whole-ranges.json:143, :153; browser/sizing.spec.mjs:177, :194, :213 (Meta only where Meta is Command); browser/headings.spec.mjs:369 |
| SR-3 | MUST | pass | inspected: `grep -n '\.sort(' packages/core/src/dom/*.ts` prints nothing; core/grid-dom.test.ts:436 (painted first row follows the source's sort), :446 (a reversed array is painted reversed) |
| SR-4 | MUST | pass, accepted as is | drops: core/grid-marks.test.ts:211 'marks survive a sort …; the Selection does not'; cases/selection/held.json:12. Keeps: cases/selection/held.json:7 'the same order keeps the selection', cases/source/from.json:123, :251 (version unmoved). **Not asserted:** at layer 2, a sort that leaves the sequence identical keeps the Selection |
| SR-5 | MUST | pass | cases/query/sort.json:7, :17, :27, :37 (+11: null ordering, stability, ordinal collation, mixed number types); C# ExGrid.Tests/SortSemanticsTests.cs |
| SR-6 | MUST | pass | core/grid-dom.test.ts:436 'a header says the Sort in force' (`none` / `descending`) |
| ED-1 | MUST | pass, accepted as is | grid-view.ts:4980 makes one `input.ex-editor` per edit, and `#paintEditor` (:12673) floats it outside the rows; core/grid-edit.test.ts:175. **Not asserted:** at most one input under the root while editing, and row writes unchanged by editing |
| ED-2 | MUST | pass | core/grid-edit.test.ts:175, :221 'F2 opens Caret … F2 again is Overwrite', :295; cases/keys/editing-keys.json:7; browser/features.spec.mjs:78, :720, :758 (+9, incl. f4-enter-mode, declarations; :871/:932 @blazor) |
| ED-3 | MUST | pass | core/grid-edit.test.ts:308; cases/keys/editing-keys.json:7 'every mode claims commit, cancel, Tab'; browser/features.spec.mjs:95 'Escape cancels the editor and never blurs the grid mid-edit' |
| ED-4 | MUST | pass | core/grid-edit.test.ts:175; browser/features.spec.mjs:33 (React); browser/mud.spec.mjs:280 'the Wrapper's editor opens … with the typed character' @blazor |
| ED-5 | MUST | pass | core/grid-edit.test.ts:175 (the cell still reads 'Book-0' after the commit; 'the grid writes nothing'); core/grid-paste.test.ts:81 |
| ED-6 | MUST | pass | core/grid-edit.test.ts:785 'an edit with typing in it stays when the Window leaves its row' (the text unchanged); :679 |
| ED-7 | MUST | accepted as is, no test | no test changes the density while an edit is open. `#paintEditor` re-derives the box from `#metrics` on each pass, so the code appears to do it |
| ED-8 | MUST | accepted as is, no test | no test opens an edit over a Placeholder. `#isEditableCell` (grid-view.ts:4849) refuses a row outside the Window and refuses during placeholder mode |
| ED-9 | MUST | pass | browser/presentation.spec.mjs:517 "the editor's box is exactly the cell's (ED-9)" |
| ED-10 | MUST | pass | core/grid-paste.test.ts:81 'a block … raises one intent with every cell'; core/grid-edit.test.ts:392 'Ctrl+Enter fills the Selection with one paste intent' |
| ED-11 | MUST | pass | browser/gestures.spec.mjs:261 'a composing IME keydown is never taken (ED-11)' |
| ED-12 | MUST | pass, accepted as is | core/grid-edit.test.ts:327 'a press on another cell commits and selects it'; core/grid-actions.test.ts:237; core/grid-pending-commit.test.ts:242 (a press made while pending is taken after the answer); core/grid-summary.test.ts:376. **Not asserted:** a header press commits; the editor's input stops propagation; presses on the rows, a header and a column menu during a slow `onEdit` (in order and reversed) raise one Edit Intent, and a Source replaced afterwards still discards the next editor |
| ED-13 | MUST | pass | core/grid-edit.test.ts:202 'ED-13: Control and Alt together type a character'; browser/features.spec.mjs:60 |
| ED-14 | MUST | pass | core/grid-edit.test.ts:175 (no validate → Accept → intent), :349, :376 (a Flag raises it unchanged); core/grid-cell-message.test.ts:291 |
| ED-15 | MUST | pass | core/grid-edit.test.ts:349 'a Reject holds every commit gesture'; core/grid-pending-commit.test.ts:242; browser/features.spec.mjs:980, :996; browser/script-grid.spec.mjs:112 @blazor |
| ED-16 | MUST | pass | core/grid-edit.test.ts:349 (a Flag raises the intent with '-5' unchanged); core/grid-cell-paint.test.ts:48 (a Cell State is painted only from the function); blazor/bridge.test.ts:806 |
| ED-17 | MUST | pass | core/grid-cell-message.test.ts:43 'asked once the Focus has stood still for 300 ms, never while painting', :105 'a run of arrows faster than the delay asks nothing', :228 |
| ED-18 | MUST | pass, accepted as is | shape refusals: core/grid-paste.test.ts:211, :226, :259. **Not asserted:** that no validate function runs during a clipboard paste (grid-paste.ts never names `validate`) |
| ED-17b | MUST | pass | core/grid-cell-message.test.ts:118, :141, :158 (the open editor's message outranks it), :177; browser/features.spec.mjs:1018 |
| ED-17c | MUST | pass | core/grid-bands.test.ts:26 'a move within that row paints nothing', :45 'off unless asked'; core/grid-cell-message.test.ts:241 'not heard for a rest while nobody asks why'; inspected: packages/blazor/src has no pointer listener |
| ED-19 | MUST | pass | browser/features.spec.mjs:385 'ED-19: after a refused fill the editor stands, and Enter then raises exactly one Edit Intent'; core/grid-edit.test.ts:392 |
| ED-20 | MUST | pass, accepted as is | core/grid-pending-commit.test.ts:360 'Ctrl+Enter waits for the verdict, a Reject holds the editor, and an Accept raises one fill'; browser/script-grid.spec.mjs:172 @blazor; C# ExGrid.Components/ScriptGridAnswerTests.cs:75. **Not asserted:** exactly one validate call per fill, and no validate call under the Editable refusal |
| ED-21 | MUST | pass, accepted as is | core/grid-edit.test.ts:740 (`columnsChanged`, `sourceChanged`), :762 (`columnNoLongerEditable`), :436/:555 (`orderMoved`), :875 (a handler hands over other columns, accepted, and no discard follows). **Not asserted:** `rowLeftTheWindow`; a commit whose handler replaces the Source (accepted or refused); a refused commit whose handler changes the columns (the refused intent, then one discard) |
| ED-22 | MUST | pass | browser/circuit.spec.mjs:38 (describe), :595; browser/edit-stands.spec.mjs:540 (describe), :730 (describe); browser/declarations.spec.mjs:870, :2067 (+many); core/grid-pending-commit.test.ts:552 |
| ED-23 | MUST | pass | core/grid-edit.test.ts:308 'Backspace opens an empty Overwrite'; browser/excel-keys.spec.mjs:96 |
| ED-24 | MUST | pass, accepted as is | core/grid-edit.test.ts:945 (one Clear Intent whose targets are the selection; refused whole as `targetNotEditable`); browser/excel-keys.spec.mjs:78. **Not asserted:** zero validate calls; the intent carries the Row Sequence Version |
| ED-25 | MUST | pass | cases/keys/grid-keys.json:186; browser/excel-keys.spec.mjs:51 (inside the editor), :136 (display-only) |
| ED-26 | MUST | pass | browser/edit-stands.spec.mjs:61, :153 (both Chromes), :180, :350 @blazor, :287, :323; browser/pointing-scope.spec.mjs:216 |
| ED-27 | MUST | pass | browser/edit-stands.spec.mjs:380 (both Chromes), :438; C# ExGrid.Components/ShippedStylesheetTests.cs:290 An_edit_whose_keyboard_is_elsewhere_is_drawn_with_a_1px_outline; core/stylesheet-lengths.test.ts:65 |
| ED-28 | MUST | pass, accepted as is | browser/edit-stands.spec.mjs:471 (describe, 40 ms, both Chromes): :482 thirty times, :508 '=1'; core/grid-chrome-slots.test.ts:192. **Not asserted:** on /sheet at 150 ms, a character typed onto a cell and the Formula Bar pressed in the task that paints the Cell Editor, after which the bar keeps the keyboard (declarations.spec.mjs:870 is the nearest case) |
| ED-29 | MUST | pass | browser/declarations.spec.mjs:832, :870 @blazor, :899 (case 7k), :1214 |
| ED-30 | MUST | accepted as is, no test | composition held: core/grid-edit.test.ts:489, :508; browser/key-field.spec.mjs:187, :225, :249 (+9, incl. k6 :337, k15 :475, 150 ms @blazor); browser/windows-ime.spec.mjs:85 … :374 (CI `windows-ime`). **Missing:** a composition finished while a popover opens (Ctrl+F, Ctrl+1, Format Cells…). The 2026-10-30 note removed its tests and put nothing in their place, but the Statement and Pass columns still name the clause |
| ED-31 | MUST | pass, accepted as is | browser/circuit.spec.mjs:630 'a press made behind a page move lands on the row painted under it at the press' @blazor; core/grid-selection.test.ts:459 (a press before the grid heard a scroll); core/grid-actions.test.ts:267. **Not asserted:** at layers 1/2, a press told an earlier slice or layout, another row order, reordered columns, or a layout no longer kept |
| ED-32 | MUST | pass | core/grid-pending-commit.test.ts:150, :169, :380 |
| ED-33 | MUST | pass | core/grid-pending-commit.test.ts:182 (aria-busy, nothing applied), :207, :242; browser/script-grid.spec.mjs:86 @blazor |
| ED-34 | MUST | known limitation, carried from 0.2.0 | open on Blazor (ADR-0240's note of 2026-10-29): a cancel that reaches .NET after a synchronous `OnEdit`/`OnPaste` has written is still said as cancelled. The React/Script Core half is held: core/grid-pending-commit.test.ts:429, :458, :533; core/grid-fill-handle.test.ts:171; blazor/bridge.test.ts:769; C# ScriptGridAnswerTests.cs:93, ScriptGridFillTests.cs:85 |
| ED-35 | MUST | pass | blazor/bridge.test.ts:750, :784, :806; C# ExGrid.Components/ScriptGridAnswerTests.cs:75, :115, :222; browser/script-grid.spec.mjs:112, :133, :172 @blazor |
| ED-36 | MUST | pass, accepted as is | layer 1: core/grid-breach.test.ts:60, :83, :92 (a set rule without a Row Key throws); C# ExGrid.Tests/GridRulesetTests.cs:156 ED36_a_set_rule_marks_every_cell_its_breach_names_by_row_key, :186. **Not asserted (layer 2):** `Error` painted on every named cell, one outside the Window once scrolled to, with the editor closed on the commit |
| ED-37 | MUST | pass | core/grid-breach.test.ts:74; C# GridRulesetTests.cs:173 ED37_every_involved_cell_carries_the_breach_message |
| ED-38 | MUST | pass | core/grid-breach.test.ts:119, :184 'F8 … brings it to an error far below the Window', :206 (no findError: F8 is the browser's); C# GridRulesetTests.cs:245; blazor/bridge.test.ts:898; browser/script-grid.spec.mjs:145 @blazor |
| ED-39 | MUST | pass, accepted as is | core/grid-breach.test.ts:269, :286 (a Filter says hidden; the press steps as F8), :304 (asked again; a late answer dropped), :328; C# GridRulesetTests.cs:275, ScriptGridAnswerTests.cs:337. **Not asserted:** asked again on another source or another set of visible columns |
| SL-1 | MUST | pass | cases/selection/keyboard.json:242; C# ExGrid.Tests/SelectionKeyboardTests.cs:15 SL1_select_all_over_a_million_rows_is_one_rectangle_and_allocates_nothing_per_cell |
| SL-2 | MUST | pass | cases/selection/keyboard.json:251, :260, :269; C# SelectionKeyboardTests.cs:36 SL2_selecting_the_whole_result_succeeds_at_any_size; core/selection-boundary.test.ts:46 |
| SL-3 | MUST | pass, accepted as is | version: cases/selection/held.json:7, :12, :17; core/grid-selection.test.ts:213; core/grid-marks.test.ts:211. Negative case: cases/source/from.json:251 (an edit that moves nothing keeps the version). **Not asserted (layer 2):** the Selection dropped when the visible-column set changes (core/grid-copy.test.ts:468 checks only the outline), and kept when values change and the order does not (`#reconcileSelection` does both) |
| SL-4 | MUST | pass, accepted as is | core/grid-dom.test.ts:284 (a fresh array of the same columns is no change). **Not asserted:** the Selection survives it. `#reconcileSelection` compares names in order (`sameItems`) |
| SL-5 | MUST | pass | cases/selection/range.json:21, :26, :31; cases/selection/gestures.json:73, :113, :129 (the only cell cannot be taken out); cases/selection/active-cell-third-run.json:62 … :162 |
| SL-6 | MUST | pass, accepted as is | core/grid-selection.test.ts:119 (one `ex-range` element for the range, beside its tint and the Focus); browser/virtualisation.spec.mjs:327. **Not asserted:** no cell class changes when the selection moves |
| SL-7 | MUST | pass | core/grid-off-screen.test.ts:28 (scrolled out, said, and brought back); core/grid-edit.test.ts:785 (editor text survives its row leaving) |
| SL-8 | MUST | pass | cases/selection/queries.json:16, :24 'overlapping ranges count twice'; cases/selection/range.json:19 |
| SL-9 | MUST | pass | core/grid-pager.test.ts:103 'Ctrl+A selects the page, and the status line … offers the whole result'; cases/selection/keyboard.json:219 |
| SL-10 | MUST | pass | core/grid-off-screen.test.ts:28 (scrolling); core/grid-pager.test.ts:103 (paging: 'outside the visible range') |
| SL-11 | MUST | pass | browser/features.spec.mjs:1628 'SL-11: a drag held against either boundary of a page does not turn it' |
| SL-12 | MUST | pass | cases/selection/edge-band.json:27 … (the rate rises with depth); core/grid-selection.test.ts:412 'a drag held in the bottom edge band scrolls a step a tick and extends with it' |
| SL-13 | MUST | pass | cases/selection/edge-band.json:321 'the ceiling stays one row short of the fling threshold', :328 |
| SL-14 | MUST | pass | browser/presentation.spec.mjs:537 @behaviour, :576 @blazor |
| SL-15 | MUST | pass, accepted as is | released: browser/presentation.spec.mjs:537; core/grid-selection.test.ts:473, :498. **Not asserted:** coming back with the button held resumes the drag (layer 3) |
| SL-16 | MUST | pass | cases/selection/keyboard.json:219 'Ctrl+A under a pager names the page, and the Focus stays'; core/grid-pager.test.ts:103 |
| KB-1 | MUST | pass | inspected: grid-view.ts:2320 `root.addEventListener('keydown', …, true)`; browser/edit-stands.spec.mjs:61 (both Chromes); browser/mud-app.spec.mjs:247 @blazor; browser/features.spec.mjs:1122 |
| KB-2 | MUST | pass | inspection: the tables are in packages/core/src/keys/ (grid-keys.ts, editing-keys.ts). The only key sets in grid-view.ts are `modifierKeys` (the gate's release mechanics) and `appleCaretKeys` (Home and End on Apple, which the restatement names), with the PageUp/PageDown drop at :5115; C# ExGrid.Docs.Generator.Tests/KeyboardCompletenessTests.cs |
| KB-3 | MUST | pass | cases/keys/grid-keys.json:16, :24; browser/features.spec.mjs:1653 (off Apple, Meta+Enter fills nothing); `macos-desktop` in ci.yml:706 runs features.spec.mjs, sheet-vs-excel, sizing, part-c and write-lands with grep `$KB3` ('ED-19: after a refused fill', 'on an Apple platform': features.spec.mjs:385, :913, :932) |
| KB-4 | MUST | pass, accepted as is | cases/keys/grid-keys.json:16, :24 (keyboard); browser/sizing.spec.mjs:213 (pointer: 'Meta counts as Ctrl … only where Meta is Command'). **Not asserted:** one layer-2 test that Ctrl+click and Ctrl+A agree for each platform value. Both read `#metaIsPrimary` (grid-view.ts:7547, :10370) |
| KB-5 | MUST | pass | cases/keys/grid-keys.json:9 … :59 (Home/End with Ctrl and Shift), :109; cases/selection/keyboard.json:7, :42, :54, :72; cases/selection/active-cell.json:119 |
| KB-6 | MUST | pass | cases/selection/cycling.json:7, :15, :23, :31, :39 |
| KB-7 | MUST | pass | cases/selection/cycling.json:92, :101 'no wrap target is invented' |
| KB-8 | MUST | pass | browser/features.spec.mjs:125, :167, :184, :236 (+:197 @blazor); browser/key-field.spec.mjs:574, :598, :848; browser/sheet.spec.mjs:820, :848 (both Chromes) |
| KB-9 | MUST | pass | core/grid-selection.test.ts:151 'the first key with nothing selected places the Focus on the first cell on screen, without its step' |
| KB-10 | MUST | pass | browser/features.spec.mjs:1679 'KB-10: Home and Ctrl+Home … pinned and unpinned alike' |
| KB-11 | MUST | pass | browser/features.spec.mjs:1182 'KB-11: arrows typed in a Template Column's input move its caret' |
| KB-12 | MUST | pass | browser/key-field.spec.mjs:616 (both Chromes), :865; browser/features.spec.mjs:1086; core/grid-edit.test.ts:175 |
| KB-13 | MUST | accepted as is, no test | only the key table: cases/keys/grid-keys.json:109 (`moveByViewport`); cases/selection/keyboard.json:149 (the Focus moves by the rows passed). **Missing:** Focus and Viewport moving by the same N, and the Focus's offset unchanged after presses 2 and 3 |
| KB-14 | MUST | accepted as is, no test | parts at layer 1: cases/selection/keyboard.json:160, :168 (the Focus clamps); cases/viewport/geometry.json:91, :105 (reveal). **Missing:** PageDown to the end at layer 2, with the Focus fully visible at every step |
| KB-14a | MUST | accepted as is, no test | no test reports a shrinking box (or a scrollbar appearing over the Focus) and checks scroll and reveal. core/grid-selection.test.ts:572 covers only a box that grows back |
| KB-15 | MUST | pass | browser/features.spec.mjs:1048 'Ctrl+PageDown is neither handled nor prevented (KB-15)'; cases/keys/grid-keys.json (the Control forms of PageUp/PageDown are the browser's) |
| KB-16 | MUST | pass | ran §22 Step 3's command (dod.md:1078) on packages/core/src/keys/: it printed nothing |
| KB-17 | MUST | pass | core/grid-context-menu.test.ts:198; core/grid-column-menu.test.ts:51, :69; core/grid-filter-panel.test.ts:245 (Cancel sends nothing); browser/popovers.spec.mjs:806 (both Chromes) |
| KB-18 | MUST | pass | core/grid-actions.test.ts:413 (Escape from the control: the root is focused); browser/features.spec.mjs:1416 on /cells |
| KB-19 | MUST | pass | browser/features.spec.mjs:1155 'a display-only grid does not take printable keys away from the page' |
| KB-20 | MUST | pass | core/grid-actions.test.ts:118; browser/features.spec.mjs:1365 |
| KB-21 | MUST | pass | core/grid-actions.test.ts:118 'the arrows choose, clamped; Space fires the chosen one and leaves'; browser/features.spec.mjs:1365 |
| KB-22 | MUST | pass | core/grid-actions.test.ts:155; browser/features.spec.mjs:1393, :1405 |
| KB-23 | MUST | pass | core/grid-actions.test.ts:413 (one request; no later mount handed it), :380; browser/page-follow.spec.mjs:247 @blazor |
| KB-24 | MUST | pass | browser/features.spec.mjs:1416 'Space puts the caret in a Template cell's field, and Escape brings the keyboard back' |
| KB-25 | MUST | pass | core/grid-edit.test.ts:417 'Space opens Overwrite …; with nothing selected Space opens nothing' |
| KB-26 | MUST | pass | core/grid-actions.test.ts:95 'a held Space fires once'; browser/features.spec.mjs:1433, :1447 |
| KB-27 | MUST | pass | browser/features.spec.mjs:1499 'a press dragged off an action leaves no button holding the keyboard' |
| KB-36 | MUST | pass | core/grid-actions.test.ts:176 'a click fires … and moves no Focus'; browser/inspectors.spec.mjs:91 (RI-4), :108 (RI-26), :119 (RI-5), :127 (RI-6), :139 (RI-7) @blazor |
| KB-28 | MUST | pass | cases/keys/grid-keys.json:100; core/grid-column-menu.test.ts:79; browser/popovers.spec.mjs:846 |
| KB-29 | MUST | pass | core/grid-context-menu.test.ts:131; core/grid-filter-slot.test.ts:215; browser/popovers.spec.mjs:241 (both Chromes) |
| KB-30 | MUST | pass | cases/chrome/menu-keys.json:7 … :153; core/grid-context-menu.test.ts:157, :173; browser/popovers.spec.mjs:514, :542, :576 (both Chromes) (+mui/mantine specs) |
| KB-31 | MUST | pass | core/grid-filter-slot.test.ts:290; react/filter-slot-contract.ts:111; browser/popovers.spec.mjs:591, :754 (both Chromes); browser/circuit.spec.mjs:438 @blazor |
| KB-32 | MUST | pass | browser/popovers.spec.mjs:283 (both Chromes), :332, :438 (+3 @blazor, keys typed at once with the answers held); browser/mud-app.spec.mjs:542 (from inside an Inner Popup) |
| KB-33 | MUST | pass | browser/circuit.spec.mjs:297 'keys typed straight after a key that opens a menu reach the menu (KB-33)' @blazor |
| KB-34 | MUST | pass, accepted as is | cases/chrome/menu-keys.json:7 (the key resolved against the place); browser/circuit.spec.mjs:275 '↓ Enter … Copy with headers ran' @blazor, built-in Chrome. **Not asserted:** under the mud Chrome |
| KB-35 | MUST | pass | browser/mud-app.spec.mjs:607 (MudSelect: the list closes, the popover stands; MudDatePicker: the popover stands) @blazor, 150 ms |
| KB-40 | MUST | pass, accepted as is | browser/features.spec.mjs:1713; browser/page-follow.spec.mjs:103; cases/selection/keyboard.json:176 (Shift+PageDown moves the Extent). **Not asserted:** Shift+PageDown moves the view a page with the Extent (grid or browser) |
| KB-41 | MUST | pass | cases/selection/cycling.json:133, :146, :157; browser/sheet-vs-excel.spec.mjs:281 |
| KB-42 | MUST | pass | cases/selection/active-cell.json:67, :76, :103; browser/sheet-vs-excel.spec.mjs:320; browser/features.spec.mjs:957 |
| KB-43 | MUST | pass | cases/selection/active-cell-third-run.json:30, :41, :142 (+2) |
| KB-44 | MUST | pass, accepted as is | core/grid-leave.test.ts:63, :74, :89; core/grid-pending-commit.test.ts:458; browser/pivot.spec.mjs:498; browser/features.spec.mjs:249. **Not asserted:** a held Escape that closes a Formula Entry's list leaves the edit standing |
| KB-45 | MUST | pass, accepted as is | browser/page-follow.spec.mjs:80 (arrows, both kinds), :103, :153 (resize not followed), :169 (editor) @behaviour; :195, :221 (scripted focus) @blazor; core/grid-edit.test.ts:160. **Not asserted:** that Ctrl+arrows, Home/End, PageUp/PageDown, Enter/Tab cycling and Ctrl+Backspace each follow; no follow once the keyboard has left or the Focus has moved on; the follow on the Blazor hosts |
| KB-39 | MUST | pass | cases/keys/grid-keys.json:144; core/grid-edit.test.ts:967; browser/excel-keys.spec.mjs:28, :136 |
| CP-1 | MUST | pass, accepted as is | L1 `cases/clipboard/copy.json:15 'past the cap it does not copy at all'`; L2 `core/grid-copy.test.ts:110 'no Selection, and one past the cap, are refused'`; L3 `browser/features.spec.mjs:321 'a misaligned selection refuses and the clipboard stays untouched (CP-1/CP-3)'`. Not asserted: in a real browser, the clipboard untouched by a copy refused **for the cap** (layer 3 refuses only a misaligned one) |
| CP-2 | MUST | pass, accepted as is | `cases/clipboard/copy.json:22 'a whole column of a million cells copies'`, `:30`, `:39 'the cap is the Consumer's to raise'`; `core/grid-copy.test.ts:110` (cap 2: 2 copy, 3 refuse). Not asserted: 1,000,001 cells refused at the default cap (the +1 boundary is held at cap 2 only) |
| CP-3 | MUST | pass | `cases/clipboard/copy.json:108 'nothing selected…'`, `:78 'ranges lining up on neither axis…'`, `:114 'a misaligned selection over the cap reports the misalignment'`; `core/grid-copy.test.ts:94` |
| CP-4 | MUST | pass | `browser/features.spec.mjs:272 'the clipboard carries both formats, and #### never reaches it'` @behaviour; `core/grid-copy.test.ts:73` |
| CP-5 | MUST | pass | `browser/features.spec.mjs:272` (narrowed column paints `#`, clipboard holds 1000000); `core/grid-copy.test.ts:73` |
| CP-6 | MUST | pass, accepted as is | React: `browser/features.spec.mjs:501 'CP-6 / ADR-0005: a Ctrl+C inside the Window writes with no permission granted and no prompt, by the copy event'`; Blazor: `browser/declarations.spec.mjs:1990 'CP-6/CP-10/CP-14 … (mud Chrome)'`, `browser/key-field.spec.mjs:526` (both with permissions granted). Not asserted: event route / no prompt on the Blazor hosts |
| CP-7 | MUST | pass | L1 `cases/source/fetch.json:384 'rows for a copy are one fetch … and kept nowhere'`; L2 `core/grid-copy.test.ts:141`, `:498`; L3 `browser/features.spec.mjs:1738 'CP-7: a copy of rows beyond the Window takes the asynchronous route'` |
| CP-8 | MUST | pass, accepted as is | No test copies after a column reorder or with a column hidden. Code: copy reads the resolved columns, which are the display order the Consumer hands, a hidden column being one left out (`packages/core/src/dom/grid-copy.ts:26`, `view-state.ts:54`) |
| CP-9 | MUST | pass | `cases/clipboard/assemble.json:75 'vertically aligned ranges stack in position order'`, `cases/clipboard/copy.json:98 'segments come back in position order'`, `:88 'overlapping row spans … refused as misaligned'` |
| CP-10 | MUST | pass | L3 `browser/features.spec.mjs:561 'CP-10 / ADR-0005: text selected with the mouse, then …'` (3 ways) @behaviour, `browser/declarations.spec.mjs:1990` (mud, @blazor); L2 `core/grid-copy.test.ts:271` |
| CP-11 | MUST | pass | `cases/clipboard/paste.json:7`, `:86`, `:94`, `:102`, `:110 'range to one cell is refused'`, `:117 'a ragged shape is refused'` |
| CP-12 | MUST | pass | `cases/clipboard/paste.json:138 'a block into disjoint ranges is refused'`, `:148` |
| CP-13 | MUST | pass | `cases/clipboard/paste.json:178 'a cell outside every target range is a caller's defect'`, `:156`; `core/grid-paste.test.ts:81` |
| CP-14 | MUST | pass, accepted as is | React: `browser/features.spec.mjs:597 'an Excel-shaped paste arrives typed and at full precision'`, `:642`; L1 `cases/clipboard/parse.json:73`, `:117`. Not asserted: an Excel-shaped paste on the Blazor hosts arriving typed in .NET (Blazor pastes are text only: `browser/circuit.spec.mjs:151`, `declarations.spec.mjs:1990`) |
| CP-15 | MUST | pass | `cases/clipboard/copy.json:108`, `cases/clipboard/paste.json:172 'nowhere to paste is refused with its own reason'` |
| CP-16 | MUST | pass | `cases/clipboard/paste.json:202`, `:235 'the declaration outranks every shape rule'`; `core/grid-paste.test.ts:211`; Ctrl+Enter: `core/grid-edit.test.ts:392` |
| CP-17 | MUST | pass | L1 `cases/clipboard/assemble.json:15`, `:23` (raw form), `:47`, `:57`; L3 `browser/features.spec.mjs:521` (header row), `browser/pivot-csv.spec.mjs:372` (raw form in HTML) |
| CP-18 | MUST | pass | `cases/clipboard/copy.json:122 'a selection exactly on it copies plainly and refuses with headers'`, `:131`; `core/grid-context-menu.test.ts:263` |
| CP-19 | MUST | pass | `browser/features.spec.mjs:521 'CP-19 / ADR-0005/0036: a menu copy writes with no permission granted and no prompt, by the asynchronous route'` (chrome + edge projects) |
| CP-20 | MUST | pass | `cases/clipboard/parse.json:13`, `:21`, `:27` |
| CP-21 | MUST | pass | `browser/circuit.spec.mjs:151 'a paste past a Server hub's message limit arrives whole (CP-21)'` @blazor, `browser/script-grid.spec.mjs:191`; console via `browser/fixtures.mjs:515-530` |
| CP-22 | MUST | pass | `core/grid-paste.test.ts:259 'a paste past the byte cap is refused whole, counted in UTF-8 across both flavours'` (exactly on cap parsed, one past refused, no intent; judged in the browser before .NET reads anything); default `packages/core/src/dom/grid-paste.ts:6` |
| CP-23 | MUST | pass | L2 `core/grid-copy.test.ts:168`, `:519`; L3 `browser/circuit.spec.mjs:254 'a clipboard write the browser rejects is refused by name (CP-23)'` @blazor |
| CP-24 | MUST | pass | L1 `cases/clipboard/fill.json:7`, `:15`, `:23`, `:31`; L2 `core/grid-edit.test.ts:995`; L3 `browser/excel-keys.spec.mjs:115` |
| CP-25 | MUST | pass | `cases/clipboard/fill.json:39`, `:53`, `:68`, `:76`, `:84`, `:93` (+3); `core/grid-edit.test.ts:995`, `:1020` (sourceUnavailable) |
| CP-26 | MUST | pass | `core/grid-copy.test.ts:306`, `:331`, `:588`; `browser/copied-range.spec.mjs:42` |
| CP-27 | MUST | pass | `core/grid-copy.test.ts:331`, `:354`, `:379` (+1); `browser/copied-range.spec.mjs:82` |
| CP-28 | MUST | pass, accepted as is | `core/grid-copy.test.ts:306 'CP-26/CP-28 … only the next Escape leaves'`, `core/grid-leave.test.ts:119` (Interactive before outline); `browser/copied-range.spec.mjs:99`. Not asserted: the popover and the control layers peeled before the outline |
| CP-29 | MUST | pass, accepted as is | `core/grid-copy.test.ts:468 'the order moving or the columns changing drops the outline'`. Not asserted: an edit opening (F2) removing it; `grid-view.ts:4946` (`#startEdit` clears `#copied`) does |
| CP-30 | MUST | pass, accepted as is | `core/grid-copy.test.ts:447`; `browser/copied-range.spec.mjs:120` (`?upstream=1`, F9). Not asserted: the same for a copy gathered beyond the Window |
| CP-31 | MUST | pass | `browser/copied-range.spec.mjs:99 '… a paste leaves it (CP-28, CP-31)'` (no layer-2 test found) |
| CP-33 | MUST | pass | `cases/keys/copy-with-headers-keys.json`; `core/grid-context-menu.test.ts:293`, `:312`, `:330` |
| CP-32 | MUST | pass | `browser/copied-range.spec.mjs:145 'draws no outline, and the copy still lands'`, `:42` (dashed/none/0s, top edge pixels); `core/grid-copy.test.ts:550` |
| CTX-1 | MUST | pass | `core/grid-context-menu.test.ts:76`, `:93`; `browser/popovers.spec.mjs:178` (builtin @behaviour, mud @blazor) |
| CTX-2 | MUST | pass | `core/grid-context-menu.test.ts:106 'the core's commands, then the Consumer's'`; `browser/popovers.spec.mjs:152` |
| CTX-3 | MUST | pass | `core/grid-context-menu.test.ts:106 '… handed the row, the column and the Selection as rectangles in their version'` |
| CTX-4 | MUST | pass, accepted as is | L1 `cases/keys/grid-keys.json:135`; L2 `core/grid-context-menu.test.ts:131`; L3 `browser/popovers.spec.mjs:190 'the ContextMenu key opens it on the Focus'`. Not asserted in layer 3: Shift+F10, and the browser's menu staying away under either key (`defaultPrevented` is checked for the mouse only, `:152`) |
| CTX-5 | MUST | pass | Inspection: `grid-view.ts:2328` (Viewport `contextmenu`), `:2282` (summary box), `menu-popover.ts:172-174` (its container); `summary-menu.ts` listens on its own element only |
| BIG-1 | MUST | pass | `browser/virtualisation.spec.mjs:39 'the far corner is reachable and painted at 10⁶ rows (BIG-1)'` |
| BIG-2 | MUST | pass | `browser/virtualisation.spec.mjs:212 'the element count is the same at 10³, 10⁵ and 10⁶ rows'` |
| BIG-3 | MUST | pass | `browser/virtualisation.spec.mjs:327 'Ctrl+A over 10⁶ × 100 is one rectangle, counted as 10⁸'` |
| BIG-4 | MUST | pass | `cases/viewport/compression.json:8`, `:16`, `:23 'a true height past the ceiling is refused'`; `cases/viewport/geometry.json:119` |
| BIG-5 | MUST | pass | `browser/virtualisation.spec.mjs:227`, `:260` |
| BIG-6 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/observational.spec.mjs:149 '… (DOM-5, PF-6, BIG-6)'` → metrics.json `BIG-6` |
| BIG-7 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/observational.spec.mjs:136 'mount to first painted row at 10⁶ rows (BIG-7)'` |
| PST-1 | MUST | pass | `cases/clipboard/paste.json:156 'one value into a whole column of a million rows is one range'`; `core/grid-paste.test.ts:81` |
| PST-2 | MUST | pass | `cases/clipboard/paste.json:156 '… rows not fetched included'`, `:164` |
| PST-3 | MUST | pass | `browser/gestures.spec.mjs:139`; `core/grid-off-screen.test.ts:28` |
| PST-4 | MUST | pass | `cases/clipboard/paste.json:128 'a payload far larger than the target is refused by shape, with no plan to raise an intent from'` |
| PST-5 | MUST | pass | `browser/gestures.spec.mjs:160 'a ~10MB paste parses without freezing the grid'` (next key ≤ 500 ms) |
| PST-6 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/gestures.spec.mjs:160` → metrics.json `PST-6` (see notes: 400 source cells, not 10⁵/10⁶) |
| PST-7 | MUST | pass | `cases/clipboard/parse.json:111 '&nbsp; is value, not markup'` |
| ERR-1 | MUST | pass, accepted as is | copy cap/misalignment/empty `cases/clipboard/copy.json:15`, `:78`, `:108`; paste shape/editability `paste.json:117`, `:202`; oversized `viewport/compression.json:23`; width `columns/column-width-spec.json:12`; gutter `viewport/box.json:145`, `:151`. Not asserted: both-or-neither rows/Source (`grid-view.ts:2651`, `ExGrid.cs:491` throw; no test) |
| ERR-2 | MUST | pass | L1 `ExGrid.Tests/RefusalWordsTests.cs:39`, `:76`, `:89` (+3), `core/grid-words.test.ts:30`; L2 `core/grid-copy.test.ts:129`, `:243`, `core/grid-paste.test.ts:162`; L3 `browser/features.spec.mjs:403` |
| ERR-3 | MUST | pass | `core/fetch-boundary.test.ts:41 'a page whose rows run past its own total is refused as a failed fetch'`, `:52` |
| ERR-4 | MUST | pass | core/grid-distinct-rows.test.ts:53, :59; cases source/from.json:305 (fixed by #539) |
| ERR-5 | MUST | pass | `cases/source/fetch.json:313 'a failure is heard, kept, and leaves the rows on screen standing'`, `:330`, `:343` |
| ERR-6 | MUST | pass | as ERR-10: ExGrid.Components/ScriptGridHandleTests.cs; browser/shipped-files.spec.mjs (row restated by #540) |
| ERR-7 | MUST | pass | L3 `browser/features.spec.mjs:1768 'ERR-7: disposing a grid while its interop calls are in flight …'` @blazor; L2 `ExGrid.Components/ScriptGridSurfaceTests.cs:138` |
| ERR-8 | MUST | pass | `cases/viewport/box.json:145 'a vertical scrollbar that swallows the width is refused by name'`; `core/grid-copy.test.ts:110` (names `copyCellCap`) |
| ERR-9 | MUST | pass | `ExGrid.Components/ShippedAssetTests.cs:78`, `:93`; `ShippedFilesTests.cs:21`, `:31` (+Wrappers'); L3 `browser/freshness.spec.mjs:194`, `browser/shipped-files.spec.mjs:112`, `:147` |
| ERR-10 | MUST | pass, accepted as is | L2 `ExGrid.Components/ScriptGridHandleTests.cs:61` (another stamp); L3 `browser/shipped-files.spec.mjs:186` (another version, older than the stamp), `:197`, `:213` (integrity-pinned). Not asserted for ExGrid in layer 2: no stamp, and no module |
| ERR-11 | MUST | pass | L2 `core/grid-dom.test.ts:37`, `pivot/test/pivot-view.test.ts:660`, `sheet/test/create-sheet.test.ts:2416`, `ExGrid.Components/ShippedAssetTests.cs:102` (+2); L3 `browser/shipped-files.spec.mjs:262`, `:276`, `:289` per product |
| CON-1 | MUST | pass | `browser/fixtures.mjs:515` (every test's fixture, written to console.json); `browser/harness.spec.mjs:204` |
| CON-2 | MUST | pass | `browser/fixtures.mjs:516` |
| CON-3 | MUST | pass | `browser/fixtures.mjs:523` (third-party warnings left for results.md) |
| CON-4 | MUST | pass | `browser/presentation.spec.mjs:141 'CON-4 / ADR-0013: no ResizeObserver loop error from attach through density changes and window resizes'` |
| CON-5 | MUST | pass | `browser/presentation.spec.mjs:604 'the Blazor error UI never appears (CON-5)'` @blazor |
| CON-6 | MUST | pass | `browser/fixtures.mjs:524` (Server host log per test, `:381`; WebAssembly's in the console) |
| CON-7 | MUST | pass | `dotnet/tests/UnobservedTaskWatch.cs:22` (assembly fixture, `GC.Collect`/`WaitForPendingFinalizers` at the end), compiled into every bUnit suite (`Directory.Build.targets:52`) |
| CON-8 | MUST | pass | `.oxlintrc.json:50` (`no-console`); `browser/fixtures.mjs:530`; `browser/features.spec.mjs:675 'CON-8 … a copy whose payload fails to build says so'` @blazor; `browser/harness.spec.mjs:234`, `:240`, `:246` |
| PF-1 | MUST | pass | Grep of `packages/*/src` and `dotnet/src`: only `grid-view.ts:11289` (ADR-0204's follow) and `sheet-blazor/src/razor-sheet-slots.ts:342,346` (toolbar list placement), both allowed; `browser/virtualisation.spec.mjs:504 'a scroll frame makes at most one interop call (PF-1)'` @blazor |
| PF-2 | MUST | pass | pivot/pivot-view.test.ts:431 (fixed by #539: the toggles are heard on the report grid's root) |
| PF-3 | MUST | pass, accepted as is | Inspection `paint/cell-classes.ts` (one string per combination, composed once); `core/grid-cell-paint.test.ts:130`, `core/grid-header-groups.test.ts:113`, `core/grid-dom.test.ts:88`. Not asserted: the allocation slope (no Script Core measurement, as the row says) |
| PF-4 | MUST | pass, accepted as is | No test counts offset reads (`OffsetReads` went with the C# core). Code: `grid-view.ts:3333` `#onScroll` reads `scrollTop` and `scrollLeft` once each per scroll event |
| PF-5 | MUST | pass, accepted as is | Element count: `browser/virtualisation.spec.mjs:327` (10⁸ cells, Pinned Columns: +6 elements), `core/grid-selection.test.ts:119`, `:255`. Not asserted: rows painted independent of selection area (no layer-2 count) |
| PF-6 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/observational.spec.mjs:149` → `PF-6` |
| PF-7 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/observational.spec.mjs:193 'a selection drag, one row a step, costs this much per step (PF-7)'` |
| PF-8 | OBSERVATIONAL | recorded | `spikes/render-bench` harness; latest entry `spikes/render-bench/results/20261030-script-core` |
| MEM-1 | MUST | accepted as is, no test | No measurement: the C# `GC.GetAllocatedBytesForCurrentThread` scroll test went with the core (#501) and nothing in `packages/*/test` measures allocation at 10³ vs 10⁶ rows |
| MEM-2 | MUST | pass | `browser/memory.spec.mjs:82 'mounting and disposing the grid fifty times returns nodes and listeners to baseline (MEM-2)'` @behaviour (asserts ±2; also records) |
| MEM-3 | MUST | pass, accepted as is | `ExGrid.Tests/GridSourceFetchTests.cs:83 'ASY-2 / MEM-3: each fetch's token source is disposed once'`. Not asserted: the Blazor adapter's DotNetObjectReference, IJSObjectReferences and CTSs counted against disposals; by reading each is disposed once (`ExGrid.cs:1528`, `:1561` `using`; `:2341-2377`) |
| MEM-4 | MUST | pass | L2 `core/grid-dom.test.ts:118 'MEM-4: destroying the grid removes every listener …'`; L3 `browser/memory.spec.mjs:124` @blazor |
| MEM-5 | MUST | pass | `browser/memory.spec.mjs:187 'a ten-minute scripted scroll does not grow the JS heap (MEM-5, MEM-6)'` @blazor, run with `EXGRID_SOAK=1` on CI's long run (`ci.yml:396`, `:528`); asserts ≤ 20% |
| MEM-6 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/memory.spec.mjs:187` → `MEM-6` |
| MEM-7 | OBSERVATIONAL | not run | Nothing measures it (§22 Step 6: `not run` until the Script Core has a measurement) |
| DOM-1 | MUST | pass | `browser/virtualisation.spec.mjs:212` |
| DOM-2 | MUST | pass | `browser/virtualisation.spec.mjs:327` (+6: tints, boxes, shade layer, Heading Marks); `browser/heading-mark.spec.mjs:164` (state markers = Row Headings) |
| DOM-3 | MUST | pass | `browser/virtualisation.spec.mjs:579 'the cells in the DOM are the painted rows × (pinned + scrollable columns on screen), exactly'` |
| DOM-4 | MUST | pass | `browser/features.spec.mjs:1122 'two instances stay independent: no --ex-* on :root, no window global'` @behaviour; `browser/mud-app.spec.mjs:247` @blazor |
| DOM-5 | OBSERVATIONAL | recorded by CI, not copied (Step 4) | `browser/observational.spec.mjs:149` → `DOM-5` |
| RR-1 | MUST | pass | L2 `ExGrid.MudBlazor.Tests/MudExGridPaperTests.cs:128`; L3 `browser/mud.spec.mjs:234 'a theme switch re-renders nothing inside the grid'`, `browser/mud-app.spec.mjs:449` |
| RR-2 | MUST | pass | core/grid-dom.test.ts:251 (test added by #539) |
| RR-3 | MUST | pass, accepted as is | No test. By reading, a metrics change resolves the columns anew and every painted row is painted again in that pass (same path as RR-2) |
| RR-4 | MUST | pass | `core/grid-dom.test.ts:88 'a row that stays on screen is not painted again when the Viewport moves one row'` |
| RR-5 | MUST | pass | `core/grid-dom.test.ts:178 'ADR-0003 RR-5 …'` |
| RR-6 | MUST | pass | `core/grid-bands.test.ts:26 '… a move within that row paints nothing'` |
| RR-7 | MUST | pass, accepted as is | No test presses a key that moves nothing and counts writes; `core/grid-dom.test.ts:274` shows a pass with nothing changed writes nothing but the Settled Mark |
| RR-8 | MUST | pass, accepted as is | No test hands an unchanged Scrollbar Gutter report; the report only invalidates a pass (`grid-view.ts:2383-2386`), which writes nothing unchanged (`core/grid-dom.test.ts:274`) |
| RR-9 | MUST | pass | Inspection: `ExGrid.Popover.cs:141-143` (`_drawPopover ??=`, `_popoverContentsStanding ??=`) |
| RR-10 | MUST | pass | Inspection: `ExGrid.cs:660`, `ExSheet.cs:378`, `ExPivot.cs:304`, `ExChart.cs:333`, `ExBoard.cs:284` (`ShouldRender() => _renderOwed`) |
| RR-11 | MUST | pass | `core/grid-bands.test.ts:26 'ADR-0029 UX-13 / RR-11 …'` |
| RR-12 | MUST | pass, accepted as is | `core/grid-actions.test.ts:118`, `:380`, `:413` (behaviour). Not asserted: per-row paint counts across entering, choosing, leaving, and a focus request; by reading only the row whose `chosen` moved is painted again (`grid-view.ts` `PaintedRow.chosen`) |
| RR-13 | MUST | pass | `core/grid-dom.test.ts:100 'ADR-0038 RR-13 …'` |
| RR-14 | MUST | pass | `core/grid-dom.test.ts:274 'a pass with nothing changed writes nothing'` |
| ASY-1 | MUST | pass | `cases/source/fetch.json:99 'a superseded question is called off, and its answer never reaches the screen'`, `:116`; `core/fetch-boundary.test.ts:81` |
| ASY-2 | MUST | pass | `cases/source/fetch.json:353`; `ExGrid.Tests/GridSourceFetchTests.cs:83`; `ExGrid.Components/ScriptGridHandleTests.cs:302`; `core/grid-work-in-order.test.ts:330` |
| ASY-3 | MUST | pass, accepted as is | No burst test; each scroll event reads the offsets then and paints from them (`grid-view.ts:3333-3353`) |
| ASY-4 | MUST | pass, accepted as is | `core/grid-dom.test.ts:410 '… its range is asked for once'`; `cases/source/fetch.json:68`. Not asserted: a synchronous source cannot re-enter (no test answers inside `needRange`) |
| ASY-5 | MUST | pass, accepted as is | No test fires the settle timer after destroy; `destroy()` cancels it (`grid-view.ts:2568`) |
| ASY-6 | MUST | pass | Judged from Step 1/4 logs: `tests/browser/playwright.config.mjs:143` (300 s), MTP `--timeout 5m` (§22 Step 1); all suites green |
| ASY-7 | MUST | pass | L2 `core/grid-dom.test.ts:312`, `:330`, `core/grid-selection.test.ts:509` (+cell-message, filter-panel, chrome-slots); L3 `browser/settled-mark.spec.mjs:30`–`:153` (@behaviour and @blazor), `browser/input-text-order.spec.mjs:5`–`:144` |
| ASY-8 | MUST | pass | `browser/differential.spec.mjs:28 'ADR-0240 §8: ${product} ends the same typed fast and slow, seed …'` @blazor, ExGrid/ExSheet/ExPivot, seeds `:42` |
| ST-1 | MUST | pass | `core/grid-consistency.test.ts:407` (seeds), `:411` (10⁶, passed locally with `EXGRID_ST1_MILLION=1`); seed to results.md (Step 5) |
| ST-2 | MUST | pass, accepted as is | `core/grid-edit.test.ts:780`, `:785`; `browser/sheet.spec.mjs:698`, `:743`. Not asserted: an empty Window pushed at the end of a sequence paints nothing and throws nothing |
| ST-3 | MUST | pass | `browser/presentation.spec.mjs:89 'UX-3 / ST-3 / ADR-0027 P6 …'` |
| ST-4 | MUST | pass | `cases/view-state/write.json:79`, `:2236`, `read.json`; `ExGrid.Tests/QuerySerialisationTests.cs:92`, `GridViewStateJsonTests.cs` |
| ST-5 | MUST | pass, accepted as is | `browser/circuit.spec.mjs:502 'a store change reaches both, a sort reaches one, and a shared source is refused'` (Server). Not asserted: same-dispatcher sharing and attach after disposal; no layer-2 test of `CircuitBinding` (`dotnet/src/ExGrid/CircuitBinding.cs:27-50` does both) |
| WR-1 | MUST | pass | L2 `ExGrid.MudBlazor.Tests/MudScriptFilterPanelTests.cs:37`; L3 `browser/filter-panel.spec.mjs:67`, `:91`, `:105` (each under both Chromes) |
| WR-2 | MUST | pass | ExGrid.MudBlazor.Tests/MudScriptFilterPanelTests.cs:54, :80 (row restated to ADR-0410 by #540) |
| WR-3 | MUST | accepted as is, no test | No ExGrid.MudBlazor test registers a non-English localizer, or checks the Blank wording is the Chrome's (`MudScriptFilterPanelTests.cs:40` checks the core's "(Blanks)"); only ExSheet's (`ExSheet.MudBlazor.Tests/MudSheetPackageTests.cs:159`, `:185`) |
| WR-4 | MUST | pass, accepted as is | Items/order/roles: `browser/popovers.spec.mjs:152` (mud, @blazor), keys `core/grid-context-menu.test.ts:157`. Not asserted: each item's Enabled state and Material icon, none for a Consumer's command; `MudExGridScriptMenu.razor:19-20` draws `Disabled`/`StartIcon` |
| WR-5 | MUST | pass | `browser/popovers.spec.mjs:144` (`CHROMES = ['builtin','mud']`), `browser/find.spec.mjs:44`–`:135`, `browser/filter-panel.spec.mjs` |
| WR-6 | MUST | pass | L2 `MudExGridPaperTests.cs:97`, `:108`; L3 `browser/mud-app.spec.mjs:449` |
| WR-7 | MUST | pass | `browser/mud-app.spec.mjs:137`, `:191`, `:247`, `:294`, `:355`, `:620` |
| WR-7a | MUST | pass | `browser/stretch.spec.mjs:56` |
| WR-8 | MUST | pass | `ExGrid.MudBlazor.Tests/WrapperScriptTests.cs:60`, `:72`, `:87` |
| WR-9 | MUST | pass | `browser/fixtures.mjs:515-530`; `browser/mud-app.spec.mjs:403`; `UnobservedTaskWatch.cs` in each Wrapper suite |
| WR-10 | MUST | pass, accepted as is | CI `ci.yml:822` `mudblazor-floor` (layer 3, `--grep "$WRAPPED"`, `:839`), `:881` `mudblazor-range`. Not asserted: "WR-10 tests pin the role and name on every MudSelect a Wrapper draws" — none exist, and no Wrapper draws a MudSelect now |
| SRV-1 | MUST | pass | Inspection: `ExGrid.DemoHost.csproj:19`, `ExGrid.DemoHost.Server.csproj:11` reference only DemoPages; `Program.cs:63`, `App.razor:23,27` `InteractiveServer` (prerender default) |
| SRV-2 | MUST | pass | CI's Server-host layer-3 jobs; skips on Server only `measure-live.spec.mjs:26`, `measure-pivot.spec.mjs:26`, `shipped-files.spec.mjs:187` (named exceptions) |
| SRV-3 | MUST | pass | `browser/circuit.spec.mjs:502` |
| SRV-4 | MUST | pass | `browser/fixtures.mjs:524`, `:381` (Server host log) |
| SRV-5 | MUST | pass, accepted as is | `browser/circuit.spec.mjs:38`, `:275`, `:438` (+6), `page-follow.spec.mjs:195`, `:221`, `format-cells.spec.mjs:191` (300 ms). Not asserted: CP-21 and CP-23 under 150 ms (`circuit.spec.mjs:151`, `:254` run at the proxy's 0 ms) |
| SRV-6 | OBSERVATIONAL | recorded | `browser/measure.spec.mjs:33 'what the pointer reports cost on a circuit, at 0, 50 and 150 ms'` (`EXGRID_MEASURE=pointer`) |
| SRV-7 | MUST | pass | `browser/circuit.spec.mjs:791`, `:804` (both Chromes, @blazor), `script-grid-chrome.spec.mjs:126`, `format-cells.spec.mjs:242` @behaviour; `MudScriptFilterPanelTests.cs:98`, `MudScriptFindPanelTests.cs:63` |
| MK-1 | MUST | pass | `cases/rows/row-mark-rules.json:7`, `cases/source/marks.json:144`, `:159`; `core/grid-marks.test.ts:168`; `browser/marks.spec.mjs:72` |
| MK-2 | MUST | pass | `cases/rows/row-mark-rules.json:36`; `core/grid-marks.test.ts:129`; `core/grid-row-kind.test.ts:100` (Group/Total) |
| MK-3 | MUST | pass | `core/grid-marks.test.ts:108`, `:129`, `:168` (Ctrl+A+Space over 10⁵ rows: one intent); `cases/source/marks.json:174`, `core/fetch-marks.test.ts:204`, `ExGrid.Tests/RowMarkFetchTests.cs:174` |
| MK-4 | MUST | pass | `core/grid-marks.test.ts:108 '… only that row paints again'` |
| MK-5 | MUST | pass | `core/grid-marks.test.ts:211`; `cases/source/marks.json:65` |
| MK-6 | MUST | pass | `browser/marks.spec.mjs:41` @behaviour (chrome+edge); `core/fetch-marks.test.ts:152`; `RowMarkFetchTests.cs:114` |
| MK-7 | MUST | pass | `browser/marks.spec.mjs:56`; `core/fetch-marks.test.ts:177`; `cases/source/marks.json:95` |
| MK-8 | MUST | pass | `core/fetch-marks.test.ts:163`; `cases/source/marks.json:112`; `RowMarkFetchTests.cs:128` |
| MK-9 | MUST | pass, accepted as is | `core/grid-marks.test.ts:228`, `:257`, `:282` (+2). Not asserted: the pager clauses (a header press on a page turned since; a pager added, removed or resized between press and click), and "Mark all N rows" refused under a moved order; `grid-view.ts:4301-4319` does them |
| DC-1 | MUST | pass | core/grid-row-headings.test.ts:51 'none given, no band'; core/grid-fill-handle.test.ts:65 'not without the declaration'; core/grid-formula-bar.test.ts:138 (bar off: no `.ex-formula-bar`); core/grid-completion.test.ts:122 'DC-1: nothing declared, nothing asked' (+2 ExGrid.Tests CellTypeTests.cs:19, CellAlignTests.cs:16) |
| DC-2 | MUST | pass | core/grid-cell-paint.test.ts:246 'where a header click selects, it selects the whole column and sorts nothing'; cases/selection/headings.json 'declared header click selects the whole column', 'Shift+click … the Focus stays'; browser/sheet.spec.mjs:77 'SH-18/DC-2/DC-3' |
| DC-3 | MUST | pass | core/grid-row-headings.test.ts:51, :125 'press selects the row … Shift extends', :229 'corner selects all, Focus top-left on screen'; browser/sheet.spec.mjs:77, :134; browser/headings.spec.mjs:92 'Row Heading click … scrollLeft where it was' |
| DC-4 | MUST | pass, accepted as is | cases/selection/headings.json '(DC-4): a row selected by its heading cycles only through real columns'; not asserted: Ctrl+A then copy yields no heading label |
| DC-5 | MUST | pass | core/grid-row-headings.test.ts:253 'the band goes, the rows start at the Viewport's top'; :51 (no Row Headings unless given) |
| DC-6 | MUST | pass | core/grid-row-headings.test.ts:51 'at the width declared', :74 'undeclared, the width is the wider…'; core/stylesheet-lengths.test.ts:192 'every length in ex-grid.css is a token fallback…'; inspected `dotnet/src/ExGrid/Assets/ex-grid.css:591` (no width) |
| DC-7 | MUST | pass | cases/selection/headings.json 'Ctrl+arrow follows the Consumer's edge answer…'; ExGrid.Tests/EdgeAnswerTests.cs:25, :34, :63; browser/sheet.spec.mjs:52 'SH-18/DC-7' |
| DC-8 | MUST | pass, accepted as is | cases/clipboard/spill.json 'DC-8: a block onto one cell … plans the block', 'one plan … one notification'; core/grid-paste.test.ts:226 '…then it becomes the Selection'; browser/declarations.spec.mjs:1965, :2170; not asserted: nothing scrolls to show the block |
| DC-9 | MUST | pass | cases/clipboard/spill.json 'DC-9: without the declaration, range to one cell keeps its refusal'; core/grid-paste.test.ts:226 (`singleCellTarget`) |
| DC-10 | MUST | pass | cases/clipboard/spill.json 'DC-10: past the last row / last column refused by name', 'Editable is judged on the spilled block'; core/grid-paste.test.ts:226 (`spillPastExtent`) |
| DC-11 | MUST | pass | core/grid-placement.test.ts:67 'placed … heard once, Focus scrolled into view', :81 'request under another Row Sequence Version places nothing'; browser/sheet.spec.mjs:174 |
| DC-12 | MUST | pass | cases/selection/fill-rules.json 'bottom-right, top-right of whole columns, bottom-left of whole rows, nowhere on every cell'; core/grid-fill-handle.test.ts:65, :81; browser/selection-look.spec.mjs:476, :497, :518 (both Chromes) (+3 declarations.spec.mjs:1864, :1902, :1942) |
| DC-13 | MUST | pass | core/grid-fill-handle.test.ts:91 'one axis, outline, one intent'; browser/declarations.spec.mjs:1706 'DC-13/DC-27 (both Chromes)', :1732, :1775 |
| DC-14 | MUST | pass | core/grid-fill-handle.test.ts:224 'target over a column that is not Editable is refused whole before anything is raised' |
| DC-15 | MUST | pass | inspection of `packages/core/src/dom/`: no layout read in the header click, Headings, fill drag or edge answer; panel listeners on elements inside the root (see note) |
| DC-16 | MUST | pass, accepted as is | core/grid-cell-paint.test.ts:220 'an edit opens on the Consumer's text'; core/grid-edit.test.ts:221 'F2 opens Caret on the cell's text'; browser/sheet.spec.mjs:41; not asserted: Overwrite over a declared opening text (the test titled 'typing still replaces it' types nothing) |
| DC-17 | MUST | pass | core/grid-completion.test.ts:129, :143 '↑ and ↓ choose, Tab accepts', :169; browser/declarations.spec.mjs:51, :94, :170 (both Chromes) |
| DC-18 | MUST | pass | core/grid-completion.test.ts:202 'an answer for text that has since changed is dropped' |
| DC-19 | MUST | pass | core/grid-point.test.ts:104, :153, :199; browser/declarations.spec.mjs:568, :606, :624 (+2 :668 both Chromes, f4-enter-mode.spec.mjs:139) |
| DC-20 | MUST | pass | browser/declarations.spec.mjs:965 'DC-20: typed quickly on a 150 ms circuit, no arrow points where the text forbids it @blazor' |
| DC-21 | MUST | pass, accepted as is | core/grid-formula-bar.test.ts:70 'bar reads the Focus cell's text and the Name Box names the Focus, band off the Viewport'; not asserted: on a display grid the bar shows the full value behind a `####` |
| DC-22 | MUST | pass | core/grid-formula-bar.test.ts:87 'Enter commits it once', :109 'shown in the bar … Escape cancels the one edit'; browser/sheet.spec.mjs:396 'SH-18/DC-22' |
| DC-23 | MUST | pass | cases/columns/grid-metrics.json (`formulaBarHeightPx`); core/grid-formula-bar.test.ts:70 (scroller height = 480 − band) |
| DC-24 | MUST | pass | inspection of `grid-view.ts`, `cell-editing.ts`, `formula-bar.ts`: caret read in place (`grid-view.ts:6388`), bar caret on keyup/mouseup (`:2287`); no layout read on this path (see note) |
| DC-26 | MUST | pass | core/grid-cell-paint.test.ts:98 'a cell declared a Number in a Text column is right-aligned and becomes ####'; ExGrid.Tests/CellTypeTests.cs:59, :71 |
| DC-27 | MUST | pass | core/grid-fill-handle.test.ts:91, :125, :138; blazor/bridge.test.ts:735; browser/declarations.spec.mjs:1706 |
| DC-28 | MUST | pass | browser/declarations.spec.mjs:756 'keys typed into the Formula Bar before a 150 ms circuit has answered … keep their meaning @blazor', :997 |
| DC-29 | MUST | pass | core/grid-cell-paint.test.ts:108 'a cell's own alignment'; ExGrid.Tests/CellAlignTests.cs:24, :33 |
| DC-30 | MUST | pass | core/grid-edit.test.ts:967 'Ctrl+Z and Ctrl+Y forwarded … never while an edit is open; unheard they are the browser's'; browser/declarations.spec.mjs:2256, :2295 @blazor, :2335 |
| DC-31 | MUST | pass | browser/declarations.spec.mjs:107 '← and → move the caret and the list stays', :136 '=SS completes the span at the caret', :647 |
| DC-32 | MUST | pass | core/grid-copy.test.ts:561 'the Consumer's two flavours are written … a refusal says the Consumer's sentence', :588, :611 (asynchronous route) |
| DC-33 | MUST | pass | cases/clipboard/parse.json; browser/declarations.spec.mjs:2170 'Excel's column … same flavours', :2199 'SH-14/DC-33 @blazor' |
| DC-34 | MUST | pass | core/grid-point.test.ts:171 'a caret moved into the text is Caret'; browser/declarations.spec.mjs:668 (both Chromes), :719 @blazor |
| DC-35 | MUST | pass | core/grid-cell-paint.test.ts:108 '…painted text fitted to the content width, named by its value' (`aria-label`) |
| DC-36 | MUST | pass | core/grid-column-gestures.test.ts:64 'a grip stands … only while a resize is heard', :140 'double click fits'; browser/declarations.spec.mjs:2359 |
| DC-37 | MUST | pass | core/grid-context-menu.test.ts:123 'may leave Copy with headers off', :312 'hides Copy with headers leaves Ctrl+Shift+C to the page' |
| DC-38 | MUST | pass | core/grid-paste.test.ts:125 'a paste the Consumer refuses leaves the Selection … nor does a spill'; browser/declarations.spec.mjs:2150 |
| DC-39 | MUST | pass | browser/sheet-vs-excel.spec.mjs:251 'while a drag is held the Name Box shows 4R x 3C, D5 after release', :266 'DC-39: a range extended by key shows no size' |
| DC-40 | MUST | pass, accepted as is | core/grid-edit.test.ts:995 'Ctrl+D and Ctrl+R … naming the source; one deep fills from beyond it'; not asserted: a clipboard paste and Ctrl+Enter carry no `fillSource` |
| DC-41 | MUST | pass, accepted as is | core/grid-edit.test.ts:392 'Ctrl+Enter … one paste intent naming the cell typed in', :995 (fill key `enteredAt` undefined); not asserted: a clipboard paste names none, and the pair differ only in `EnteredAt` |
| DC-42 | MUST | pass | core/grid-row-headings.test.ts:125, :190; core/grid-selection.test.ts:334, :357; browser/headings.spec.mjs:148, :176, :196 (+3 :369, :387, :405) |
| DC-43 | MUST | pass | core/grid-row-headings.test.ts:209 'scrolls down only'; core/grid-selection.test.ts:436 'scrolls sideways only'; browser/headings.spec.mjs:217, :240 |
| DC-44 | MUST | pass, accepted as is | browser/headings.spec.mjs:261, :298, :320 'redrawn only when the Extent moves to another Heading'; ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:63, :75; not asserted: without a size label there is no Size Tip |
| DC-45 | MUST | pass | core/grid-point.test.ts:226; browser/declarations.spec.mjs:1061, :1085 (both Chromes), :1106, :1243 '150 ms … four F4 in a burst @blazor' (+4 :1120, :1162, :1188, :1270) |
| DC-46 | MUST | pass | cases/cells/reference-colouring.json; core/grid-reference-outlines.test.ts:87, :102; browser/declarations.spec.mjs:1454, :1481, :1558 |
| DC-47 | MUST | pass | core/grid-reference-outlines.test.ts:184, :201; browser/reference-text.spec.mjs:136 '150 ms burst', :194, :260 'IME composition' (+3 :163, :228, windows-ime.spec.mjs:319) |
| DC-48 | MUST | pass | browser/reference-text.spec.mjs:540 'longer than the field keeps its colours at either end (both surfaces, both Chromes)', :584 'comparison finds a layer half a pixel out' |
| DC-49 | MUST | pass | core/grid-reference-outlines.test.ts:112 'each key's colour told once per change … empty list as the edit ends', :138, :159; sheet/create-sheet.test.ts:227 |
| DC-50 | MUST | pass | core/grid-reference-outlines.test.ts:125 'columns asked for are outlined over their bodies'; browser/declarations.spec.mjs:1610, :1641 |
| DC-51 | MUST | pass | inspection of `core/src/dom/reference-text.ts`, `reference-outlines.ts`: `createRange` + `CSS.highlights` named per grid, `scrollLeft` write; no layout read, no MutationObserver needed now the core renders |
| DC-52 | MUST | pass | core/grid-pointed-at.test.ts:79, :94, :107 'press on a column header is the column … neither sorts' (+1 :167); browser/pointing-scope.spec.mjs (both tags); ExGrid.Components/ShippedStylesheetTests.cs:251 |
| DC-53 | MUST | pass, accepted as is | core/grid-pointed-at.test.ts:135 'dashes over the named row's cell while painted, or down a column'; browser/pointing-scope.spec.mjs:442 'dashes follow the row through a sort', :876; ShippedStylesheetTests.cs:275; not asserted: no scroll for an unpainted row, and an empty request draws nothing |
| DC-54 | MUST | pass | browser/pointing-scope.spec.mjs:249, :386, :420 '=-( … at 0 and 150 ms', :735 'press and Shift+↓ @blazor'; sheet/pointing-scope.test.ts:385; inspection of `pointed-at.ts`, `point-mode.ts` (no document/window listener, no layout read) |
| DC-55 | MUST | pass | core/grid-pointed-at.test.ts:151 'steps from a cell and from a column'; sheet/pointing-scope.test.ts:590; browser/pointing-scope.spec.mjs:939 'Id's header, → … scrollTop unchanged', :691 |
| DC-56 | MUST | pass, accepted as is | browser/reference-text.spec.mjs:421 'Excel's shade — #0401a2, #630101 — on #c6c6c6 (both Chromes)'; not asserted: places 3 to 7 and the dark-ground mix (the code has them, `ex-grid.css:1332-1338`) |
| DC-57 | MUST | pass | cases/keys/grid-keys.json; core/grid-declared-keys.test.ts:26, :39, :72 'a key the core answers … refused by name' |
| DC-58 | MUST | pass | cases/columns/overflow.json; ExGrid.Tests/BoldWidthTests.cs; core/grid-cell-paint.test.ts:130, :206 (row not repainted); browser/appearance.spec.mjs:279 @blazor, :331 (+3 sheet-paper.spec.mjs:57, :524, :563); the Split Fill (ADR-0490, added after rc.3): core/grid-cell-paint.test.ts:149, :171, :177, :183; browser/appearance.spec.mjs:242, :270 @blazor |
| DC-59 | MUST | pass | core/grid-cell-paint.test.ts:189 'an edge is drawn once'; browser/sheet-borders.spec.mjs:95, :241, :260 (100% and chrome-150); browser/part-c.spec.mjs:253, :332, :435 (ADR-0206, pinned) (+5) |
| DC-60 | MUST | pass | core/grid-consumer-popover.test.ts:41, :62; browser/format-cells.spec.mjs:61, :460 'scrolls … closes as a Cancel below one row', :493 'each Sheet's Format Cells is its own' (+3 :257, :278, :305 @blazor) |
| DC-61 | MUST | pass | core/grid-keyboard-handoff.test.ts:42, :56, :132 (+6); blazor/bridge.test.ts:636, :648; browser/pivot.spec.mjs:1121, :1149, script-grid.spec.mjs:44 @blazor |
| DC-62 | MUST | pass | core/grid-leave.test.ts:52, :63, :74 (+5); browser/features.spec.mjs:1279 |
| DC-63 | MUST | pass | core/grid-edit.test.ts:98 (`onCellDoubleClick` heard); browser/features.spec.mjs:1217, :1259 |
| DC-64 | MUST | pass | core/grid-change-highlight.test.ts:32 'unmarked in one step; only the rows whose marks ended paint', :54 'nothing asked without the declaration; identity is the signal', :79; core/change-highlight.test.ts (7) |
| DC-65 | MUST | pass | browser/grid-live.spec.mjs:125 'DC-65: a mark stays on its cell across a scroll @blazor' |
| DC-66 | MUST | pass | browser/grid-live.spec.mjs:165 'marks paint without animating, forced colours restate them, live region says nothing @blazor'; ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs |
| DC-67 | MUST | pass | core/grid-reference-drag.test.ts:108, :127, :152 (+13); blazor/bridge.test.ts:1102; browser/declarations.spec.mjs:1502 |
| DC-68 | MUST | pass | cases/selection/placed-ranges.json:7 (the order handed), :16 (not sorted), :24 (overlapping, neither merged nor refused), :31 (the Focus's range the one handed last), :38 (each in its own turn), :47, :56 (refused, nothing changes), :67; core/grid-placement.test.ts:226 (heard once, scrolled into view, read back as placed), :245, :271 (an older Row Sequence Version refused), :283 (throws, changes nothing), :320 (a Reject holds it) (+2 :264, :296); blazor/bridge.test.ts:1362; ExGrid.Components/ScriptGridHandleTests.cs:141 (what Blazor sends) |
| DC-25 | MUST | pass | browser/declarations.spec.mjs:2384 'positions grid, declaring nothing, keeps ExGrid's own behaviour', :2335; browser/sheets.spec.mjs:29, :157 |
| SH-1 | MUST | pass | ExSheet.Engine.Tests/PackageBoundaryTests.cs:8 'engine references only the BCL'; HeadlessTests.cs:43; inspection of csproj: ExSheet → ExSheet.Engine, ExGrid; only ExSheet.MudBlazor references ExSheet |
| SH-2 | MUST | pass | browser/sheet.spec.mjs:784 'SH-2: the Focus reaches XFD1048576 and the DOM does not grow' |
| SH-3 | MUST | pass | cases/viewport/compression.json 'VZ-8: refusal at the scale-one Layout Ceiling', 'exactly at the ceiling accepted', 'past the ceiling refused'; message names rows, height and ceiling (`viewport-geometry.ts:85`) |
| SH-4 | MUST | pass | sheet/create-sheet.test.ts:89 'a row the change did not name is not painted again' |
| SH-5 | MUST | pass | cases/sheet/structure.json (SH-5 cases), cases/sheet/undo.json; browser/sheet-vs-excel.spec.mjs:711 'inserting rows keeps the Selection'; browser/sheet.spec.mjs:439, :475, :546 |
| SH-6 | MUST | pass | cases/sheet/reference-grammar-culture.json; ExSheet.Engine.Tests/ReferenceGrammarCultureTests.cs:92 |
| SH-7 | MUST | pass | ExcelCases/*.json (164 files, every function); ExSheet.Engine.Tests/RefusalTests.cs:60 'corpus #NAME? refused where Excel answers otherwise'; cases/sheet/functions.json; sheet-engine/catalogue.test.ts:56 |
| SH-8 | MUST | pass | cases/sheet/recalculation.json 'only the dependents recompute, counted', 'Values published only from a completed recalculation' |
| SH-9 | MUST | pass | ExcelCases/errors.json 'cycle is #CIRC! in each member and each dependent', 'Breaking the cycle recovers…'; ExcelCaseTests.cs:30 |
| SH-10 | MUST | pass | ExcelCases/dates.json, general-width.json; cases/sheet/number-format.json '15 significant digits', 'what General cannot fit is ####' |
| SH-11 | MUST | pass | cases/sheet/culture.json 'SH-11: a document saved under en-US … de-DE shows the same numbers' |
| SH-12 | MUST | pass | cases/sheet/sheet-document.json 'SH-12, SH-17: a document holds no Values', 'unknown version is refused', round-trips |
| SH-13 | MUST | pass | cases/sheet/undo.json; sheet/create-sheet.test.ts:139 'a document opened empties it', :1780 'a command is one step'; browser/sheets.spec.mjs:51 'each Sheet keeps its own undo stack' |
| SH-14 | MUST | pass | cases/sheet/clipboard.json; browser/declarations.spec.mjs:2199 'SH-14/DC-33 … @blazor' |
| SH-15 | MUST | pass | cases/sheet/fill.json (series, whole axis, refusals, formats); ExcelCases/fill.json |
| SH-16 | MUST | pass | cases/sheet/linked-table.json (SH-16 cases); ExcelCases/linked-tables.json; browser/sheet.spec.mjs:574 |
| SH-17 | MUST | pass, accepted as is | cases/sheet/headless.json 'engine alone … are the Values Excel gives'; ExSheet.Engine.Tests/HeadlessTests.cs:23; cases/sheet/random.json; not asserted: engine-alone Values equal to a rendered Sheet's |
| SH-18 | MUST | pass | browser/sheet.spec.mjs (SH-18 tests, :52, :77, :396 and the insert/delete tests) under the fixture's console check |
| SH-20 | MUST | pass | ExcelCases/general-width.json; cases/sheet/general-width.json; sheet/create-sheet.test.ts:366 |
| SH-21 | MUST | pass | cases/sheet/format-levels.json; cases/sheet/sheet-document.json 'SH-21: formats on whole columns … one entry' |
| SH-22 | MUST | pass | cases/sheet/column-width.json; ExcelCases/column-widths.json; ExSheet.Engine.Tests/ColumnWidthTests.cs |
| SH-23 | MUST | pass | cases/sheet/fill.json 'SH-23: Ctrl+D copies …', 'a fill key copies'; cases/sheet/cell-format-carry.json; core/grid-edit.test.ts:1032 'source past the Window' |
| SH-24 | MUST | pass, accepted as is | sheet/create-sheet.test.ts:128 'Delete clears … as one step, formats stay'; cases/sheet/cell-format-carry.json 'SH-38, SH-24'; not asserted: no step over empty cells, Selection and Focus unchanged |
| SH-25 | MUST | pass | sheet/sheet-find.test.ts:26 'finds the text a cell is shown with', :35 'from the cell after the Focus, wrapping, scope' (match case, whole cell at :31-32), :137 |
| SH-26 | MUST | pass | cases/sheet/column-width.json (CW cases); browser/sheet-vs-excel.spec.mjs:740 |
| SH-27 | MUST | pass | cases/sheet/fill.json 'SH-27' cases; browser/sheet-vs-excel.spec.mjs:361 |
| SH-28 | MUST | pass | cases/sheet/reference-cycle.json (107); browser/declarations.spec.mjs:1188, :1323, :1339 |
| SH-29 | MUST | accepted as is, no test | sheet/create-sheet.test.ts:1866 'every command is refused by name … onEditingChanged', :469; browser/sheet.spec.mjs:594, :615 @blazor, :644; gap: replacing the Sheet Document while an edit is open |
| SH-30 | MUST | pass | cases/sheet/formula-references.json (43); ExSheet.Engine.Tests/FormulaReferencesTests.cs; sheet-engine/surface.test.ts |
| SH-31 | MUST | pass | sheet/create-sheet.test.ts:227; browser/declarations.spec.mjs:1610, :1641; browser/sheets.spec.mjs:157 |
| SH-32 | MUST | pass | cases/sheet/pointed-text.json; sheet/pointing-scope.test.ts:177, :202, :250 (+17); browser/pointing-scope.spec.mjs (14), sheets.spec.mjs:157 |
| SH-33 | MUST | pass | cases/sheet/linked-table.json 'SH-33: a repeated key refuses the snapshot by name…'; ExSheet.Engine.Tests/LinkedTableTests.cs:21 |
| SH-34 | MUST | pass | cases/sheet/pointed-text.json; browser/pointing-scope.spec.mjs:442 'dashes follow the row through a sort' |
| SH-35 | MUST | pass | sheet/pointing-scope.test.ts (15); browser/pointing-scope.spec.mjs:691, :735 @blazor, :939 (+25) |
| SH-36 | MUST | pass | cases/sheet/completion-trigger.json, completion-trigger-sheet.json; ExSheet.Engine.Tests/CompletionTriggerTests.cs; browser/declarations.spec.mjs:220, :246 (+18) |
| SH-37 | MUST | pass, accepted as is | ExSheet.Engine.Tests/PointingScopeWatchTests.cs:104 'ADR0058_a_range_of_columns_is_refused_by_the_grammar' (message names ADR-0058's next step); not asserted: the same watch over `packages/sheet-engine` |
| SH-38 | MUST | pass | cases/sheet/cell-format.json, cell-format-carry.json, cell-format-document.json; ExSheet.Engine.Tests/CellFormatTests.cs |
| SH-39 | MUST | pass | browser/sheet-paper.spec.mjs:57, :121, :158 (both schemes, both Chromes); ExSheet.MudBlazor.Tests/PaperStylesheetTests.cs |
| SH-40 | MUST | pass | cases/sheet/number-format-colour.json (39); browser/sheet-paper.spec.mjs:57 |
| SH-41 | MUST | pass | core/column-geometry.test.ts:39; ExGrid.Tests/ColumnGeometryTests.cs:71; browser/sheet-paper.spec.mjs:697, :736, :750 (both Chromes) |
| SH-42 | MUST | pass | sheet/sheet-keys.test.ts; cases/sheet/culture.json (SH-42); browser/format-keys.spec.mjs:44, :73, :115; CI windows-desktop: browser/windows-desktop.spec.mjs:72 'Ctrl+1 to Ctrl+5 through the OS' |
| SH-43 | MUST | pass | sheet/create-sheet.test.ts:959, :2325 'formatting key while an edit is open changes nothing, says why, heard as a refusal', :1866; browser/format-keys.spec.mjs:141 'Ctrl+U' |
| SH-44 | MUST | pass | cases/sheet/cell-format.json (20), cell-format-carry.json; ExSheet.Engine.Tests/CellFormatTests.cs |
| SH-45 | MUST | pass | sheet/format-cells-draft.test.ts (32), format-cells-panel.test.ts; core/grid-pending-commit.test.ts:612, :639, :665 (ADR-0210); browser/format-cells.spec.mjs:61, :228, :242, format-cells-keys.spec.mjs:92 (80 ms), format-cells-mud.spec.mjs |
| SH-46 | MUST | pass | browser/sheet-borders.spec.mjs:120, :158, :199, :228 'dashes at the run's scale' (+26; 100% and chrome-150) |
| SH-47 | MUST | pass | ExSheet.MudBlazor.Tests/MudSheetPackageTests.cs:62, :74, :104, :119; `dotnet/tests/ExGrid.PackageSmoke/check.sh` (CI `package` job) |
| SH-48 | MUST | pass, accepted as is | sheet/create-sheet.test.ts:1084 'shown only when asked, above the grid, band off the height'; browser/sheet-toolbar.spec.mjs:109 (both Chromes), :302; not asserted: the Viewport loses exactly rows × the Toolbar Row height |
| SH-49 | MUST | accepted as is, no test | sheet/create-sheet.test.ts:1084 (default row order); ExSheet.Components/ScriptSheetTests.cs:870 'ToolbarContent crosses as the rows it declares, in markup order'; gap: the reference check (no internal member from an item type) was removed in #501 |
| SH-50 | MUST | pass | browser/sheet-toolbar.spec.mjs:132, :153, :309, :501 (both Chromes); browser/format-keys.spec.mjs:94 'pressed straight after Shift+ArrowDown @blazor'; sheet/create-sheet.test.ts:1102, :1537 |
| SH-51 | MUST | pass | sheet/create-sheet.test.ts:1459 'Ctrl+F1 … the browser's where it does not'; browser/sheet-toolbar.spec.mjs:585; CI windows-desktop: browser/windows-desktop.spec.mjs:104 |
| SH-52 | MUST | pass | sheet/create-sheet.test.ts:1251, :1448 'KeyTips taken for one another refused by name', :1513; browser/sheet-toolbar.spec.mjs:329, :616, :714 (80 ms) (+20); CI windows-desktop: browser/windows-desktop.spec.mjs:122, :141 |
| SH-53 | MUST | pass | sheet/sheet-keys.test.ts:78, :85; browser/part-c.spec.mjs:526, :551 |
| SH-54 | MUST | pass | browser/part-c.spec.mjs:85, :116, :134, :194 (both Chromes, pinned); browser/sheet-paper.spec.mjs (SH-54, chrome-150); PaperStylesheetTests.cs |
| SH-55 | MUST | pass | sheet/format-cells-draft.test.ts:442, :454, :467 |
| SH-56 | MUST | pass | cases/sheet/sheet-day.json (11); sheet/create-sheet.test.ts:1923 'browser's zone at once, fixed day first', :1934 |
| SH-57 | MUST | pass, accepted as is | sheet/create-sheet.test.ts:1934 'read again as midnight passes there' (fake timers); cases/sheet/sheet-day.json 'the day the Sheet already has changes nothing'; not asserted: readings at most an hour apart, and no clock read once destroyed |
| SH-58 | MUST | pass | browser/sheet-today.spec.mjs; sheet/src/sheet-moment.ts:20 (row restated by #540) |
| SH-59 | MUST | pass | cases/sheet/indirect.json, now-source.json, volatile.json |
| SH-60 | MUST | pass | sheet/create-sheet.test.ts:1947 'NOW moves on as the minute turns'; cases/sheet/volatile.json 'no volatile Formula is not recalculated by the clock'; browser/sheet-today.spec.mjs:81, :97, :120 |
| SH-61 | MUST | pass | cases/sheet/linked-table.json 'two rows hold the same Values in every key column', 'a blank part … has no key'; cases/sheet/sheet-document.json (version 9); sheet/pointing-scope.test.ts:187 |
| SH-62 | MUST | pass | cases/sheet/spill.json (#SPILL!, overlap order, not recorded, #NUM! past 2^24) |
| SH-63 | MUST | pass | cases/sheet/spill.json 'A1# reads the Spill Range…', 'is #REF! when A1 does not spill', '# follows one cell only, @ refused' |
| SH-64 | MUST | pass | ExcelCases/filter.json, unique.json, sort.json, sortby.json, sequence.json, transpose.json, sumproduct.json, index.json, xlookup.json, row.json, column.json |
| SH-65 | MUST | pass | sheet/create-sheet.test.ts:419 'spilled cell shows its Anchor's Formula in the bar, dimmed, read only'; cases/sheet/spill.json (typing, copy without/with Anchor); browser/sheet-spill.spec.mjs:12, :30 |
| SH-66 | MUST | pass | cases/sheet/random.json; ExcelCases/rand.json, randbetween.json; sheet/create-sheet.test.ts:1897, :1913 |
| SH-67 | MUST | pass | cases/sheet/let.json (107, refusals on entry); ExcelCases/let.json; ExSheet.Engine.Tests/LetTests.cs:15; sheet/create-sheet.test.ts:113 (refusal holds the editor) |
| SH-68 | MUST | pass | cases/sheet/subtotal.json, error-type.json, indirect.json; ExSheet.Engine.Tests/SubtotalTests.cs:25; sheet-engine/surface.test.ts:60 'no row can be hidden while SUBTOTAL … skip nothing' |
| SH-69 | MUST | pass | sheet-engine/move-reference.test.ts |
| SH-19 | OBSERVATIONAL | recorded | browser/measure-sheet.spec.mjs:108 'SH-19: completion's keystroke-to-list time … 150 ms'; ExSheet.Engine.Tests/Measurements.cs:39, :47, :55 (Explicit facts: full recalculation, one edit, a row inserted) |
| FD-1 | MUST | pass, accepted as is | cases keys/grid-keys.json:194; core/grid-find.test.ts:133; browser/find.spec.mjs:184, :199, :213 (+5). Not asserted: Ctrl+F inside a Template cell's own control stays the control's |
| FD-2 | MUST | pass | core/grid-find.test.ts:133; browser/find.spec.mjs:472 |
| FD-3 | MUST | pass, accepted as is | browser/find.spec.mjs:44, :251; core/grid-find-slot.test.ts:124. Not asserted: a pointer-down elsewhere closes Find and returns the keyboard |
| FD-4 | MUST | pass, accepted as is | core/grid-find.test.ts:200. Not asserted: visible columns in current order (hidden/moved column); no scope when a single cell is selected (held for Find All only, grid-find-all.test.ts:141) |
| FD-5 | MUST | pass, accepted as is | core/grid-find.test.ts:183; core/grid-find-slot.test.ts:135; browser/find.spec.mjs:135. Not asserted: with a scope the Selection stands |
| FD-6 | MUST | pass | core/grid-find.test.ts:200, :221, :237 (row restated to ADR-0451 section 3 by #540) |
| FD-7 | MUST | pass | cases finding/find.json (12 cases), source/finding.json:7, :29; ExGrid.Tests/Cases/FindCaseTests.cs:25 |
| FD-8 | MUST | pass | core/grid-find.test.ts:154, :183; browser/find.spec.mjs:441, :455 |
| FD-9 | MUST | pass | ExGrid.Tests/GridFindTests.cs:52 The_fetching_source_finds_through_its_delegate, :151 A_source_without_find_says_it_cannot |
| FD-10 | MUST | withdrawn | withdrawn by #540: nothing left to judge |
| FD-11 | MUST | pass, accepted as is | No test; code: core/src/dom/grid-view.ts:8955 (throws on an answer outside the request), :8976 (column left = orderChanged) |
| FD-12 | MUST | pass | core/grid-find-all.test.ts:114; react/find-slot-contract.ts:104 (mui, mantine); ExGrid.MudBlazor.Tests/MudScriptFindPanelTests.cs:41; ExGrid.Tests/GridFindTests.cs:83, :151 |
| FD-13 | MUST | pass | cases finding/find-all.json (14 cases); ExGrid.Tests/Cases/FindCaseTests.cs:46 |
| FD-14 | MUST | pass, accepted as is | core/grid-find-all.test.ts:130, :143, :150 (+2). Not asserted: answer outside the request / too many / too few / out of order throws (code grid-view.ts:9540-9559) |
| FD-15 | MUST | pass, accepted as is | core/grid-find-all.test.ts:150, :162, :186; browser/find.spec.mjs:524. Not asserted: Enter on a line (find-all-list.ts:134) |
| FD-16 | MUST | pass, accepted as is | core/grid-find-all.test.ts:244, :256, :273. Not asserted: list goes on an option change and after Replace All |
| FD-17 | MUST | pass | sheet/create-sheet.test.ts:893, :909; sheet/sheet-find.test.ts:76, :94, :149; pivot/pivot-view.test.ts:2541, :2627 |
| PV-1 | MUST | pass | ExGrid.PackageSmoke/check.sh:137, :151, :156, :179 (row restated by #540) |
| PV-2 | MUST | pass | pivot/pivot-view.test.ts:309, :320, :326 (fixed by #540: Row Kinds and the header press). The server report Window clause is the LV rows' known limitation |
| PV-3 | MUST | pass | cases pivot/items.json (55), source-snapshot.json:82, :240; pivot-engine/incremental.test.ts:323; browser/pivot-csv.spec.mjs:260, :293 |
| PV-4 | MUST | pass | cases pivot/aggregation.json (30), source-snapshot.json:282-559 |
| PV-5 | MUST | pass | cases pivot/hidden.json:9, :34, :127; pivot/pivot-view.test.ts:1822 |
| PV-6 | MUST | pass | cases pivot/forms.json (23), columns.json (10), sorting.json (16); ExPivot.Engine.Tests/Cases/PivotReportCaseTests.cs:27 |
| PV-7 | MUST | pass | cases pivot/general.json, general.texts.json, items.json:378-402 (+1); pivot/pivot-view.test.ts:349; browser/pivot-csv.spec.mjs:228, :339, :372 (+1) |
| PV-8 | MUST | pass | cases pivot/layout-edits.json (20); pivot/pivot-view.test.ts:1474, :1581; ExPivot.Engine.Tests PivotLayoutCaseTests.cs:24 |
| PV-9 | MUST | pass | pivot-react/pivot-slot-contract.ts:205 (mui, mantine); pivot-blazor/razor-pivot-slots.test.ts; browser/pivot.spec.mjs:138, :149, :162; browser/script-pivot.spec.mjs:78 |
| PV-10 | MUST | pass | browser/pivot.spec.mjs:138, :149, :162 (+5); pivot/pivot-view.test.ts:1058 (row restated by #540) |
| PV-11 | MUST | pass | browser/pivot.spec.mjs:668, :761, :782 (+5); pivot/pivot-view.test.ts:1474, :1531, :1568 (+1) |
| PV-12 | MUST | pass | pivot/pivot-view.test.ts:2132, :2160, :2201; browser/pivot.spec.mjs:947, :967 |
| PV-13 | MUST | pass, accepted as is | pivot/pivot-view.test.ts:384, :448, :464; cases pivot/hidden.json:412, report-source.json:373; browser/pivot.spec.mjs:303, :316; pivot-find.spec.mjs:160. Not asserted: Selection kept across a values-only refresh |
| PV-14 | MUST | pass | pivot/pivot-view.test.ts:2245, :2267, :2279 (+2); cases pivot/hidden.json:106; browser/pivot.spec.mjs:339, :398, :656 (+1) |
| PV-15 | MUST | accepted as is, no test | No count of report-grid updates per Field List interaction, nor of the grid Chrome asked once; Razor render-count tests removed |
| PV-16 | MUST | pass | cases pivot/layout-json.json (23), source-json.json (113); ExPivot.Engine.Tests PivotJsonCaseTests.cs:18 |
| PV-17 | MUST | pass, accepted as is | browser/pivot.spec.mjs:263. Not asserted: a menu open in one pivot is not open in the other |
| PV-18 | MUST | pass, accepted as is | ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:43 Every_visual_token..., :58 The_wrapper_sets_no_geometry_token, :62; browser/pivot.spec.mjs:1233, :1265. Not asserted: the expand/collapse glyph is plain markup |
| PV-19 | MUST | pass | browser/pivot-find.spec.mjs:160, pivot-csv.spec.mjs:321; ExPivot.Components/ScriptPivotTests.cs:93; pivot/pivot-view.test.ts:2437 |
| PV-20 | MUST | pass | browser/pivot-db.spec.mjs:109, pivot-risk.spec.mjs:70, grid-live.spec.mjs:292 (+3 spec files: pivot, pivot-csv, pivot-live) |
| PV-21 | OBSERVATIONAL | recorded | browser/measure-pivot.spec.mjs:265, :379, :416, :471, :598; recorded in Step 6 |
| PV-22 | MUST | pass | ExGrid.DemoApi.Tests/PivotSourceTests.cs:85, :98, :129 (+5); ExPivot.Engine.Tests/SnapshotSourceTests.cs |
| PV-23 | MUST | pass | cases pivot/source-questions.json:559, :678, report-source.json:1143; pivot/pivot-view.test.ts:2361 |
| PV-24 | MUST | pass | pivot/pivot-view.test.ts:1729; cases pivot/source-questions.json:939 |
| PV-25 | MUST | pass, accepted as is | pivot/pivot-view.test.ts:1063, :1083. Not asserted: a further change cancels the question in flight (pivot-view.ts:1090) |
| PV-26 | MUST | pass | cases pivot/hidden.json:142, :234; pivot/pivot-view.test.ts:1672, :2463 |
| PV-27 | MUST | pass | pivot-engine/slicing.test.ts:50, :72, :82; ExPivot.Engine.Tests SlicedWorkTests |
| PV-28 | MUST | pass | pivot/pivot-view.test.ts:1049, :1946, :1972 (+1) |
| PV-29 | MUST | pass | cases pivot/source-questions.json:831; pivot/pivot-view.test.ts:1258, :1279, :1288 |
| PV-30 | MUST | pass | pivot/pivot-view.test.ts:1147, :1167, :1423 (+1); browser/pivot.spec.mjs:931, :992, :1045 |
| PV-31 | MUST | pass | cases pivot/orderkeys.json (16); pivot-engine/order-key.test.ts:27, :41; browser/pivot-risk.spec.mjs:70, :86, :108 |
| PV-32 | MUST | pass | cases pivot/dates.json (17); browser/pivot.spec.mjs:1091 |
| PV-33 | MUST | pass | ExPivot.Components/ScriptPivotTests.cs:102; cases pivot/words.tables.json; ExPivot.Engine.Tests/JapaneseWordsTests.cs:16 (fixed by #540: en-GB words under an en-GB UI culture) |
| PV-34 | MUST | pass | pivot-engine/live.test.ts:158 (24 random runs); ExPivot.Engine.Tests LiveDataTests.cs:56 |
| PV-35 | MUST | pass, accepted as is | pivot/pivot-view.test.ts:818, :852, :1006; cases pivot/hidden.json:412. Not asserted: the 250 ms default (always passed explicitly); Selection and open menu kept over a values-only redraw; an Item appearing drops the Selection |
| PV-36 | MUST | pass | pivot/pivot-view.test.ts:969; cases pivot/report-source.json:842; browser/pivot-live.spec.mjs:156, :289 |
| PV-37 | MUST | pass | pivot/pivot-view.test.ts:895, :927, :938; browser/pivot-live.spec.mjs:199 |
| PV-38 | MUST | pass | pivot/pivot-view.test.ts:816, :1167; pivot-engine/server-live.test.ts:412 |
| PV-39 | MUST | pass | pivot/pivot-view.test.ts:2279, :2306; browser/pivot.spec.mjs:420, :457, :481 (+4) |
| PV-40 | MUST | pass | pivot-engine/slicing.test.ts, sliced-layout.test.ts:102, :226, incremental.test.ts:336 (+1); pivot/pivot-view.test.ts:1063; browser/measure-pivot.spec.mjs:416 |
| PV-41 | MUST | pass | browser/pivot.spec.mjs:616, :637, :1121 (+1); pivot/pivot-view.test.ts:2918 |
| PV-42 | MUST | pass, accepted as is | cases pivot/row-keys.json:9, :67; pivot/report-source.test.ts:50; core/grid-actions.test.ts:352, grid-cell-paint.test.ts:206. Not asserted: no pivot-level count of rows painted over a live redraw (holds by composition, report-grid.ts:392) |
| PV-43 | MUST | pass | pivot/pivot-view.test.ts:415; ExPivot.Engine.Tests/RowKeyTests.cs:40 (fixed by #539) |
| PV-45 | MUST | pass, accepted as is | pivot/report-source.test.ts:50; core/grid-cell-paint.test.ts:206. Not asserted: per-redraw paint count over the pivot |
| PV-47 | MUST | withdrawn | withdrawn by #540: nothing left to judge |
| PV-48 | OBSERVATIONAL | not run | its harness measured the report Window path ADR-0370 section 3 ended (the C# report Windows and the Razor ExPivot drawing them); nothing measures the Leaf Aggregate path yet (Step 6) |
| PV-49 | MUST | pass, accepted as is | pivot/pivot-view.test.ts:895; browser/pivot-live.spec.mjs:199; ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:142. Not asserted: Summary answering while stale; refusal's Japanese text and replacement by id; no row painted again |
| PV-50 | MUST | known limitation, carried from 0.2.0 | browser/pivot.spec.mjs:707, :735. Row itself says Escape, Tab and the no-report panel typed while opening are not checked over the Script Core |
| DA-1 | MUST | pass | ExGrid.PackageSmoke/check.sh:119-148, :162, :179 (row restated by #540) |
| DA-2 | MUST | pass | ExGrid.Data.Tests/ImmutabilityTests.cs:14, :45 |
| DA-3 | MUST | pass | ExGrid.Data.Tests/KindTests.cs:12, :51, :92 (+5) |
| DA-4 | MUST | pass | ExGrid.Data.Tests/ObjectTests.cs:13, :58 |
| DA-5 | MUST | pass | ExGrid.Data.Tests/LoadTests.cs:12, :67, CsvLoadTests.cs:67 (+2: DataReaderTests.cs:348, ExGrid.Data.Arrow.Tests/LoadTests.cs:59) |
| DA-6 | MUST | pass | ExGrid.Data.Tests/RefusalTests.cs:12, CsvRefusalTests.cs:19; ExGrid.Data.Arrow.Tests/RefusalTests.cs:15 (+1) |
| DA-7 | MUST | pass | ExGrid.Data.Tests/CsvTests.cs:15, :86, :118 (+3 and the separator/decimal/thousands/culture/date tests) |
| DA-8 | MUST | pass | ExGrid.Data.Tests/CsvEncodingTests.cs:37, :50, :103 (+1) |
| DA-9 | MUST | pass | ExGrid.Data.Tests/CsvSuggestionTests.cs:29, :274 |
| DA-10 | MUST | pass | ExGrid.Data.Tests/DataReaderTests.cs:78, :125, :141 |
| DA-11 | MUST | pass | cases data/change-batch.json (19); ExGrid.Data.Tests/RecordKeyTests.cs:29, ChangeBatchCaseTests.cs:50 |
| DA-12 | MUST | pass | ExGrid.Data.Tests/SharingTests.cs:67 |
| DA-13 | MUST | pass | cases arrow/write.json; ExGrid.Data.Arrow.Tests/RoundTripTests.cs:11, :79, ArrowCaseTests.cs:58 |
| DA-14 | MUST | pass | cases arrow/read-text.json, refusals-types.json; ExGrid.Data.Arrow.Tests/CompressionTests.cs:20, :33 |
| DA-15 | MUST | pass | cases arrow/write.json (date32, coarsest timestamp); round trip reads with no codec |
| DA-16 | MUST | pass | ExGrid.PackageSmoke/check.sh:231-236 (ArrowRoundTrip.cs) |
| DA-17 | OBSERVATIONAL | recorded | ExGrid.Data.Tests/MeasureTests.cs:36, :55; ExGrid.Data.Arrow.Tests/MeasureTests.cs:22; browser/measure-pivot.spec.mjs:265, :537, :598 (+1); recorded in Step 6 |
| DA-18 | MUST | known limitation, carried from 0.2.0 | ExPivot.Components/OneAggregationDefinitionTests.cs:42, :51, :60 (C# only). Script Core has two definitions: core/src/summary/summary-accumulator.ts, pivot-engine/src/aggregates/parts.ts |
| SM-1 | MUST | pass | core/grid-summary.test.ts:115 (whole request); cases summary/summary.json:2 (row restated by #540) |
| SM-2 | MUST | pass, accepted as is | core/grid-summary.test.ts:84; browser/selection-summary.spec.mjs:165, :224. Not asserted: an answer under a stale RSV shows nothing |
| SM-3 | MUST | pass | core/grid-summary.test.ts:243, :294; sheet/create-sheet.test.ts:2302 |
| SM-4 | MUST | pass | cases summary.json (million rows); core/grid-summary.test.ts:134; ExGrid.Tests/GridSummaryTests.cs:60; browser/selection-summary.spec.mjs:258 |
| SM-5 | MUST | pass | core/grid-summary.test.ts:236; ExGrid.Tests/GridSummaryTests.cs:91, :114; ExGrid.Components ScriptGridHandleTests.cs:341. `OnSummarize` is refused beside a source that summarises, as the row reads since #557 restated it to the code (the user's decision of 2026-11-07, ADR-0130's note) (grid-view.ts:2988, ExGrid.Surface.cs:206) |
| SM-6 | MUST | pass | cases summary.json (SM-6); sheet/sheet-summary.test.ts:163; ExGrid.Tests/GridSummaryTests.cs:50, :75 |
| SM-7 | MUST | pass | core/summary.test.ts:72; sheet/sheet-summary.test.ts:51, :57; ExGrid.Tests/GridSummaryTests.cs:124; browser/selection-summary.spec.mjs:100 |
| SM-8 | MUST | pass | ExGrid.DemoApi.Tests/TradeSummaryTests.cs:46 |
| SM-9 | MUST | pass | core/grid-summary.test.ts:67, :147, :332; sheet/sheet-summary.test.ts:69; browser/selection-summary.spec.mjs:44, :64 |
| SM-10 | MUST | pass, accepted as is | No test asserts the absence of a live region; selection-summary.ts sets no aria-live or role |
| SM-11 | MUST | pass | browser/selection-summary.spec.mjs:44, :165, :224 (+1) |
| SM-14 | MUST | pass, accepted as is | core/grid-summary.test.ts:218. Not asserted: switched on again asks; ExSheet/ExPivot carry it to their grid (pivot-view.ts passes showSelectionSummary) |
| SM-12 | MUST | pass, accepted as is | sheet/sheet-summary.test.ts:51, :83, :116 (+3). Not asserted: every cell of a spilled array is summed |
| SM-13 | MUST | pass | cases pivot/report-source.json:730; pivot/pivot-view.test.ts:720, report-summary.test.ts |
| SM-15 | MUST | pass, accepted as is | core/grid-summary.test.ts:294; sheet/create-sheet.test.ts:2302. Not asserted: with no figure the walk is not made |
| LV-1 | MUST | pass | core/grid-actions.test.ts:352, grid-cell-paint.test.ts:206; react/ex-grid.test.ts:153 |
| LV-2 | MUST | pass | core/grid-distinct-rows.test.ts:65; cases source/live.json:119; core/fetch-live.test.ts:235 (fixed by #539) |
| LV-3 | MUST | pass | cases source/live.json:7-168 |
| LV-4 | MUST | pass | cases source/live.json:239-470, change-highlight.json:196 |
| LV-5 | MUST | pass | core/live-requery-property.test.ts:344, source-boundary.test.ts:262; browser/grid-live-local.spec.mjs:67 |
| LV-6 | MUST | pass | cases source/live.json:486-649; core/source-boundary.test.ts:165, :179, :203 (+1: fetch-live.test.ts:191) |
| LV-7 | MUST | pass | cases source/live.json:674-734; core/source-boundary.test.ts:187; browser/grid-live-local.spec.mjs:67 |
| LV-8 | MUST | pass | core/fetch-live.test.ts:103, :110, :131 (+1); browser/grid-live.spec.mjs:330 |
| LV-9 | MUST | pass | cases change-highlight.json (12); core/fetch-live.test.ts:250-389, source-boundary.test.ts:232 |
| LV-10 | MUST | pass | core/grid-distinct-rows.test.ts:73; core/fetch-live.test.ts:235 (fixed by #539) |
| LV-11 | MUST | pass | core/grid-edit.test.ts:834, :848; browser/write-lands.spec.mjs:92, :119, :141 (+1) |
| LV-12 | MUST | pass | core/grid-actions.test.ts:214, :267, grid-held-row-commands.test.ts:109; browser/write-lands.spec.mjs:157 |
| LV-13 | MUST | pass | core/grid-edit.test.ts:862, grid-actions.test.ts:295; browser/write-lands.spec.mjs:183, :215, :240 (+3) |
| LV-14 | MUST | pass | browser/write-lands.spec.mjs:183; inspection of the grid-view.ts key gate and grid-paste.ts |
| LV-16 | MUST | pass | cases source/live.json:606, :627, :649 |
| LV-17 | MUST | pass | core/grid-edit.test.ts:900; browser/write-lands.spec.mjs:281, :311 |
| LV-18 | MUST | pass | browser/grid-live-local.spec.mjs:37, :67, :96 |
| LV-19 | MUST | pass | core/grid-edit.test.ts:379, grid-pending-commit.test.ts:380 |
| LV-20 | MUST | pass, accepted as is | core/grid-edit.test.ts:662, :679, :696 (+2); browser/write-lands.spec.mjs:429. Not asserted: RowLeftTheWindow commit lands once the key is back; with a Row Key, an order-moved press with its row gone is OrderMoved (grid-view.ts:6092-6108) |
| LV-21 | MUST | pass | browser/write-lands.spec.mjs:92 |
| LV-22 | MUST | accepted as is, no test | blazor/bridge.test.ts:175 (release protocol only). Weak-reference retention tests removed with Razor; nothing measures retention by the Script Core or the ExPivot report source |
| LV-23 | OBSERVATIONAL | not run | its harness measured the report Window path ADR-0370 section 3 ended (the C# report Windows and the Razor ExPivot drawing them); nothing measures the Leaf Aggregate path yet (Step 6) |
| LV-24 | MUST | known limitation, carried from 0.2.0 | pivot-engine/incremental.test.ts:299; ExPivot.Engine.Tests/IncrementalOracleTests.cs:135. Component and browser clauses not met (DoD note) |
| LV-25 | MUST | known limitation, carried from 0.2.0 | ExPivot.Engine.Tests/ReportProtocolTests.cs:85. Server report Window path removed; rewrite owed |
| LV-26 | MUST | known limitation, carried from 0.2.0 | ExPivot.Engine.Tests/ReportProtocolTests.cs:108, :137; cases pivot/report-source.json:493. Layer 2 not met |
| LV-27 | MUST | known limitation, carried from 0.2.0 | ExPivot.Engine.Tests/WindowChangesDigestTests.cs:42, :132, :160 (+2). Layers 2 and 3 not met |
| LV-28 | MUST | known limitation, carried from 0.2.0 | ExPivot.Engine.Tests/ReportProtocolTests.cs:248, :304; browser/pivot-db.spec.mjs:79 (Leaf Aggregate path). Layer 2 not met |
| LV-29 | MUST | pass | pivot-engine/incremental.test.ts:299, :336; ExPivot.Engine.Tests/IncrementalOracleTests.cs:135 |
| LV-30 | MUST | known limitation, carried from 0.2.0 | pivot/report-source.test.ts:261, :279; ExPivot.Engine.Tests/ReportProtocolTests.cs:171, :384. Layer 2 render counts not met |
| LV-31 | MUST | known limitation, carried from 0.2.0 | Layer 2 and the remote browser scenario not met (DoD note) |
| LV-32 | MUST | pass, accepted as is | core/grid-actions.test.ts:267, grid-marks.test.ts:257, grid-find.test.ts:301 (+4). Not asserted: paste, Delete, Ctrl+D/R, fill handle and fill key refused as SourceChanged (grid-view.ts:7973, :8041); ExSheet document clause |
| LV-33 | MUST | pass, accepted as is | core/grid-edit.test.ts:436, :555, :1078. Not asserted: replaced-Source variants (typing, composition, nothing selected); a moving key aimed the same way |
| LV-34 | MUST | known limitation, carried from 0.2.0 | ExPivot.Engine.Tests/ReportProtocolTests.cs:504-609 (6). Component and browser clauses not met (DoD note) |
| LV-15 | OBSERVATIONAL | recorded | browser/measure-live.spec.mjs:93; recorded in Step 6 |
| CH-1 | MUST | pass | chart/chart-dom.test.ts:174 |
| CH-2 | MUST | pass | chart/chart-dom.test.ts:212; browser/chart.spec.mjs:54 |
| CH-3 | MUST | pass, accepted as is | chart/chart-dom.test.ts:170. Not asserted: the 10^3 / 10^5 sizes (test compares 10 and 10,000 points) |
| CH-4 | MUST | pass | chart/chart-dom.test.ts:174, :200; browser/chart.spec.mjs:31 |
| CH-5 | MUST | pass | cases chart/scene.json:663, :684; chart/chart-dom.test.ts:318; browser/chart.spec.mjs:111 |
| CH-6 | MUST | pass | cases chart/appearance.json:129, :537 |
| CH-7 | MUST | pass | cases chart/surface.json:135, :171, surface-mesh.json:135; chart/surface-dom.test.ts:92; browser/surface.spec.mjs:27 |
| CH-8 | MUST | pass | cases chart/summary.json:174, csv.json:634; browser/chart.spec.mjs:132 |
| CH-9 | MUST | pass | browser/chart-showcases.spec.mjs:70, :98, :157 (+4) |
| CH-10 | MUST | accepted as is, no test | browser/chart-files.spec.mjs:58, :70 (chrome-150: PNG and 3D surface). Scale 1.25 never run; no check that 2D plot lines lie on Device Pixels |
| CH-11 | MUST | accepted as is, no test | cases chart/appearance.json:943 (line-style keys only). No forced-colors test for ExChart |
| CH-12 | MUST | pass | chart/chart-dom.test.ts:657, :694; browser/chart-files.spec.mjs:27, :35, :58 |
| CH-13 | MUST | pass | ExGrid.PackageSmoke/check.sh:162, :166 |
| CH-14 | SHOULD | pass | spikes/render-bench/results/20261019-chart (3 JSON + summary.txt); predates the TS move |
| RT-1 | MUST | out of scope (§34) | browser/responsive.spec.mjs:3, responsive-family.spec.mjs:48 (grid/sheet/pivot x builtin/mud). Not asserted: phone portrait/landscape viewports |
| RT-2 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-3 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-4 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-5 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-6 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-7 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-8 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-9 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-10 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-11 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-12 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-13 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-14 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-15 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-16 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-17 | MUST | out of scope (§34) | Desktop invariants held by other sections; targeted touch regressions and observations owed |
| RT-18 | MUST | out of scope (§34) | Touch not implemented; no real-device record |
| RT-19 | OBSERVATIONAL | out of scope (§34) | No measurement exists (touch not implemented); owed in Step 6 |
