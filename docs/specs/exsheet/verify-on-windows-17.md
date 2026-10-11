# What to verify on Windows, seventeenth run

Status: ready-for-human

*(Numbered seventeenth from `docs/agents/numbering.md`. It is the first half of the sign-off's Windows
run: ADR-0202's note of 2026-10-11 splits that run in two. This run asks Excel, and the platform,
everything whose answer does not depend on the build, before the release candidate, so that a
disagreement can be corrected before the tag. Its Part G is the record of ExGrid's by-hand rows. The
eighteenth run, on `0.1.0-rc.1`, runs again only the rows whose code changes before then, and is not made
if none does (ADR-0208).)*

*(Revised on 2026-10-11, after a completeness check against the ADRs, the Definition of Done and the
tickets, while the run was under way. A session that started from `8436e45e` merges this revision's
commit: it changes no product code, only this document, the corpus and `oracle.ps1`'s `cellFormat`
action, so `8436e45e` stays the verified commit of Parts C to H. The changes are listed at the end.)*

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
| G | ExGrid's by-hand rows | VZ-14, VZ-16, FN-12e and ED-30: the record each holds until its code changes (ADR-0208) |
| H | What else waits on Excel or this desktop | UX-19 (ticket 24), SL-5, SH-36's completion texts, DC-33's real Excel clipboard, SH-45 and SH-46's Cell Format readings, the corpus cases `oracle.ps1` cannot ask |

The Parts are independent. **Run them in the order A, B, C, D, E, F, G, H.** Commit and push after each
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

ADR-0202 counted 264 `uncertain` and 394 `documented` cases on `main`. With tickets 14 to 18 and this
revision's cases, the run asks about 360 `uncertain` and 450 `documented` ones. Step 1's count is the
one that holds.

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
   - **Add ERRORTYPE-013 and ERRORTYPE-016 to ERRORTYPE-019** to that list. They type newer Error
     Values, which Excel reads only from the keyboard, but carry no "ask by keys" mark, so their COM
     answers are not Excel's real ones.
   - Rerun alone, with `-Id`, every case that raised a dialog during the full pass, so that the dialog's
     text is recorded. In earlier runs the watcher could not read any dialog text during a full pass.
   - Excel is visible and in front, and the user is told beforehand.
4. **Each culture the corpus names.** For every other `culture` the corpus names:
   - ask the user, then `Set-Culture` to that culture;
   - run `-Id` with that culture's blocked cases, through COM and then `-Keys` as above.

   When every culture is done, put the user's regional format back, and record it.
