# 143: The Selection on the Paper looks like Excel's

Status: done

**What to build:** ADR-0071's "What was decided after Part C", the Selection. On a Sheet's Paper the
outline and the Focus's are 2 Device Pixels, on the gridline and one outside it, with a white line of
one Device Pixel inside; the shade multiplies with the cells, so a black line stays black. Built-in:
`#217346` and `#C7C7C7`. `ExSheet.MudBlazor`: the palette's primary, and the primary mixed into white.
The white line is not drawn while an edit is open. ExGrid's own selection is unchanged.

**Blocked by:** 140

- [x] **SH-49's readings** at 100% and 150%, under both Chromes.
- [x] **DC-59 as amended**: a line lies above the shade, below the outline.
- [x] **No new element per cell, and nothing per cell reaches JavaScript** (ADR-0008, ADR-0027).

## Comments

2026-10-02, claude/exsheet-part-c. `ex-sheet.css`: the outline's width is `--ex-outline-width`
(2 × `--ex-dp`), a new core token that defaults to 2px; the white line is an inset box-shadow dropped
under `.ex-editing`; the scrollable layer's shade multiplies, black at 22% by default. Layer 3:
`part-c.spec.mjs` under both Chromes, at 100% and a real 150%; `sheet-paper.spec.mjs`'s SH-39 takes
the `#C7C7C7` shade. Over a Pinned Column the shade still lies over the lines (owed, ADR-0071).
