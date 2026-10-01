# 45: The engine holds Font, Fill and Border

Status: done

**What to build:** the engine's half of [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md). A Cell Format holds Font, Fill and Border beside the
Number Format and the Alignment. The new parts follow every rule the first two follow. The names
follow the glossary.

**Blocked by:** None (can start immediately)

- [x] **Rename to the glossary's words.** `ExSheet.Engine` is not published, so this breaks nobody.
  - `AxisStyle` → `AxisFormat`
  - `SheetEdit.SetStyle` → `SetCellFormat`
  - `Sheet.SetFormat` and its overloads → `SetNumberFormat`
  - `StyleEdit` and `StylesEdit` → `CellFormatEdit` and `CellFormatsEdit`
  - Comments say Cell Format, never "style" (`CONTEXT.md`).
- [x] **Types.**
  - A colour that is Automatic or RGB.
  - A Font: colour, bold, italic, single underline and strikethrough.
  - A Fill: one solid colour.
  - A Border side: one of the thirteen line styles, with a colour.
  - `CellFormatChange`: every part optional. Its borders are range-relative: outline, inside, top,
    bottom, left, right, inside horizontal, inside vertical, and none.
- [x] **Every part at three levels**, cell over row over column, with null meaning "inherit"
      (SH-38).
- [x] **A change's borders apply to each range**, so several ranges get their own outlines (SH-44).
  - Whether setting a side also writes the neighbour's is a reading until the eleventh Windows run
    (case 7). Build it as each cell's own side only. Name the case in the test.
- [x] **Structure.** Insertion and deletion move the new parts. An inserted row or column copies the
      one before it, as today; for borders this is a reading until case 12.
- [x] **Copying and editing.**
  - An ExSheet-to-ExSheet copy (`SheetBlock`) carries the new parts.
  - Ctrl+D, Ctrl+R and the fill handle carry them.
  - Delete keeps them.
  - Undo restores them.
- [x] **The Sheet Document**, at a new version, records Font, Fill and Border for cells, rows and
      columns (SH-38, ADR-0048).
  - A document of an older version reads with none.
  - A colour of another kind is refused by name.
  - Version 7 is taken on `claude/exsheet-pointing-scope`, so take the next free number at merge.
- [x] Layer 1 tests, named with ADR-0071 and SH-38 or SH-44.

## Comments

*(2026-10-01, built.)* A Cell Format holds Font, Fill and Borders beside the Number Format and the
Alignment, and each new part follows the rules the first two follow.

- **Renames.** `AxisStyle` is `AxisFormat`; `SheetEdit.SetStyle` is `SetCellFormat`, which takes a
  `CellFormatChange`; `Sheet.SetFormat` and `SheetEdit.SetFormat` are `SetNumberFormat`;
  `StyleEdit` and `StylesEdit` are `CellFormatEdit` and `CellFormatsEdit`; `GetFormat`,
  `GetRowFormat` and `GetColumnFormat` are `GetNumberFormat`, `GetRowNumberFormat` and
  `GetColumnNumberFormat`; `SheetDocumentAxisStyle` is `SheetDocumentAxisFormat`; and a property
  holding a Number Format is `NumberFormat`, not `Format`. `Sheet.Styles.cs` is
  `Sheet.CellFormat.cs`. The case corpus's `style` action is `cellFormat`. `SheetBlock.FormatAt`
  and `AlignmentAt` became `CellFormatAt`, which answers the whole Cell Format.
- **Types.** `CellColour` (Automatic, or RGB as `0xRRGGBB`); `CellFont` (colour, bold, italic,
  underline, strikethrough); `CellFill` (`None`, or `Solid` in an RGB colour); `BorderLineStyle`
  (the thirteen, and `None`); `BorderLine` (a line style and a colour); `CellBorders` (the four
  sides); `CellFormat` (a cell's Cell Format as it shows); `CellFormatChange` (every part optional,
  each Font emphasis on its own); `BorderChange` (top, bottom, left, right, inside horizontal,
  inside vertical, and the presets `Outline`, `Inside` and `None`). `Sheet.GetCellFormat`,
  `GetFont`, `GetFill` and `GetBorders` answer cell over row over column.
- **A Font or Borders is patched**, so a change that sets bold keeps each cell's italic. Each cell
  takes the change applied to what it showed. Formatting whole rows over a column that records a
  Font or Borders gives the cells where they cross a record of their own, so they keep the column's
  part. Over the whole Sheet, a row's own Font and Borders are patched, not cleared.
- **The Sheet Document** is version 7, which reads versions 1 to 6 with no Font, Fill or Borders.
  Version 7 is also taken on `claude/exsheet-pointing-scope`, so a comment at the constant says the
  number is fixed at merge. A cell, row or column may record `"font"` (an object with `"color"`,
  `"bold"`, `"italic"`, `"underline"`, `"strikethrough"`, only where they differ from the default),
  `"fill"` (`"none"` or `"#RRGGBB"`) and `"borders"` (`"top"`, `"bottom"`, `"left"`, `"right"`, each
  `{"style": …, "color": …}`, with the style named as Excel's file names it, such as `"thin"` or
  `"slantDashDot"`). A colour is `"automatic"` or `"#RRGGBB"`, and an Automatic colour is not
  written. Any other kind of colour, such as a theme object, `"red"` or `"#F00"`, is refused, and
  the refusal names it.
- **`src/ExSheet`** behaves as before. `FormatSelectionAsync` sends a `CellFormatChange`, and
  `SheetRow` reads `GetNumberFormat`.
- **Readings, provisional until the eleventh Windows run:**
  - Case 7: setting a side writes the cell's own side only. An outline, Inside or None on a range
    never touches the neighbour's side of the same edge.
  - Case 12: an inserted row or column copies the Borders of the one before it, as it copies the
    rest of the Cell Format.
  - Case 15: a whole column's outline records its left and right sides on the column, and the top
    of its first cell and the bottom of its last on those cells. A level is the same the whole
    length of its column. Whole rows are the same the other way round. Over the whole Sheet, an
    outline therefore records the top and bottom rows' 16,384 cells each.
- **Interpretations, not readings:**
  - A Fill's colour is RGB. `CellFill.Solid` refuses Automatic, and so does the document, because
    Excel's Fill offers No Color and colours, never Automatic.
  - The document's key is `color`, spelt as in Excel's file and as the alignment's `"center"` is.
- **For tickets 48 and 49:** `SheetChange.Rows` still names only rows that hold a cell. A whole
  column's or row's Fill or Borders paints empty cells too, so the painting tickets need the window
  repainted when a level changes.
- **Tests:** 67 new cases in `CellFormatTests`, `CellFormatCarryTests` and
  `CellFormatDocumentTests` (layer 1), named with ADR-0071 and SH-38 or SH-44. ExSheet.Engine.Tests
  2036, ExSheet.Components.Tests 290, ExGrid.Tests 826, ExGrid.Components 1062 (one skipped) and
  ExGrid.MudBlazor.Tests 88, all passing.

*(2026-10-01, orchestrator, at the merge of `claude/exsheet-start-8cx3v1`.)* Pointing Scope reached the
base branch first with version 7 for a Linked Table's key, so the Cell Format parts are version 8:
`font`, `fill` and `borders` are read from version 8, and a version 7 document holding them is refused.
