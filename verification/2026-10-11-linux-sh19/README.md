# SH-19 measured: ExSheet's recalculation at a large Sheet, and completion on a circuit

Date: 2026-10-11, from 21:40 to 22:45 UTC, and after review on 2026-10-12, from 00:04 UTC. Branch
`claude/sh19-measure`, on `e3ad9a50`. The passes of 2026-10-11 measured the commit that first added
this record (`b9383e3e`); those of 2026-10-12 the commit after review, which changed only what is
checked outside the clock and how the browser spec takes a median.

SH-19 is OBSERVATIONAL in the Definition of Done (§27): recorded, never gated. Its case was decided
with the user on 2026-10-11: a synthetic Sheet from a fixed seed, 10,000 rows by 20 columns with about
50,000 Formulas — per-row arithmetic, a running total chained down a column, and `SUM` over whole
columns. It records a full recalculation, one edit's propagation, and a row inserted near the top;
and completion's keystroke-to-list time on the Server host with 150 ms injected. This is the first
SH-19 record, so there is no earlier one to compare with.

`metrics.json` holds every number: CoreCLR's under `coreclr`, the browser's under `chromium-local`
as the fixtures' `record()` writes them, and the machine under `machine`. [`raw/`](raw/) holds the
runs' own output.

**Every number is this machine's, and the machine was shared.** Other agents built and tested on the
same container meanwhile: its load average is given with each pass. Nothing here gates anything.

## The machine

