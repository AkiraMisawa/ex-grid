# The eighteenth Windows run: Format Cells' readings

Questions gathered from `claude/run17gh-readings` (the seventeenth run's Part H and the readings of
2026-10-12) for the next Windows run, to be folded into its spec when it is written.

Each in a new workbook at 100% zoom, the lines set through COM on each cell's own sides unless it says otherwise, then
the cells selected, Ctrl+1, and the Border tab (Ctrl+Tab three times from Number, or the tab pressed). **Read**: the
Style list's selected entry (its focus rectangle; a picture of the list enlarged, as R8 took), the Colour box, and
which edge buttons are pressed. Escape afterwards, and read back that every line is as set.

1. **One line on a left or right edge.** (a) B2's left thick, nothing else. (b) C2's left thick and B2 recording
   nothing; select B2 (its right shows C2's line, if case 13's neighbour rule holds sideways; read whether Right is
   pressed). **Decides**: Thick in both extends what was seen on a top and a bottom edge to the left and the right;
   Thin in either limits the rule to what was seen (top and bottom), and ExSheet follows.
2. **Two edges with lines, none thin.** B2 with a thick top and a medium right of its own. **Decides**: Thin keeps the
   rule; Thick means the top's line opens (and R8 (1)'s Thin was the top's); Medium means the right's, or the last
   edge's.
3. **The same line on two edges.** B2 with a thick top and a thick bottom. **Decides**: Thin keeps "exactly one edge";
   Thick means the rule is "one line shown", whatever the number of edges.
4. **A range whose edges show one line.** A1:B1's bottom thick; select A2:B2 (its top shows the thick line, the rest
   nothing). **Decides**: Thin keeps the rule to one cell; Thick extends it to a range.
5. **Several one-cell ranges.** A1's and C1's bottom thick; A2, then Ctrl+click C2. **Decides** as 4, for a Selection
   of several ranges.
6. **A line in another colour or style.** (a) A1's bottom thick red (`#FF0000`); select A2. (b) A1's bottom medium
   dashed, Automatic; select A2. **Decides**: red in the Colour box in (a), and Medium Dashed in the Style list in
   (b), confirm that each follows the line, as Thick, Thin, black and Automatic were seen to; anything else is a
   difference to record.
7. **Ticket 102: both cells record a line on one edge.** The fourteenth run's case 12: C2's left thin blue; E5's right
   thick red, E5 = 1; E5, Ctrl+C, B2, Ctrl+V. Save, and read the file: B2 records a thick right `FFFF0000` and C2 a
   thin left `FF0000FF`. Then C2, Ctrl+1, Border. **Read** whether Left is pressed, the preview's left line (thick red
   or thin blue), and the Style and Colour on opening. Also over B2 (its right). **Decides** ticket 102's reading (the
   drawn line, B2's thick red, shows over C2) and which line the tab opens on where two records lie on one edge.
8. **h10's second case over A1.** A1's bottom thick, then A2's top thin through COM (one thin line on the edge); select
   A1, Ctrl+1, and show the Border tab before the picture. **Read** the Bottom button, the preview and the Style.
   **Decides**: the run's report says the dialog over A1 showed the thin line, but its picture shows the Number tab.
9. **Ticket 103: a code of its own reopened under en-US.** Under en-GB, A1 = 46027, `d-mmm-yy` typed into Custom, OK;
   save as `.xlsx` and read A1's `numFmt` (a custom id, 164 or above, not 15). Set-Culture en-US, a new Excel, open the
   file, A1, Ctrl+1: **read** the Number tab's category and Type. Then choose Custom, OK without a change, save, and
   read A1's `numFmt` again. **Decides** the reading that the code shows as plain `d-mmm-yy` under en-US and becomes
   built-in 15 once the Number tab is touched and OK pressed.
10. **Ticket 103: month names under zh-CN and ko-KR.** Set-Culture zh-CN, a new Excel, A1 = 46027, Custom `d-mmm-yy`,
    then `mmm`, then `mmmm`; **read** the cell's text each time. The same under ko-KR, then put the regional format
    back. **Decides** whether ExSheet keeps ICU's names there (`1月`, `1월`) or needs Windows', as it took for ja-JP.
