# 22: Ctrl+click and Ctrl+drag on Headings

Status: done

**What to build:** ADR-0050, item 1 (added 2026-09-29) and ADR-0012 (2026-09-29). Ctrl+click on a
Heading, or on a plain ExGrid's column header, adds the whole column (row) as a new range, or takes
a wholly selected one out. Ctrl+drag adds the columns (rows) crossed as one range. Neither sorts.
The user observed both in Excel (Microsoft 365, Windows).

**Blocked by:** 21 (the Heading drag mode Ctrl+drag runs on)

- [x] Ctrl+click on a column not wholly selected adds it as a new range, with the Focus on its
      first visible row (SR-2e)
- [x] Ctrl+click on a wholly selected column takes it out, and the Focus follows ADR-0052's
      take-out rule (the first remaining cell, by rows, of the range made last) (SR-2e)
- [x] Ctrl+drag adds whole columns from the pressed one to the pointer's as one range (SR-2e)
- [x] The same on Row Headings, by rows (DC-42)
- [x] Meta counts as Ctrl only on an Apple platform, as it does on cells (ADR-0012) (SR-2e)
- [x] Neither sorts on a plain ExGrid (SR-2e)

## Comments

2026-09-29, built. The pure half is `GridSelection.ToggleColumn` / `ToggleRow`, with
`CoversColumn` / `CoversRow` (every cell of the line selected, whichever ranges hold them) and
`SelectionRange.SubtractArea`, which takes a rectangle out as `Subtract` takes a cell and lists the
pieces in the same order; `ToggleRange` now shares its add and its take-out with them. A take-out
that would leave nothing selected changes nothing, as the only selected cell cannot be taken out
(ADR-0052). The component (`ExGrid.HeadingDrag.cs`) maps a Ctrl press to the toggle, and a drag
from an added column or row grows that range with `ExtendToColumn` / `ExtendToRow`; a take-out
begins no drag, as none begins from a cell taken out (ADR-0012). On a plain header the press
selects nothing by itself, so Ctrl+click toggles at the click and Ctrl+drag adds once the pointer
reaches another column. Meta counts as Ctrl where the browser said Meta is Command, from the same
answer the cells read.

Layer 1: `tests/ExGrid.Tests/HeadingToggleTests.cs` (14 tests). Layer 2:
`tests/ExGrid.Components/HeadingCtrlClickTests.cs` (11 tests; 9 were red before the wiring, the
other two pin that Meta off an Apple platform is unchanged). Layer 3 is written and not yet run:
the `SR-2e/DC-42:` and `DC-42: Ctrl+click on a Row Heading` tests in
`tests/ExGrid.Browser/headings.spec.mjs` (/sheet), and the three `(SR-2e, ADR-0012)` tests in
`tests/ExGrid.Browser/sizing.spec.mjs` (a plain ExGrid). Where a column reorder is wired, Ctrl+click
works at the click, but a Ctrl+drag on the header reorders, as ticket 21 records.

The boxes are ticked on layers 1 and 2. SR-2e is verified on layer 3 as well, so the ticket stays
open until that run: `Status: done` waits for it.

2026-09-29, layer 3 run: the `SR-2e/DC-42:` and `DC-42: Ctrl+click on a Row Heading` tests of
`headings.spec.mjs` and the three `(SR-2e, ADR-0012)` tests of `sizing.spec.mjs` pass in Chrome on
macOS against both hosts; Edge and Linux are CI's. The Meta test takes its macOS branch there, where
Cmd+click adds the column. Done.
