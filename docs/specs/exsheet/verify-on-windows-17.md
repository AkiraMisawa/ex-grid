# What to verify on Windows, seventeenth run

Status: ready-for-human

*(Numbered seventeenth from `docs/agents/numbering.md`. It is the first half of the sign-off's Windows
run: ADR-0202's note of 2026-10-11 splits that run in two. This run asks Excel, and the platform,
everything whose answer does not depend on the build, before the release candidate, so that a
disagreement can be corrected before the tag. The eighteenth run, on `0.1.0-rc.1`, runs ExGrid's
by-hand rows on the candidate itself.)*

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-16.md`](verify-on-windows-16.md) and its report
(`verification/2026-10-02-windows-16/report.md`) first. Its method and tools apply unchanged:
- the probe, `ime-probe.mjs`, and `input-server.ps1`;
- Excel driven by `excel/ask-excel.ps1`;
- keys sent as virtual-key and scan codes;
- the hosts and the latency proxy.

**Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`. Change no code and no test: a disagreement is listed, never fixed on the
spot (`oracle.ps1`'s own rule). The one exception is the corpus: `-Update` rewrites `source` for the cases
that agree, as Part A says.

What only this desktop can answer:

| Part | What it asks | Settles |
|---|---|---|
| A | ExSheet's whole case corpus, put to Excel | SH-7; tickets 14, 16, 17 and 18's open boxes; ADR-0202 |
| B | What the corpus cannot ask: undo, rename and refresh of a draw; completion lists; the status bar | ADR-0180's three questions; ticket 16's popup texts; SM-7 |
| C | F4's edge cases, in Excel and in ExSheet | SH-28: the seventh run's Parts A and B, never run |
| D | KeyTips, Ctrl+F1 and F10, in Excel and in the browsers | SH-51, SH-52 (ticket 160) |
| E | ExPivot's 28 readings, beside Excel | §29, ExPivot ticket 07: twelve readings gate `0.1.0` |
| F | Where the keyboard is after ↓, under NVDA and Narrator | the Definition of Done's §21.11, "The Cell Editor's own ARIA semantics" |
| G | A dry run of ExGrid's by-hand rows | VZ-14, VZ-16, FN-12e and ED-30, before the eighteenth run makes them count |

The Parts are independent. **Run them in the order A, B, C, D, E, F, G.** Commit and push after each
one, so that a run stopped half way leaves every finished Part on the branch.

## Setup

- **Branches.**
  - Fetch **`claude/windows-run-17`** at the commit the request names, and record it as the verified
    commit. It is `main` with tickets 14 to 18 of `docs/specs/exsheet-functions` built on it
    (`claude/exsheet-functions-14-18`), and this document.
  - Branch **`claude/exsheet-windows-verify-17`** from it. If the tip moves during the run, do not merge
    it in.
- **Authorisation.** The request that hands this run over says whether the user has authorised it in
  advance. If it does not, ask before the first input.
  - What may be authorised in advance: real keys and mouse to Excel and the browsers, the IME switched
    on and off by its keys, and NVDA started and stopped.
  - **Ask the user every time, authorised or not:**
    - before changing Windows' regional format (`Set-Culture`; Part A). Put it back afterwards, and
      record both;
    - before starting Narrator (Part F). The sixteenth run could not stop it, and the user had to;
    - before changing the display scale (Part G);
    - whenever a case asks what the user hears.
  - Say "starting" before the first input and "finished" after the last.
- **Record the environment** once, at the start:
  - Windows' version and build;
  - Excel's version, build and update channel, and its display language;
  - Chrome's and Edge's versions;
  - the Microsoft IME's version;
  - NVDA's version;
  - the display scale and resolution;
  - the regional format (`Get-Culture`) and the keyboard layouts installed.
- **Excel.**
  - Before Part A, close every workbook and Excel itself. `oracle.ps1` opens its own.
  - With `-Keys`, nothing else may take the foreground, so tell the user before it starts.
  - No UI Automation between keys, as the sixteenth run had it.
- **The DemoHost** (Parts C, D, F and G): the pages are under `samples/ExGrid.DemoPages`.
  - Run it on WebAssembly and on the Server host, and on the Server host behind 150 ms (the latency
    proxy's `setRoundTrip`), as the sixteenth run did.
  - Use the ports `hosting.mjs` derives. No other checkout runs on this desktop.
  - Layers 1 and 2 need not be run.
- **Results.**
  - Everything goes under `verification/<date>-windows-17/`: the scripts, the records, the logs, the
    pictures, and one `report.md` with a row per case. Each row says what was read and, where this
    document gives a reading, whether it matches.
  - Part A's oracle writes `tests/ExSheet.Engine.Tests/ExcelOracle/results-<date>.json`. Commit that
    as well.

## Part A — the case corpus, put to Excel (ADR-0202, SH-7)

ADR-0202 counts the corpus at decision time:
- 264 cases are `source: "uncertain"`: the engine's own answer, never asked of Excel;
- 394 are `documented`: read from Microsoft's pages;
- tickets 14 to 18 add more, `LET-101` to `LET-112` among them.

Every one of them is asked here. Read `oracle.ps1`'s help (`Get-Help`, or the comment at its top)
before the first run. The equality run (`verify-equality-on-windows.md`) records one mistake: a case
that names no culture is en-US, and is blocked under any other regional format.

1. **Count first.** Count the corpus by `source` and by `culture`, per file, before anything runs. Put
   the counts in the report.
2. **Through COM, under en-US.**
   - If the regional format is not en-US, ask the user, then `Set-Culture en-US`.
   - Run `oracle.ps1` over the whole corpus (no `-Area`, no `-Id`).
3. **By keys.**
   - Run `oracle.ps1 -Keys` with `-Id` set to every case the COM pass blocked with "ask by keys". The
     `LET-KEYS` cases and the `LET-1xx` refusals are among them.
   - Excel is visible and in front, and the user is told beforehand.
4. **Each culture the corpus names.** For every other `culture` the corpus names:
   - ask the user, then `Set-Culture` to that culture;
   - run `-Id` with that culture's blocked cases, through COM and then `-Keys` as above.

   When every culture is done, put the user's regional format back, and record it.
5. **`-Update`.** Run `oracle.ps1 -Update` over the same cases, so that every case that agreed becomes
   `observed`. It never touches a case that disagreed, was blocked, or was recorded.
6. **The report.**
   - The counts after the run, by `source`, against the counts from step 1.
   - Every case still `uncertain` or `documented`, with why it is still blocked: `undo` cannot go
     through COM, a table still waiting has no Excel counterpart, and so on.
   - **Every disagreement on its own line:** the case id, its file, the Formula or the input, Excel's
     answer, and the corpus's `expect`.
     - Those of tickets 16 to 18 are named by id in each ticket's open box: `SUBTOTAL-020`,
       `SUBTOTAL-021`, `AGGREGATE-025`, `AGGREGATE-026`, `ERRORTYPE-011` to `ERRORTYPE-019`, and
       `INDIRECT-023` to `INDIRECT-029`.
     - `AGGREGATE-025` keeps Microsoft's 6 as its `excelExpect`. Record which way Excel goes.

Commit the corpus as `-Update` left it, and the results file.

## Part B — what the corpus cannot ask, by hand in Excel

Typed with real keys into a fresh workbook, as ADR-0180 and tickets 15 and 16 ask. Read each Value from
the cell after the step; a screenshot of each step goes to `shots/`.

| # | Steps | What to record | Asked by |
|---|---|---|---|
| b1 | A1 `=RAND()`, Enter. B1 `1`, Enter. Ctrl+Z. Ctrl+Y | A1 after each step: after Ctrl+Z, is it the draw before B1 was typed, the draw after it, or a new one? After Ctrl+Y, the same question | ADR-0180, "Undo draws again" (the corpus cannot undo through COM) |
| b2 | A1 `=RAND()`. Rename the sheet (double click its tab, type `Data`, Enter). Then Ctrl+Z, if Excel offers it | A1 after the rename, and after the undo if there was one | ADR-0180's first note of 2026-10-11 |
| b3 | A1 `=RAND()`. Make D1:E3 a Table. Data › From Table/Range, then Close & Load to a new sheet. On the first sheet, Data › Refresh All with the data unchanged; then change E2 and Refresh All again | A1 after each refresh: does a refresh that brings the same data draw again, and one that brings new data? | ADR-0180's second note of 2026-10-11 (a Linked Table's declaration and snapshot draw again) |
| b4 | Type `=SUBTOTAL(` and read the list Excel shows for `function_num`; Escape. The same for `=AGGREGATE(` (`function_num`), then `=AGGREGATE(1,` (`options`) | Every entry's text, in order, verbatim | ticket 16: confirm the completion texts, which are Microsoft's documented ones today |
| b5 | Right click the status bar and tick Average, Count, Numerical Count, Minimum, Maximum and Sum. A1 `1`, A2 `2`, A3 `=1/0`, A4 `x`, A5 left blank. Select A1:A5 | Each figure the status bar shows, verbatim | SM-7 (ADR-0130): "an error among the values leaves Count alone" |
| b6 | As b5, without A3's error. Select A1:A5 | The same | SM-7's control |

## Part C — F4's edge cases (SH-28)

The seventh run's Parts A and B (`verify-on-windows-7.md`), never run, as ADR-0202 asks:

- **Part A, in Excel:** its 22 cases, as written. Every F4 is a real key press sent to Excel's window,
  never `Range.Formula`. Read the text from the Formula Bar before the edit is committed, then Escape.
- **Part B, in ExSheet:** the same cases on `/sheet`, typed into a cell and again into the Formula Bar.
  - Run them in Chrome and Edge, on WebAssembly and on the Server host.
  - On the Server host behind 150 ms: case 1 as a burst, `=B2` then four F4 presses.
  - Record whether F4 with no edit open does anything.
- **Results:** where Excel's text differs from the reading in the seventh run's table, or ExSheet's from
  Excel's, write the difference on a line of its own, naming ADR-0051.
- Its Part C (a Heading reading) is not asked: the runs after the seventh settled it (ADR-0052).

## Part D — KeyTips, Ctrl+F1 and F10 (SH-51, SH-52, ticket 160)

Ticket 160's readings, and the build beside them:

| # | Where | Steps | What to record |
|---|---|---|---|
| d1 | Excel | Alt, then H; read every KeyTip of the Home tab; Escape twice | Every Home tab letter and what it runs, verbatim; the letters the Home tab leaves free |
| d2 | Excel | Ctrl+F1, twice | What it does (the ribbon collapses, then comes back) |
| d3 | `/sheet`, Chrome and Edge, WebAssembly and Server | A press on D10. Alt pressed and released alone; then F10 | Whether the browser's own menu opens, or its menu bar or address bar takes the keyboard; whether the Sheet shows its KeyTips; where DOM focus is (the probe) |
| d4 | as d3 | Alt, H, then each formatting letter the Sheet shows, one at a time, Escape between | What each letter runs on D10. Compare each letter with d1's: ExSheet uses Excel's Home tab letter where Excel has one (ADR-0100) |
| d5 | as d3 (`/sheet` binds `ShowToolbar`, so the toggle is bound) | Ctrl+F1, twice | Whether the key reaches the page and the toolbar hides and shows, or the browser takes it |
| d6 | as d3 | With an edit open (F2 on D10): Alt, then F10 | Nothing starts (SH-52); record where the keyboard is |

If Alt cannot be kept from the browser in either Chrome, record that as it is. Ticket 160 says that
KeyTips then start from F10 alone, and ADR-0100 records why; changing that is not this run's.

## Part E — ExPivot's 28 readings beside Excel (ticket 07)

Every item of `docs/specs/expivot/excel-behaviours.md`, beside Excel. ADR-0202:

- **The twelve readings that change a number gate `0.1.0`**: items 2 to 8 and 15 to 17. Run them first.
- **A difference in layout or wording** becomes a known limitation of `0.1.0`.

For each item:

1. **The same records in both.** Use `/pivot-csv`, whose records can be given as a file, or export a
   page's records. Open the same file in Excel and make a PivotTable from it. Record which file and
   which page.
2. **Build the item's layout in both.** Use the same fields, Areas, aggregation and options in each.
3. **Read both.** Record:
   - the item marked **observed** or **contradicted**;
   - Excel's cells, verbatim (Copy as values into the report, and a screenshot);
   - ExPivot's cells, from the page;
   - for the twelve, the report compared cell by cell (ticket 07's second box).
4. **Item 27, the Japanese edition's words**, needs Excel in Japanese. If the display language is not
   Japanese, ask the user whether to add it. If not, record the item as not run.

Results go to `verification/<date>-windows-17/expivot.md`, one row per item.

## Part F — the screen readers (the Definition of Done's §21.11)

What the sixteenth run heard may be a keyboard defect rather than wording. Under Narrator, ↓ was read as
"Scan", and DOM focus went to `body`. This Part checks where DOM focus is after ↓, with and without a
Focus. A defect found here is fixed before the tag. Wording is out of `0.1.0`'s scope.

- **The page.** `/sheet`, Chrome, WebAssembly; then Edge.
- **The two starting states:**
  - **without a Focus**: a fresh page, the keyboard reaching the grid by Tab from the control before it;
  - **with a Focus**: a press on D10 first.
- **The steps.** In each state: Tab (or the press), ↓, →, then ↓ ↓.
- **After every step,** the probe records:
  - `document.activeElement`: the Keyboard Field `input.ex-key-field`, the root, the Cell Editor, or
    `body`;
  - its `aria-activedescendant`;
  - the Focus cell.
- **NVDA:** start it. Record what it says from its Speech Viewer, as text, after every step. Stop it.
- **Narrator:** ask the user first.
  - The user starts it and listens. No copy command is sent: the sixteenth run's copy of the last
    phrase may itself have moved the keyboard.
  - After each step, the user says what they heard, and the session writes it down with the probe's
    readings.
  - The user stops Narrator.
- **Without a screen reader:** the same steps, as the control.

Expected of the build:
- DOM focus stays on the Keyboard Field, on this grid that edits (ADR-0080), at every step, with or
  without a Focus. It never goes to `body`.
- After ↓, the Focus moves and `aria-activedescendant` names its cell.

Record any step where that does not hold, under each reader and without one.

## Part G — a dry run of ExGrid's by-hand rows

These rows count only on the release candidate, in the eighteenth run. Running them here finds a defect
while it can still be fixed. Record each verdict, and the difference from the earlier runs.

- **VZ-14.** Run `tests/ExGrid.Browser/scrollbar.spec.mjs`'s "on a Windows desktop at fractional
  display scaling (VZ-14)" block at **150%**, headed, under both browsers, on both hosts, as the
  fifteenth run's Part A did.
- **VZ-16.**
  - At 100%, change Chrome's zoom (Ctrl+= and Ctrl+-) on `/sheet`. After each change, record
    `devicePixelRatio` and the `--ex-dp` the grid's root carries in its inline style, which is one
    Device Pixel in CSS pixels.
  - Ask the user to set the display scale to **225%**, if the display offers it. On `/sheet?case=11`,
    the page `device-pixel-225.spec.mjs` emulates, read a thin line and a gridline in device pixels
    from a screenshot at that scale, and record the widths. VZ-16 expects a thin line of one Device
    Pixel and a gridline of two.
- **FN-12e.** In Chrome and Edge, measure on this desktop the widest glyph of each `CellTextMetrics`
  class in Segoe UI and in the grid's face. Use a script that renders the glyphs and reads their widths.
  Record them beside ADR-0016's recorded table. Also check, on `/features`:
  - that a bold amount estimated to fit paints without an ellipsis;
  - that `####` fills its cell, with no room for one more `#`.
- **ED-30.** The sixteenth run's Part A, cases k1 to k16, on the verified commit, with the same keys
  and readings. Record only what reads differently from the sixteenth run's record.
- **The scale.** Put the display scale back as the user had it, and record it.

## Finishing

1. Commit everything to `claude/exsheet-windows-verify-17` and push, after each Part as above, and
   once more at the end.
2. The report's last section lists every difference: each disagreeing corpus case, and each reading
   that did not match. Name each one's ticket, ADR paragraph or Definition of Done row.
3. It proposes nothing on the user's behalf.
4. The last message names the branch and the commit, and says which Parts ran and which did not, and
   why.