5. **`-Update` goes in the same call as each pass.** It reruns the pass, then rewrites to `observed`
   only the cases that agreed **in that call**, so a separate COM call cannot promote what only the keys
   pass, or another culture, answered.
   - Give `-Update` to the keys pass and to each culture's passes.
   - Rerun the en-US COM pass with `-Update` once, since it ran without it.
   - It never touches a case that disagreed, was blocked, or was recorded.
   - LVL-027 to LVL-029 use the action `cellFormat`, which `oracle.ps1` did not know (it knew `style`,
     the action's name before 2026-10-01). This revision teaches it the name. Run those three again
     after the merge.
6. **The report.**
   - The counts after the run, by `source`, against the counts from step 1.
   - Every case still `uncertain` or `documented`, with why it is still blocked: `undo` cannot go
     through COM, a table still waiting has no Excel counterpart, and so on.
   - **Every disagreement on its own line:** the case id, its file, the Formula or the input, Excel's
     answer, and the corpus's `expect`.
     - Those of tickets 16 to 18 are named by id in each ticket's open box: `SUBTOTAL-020`,
       `SUBTOTAL-021`, `AGGREGATE-025`, `AGGREGATE-026`, `ERRORTYPE-011` to `ERRORTYPE-019`, and
       `INDIRECT-023` to `INDIRECT-029`.
     - `AGGREGATE-025` and `INDIRECT-023` no longer carry an `excelExpect`. Excel's answer to each reads
       as a disagreement when it differs from the engine's refusal, which is intended.
     - Also list AGGREGATE-042 to AGGREGATE-054, SUBTOTAL-035 to SUBTOTAL-038 and the three carried
       since the first run, TABLE-011, TABLE-013 and COPY-025.

Commit the corpus as `-Update` left it, and the results file.

## Part B — what the corpus cannot ask, by hand in Excel

Typed with real keys into a fresh workbook, as ADR-0180 and tickets 15 and 16 ask. Read each Value from
the cell after the step; a screenshot of each step goes to `shots/`.

| # | Steps | What to record | Asked by |
|---|---|---|---|
| b1 | A1 `=RAND()`, Enter. B1 `1`, Enter. Ctrl+Z. Ctrl+Y | A1 after each step: after Ctrl+Z, is it the draw before B1 was typed, the draw after it, or a new one? After Ctrl+Y, the same question | ADR-0180, "Undo draws again" (the corpus cannot undo through COM) |
| b2 | A1 `=RAND()`. Rename the sheet (double click its tab, type `Data`, Enter). Then Ctrl+Z, if Excel offers it | A1 after the rename, and after the undo if there was one | ADR-0180's first note of 2026-10-11 |
| b3 | Save a CSV of three rows to disk. In a new workbook, A1 `=RAND()`. Data › From Text/CSV, the file, Load to a new sheet. Back on A1's sheet: Data › Refresh All with the file unchanged. Then edit the CSV in Notepad, save it, and Refresh All again | A1 after each refresh: does a refresh that brings the same data draw again, and one that brings new data? The source changes outside the workbook, so the second refresh is not also an edit of the workbook | ADR-0180's second note of 2026-10-11 (a Linked Table's declaration and snapshot draw again) |
| b4 | Type `=SUBTOTAL(` and read the list Excel shows for `function_num`; Escape. The same for `=AGGREGATE(` (`function_num`), then `=AGGREGATE(1,` (`options`) | Every entry's text, in order, verbatim | ticket 16: confirm the completion texts, which are Microsoft's documented ones today |
| b5 | Right click the status bar and tick Average, Count, Numerical Count, Minimum, Maximum and Sum. A1 `1`, A2 `2`, A3 `=1/0`, A4 `x`, A5 left blank. Select A1:A5 | Each figure the status bar shows, verbatim | SM-7 (ADR-0130): "an error among the values leaves Count alone" |
| b6 | As b5, without A3's error. Select A1:A5 | The same | SM-7's control |
| b7 | A1 `1`, A2 `TRUE`, A3 a date typed as `2026-10-11`, A4 `x`. Select A1:A4; then A1:A2 alone; then A3 alone | Each figure, verbatim, for each selection | ADR-0130's table: a Boolean counted but not numerically counted, a date counted |
| b8 | A1 `1000` formatted `#,##0.00`, A2 `2000` formatted `0%`. Select A1:A2; then A2:A1 from A2 | The Sum's and the Average's format, verbatim | SM-9, the mixed-format rule (ADR-0130) |
| b9 | The five corpus cases of undo, by real keys as `by-hand.ps1` typed them (`verification/2026-09-27-windows-excel-2/`): CW-013, CW-014, LVL-023, LVL-024 and LVL-030, from their files' `cells` and `actions` | Excel's answer for each, beside the case's `expect`. The orchestrator promotes those that agree; `oracle.ps1` cannot | the corpus (ADR-0208's check) |
| b10 | The five cases of a width widened by entry, typed: CW-021, CW-022, CW-023, CW-025 and CW-026 | The column's width after the entry, beside `widens` and `widthAtMost`/`widthAtLeast` | as b9 |

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
- **Its Part C, too** (`verify-on-windows-7.md`, "one Heading reading"). ADR-0052 still calls it read,
  not observed. In Excel and then on `/sheet` in Chrome and Edge: a click on the Column Heading C of an
  empty Sheet, then Ctrl+Shift+→. Record the Selection, the active cell, and whether the view moves
  sideways, down, or not at all.

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

Run d3, d4 and d6 again on `/sheet?chrome=mud`: ADR-0100 has the Chrome draw the letters under both
Chromes.

**SH-42: Ctrl+1 to Ctrl+5 on the build.** Their only record was taken on a standalone `keys.html`, never
on ExSheet (`verification/2026-10-01-windows-browser-11/keys.md`). On `/sheet`, with several tabs open
in the window, under Chrome and Edge and on both hosts, press each of Ctrl+1 to Ctrl+5 on D10. Record
whether the Sheet acts (as Excel's formatting keys) and whether the browser switches tab. Reuse that
run's `keys-probe.mjs` and `browser-input.ps1`.

If Alt cannot be kept from the browser in either Chrome, record that as it is. Ticket 160 says that
KeyTips then start from F10 alone, and ADR-0100 records why; changing that is not this run's.

## Part E — ExPivot's 28 readings beside Excel (ticket 07)

Every item of `docs/specs/expivot/excel-behaviours.md`, beside Excel. ADR-0202:

- **The twelve readings that change a number gate `0.1.0`**: items 2, 3, 4, 7, 8, 15, 16 and 17, and
  both halves of items 5 and 6 (each holds two readings). Run them first.
- **A difference in layout or wording** becomes a known limitation of `0.1.0`.

For each item:

1. **The same records in both.** Use `/pivot-csv`, which reads a CSV file the user brings. Write a CSV
   for each item that holds what the item needs (text differing only in case, blanks, one number, no
   numbers, a hidden item, fifteen digits). Give it to `/pivot-csv`, and open the same file in Excel to
   make a PivotTable from it. No page exports its records. Record each file.
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

## Part G — ExGrid's by-hand rows

This Part is the record of these rows, made at the verified commit. It holds until a commit changes the
code a row covers (ADR-0208). Record each row's verdict against its criterion as written in the
Definition of Done, exactly as a sign-off would, and the difference from the earlier runs. A row that
cannot be run here is `not run`, with the reason, never a pass.

- **VZ-14.** Run `tests/ExGrid.Browser/scrollbar.spec.mjs`'s "on a Windows desktop at fractional
  display scaling (VZ-14)" block at **150%**, headed, under both browsers, on both hosts, as the
  fifteenth run's Part A did.
- **VZ-16.**
  - Change the browser's zoom on a display-only page such as `/wide`, never on `/sheet`: ExSheet claims
    Ctrl+- and Ctrl++ for its structure keys (ADR-0050). Use the browser's menu, or Ctrl+= and Ctrl+-
    with the keyboard outside any Sheet. After each change, record `devicePixelRatio` and the `--ex-dp`
    the grid's root carries in its inline style.
  - **A real 2.25.** Do not change the display scale: that needs a sign-out, which ends WSL and this
    session. At the display's 150%, set the browser's zoom to 150% with real keys, so that
    `devicePixelRatio` is 2.25, as the eleventh run's Part C did
    (`verification/2026-10-02-windows-excel-11c/beside-excel.md`). On `/sheet?case=11`, the page
    `device-pixel-225.spec.mjs` emulates, read a thin line and a gridline in device pixels from a
    screenshot, and record the widths. VZ-16 expects a thin line of one Device Pixel and a gridline of
    two.
- **FN-12e.** In Chrome and Edge, measure on this desktop the widest glyph of each `CellTextMetrics`
  class in Segoe UI and in the grid's face. Use a script that renders the glyphs and reads their widths.
  Record them beside ADR-0016's recorded table: macOS and Linux are recorded there already, and only
  Windows is missing (`tests/GlyphWidths/README.md`). `system-ui` is Segoe UI only under an English
  display language; under a Japanese one, Chrome resolves it to Yu Gothic UI. Record the display
  language. Also check, on `/sizing` and `/sheet?case=3c` (where `sizing.spec.mjs` and
  `part-c.spec.mjs` read them):
  - that a bold amount estimated to fit paints without an ellipsis;
  - that `####` fills its cell, with no room for one more `#`.
- **ED-30.** The sixteenth run's Part A, cases k1 to k16, on the verified commit, with the same keys
  and readings. **Give every case a verdict row**, since this Part is the record; mark what reads
  differently from the sixteenth run.
  - Add one case for DC-47 (ticket 29): in D10, `=A1+`, IME on, `kana`, Space, Enter, and no further
    key. Read whether A1's Reference colour comes back and the coloured layer shows. The fifteenth
    run's i5 covered only a composition ended by Escape.
- **The scale.** Put the display scale back as the user had it, and record it.

## Part H — what else waits on Excel or this desktop

| # | What | Where | What to record | Settles |
|---|---|---|---|---|
| h1 | A1:C5 dragged | Excel, and `/sheet` beside it | Any outline around A1 besides the range's outline and the untinted cell. A screenshot of each | UX-19, ticket 24 question 1 (ADR-0008, "read, not observed") |
| h2 | A1:B2, then Ctrl+click D4:E6 | as h1 | Which range carries an outline, if any; whether the active cell carries a border; where the fill handle is, if any | ticket 24 question 2 |
| h3 | A whole column, then a whole row, selected from the Headings | as h1 | As h1 | ticket 24 question 3 |
| h4 | A1:B2, Ctrl+click F6, Ctrl+click F6 again; then Enter three times | Excel | The Selection and the active cell after the take-out (the reading: A1), then the cell after each Enter | SL-5 (ADR-0052, "read, not observed") |
| h5 | Type `=VLOOKUP(1,A1:B2,2,` and read the list; Escape. The same for `=HLOOKUP(1,A1:B2,2,`, `=MATCH(1,A1:A2,`, `=YEARFRAC(A1,A2,`, `=XMATCH(1,A1:A2,` and `=XMATCH(1,A1:A2,0,`; and the list a Boolean argument offers (`=VLOOKUP(1,A1:B2,2,` is one) | Excel, English | Every entry's text, in order, verbatim. Compare with `src/ExSheet.Engine/Formulas/Functions.cs` | SH-36 (tickets 04, 09, 11) |
| h6 | A cell copied in Excel and pasted into `/sheet`, and a cell copied in `/sheet` and pasted into Excel, both directions, with `clipboard-probe.mjs` (`verification/2026-09-27-windows-excel-2/`) | Excel, Chrome and Edge, both hosts | What lands on each side, as the second and third runs recorded it | DC-33, ticket 14 (a real Excel copy, which CI cannot make) |
| h7 | Ctrl+I, Ctrl+U, and Format Cells' Font tab, each over a number showing `####` | Excel | Whether the column widens | SH-45 (ADR-0071's readings) |
| h8 | Strikethrough alone over a number showing `####` | Excel | Whether the column widens | ticket 100 |
| h9 | Under en-GB (with the user's consent, as Part A): typed `d-mmm-yy`, `m/d/yyyy` and `h:mm` in Format Cells' Custom box; under ja-JP: `mmmmm` | Excel | What each shows on a date and a time | ADR-0071, ticket 103 |
| h10 | Format Cells' Border tab: take a shown top edge away; and where both cells record an outer edge, which one the dialog shows | Excel | What each cell keeps, and what the dialog showed | ticket 102 |
| h11 | Two filled cells side by side, and dashes over a covered gridline, as the DemoHost's case `fills` | Excel, beside `/sheet?case=fills` | Screenshots at 100% and 150% zoom | SH-46, ticket 99 |
| h12 | The Number Formats `@;0` and `0;@;0` on a number and on text | Excel | What each shows | ticket 89 |

## What this revision changed

- Part A: the counts, ERRORTYPE-013 and 016 to 019 by keys, `-Update` in each pass's own call,
  `cellFormat`, and the cases listed.
- Part B: b3 refreshes a CSV changed outside the workbook; b7 to b10 added.
- Part C: the seventh run's Part C is asked, since it was never settled.
- Part D: the MudBlazor Chrome, and SH-42 on the build.
- Part E: the twelve counted by halves; the records given as CSV files.
- Part G: zoom off `/sheet`, 2.25 by the browser's zoom at 150%, FN-12e's pages and display language,
  ED-30's verdict rows and DC-47's case.
- Part H added.

## Finishing

1. Commit everything to `claude/exsheet-windows-verify-17` and push, after each Part as above, and
   once more at the end.
2. The report's last section lists every difference: each disagreeing corpus case, and each reading
   that did not match. Name each one's ticket, ADR paragraph or Definition of Done row.
3. It proposes nothing on the user's behalf.
4. The last message names the branch and the commit, and says which Parts ran and which did not, and
   why.
