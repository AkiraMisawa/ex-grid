# Run 17 C-F, ARIA's row indices: what the orchestrator writes

Branch `claude/run17cf-aria-rows`, from `main` at `a533de01`. Decision (A) of 2026-10-12, taken with the user after the
seventeenth Windows run's NVDA reading (`verification/2026-10-11-windows-17/report.md`, Part F, on
`claude/exsheet-windows-verify-17`): near the top of `/sheet` NVDA named D10's row `row 11` and A1's `row 2`, while
`aria-rowindex` said 10 and 1. A reader counts the rows painted before a row near the top, the header row among them, and
reads `aria-rowindex` far down, where the DOM holds a Window; the header row carried no index of its own, so the two
numbers for one row differed by one. ExGrid's row indices are now ARIA's: the header's rows are counted first. Nothing
here changes an ADR, `CONTEXT.md`, the Definition of Done or the Docs Site; the text for each is below, in the
repository's voice.

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
- Run at `3060902c`: layer 2 green in every project the change touches (ExGrid.Components 2,073 passed and 1 skipped;
  ExSheet.Components 755; ExPivot.Components 303; ExGrid.MudBlazor.Tests 235; ExSheet.MudBlazor.Tests 62;
  ExPivot.MudBlazor.Tests 76). Layer 3 with Playwright's bundled Chromium, headed under xvfb, `--project=chrome`, on
  both hosts: `aria-rows.spec.mjs` with `--repeat-each=3` (9 of 9 on each); `stripes`, `virtualisation`,
  `presentation`, `features` and `pivot-db` whole (83 passed, 1 skipped, on each); and the changed tests of
  `gestures` (HG-), `key-field` (A11Y), `mud-app`, `circuit`, `grid-live`, `grid-live-local`, `pivot`, `pivot-csv`
  and `pivot-risk`, all passing on each host.

## ADR-0033: what to write

The table's two rows, restated:

- old: ``| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |``
- new: ``| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM; the row count holds the header's rows as well as the data rows *(2026-10-12, below)* | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |``

- old: ``| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |``
- new: ``| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell, header rows and Header Group cells included; a row's index counts the header's rows before it *(2026-10-12, below)* | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |``

The example, restated (whole lines, as the block holds them):

- old:
  ```
  root   role="grid"  aria-multiselectable="true"
         aria-rowcount="1000000"  aria-colcount="40"
         aria-activedescendant="ex-7-r412-c3"
  row    aria-rowindex="413"
  cell   aria-colindex="4"  id="ex-7-r412-c3"
  ```
- new:
  ```
  root   role="grid"  aria-multiselectable="true"
         aria-rowcount="1000001"  aria-colcount="40"
         aria-activedescendant="ex-7-r412-c3"
  header aria-rowindex="1"
  row    aria-rowindex="414"
  cell   aria-colindex="4"  id="ex-7-r412-c3"
  ```

A section appended at the end of the ADR, after "Added after the fifth Windows run":

```
## Added after the seventeenth Windows run: the header's rows are counted *(2026-10-12, decided with the user)*

The indices above counted the data rows alone: the first data row was `aria-rowindex="1"`, and the header row carried no
index. A reader does not read one number for a row everywhere. Near the top it counts the rows before a row, the header
row among them; far down, where the DOM holds a Window, it has only `aria-rowindex`. The seventeenth Windows run read
NVDA on `/sheet` (`verification/2026-10-11-windows-17/report.md`, Part F): D10's row was `row 11` and A1's `row 2`, while
the attribute said 10 and 1. The same row would have had two numbers, one at the top and one a scroll away.

**The decision:** ARIA's indices count the header's rows first.

- The header row is `aria-rowindex="1"`. Under Header Groups ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md))
  each tier is a row of its own, top first, before the header row, and the header is a `rowgroup` holding them. Each
  of those rows is `display: contents`, so the band lays out, paints and stacks as the one element did; a rectangle
  carries `aria-colindex` (its first member's), `aria-colspan`, and `aria-rowspan` where it spans more than one tier.
- A data row's `aria-rowindex` is its position plus the header's rows plus one; `aria-rowcount` is the data rows' total
  plus the header's rows. A hidden header ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)) has no rows, and the
  first data row is 1 again.
- Cell ids are unchanged: `ex-7-r412-c3` still names the row at position 412, now `aria-rowindex="414"`.
- ExSheet and ExPivot inherit it. Under ExSheet's Column Headings, A1 is row 2, so a reader names a Sheet row by its
  number plus one, at the top and far down alike. **Saying the Sheet's own number** — `aria-rowindextext`, or a Row
  Heading as a `rowheader` cell — **is decided after the next Windows run** reads what NVDA and Narrator do with each.

Chromium's accessibility tree holds each header row as a row owning its column headers, `display: contents` included,
near the top and far down (layer 3, over the DevTools protocol). What a reader says is still the Windows run's.
```

## ADR-0032: one line (optional, so the reader finds where the band's rows went)

- old: ``- **ARIA** (`aria-colspan` on group cells) lands with the structural surface of``
- new: ``- **ARIA** (`aria-colspan` on group cells, and each tier a row of its own since 2026-10-12) lands with the structural surface of``

