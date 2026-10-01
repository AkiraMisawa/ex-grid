# 22: The work after an answer, sliced

Status: done

**What to build:** what ADR-0065 settled on 2026-10-01. Near the 200,000-leaf cap, the work after a
question's pass held a browser for 1.9 s in one task (`verification/2026-10-01-linux-measure`):

- the bundled source assembling its answer;
- ExPivot making the cube;
- ExPivot laying out the report.

Each yields to the browser at least every 30 ms, as the pass does. A gesture made meanwhile
supersedes the question, and the report on screen stays until the new one is complete. Nothing is
shown half-built (ADR-0066's "never half a batch").

**Blocked by:** None

- [x] PV-40 in layer 2: the yields counted for a large answer, and a gesture that supersedes a
  question mid-build
- [x] The longest task near the cap observed again with `measure-pivot.spec.mjs` on a published
  build, and recorded beside the first measurement

## Comments

2026-10-01: Built.

- **The engine** (`Slicer`). One piece of work's slice is carried across the pass and the steps
  after it.
  - The pass looks at the clock every `RecordsPerCheck` rows, as before.
  - The steps after it count their units — a leaf assembled, a leaf's cells merged, a row laid out
    — and look at the clock every 1,024 of them (`PivotSlicing.UnitsPerCheck`, internal). So a
    small answer never reads the clock, and the pass's own tests (PV-27) keep their yields.
  - `PivotSlicing.YieldAsync` is public, for work that goes on after a sliced piece has yielded.
- **The bundled source** assembles its answer in the pass's slices, and so it does the answer held
  for live data. A Change Batch applied while an answer is assembled from the answer held waits
  until it is made, and is folded in then: no answer is half a batch (ADR-0066).
- **`PivotEngine.CubeAsync` and `ReportAsync`, and `PivotReport.HasSameRowsAsAsync`**, run the very
  steps of `Cube`, `Report` and `HasSameRowsAs`, which run them unsliced, so the two forms give the
  same result by construction.
  - The report is laid out by a depth-first walk that can stop after any step, in the recursion's
    order.
  - A node's children are ordered by a stable merge sort that can stop after any comparison. The
    order is a total one, so it is the order `List.Sort` gave.
- **ExPivot** keeps the question out until its report is complete. The answer's cube is made, its
  report laid out, its rows compared with the report on screen and its label columns sized, all in
  slices, while the report on screen stays, under the loading indication.
  - A gesture made meanwhile supersedes the question through the generations, as it would at the
    source, and changes of data wait for it.
  - A layout of the answer held is laid out in the gesture's own turn when it is quick. One that
    grows long is the work in flight, in the same way.
  - New words while a report is built reach the report shown, and the report on screen is laid out
    again in slices.
  - Between the engine's steps, ExPivot carries the slice. A step that yielded is followed by a
    yield; a large step that did not (512 leaves, or rows and columns) is followed by one when less
    than half the slice is left; a small step never reads the clock.
  - `ExPivot.Slicing` takes the slicing (`PivotSlicing.Default` when left out).
  - The label columns' widths are measured once per report and metrics, no longer on every drag of
    a column's width.

Tests: `SlicedWorkTests` (13, layer 1) and `SlicedBuildTests` (7, layer 2, PV-40). Each was seen
failing first, against stubs that did not slice; the quick layout's test passes either way, by
design.

- **Layer 1.** The sliced forms give exactly the synchronous forms' results, part for part and node
  for node, over the deals' edge cases, the sales in every form, an answer with no leaf, and some
  17,000 leaves.
  - Thousands of siblings are ordered in slices as at once, by label, by key and by value. An Order
    Key that throws fails the same way.
  - The sliced forms yield, end a slice at its budget by a stepping clock, and stop at a yield when
    cancelled, reading the clock no more.
  - The bundled source assembles a large answer in slices, over a Snapshot and over untyped
    accessors. A question cancelled during the assembly stops there and holds nothing half made.
  - A batch applied at a yield of the held answer's assembly is not in that answer, and is in the
    next.
- **Layer 2.**
  - A 3,000-leaf answer is built in slices: at every yield, held by the test, the report on screen
    is the one before, whole, under `IsLoading`.
  - A gesture during the build supersedes the question, and so does one the answer held lays out.
  - A long layout of the answer held is sliced and superseded, and a quick one yields nothing.
  - A batch during a build is never shown half.
  - New words during a build reach the report shown.

Layer 3: `pivot.spec.mjs` and `pivot-live.spec.mjs` passed 53 of 53 on both hosts, after ticket
21's merge, on Linux under xvfb with the container's Chromium.

Observed, in `verification/2026-10-01-linux-measure-sliced`, against the branch before the change on
the same machine:

- **Near the cap (197,151 leaves), the longest task fell from 1.65 s to 137 ms** (medians), and the
  answer went from 2.59 s to 2.79 s.
- **Below 30,000 leaves, the median run has no long task**, and the answers are unchanged.
- **Gestures laid out from the answer held are as quick as before.** Collapse, sort and form take
  22–35 ms in their medians, and each is still painted in the input's own turn.
- **The tasks left are not the work after the answer.** A diagnostic build traced them to the
  runtime's full collections — about 70 ms, landing inside a 30 ms slice — and to the one turn that
  puts the report on screen: 30–54 ms near the cap, including the grid's first render of it.
- **On CoreCLR, the synchronous forms cost what they did.**

Two things were found, and are not built:

- **Near the cap, a question runs one or two full collections of Mono's stop-the-world collector, of
  about 70 ms each.** No slicing can cut them. Fewer allocations could: the cube's parts and cells
  are sized at twice the leaves, and the answer and the cube each copy the parts.
- **Putting a large report on screen is one turn.** That covers the presentation and the grid's
  first render of the new Window, which walks every row of it. Near the cap it took 30–54 ms, and
  it was not traced further.
