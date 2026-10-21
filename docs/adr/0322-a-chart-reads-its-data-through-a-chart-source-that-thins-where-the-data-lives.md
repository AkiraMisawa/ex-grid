# A chart reads its data through a Chart Source that thins where the data lives

*Decided with the user, 2026-10-20, in the grilling of the Chart Spec (AI-Q7 and AI-Q11; [ADR-0320](0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md)).
Where the Engine meets the Snapshot was left open by the grilling and is decided here as a default,
under the user's standing instruction to record one; the user may revisit it.*

A `ChartDefinition` holds every point in memory. On the Server host that memory sits beside the data,
and Thinning ([ADR-0254](0254-past-the-plots-pixels-a-series-is-thinned-to-its-minimum-and-maximum.md)) sends the browser only what the plot's pixels can show. On the WebAssembly host
a Snapshot of millions of rows a capability made on the server would have to cross to the browser
whole before it is thinned (inferred from the code, not measured).

Prior art, checked 2026-10-20: ApexCharts loads every point into the browser and reduces them there
with LTTB (`dataReducer`), re-sampling the raw series on zoom. AG Grid's Integrated Charts chart the
selected cell range; its documentation does not say what they read under the Server-Side Row Model.
Its Viewport Row Model, where the server sends only the rows in view, is the grid-side form of what is
decided here.

## Decisions

- **A Chart Source answers "this column, from x0 to x1, at N pixels"** with each pixel's first, last,
  minimum and maximum point and its count, as Thinning defines them. The tooltip on a thinned pixel
  says its range (ADR-0254) whatever side thinned it. It is the chart's counterpart of the grid
  being told its Window ([ADR-0001](0001-consumer-pushes-the-window-grid-does-not-fetch.md)).
- **Where it thins is the source's business.** For data held on the server, the server thins and the
  browser receives only thinned points. For data sent to the browser, ExChart's reference
  implementation thins it there and re-reads the raw columns on every zoom, which is what ApexCharts
  does. The chart and the spec are the same either way.
- **Thinning stays first, last, minimum and maximum, never LTTB.** A point-picking reduction can
  drop a pixel's extreme, and a risk chart whose spike vanished on zoom-out looks right and is not.
- **The reference implementation is the specification**, as `GridSource.From` is for filtering and
  sorting (ADR-0001): a server's source answers what it would.
- **A chart drawn from a spec follows its handle's latest Snapshot.** Changed values are redrawn and
  added and removed rows appear and go ([ADR-0067](0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)), because the spec named whole columns. This is
  unlike a chart made from a selection, which is the set of Row Keys it was made from ([ADR-0258](0258-the-selection-bridge-follows-the-rows-it-was-made-from.md)).
- **An answer names the Snapshot it was read from, and an answer for an older one is never
  painted.** A zoom and an update crossing on the wire cannot mix two Snapshots in one picture
  (spine rule 6).
- **The Engine keeps a column seam of its own; the Snapshot's bridge is in `ExChart`.** *Default,
  decided here:* ExChart.Engine references nothing today, and keeps it so; its reference
  implementation reads plain columns. `ExChart`, which already reaches `ExGrid.Data` through
  `ExGrid`, adapts a Snapshot to the seam, so a Consumer adds no package. *Amended 2026-10-20, while
  building it:* this said ExChart would reference `ExGrid.Data` directly. A direct reference would add
  a dependency to ExChart's package, which CH-13 in the Definition of Done forbids (exactly ExGrid and
  the engine); the reference through `ExGrid` gives the same types, so ExChart keeps it.

## Consequences

- The Chart Source is a public seam; its shape, and the Snapshot version an answer carries, are
  settled when it is built and tested against the reference implementation.
  *Settled 2026-10-21 by [ADR-0344](0344-a-chart-source-answers-thinned-points-and-a-board-charts-a-server-input-through-it.md):
  `IChartSource.ThinAsync`, answered as `ChartSource.Thin` answers.*
- A chart made in C# from a `ChartDefinition` with its points keeps working as it does; the source is
  how a Chart Spec's chart reads, and a Consumer may give one to a definition too.
