# 46: A Number Format's colour reaches the display

Status: done

**What to build:** [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) and ADR-0047's note of 2026-09-30. A named colour in a Number Format
(`[Red]` and the seven others) is painted, and it wins over the Font colour.

**Blocked by:** None (can start immediately)

- [x] Formatting a Value answers the colour of the section it used, with the text: one of the eight
      names, or none (SH-40).
- [x] The display the component reads (`Sheet.GetDisplay` and the painted-text path) carries that
      colour.
- [x] The eight names map to RGB in one table, as Excel's legacy palette. This is a reading until the
      eleventh Windows run, case 1. Name the case in the test.
- [x] The Number Format's colour wins over the Font colour. This is a reading until case 2.
- [x] `[ColorN]` stays refused by name.
- [x] Layer 1.

## Comments

*(2026-10-01, built.)* Formatting a Value answers, with its text, the colour its section names:
a `NumberFormatColour` (Black, Blue, Cyan, Green, Magenta, Red, White, Yellow), or none.
`CellDisplay.Colour` carries it from both `Sheet.GetDisplay` overloads, so the display at a column's
width, which the component paints, carries it too. `NumberFormatColours.Rgb` is the one table of
Excel's legacy palette, as `0xRRGGBB`. `src/ExSheet` is unchanged: painting is ticket 48.

- **The precedence over the Font colour** is stated on `CellDisplay.Colour` and on
  `NumberFormatColour`. The engine has no Font until ticket 45, so what is tested is the engine's
  half of case 2: -5 in `0;[Red]-0` answers Red, and 5 answers none, so the Font colour shows there.
  Ticket 48 paints it.
- **`[ColorN]`** is still refused, and the test asserts that the reason names a numbered colour.
- **Readings, provisional until Excel is asked:**
  - Each name's RGB (the eleventh Windows run, case 1) and the precedence (case 2).
  - "The section it used" is read literally, so there is no colour where no section shows the
    Value: General, booleans, Error Values, text in a format with no text section (text in
    `[Red]0`), and a number shown as General beside a lone text section (`[Red]@`). The run does not
    ask these yet. Cases for text and for `TRUE` in `[Red]0` would settle them.
  - A number its section cannot show (`####`, in the column's width or at any width) keeps that
    section's colour, so the `####` is painted in it. The run does not ask this yet either.
- **Tests**: 45 new cases in `NumberFormatColourTests` (layer 1). ExSheet.Engine.Tests 2014,
  ExSheet.Components.Tests 290, ExGrid.Tests 826, ExGrid.Components 1062 (one skipped),
  ExGrid.MudBlazor.Tests 88, all passing.
