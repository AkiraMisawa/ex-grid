# Run 17 C-F, ARIA's row indices and the screen readers' passes: what the orchestrator writes

Branch `claude/run17cf-aria-rows`, from `main` at `a533de01`, with `origin/main` at `2265cda6` merged in. It carries two
things from the seventeenth Windows run's Part F (`verification/2026-10-11-windows-17/report.md`, on
`claude/exsheet-windows-verify-17`):

- **Decision (A) of 2026-10-12, taken with the user, after the NVDA reading.** Near the top of `/sheet` NVDA named D10's
  row `row 11` and A1's `row 2`, while `aria-rowindex` said 10 and 1. A reader counts the rows painted before a row near
  the top, the header row among them, and reads `aria-rowindex` far down, where the DOM holds a Window; the header row
  carried no index of its own, so the two numbers for one row differed by one. ExGrid's row indices are now ARIA's: the
  header's rows are counted first. Built on the branch; edits 1 to 3, 5, 6, 8, 9 and 11 to 13 below.
- **The decisions of 2026-10-12 on the Narrator and NVDA passes**, as the requester relays them: Narrator's scan mode
  taking the arrows is not a keyboard defect (with scan mode off every arrow reached the grid, and Narrator turns scan
  mode on again with each new page), and NVDA's silence on the first pass comes from the probe's scripted focus before
  any user input, and is recorded only. Commits `76bd2116` (Narrator) and `2fc23f56` (NVDA's silence asked three ways)
  on `claude/exsheet-windows-verify-17`. Nothing in the product changes; edits 4, 7, 10 and 11 below.

Nothing here edits an ADR, `CONTEXT.md`, the Definition of Done or the Docs Site's pages on the branch: their text is the
numbered list below, in the repository's voice.

## What was done

- **The header row is `aria-rowindex="1"`** (`src/ExGrid/Components/ExGrid.razor`). Untiered, the header is the one
  `role="row"` element it was, with the index added.
- **Under Header Groups the header is a `rowgroup`** holding a row per tier, top first (`aria-rowindex` 1 to T), then the
  leaf row (`T + 1`). Each is a `display: contents` element, written inline, so the band lays out, paints and stacks
  exactly as the one element of cells and rectangles did (ADR-0032): the leaf cells stay the header's flex items, the
  rectangles stay positioned against it, and the z-indices are unchanged. The pinned rectangles' sticky anchor stands in
  each tier's row that has one. A rectangle carries `aria-colindex` (its first member's; the band's row holds no cell at
  the columns no tier covers, so its cells' positions among siblings would be wrong), `aria-colspan` as before (A11Y-13),
  and `aria-rowspan` where it spans more than one tier. Leaf headers are written once, by a method both shapes call.
  This structure is the branch's choice, made while building; the user decided that the header's rows are counted.
- **Every data row and Placeholder counts after the header's rows**: `ExGridRow` takes `HeaderRowCount` (a new
  parameter, compared in `ShouldRender`, constant while the tiers are) and writes `HeaderRowCount + RowIndex + 1`; the
  root's Placeholders the same. Cell ids are unchanged (`…r412c3` stays the 0-based position). `aria-rowcount` is the
  data rows' total plus the header's rows; with `HideHeader` it is the total alone and the first row is 1.
- **ExSheet and ExPivot inherit it through ExGrid.** ExSheet's Column Headings are the header row, so A1's row is 2 and
  `aria-rowcount` is 1,048,577; with `ShowColumnHeadings` off, A1's row is 1 and the count 1,048,576. ExPivot's report
  counts a row per tier of its column Items' Header Groups before its header row. Saying the Sheet's own row number
  (`aria-rowindextext` or rowheader cells) is decided after the next Windows run reads support, as the user decided.

Tests, each named with its ADR:

- Layer 2, `tests/ExGrid.Components/AccessibilityTests.cs` (a million rows, 100 columns): the count with the header row
  (A11Y-2, `1000001`); at the top the header row 1 and the data rows from 2; far down at row 500,000 and column 30 the first
  painted row `500002`, Placeholders included, and the cell `r500000c30` at `aria-colindex` 31 (A11Y-3); a Header Group
  band as row 1 and the header row 2, data from 3, `500003` far down; two tiers as rows 1 to 3 top first, a two-tier
  rectangle with `aria-rowspan="2"`; a band declared later moving every painted row's index; the header hidden: no header
  row, `1000000`, first row 1, `500001` far down. `tests/ExSheet.Components/SheetRenderingTests.cs`: the Column Headings
  as row 1, A1 as row 2 and every painted row its Row Heading plus one, at the top and at A500000, and without the
  Column Headings the two equal. `tests/ExPivot.Components/PivotRenderingTests.cs`: a report's header row 1 and rows from
  2; with two column fields the column Items' row 1, the header row 2, rows from 3. Every other test that read a row's
  position from `aria-rowindex` now subtracts the header's rows (`tests/ExGrid.Components/Support/AriaRows.cs`,
  `SheetTestContext.SheetRowOf`, `PivotTestContext.HeaderRowsOf`).
