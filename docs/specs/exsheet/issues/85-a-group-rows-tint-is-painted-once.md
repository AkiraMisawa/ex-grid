# 85: A group or total row's tint is painted once on every cell

Status: done

**What to build:** a fix found by ticket 81. A group or total row (ADR-0024) puts its tint on the row and again on
each cell. A scrollable cell then shows the tint twice. A pinned cell, whose own ground covers the row's, shows it
once. With the default token, the two differ by about 9 grey levels. That figure is arithmetic and has not been
read from the screen. Nothing compares the two today.

**Blocked by:** None (can start immediately)

- [x] **One shade.** Every cell of a group or total row, pinned or scrollable, shows the same shade, at the
      tint the ADR and its token intend.
- [x] **Keep ticket 81's fix.** A lined cell keeps the tint under its lines.
- [x] **Keep the other backgrounds.** Row Stripes (ADR-0038) and the hover band (ADR-0029) still read as before
      on those rows.
- [x] **Layer 3.** Compare a pinned cell's pixel with a scrollable cell's in the same group row, and in the same
      total row. Name the test after ADR-0024 and ADR-0038. Read the pixels first, and say in the comment
      whether the 9 levels were real.

## Comments

2026-10-01, agent cf-84.

### The pixels, read first: the 9 levels were real

- **Measured:** macOS, headless Chrome, the WebAssembly host, port 5451, on 004a029's stylesheet, with
  the default tokens over a white Canvas, on `/stripes`.
  - Group row 3: the pinned cell painted 245,245,245, and all three scrollable cells 236,236,236.
  - Under the hover band, group row 12 painted 238,238,238 pinned and 229,229,229 scrollable.
- **9 grey levels in both, as the arithmetic said.** One layer of rgba(128, 128, 128, 0.08) over 255
  is 244.8, and a second over that is 235.6.
- **The total row's rule was not read:** the test stops at the group row. By the same arithmetic,
  rgba(128, 128, 128, 0.45) once over white is 198, twice 166 or 167. So a scrollable cell's rule was
  about 32 levels darker than a pinned cell's.

### What changed

- **The tint is painted on the cells, and on the row only where no cell is**
  (`src/ExGrid/wwwroot/ex-grid.css`, the Row Kind block).
  - Each cell paints it once, over whatever ground it has: a pinned cell's opaque one, a Fill, or
    none.
  - A group or total row paints no background beneath its cells (`background-image: none`, as it
    already painted no row rule and no stripe).
  - The row's `::after`, after the last cell, grows into the space beyond the last column and paints
    the tint there. That is the space the old comment kept the row's own tint for.
  - The gap standing in for the columns virtualised away paints it too. That gap is beneath the
    pinned block once the cells are painted, but not while a sideways scroll waits for them.
  - A Placeholder paints no scrollable cell, only its pinned ones and its bar, which is its
    `::after`, so it keeps the tint on the row.
- **The tint is still named in `--ex-tint`, on the row and on its cells, by the same selectors.** So:
  - a lined cell still paints it beneath its lines (ticket 81);
  - a group row's tint still beats a pinned cell's stripe (UX-16);
  - Missing still replaces it.
- **The pinned/scrollable difference is gone for other cells of these rows too:**
  - a filled scrollable cell already showed the tint once, over its Fill, and still does;
  - a Missing scrollable cell in a group row was the group's tint and Missing's together, while a
    pinned one was Missing's alone. It is now Missing's alone on both, which is what ADR-0024's
    "the state wins" says.
- **Forced colours are untouched.** The gradients are discarded there, and the `::after` paints
  nothing. A total row keeps its overline.
- **Considered and not taken:**
  - **ADR-0038's way for stripes, the row and the pinned cells only.** A filled scrollable cell would
    then lose the tint under its Fill, which ticket 81's tests forbid.
  - **An opaque ground on every scrollable cell of these rows.** The ground beneath the row is not
    always `--ex-background`'s: a Consumer may leave the grid transparent over its page.

### Tests

- **Layer 2:** `RowKindTests.A_group_or_total_rows_tint_is_painted_once_over_every_cell` (ADR-0024 /
  ADR-0038). It emulates the cascade over the shipped rules outside any at-rule (AngleSharp's
  matching and specificity), on a grid with stripes on, one pinned column, and group and total rows
  at striped and unstriped positions. It checks that:
  - the row paints no background image;
  - every cell, pinned and scrollable, paints `var(--ex-tint)`, the role's own, not the stripe's;
  - the row's `::after` grows and paints it;
  - a detail row is untouched;
  - after a fling, a group or total Placeholder paints it on the row and not again on its bar.
  - On the stylesheet before this change it failed: the row's background image was
    `var(--ex-tint)` where `none` was expected.
