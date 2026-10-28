# A chart drawn in the browser takes format codes and the Consumer's own script in place of a .NET function

*Decided with the user on 2026-10-28 in the project thread "ExChartのBlazor版をTSコアの包みにする" ("Make
Blazor's ExChart a wrapper over the TS core"). Shown that Blazor's ExChart over its Script Core cannot call a
.NET function at a position only the browser knows (an axis Format at a tick, a sampled series at a pixel, a
sampled surface's z), and offered declarative replacements, an asynchronous call to .NET, or nothing in their
place, the user chose declarative replacements. The user asked how Blazor-ApexCharts supports Blazor Server
("apex chartsではどうやってblazor serverのサポートしてるのか調査したらいいかも", "it may be worth looking
into how ApexCharts supports Blazor Server"); what it does is in Considered and rejected. Asked whether the
choice allows flexible formatting ("我々の選択はフレキシブルなフォーマット可能？"), the user was offered the
Consumer's own script for what a format code cannot say (section 3).*

This amends [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 1
(what Blazor's ExChart adapter carries), [ADR-0257](./0257-notes-and-interpolated-series-are-the-consumers-said-as-such.md)
and [ADR-0291](./0291-a-fitted-line-is-the-consumers-and-says-its-method-and-coefficients.md) (an interpolated or fitted curve handed in as a function), and
[ADR-0250](./0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md) (the
engine's one reference).

## Context

ExChart takes .NET functions in four places where the chart, not the Consumer, chooses the value it is
asked about:

- `ChartAxis.Format`, a `Func<double, string>`, writes every tick, the position under the pointer, and the
  ends of a range typed or dragged.
- `ChartSeries.Sampled` takes a `Func<double, ChartValue?>`, asked at about one position per device pixel
  and at the pointer.
- `SurfaceDefinition.Sampled` takes a `Func<double, double, ChartValue?>`, asked at every node and at the
  slice the user picks.
- A `Format` on a Price Range writes a candle's change and a merged candle's volume, and a surface axis'
  `Format` and a mesh's `ZFormat` write ticks and values between the ones handed in.

The Razor component calls them in .NET, beside the drawing. Blazor's adapter over the Script Core (ADR-0370
section 1, first built in #452) draws in the browser, where on Blazor Server no .NET function can be called
synchronously, and where a round trip per tick or per pixel would put the circuit on the path of every
drawing. #452 therefore refuses each of them by name, and carries a `Format` only for values the definition
holds, as the texts it writes them. Once the Razor component is deleted (ADR-0370 section 11), what replaces
these four is what a Blazor Consumer has, and React's Consumer has had JavaScript functions from the start.

## Decisions

### 1. A .NET function is never called from the browser's drawing

The chart never waits on .NET for a text or a value it draws, on either hosting model. What a .NET function
writes or answers crosses only where the definition holds the input: a `Format` over the values handed in,
as texts (#452), and a Row Key, as itself. Anywhere else, the adapter refuses the definition with a
`NotSupportedException` that names what to use instead (sections 2 to 4). The same definition draws the same
picture on Server and on WebAssembly, and at the same moment as the data arrives (spine rules 1 and 6).

### 2. An axis, a series and a surface take an Excel format code

- **`ChartAxis.FormatCode`**, **`ChartSeries.FormatCode`**, **`SurfaceAxis.FormatCode`** and
  **`SurfaceDefinition.ZFormatCode`** take an Excel number format code in the invariant spelling, as a
  grid built from a source's shape does
  ([ADR-0342](./0342-a-grid-builds-its-columns-at-run-time-from-a-sources-shape.md),
  [ADR-0120](./0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md)):
  `#,##0.00`, `0.0%`, `0.00"x"`, `#,##0,"K"`, `yyyy-mm-dd`, `mmm yy`.
- **The family's one reader writes it in both cores**: `FormatCode`, ADR-0342's subset, held to the shared
  cases under `tests/cases/shaped/`. A code it does not read (a condition in brackets, a colour, a fraction)
  is refused as the definition is checked, naming the code and what it does not read. When a Consumer needs
  more of Excel's language, the subset grows in both cores at once, held by the same cases; this ADR does
  not grow it.
- **It writes every value the chart shows**: an axis' ticks, the position under the pointer and a range's
  ends; a series' values, a candle's change and a merged candle's volume; a surface's ticks and the values
  between its nodes. Every value, held or computed, is written by the one code, so a computed value never
  reads differently from a held one. On a chart's axis that is more than a `Format` writes: a `Format`,
  written for the values a column holds, writes a logarithmic axis' ticks but leaves its stepped and calendar
  ticks to the axis' own labels, which a code, written for the axis, does not. A code writes a numbers
  axis' ticks at its steps (a Named position keeps its name) and a Dates axis' calendar ticks while
  `AxisStyle.Label` is `Auto`; a Label the Consumer chose still writes its dates. A Dates axis written by a
  code steps no finer than the code tells dates apart (`mmm yy` ticks months, never days that would all read
  alike), found by writing days a day, a month and a year apart with it, and a code that tells none apart,
  one of the time alone, is refused there.
- **A code is of one kind, and the axis or the values must be of it**: a date code on a Dates axis, a
  number code on a numbers axis and on a series' values. A Categories axis writes its labels as they are,
  so a code there has nothing to write and is refused.
- **A Func and a code on the same member are refused**: the chart would have two writings of one value.
- **ExChart's engine takes its first reference, `ExGrid.Data`**, where the C# reader moves, made public.
  [ADR-0250](./0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md)'s
  "no reference at all" was so the engine knows no grid and no UI; the family's data package is neither,
  has no dependency of its own, and already carries ExPivot's engine. A second reader in the engine would
  be two writings of one code, which the shared cases exist to prevent.
- **The Razor component honours it too**, through the same C# reader, so a definition reads the same under
  either component until the Razor one is deleted.
- **A `Format` over held values stays**, crossing as texts (#452). It is still how a series keeps a
  decimal's `3.30` or writes what no code says; it cannot write what the chart computes, which is why a
  candle's needs a code.

### 3. What a code cannot say is the Consumer's own script, named, never evaluated

*Proposed to the user with the question of flexibility (2026-10-28); included unless the user declines.*

- **A `ScriptFunction` names a function exported from a JavaScript module the Consumer ships**:
  `ScriptFunction.Of("./js/chart-formats.js", "tenor")`. It may stand where a code stands (an axis, a
  series, a surface axis, a surface's z) and where a sampler stands (`ChartSeries.Sampled`,
  `SurfaceDefinition.Sampled`). The adapter imports the module in the browser, takes the export, and hands
  the Script Core a plain function, which it calls as React's Consumer's own function is called: at once,
  in the drawing.
- **Nothing is evaluated from a string.** The module is a file the Consumer serves like any other static
  asset, loaded by `import()`; a page whose Content Security Policy forbids `unsafe-eval` keeps it.
- **A module that does not load, an export that is missing or is not a function, and a function that
  throws or returns what the member does not take** are refused by name, in the chart's place, never
  written another way.
- **This is the Consumer's code, not the product's.** [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)
  allowlists the JavaScript the family ships; a `ScriptFunction` adds no product JavaScript beyond the
  `import()` of a module the Consumer names, which the adapter already does for its own.

### 4. The family's own functions cross as what they were made from

`Interpolate.Linear`, `Interpolate.NaturalCubicSpline`, `Interpolate.Bilinear`, `Interpolate.Triangles` and
`Fit.*` have twins in the Script Core held to the shared `chart` cases (ADR-0370 section 3). A series or a
surface made from one crosses as its method and the points or coefficients it was made from, and the Script
Core makes the same function again; the curve, its Segments and its Notes read as they do in .NET. An
interpolant or a fitted curve keeps what it was made from for that reason. A series or a surface sampled
from any other .NET function is refused by name, pointing at `ScriptFunction` (section 3) or at handing
the points in.

## Considered and rejected

- **Calling .NET synchronously from the browser.** Blazor-ApexCharts' `FormatXAxisLabel` and
  `FormatYAxisLabel` take a `Func<decimal, string>` and call it through `DotNetObjectReference.invokeMethod`,
  which only WebAssembly allows: its documentation says "Only supported in Blazor WebAssembly", and on Server
  its script logs a warning and draws the raw number. A label quietly written another way on one hosting
  model is what spine rule 1 refuses.
- **Asking .NET asynchronously and drawing when it answers.** Ticks, the pointer's reading and a resampled
  zoom would each wait a circuit's round trip, and the picture would depend on when the answer came (spine
  rule 6). Blazor-ApexCharts does this for one thing only, a tooltip rendered by Razor, where an empty element
  is shown first and filled when .NET answers; ADR-0410 (a Blazor adapter fills a Script Core slot by moving its Razor into it)
  takes the same shape for Chrome, where nothing drawn depends on it.
- **JavaScript source in a string, evaluated in the browser.** Blazor-ApexCharts' Server path: a
  `Formatter` is a string such as `"function (v) { return v + '%'; }"`, sent as `{"@eval": …}` and passed to
  `eval`. It needs `unsafe-eval` in a page's Content Security Policy, and the code lives in a C# string with no
  module, no types and no test. Section 3 gives the same reach from a module.
- **Pre-computing in .NET at the positions the chart will ask.** The positions depend on the width, the
  range, the scale and the pointer, all decided in the browser; .NET would have to run the Script Core's
  layout again and agree with it to the last bit, and the pointer's position cannot be known ahead.
- **ExSheet's Number Format in place of ADR-0342's subset.** It reads conditions, colours and fractions,
  but lives in the sheet engine, which neither chart package depends on; and a colour is a cell's, not an
  axis label's.

## Consequences

- Blazor's ExChart over the Script Core reaches parity with the Razor component for every Consumer whose
  axis formats a code can say, which is every Format in the Docs Site's Examples, and for the family's own
  interpolants and fits.
- A Consumer with an axis `Format` moves it to a code or a `ScriptFunction` before the Razor component is
  deleted; the refusal names which member to move. That move is a Change Note when it ships.
- React's Consumer gains `formatCode` beside its own functions, with the same reader.
- The C# `FormatCode` reader leaves ExGrid's internals for `ExGrid.Data`, made public, and ExChart's engine
  depends on that package; it is the same reader, held by the same cases.

## Implementation notes

- `FormatCode` moves to `ExGrid.Data` (C#, public), so `ExChart.Engine` reaches it without depending on a
  grid; the package smoke check names `ExGrid.Data` as the engine's one dependency. In TypeScript it stays in
  `@ex-grid/core`, which `@ex-grid/chart` already depends on.
- A value of the other kind (a number under a date code) is refused with an `ArgumentException`, as the
  TypeScript reader throws a `TypeError`, rather than shown as its own text. Every .NET number type is a
  number. Two readers of one code are equal, so a chart's records compare as before. The reader's refusals
  no longer name a grid: a part it does not read is one "which the reader does not take".
- The wire carries a code as a string and a `ScriptFunction` as `{module, export}`; the adapter's browser
  half imports each module once per chart and refuses before attaching when one fails.
- `Interpolant1D` gains the points it was made from (`Xs`, `Ys`), which a curve given another method or
  function by `with` drops. A `FittedCurve` already holds what it was made from.
- *Changed while implementing (2026-10-28).* The note first said `ChartSeries` and `SurfaceDefinition` would keep
  the curve beside the sampler. A copy kept beside it goes stale when the sampler is replaced. The adapter
  instead takes the sampler as the curve's when it is that curve's own `ValueAt`, which delegate equality tells
  without reading the function. A sampler that wraps a curve is the Consumer's function, and is refused.
- A sampled surface is sampled in .NET at its nodes before it crosses, and a slice reads those nodes, so it
  already crosses as its table. `Interpolate.Bilinear` and `Interpolate.Triangles` need nothing more, and only a
  series' sampler is made again in the browser.