| | |
|---|---|
| Container | A cloud container: a KVM guest, Intel Xeon Processor @ 2.10 GHz, 4 vCPUs, 15 GiB, no GPU (no `/dev/dri`) |
| OS | Ubuntu 24.04.5 LTS, Linux 6.18.44 |
| .NET | SDK 10.0.203 through nix; runtime 10.0.7, CoreCLR, workstation GC; the tests built with `-c Release` |
| Browser | Playwright's bundled Chromium 141.0.7390.37 (build 1194), not branded Chrome or Edge, which the container does not have; headed under `xvfb-run -a` (1280×1024×24). With no GPU it draws with its software rasteriser (SwiftShader, shipped beside it) |
| Playwright | 1.62.1, Node 24.14.1 (the flake's browser shell) |
| Server host | `samples/ExGrid.DemoHost.Server`, a Debug build started by `dotnet run`, as the suite's config starts it; `latency-proxy.mjs` between it and the browser |

## How

**Recalculation, on CoreCLR** — `tests/ExSheet.Engine.Tests/Measurements.cs`, four `[Fact(Explicit = true)]`
measurements that run only when asked for:

```sh
nix develop -c dotnet build tests/ExSheet.Engine.Tests -c Release
nix develop -c dotnet tests/ExSheet.Engine.Tests/bin/Release/net10.0/ExSheet.Engine.Tests.dll -explicit only -showLiveOutput \
  -method "ExSheet.Engine.Tests.Measurements.SH19_*"      # SH-19's three, about a minute
nix develop -c dotnet tests/ExSheet.Engine.Tests/bin/Release/net10.0/ExSheet.Engine.Tests.dll -explicit only -showLiveOutput \
  -class ExSheet.Engine.Tests.Measurements                # and the variant below, about a quarter of an hour
```

*(Moved on 2026-10-12, when SH-19's case was decided with the user as the one below. `Measurements.cs`
now holds SH-19's three alone, and `-explicit only` runs exactly those three in that project. The
variant left the suite for `spikes/exsheet-sh19-whole-column`, a console project outside
`ExGrid.slnx` that builds the same Sheet from the same seed, times the same three measurements and
checks every run as the test did:
`nix develop -c dotnet run -c Release --project spikes/exsheet-sh19-whole-column`. The commands
above are the ones the passes here ran.)*

- **The Sheet** is built from seed 20261011. Rows 1 to 10,000 of columns A to T are the 10,000 by 20:
  - A to O hold constants: a quantity in A, a price in B, amounts in C to O;
  - P, Q and R are per-row arithmetic, each reading the one before: `=A1*B1`, `=P1-C1+D1*E1`,
    `=(Q1+F1)/(1+G1/100)`;
  - S is the running total chained down the column: `=R1`, then `=S1+R2` and on to S10000;
  - T is per-row arithmetic again, `=R1*H1/100-I1`.

  Beside the block, V1 to V20 total each of its columns over the whole column, `=SUM(A:A)` to
  `=SUM(T:T)`. They stand beside it because a whole-column `SUM` in its own column would read
  itself. That is 50,020 Formulas, 20 of them reading a whole column, and 150,000 constants.
- **A full recalculation** is `Sheet.Open` on the Sheet's own `SheetDocument`: every Value computed
  from the Entries, as a Consumer's saved Sheet opens (ADR-0048). The engine has no other way to
  compute everything; it recalculates only what a change reaches (ADR-0047).
- **An edit** is a constant typed into a cell as the Cell Editor commits it, `Sheet.Do(SheetEdit.Enter(…))`,
  undo step included, 7 and 11 in turn so that each run changes the Value. Three cells: A1, which the
  whole running total reads; A10000, which only its own row and the totals read; and O5000, which only
  its column's total reads.
- **A row inserted near the top** is `Sheet.Do(SheetEdit.InsertRows(1))`, a row inserted at row 2, as
  Ctrl with + inserts one. Its undo puts the Sheet back for the next run, and is timed too.
- Each time is taken seven times, after two runs to warm up, each from a collected heap; the median,
  best and worst are given.
- **Each timed run is checked after it, outside the clock**, against the same arithmetic done in the
  test, so that no time is of a wrong answer: every Formula's text in P to T and in V, every Value of
  P to T, all twenty totals, and for an edit and an insertion how many Formulas it recalculated. A
  row inserted is checked before its undo: row 2 holds nothing, every number of the block and of its
  totals is one row further down and unchanged, and every Formula reads where its cells went (S3 is
  `=S1+R3`). The undone Sheet is checked as it was before the insertion.

  *(Changed after review, 2026-10-12. The passes before the fourth checked less: R, S and T and the
  one total V18, once after each series, and the inserted Sheet only after it was undone. The timed
  work is the same. The fourth and fifth passes ran the checks as committed, and every check held.)*

**Completion, on the Server host** — `tests/ExGrid.Browser/measure-sheet.spec.mjs`, which skips
itself unless `EXGRID_MEASURE=sheet`, and on WebAssembly:

```sh
cd tests/ExGrid.Browser
EXGRID_HOSTING=server EXGRID_MEASURE=sheet npx playwright test measure-sheet.spec.mjs --project=chrome
```

Here it ran through a local config, never committed, that runs the suite's config with one `chrome`
project launching the bundled Chromium, on this worktree's ports (5480 for the Server host's proxy),
under the layer-3 lock, with `--repeat-each=3`: three passes of twelve runs each.

- On `/sheet`, F2 (empty) is chosen and `=` typed, which opens the Cell Editor. Then **S** is typed,
  which opens the list, and **U**, which narrows it to the five functions beginning with SU. Escape
  closes the list and a second Escape cancels the edit, and the run starts again.
- Each key is armed only once the circuit is quiet (`circuitQuiet`), so nothing earlier is still on
  the wire.
- **The time is the page's own.** The keystroke is the keydown's `timeStamp`, read by a capture-phase
  listener on the window. The list is the first DOM change after which every candidate begins with
  the text typed and the list differs from the one shown before the key, kept by a MutationObserver.
  The frame is the animation frame after that change. Both are installed through `alterPage`.
- The same keys are measured with no round trip too, on the same host: the host's own share. Only
  the 150 ms case is SH-19's.

## Recalculation

### SH-19's case

The second pass, at a load average of 3.6 to 5.9 (22:18 to 22:19 UTC):

