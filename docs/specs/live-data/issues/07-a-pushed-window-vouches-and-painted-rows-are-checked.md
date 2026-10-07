# 07: A pushed Window vouches, and the painted rows are checked

Status: done

**What to do:** build ticket 02's decision, recorded in [ADR-0141](../../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)'s section of 2026-10-07. A
Consumer that pushes its Window may vouch that it holds no Row Key twice. A vouched Window is not walked
whole. The painted rows' keys are checked on every render, vouched or not.

**Blocked by:** None

## What to build

- **A grid parameter beside `RowKey`**, meaningful only with a Row Key, by which a Consumer that pushes its
  Window vouches for it, with its XML doc. `TakeInWindow` (`src/ExGrid/Components/ExGrid.RowKey.cs`) then
  treats the Window as it treats a source's `VouchesDistinctRows`.
- **On every render, the grid checks the Row Keys of the rows it paints** before Blazor's own exception,
  and refuses a repeat by name, naming the key and its positions (LV-2's message).
- **ExPivot vouches for its report**, with a comment at the site naming the engine's guarantee
  (`AxisNode.Child` keeps children by Item).

## Done when

- [x] LV-10 as restated passes, counting the key function's calls (§32)
- [ ] The grid's check of a pushed 10⁶-row vouched Window is gone from ticket 01's measurement. Record the
  pushed-Window figure again beside [ticket 01's](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md): 40 ms on CoreCLR, 257 ms in the browser
- [x] Layer 1 and 2 green

## Comments

2026-10-07: Built. The parameter is `VouchesDistinctRows`, the name `IGridSource` already gives the same
promise. It joins the push parameters a Source replaces, so passing it beside a Source is refused by
name (ADR-0001's rule). `TakeInWindow` takes it as it takes a source's vouch: only with a Row Key, and a
vouch withdrawn over the Window in hand has that Window checked whole. On every render the row loop asks
each painted row's key once, for its `@key`, and checks it against those painted before it in one map
kept for the grid's life. A repeat is refused with LV-2's message before Blazor's exception, vouched or
not, and so is a null painted row, which the whole-Window walk used to refuse. ExPivot passes
`VouchesDistinctRows` beside its Row Key; its parameter takes sequence 32 so the lines around it stay as
they were.

Layer 2: `RowKeyTests` counts the key function's calls. A vouched pushed Window of 10⁵ rows asks only
the painted rows' keys, and an unvouched one asks every row's. A repeat or a null among the painted rows
of a vouched Window, pushed or a source's, is refused by name, also when it is scrolled into view.
`RenderAllocationTests` shows the check allocates nothing per painted row per render, against the same
grid without a Row Key. `ReportRowKeyTests` shows ExPivot's report is vouched for. Full suite green.

The pushed-Window figure on CoreCLR, ticket 01's harness and method (10⁶ rows, the least of 15 runs,
`DOTNET_TieredCompilation=0`; load average 3.7–9 from other agents' work). The grid's take-in of a
pushed Window under a Row Key is 45.8–46.3 ms unvouched (ticket 01: 41.3–47.1 ms, the check alone then
39.9–42.0 ms, now 45.4–46.2 ms under the load). Vouched, it is 0.011–0.015 ms, and 0.105–0.140 ms with
the render that checks the 18 painted rows. The browser's 257 ms was not re-recorded.

2026-10-07, after the code review: the pass over a Window and the check of the painted rows now take
their keys through one method, and the public doc no longer quotes a timing. The figure's box is ticked
on the CoreCLR figure, as the brief allowed; the browser half of it (257 ms) stays open.

2026-10-07, after the late reviews: the figure's box is unticked. The CoreCLR half is recorded above;
the browser half (ticket 01's 257 ms, taken again with a vouched pushed Window) is not, and stays open.
It needs a browser harness page with a pushed grid, and the layer-3 lock. The Docs Site now says the
feature: `samples/ExGrid.Docs/Pages/Grid/GridData.razor`, "Pushing a Window", explains the Row Key and the
vouch; the million-row Example declares both, under each Chrome (`MillionRows.razor`,
`MillionRowsMud.razor`). The site was run and the Example used once under each Chrome: Ctrl+End painted
the last trade, and the console stayed clean.

2026-10-07, layer 3 on the final code (2fcec664 and dc2cd665): `selection-summary`, `grid-live-local`,
`grid-live` and `pivot-live` pass locally on WebAssembly under Chrome, headless, 20 of 20, with a clean
console.

2026-10-07, the orchestrator's decisions on the reviews: the figure's box stays unticked. What was
measured is the CoreCLR figure, recorded above: ticket 01's harness and method, 10⁶ rows, the grid's
take-in of a pushed keyed Window at 45.8–46.3 ms unvouched and 0.011–0.015 ms vouched. Ticket 13
re-records the browser figure. The map of painted keys is now also cleared when a new Window is taken in,
so no Row Key of a row outside the current Window is held, even when the new Window paints no row
(ADR-0160; a layer-2 test with weak references).

2026-10-07, layer 3 again after the orchestrator's decisions (dc14cd63): the same four specs pass
locally on WebAssembly under Chrome, headless, 20 of 20, with a clean console.
