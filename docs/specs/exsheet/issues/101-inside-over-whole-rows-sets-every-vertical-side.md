# 101: Inside over whole rows sets every vertical side

Status: done

**What to build:** the fourteenth Windows run's case 14. Inside over rows 3:4 set the line between the
two rows, the lines between columns, XFD's right **and A's left**. Excel records it as a row format: row 3
holds left, right and bottom, and row 4 left, right and top. ADR-0071 read A's left as not set.
ADR-0071, "What the fourteenth Windows run settled", ticket 57's model.

**Blocked by:** None (can start immediately)

- [x] **Inside (and Inside vertical) over whole rows sets A's left**, as it sets every other vertical
      side and XFD's right. `CellFormatAt` answers it on A3 and A4.
- [x] **Inside over the whole Sheet is unchanged** (case 15 confirmed it), and so are an outline over
      whole rows (the twelfth run's case 14) and Inside over whole columns.
- [x] **The Sheet Document round-trips it**, and one undo step takes it back.
- [x] **Layer 1/2.**

## Comments

*(2026-10-02, agent cf-100.)* Built in the engine.

- **Where.**
  - `BorderChange.On(CellRange)` is new and internal (`src/ExSheet.Engine/CellFormatChange.cs`).
    Over whole rows short of the whole Sheet, a change that sets no left edge takes the inside
    vertical line as its left edge.
  - `Sheet.ApplyCellFormat` sets every change through it.
  - Over whole rows, the left of column A is the range's left edge (`PlaceInRange.Of`), so it now
    takes the inside line. The row level already gave every other vertical side and XFD's right the
    same line.
- **How it is recorded.** As Excel's file records it: row 3 holds left, right and bottom, and row 4
  left, right and top. No cell is given a record.
  - Before, `CellsGivenTheirOwn` gave each row's cell in column A a record that kept its left as it
    was. Under Inside the left of column A now takes the inner sides' line, so that record is no
    longer made.
  - A cell in column A that holds an Entry, or recorded a left of its own, takes the line too. It
    records nothing it would show anyway.
- **Unchanged.**
  - An outline over whole rows (the twelfth run's case 14) still sets A's left from its left edge,
    recorded on column A's cells.
  - Where a change sets both a left edge and an inside vertical line, A's left takes the left edge,
    as before.
  - `On` leaves whole columns and the whole Sheet alone. Inside over the whole Sheet already set every
    side (case 15).
- **No public signature changed.** `BorderChange`'s remarks and `PlaceInRange.Of`'s doc comment now
  say what Inside sets over whole rows and over the whole Sheet.
- **Tests.**
  - **Layer 1** (`CellFormatTests`):
    - Inside over 3:4: the rows' records, `GetCellFormat` on A3 and A4, XFD's right, the Sheet
      Document round trip, and one undo (case 14-14).
    - Inside vertical alone over 3:4, on a column-A cell that holds an Entry and one with a left of
      its own (case 14-14).
    - A left edge and an inside line together over whole rows (cases 12-14 and 14-14).
    - Inside over the whole Sheet (case 14-15).
    - The first two fail with the change taken out. The other two pass either way and pin what
      stays.
  - **Layer 2** (`FormatCellsTests`): Format Cells over 3:4, Border, Inside, OK. It checks
    `CellFormatAt` on A3, A4 and XFD4, the rows' records, and one undo step.
  - **Layer 3.** No spec asserted that A's left is not set, so none changed. It was not run, as
    briefed.
  - **Layers 1 and 2** (`dotnet test ExGrid.slnx`): the counts are in ticket 100's comment, from the
    same run.
