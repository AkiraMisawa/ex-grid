# 45: The engine holds Font, Fill and Border

Status: ready-for-agent

**What to build:** the engine's half of [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md). A Cell Format holds Font, Fill and Border beside the
Number Format and the Alignment. The new parts follow every rule the first two follow. The names
follow the glossary.

**Blocked by:** None (can start immediately)

- [ ] **Rename to the glossary's words.** `ExSheet.Engine` is not published, so this breaks nobody.
  - `AxisStyle` → `AxisFormat`
  - `SheetEdit.SetStyle` → `SetCellFormat`
  - `Sheet.SetFormat` and its overloads → `SetNumberFormat`
  - `StyleEdit` and `StylesEdit` → `CellFormatEdit` and `CellFormatsEdit`
  - Comments say Cell Format, never "style" (`CONTEXT.md`).
- [ ] **Types.**
  - A colour that is Automatic or RGB.
  - A Font: colour, bold, italic, single underline and strikethrough.
  - A Fill: one solid colour.
  - A Border side: one of the thirteen line styles, with a colour.
  - `CellFormatChange`: every part optional. Its borders are range-relative: outline, inside, top,
    bottom, left, right, inside horizontal, inside vertical, and none.
- [ ] **Every part at three levels**, cell over row over column, with null meaning "inherit"
      (SH-38).
- [ ] **A change's borders apply to each range**, so several ranges get their own outlines (SH-44).
  - Whether setting a side also writes the neighbour's is a reading until the eleventh Windows run
    (case 7). Build it as each cell's own side only. Name the case in the test.
- [ ] **Structure.** Insertion and deletion move the new parts. An inserted row or column copies the
      one before it, as today; for borders this is a reading until case 12.
- [ ] **Copying and editing.**
  - An ExSheet-to-ExSheet copy (`SheetBlock`) carries the new parts.
  - Ctrl+D, Ctrl+R and the fill handle carry them.
  - Delete keeps them.
  - Undo restores them.
- [ ] **The Sheet Document**, at a new version, records Font, Fill and Border for cells, rows and
      columns (SH-38, ADR-0048).
  - A document of an older version reads with none.
  - A colour of another kind is refused by name.
  - Version 7 is taken on `claude/exsheet-pointing-scope`, so take the next free number at merge.
- [ ] Layer 1 tests, named with ADR-0063 and SH-38 or SH-44.
