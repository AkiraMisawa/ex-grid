# 85: A group or total row's tint is painted once on every cell

Status: ready-for-agent

**What to build:** a fix found by ticket 81. A group or total row (ADR-0024) puts its tint on the row and again on
each cell. A scrollable cell then shows the tint twice. A pinned cell, whose own ground covers the row's, shows it
once. With the default token, the two differ by about 9 grey levels. That figure is arithmetic and has not been
read from the screen. Nothing compares the two today.

**Blocked by:** None (can start immediately)

- [ ] **One shade.** Every cell of a group or total row, pinned or scrollable, shows the same shade, at the
      tint the ADR and its token intend.
- [ ] **Keep ticket 81's fix.** A lined cell keeps the tint under its lines.
- [ ] **Keep the other backgrounds.** Row Stripes (ADR-0038) and the hover band (ADR-0029) still read as before
      on those rows.
- [ ] **Layer 3.** Compare a pinned cell's pixel with a scrollable cell's in the same group row, and in the same
      total row. Name the test after ADR-0024 and ADR-0038. Read the pixels first, and say in the comment
      whether the 9 levels were real.