- Layer 3, `tests/ExGrid.Browser/aria-rows.spec.mjs` (new): Chromium's accessibility tree, over the DevTools protocol,
  on `/features` (a Header Group band, then the second, untiered grid) and `/sheet`: each header row a row of the tree
  owning its column headers, in order, none ignored; the data rows' indices consecutive after them, near the top and
  after a scroll far down (row 300 of `/features`, A500000 of `/sheet`); D10's row 11. The specs that read
  `aria-rowcount` as a row total or `aria-rowindex` as a position read them through `fixtures.mjs`' new `dataRowCount`
  and `headerRowsOf` (`scrollRowToTop` included).

### Corrected in the third round (2026-10-12)

- **`tests/ExGrid.Components/PointedStepTests.cs`'s `PaintedRows` still read a row's position as `aria-rowindex - 1`**,
  one more than the position since the header row is counted. Its `DoesNotContain(0, …)` checks could not fail, and the
  others tested the row above the one they named. It reads `AriaRows.PositionOf` now. Each of its checks was shown to
  fail when its premise is broken, one mutation of the test at a time, run with the fixed helper and with the old one
  (`docs/agent-runs/run17cf-aria-rows/mutate_pointed_step.py`, git-ignored):

  | Check | Mutation | Fixed helper | Old helper |
  |---|---|---|---|
  | `DoesNotContain(30)`, finding the row beyond the painted rows | row 30 scrolled to the top first | fails at the check | passes |
  | `DoesNotContain(0)`, down from a column after revealing row 30 | the reveal left out | fails at the check | passes |
  | `DoesNotContain(20)`, before revealing row 20 | row 20 scrolled to the top first | fails at the check | passes the check |
  | `Contains(20)`, after revealing row 20 | row 18 revealed instead (rows 10 to 19 painted) | fails at the check | passes the check |
  | `DoesNotContain(12)`, under `PageSize` 5 before the reveal | `PageSize` 6, the page of rows 12 to 17 shown first | fails at the check | passes the check |
  | `Contains(12)`, after the page turns | `PageSize` 6, row 11 revealed (rows 6 to 11) | fails at the check | passes |
  | `DoesNotContain(0)`, after the page turns | `PageSize` 13, the grid grown to 300 px before the reveal: nothing turns | fails at the check | passes |

  "Passes the check" means the old helper let that check through, and the test failed at a later assertion. With no
  mutation, the class's 19 tests pass with either helper.
- **`aria-rows.spec.mjs` on `/features` compared the count with itself**: `dataRowCount` is `aria-rowcount` less the
  header's rows, so `aria-rowcount == dataRowCount + 2` held for any count. It now reads `/features`' known totals,
  Features.razor's 400 trades (`402`) and the second grid's 20 (`21`), and checks `dataRowCount` against them. After the
  scroll far down it checks that each header row is the row it was at the top (its index, the column headers it owns,
  no cells, not ignored), and that every data row holds cells and no column header, on `/features` and `/sheet`.
- No other `aria-rowindex` arithmetic is left in the test tree. The archived harness copies under `verification/`
  (`2026-10-06-macos-*/harness/*.txt`, `2026-10-10-linux-merged-live-costs/harness/profile-question.cjs.txt`) read
  `aria-rowcount`, as records of runs already made; they are not edited.

## The edits, in order

Each block is whole lines with the target's indentation. Each old text occurs once in its file at the branch's tip, and
the list applies in order (checked by `docs/agent-runs/run17cf-aria-rows/check_proposal.py` in the branch's worktree,
which is git-ignored; its output is at the end). Edits marked *(optional)* may be left out; no other edit depends on
them.

### 1. `docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md`

The table's row for the counts.

Old:

````text
| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |
````

New:

````text
| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM; the row count holds the header's rows as well as the data rows *(2026-10-12, below)* | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |
````

### 2. `docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md`

The table's row for the indices.

Old:

