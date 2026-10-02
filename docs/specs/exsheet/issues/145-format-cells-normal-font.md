# 145: Format Cells' *Normal font* box

Status: done

**What to build:** ADR-0071's "What was decided after Part C", *Normal font*. The Font tab gains the box
under both Chromes. Checking it sets the colour Automatic and every emphasis off, touched; it shows
checked exactly while every part is the default.

**Blocked by:** None (can start immediately)

- [x] **OK records the default Font on each selected cell**, over a row's or a column's Font (SH-55).
- [x] **Changing any part afterwards unchecks it.**
- [x] **Layer 2** under both Chromes.

## Comments

2026-10-02, claude/exsheet-part-c. `FormatCellsDraft.IsNormalFont` and `SetNormalFont`; the box in
`FormatCellsPanel` and `MudFormatCellsDialog`. Layer 2: `FormatCellsDraftTests`, `FormatCellsTests`,
`MudFormatCellsTests`.