| Measurement | Formulas recalculated | Median | Best | Worst |
|---|---:|---:|---:|---:|
| A full recalculation (`Sheet.Open`) | 50,020 | **621 ms** | 605 ms | 813 ms |
| An edit to A1: the whole running total | 10,010 | **87 ms** | 74 ms | 103 ms |
| An edit to A10000: its row and the totals | 11 | **41 ms** | 39 ms | 84 ms |
| An edit to O5000: its column's total only | 1 | **17 ms** | 16 ms | 19 ms |
| A row inserted at row 2 | 50,015 | **1,798 ms** | 1,670 ms | 1,952 ms |
| That insertion undone | — | **2,479 ms** | 2,229 ms | 2,742 ms |

The first pass, at a load average of 9 to 22 while other agents' suites ran, gave 649 ms, 140 ms,
79 ms, 30 ms, 1,781 ms and 2,618 ms: the same order, wider spread (`raw/coreclr-pass1-all.log`).

The fourth pass, with every run checked as committed, ran on 2026-10-12 from 00:04 to 00:06 UTC at a
load average of 25 to 31, the busiest of any pass here: 709 ms, 92 ms, 65 ms, 21 ms, 2,194 ms and
3,325 ms, the same order again, and every check held (`raw/coreclr-pass4-sh19-checked.log`). The
second pass's table stays this record's numbers, being the one taken on the quietest machine.

### A variant: every row reading a whole column

The decided case puts its whole-column `SUM`s beside the block, twenty of them. A Sheet also often
has a column of each row's share of a total, `=R1/SUM(R:R)`. The variant is the same Sheet with T
written that way, so that 10,020 Formulas read a whole column where SH-19's case has 20. It is not the
decided case; it was measured because the cost it shows is the one
`docs/specs/exsheet/formula-dependencies.md` §11 names as "cost to know", and its open question 11
asks about. Its runs take tens of seconds each, so each is taken three times, with no run to warm up.

*(Since 2026-10-12 the variant runs as `spikes/exsheet-sh19-whole-column`, not in the test suite: the
user decided that SH-19 is the case above, and that an edit of about 45 s where a whole-column `SUM`
sits on every row is a Known limitation of `0.1.0` (ADR-0202). The spike's README carries the
numbers below and a run of its own. They were taken by the test `Every_row_reading_a_whole_column`,
since removed, and stay as recorded.)*

The third pass, from 22:30 to 22:44 UTC at a load average of 4 to 6:

| Measurement | Formulas recalculated | Median | Best | Worst |
|---|---:|---:|---:|---:|
| Built by one `SetEntries` | 50,020 | 85.4 s (one run) | | |
| A full recalculation (`Sheet.Open`) | 50,020 | **66.3 s** | 65.9 s | 70.9 s |
| An edit to A1 | 20,009 | **48.0 s** | 46.6 s | 50.4 s |
| An edit to A10000 | 10,010 | **44.8 s** | 44.5 s | 45.0 s |
| An edit to O5000 | 1 | **26 ms** | 22 ms | 28 ms |
| A row inserted at row 2 | 50,016 | **47.2 s** | 45.8 s | 48.8 s |
| That insertion undone | — | **49.4 s** | 49.4 s | 50.3 s |

The first pass, at a load average of 9 to 22, gave 84.1 s, 46.7 s, 46.2 s, 51 ms, 44.9 s and 49.8 s,
and a trial of the full recalculation alone, at about 6, gave 74.3 s.

The fifth pass, with every run checked as committed, ran on 2026-10-12 from 00:07 to 00:23 UTC at a
load average of 13 to 17: built in 100.1 s, then 76.5 s, 42.5 s, 39.0 s, 22 ms, 47.6 s and
52.5 s, with 20,009, 10,010, 1 and 50,016 Formulas recalculated as before, and every check held
(`raw/coreclr-pass5-variant-checked.log`).

## Completion on a circuit

