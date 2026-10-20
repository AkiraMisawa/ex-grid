# ExChart is shown in five Showcases and a Chart section of nine pages

*Decided with the user, 2026-10-19 (M4 grilling Q1, Q4 and Q5). Rewritten the same day: the user
merged the three curve Showcases into one whose views are switched by buttons. ADR-0250 put the proof of concept's
pages into the first version; this says where they live and in what shape. ADR-0110 governs the Docs
Site as a whole.*

## Three Showcases: Exposure profile, Curves and FX volatility

The proof of concept's pages become three Showcases (CONTEXT.md): **Exposure profile**, **Curves**
and **FX volatility**. Curves holds what were the proof of concept's CDS curves, Curves and Rates
pages as three views of one page, switched by a row of buttons above one chart.

*The rationale was replaced once.* The first decision of the day made each page a Showcase of its
own, five in all, accepting that CDS curves, Curves and Rates overlap in the features they show. On
seeing four Showcases under way, the user asked for the curve pages to be one, switched by a click:
three pages that show the same features read as repetition, and a switch between views is itself
something a Consumer builds.

- **The switch is page code**, read as the Example's code (ADR-0110): each button hands the chart
  another `ChartDefinition`, and the chart redraws it. No view switcher ships in a package.
- **FX volatility keeps the proof of concept's five tabs**: the vol table (a grid), the 3D surface, the
  term structure and the smile on category axes, and the deposits and forwards.
- **The data is precomputed, fixed values under `samples/`.** No analytics code is carried over: not
  the exposure simulation, not the CDS bootstrap, not the volatility arithmetic of `FxVol.cs`. The
  grid and the chart neither hold nor execute (spine rule 3), and a Showcase that bootstrapped a
  curve would teach a Consumer to put a pricing library behind a chart.
- **What links the tabs is page code**, read as the Example's code (ADR-0110): the term and smile
  tabs pick a delta or an expiry from the page's own list. The slice drawn beside the 3D surface when
  a tenor or strike is clicked is the surface's own (ADR-0274), not the page's.
- **No volatility viewer, or any other finance-shaped component, ships in a package.** It would put
  one desk's layout and conventions into a general chart.
- **Each Showcase also sits in the DemoHost**, so layer 3 drives it on both hosts.
- The README's recordings are made from them (ADR-0110): the selection made into a chart from
  Exposure profile, the surface turned from FX volatility.

## A Chart section of seven pages

As ExPivot's pages are a Pivot section, ExChart's are a **Chart** section:

| Page | What it covers |
|---|---|
| Overview | A chart placed on its own, and one made from the grid |
| Series and axes | Series Kinds, steps, gaps, Interpolated series and Segments (ADR-0253, ADR-0257) |
| Panels, legend and colours | Panels, aligned zeros and the note when they are not, the legend, the three appearance keys (ADR-0256, ADR-0271) |
| Tooltip and Chart Filters | The tooltip, the dragged range, state raised as events (ADR-0270, ADR-0272, ADR-0273) |
| From the grid | The selection bridge, adding a series, a click back to the grid (ADR-0258) |
| 3D surface | Turning, zooming, the tooltip and the slice (ADR-0274) |
| Reading and export | The summary read aloud, "View as table", CSV, SVG and PNG (ADR-0275, ADR-0282) |

Every Example carries the Built-in / MudBlazor switch (ADR-0110, ADR-0281).

## Two more Showcases and pages, from the scatter and the candlesticks

*Decided with the user later on 2026-10-19 and 2026-10-20, in the threads that build them (the scatter's
grilling Q6, ADR block 0290; the candlesticks' Q9, ADR block 0300).* Scatter charts and candlesticks with
error bars joined the first version, each with a Showcase and a page of its own: **CDS spread vs
leverage** and **Equity index daily bars**, and the pages **Scatter and fitted lines** (ADR-0290's name for it) and **Candles and
intervals**. The first version therefore has five Showcases and a Chart section of nine pages. Those threads'
records say what theirs show; the title was rewritten from "three Showcases and … seven pages" on 2026-10-20,
when their work landed beside this, and the two Showcases follow the shape the first three set: the grid
beside its charts, fixed data, and a view switched from the bar where there is more than one.

## Considered

- **Five Showcases, one per page of the proof of concept**: chosen first, then replaced the same day
  (above). Three of them repeated the same features.
- **Two Showcases, merging the curve pages and FX volatility into one "Credit and rate curves"**:
  would hide the 3D surface, the one feature only FX volatility shows, behind a curve page.
- **One Showcase (FX volatility) and the rest as Examples on the feature pages**: a Consumer would
  not see a whole screen of their own kind.
- **One long page, or a page per chart type**: one page buries a feature under scrolling; a page per
  type (line, step, points, surface) repeats the legend, the tooltip and the grid bridge on each.
- **A volatility viewer component in `ExChart`**: one line for a Consumer, at the cost of a desk's
  layout in a general package.
