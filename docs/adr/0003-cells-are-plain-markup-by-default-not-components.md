# The memoisation boundary sits at the row. Cells are plain markup

The grid paints **one row as one component**, and **the cells inside it as plain markup**. The
row component implements `ShouldRender()` by hand and skips re-rendering entirely when the row's
**identity** has not changed. Cells are not individual components.

## Measurements

`spikes/render-bench`, run on real hardware (Windows / Chrome 151 / 32 cores / .NET 10 Release,
no AOT). Milliseconds per render, median. `step` is how many rows are scrolled per frame —
`1` is slow scrolling (typical), `50` is a fling (worst case).

| Cells | step | `Direct` | `Accessor` | `AccessorMeta` | **`RowComponent`** | `Component` |
|---|---|---|---|---|---|---|
| 800 | 1 | 10.00 | 10.40 | 11.70 | **1.90** | 6.10 |
| 800 | 50 | 10.70 | 11.00 | 12.20 | not measured | 36.20 |
| 2000 | 1 | 23.90 | 24.60 | 28.20 | **2.80** | 14.90 |
| 2000 | 50 | 25.60 | 26.40 | 30.70 | 32.80 | 92.00 |

`Direct` is the floor with no abstraction at all (plain markup, direct field access);
`Accessor` adds a per-column value accessor; `AccessorMeta` adds a per-cell metadata lookup;
`RowComponent` puts the boundary at the row; `Component` puts it at the cell.

## Why the position of the boundary changes this by 10×

Plain markup gives Blazor no way to be told "this row has not changed". The parent rebuilds the
render tree for every row and diffs it, so **the cost is paid for every cell even when almost
nothing on screen changed**. A component creates a boundary there, and when nothing has changed
the child's rendering is skipped wholesale.

**The number of boundaries is what matters.** At the cell it is 800; at the row it is 40. Both
work well during slow scrolling (39 of 40 rows are unchanged), but during a fling everything
changes, the boundaries buy nothing, and only their overhead remains — which is why the cell
boundary collapses to 92.00 ms. At the row the overhead is one twentieth, so even the worst case
is only +7% against plain markup.

## Considered Options

- **Cell as component** — rejected. It is faster than plain markup during slow scrolling
  (6.10 vs 11.70) but collapses to 92.00 ms on a fling. 800 boundaries is too many.
- **Plain markup everywhere** — rejected. It is fastest in the worst case but **5–10× slower in
  the typical case**, and the typical case is where users spend their time.
- **Rely on Blazor's automatic parameter change detection (write no `ShouldRender()`)** —
  rejected after **trying it and finding it did not work** (10.30 ms at step=1 / 800 cells —
  slower than plain markup). Blazor only reports "unchanged" for value and immutable-typed
  parameters; a mutable reference type such as `Row` is treated as **"may have changed" even when
  the reference is identical**. That is a deliberately safe design, and the only way around it is
  to write `ShouldRender()` yourself.

## Consequences

- **The change signal becomes "a different instance", not "rewritten contents".** Because
  `ShouldRender()` compares by reference, a Consumer that rewrites a row object in place gets no
  repaint. When data changes, the Consumer must **return a different instance** or carry a version
  on the row and bump it. This has to be stated in the API contract.
  The first Consumer's baseline is a content-addressed immutable snapshot, so a new feed version
  naturally produces new instances and the constraint does not bite
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) relies on the same
  property for paging consistency).
- **The abstraction itself is cheap.** A runtime Column's value accessor (boxing included) costs
  +3–4%, and adding a per-cell metadata lookup brings it to +14–18%. The **Column** and
  **Cell Metadata** designs in `CONTEXT.md` fit inside that budget.
- **A column with custom rendering becomes a component for that column and gets slower.** Say so
  to the Consumer. Specifying it on every column approaches the `Component` row of the table
  ([ADR-0020](./0020-action-and-template-columns.md)).
- **Variable row height would break the boundary.** A row that decides its own height feeds back
  into the virtualisation arithmetic. Settled in
  [ADR-0013](./0013-fixed-row-height.md): the height is fixed.
- **This decision does not rescue the fling case.** A fling over 2000 cells exceeds the budget in
  every mode (25–33 ms), and the row boundary does not help there because every row changes.
  Handled separately in [ADR-0004](./0004-cap-the-cells-touched-per-frame.md).
- The numbers are from **Release IL without AOT**. AOT may widen the margin; untested.
