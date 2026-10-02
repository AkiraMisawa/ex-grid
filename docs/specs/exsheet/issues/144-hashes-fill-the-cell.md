# 144: `####` fills the cell

Status: ready-for-agent

**What to build:** ADR-0016's note of 2026-10-02. A `####` run is emitted longer than the cell, may break
between any two `#`, and the cell's one line shows exactly the `#` that fit in its own face. Whether a
number fits is decided as before.

**Blocked by:** None (can start immediately)

- [ ] **The cell is full**: no room left for one more `#`, no cut `#`, no ellipsis (FN-12e).
- [ ] **A shown number never wraps.**
- [ ] **A11Y-7 and CP-5 still hold.**
- [ ] **Layer 1** for the run's length; **layer 3** on a Sheet at Part C's case 3c width.
