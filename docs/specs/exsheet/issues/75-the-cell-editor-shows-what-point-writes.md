# 75: The Cell Editor shows what Point writes

Status: ready-for-agent

**What to build:** ADR-0058, "What the thirteenth Windows run settled", the defect seen and not asked.
After `Home` or Shift+→ over Point wrote `A10` or `D10:E10` (b3–b5 of the thirteenth run), the Cell
Editor's own `scrollLeft` stayed where it was (81.3 px), and the caret and the written Reference lay 24
to 50 px past its right edge, in all six configurations. The user saw a Formula that did not show what
had just been written.

**Blocked by:** None.

- [ ] After Point writes into the Cell Editor or the Formula Bar — by an arrow, `Home`, a Shift+arrow, a
      press on the Sheet, or a press on a registered grid — the field's caret is inside its visible
      width, so the written Reference can be read. Find which of these paths leave the field's
      `scrollLeft` behind, and fix them all, not only the two the run saw (ADR-0051, ADR-0058)
- [ ] The field's scroll is set through the scroll offsets ADR-0021 allows, at the caret the core
      placed, without measuring text (no layout read on the path to a paint). If the caret cannot be
      brought into view without measuring, stop and report the proposal: that is a decision
- [ ] Reference Outlines' text layer (ADR-0057) follows the field's `scrollLeft`, as it does when the
      user types, so the colours stay over the right characters (DC-48)
- [ ] Layer 3 on `/sheet` under both Chromes, on both hosts: `=XLOOKUP(1,A2:A4,B2:B4,,` in D10, then
      `Home`; and then Shift+→ instead: the caret's position lies within the editor's client width, and
      the coloured layer matches the field's `scrollLeft`

## Comments
