# 48: ExSheet paints Font and Fill on white Paper

Status: ready-for-agent

**What to build:** the component's half of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "What a Cell Format holds" and "Paper and Ink".

**Blocked by:** 45, 46, 47

- [ ] **ExSheet declares ADR-0050 item 15** from the engine's Cell Format. A Number Format's colour
      (ticket 46) takes the Font colour's place.
- [ ] **The Paper and Ink tokens** are `--ex-sheet-paper` and `--ex-sheet-ink`, with Excel's white
      and black as defaults in every scheme (SH-39).
  - On the Paper, the Focus, the Selection, Reference Outlines (with the pointed shade) and the Cell
    Editor keep their light-scheme appearance.
  - The Headings, the Name Box, the Formula Bar and popovers follow the scheme.
  - ADR-0027's note of 2026-09-30 permits this exception. Say in a comment how it is done.
- [ ] `ExGrid.MudBlazor`'s stylesheet does not set the Paper tokens (inspect, SH-39).
- [ ] A Fill or Font on a whole row or column paints cells that hold nothing, so a change to a
      level repaints every painted row it covers. `SheetChange.Rows` lists only rows that hold a
      cell, so it does not cover them (found by ticket 45).
- [ ] A row repaints only when its Values or its Cell Format changed (SH-4, DC-58).
- [ ] **Layer 3** under the light and the dark scheme, under both Chromes, on both hosts: recorded
      colours read as recorded, and a clean console.
