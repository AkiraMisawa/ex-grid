# 143: The Selection on the Paper looks like Excel's

Status: ready-for-agent

**What to build:** ADR-0071's "What was decided after Part C", the Selection. On a Sheet's Paper the
outline and the Focus's are 2 Device Pixels, on the gridline and one outside it, with a white line of
one Device Pixel inside; the shade multiplies with the cells, so a black line stays black. Built-in:
`#217346` and `#C7C7C7`. `ExSheet.MudBlazor`: the palette's primary, and the primary mixed into white.
The white line is not drawn while an edit is open. ExGrid's own selection is unchanged.

**Blocked by:** 140

- [ ] **SH-49's readings** at 100% and 150%, under both Chromes.
- [ ] **DC-59 as amended**: a line lies above the shade, below the outline.
- [ ] **No new element per cell, and nothing per cell reaches JavaScript** (ADR-0008, ADR-0027).
