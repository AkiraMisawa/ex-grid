# The eighteenth Windows run: the Heading Mark

Questions gathered from `claude/heading-mark` for the next Windows run, to be folded into its spec when it is
written. ADR-0008's Heading Mark applies to every Selection. On 2026-10-14 the user also decided to build its
shade, coloured letters and all-selected corner triangle (D1), and to let the mark replace the outline's side
where the header or Row Headings cover it (D2). Both decisions are recorded in ADR-0008; neither is pending.

R4 and R5 of the readings of 2026-10-12 established the light shades and line. The cases they did not picture
are built by generalisation and remain readings below. A decision to build them is not an Excel observation.

1. **One cell, and one ordinary range.** R4 read several areas, and R5 whole columns and rows. On a blank sheet at
   Excel's 100% zoom on the 150% display: D4 alone, then D4:F7 (from D4). Read down the column Headings' bottom
   edge under C, D, F and G and across the row Headings' right edge at rows 3, 4, 7 and 8, as R4's `px.ps1` read
   the active cell's edges. What decides: that every Selection marks its Headings, as built; the line's width and
   colour.
2. **A display at 100%.** R4's three selections and question 1's on a display at 100%, if the desk has one: the
   line's width in device pixels (built: two at every scale, by generalisation from the 150% display).
3. **Where the two lines meet.** A1 alone, then A1:B2: read the device pixels where row 1's Heading's line meets
   column A's, under and beside the corner. R4 (2)'s picture showed the two lines joining there; ExGrid's stop at
   the corner, which stands above them.
4. **A range at the top or the left of the sheet.** A1:B2 as one range, and column C as a whole column (R5 (2)
   read B:D): down the column Heading's bottom edge into row 1, and across the row Heading's right edge into
   column A. R5 (2)'s picture showed one line of two device pixels there, the range's outline and the Heading's
   line in one place. D2 replaces the earlier four-device-pixel pairing: where a header or Row Heading covers
   the outline's side, the Heading Mark supplies its two Device Pixels without a second line beside it. Confirm
   that this remains Excel's appearance for A1 alone and A1:B2, and at both zooms. The outline stays inside at a
   pinned block's boundary or an edge without a Heading; those have no mark to replace that side.
5. **Frozen panes.** View, Freeze Panes on column B (Excel's Pinned Columns), then A3:D5 and a scroll right by
   three columns: whether the Headings of the frozen columns keep their line while the scrolled ones pass beneath.
6. **The Heading's shade and its letters.** R4 and R5 read selected Headings shaded `#E1E1E1` (from `#F0F0F0`),
   and those of a whole column or row `#D3F0E0`, with their letters in `#217346` (from `#444444`). D1 builds
   all three, including the corner's `#217346` triangle when every cell is selected (R5 (1)). Read the same
   cases at Excel's 100% and 150% zoom, with the ground sampled clear of the letters, and read Excel's Dark Gray
   and Black Office themes, whose Heading shades remain unobserved. ExSheet's light defaults follow the recorded
   colours; its dark Headings follow the frame's scheme, and a Theme or MudBlazor palette can supply the tokens.
