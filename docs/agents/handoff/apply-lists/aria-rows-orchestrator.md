# Apply list: ARIA's row indices (claude/run17cf-aria-rows)

Re-based onto `origin/main` at `9a9638b4` (after #109, #110 and #111), from the proposal
`docs/agents/proposals/run17cf-aria-rows.md` (deleted on the branch; its last version is at `f3de4c15`).
Each block is whole lines; the list applies in order. Blocks marked **Rewritten** differ from the proposal's
because #109 (2026-10-13, decided with the user) closed every place a screen reader was owed. The proposal's
edits 11 to 13 (the Docs Site's pages) are on the branch, not here. Optional blocks may be left out; no other
block depends on them. The proposal's edit 10 note of 2026-10-13 (the sign-off build's Windows run re-checking
DOM focus under NVDA and Narrator, a defect fixable after the tag) is dropped: the user decided it earlier on
2026-10-13, and #109 superseded it the same day (block 11 says so).

Found while re-basing, and left to the orchestrator (no block changes them):

- **#109's summary of Part F under Narrator.** ADR-0033's 2026-10-13 note on the Keyboard Field and §21.11's
  2026-10-13 note say Part F found DOM focus on the Keyboard Field after every ↓ "under NVDA, under Narrator and
  with neither". The record (`verification/2026-10-11-windows-17/report.md`, Part F) says so of Narrator with its
  scan mode off only; in the scan mode Narrator starts in, DOM focus went to `body`, or in Chrome without a Focus
  among the page's buttons. Blocks 4, 7 and 11 state the condition in their own 2026-10-12 notes, before #109's,
  and leave #109's sentences as written.
- **A11Y-13** (a Header Group cell's `aria-colspan`) is unchanged. The branch also writes `aria-colindex` on each
  group cell and `aria-rowspan` where it spans more than one tier, pinned in layer 2
  (`AccessibilityTests.A_header_group_band_is_a_row_before_the_header_row`, `Each_tier_is_a_row_top_first`) and
  named in block 5; no block widens the criterion.

### 1. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md

The table's row for the counts. As the proposal had it; re-based onto main unchanged.

Old:

````text
| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |
````

New:

````text
| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM; the row count holds the header's rows as well as the data rows *(2026-10-12, below)* | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |
````

### 2. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md

The table's row for the indices. As the proposal had it.

Old:

````text
| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |
````

New:

````text
| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell, header rows and Header Group cells included; a row's index counts the header's rows before it *(2026-10-12, below)* | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |
````

### 3. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md

The example under "So the root does the talking". As the proposal had it.

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

### 4. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md

*(Optional; no other edit depends on it.)* **Rewritten.** The note on the sixteenth run's Narrator try, in "On a grid that edits, the tab stop and `aria-activedescendant` are the Keyboard Field's". Main now follows it with #109's note of 2026-10-13; the user's decisions of 2026-10-12 go between the two, in date order, as records, and say that DOM focus stayed on the field with Narrator's scan mode off, the case #109's note summarises as "under Narrator".

Old:

````text
  may have caused that. Not settled; the real screen reader stays owed, as the user chose. ADR-0080.)*
````

New:

````text
  may have caused that. Not settled; the real screen reader stays owed, as the user chose. ADR-0080.)*
  *(2026-10-12, decided with the user after the seventeenth Windows run: Narrator's scan mode taking
  the arrows is not a keyboard defect, since with scan mode off every arrow reached the grid, and
  NVDA's silence on that run's first pass is recorded only. ADR-0080 has what each reader said: with
  Narrator's scan mode off, DOM focus stayed on the Keyboard Field; in scan mode it did not.)*
````

### 5. docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md

**Rewritten.** A section between "Added after the fifth Windows run" and #109's "Verified against the accessibility tree, not a screen reader", in date order. Re-based: the Sheet's own row number is left open to be taken up on a user's report, a consequence of #109 and not a new decision of the user's; the closing paragraph says what the grid writes and what layer 3 reads, and that a reader's words far down are learned from users' reports, where the proposal had left them to the next Windows run.

Old:

````text
refusal stays where it is. A Focus move that changes no Selection still writes nothing (A11Y-10).
````

New:

````text
refusal stays where it is. A Focus move that changes no Selection still writes nothing (A11Y-10).

## Added after the seventeenth Windows run: the header's rows are counted *(2026-10-12)*

The indices above counted the data rows alone: the first data row was `aria-rowindex="1"`, and the
header row carried no index. The seventeenth Windows run read NVDA on `/sheet`
(`verification/2026-10-11-windows-17/report.md`, Part F): near the top D10's row was `row 11` and
A1's `row 2`, while the attribute said 10 and 1, so NVDA counted the rows before a row, the header
row among them. Far down, where the DOM holds a Window, the only number the page gives a reader is
`aria-rowindex`. The same row could have been given two numbers, one at the top and one a scroll
away.

*(2026-10-12, decided with the user: **ARIA's row indices count the header's rows first.**)*

- The header row is `aria-rowindex="1"`. A data row's `aria-rowindex` is its position plus the
  header's rows plus one; `aria-rowcount` is the data rows' total plus the header's rows. A hidden
  header ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md)) has no rows, and the first data row
  is 1 again.
