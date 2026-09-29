# 21: A drag across Headings, and the Size Tip

Status: ready-for-agent

**What to build:** ADR-0050, item 1 (added 2026-09-29), ADR-0012 ("a drag and Ctrl+click on the
header", 2026-09-29) and ADR-0052 (the Size Tip). A drag across Column Headings or Row Headings
selects whole columns or rows, in ExSheet and on a plain ExGrid's column header alike. While it
covers more than one, its size shows in the Size Tip at the Extent's Heading and the Name Box is
empty.

**Blocked by:** None (can start immediately). Ticket 20 changes the reveal the drag uses; whichever
lands second takes the other's change.

- [ ] Declared (`HeaderClickSelects`, `RowHeadings`): the press selects at once; each move puts the
      Extent on the column (row) under the pointer; the range is whole columns (rows) from the
      pressed one; the Focus stays where the press put it (DC-42)
- [ ] The pointer over the cells keeps it a Heading drag: only its column (row) counts (DC-42)
- [ ] Shift+press extends from the Focus's column (row), and the drag goes on (DC-42)
- [ ] The edge band auto-scrolls along the Heading's axis only (DC-43)
- [ ] Plain ExGrid: a press on a header selects nothing; released on the same column having
      crossed no other, it sorts; reaching another column's header or cells makes it a drag that
      selects whole columns and never sorts, even released back over the pressed column (SR-2d)
- [ ] The browser's `click` on the common ancestor after a press and a release on different headers
      does not sort (SR-2d)
- [ ] The Size Tip: with `NameBoxSizeLabel` supplied and the drag over more than one column or row,
      the label at the Extent's Heading, inside the grid's box, and the Name Box empty; otherwise
      none, and the Name Box names the Focus. Redrawn only when the Extent changes Heading (DC-44)
- [ ] The Size Tip's class and Visual Tokens are added to the presentation surface (ADR-0029), and
      the MudBlazor Chrome paints it from the same tokens
- [ ] No JavaScript is added (DC-15)
- [ ] A drag on a column's resize grip still resizes, and is not a Heading drag

## Comments

The Row Heading press path today deliberately starts no drag: "a drag's moves extend by cell, and
would collapse the whole rows" (`ExGrid.razor`, the Row Heading branch of the press handler). A
Heading drag is therefore its own drag mode, whose moves call `ExtendToColumn` / `ExtendToRow`,
not the cell drag's.

The header's click handler reads the column from the pointer's offset in the band. After a press
on one header and a release on another, the browser fires `click` on their common ancestor, so the
handler would sort the column under the release. The press has to be remembered, and a click that
ends a drag ignored.