The third pass, with the spec as first committed, from 22:26 to 22:29 UTC at a load average of 13 falling
to 3: three repetitions of twelve runs, 36 keystrokes of each kind. The time is from the keydown to
the animation frame after the list shows what was typed; the time to the DOM change itself is under
a millisecond less at 150 ms (`metrics.json` has both, for each repetition).

| The key | Round trip | Median of the three repetitions | All 36 runs: median | Best | Worst |
|---|---:|---|---:|---:|---:|
| **S**, the list opens | **150 ms** | 158.9, 163.0, 159.9 ms | **160.0 ms** | 157.7 ms | 190.3 ms |
| **U**, the list narrows | **150 ms** | 198.5, 198.2, 200.3 ms | **199.0 ms** | 156.9 ms | 206.6 ms |
| S, the list opens | 0 ms | 23.5, 25.2, 20.5 ms | 22.4 ms | 8.0 ms | 49.8 ms |
| U, the list narrows | 0 ms | 23.8, 19.4, 19.3 ms | 19.9 ms | 11.0 ms | 44.7 ms |

*(The medians corrected after review, 2026-10-12. The spec as it ran took the upper of the two
middle runs of twelve for its median, and so did this table. Each is now the mean of the two middle
runs, from the time of each run, which the spec kept; the spec takes its median that way since.
`metrics.json` keeps the `median` fields as `record()` wrote them, and says so.)*

The narrowing times fall in two clusters: 29 of the 36 between 190 and 207 ms, the other 7 between
157 and 166 ms. The two earlier passes, with earlier versions of the spec that read the same times,
agree: the list opened at medians of 159 to 170 ms and narrowed at 168 to 200 ms (the first pass's
are the upper of the two middle runs, since that spec kept no time of each run).

The fourth pass, with the spec as committed after review, ran on 2026-10-12 from 02:03 to 02:06 UTC
at a load average of 7 to 17, three repetitions of twelve runs again; then without `EXGRID_MEASURE`
on the Server host, and with it on WebAssembly, the spec skipped three times each
(`raw/browser-pass4-median.log`):

| The key | Round trip | Median of the three repetitions | All 36 runs: median | Best | Worst |
|---|---:|---|---:|---:|---:|
| **S**, the list opens | **150 ms** | 163.9, 162.3, 165.8 ms | **163.9 ms** | 158.4 ms | 184.8 ms |
| **U**, the list narrows | **150 ms** | 192.4, 159.8, 164.7 ms | **168.0 ms** | 156.9 ms | 206.0 ms |
| S, the list opens | 0 ms | 26.8, 24.7, 23.9 ms | 25.3 ms | 15.6 ms | 92.8 ms |
| U, the list narrows | 0 ms | 24.5, 23.5, 29.9 ms | 24.9 ms | 10.7 ms | 80.9 ms |

Here only 9 of the 36 narrowing times fell in the upper cluster, between 197 and 206 ms; the other
27 lay between 157 and 187 ms. How often the wait below falls changes from pass to pass, and the
narrowing's median moves with it, by 31 ms between the third pass and this one.

**Up to forty milliseconds of the narrowing are the latency proxy's, not the product's.** The proxy's
sockets are Node's defaults, which keep Nagle's algorithm on (`latency-proxy.mjs` never calls
`setNoDelay`). When the host writes two small messages back to back, the proxy writes them to the
browser's socket back to back after the delay, and the second can be held until the browser
acknowledges the first, which it may delay: 40 ms at least on Linux. That is the likely mechanism,
not one traced; what was shown is that turning Nagle off in the proxy takes the 40 ms away. Kestrel
and Chromium turn Nagle off on their own sockets, so a real network between them has no such wait.
The experiment, not committed: the same spec, one repetition, with `client.setNoDelay(true)` and `upstream.setNoDelay(true)` added
to the proxy (22:24 to 22:26 UTC, a load average of 12):

| The key | Round trip | Median | Best | Worst |
|---|---:|---:|---:|---:|
| S, the list opens | 150 ms | 163.3 ms | 159.6 ms | 251.2 ms |
| U, the list narrows | 150 ms | **158.9 ms** | 157.3 ms | 186.7 ms |