````text
| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |
````

New:

````text
| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell, header rows and Header Group cells included; a row's index counts the header's rows before it *(2026-10-12, below)* | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |
````

### 3. `docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md`

The example under "So the root does the talking".

Old:

````text
root   role="grid"  aria-multiselectable="true"
       aria-rowcount="1000000"  aria-colcount="40"
       aria-activedescendant="ex-7-r412-c3"
row    aria-rowindex="413"
cell   aria-colindex="4"  id="ex-7-r412-c3"
````

New:

````text
root   role="grid"  aria-multiselectable="true"
       aria-rowcount="1000001"  aria-colcount="40"
       aria-activedescendant="ex-7-r412-c3"
header aria-rowindex="1"
row    aria-rowindex="414"
cell   aria-colindex="4"  id="ex-7-r412-c3"
````

### 4. `docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md` *(optional)*

The note on the sixteenth run's Narrator try, in "On a grid that edits, the tab stop and `aria-activedescendant` are
the Keyboard Field's": a pointer to what the seventeenth run settled.

Old:

````text
  may have caused that. Not settled; the real screen reader stays owed, as the user chose. ADR-0080.)*
````

New:

````text
  may have caused that. Not settled; the real screen reader stays owed, as the user chose. ADR-0080.)*
  *(2026-10-12, decided with the user after the seventeenth Windows run: Narrator's scan mode taking
  the arrows is not a keyboard defect, and NVDA's silence on that run's first pass is recorded only.
  ADR-0080 has what each reader said.)*
````

### 5. `docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md`

A section appended at the end of the ADR, after "Added after the fifth Windows run".

Old:

````text
refusal stays where it is. A Focus move that changes no Selection still writes nothing (A11Y-10).
````

New:

````text
refusal stays where it is. A Focus move that changes no Selection still writes nothing (A11Y-10).

## Added after the seventeenth Windows run: the header's rows are counted *(2026-10-12)*

The indices above counted the data rows alone: the first data row was `aria-rowindex="1"`, and the
header row carried no index. A reader does not read one number for a row everywhere. Near the top it
counts the rows before a row, the header row among them; far down, where the DOM holds a Window, it
has only `aria-rowindex`. The seventeenth Windows run read NVDA on `/sheet`
(`verification/2026-10-11-windows-17/report.md`, Part F): D10's row was `row 11` and A1's `row 2`,
while the attribute said 10 and 1. The same row would have had two numbers, one at the top and one a
scroll away.

*(2026-10-12, decided with the user: **ARIA's row indices count the header's rows first.** **Saying
the Sheet's own row number** — `aria-rowindextext`, or a Row Heading as a `rowheader` cell — **is
decided after the next Windows run** reads what NVDA and Narrator do with each.)*

- The header row is `aria-rowindex="1"`. A data row's `aria-rowindex` is its position plus the
  header's rows plus one; `aria-rowcount` is the data rows' total plus the header's rows. A hidden
  header ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)) has no rows, and the first data row
  is 1 again.
- Cell ids are unchanged: `ex-7-r412-c3` still names the row at position 412, now
  `aria-rowindex="414"`.
- ExSheet and ExPivot inherit it. Under ExSheet's Column Headings, A1 is row 2, so a reader names a
  Sheet row by its number plus one, at the top and far down alike.
