# What to verify on a Windows machine

Status: done — run at 9a78a14 on 2026-09-27 (`verification/2026-09-27-windows-excel/`,
`verification/2026-09-27-windows/`). Part C was recorded as not run, as this page asks while its
tests are unwritten; DC-8 and DC-13 ran in the second run's Part D, and the IME cases in the
fifteenth run. What it settled is in ADR-0047 and ADR-0048. *(Set from the records on 2026-10-10.)*

This is written for a Claude Code session started on a Windows desktop that has **Excel, Chrome
and Edge**. The Linux container where ExSheet is being built has none of the three. Everything
below is something only such a machine can answer. Work through the parts in order, record what
you observe, and **decide nothing**: where Excel and the engine disagree, or a check fails, write
it down where each part says, and leave the decision to the user (`AGENTS.md`, "Decisions are
serial"). Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

Branch: `claude/exsheet-start-8cx3v1`. Commit what you record there, in English, and push.

## Setup

- `git clone` the repository and check out `claude/exsheet-start-8cx3v1`.
- **.NET SDK 10** (`winget install Microsoft.DotNet.SDK.10`). `nix` is not used on Windows; run
  `dotnet` directly wherever `AGENTS.md` writes `nix develop -c dotnet`.
- **Node 22 or later**, then `npm ci` in `tests/ExGrid.Browser`.
- **Google Chrome and Microsoft Edge**, installed normally: the suite drives the installed
  browsers, not downloaded ones (ADR-0026).
- **Excel desktop (Microsoft 365)**. Record its version and build (File → Account → About Excel),
  and Windows' display language and regional format.
- Sanity check: `dotnet build ExGrid.slnx` gives 0 warnings, and `dotnet test ExGrid.slnx` is green.

## Part A — Excel as the oracle

ADR-0047 admits a function only when it gives Excel's answer, and the engine was built without an
Excel to ask. Part A asks.

**How.** The engine's tests and Excel read **one corpus of cases**:
`tests/ExSheet.Engine.Tests/ExcelCases/*.json`, one file per area (arithmetic, errors, each function,
dates, number formats, typed constants, structure, fill, copy, Linked Tables, …). Each case says what
is entered, what is done, which cell is checked, and what is expected there as Excel's `Value2`,
`Text` or `Formula` would give it, with a `source`: `documented`, `observed` or `uncertain`.
`ExcelCaseTests` runs every case against the engine; **`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`
runs every case against Excel through COM.** Run the whole corpus with it, not only ticket 19:

```powershell
pwsh tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1           # all cases; -Area / -Id narrow it
```

- **The script has never been run.** It was written in a container without Excel. Expect to fix
  it on its first run; commit the fixes, and keep it simple (a fresh workbook per case, closed
  without saving, Excel quit at the end).
- **Formulas** go in through `Range.Formula2` (`Range.Formula` on an Excel without it), which takes
  the invariant (en-US) syntax the engine stores (ADR-0047). `Formula2` is used because
  `Range.Formula` applies Excel 2019's implicit intersection, which ADR-0047 rejected; the results
  file says which one was used.
- **Typed constants** go in through `Range.FormulaLocal`, which parses as the UI does under the
  machine's regional format. A case whose `culture` is not the machine's is reported `blocked`.
  Cases that need another culture (`de-DE`, `ja-JP`, `en-GB`) can only be answered by changing the
  Windows regional format. **Do not change it without asking the user.** If they agree, change it,
  run again with `-Area`/`-Id` for those cases, and change it back.
- A case the engine answers differently **by decision** (`engineDiffersByDecision` names the ADR:
  `#CIRC!`, `#GETTING_DATA`, spill refused, XLOOKUP's binary search refusals, fill patterns not yet
  taken, one Sheet, an undoable rename) is compared with its `excelExpect`, Excel's expected answer,
  or only recorded where there is none. Such a case does not fail the engine; it confirms Excel's
  side.
- A case with `oracleSkip` (an undo, a table still waiting for data, a qualifier naming no sheet)
  is reported `blocked` with its reason. Answer it by hand if it matters, and say so.
- A case that needs the mouse (the fill handle, what is selected afterwards) cannot be answered
  through COM's `AutoFill`, which selects nothing. Ask the user to do it by hand and describe
  what they see, and record it as observed by hand.

**The results.** The script writes `tests/ExSheet.Engine.Tests/ExcelOracle/results-<date>.json`:
per case, Excel's answer, what was expected, and `agree`, `disagree`, `blocked` or `recorded`, with
the Excel version and build and the machine's regional format. **Commit the results file.** Then
run it again with `-Update`, which rewrites `source` to `observed` for the agreeing cases only, and
commit the corpus. **Disagreements are listed for the user, never fixed on the spot**: not in the
engine, not in the case, and `-Update` leaves them exactly as they were. Whether the engine, the
case or an ADR changes is the user's decision.

**What.**

1. **Every case of the corpus**, as above. Ticket 19's rows
   ([`issues/19-verify-against-excel.md`](issues/19-verify-against-excel.md)) are in it with
   `source: "uncertain"` and a `ticket19Row`; fill in the ticket's "Excel's answer" column from the
   results file, and add the Excel build to its Comments.
2. **Formatting of inserted rows and columns.** Give row 2 a number format and a fill, then insert
   a row at 3 (`Rows(3).Insert()` with the default `CopyOrigin`). Does row 3 take row 2's format?
   Do the same for columns. The engine inserts them blank today. *(Since settled: an inserted row
   takes the formatting of the row above and a column that of the column to its left, as ADR-0046
   records, and the engine does so. Noted 2026-09-30.)*
3. **Inserting where a Reference would be pushed off the Sheet.**
   - Put a value in `A1048576` and `=A1048576` in `B1`, then insert a row at 1. Is it refused, and
     with what message?
   - Put `=SUM(A1:A1048576)` in `B1` and insert a row at 5. What does the Formula become?
   - The engine refuses the first today, and is unsure of both.
4. **Fill** (with `AutoFill`, `xlFillDefault`), reading the target's Values and Formulas:
   - a single number, a single date, a date with a time, `Item 1`;
   - two numbers (1, 3); three non-linear numbers (1, 2, 4), checking whether the trend matches the
     engine to the last digit;
   - a two-cell pattern filled **up** and filled **left**;
   - a source mixing a Formula, text and a blank.
5. **What is selected after a fill-handle drag**: source plus target, or the source alone? This is
   done by hand.
6. **A circular reference.** `A1: =B1`, `B1: =A1`. Record what each cell shows, and any status bar
   text, by hand.
7. **Spilled arrays.** `=A1:A3` in a cell, with values in `A1:A3`. Does it spill?
8. **Whitespace.** Set `Formula` to `= A1 + B1`, then read `Formula` back.
9. **XLOOKUP's binary search** (`search_mode` 2 and −2) over sorted data **with duplicate keys**:
   which of the equal rows is returned? Also try unsorted data, and record what Excel returns
   there, although the engine refuses it (ADR-0047).
10. **Pasting unreadable Formula text.** Copy the text `=1+` from Notepad and paste it onto a cell.
   Is it refused, or taken as text? This is done by hand.

Several of items 2–10 have cases in the corpus too (the push-off insertions, fill patterns, the
circular reference, spilling, whitespace, XLOOKUP's duplicated keys); the results file answers
those, and the items below ask what the corpus cannot express.

**Where to record.**

- `ExcelOracle/results-<date>.json`, for the corpus. List its disagreements in
  `verification/<date>-windows-excel/results.md` under "Disagreements" as well.
- Ticket 19's table, for its rows.
- `verification/<date>-windows-excel/results.md` for items 2–10. Give one section per item, with
  the inputs, what Excel did, and what the engine does today. The engine's current behaviour is
  in the tests under `tests/ExSheet.Engine.Tests`, and in the `## Comments` of tickets 13–16.

**Do not change the engine.** Where Excel disagrees, say so in `results.md` under a heading
"Disagreements", and name the ticket and the ADR it touches.

## Part B — layer 3 on Chrome and Edge, both hosts

The Linux container ran the existing browser suite on Chromium only, with no Edge. That was 202
passed on the WebAssembly host and 204 on the Server host, 0 failed. ADR-0017 needs both
browsers, and ADR-0026 runs them on the machine's own Windows scrollbars.

- From `tests/ExGrid.Browser`, follow `docs/definition-of-done.md` §22 Step 4 as it is written:
  `npx playwright test`, then `EXGRID_HOSTING=server npx playwright test`. Tee each into
  `verification/<date>-windows/`.
- Write `verification/<date>-windows/results.md` in the shape of
  `verification/2026-09-23-windows/results.md`: the environment, the counts per browser and host,
  and every failure with its criterion ID.
- This run covers **DC-1** (§26) on Windows: every declaration that ExSheet added to ExGrid is off
  on the existing pages, so the whole existing suite must pass unchanged. A failure here is a
  regression from the ExSheet work, and the most important thing this part can find.

## Part C — the new declarations in a real browser (only if their tests exist)

The browser tests for the new ExGrid declarations had **not been written** when this document was.
Those are: the Headings, the Formula Bar, the fill-handle drag, spilling paste, and Point mode. The
ExSheet DemoHost page had not been written either. **Do not improvise them here.**

- Check the tickets under `docs/specs/exsheet/issues/`. If ticket 18 (the browser suite) or the
  tickets it depends on are `done`, run the new specs as in Part B and record them in the same
  `results.md`.
- If they are not done, write "Part C: not run, tests not yet written" and stop. A check that did
  not happen is recorded as not run, never as passed (§1).

When they do exist, these are the criteria that only a real Windows desktop answers well:

- **DC-3**: the Row Headings stay at the left edge while scrolling sideways, over a classic
  scrollbar.
- **DC-13**: dragging the fill handle with the real mouse, including grabbing a corner that sits
  near the Pinned Columns.
- **DC-8**: a spilling paste from the real clipboard, copied from Excel itself.
- **DC-22**: real keys typed into the Formula Bar, on both hosts. Watch for the race recorded in
  the conversation that built it: keys typed straight after F2, before the round trip returns, may
  land at the cell editor's caret instead of the bar's. Report whether you can make it happen.
- **An IME** (Japanese input) in the Cell Editor and in the Formula Bar. `AGENTS.md` lists a real
  IME as a run by hand.

## Part D — Excel's behaviours, observed beside ExSheet

Keys, clicks and drags, driven by you on both sides: Excel through COM, real keys and the real
mouse, and ExSheet through Playwright. Follow
[`excel-behaviours.md`](excel-behaviours.md). It says how to warn the user before
sending input, which method to try first, and when to ask them instead. Record the results in
`verification/<date>-windows-excel/behaviours.md`, and commit the Playwright probes.

## Finishing

Commit `verification/<date>-windows-excel/`, `verification/<date>-windows/`, the oracle script
with any fixes its first run needed, its results file, the corpus as `-Update` left it, and the
ticket 19 table, then push. The last message to the user lists every disagreement and
every failure, each with the ticket or criterion it belongs to. It proposes nothing on the user's
behalf.
