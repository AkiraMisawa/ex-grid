# 06: The grid holds no row beyond its Window

Status: ready-for-agent

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
- [ ] Layer 1 and 2 green

## Comments
