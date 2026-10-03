# 15: AVERAGEIFS and COUNTIFS

Status: needs-triage

**What to build:** `AVERAGEIFS` and `COUNTIFS` in ExSheet's function set, under ADR-0047's rule: a
function is admitted only when it answers as Excel does, edge cases included. The sample needs them
for "the average of every AAA sovereign at this tenor" without listing the names.

- **The cases** are written from Excel's observed behaviour: criteria syntax, blanks and text in the
  ranges, an Error Value in a matching row of the average range, and no matching row (`#DIV/0!`).
- **A Windows run** asks Excel those cases before the functions are admitted.

This is an ExSheet ticket. When it is picked up, it moves to `docs/specs/exsheet/issues/` under a
number from a block reserved in `docs/agents/numbering.md`.

**Blocked by:** None. A Windows run is needed before the functions are admitted.

- [ ] The cases in the corpus, observed in Excel
- [ ] The functions, with tests named after ADR-0047
