# A Row Key names a row across versions, and the grid repaints a changed row in place

*(Decided with the user, 2026-10-05, in the grilling of ExGrid's live data — Q7 and R1. It began
as a research question: how ag-grid repaints when its data changes
([the note](../research/ag-grid-rendering-on-data-change.md)). The measurements are in
[`verification/2026-10-05-macos-live-update-measure`](../../verification/2026-10-05-macos-live-update-measure/README.md).)*

A changed row is a new instance, and Row Identity is that reference
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). The grid also keys each
row's component by that instance (`ExGrid.razor:446`, `:8681`). So when a row's data changes,
Blazor throws away the row's component and every element in it, and builds them again, even when
one cell of twenty changed. ag-grid pairs a changed row with its previous self by the id
`getRowId` gives, and refreshes it in place.

**ExGrid gains an opt-in declaration, the Row Key**: a function from a row to a value that tells
the row from every other row, and stays the same when the row's data changes.

## What the grid does with it

- **It pairs a row's next version with the one it painted.** The key, not the instance, is the row
  component's `@key`. A changed row keeps its component, and Blazor's diff writes only the text and
  attributes that differ.
- **Row Identity stays the change signal.** `ShouldRender` still compares the row by reference. A
  key never tells the grid that a row is unchanged, and a row rewritten in place still does not
  repaint.
- **The grid compares no values and holds nothing between Windows.** The key pairs one render's rows
  with the next render's, and nothing else.
  [ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)'s
  rule stands: the grid never compares values itself.
- **A key that repeats in a Window, or a null key, is refused by name**, before Blazor's own
  exception for clashing keys. Two rows that answer one key would be painted as one.
- **Without the declaration nothing changes.** The instance is the key, as today.

## Why: measured

Measured on 2026-10-05 with a copy of the row in `spikes/render-bench` (its `/live` page) and on a
Blazor Server spike. Per tick, 40 painted rows were replaced, with 20 columns and 3 cells of each row
changed:

| | Keyed by instance (today) | Keyed by a Row Key |
|---|---:|---:|
| DOM mutation records per tick (WebAssembly, headless Chrome) | 1,840 | 180 |
| Render per tick | 16.0 ms | 8.6 ms |
| Bytes per tick on a circuit, uncompressed | 201,859 | 14,792 |
| … on the wire, with the default compression | 40,712 | 3,134 |

- **The .NET render costs about the same either way** (the keyed render is 0.81–0.98 of today's).
  What the key saves is the DOM work and what crosses the circuit.
- **With every cell of a row changed**, the wire bytes are still 0.43–0.46 of today's.
- **The browser times are headless**, and are read only one against the other.
- **The copy understates today's cost.** The real ExGrid's rows were 1.07–1.18 times heavier to
  build again than the spike's copy.

## Who declares it

- **The bundled sources declare it.**
  - `GridSource.From` declares it when it is given a key
    ([ADR-0141](./0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)).
  - ExPivot declares it for its report rows. A row's key is what the row stands for: its role, its
    Value Field and its Items, the pairing `ReportHistory` already makes.
- **A Consumer that pushes its own Window** declares it when its rows have one.
- **The Row Mark adapter's key is a Row Key**
  ([ADR-0043](./0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md)). One value names
  a row for its marks, for a source and for the grid.

## Considered options

- **Keep the instance as the key.** Rejected on the numbers above.
- **Key a row's component by its position.** Rejected. A component would carry one row's painting
  onto another row after a sort, and a Template cell's own controls would show the old row's state
  on the new one. That is the quiet wrongness principle 1 exists to prevent.
- **The grid compares a row's values to decide what to repaint, as ag-grid's cells do** (with
  `colDef.equals` or `===`). Rejected by ADR-0068: the grid never compares values itself.

## Consequences

- **A row's component now lives as long as its key is in the Window**, not as long as its
  instance.
  - Whatever lives in that component, such as a Template cell's own controls and their state, now
    survives a change of the row's values.
  - ADR-0037's engagement and
    [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)'s P7 are held by the
    core, not by the row. They are re-read against this when it is built.
- **A press on an Action names the row as it was painted when it was pressed.** See
  [ADR-0142](./0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md).
- **`CONTEXT.md` gains Row Key.** It is distinct from Row Identity, the test for sameness, and from
  Record Key, a Snapshot's declared column. A Snapshot's rows take their Row Key from the Record
  Key.
- **The Definition of Done gains LV-1 and LV-2** (§31), and PV-42 for ExPivot's key.
