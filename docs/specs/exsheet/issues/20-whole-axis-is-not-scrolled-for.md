# 20: An axis the range spans end to end is not scrolled for

Status: ready-for-agent

**What to build:** ADR-0052, "What the user's run settled" (2026-09-29). With the view at the top,
a Column Heading click puts the Focus on row 1, the range's top edge, so the Extent is the column's
last row. Shift+→ then moves the Extent to the last row of the next column, and the reveal scrolls
the Sheet to its bottom. Excel and Google Sheets both leave the view where it was. **While
extending, keep the Extent in view only on an axis the range holding the Focus does not span end
to end**, judged on the range after the move. The Extent and the Selection are unchanged: this is
a rule about the view.

The same state is reached in a plain ExGrid by Shift+click on a header or Ctrl+Space on row 1, and
on the other axis by a Row Heading click with the view at the left (Shift+↓ scrolls to the last
column today). All of them take the rule.

**Blocked by:** None (can start immediately)

- [ ] Shift+→ / Shift+← over whole columns never scrolls vertically; Shift+↓ / Shift+↑ over whole
      rows never scrolls horizontally; over the whole grid neither (SR-2c)
- [ ] Shift+↑ from C:C selected on row 1 leaves C1:C(last − 1) and scrolls to show the Extent, as
      Excel does (SR-2c)
- [ ] Every extension that reveals the Extent takes the rule: Shift+arrow, Ctrl+Shift+arrow,
      Shift+Home/End, Shift+PageUp/PageDown, Shift+click, and a drag
- [ ] The Focus reveal (non-extending moves), Ctrl+Backspace and the Consumer's placement are
      unchanged
- [ ] Layer 3: with the view at the top, a Column Heading click then Shift+→ leaves `scrollTop`
      where it was, on both hosts

## Comments

Where it goes wrong today: `GridSelection.ExtentOf` takes the corner opposite the Focus, which for a
whole column with the Focus on row 1 is the last row; the reveal in `ExGrid.razor` (the
`_revealTarget` path) then reveals both axes of that cell. The fix belongs in the reveal, not in
`ExtentOf`: Shift+↑ from the same state must still move the Extent from the last row.
