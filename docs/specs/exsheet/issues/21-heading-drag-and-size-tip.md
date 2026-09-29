# 21: A drag across Headings, and the Size Tip

Status: done

**What to build:** ADR-0050, item 1 (added 2026-09-29), ADR-0012 ("a drag and Ctrl+click on the
header", 2026-09-29) and ADR-0052 (the Size Tip). A drag across Column Headings or Row Headings
selects whole columns or rows, in ExSheet and on a plain ExGrid's column header alike. While it
covers more than one, its size shows in the Size Tip at the Extent's Heading and the Name Box is
empty.

**Blocked by:** None (can start immediately). Ticket 20 changes the reveal the drag uses; whichever
lands second takes the other's change.

- [x] Declared (`HeaderClickSelects`, `RowHeadings`): the press selects at once; each move puts the
      Extent on the column (row) under the pointer; the range is whole columns (rows) from the
      pressed one; the Focus stays where the press put it (DC-42)
- [x] The pointer over the cells keeps it a Heading drag: only its column (row) counts (DC-42)
- [x] Shift+press extends from the Focus's column (row), and the drag goes on (DC-42)
- [x] The edge band auto-scrolls along the Heading's axis only (DC-43)
- [x] Plain ExGrid: a press on a header selects nothing; released on the same column having
      crossed no other, it sorts; reaching another column's header or cells makes it a drag that
      selects whole columns and never sorts, even released back over the pressed column (SR-2d)
- [x] The browser's `click` on the common ancestor after a press and a release on different headers
      does not sort (SR-2d)
- [x] The Size Tip: with `NameBoxSizeLabel` supplied and the drag over more than one column or row,
      the label at the Extent's Heading, inside the grid's box, and the Name Box empty; otherwise
      none, and the Name Box names the Focus. Redrawn only when the Extent changes Heading (DC-44)
- [x] The Size Tip's class and Visual Tokens are added to the presentation surface (ADR-0029), and
      the MudBlazor Chrome paints it from the same tokens
- [x] No JavaScript is added (DC-15)
- [x] A drag on a column's resize grip still resizes, and is not a Heading drag

## Comments

The Row Heading press path today deliberately starts no drag: "a drag's moves extend by cell, and
would collapse the whole rows" (`ExGrid.razor`, the Row Heading branch of the press handler). A
Heading drag is therefore its own drag mode, whose moves call `ExtendToColumn` / `ExtendToRow`,
not the cell drag's.

The header's click handler reads the column from the pointer's offset in the band. After a press
on one header and a release on another, the browser fires `click` on their common ancestor, so the
handler would sort the column under the release. The press has to be remembered, and a click that
ends a drag ignored.

2026-09-29, built. A Heading drag is its own drag mode, in `src/ExGrid/Components/ExGrid.HeadingDrag.cs`,
wired into `ExGrid.razor`'s press, move, release, edge-tick and header-click handlers. The press
maps to `SelectColumn` / `SelectRow` (`ExtendToColumn` / `ExtendToRow` with Shift) and every move,
over the header or over the cells, to `ExtendToColumn` / `ExtendToRow`, so no transition is new.
The header carries its own move and release handlers only while a Heading gesture runs, as the
Viewport's move handler is attached only during a drag (ADR-0008). On a plain header the press is
remembered; the drag begins when a move, the release or the click lands on another column, and
from then on the click is swallowed. The click itself also finishes a press whose release was not
heard, which on a circuit can outrun the handler that hears it: a click on another column than
the one pressed is a drag's tail and does not sort. The edge band zeroes the other axis. The Size
Tip is an element under the root, outside the scroll container like a popover, placed from the
geometry at the Extent's Heading and sized by the Cell Metrics' estimate of its text, written
inline, and clamped inside the grid's box; it is `aria-hidden` paint and takes no pointer. Its
class is `ex-size-tip` and its tokens are `--ex-size-tip-background`, `--ex-size-tip-color` and
`--ex-size-tip-outline` (`ex-grid.css`); the MudBlazor Wrapper maps them onto the colours MudBlazor
gives its tooltips (`mud-ex-grid.css`). A scroll during a Heading drag re-renders the root when a
Size Tip stands, so the tip keeps to its Heading.

Layer 2: `tests/ExGrid.Components/HeadingDragTests.cs` (29 tests; 26 were red before the change)
and `tests/ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs`. Layer 3 is written and not yet run:
the `DC-42:`, `DC-43:` and `DC-44:` tests in `tests/ExGrid.Browser/headings.spec.mjs` (/sheet), and
the `(SR-2d, …)` tests and the resize-grip test in `tests/ExGrid.Browser/sizing.spec.mjs` (a plain
ExGrid).

Left open, each raised with the orchestrator:

- **A plain header where a column reorder is wired.** ADR-0011 makes a header drag a reorder, and
  ADR-0012's addition of 2026-09-29 makes it a Heading drag; neither says which wins where the
  Consumer wires `OnColumnOrderChanged`. There the header is left as it was: a drag reorders, and
  the click sorts or, declared, selects. The Plain ExGrid box stays open for that case alone.
- **The presentation surface.** The class and the three tokens exist and are painted, but
  ADR-0029's lists are the presentation surface, and an agent does not edit an ADR. That box stays
  open until the orchestrator enters `ex-size-tip` and its tokens there (and says whether the class
  is stable or internal).

2026-09-29, layer 3 run: the `DC-42:`, `DC-43:` and `DC-44:` tests of `headings.spec.mjs` and the
`(SR-2d, …)` and resize-grip tests of `sizing.spec.mjs` pass in Chrome on macOS against both hosts
(Edge and Linux are CI's). The first run caught a product defect the layer 2 tests could not: the
DC-44 test counted four writes to the Size Tip while the pointer went along one column. A move over
a column's resize grip reports offsets measured from the grip, and the header's handler read them
as the header's, so the Extent jumped to column A and back. Every event of a Heading gesture is now
placed by its client delta from the press, whatever element it fired on — as the resize and the
reorder already read ClientX deltas — and the rows are read through the mapping as the edge band's
tick reads them. `HeadingDragTests.Over_a_resize_grip_the_pointer_places_the_extent` pins it (red
before the fix). The other first-run failures were the tests': a locator that matched both ranges
after a Ctrl+click, and a sort status the page writes in lower case. The two boxes above stay open
for the reasons given.

2026-09-29, decided with the user (orchestrator): where a column reorder is wired, a header drag stays
the reorder, and Shift+click and Ctrl+click still select (ADR-0012, the same day's addition; SR-2d).
That is what was built, so the Plain ExGrid box closes on it. `ex-size-tip` is a stable class and
its three tokens are Visual Tokens (ADR-0029, "Added by ADR-0052's Size Tip").
