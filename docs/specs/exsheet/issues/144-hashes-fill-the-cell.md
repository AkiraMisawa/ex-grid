# 144: `####` fills the cell

Status: done

**What to build:** ADR-0016's note of 2026-10-02. A `####` run is emitted longer than the cell, may break
between any two `#`, and the cell's one line shows exactly the `#` that fit in its own face. Whether a
number fits is decided as before.

**Blocked by:** None (can start immediately)

- [x] **The cell is full**: no room left for one more `#`, no cut `#`, no ellipsis (FN-12e).
- [x] **A shown number never wraps.**
- [x] **A11Y-7 and CP-5 still hold.**
- [x] **Layer 1** for the run's length; **layer 3** on a Sheet at Part C's case 3c width.

## Comments

2026-10-02, claude/exsheet-part-c. `OverflowRules.Decide` counts the run at half a digit; `ExGridRow`
wraps it in `.ex-hashes`, which may break anywhere. Layer 1: `OverflowRuleTests`. Layer 3:
`part-c.spec.mjs` at case 3c (whole `#` only, no room for one more, the rest on a hidden line, red).