## Definition of Done: A11Y-2 and A11Y-3, restated

- old: ``| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted | `aria-rowcount="1000000"` |``
- new: ``| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice; the row count holds the header's rows as well as the data rows (ADR-0033, 2026-10-12) | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted, under one header row, a Header Group band, and a hidden header; Layer 3 on `/features` and `/sheet` | `aria-rowcount="1000001"` under one header row, one more per tier of Header Groups, `1000000` with the header hidden; `/sheet` says `1048577` *(2026-10-12: the pass value read `aria-rowcount="1000000"`; the header's rows are counted since the seventeenth Windows run's NVDA reading)* |``

- old: ``| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes | Layer 2 scrolled to row 500,000 and column 30 | first painted row's index is its absolute one, not 1 |``
- new: ``| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes, and a row's index counts the header's rows before it: the header row is 1, each tier of Header Groups a row of its own before it (ADR-0033, 2026-10-12) | Layer 2 scrolled to row 500,000 and column 30, under one header row, a Header Group band, and a hidden header; Layer 3 over the DevTools protocol on `/features` and `/sheet`, at the top and after a scroll far down | the first painted row's index is its position plus the header's rows plus one — `500002` under one header row, `500003` under one tier, `500001` with the header hidden — and the cell at column 30 is `aria-colindex="31"`; Chromium's accessibility tree holds each header row as a row owning its column headers *(2026-10-12: the pass read "first painted row's index is its absolute one, not 1")* |``

## The Docs Site

`samples/ExGrid.Docs/Pages/Grid/GridSelectionAndKeys.razor`, "The keyboard" (the Change Note's `Docs:` link):

- old:
  ```
          <p>Click into the grid, or tab to it: the grid is one tab stop, and its keys are Excel's.</p>
  ```
- new:
  ```
          <p>Click into the grid, or tab to it: the grid is one tab stop, and its keys are Excel's.</p>
          <p>
              A screen reader reads the grid as a table. The header row is its row 1 and the first row of data its row 2,
              so a reader says the same number for a row near the top, where it counts the rows before it, and a million
              rows down, where it reads the number the grid writes. Each tier of Header Groups is a row of its own above
              the header row, and the rows of data are counted after them; with <code>HideHeader</code>, the first row of
              data is row 1.
          </p>
  ```

`samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor`, "A first sheet" (the ExSheet Change Note's `Docs:` link):

- old:
  ```
              <li><code>ShowRowHeadings</code>, <code>ShowColumnHeadings</code> and <code>ShowFormulaBar</code> hide the Headings or the Formula Bar; the cells are still addressed A1.</li>
  ```
- new:
  ```
              <li><code>ShowRowHeadings</code>, <code>ShowColumnHeadings</code> and <code>ShowFormulaBar</code> hide the Headings or the Formula Bar; the cells are still addressed A1.</li>
              <li>A screen reader counts the Column Headings as the table's row 1, so it names a Sheet row by its number plus one: A1 is in row 2, near the top and far down. With <code>ShowColumnHeadings</code> off, the two numbers agree.</li>
  ```

`samples/ExGrid.Docs/Pages/Pivot/PivotOverview.razor`, "The parts of a pivot":

- old:
  ```
              the Value Fields' Aggregations are computed from.
  ```
- new:
  ```
              the Value Fields' Aggregations are computed from. A screen reader counts the report's header rows before its
              first row: a row for each tier of column Items, then the row of column headers.
  ```

## Change Notes written

- `docs/releases/unreleased/run17cf-aria-rows-header-rows-counted.md` (ExGrid, Behaviour, ADR-0033).
- `docs/releases/unreleased/run17cf-aria-rows-sheet-row-numbers.md` (ExSheet, Behaviour, ADR-0033).

## For the merge

- `origin/main` at `2265cda6` (ADR-0201, #89) is merged in (`5eca9502`), with no conflict; the new public parameter
  `ExGridRow<TRow>.HeaderRowCount` is declared in `src/ExGrid/PublicAPI.Unshipped.txt`. After the merge the solution
  builds with no warning, and layers 1 and 2 pass as above (ExGrid.Tests 1,702; ExGrid.Docs.Generator.Tests 319).
  Layer 3 ran on `3060902c`, before the merge.
- Seen once and not this change's: on the WebAssembly host, `pivot.spec.mjs` run with `--grep` failed its first
  test's ADR-0056 check, "the stylesheets are not the ones the file booted with" (`ex-grid.min.css` and
  `ex-sheet.min.css` present after the test, only `app.css` at boot). The test itself passed, and the same grep passed
  on a rerun and on the Server host. The boot reads the document once `ready()` holds, and the package stylesheets are
  root components (ADR-0123, #82), so whether they are in it may depend on when that read lands: a candidate for the
  spine's sixth principle, left to whoever owns the fixture.
- `verification/2026-10-11-windows-17/part-e/pivot-probe.mjs` (on `claude/exsheet-windows-verify-17`) reads the
  report's rows by `aria-rowindex`; a later run of it subtracts the header's rows.