- Cell ids are unchanged: `ex-7-r412-c3` still names the row at position 412, now
  `aria-rowindex="414"`.
- ExSheet and ExPivot inherit it. Under ExSheet's Column Headings, A1 is row 2, so the grid gives a
  Sheet row its number plus one, at the top and far down alike: the number NVDA and Narrator counted
  near the top.
- *(Found while building, 2026-10-12.)* Under Header Groups
  ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)) each tier is a row
  of its own, top first, before the header row, and the header is a `rowgroup` holding them. Each of
  those rows is `display: contents`, so the band lays out, paints and stacks as the one element did. A
  rectangle carries `aria-colindex` (its first member's), `aria-colspan`, and `aria-rowspan` where it
  spans more than one tier.
- *(Left open while building, 2026-10-12.)* Saying the Sheet's own row number — `aria-rowindextext`,
  or a Row Heading as a `rowheader` cell — is not done. It stays open, to be taken up on a user's
  report: since 2026-10-13 no run reads what a screen reader does with either ("Verified against
  the accessibility tree" below), so no Windows run is asked.

Chromium's accessibility tree holds each header row as a row owning its column headers,
`display: contents` included, and the data rows' indices consecutive after them, at the top and
after a scroll far down (layer 3, over the DevTools protocol: `aria-rows.spec.mjs`). That is the
check, as the section below has it. The seventeenth run heard readers near the top only; what one
says of a row far down is learned from users' reports.
````

### 6. docs/adr/0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md

*(Optional; no other edit depends on it.)* One line, so that a reader finds where the band's rows went. As the proposal had it.

Old:

````text
- **ARIA** (`aria-colspan` on group cells) lands with the structural surface of
````

New:

````text
- **ARIA** (`aria-colspan` on group cells, and since 2026-10-12 each tier a row of its own) lands with the structural surface of
````

### 7. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md

**Rewritten.** The screen-reader bullet of "What the sixteenth Windows run settled": what the seventeenth run heard and the user's decisions of 2026-10-12, as the proposal had them (records), then a pointer to #109's decision of 2026-10-13, which closes the bullet's "stays owed".

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
  no arrow reached the page: DOM focus left the field for `body` at the first ↓, but in Chrome
  without a Focus the grid was never reached, and DOM focus "moved among the page's buttons"
  (`button#sheet-undo`, `button#sheet-redo`, `button#sheet-format-cells`, `button#sheet-insert-row`,
  `button#sheet-revalue`). After the press on D10, ↓ read `column header cap E, column 5 of 16384.`,
  Narrator's own cursor on the column headers. With scan mode turned off by Insert+Space (Narrator said "Scan off"), every arrow's
  keydown reached the field, which kept DOM focus, and Narrator read
  `Item, column header cap A, row 2 of 1,048,576, column 1` after the first ↓ and
  `Row 12 of 1,048,576.` after ↓ from D10. As the second page loaded, Narrator said "Scan" again.)*
  *(2026-10-12, decided with the user: **Narrator's scan mode taking the arrows is not a keyboard
  defect.** With scan mode off every arrow reached the grid, and Narrator turns scan mode on again
  with each new page. **NVDA's silence on the first pass comes from the probe's scripted focus before
  any user input, and is recorded only.**)*
  *(2026-10-13, decided with the user: no screen reader is run to verify a release, and none is owed;
  ADR-0033's "Verified against the accessibility tree, not a screen reader". The notes above are
  records.)*
````

### 8. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md

*(Optional; not in the proposal.)* A leftover of #109 in the same ADR, found while re-basing edit 7: the paragraph on the field's active descendant still says a screen reader is owed. Worded as ADR-0033's own note on ADR-0037's button.

Old:

````text
user. What a screen reader then says is still owed a real one, as ADR-0033 already records.
````

New:

````text
user. What a screen reader then says is still owed a real one, as ADR-0033 already records.
*(No longer owed: the tree is the check, 2026-10-13, ADR-0033's "Verified against the accessibility
tree, not a screen reader".)*
````

### 9. docs/definition-of-done.md

A11Y-2, the whole row. As the proposal had it.

Old:

````text
| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted | `aria-rowcount="1000000"` |
````

New:

````text
| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice; the row count holds the header's rows as well as the data rows (ADR-0033) | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted, under one header row, a Header Group band, and a hidden header; Layer 3 on `/features` and `/sheet` | `aria-rowcount="1000001"` under one header row, one more per tier of Header Groups, `1000000` with the header hidden; on `/features`, `402` over the first grid's 400 rows under a Header Group band and `21` over the second grid's 20; on `/sheet`, `1048577` *(2026-10-12, decided with the user: ARIA's row indices count the header's rows first, after the seventeenth Windows run's NVDA reading; ADR-0033. Until then the pass value read `aria-rowcount="1000000"`.)* |
````

### 10. docs/definition-of-done.md

A11Y-3, the whole row. As the proposal had it.

Old:

````text
| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes | Layer 2 scrolled to row 500,000 and column 30 | first painted row's index is its absolute one, not 1 |
````

New:

````text
| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes, and a row's index counts the header's rows before it: the header row is 1, each tier of Header Groups a row of its own before it (ADR-0033) | Layer 2 scrolled to row 500,000 and column 30, under one header row, a Header Group band, and a hidden header; Layer 3 over the DevTools protocol on `/features` and `/sheet`, at the top and after a scroll far down | the first painted row's index is its position plus the header's rows plus one — `500002` under one header row, `500003` under one tier, `500001` with the header hidden — and the cell at column 30 is `aria-colindex="31"`; in Chromium's accessibility tree each header row is a row owning its column headers, at the top and far down *(2026-10-12, decided with the user: ARIA's row indices count the header's rows first, after the seventeenth Windows run's NVDA reading; ADR-0033. Until then the pass read "first painted row's index is its absolute one, not 1".)* |
````

