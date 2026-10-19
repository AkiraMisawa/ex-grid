# 17: A chart follows the rows it was made from

Status: done

**Why:** [ADR-0258](../../../adr/0258-the-selection-bridge-follows-the-rows-it-was-made-from.md) (M3-Q3, M3-Q7).

- [x] Values are read by Row Key from the Consumer's data, never from the Window: a row filtered out of the grid is not gone
- [x] A recalculated value is redrawn; a row that no longer exists is a gap at the place it had, never zero, and a Note says how many are gone
- [x] A new row is never added
- [x] While nothing it reads changed, the bridge hands back the same Chart Definition, so the chart skips its render (ADR-0252)

**Blocked by:** 15, 16.

Roadmap: internal