The proxy was put back as it is in the repository afterwards; every number above that table was
taken through it unchanged.

## What the numbers say

Observations, not decisions: what to do about any of them is the user's.

- **Read as this record reads it, SH-19's case recalculates in well under a second, and an edit in
  under a tenth.** The full recalculation is 0.6 s; an edit that runs down the whole 10,000-cell
  running total is 87 ms. That is one reading's result, not a decided one. The case says "10,000 rows
  by 20 columns … and `SUM` over whole columns", and this record puts the twenty whole-column `SUM`s
  beside the block. A whole-column `SUM` can as well sit inside it, in any column but the one it
  reads — the variant below, each row's share of a total — and read that way an edit takes about
  45 s and holds the circuit as long, where the spec asks that "a large Sheet stays responsive"
  (`docs/specs/exsheet/spec.md`, story 19). Which reading SH-19 means is the user's to say.
  *(Decided with the user on 2026-10-12: SH-19 is the reading this record took, the totals beside
  the block; the variant is a Known limitation of `0.1.0` (ADR-0202), and runs as
  `spikes/exsheet-sh19-whole-column`.)*
- **An edit's time follows the whole-column `SUM`s it reaches more than the cells it recalculates.**
  One Formula, `=SUM(O:O)`: 17 ms. Eleven, six of them whole-column `SUM`s: 41 ms. 10,010, the same
  six and the 10,000-cell running total: 87 ms. Not profiled. Read as a sum, each whole-column `SUM`
  is about 5 ms in a Sheet holding 200,000 cells, the 10,000 chained cells about 46 ms together, and
  about 12 ms is spent whatever the edit reaches. The code says why a whole-column range costs what
  the Sheet holds rather than what the column holds: a range larger than the cells the Sheet holds is
  read by visiting every held cell (`Sheet.CellsIn`, `Sheet.cs:557-578`). That is done once to check
  the Formula's References for `#CIRC!` and `#GETTING_DATA` (`Sheet.Taint`, `Sheet.cs:539-555`), and
  once more for the `SUM` itself, whose cells are then sorted (`StagedReader.NonBlankIn`,
  `Sheet.cs:594-595`).
- **A row inserted near the top takes 1.8 s, three times a full recalculation, and its undo 2.5 s.**
  Every Formula below the row has its References rewritten, so all 50,015 are recalculated. Beside
  that, the insertion snapshots and compares every held cell, before and after, and rebuilds the
  dependency graph (`Sheet.Restructure`, `Sheet.Structure.cs:110-167`). The undo is the inverse
  edit, then every rewritten Formula put back with one more recalculation (`Sheet.Unrestructure`,
  `Sheet.Steps.cs:69-84`).
- **With 10,000 Formulas each reading a whole column, a single edit takes about 45 s.** Two things
  grow together:
  - each of the 10,000 `=R…/SUM(R:R)` visits all 200,000 held cells twice;
  - every lookup of a cell's dependents scans every Formula that reads a range (`Sheet.Dependents`,
    `Sheet.cs:377-395`), and a recalculation makes one or two such lookups for each cell it reaches.

  The edit to O5000, which reaches no `R`, stays at 26 ms. The engine is synchronous (`Sheet`'s
  remarks), so on the Server host the circuit that holds the Sheet is held as long.
- **On a circuit of 150 ms, the list is on screen one round trip and about 10 to 15 ms after the
  key.** It opens at a median of 160 ms (164 ms in the fourth pass). It narrows at 199 ms through
  the suite's latency proxy in the third pass and at 168 ms in the fourth, as the proxy's wait fell
  on 29 and on 9 of the 36 runs, and at 159 ms with that wait taken out; on the host with no round
  trip, both take about 20 to 25 ms. So nearly all of SH-19's keystroke-to-list time is the round
  trip itself.
