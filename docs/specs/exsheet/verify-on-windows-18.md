# What to verify on Windows, eighteenth run

Status: ready-for-human
Roadmap: internal

*(Numbered eighteenth from `docs/agents/numbering.md`. It is the second half of the sign-off's Windows run,
as ADR-0202's note of 2026-10-11 splits it and ADR-0208 shapes it. The seventeenth run asked Excel before the
release candidate, and run 18a, `verification/2026-10-14-windows-readings-18a/`, asked the questions its
corrections raised. This run renews the record of every by-hand row **at the commit `0.1.0-rc.1` is to be tagged on**, and asks what run 18a
and the corrections after it (#125 to #133) left open.)*

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-17.md`](verify-on-windows-17.md), its report (`verification/2026-10-11-windows-17/report.md`)
and run 18a's report (`verification/2026-10-14-windows-readings-18a/report.md`) first. Their method and tools
apply unchanged:
- the probes, `input-server.ps1` and `browser-input.ps1`, with keys sent as virtual-key and scan codes;
- run 17's `part-g/` scripts (`vz14.cmd`, `part_g_vz16.py`, `fn12e.cmd`, `part-g/ed30/`) and `sh42-probe.mjs`;
- Excel driven by run 18a's `excel/xl-server.ps1` and `xs.py`, each case in an Excel of the driver's own;
- `oracle.ps1`, as run 17's Part A ran it;
- the hosts and the latency proxy.

**Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.
Change no code and no test: a disagreement is listed, never fixed on the spot. The two exceptions are the
corpus, where `-Update` rewrites `source` for the cases that agree (Part C), and `verification/by-hand.json`,
where each row this run records gets its record (Part A, "Filing the records").

**The build is the head of `release/0.1`, not `main`, and the run comes before the tag.** The release check refuses
a release candidate unless every by-hand row holds at the commit it is tagged on, on a record whose covered code has
not changed since (ADR-0208), so the records exist before `v0.1.0-rc.1` does. The run is made at the head of
`release/0.1`, once the branch is cut (ADR-0205); its records are committed on top, and `v0.1.0-rc.1` is tagged on
the commit carrying them, which differs from the verified commit only under `verification/` and in the corpus.
*(2026-10-18, decided with the user: until then this file said not to start before the tag, which the release
check could never let happen.)* If `release/0.1` is not yet cut when this run is handed over, do not start: say so
and stop.

What this run asks:

| Part | What it asks | Where | Settles |
|---|---|---|---|
| A | ExGrid's and ExSheet's by-hand rows on this desk: VZ-14, VZ-16, FN-12e, ED-30, SH-42, SH-51, SH-52 | this desk | their records at the commit `0.1.0-rc.1` is to be tagged on (ADR-0208); the release check |
| B | The by-hand rows this desk cannot run: FN-12e and KB-3 on macOS, WR-10, VZ-18 | the user's Mac; Windows or Linux; none | the same |
| C | The corpus's cases still `uncertain` or `documented` | Excel | ADR-0208's "no `uncertain` case without a written reason"; ADR-0047, ADR-0124 |
| D | Excel's answers no run has read yet | Excel | the Toolbar's Escape, the Heading Mark, ExPivot's General and captions |
| E | ExSheet's build beside what run 18a read of Excel | `/sheet` | #128, #129 and #131 as built |

Run them in the order A, C, D, E. Part B is not this desk's: the request that hands it over says who runs it.
Commit and push after each Part, so that a run stopped half way leaves every finished Part on the branch.

## Where each question comes from

Every question file of `docs/specs/exsheet/run-18/`, and what this run does with it. "18a" is run 18a's report.

| File | Answered by 18a and its corrections | Carried here |
|---|---|---|
| `area-oracle.md` | 139 cases asked; 40 promoted; the rest corrected or recorded in #125 to #127 | Part C: PMT-043 by keys, and every case still `uncertain` |
| `aria-rows.md` | not asked | Part D, d6's note: how the ExPivot probe's row numbers are read |
| `dod-gaps-browser.md` | not asked (by-hand rows wait for `release/0.1`) | Part A, VZ-14 |
| `f4-enter-mode.md` | 1 to 8 read in Excel (18a §2), and built (#128); 9 on Windows passed (18a §3) | Part E, e1 to e8: ExSheet's side, which 18a did not read. Part B: 9's macOS half |
| `fill-handle-axis.md` | 1 to 7 read; 4, 5 and 7 built (#129, ADR-0050's notes of 2026-10-14) | Part E, e9 to e11 |
| `focus-among-ranges.md` | 2 to 4 read, and built (#131); 1 not asked (no display at 100%) | Part D, d5 (if a display at 100% is at hand); Part E, e12 and e13 |
| `format-cells-readings.md` | 1 to 10 read; built (#130); ko-KR's `mmm` left for after `0.1.0` | nothing: the Border tab's state on opening is a dialog's, which layer 2 reads exactly; ko-KR is not asked for `0.1.0` (the file's note of 2026-10-16) |
| `glyphs.md` | not asked (by-hand rows wait for `release/0.1`) | Part A, FN-12e: item 1, the paintings. Item 2, the measurement, is no longer the row's (narrowed, decided with the user 2026-10-16) |
| `heading-mark.md` | not asked | Part D, d2 to d5 |
| `pivot-general.md` | not asked | Part D, d6 |
| `toolbar.md` | not asked | 1: Part D, d1. 2: Part A, SH-52. 3: Part A, ED-30 |
| `widening.md` | 1 to 11 read; case 5 decided with the user and built (ADR-0071, 2026-10-14) | Part E, e14: the toolbar's Font Colour as decided |

## Setup

- **Branches.**
  - Fetch **`release/0.1`** and record its head, in full, as the verified commit.
  - Branch **`claude/exsheet-windows-verify-18`** from it. If `release/0.1` moves during the run, do not move to
    it: the Parts made at another commit go in a directory of their own ("The verified commit's tag"), and the
    rows whose covered code the move changed are run again there (ADR-0208).
- **Authorisation.** The request that hands this run over says whether the user has authorised it in advance.
  If it does not, ask before the first input.
  - What may be authorised in advance: real keys and mouse to Excel and the browsers, the IME switched on and
    off by its keys, and changes of the regional format (`Set-Culture`), each put back and recorded (run 18a
    was told a change of regional format need not be asked each time).
  - **Ask the user every time, authorised or not:**
    - before turning the Microsoft IME's Predictive input off (Part A, ED-30), and again before putting it
      back;
    - before installing or switching Office's display language (Part D, d6);
    - before anything that needs a sign-out, such as changing the display scale. Do not change it: a sign-out
      ends WSL and this session.
  - Say "starting" before the first input and "finished" after the last.
- **Record the environment** once, at the start: Windows' version and build; Excel's version, build, update
  channel and display language; Chrome's and Edge's versions; the Microsoft IME's version and its Predictive
  input setting; the display scale and resolution; the display language; the regional format (`Get-Culture`)
  and the keyboard layouts installed.
- **Excel.** Before Part C, close every workbook and Excel itself. Copy Excel's AutoRecover folder first and
  compare it at the end, as run 18a did. With `-Keys`, nothing else may take the foreground, so tell the user
  before it starts. No UI Automation between keys while an edit is open, but the Formula Bar's selection read
  as run 18a's `bar2` reads it.
- **The DemoHosts**, built from the verified commit in this run's own worktree:
  - WebAssembly, the Server host, and the Server host behind the latency proxy (`setRoundTrip`), as each row
    asks;
  - ports no other checkout on this desktop uses (`EXGRID_BASE_URL`; `hosting.mjs` derives the rest);
  - Layers 1 and 2 need not be run.
- **Results.** Everything goes under `verification/<date>-windows-18/`: the scripts, the records, the logs, the
  pictures, and one `report.md` with a row per case. Each row says what was read and, where this document gives
  a reading, whether it matches. Part C's oracle writes `dotnet/tests/ExSheet.Engine.Tests/ExcelOracle/results-<date>-run18-*.json`;
  commit those as well. Whole-window pictures that show the title bar's account corner stay on the machine.

## Part A — the by-hand rows on this desk (ADR-0208)

This Part is the record of these rows at the verified commit. Each holds until a commit changes the paths its
row names in the Definition of Done's Step 4 table, "Rows checked by hand, and the code each covers".

- **Which rows to run.** ADR-0208 runs again only a row whose paths changed since its record, and a row with no
  record. At `origin/main` on 2026-10-17 (`42469ac4`), every row of this Part is one or the other: VZ-14's, ED-30's
  and SH-42's paths have changed since their records (67, 138 and 65 files), and VZ-16, FN-12e, SH-51 and SH-52 have
  no record. Check it again at the verified commit, for each row, and put the output in the report:

  ```sh
  git diff --name-only <the row's record commit> <the verified commit> -- <the row's paths>
  ```

  A row whose paths are unchanged is not run, and the report says so.
- **The verdict** is read against the row as the Definition of Done words it at the verified commit, exactly as
  a sign-off would. Give each row a verdict row: commit, browser, host, scale, verdict. A row that cannot be run
  here is `not run`, with the reason, never a pass. Mark what reads differently from the seventeenth run.
- **The display** stays at Windows' 150%, as in runs 17 and 18a.

### VZ-14

`scrollbar.spec.mjs`'s block "on a Windows desktop at fractional display scaling (VZ-14)", headed, in Chrome and
Edge, on WebAssembly and on the Server host, as run 17's `part-g/vz14.cmd` ran it.

- *From `dod-gaps-browser.md`, item 1.* Since commit `2d8695d9` the gutter's observer, and the Layout Ceiling's,
  tell C# from a task of their own after the browser's observation, not inside its callback (`toldAfterTheObservation`
  in `dotnet/src/ExGrid/Assets/ex-grid.js`; CON-4, ADR-0013). This is the first run to read that deferred report at a
  scale that is not a whole number of device pixels per CSS pixel. No step is added; record the gutter, the
  `devicePixelRatio` and the Focus's place as run 17 did.

### VZ-16

As run 17's Part G read it (`part_g_vz16.py`), in Chrome and Edge, on WebAssembly:

- **A zoom told as it changes.** On `/wide`, never on `/sheet` (ExSheet claims Ctrl+- and Ctrl++, ADR-0050). Real
  keys outside any Sheet: Ctrl+= five times, Ctrl+0, Ctrl+- four times, Ctrl+0. After each, record
  `devicePixelRatio` and the `--ex-dp` on the grid's root.
- **A real 2.25.** The display's 150% and the browser's 150%, reached on `/wide`, then `/sheet?case=11` on the
  same origin. Read B2's thin Border and E8's gridline in device pixels from the compositor's picture, as
  `device-pixel-225.spec.mjs` reads it. The row expects a thin line of one Device Pixel and a gridline of two.
  - The clip is in the document's units before the browser's zoom, and the page's scroll is part of it. Run 17
    misread it three ways before it read it right (`records/part-g-first-try/`); start from its fourth pass.
  - ADR-0206's tints and pinned shade landed after run 17's record, which is why the row runs again.

### FN-12e (Windows)

*From `glyphs.md`*, as the row reads since 2026-10-16. The user decided that day to narrow the Windows row to the
painting, as the macOS row is: the Windows widths are committed (`tests/GlyphWidths/system-ui.windows.chrome.json`,
`system-ui.windows.msedge.json` and Roboto's two) and held by layer 1, so `glyphs.md`'s item 2, the measurement,
is not asked. The estimate's tables changed after run 17's commit, within the row's paths, so the painting is.

- **The paintings**, in Chrome and Edge, on WebAssembly and on the Server host, headed, as run 17's
  `part-g/fn12e.cmd` ran them:
  - `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)";
  - `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in,
    and no more fit (ADR-0016)".
- Record the display language: `system-ui` is Segoe UI only under an English one. Other display languages are a
  Known limitation of `0.1.0` (decided with the user, 2026-10-12), not asked.

### ED-30

The sixteenth run's cases k1 to k16, with run 17's additions (k6x, k6y, k8x, k13x) and its DC-47 case, on
`/sheet` and `/sheet?chrome=mud`, in Chrome and Edge, on WebAssembly, on the Server host and on the Server host
behind 150 ms: 12 records per case. k7 runs behind 150 ms only, k11 on `/features` and `/features?chrome=mud`,
k14 on `/sheets` and `/sheets?chrome=mud`. Use run 17's probe and scripts (`part-g/ed30/`), with the same keys
and readings, and compare every state with run 17's record of the same case, state and configuration.

- **k6, k6x and k6y read as ADR-0080 reads them, not as the sixteenth run's column.** That column expected one
  composition of two clauses; ADR-0080 records it as the procedure's error: this IME ends a converted clause's
  composition at the next clause's first key, in Excel too. So k6 expects two compositions, the Cell Editor
  opening on the first while the keyboard stays in the Keyboard Field for the second, and D10 `かな感じ`, with no
  key lost, doubled or reordered: the row's "keys typed after a composition's end follow it in order". Run 17
  read exactly that and marked it "not met as written" against the old column; that mark is not carried here.
- **What ED-30's last record failed on, and where each stands.** The record is the sixteenth run's, `fail`, for
  its differences 1 and 3. Difference 1, a key of the second clause lost on WebAssembly, was fixed by ticket 120
  and read gone in every configuration by run 17 (k6, k6x, k6y). Difference 3, the press on D12 that never reached
  the page, is the IME's prediction list, which k8 now runs with off (below). Neither is expected again; a reading
  of either is a defect, recorded as one.

- **k8 with Predictive input off**, as the Definition of Done's Step 4 row says (decided with the user, 2026-10-16).
  In runs 16 and 17, k8's press on D12 landed on the IME's own prediction list and never reached the page.
  `verification/2026-10-12-windows-ed30-k8/` turned the Microsoft IME's Predictive input off and read k8 as written
  in 12 of 12 configurations, at run 17's commit; it was not ED-30's record, and its method is the row's.
  - With the user's consent, set the IME's General page, Predictive Input, to **Off** before k8, as that run did
    (its `scripts/uia-settings.ps1` and `uia-combo.ps1`), and export the IME's registry key before and after.
  - Run k8 and k8x with it off. Run every other case at the setting the user had, as run 17 did, and record it.
  - Put it back as it was, with the user's consent, and compare the registry key with the export from before.
    That run found that putting it back left a value that was not there before; remove such a value, and record it.
- **The IME and the Alt key** (*from `toolbar.md`, item 3*; ED-30 shares the root's `keyup` listener with the
  Alt hold). On `/sheet` with the toolbar shown, in Chrome and Edge, on WebAssembly and on the Server host:
  - **t3a.** With the Microsoft IME on and nothing composed, its own toggle (半角/全角, and Alt+\` where the
    keyboard has it): do the KeyTips show? Record every `keydown` and `keyup` the probe hears between the press
    and the release. The gate tells an Alt "alone" by the keydowns between them, and a key the IME takes as a
    hotkey may reach none.
  - **t3b.** Alt, H, 1 typed at once (the KeyTips' Bold, leaving the keyboard on the Sheet), then `kana` with the
    IME on, then Enter twice: does the composition start in the Focus cell, with nothing lost or doubled?
- **The verdict rows** are the Definition of Done's pass column, clause by clause, as run 17 wrote them: "D10 shows
  the composition while no edit is open", "after its end the Cell Editor holds the text and has DOM focus", "Enter
  commits it and moves to D11", "after a press on D12 mid-composition, D10 holds the text and D12 is selected".
  Each case also has its row, as run 17's table has.

### SH-42

On `/sheet`, with several tabs open in the window, in Chrome and Edge, on WebAssembly and on the Server host: each
of Ctrl+1 to Ctrl+5 on D10, with run 17's `sh42-probe.mjs`. Record whether the Sheet acts (as Excel's formatting
keys) and whether the browser switches tab.

### SH-51

On `/sheet` (which binds `ShowToolbar`), in Chrome and Edge, on WebAssembly and on the Server host: Ctrl+F1 twice
on D10. Record whether the key reaches the page and the toolbar hides and shows, or the browser takes it (run 17's
d5).

### SH-52

Run 17's d3, d4 and d6, on `/sheet` and `/sheet?chrome=mud`, in Chrome and Edge, on WebAssembly and on the Server
host:

- **d3.** A press on D10; Alt pressed and released alone; then F10. Whether the browser's own menu opens, or its menu
  bar or address bar takes the keyboard; whether the Sheet shows its KeyTips; where DOM focus is.
- **d4.** Alt, H, then each formatting letter the Sheet shows, one at a time, Escape between. What each letter runs
  on D10, beside run 17's d1 (Excel's Home tab letters). Under the MudBlazor Chrome, run 17 found the lists FC, H, B
  and N stayed open after Escape and piled up: read whether they still do.
- **d6.** With an edit open (F2 on D10): Alt, then F10. Nothing starts; record where the keyboard is.
- **Keys at machine speed** (*from `toolbar.md`, item 2*; ADR-0100, ADR-0021's seventh entry as widened on
  2026-10-13). The grid's key gate now hears Alt's `keyup` on the root, holds the release while it holds keys, and
  keeps a chord's release from the Sheet. On `/sheet` with the toolbar shown, in Chrome and Edge, on WebAssembly and
  on the Server host behind 80 ms: does the browser's menu (the ⋯ button taking focus, or a menu opening) stay
  closed, and do the KeyTips show as they do typed slowly, for each of:
  - Alt released alone;
  - Enter on an open edit, then Alt, H, 1, typed at once;
  - Alt, Alt, H, 1, typed at once;
  - Alt+↓ with the Alt released before the ↓ (now no KeyTips: the release is a chord's).

  Record the console on the Server host under the MudBlazor Chrome: run 17 found an "aria-hidden" warning there.

### Filing the records

In the same commit as each row's record, replace that row's `record` in `verification/by-hand.json`: the verified
commit in full, the tag ("The verified commit's tag"), the record's file, and the verdict as this Part gives it. A
verdict that is not a pass stays as it reads. A row put out by decision keeps its `knownLimitation`. That is filing
the record, not deciding anything.

## Part B — the rows this desk cannot run

The release check refuses a release candidate while any row has no record that holds. These rows are not this
desk's; each is listed with what to record and where, for the run the request names.

| Row | Where | What to record | Record and tag |
|---|---|---|---|
| **FN-12e** (macOS) | the user's Mac, Chrome and Edge, both hosts, headless (`EXGRID_HEADLESS=1`) | The two paintings of Part A's FN-12e, by name. The macOS widths are measured and held by layer 1, and are not measured again | `verification/<date>-macos-18/`, tagged `verified/<date>-macos-18` |
| **KB-3** | the user's Mac, the same run | Layer 3 on a Mac, where `ControlOrMeta` presses Command: `npx playwright test`, Chrome and Edge, on both hosts. The verdict names the Cmd+Enter tests on every key path, the editor's included. *From `f4-enter-mode.md`, item 9's macOS half:* `features.spec.mjs`'s two ED-2 tests, by name, where Option+←/→ move by a word and ⌘+←/→ go to the text's ends, held behind F2 or not | the same directory and tag |
| **WR-10** | Windows or Linux, with Chrome and Edge installed | The Wrappers' MudBlazor floor run and its layer-2 runs, by the Definition of Done's §22 Step 4, "The Wrappers' MudBlazor floor", at the verified commit. Its record at `0399f391` no longer holds: `dotnet/samples/ExGrid.DemoPages/SheetCases.cs` has changed since. Every stable 9.x listed on nuget.org at the time of the run is a layer-2 run | `verification/<date>-<platform>-mudblazor-floor/`, tagged by its name, as `verification/2026-10-15-linux-mudblazor-floor/` was |
| **VZ-18** | the remote-isolation PC | Nothing. It is a Known limitation of `0.1.0` (decided with the user, 2026-10-12), and its `knownLimitation` stays | — |

Each of those runs files its records in `verification/by-hand.json` as Part A says, and pushes its own tag.

## Part C — the corpus's open cases (ADR-0202, ADR-0208)

The corpus at `origin/main` on 2026-10-17 holds 35 `uncertain` and 10 `documented` cases (run 18a left 99
`uncertain`; #125 to #127 settled the rest). Count again at the verified commit, by `source` and by file, before
anything runs, and put the counts in the report.

1. **Through COM, under en-US**, `oracle.ps1 -Id` over every case still `uncertain`, with `-Update` in the same call:
   - the 28 run 18a recorded, which the engine refuses by decision and which carry their written reason
     (`engineDiffersByDecision`): AGGREGATE-069, COUNT-009, COUNTA-009, ERRORTYPE-022, IFERROR-017 to 020, IFNA-008,
     INDIRECT-073, 074, 082 to 084, 090 to 092, 094 to 100, ISERROR-011, REF-015, SEQUENCE-009 and TABLE-024. Asked
     again as a check: Excel's answer is recorded, not compared;
   - **INDIRECT-101 to INDIRECT-105**, added after run 18a (#125), which no run has asked. 101 to 103 carry the
     engine's answer, read from INDIRECT-075's and 076's sums: R2C2:C3 is B2:C2, and R3:R2C2 is B2:B3, not whole
     rows. 104 is a whole column joined to a cell, the order no run asked. 105 is rows joined then columns.
2. **By keys** (`-Keys -Update`), Excel visible and in front, the user told beforehand:
   - **PMT-043**, `=PMT(1E-320,0.5,1000)` (*from `area-oracle.md`*). Run 18a's COM pass could not enter it
     (0x800A03EC). Read whether Excel keeps the rate as typed, and what `PMT` gives;
   - **LET-113**, as run 18a asked it, again as a check.
3. **By hand**, in a fresh workbook with real keys, since `oracle.ps1` cannot ask them:
   - **NAME-006, 007 and 008**: a qualifier naming no Sheet. Excel may read it as another workbook and open a file
     dialog: record the dialog's text and what the cell holds after Escape;
   - **NAME-036**: rename the sheet to `Data` and Ctrl+Z, as run 17's b2 did, then read B1's Formula and Value (b2
     read the rename's undo but not the Formulas).
4. **Not asked**: TABLE-005 to 010. Excel has no Linked Table waiting for its data.
5. **The report** lists the counts after the run against step 1's, every case still `uncertain` or `documented` with
   why, and every disagreement on its own line: the case id, its file, the Formula, Excel's answer and the corpus's
   `expect`. Run 18a's caution holds: a case `-Update` promotes that layer 1 then fails (as EDATE-023 did,
   `RefusalTests`) is put back to `uncertain` by hand and named for the orchestrator.

Commit the corpus as `-Update` left it, and the results files.

## Part D — what no run has read of Excel

Each in a new workbook in an Excel of the driver's own, at 100% zoom unless it says otherwise, under en-US unless it
says otherwise. Pictures cut to the cells and the Headings.

| # | What | Steps and readings | From |
|---|---|---|---|
| d1 | Escape after Alt, H, F | In Excel 16: Alt, H, F, then one Escape. Are the Home tab's letters shown again (every one, F among them), the tabs', or none? Then a second Escape. ExSheet treats letters that narrow a level as a level of their own (R2 (a)); that F is such a level is by generalisation | `toolbar.md`, item 1 |
| d2 | One cell, and one ordinary range | A blank sheet, D4 alone, then D4:F7 from D4, at Excel's 100% and 150% zoom. Read down the column Headings' bottom edge under C, D, F and G, and across the row Headings' right edge at rows 3, 4, 7 and 8, with R4's `px.ps1`: the line's width in device pixels and its colour. Then the same on `/sheet` in Chrome, at the browser's 100% and 150% | `heading-mark.md`, item 1 |
| d3 | Where the two lines meet | A1 alone, then A1:B2: the device pixels where row 1's Heading's line meets column A's, under and beside the corner, in Excel and on `/sheet`. R4 (2) showed them joining; ExGrid's stop at the corner | `heading-mark.md`, item 3 |
| d4 | A range at the top or the left; frozen panes | A1 alone, A1:B2, and column C as a whole column: down the column Heading's bottom edge into row 1, and across the row Heading's right edge into column A, at both zooms. ADR-0008's D2: where a Heading covers the outline's side, the Heading Mark supplies its two Device Pixels without a second line. Then View, Freeze Panes on column B, A3:D5, and a scroll right by three columns: do the frozen columns' Headings keep their line while the scrolled ones pass beneath? Each beside `/sheet` (its Pinned Columns for the frozen pane) | `heading-mark.md`, items 4 and 5 |
| d5 | The Headings' shade and letters; a display at 100% | The cases of d2 and d4 at both zooms, the ground sampled clear of the letters: selected Headings `#E1E1E1`, a whole column's or row's `#D3F0E0` with letters `#217346`, and the corner's `#217346` triangle when every cell is selected. Then the same under Excel's Dark Gray and Black Office themes, whose Heading shades no run has read; put the theme back. **Only if a display at 100% is at hand**: R4's three selections, d2's and the Heading line's width there (`heading-mark.md` 2, `focus-among-ranges.md` 1). This desk has one display at 150%, and changing it signs Windows out, so otherwise `not run` | `heading-mark.md`, items 2 and 6; `focus-among-ranges.md`, item 1 |
| d6 | ExPivot's General and its captions | `pivot-general.md`'s ten items, each beside ExPivot on `/pivot-csv` with the same CSV, as run 17's Part E made them. Under sv-SE (items 1 and 7), fi-FI and nb-NO (item 7): Set-Culture, then a new Excel. Items 3 and 6 need the Japanese edition, and item 9 Office's other English display languages: ask the user before installing or switching one, and if not, record the item as not run. Read each through COM and from a picture | `pivot-general.md`, items 1 to 10 |

**d6's note** (*from `aria-rows.md`*). Run 17's `part-e/pivot-probe.mjs` records each painted row's `aria-rowindex`
and the report's `aria-rowcount`. Since 2026-10-12 both count the header's rows first: a row per tier of the column
Items' Header Groups, then the header row. `compare.py` matches the body by row label, so a comparison is unchanged.
Where this run reads the probe's `row` or `rowCount` as the report's own numbers, it subtracts the header's rows, as
`headerRowsOf` in `tests/browser/fixtures.mjs` counts them.

## Part E — ExSheet beside what run 18a read of Excel

Run 18a read Excel only; #128, #129 and #131 built what it read. Each case here is typed with real keys or presses on
`/sheet`, in Chrome and Edge, on WebAssembly and on the Server host, and on the Server host behind 150 ms where the row
says so. Read after every key, as run 18a read Excel: the cell's text, the Formula Bar's text and its selection or
caret with start and end offsets, the mode, the Selection and the Focus, and a picture of the cells. The reading to
match is 18a's, in its report's section 2.

| # | Case | Excel's reading (18a) | From |
|---|---|---|---|
| e1 | `=A1+B2` typed in the Formula Bar, F2, F4 | `=A1+$B$2`, a caret at the end | `f4-enter-mode.md` 1 |
| e2 | In the bar: (a) `=A1+B2`, ←, →, F4; (b) `=A1+B2`, ←×3, `0`, F4; (c) a press right after `A1` of a committed `=A1+B2`, F4. Then (b) again behind 150 ms, typed as a burst | (a) a caret at 8; (b) `=$A$10+B2`, `$A$10` selected; (c) `=$A$1+B2`, `$A$1` selected | `f4-enter-mode.md` 2 |
| e3 | In D10: `=`, ↓, Shift+↓, Shift+Home | `=A11:D12`, Point, `A11:D12` selected and outlined | `f4-enter-mode.md` 3 |
| e4 | F2 on `=Sheet2!A1`, F4; then F2 on `=Sheet2!A1+1`, ←←, F4 | a caret at the end; then `=Sheet2!$A$1+1` with `$A$1` selected | `f4-enter-mode.md` 4 |
| e5 | In D10: `=`, Ctrl+↓; then `=`, Ctrl+Shift+↓. Then the first behind 150 ms, typed as a burst | `=D1048576` in Point; `=D10:D1048576` | `f4-enter-mode.md` 5 |
| e6 | In D10: `=`, ↓; and `=`, ↓, ↓ | the Reference on a grey ground in the cell, none in the bar; the bar's selection `D11`, `D12` | `f4-enter-mode.md` 6 |
| e7 | `abc` in D10, then each of Shift+↑, Ctrl+←, Ctrl+↑, Ctrl+↓, Ctrl+Shift+←, Ctrl+Shift+↑, Ctrl+Shift+↓, each from a fresh entry | every one commits `abc`; the Selection as 18a's table | `f4-enter-mode.md` 7 |
| e8 | In D10: `=`, Shift+Home | `=A10:D10`, Point, outlined and dashed, the Reference on grey | `f4-enter-mode.md` 8 |
| e9 | C1 `1`, C2 empty; C1:C2 dragged two columns right. Then C1 `1`, C2 `5`, the same | D1:E1 `2 3`, row 2 empty; then `1 1` and `5 5` | `fill-handle-axis.md` 4; ADR-0050's note of 2026-10-14 |
| e10 | Columns B:C from their Headings, B1 `1`, C1 empty, dragged right to E | D1 `2`, E1 empty | `fill-handle-axis.md` 5 |
| e11 | Column C, C1 `5`, dragged left to A; column C, C1 `9/26/2026`, dragged right to E | B1 `4`, A1 `3`; D1 `9/27/2026`, E1 `9/28/2026` | `fill-handle-axis.md` 7 |
| e12 | R4's first selection with D4 filled yellow (the toolbar's Fill Colour), at the browser's 100% and 150%, read in device pixels across the active cell's four edges | a white device pixel between the border and the outline on the left and top; the Fill on the right and bottom | `focus-among-ranges.md` 2; ADR-0008's note of 2026-10-14 |
| e13 | R4's first selection, then F2 on D4 | no border on the edited cell; the area's line stays | `focus-among-ranges.md` 3 |
| e14 | A1 `123456789.12` in `0.00` showing `####` at a width nobody set; the toolbar's Font Colour pressed on its face; then, in a fresh Sheet, a colour chosen from its list. Then once more on a column whose width the user set | the column widens to show the number, in one undo step, both ways; a width the user set never widens | `widening.md` 5; ADR-0071, decided with the user 2026-10-14 |

