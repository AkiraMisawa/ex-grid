# The eighteenth Windows run: the fill handle on a whole axis

Questions gathered from `claude/run17gh-fill-handle-axis` (the seventeenth run's h3 and the readings of
2026-10-12, R5) for the next Windows run, to be folded into its spec when it is written. Each is a reading
until a run has observed it; none is written as Excel's.

Each in a new workbook at 100% zoom, the pictures cut to the cells and the Headings, as R5's were.

1. **Every cell, at the far corner.** Ctrl+A on an empty sheet, then `XFD1048576` in the Name Box
   and Enter, Ctrl+A again, Ctrl+Backspace: is a handle drawn at XFD1048576? Decides whether "none
   anywhere" stays (any drawn there would be a difference to note; it could not be dragged anywhere).
2. **The Extent at the first column.** Press D's Heading, Shift+press B's: is the handle at D's
   top-right (the last column, as ExGrid has it) or B's? The same for rows 4 then 2.
3. **A whole row with column A scrolled away.** Row 3 from its Heading, then the horizontal scroll
   bar's right arrow three times: is the handle drawn at the left of the part in view (as ExGrid has
   it, by generalisation), or gone?
4. **A lone number in a range that is not a whole axis.** C1 `1`, C2 `5`; select C1:C2 and drag the
   handle two columns right: D1:E1 `1 1` or `2 3`, D2:E2 `5 5` or `6 7`? Then C1 `1` with C2 empty,
   the same. Decides whether ExSheet's series by one reaches every source of several cells (today
   only whole columns or rows; one cell copies, as R5's control showed).
5. **A whole axis with a lone number beside a blank in a line.** Columns B:C with B1 `1` and C1
   empty, dragged right to E: what do D1 and E1 hold? ExSheet refuses ("numbers mixed with other
   cells"); a value there is a pattern to implement.
6. **A whole column's format.** C1 `1` and nothing else in C, C:C bold (Ctrl+B on C's Heading),
   dragged right to E: D100 and E100 bold? Then read `ActiveSheet.UsedRange.Address` through COM, or
   press Ctrl+End. A format written on the cells down every row takes the used range to row 1048576,
   and a column's format does not; alternatively, save as .xlsx and read the `<cols>` and `<c>`
   entries of `xl/worksheets/sheet1.xml`. (Inserting a row cannot tell them apart: Excel gives an
   inserted row the format of the row above either way.)
7. **Left and up, and a date.** Column C (C1 `5`) dragged left to A: B1 `4`, A1 `3`? Column C with
   C1 `9/26/2026`, dragged right: D1 `9/27/2026`? Both are ExSheet's by generalisation.
