# 91: ExSheet's fitting and widening use the grid's glyph widths

Status: ready-for-agent

**What to build:** the Excel gap left after ticket 83 (its comment, part 1). ExSheet's General fitting, its `####`
decision and ticket 58's widening charge every character one digit width (ADR-0047). So under en-GB the date key's
`05-Jan-26` widens a standard-width column (9 × 9.75 + 16 = 103.75px > 99), where Excel fits it at 8.09 (the
twelfth run's case 19). The core now holds per-glyph widths for each face (ticket 83, ADR-0016).

**Blocked by:** None (can start immediately)

- [ ] **Charge Number and Date text with the grid's metrics.** ExSheet's fitting, its `####` decision and its
      widening use `CellTextMetrics.For(ColumnType.Number/Date)` for the column's face and weight, bold included,
      instead of one digit per character.
- [ ] **Keep what ADR-0047 settled where Excel counts characters.** For example, the width recorded in the
      Sheet Document stays in Excel's character units. Say in the comment what changed and what did not.
- [ ] **Layer 1 and 2** against the runs' cases:
  - run 12, case 17 (which keys widen, and to what);
  - run 12, case 19 (`05-Jan-26` fits at 8.09 under en-GB);
  - the corpus: nothing is cut.
- [ ] **Say how close each case comes to Excel's width**, before and after.