- **Layer 3** (`stripes.spec.mjs`):
  - "a group or total row paints its tint once, on its pinned and its scrollable cells alike
    (ADR-0024, ADR-0038)".
    - In group rows 3 (striped position) and 12, the pinned cell and the three scrollable cells
      paint the same ground. That ground is the cell's resolved tint laid once over the plain row's
      ground on a canvas.
    - In total row 7, the device pixels down through the top edge match pinned against scrollable.
      The darkness the rule adds there is one layer's, wherever the edge falls among device pixels.
  - "the hover band reads the same over a group row's pinned and scrollable cells (ADR-0024,
    ADR-0029)".
- **Which way each was shown to fail:**
  - Both layer-3 tests failed in the local run above, with the pixels quoted there.
  - The total row's last assertion was rewritten after that run, to measure darkness rather than
    find one device pixel at the rule's colour. Before the fix, by reasoning, the scrollable cells'
    edge would not match the pinned cell's: two layers of the rule against one, 32 levels.
  - With the fix in place layer 3 has not run locally. CI on PR #42 runs it, including ticket 81's
    `appearance.spec.mjs` and the UX-16 tests, which this change must keep passing.
- **Layers 1 and 2:** 871 + 2304 + 88 + 1257 (1 skipped) + 35 + 539 passed, with both commits in place.

### P1–P9, and why `spikes/render-bench` was not run

- **No layer is added to any cell.** The cells of group and total rows already painted the tint, and
  the row's layer beneath them is gone. Each group or total row gains one generated box, its
  `::after`, which is not a DOM node. A detail row, the overwhelming majority, is unchanged.
- **No markup, no C#, and nothing reaching JavaScript changes,** so the DOM bound, row memoisation
  and P4 are as they were.

### Seen, and not changed here

- **Row Stripes have the same split between pinned and scrollable cells** (ADR-0038), in two
  places:
  - a Missing scrollable cell on a striped row is Missing's tint over the stripe, while a pinned one
    is Missing's alone. That is about 6 levels by arithmetic, not read.
  - a filled scrollable cell on a striped row hides the stripe under its Fill, while a filled pinned
    cell shows it over the Fill (ticket 81).

  Neither is a group or total row, which is this ticket's subject.
- **ADR-0024's Consequences say the tint is painted "on the row and on its cells".** It is now
  painted on the cells, and on the row only where no cell is. The reason the ADR gives, a pinned
  cell's opaque ground, is unchanged, so this is a wording note for the ADR's owner, not a changed
  decision.

2026-10-01, agent cf-84, after CI on PR #42 (run 36921156631).

- **What failed.** The hover-band test failed on Linux, headed, under Chrome and Edge on the
  WebAssembly host and under Chrome on the Server host: "the band paints over the group row
  (245,245,245 → 245,245,245)".
- **The cause was the test's own read, not the stylesheet.** The trace shows it:
  - the screencast frame taken after the pointer moved shows the band over all of row 12, pinned
    and scrollable cells alike, darker than the group tint past the last column;
  - the bands stood on row 12 in the DOM when the screenshot was asked for;
  - the frame recorded during the screenshot shows no band on any row. That screenshot was clipped
    to the grid, at a fractional y (216.875). The frame shows the page laid out from the grid's top,
    as if the pointer, left where it was on the screen, were over another part of the page.
  - UX-16's hover test, which captures the whole viewport, passes in the same run. Headless Chrome on
    macOS kept the band under the clipped capture, so a headless run hid it. I had not run the test
    with the fix before this; the earlier local run was on the stylesheet before the fix.
- **The fix is in the test.** `edgesOf` reads from a capture of the whole viewport, as `groundsOf`
  does. The hover test also checks that the bands still stand on row 12 after the read, so a capture
  that moves the pointer is told apart from cells that cover the band. Nothing in `ex-grid.css`
  changed: the band lies in the selection layers, above every cell.
- **Runs.** `stripes.spec.mjs`, headless on macOS, WebAssembly, port 5451: 6 passed, and 1 failed,
  the vertical-scrollbar thumb test (ADR-0029, UX-10), with a gutter of 0, macOS headless's known
  limit, which CI judges. Headed was not run on the Mac (the user's standing request), so the headed
  pass is CI's to confirm. Layers 1 and 2: 874 + 2317 + 133 + 1272 (1 skipped) + 38 + 583 passed.
