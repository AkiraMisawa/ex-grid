# 46: A Number Format's colour reaches the display

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) and ADR-0047's note of 2026-09-30. A named colour in a Number Format
(`[Red]` and the seven others) is painted, and it wins over the Font colour.

**Blocked by:** None (can start immediately)

- [ ] Formatting a Value answers the colour of the section it used, with the text: one of the eight
      names, or none (SH-40).
- [ ] The display the component reads (`Sheet.GetDisplay` and the painted-text path) carries that
      colour.
- [ ] The eight names map to RGB in one table, as Excel's legacy palette. This is a reading until the
      eleventh Windows run, case 1. Name the case in the test.
- [ ] The Number Format's colour wins over the Font colour. This is a reading until case 2.
- [ ] `[ColorN]` stays refused by name.
- [ ] Layer 1.