### 11. docs/definition-of-done.md

**Rewritten.** §21.11, the row on the Cell Editor's own ARIA semantics, the whole row. Re-based onto #109's row: the seventeenth run's Part F, shortened to where DOM focus was (the full record is block 7's, in ADR-0080), and the user's decisions of 2026-10-12, go between the 2026-10-11 note and #109's note of 2026-10-13, in date order. **Dropped:** the proposal's note of 2026-10-13 (the row stays open, the sign-off build's Windows run re-checks DOM focus after ↓ under NVDA and Narrator, a defect found then fixed after the tag). The user decided it earlier on 2026-10-13, and #109 superseded it the same day: no screen reader is run, and the check by hand is withdrawn. It never reached main, so nothing written is undone.

Old:

````text
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)). *(2026-10-11, decided with the user: **the richer contract is accepted in writing as out of `0.1.0`'s scope.** What the sixteenth Windows run heard — ↓ read as "Scan", DOM focus on `body` — may be a keyboard defect rather than wording, so the sign-off's Windows run checks where DOM focus is after ↓, with and without a Focus, under NVDA (recorded through its Speech Viewer) and Narrator (heard, with no copy command sent); a defect is fixed before the tag.)* *(2026-10-13, decided with the user, [ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)'s section of that date: no screen reader is run to verify a release. The accessibility surface is checked against Chromium's accessibility tree and the APG grid pattern in layers 2 and 3, and where DOM focus is after ↓ is held in layer 3 (`key-field.spec.mjs`'s ADR-0080 and KB-12 tests). The seventeenth Windows run's Part F found it on the Keyboard Field after every ↓, under NVDA, under Narrator and with neither. The check by hand below is withdrawn. The richer contract stays out of `0.1.0`'s scope.)* |
````

New:

````text
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)). *(2026-10-11, decided with the user: **the richer contract is accepted in writing as out of `0.1.0`'s scope.** What the sixteenth Windows run heard — ↓ read as "Scan", DOM focus on `body` — may be a keyboard defect rather than wording, so the sign-off's Windows run checks where DOM focus is after ↓, with and without a Focus, under NVDA (recorded through its Speech Viewer) and Narrator (heard, with no copy command sent); a defect is fixed before the tag.)* *(2026-10-12, the seventeenth Windows run's Part F, `/sheet` on the WebAssembly host in Chrome and Edge, recorded in [ADR-0080](adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)'s "What the sixteenth Windows run settled" (commits 76bd2116 and 2fc23f56 on `claude/exsheet-windows-verify-17`): after every ↓, with and without a Focus, DOM focus stayed on the Keyboard Field without a reader, under NVDA 2026.2, and under Narrator with its scan mode off. In the scan mode Narrator starts in, no arrow reached the page: DOM focus went to `body`, or in Chrome without a Focus "moved among the page's buttons".)* *(2026-10-12, decided with the user: **Narrator's scan mode taking the arrows is not a keyboard defect** — with scan mode off every arrow reached the grid, and Narrator turns scan mode on again with each new page; **NVDA's silence on the first pass comes from the probe's scripted focus before any user input, and is recorded only.**)* *(2026-10-13, decided with the user, [ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)'s section of that date: no screen reader is run to verify a release. The accessibility surface is checked against Chromium's accessibility tree and the APG grid pattern in layers 2 and 3, and where DOM focus is after ↓ is held in layer 3 (`key-field.spec.mjs`'s ADR-0080 and KB-12 tests). The seventeenth Windows run's Part F found it on the Keyboard Field after every ↓, under NVDA, under Narrator and with neither. The check by hand below is withdrawn. The richer contract stays out of `0.1.0`'s scope.)* |
````
