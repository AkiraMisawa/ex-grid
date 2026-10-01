# 76: A bordered cell keeps a group or total row's tint, and a pinned cell's stripe

Status: done

**What to build:** a fix found by ticket 47. ADR-0050 item 15 draws a cell's dashes, its double lines,
the pixel past the gridline and a neighbour's Fill as background layers on the cell. A group or total
row's tint, and a pinned cell's stripe tint, are background images on the same cell. On a cell that
has any of those layers, the lines replace the tint, so the row looks like an ordinary row there.
ExSheet uses neither, but an ExGrid Consumer may use both.

**Blocked by:** None (can start immediately)

- [x] On a cell with a per-cell appearance, a group or total row's tint and a pinned cell's stripe
      still show, beneath the lines and above the cell's Fill, if it has one.
- [x] Layer 2 markup and a layer-3 pixel check, with the test named after ADR-0050 item 15 and the
      ADR of the tint it keeps.
- [x] P1–P9 hold. Re-measure in `spikes/render-bench` if the change adds a layer to every cell.

## Comments

2026-10-01, agent cf-76.

### What changed

- **Each tint is named, then painted** (`src/ExGrid/wwwroot/ex-grid.css`). The rules that paint a
  cell's tint — a pinned cell's Row Stripe (`.ex-row-stripe .ex-pinned`), a group or total row's
  (`.ex-row-group .ex-cell`, `.ex-row-total .ex-cell`) and a Missing state's
  (`.ex-cell.ex-state-missing`) — set `--ex-tint` and paint `background-image: var(--ex-tint)`. Their
  selectors and order are unchanged, so the cascade picks the same tint as before (a group row's over
  the stripe, Missing over both). A cell without lines paints what it painted.
- **`.ex-cell.ex-lined` paints `var(--ex-tint, none)` as its fifth layer**, under its four lines and
  over the cell's Fill, which is its background colour. On an untinted row the layer is `none`.
