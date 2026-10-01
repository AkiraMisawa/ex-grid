# 56: ExSheet's commands act on the grid's Selection as it is now

Status: ready-for-agent

**What to build:** the rest of what ticket 51's Server fix found. It is in ADR-0050 item 14's note
of 2026-10-01 and in ticket 51's comments. The grid raises `SelectionChanged` after the render that
shows a move, which on a circuit is a round trip later. A declared key now carries the grid's
Selection. Three other paths still act on the Selection ExSheet last heard. A command run within a
round trip of a keyboard move then formats fewer cells, or other cells, than are selected, and
nothing says so. That is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [ ] **The Consumer's commands** act on the grid's current Selection, not ExSheet's copy:
      `SetCellFormatAsync` and its shorthands, and `OpenFormatCellsAsync`.
  - This needs a way to read that Selection from the grid: an opt-in read of the core's, with the
    Row Sequence Version, as ADR-0011 asks of positions.
  - A toolbar button pressed straight after Shift+arrow, on the Server host, formats the extended
    range.
- [ ] **The Context Menu's "Format Cells…"** opens over the Selection the menu was opened on. Its
      context carries the ranges but not the Focus. Either it gains the Focus, or the command reads
      the grid's Selection as above.
- [ ] **A whole-column resize** groups its undo step by the grid's Selection. Today a stale one at
      worst splits the step, and no data goes wrong.
- [ ] **Tests**:
  - Layer 2 stages the circuit's order, as ticket 51's fix does: a Range Request left unanswered
    holds the selection notification back.
  - Layer 3 on the Server host: a toolbar button straight after Shift+ArrowDown.