- *(Found while building, 2026-10-12.)* Under Header Groups
  ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)) each tier is a row
  of its own, top first, before the header row, and the header is a `rowgroup` holding them. Each of
  those rows is `display: contents`, so the band lays out, paints and stacks as the one element did. A
  rectangle carries `aria-colindex` (its first member's), `aria-colspan`, and `aria-rowspan` where it
  spans more than one tier.

Chromium's accessibility tree holds each header row as a row owning its column headers,
`display: contents` included, at the top and after a scroll far down (layer 3, over the DevTools
protocol). What a reader says is still the Windows run's.
````

### 6. `docs/adr/0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md` *(optional)*

One line, so that a reader finds where the band's rows went.

Old:

````text
- **ARIA** (`aria-colspan` on group cells) lands with the structural surface of
````

New:

````text
- **ARIA** (`aria-colspan` on group cells, and since 2026-10-12 each tier a row of its own) lands with the structural surface of
````

### 7. `docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md`

The screen-reader bullet of "What the sixteenth Windows run settled": what the seventeenth run heard, and the decision.

Old:

````text
- **A screen reader: not settled, and left owed, as the user chose.** With Narrator (NVDA is not
  installed there), Tab into `/sheet` read "Enter Table, 1048576 by 16384, edit, Scan Off", and ↓ read
  "Scan" while DOM focus went to `body`. The run's method sent Narrator's copy command after each step
  to record the speech, and may have played a part. What a screen reader says stays owed, as ADR-0033
  records.
````

New:

````text
- **A screen reader: not settled, and left owed, as the user chose.** With Narrator (NVDA is not
  installed there), Tab into `/sheet` read "Enter Table, 1048576 by 16384, edit, Scan Off", and ↓ read
  "Scan" while DOM focus went to `body`. The run's method sent Narrator's copy command after each step
  to record the speech, and may have played a part. What a screen reader says stays owed, as ADR-0033
  records.
  *(2026-10-12, the seventeenth Windows run, Part F: `verification/2026-10-11-windows-17/report.md`,
  commits 76bd2116 and 2fc23f56 on `claude/exsheet-windows-verify-17`. `/sheet` on the WebAssembly
  host, in Chrome and Edge, by real keys: ↓ → ↓ ↓ after two Tabs with no Focus, and after a press on
  D10. Without a reader and under NVDA 2026.2, DOM focus stayed on the Keyboard Field at every step,
  and the Focus moved. NVDA, as its Speech Viewer recorded, read the press on D10 as `table` /
  `row 11  D  column 4` and the next ↓ as `row 12  D  column 4`. A pass whose first step was the
  probe's scripted focus on the Revalue button said nothing from that focus to its last ↓, only the
  page's load before it (`ExGrid demo host` / `Loading…`); one that began with a real press, on the
  heading's empty right end or on the button, read every step (`Item  row 2  A  column 1` after the
  first ↓). Narrator 10.0.26100.8972, heard from the playback device, starts in scan mode, and there
  no arrow reached the page: DOM focus left the field for `body` at the first ↓, and after the press
  on D10, ↓ read `column header cap E, column 5 of 16384.`, Narrator's own cursor on the column
  headers. With scan mode turned off by Insert+Space (Narrator said "Scan off"), every arrow's
  keydown reached the field, which kept DOM focus, and Narrator read
  `Item, column header cap A, row 2 of 1,048,576, column 1` after the first ↓ and
  `Row 12 of 1,048,576.` after ↓ from D10. As the second page loaded, Narrator said "Scan" again.)*
  *(2026-10-12, decided with the user: **Narrator's scan mode taking the arrows is not a keyboard
  defect.** With scan mode off every arrow reached the grid, and Narrator turns scan mode on again
  with each new page. **NVDA's silence on the first pass comes from the probe's scripted focus before
  any user input, and is recorded only.**)*
````

### 8. `docs/definition-of-done.md`

A11Y-2, the whole row.

Old:

````text
| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted | `aria-rowcount="1000000"` |
````

New:

````text
| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice; the row count holds the header's rows as well as the data rows (ADR-0033) | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted, under one header row, a Header Group band, and a hidden header; Layer 3 on `/features` and `/sheet` | `aria-rowcount="1000001"` under one header row, one more per tier of Header Groups, `1000000` with the header hidden; on `/features`, `402` over the first grid's 400 rows under a Header Group band and `21` over the second grid's 20; on `/sheet`, `1048577` *(2026-10-12, decided with the user: ARIA's row indices count the header's rows first, after the seventeenth Windows run's NVDA reading; ADR-0033. Until then the pass value read `aria-rowcount="1000000"`.)* |
````

### 9. `docs/definition-of-done.md`

A11Y-3, the whole row.

Old:

````text
| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes | Layer 2 scrolled to row 500,000 and column 30 | first painted row's index is its absolute one, not 1 |
````

New:

````text
| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes, and a row's index counts the header's rows before it: the header row is 1, each tier of Header Groups a row of its own before it (ADR-0033) | Layer 2 scrolled to row 500,000 and column 30, under one header row, a Header Group band, and a hidden header; Layer 3 over the DevTools protocol on `/features` and `/sheet`, at the top and after a scroll far down | the first painted row's index is its position plus the header's rows plus one — `500002` under one header row, `500003` under one tier, `500001` with the header hidden — and the cell at column 30 is `aria-colindex="31"`; in Chromium's accessibility tree each header row is a row owning its column headers, at the top and far down *(2026-10-12, decided with the user: ARIA's row indices count the header's rows first, after the seventeenth Windows run's NVDA reading; ADR-0033. Until then the pass read "first painted row's index is its absolute one, not 1".)* |
````