Write each difference from 18a's reading, or between the hosts, on a line of its own, naming its ADR.

## The verified commit's tag

As in `verify-on-windows-17.md`, "The verified commit's tag" (ADR-0208; decided with the user, 2026-10-12):

- **Tag the verified commit once it is fetched, before the first Part**, and push the tag at once:

  ```sh
  git tag -a verified/<date>-windows-18 <verified commit> -m "The commit verification/<date>-windows-18/ verified"
  git push origin verified/<date>-windows-18
  ```

- The tag is `verified/` followed by the name of the record's directory under `verification/`, and nothing else. It
  is never moved. Parts made at another commit go in a directory of their own, as `…-windows-18b/`, tagged by its
  name, and the report says which Parts ran at which.
- The report names the tag beside the verified commit: "**Verified commit: `<sha>`** (head of `release/0.1`), tagged
  `verified/<date>-windows-18`".
- Part B's runs tag their own directories in the same way.

## Finishing

1. Commit everything to `claude/exsheet-windows-verify-18` and push, after each Part and once more at the end, with
   the tag pushed at the start.
2. The report's last section lists every difference: each by-hand row that is not a pass, each corpus case that
   disagrees, and each reading that did not match. Name each one's ticket, ADR paragraph or Definition of Done row.
3. It proposes nothing on the user's behalf.
4. The last message names the branch, the verified commit and the tag, says which Parts ran and which did not, and
   why, and lists the by-hand rows this run filed in `verification/by-hand.json` and those still without a record
   that holds at the verified commit, which `v0.1.0-rc.1` is to be tagged on with the records on top.