- **The suite's latency proxy adds up to 40 ms to an answer that arrives as two messages.** It is
  the harness's, and it can be in any number taken through it at a round trip above 0, here and in
  earlier records. Whether to turn Nagle off in the proxy is not this record's to
  decide: it would make those records' numbers incomparable with the next ones at that scale.

## What was not done

- **Branded Chrome and Edge, and Windows.** The browser was the bundled Chromium on Linux, drawing
  in software. This record is not a Step 4 record and was not taken for one.
- **A published build.** The Server host was a Debug build started by `dotnet run`, as the suite
  starts it. The CoreCLR measurements were a Release build.
- **The completion measurement on the decided Sheet.** SH-19's case names no Sheet for completion,
  and the candidates are the declared functions and the Linked Tables, not the Sheet's cells, so it
  ran on `/sheet`, the demo's Sheet.
- **Two costs that ADRs say SH-19 measures.** ADR-0050 says a paste over whole columns "is executed
  and measured (SH-19)", and ADR-0124 that what volatile Formulas cost at a large Sheet "is measured,
  not asserted (SH-19)". The case decided on 2026-10-11 names neither, and this record measures
  neither: it pastes nothing, and its Sheet holds no volatile function. Whether SH-19's case grows to
  cover them is the user's to decide.
- **A decided reading of the case.** Where its whole-column `SUM`s stand changes an edit's time a
  hundredfold (What the numbers say, the first point); this record took one reading. *(The user
  decided on 2026-10-12 that it is SH-19's; see the variant's note above.)*

## Raw

[`raw/`](raw/) holds each pass's own output, colour codes taken out; the browser logs keep the hosts'
lines.

| File | What |
|---|---|
| `coreclr-trial-full-recalculation.log` | 21:40 UTC: the full recalculation of both layouts, from an earlier version of `Measurements.cs` whose layouts were the same |
| `coreclr-pass1-all.log` | 21:51 to 22:08 UTC: all four measurements, at a load average of 9 to 22; that version reported an insertion's and its undo's collections together |
| `coreclr-pass2-sh19.log` | 22:18 to 22:19 UTC: SH-19's three, as first committed (`b9383e3e`); the numbers above |
| `coreclr-pass3-variant.log` | 22:30 to 22:44 UTC: the variant, as first committed; the numbers above |
| `browser-pass1.log` | 22:15 to 22:18 UTC: the spec with each run's time not yet kept |
| `browser-pass2.log` | 22:20 to 22:24 UTC: the spec reading also the first DOM change after the key, which came from the grid's own handling of the key about 2 ms after it and told nothing, so it was taken out; with the skip runs on both hosts after it |
| `browser-experiment-nagle-off.log` | 22:24 to 22:26 UTC: the experiment, with the proxy changed locally |
| `browser-pass3-final.log` | 22:26 to 22:30 UTC: the spec as first committed, `--repeat-each=3` on the Server host; then without `EXGRID_MEASURE` on the Server host, and with it on WebAssembly, each skipping three times |
| `coreclr-pass4-sh19-checked.log` | 2026-10-12, 00:04 to 00:06 UTC: SH-19's three with every run checked, as committed after review |
| `coreclr-pass5-variant-checked.log` | 2026-10-12, 00:07 to 00:23 UTC: the variant with every run checked, as committed after review |
| `browser-pass4-median.log` | 2026-10-12, 02:03 to 02:08 UTC: the spec as committed after review, `--repeat-each=3` on the Server host; then without `EXGRID_MEASURE` on the Server host, and with it on WebAssembly, each skipping three times |

The fixtures' own run record, `verification/2026-10-11-linux/` (and `verification/2026-10-12-linux/`
for the fourth pass), was deleted after each run: a run of the bundled Chromium is not a Step 4
record. Its `console.json` was empty each time, so no test
saw a console error or warning (CON-1 to CON-3). Its `metrics.json` is `chromium-local.chrome` in
this record's `metrics.json`: the last repetition, as `record()` writes it.