### 10. `docs/definition-of-done.md`

§21.11, the row on the Cell Editor's own ARIA semantics, the whole row. The table has no Verification column; the row
keeps its two cells.

Old:

````text
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)). *(2026-10-11, decided with the user: **the richer contract is accepted in writing as out of `0.1.0`'s scope.** What the sixteenth Windows run heard — ↓ read as "Scan", DOM focus on `body` — may be a keyboard defect rather than wording, so the sign-off's Windows run checks where DOM focus is after ↓, with and without a Focus, under NVDA (recorded through its Speech Viewer) and Narrator (heard, with no copy command sent); a defect is fixed before the tag.)* |
````

New:

````text
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)). *(2026-10-11, decided with the user: **the richer contract is accepted in writing as out of `0.1.0`'s scope.** What the sixteenth Windows run heard — ↓ read as "Scan", DOM focus on `body` — may be a keyboard defect rather than wording, so the sign-off's Windows run checks where DOM focus is after ↓, with and without a Focus, under NVDA (recorded through its Speech Viewer) and Narrator (heard, with no copy command sent); a defect is fixed before the tag.)* *(2026-10-12, the seventeenth Windows run, Part F, `/sheet` in Chrome and Edge ([ADR-0080](adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md); commits 76bd2116 and 2fc23f56 on `claude/exsheet-windows-verify-17`): after every ↓, with and without a Focus, DOM focus stayed on the Keyboard Field without a reader and under NVDA 2026.2, which read the press on D10 as `table` / `row 11  D  column 4` and the next ↓ as `row 12  D  column 4`. A pass whose first step was the probe's scripted focus said nothing from that focus to its last ↓, only the page's load before it (`ExGrid demo host` / `Loading…`); one that began with a real press read every step. Narrator, in the scan mode it starts in, let no arrow reach the page: DOM focus went to `body` at the first ↓, and ↓ from D10 read `column header cap E, column 5 of 16384.`. With scan mode off (Insert+Space, "Scan off"), every arrow reached the field, and Narrator read `Item, column header cap A, row 2 of 1,048,576, column 1` after the first ↓ and `Row 12 of 1,048,576.` after ↓ from D10.)* *(2026-10-12, decided with the user: **Narrator's scan mode taking the arrows is not a keyboard defect** — with scan mode off every arrow reached the grid, and Narrator turns scan mode on again with each new page; **NVDA's silence on the first pass comes from the probe's scripted focus before any user input, and is recorded only.**)* |
````

### 11. `samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor`

"The keyboard" (the ExGrid Change Note's `Docs:` link, `exgrid/selection#the-keyboard`): Narrator's scan mode, then the
rows as a reader counts them.

Old:

````text
        <p>Click into the grid, or tab to it: the grid is one tab stop, and its keys are Excel's.</p>
````

New:

````text
        <p>Click into the grid, or tab to it: the grid is one tab stop, and its keys are Excel's.</p>
        <p>
            With Narrator, turn scan mode off before you use the grid's keys, as for any web grid: Narrator's key
            and Space, Caps Lock+Space or Insert+Space. In scan mode the arrow keys move Narrator's own reading
            cursor through the page and never reach the grid. Narrator turns scan mode on again on each new page.
        </p>
        <p>
            A screen reader reads the grid as a table. The header row is its row 1 and the first row of data its row 2,
            so a reader says the same number for a row near the top, where it counts the rows before it, and a million
            rows down, where it reads the number the grid writes. Each tier of Header Groups is a row of its own above
            the header row, and the rows of data are counted after them; with <code>HideHeader</code>, the first row of
            data is row 1.
        </p>
````

### 12. `samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor`

"A first sheet" (the ExSheet Change Note's `Docs:` link, `exsheet#a-first-sheet`).

Old:

````text
            <li><code>ShowRowHeadings</code>, <code>ShowColumnHeadings</code> and <code>ShowFormulaBar</code> hide the Headings or the Formula Bar; the cells are still addressed A1.</li>
````

New:

````text
            <li><code>ShowRowHeadings</code>, <code>ShowColumnHeadings</code> and <code>ShowFormulaBar</code> hide the Headings or the Formula Bar; the cells are still addressed A1.</li>
            <li>A screen reader counts the Column Headings as the table's row 1, so it names a Sheet row by its number plus one: A1 is in row 2, near the top and far down. With <code>ShowColumnHeadings</code> off, the two numbers agree.</li>
````

### 13. `samples/ExGrid.Docs/Pages/Pivot/PivotOverview.razor`

"The parts of a pivot".

Old:

````text
            the Value Fields' Aggregations are computed from.
````

New:

````text
            the Value Fields' Aggregations are computed from. A screen reader counts the report's header rows before its
            first row: a row for each tier of column Items, then the row of column headers.
````

## Change Notes

- `docs/releases/unreleased/run17cf-aria-rows-header-rows-counted.md` (ExGrid, Behaviour, ADR-0033), unchanged.
- `docs/releases/unreleased/run17cf-aria-rows-sheet-row-numbers.md` (ExSheet, Behaviour, ADR-0033), unchanged.
- None for the screen readers' passes: nothing in the product changed. The Narrator sentences are edit 11.

## For the merge

- `origin/main` at `2265cda6` (ADR-0201, #89) is merged in (`5eca9502`), with no conflict; the new public parameter
  `ExGridRow<TRow>.HeaderRowCount` is declared in `src/ExGrid/PublicAPI.Unshipped.txt`.
- Seen once in the first round and not this change's: on the WebAssembly host, `pivot.spec.mjs` run with `--grep` failed
  its first test's ADR-0056 check, "the stylesheets are not the ones the file booted with" (`ex-grid.min.css` and
  `ex-sheet.min.css` present after the test, only `app.css` at boot). The test itself passed, and the same grep passed
  on a rerun and on the Server host. The boot reads the document once `ready()` holds, and the package stylesheets are
  root components (ADR-0123, #82), so whether they are in it may depend on when that read lands: a candidate for the
  spine's sixth principle, left to whoever owns the fixture.
- `verification/2026-10-11-windows-17/part-e/pivot-probe.mjs` (on `claude/exsheet-windows-verify-17`) reads the
  report's rows by `aria-rowindex`; a later run of it subtracts the header's rows.
- Seen in the third round, in tests this branch does not touch, and only in a whole-solution run while the machine
  was loaded (load average about 20 to 28, other checkouts building and running layer 3):
  `EditingNotificationTests.The_opening_and_the_end_are_raised_before_the_browser_has_answered` failed twice with
  "Collection was modified; enumeration operation may not execute" at its `Assert.Equal([true, false], told)`, and
  `AllocationTests.Scrolling_allocates_the_same_at_a_thousand_and_a_million_rows` (MEM-1) failed once. In five whole
  runs of each over the same hour, the last three alternated, the branch failed three times and `origin/main`
  (`2265cda6`) none; `ExGrid.Components` alone passed every time. The first reads a list that the grid's notification appends to, so a
  continuation on another thread can still be appending while it is read; the second takes
  `GC.GetAllocatedBytesForCurrentThread` across awaits, whose continuations can run on another thread. Neither path
  is one this branch changed. Whether the branch makes them likelier is not settled by five runs: a candidate for the
  spine's sixth principle, left to whoever owns those tests.

## The check's output

`python3 docs/agent-runs/run17cf-aria-rows/check_proposal.py`, against the commit before the one that adds this
section; neither changes a target. It reads each target from git at that commit, applies the edits in order, and fails
an edit whose old text is not found exactly once on whole lines, or a Definition of Done edit of more than one line. A
line number is the old text's in the file as the edits before it left it.

```
13 edits in docs/agents/proposals/run17cf-aria-rows.md, checked against 812708d9
ok   1. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md: old text once, at line 22; 1 line(s) -> 1
ok   2. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md: old text once, at line 23; 1 line(s) -> 1
ok   3. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md: old text once, at line 50; 5 line(s) -> 6
ok   4. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md (optional): old text once, at line 147; 1 line(s) -> 4
ok   5. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md: old text once, at line 218; 1 line(s) -> 34
ok   6. docs/adr/0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md (optional): old text once, at line 132; 1 line(s) -> 1
ok   7. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md: old text once, at line 201; 5 line(s) -> 25
ok   8. docs/definition-of-done.md: old text once, at line 249; 1 line(s) -> 1
ok   9. docs/definition-of-done.md: old text once, at line 250; 1 line(s) -> 1
ok   10. docs/definition-of-done.md: old text once, at line 817; 1 line(s) -> 1
ok   11. samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor: old text once, at line 37; 1 line(s) -> 13
ok   12. samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor: old text once, at line 60; 1 line(s) -> 2
ok   13. samples/ExGrid.Docs/Pages/Pivot/PivotOverview.razor: old text once, at line 27; 1 line(s) -> 2
all edits apply in order
```
