# 98: A typed date operand takes its column's date type

Status: ready-for-agent

**What to build:** a fault ticket 97 found, which predates it. It is loud, not quiet, and is left for the next PR.
- Neither filter panel knows its column's CLR date type, so a typed date always reads as a `DateTime`. The one
  exception is the offset form, which reads as a `DateTimeOffset`.
- The engine holds a column to one date type (`MixedDateTypes`, ADR-0023). So a condition typed on a `DateOnly`
  column, or on a `DateTimeOffset` column without the offset form, throws when it is applied.

**Blocked by:** None. It is planned for the PR after #42.

- [ ] **Decide how the panel learns the column's date type, and record it.** Either `FilterPanelContext` carries
      it, or a conversion rule maps a `DateTime` reading to the column's type. A `DateOnly` takes the day. A
      `DateTimeOffset` takes which offset, and that has to be stated.
- [ ] **A condition typed on a `DateOnly`, `DateTime` or `DateTimeOffset` column applies**, under both Chromes,
      and reads back as itself.
- [ ] **Layers 1 and 2**, named after ADR-0023.