- **A lined Missing cell was worse off than the ticket says, and is fixed with it.** The Missing rule
  is declared after `.ex-lined` (it has to be, for the Modified mark's `box-shadow`), so at equal
  specificity it took the whole `background-image` list. Its one layer was then sized by the list's
  first entry, the top line's size, which is `0 0` without a top line. Neither the state's tint nor
  the dashes, double lines, pixels past the gridline or covers showed: only a solid line, which is a
  `border`, survived. That broke ADR-0006 (the state is never the one that disappears) and ADR-0050
  item 15. Missing now only names its tint, and paints it itself on cells without lines
  (`.ex-cell.ex-state-missing:not(.ex-lined)`).
- **Where the tint lies among the lined rule's own layers.** It covers the whole cell, as it does on
  a cell without lines. So it lies over the two layers beneath the lines as well: a neighbour's Fill
  on the gridline this cell holds (`--ex-cover-r`, `--ex-cover-b`), and the column rule. In a group
  or total row the neighbour to the right wears the same tint over its Fill, so the Fill runs on
  unbroken across the gridline. A total row's rule also runs on across it, where a tint beneath the
  cover would break it at every filled neighbour. What is left differs by one device pixel, and only
  where an ExGrid Consumer combines a tint with a Fill or a themed column rule (ExSheet uses no tint):
  - the column rule lies beneath the tint on a lined cell, and over it elsewhere (it is an inset
    shadow there). With the default grey tint over a grey rule, the two orders composite alike.
  - the bottom cover takes this row's tint. The filled cell below, in another row, wears its own
    row's.
  - a Missing cell's tint lies over a filled right neighbour's cover pixel. The neighbour itself is
    not Missing.
  - double's middle pixel stays the grid's ground over a tint, as it does over a Fill (ADR-0063).
- **The `/appearance` page has a fourth grid, `#appearance-tints`.** Columns A–E are pinned and F–J
  scroll. In each block the first cell is plain, the second has a dashed bottom line, the third a
  yellow Fill, the fourth the Fill beside it over its gridline, and the fifth the Fill and the line.
  Rows 1 (striped), 3 (group), 5 (total) and 7 (Missing, striped) carry them; the rows between are
  plain, so no cover reaches from one of them into the next.

### Tests

- **Layer 2** (`tests/ExGrid.Components/CellAppearanceTests.cs`, against the shipped stylesheet read
  through the static-web-asset manifest; `ShippedStylesheetTests.CoreStylesheet` is now `internal`):
  - `A_lined_cell_keeps_its_group_or_total_rows_tint` (ADR-0050 item 15 / ADR-0024);
  - `A_lined_pinned_cell_keeps_its_rows_stripe` (ADR-0038);
  - `A_lined_missing_cell_keeps_its_states_tint_and_its_lines` (ADR-0006);
  - `The_lined_rule_paints_the_tint_beneath_the_lines`.

  The first three check that each lined cell in the markup is reached by the rule that names its
  tint, and that no background declared after `.ex-lined` reaches it. The fourth checks that the
  lined rule lists the tint fifth and gives every layer a size and a position. All four failed on the
  old stylesheet. Reverting the Missing rule alone also fails the Missing test.
- **Layer 3** (`appearance.spec.mjs`): three tests, named after ADR-0050 item 15 and ADR-0024,
  ADR-0038 (UX-15) or ADR-0006. Each reads the painted grid. In every tinted row and both blocks, a
  lined cell, a covered cell, and a filled lined cell paint their twin without lines: the centre, and
  the device pixels through the top edge (a total row's rule), within 1. The dashes are pure black.
  Each tint is also visible to begin with, on the plain ground and over the yellow.
- **Runs** (macOS, headless Chrome, port 5431; load average about 7–8):
  - on the stylesheet before this change, the three layer-3 tests: 3 failed. For example, a striped
    row's lined pinned cell painted 255,255,255 where its twin painted 248,248,248.
  - `appearance.spec.mjs` and `stripes.spec.mjs` on the WebAssembly host: 37 passed, 1 failed.
  - the same two files on the Server host: 37 passed, 1 failed.
  - the one failure on both hosts is `stripes.spec.mjs`'s "the vertical scrollbar paints a thumb in
    its gutter (ADR-0029, UX-10)", with a gutter of 0. That is headless Chrome on macOS. It fails the
    same way on 2ff6dfd's stylesheet, and CI judges it.
  - Layers 1 and 2: 871 + 2304 + 88 + 1252 (1 skipped) + 34 + 533 passed.

### P1–P9, and why `spikes/render-bench` was not run

- The change is the stylesheet's alone. No markup, no C#, and nothing reaching JavaScript changes.
  The DOM bound, row memoisation and P4 are therefore as they were, and the layer-2 render-count and
  interop tests still pass.
- No layer is added to every cell. A cell with lines gains one layer, which is `none` unless its row
  or state is tinted. There it is the same gradient the cell without lines beside it already paints.
- The cells that are tinted now read their image through `var()` rather than a literal.

### Seen, and not changed here

- **A group or total row's scrollable cells wear its tint twice.** The rule paints it on the row and
  on every cell, and a scrollable cell is transparent over the row, so its ground is two tints deep.
  A pinned cell is one deep, over its opaque ground. The Row Stripes rule avoids this by tinting
  pinned cells only (ADR-0038). `stripes.spec.mjs` compares group rows at odd and even positions, not
  pinned against scrollable, so nothing catches it. With the default token, the arithmetic gives a
  difference of about 9 levels of grey (not read from the screen).
- **A tone outranks a Stale or Error state's colour.** The Tone block says it is "declared before
  the Cell State block, so a state the Consumer named outranks a tone". It is declared near the end
  of the file, after the Cell States, at the same specificity, so a theme's tone colour wins over a
  Stale or Error colour.

Both bear on ADR-0024, ADR-0038 and ADR-0006 as written. Neither was this ticket's to change.
