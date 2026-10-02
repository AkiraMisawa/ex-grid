# 98: A typed date operand takes its column's date type

Status: ready-for-agent

**What to build:** a fault ticket 97 found, which predates it. It is loud, not quiet, and is left for the next PR.
- Neither filter panel knows its column's CLR date type, so a typed date always reads as a `DateTime`. The one
  exception is the offset form, which reads as a `DateTimeOffset`.
- The engine holds a column to one date type (`MixedDateTypes`, ADR-0023). So a condition typed on a `DateOnly`
  column, or on a `DateTimeOffset` column without the offset form, throws when it is applied.

**Blocked by:** None. It is planned for the PR after #42.

- [x] **Decide how the panel learns the column's date type, and record it.** Either `FilterPanelContext` carries
      it, or a conversion rule maps a `DateTime` reading to the column's type. A `DateOnly` takes the day. A
      `DateTimeOffset` takes which offset, and that has to be stated.
- [ ] **A condition typed on a `DateOnly`, `DateTime` or `DateTimeOffset` column applies**, under both Chromes,
      and reads back as itself.
- [ ] **Layers 1 and 2**, named after ADR-0023.

## Comments

2026-10-02, grilled with the user (`/grill-with-docs`). Recorded in ADR-0023's section of the same
date.

- **The Column declares it.** A Date column takes `dateType: DateType.DateOnly` (or `DateTime`, the
  default, or `DateTimeOffset`); `ColumnInfo.DateType` and `FilterPanelContext.DateType` carry it. A
  `dateType` on a column that is not Date is an `ArgumentException` when the column is built.
- **Cells are held to it.** A cell of another date type is refused, naming the column, where the
  one-date-type rule is already held. An undeclared `DateOnly` or `DateTimeOffset` column now throws
  on its first sort or filter: a breaking change, accepted on a prerelease.
- **A typed operand reads as the declared type, or is refused with `OperandRefusal.NotTheColumnsDateForm`,
  whose words name the form to type:**
  - `DateTime` refuses an offset;
  - `DateOnly` refuses a time, rather than cutting it to its day;
  - `DateTimeOffset` requires an offset. It reads ISO 8601 / RFC 3339's `2026-10-02T00:00:00+09:00`
    and `…Z`, and ticket 94's `2026-10-02 00:00:00 +09:00`. ISO 8601 leaves a time without an offset
    as local time of no stated zone, and the Server host cannot know the browser's.
- **The MudBlazor panel** draws a `DateTimeOffset` operand as a text field read through
  `ReadOperand`, with no calendar. `DateOnly` and `DateTime` keep the picker. `SetOperand` reopens
  all three.
- **Rejected:** converting operands in the engine (against ADR-0002), inferring from cells, splitting
  `ColumnType.Date`, a declared time zone (waits for a recorded need).
