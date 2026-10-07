# 06: The grid holds no row beyond its Window

Status: done

**What to do:** make [ADR-0160](../../../adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md) hold and be checked. The grid holds a Consumer's row instance and
its Row Key only while it is in the Window it was last given, apart from the two named holdings.

**Blocked by:** 04 (which removes the kept paints)

## What to build

- **`src/ExGrid/Components/CellAppearances.cs`** forgets every entry whose row instance a new Window does
  not hold (`_byPosition` and `_asked`). It keeps appearance values, not rows of older renders.
- **Name the two holdings that outlive a Window in code comments citing ADR-0160**: `_measuredWindow`
  while a measure is deferred, and the row of an Action press in flight.
- **LV-22's layer-2 test.** Take weak references to the rows of each earlier Window, apply a run of new
  Windows, make a full collection, and assert none is alive. Do it with `CellAppearance` declared and not,
  and with a Row Key declared and not. Make each Window in a method of its own so the JIT does not keep it
  alive.
- **LV-23's observation**: the loop of [the out-of-memory record](../../../../verification/2026-10-06-macos-pivot-oom/README.md), 101,001 report rows and 20
  live redraws in the browser, recorded into `metrics.json`. Ticket 13 may run it with its other
  measurements.

## Done when

- [ ] LV-22 passes; LV-23 recorded
- [x] Layer 1 and 2 green

## Comments

- 2026-10-07: built on `claude/live-data-next-cc-grid-writes`. `CellAppearances` forgets, at each new
  Window, the positions whose row it no longer holds there and the answers of rows it no longer holds
  around the painted rows; a held row whose neighbour went keeps its appearance value (DC-58). The
  two named holdings carry ADR-0160 comments (`_measuredWindow`, the Action press in flight). LV-22's
  grid half is `NoRowBeyondTheWindowTests`: twelve new Windows, with and without `CellAppearance` and
  a Row Key, then a full collection, and no earlier row alive; it failed on the cache before the fix.
  Found while building it: Blazor's render tree keeps the previous render's frames as its next
  buffer, so the rows the render before the newest painted (six here) stay reachable until the grid
  renders again. That is the renderer's, not a grid holding; the test ends with one more render and
  asserts that boundary as found. LV-22's ExPivot half is Track C's (tickets 09 to 12). LV-23, the
  browser observation, is left to ticket 13, as this ticket allows.
