# Charting from the grid — what a PoC showed, and what a grilling has to decide

*Research note, 2026-10-13. It decides nothing: no ADR, no `CONTEXT.md` entry and no criterion of
the Definition of Done is changed by it. Everything the user said during the session that reads as
a decision is collected in [§2](#2-what-the-user-said-during-the-session) so that the grilling can
confirm it and write it down; every open question is in [§8](#8-questions-for-the-grilling).*

- **The proof of concept is [`spikes/exposure-chart`](../../spikes/exposure-chart/README.md)**, a
  disposable spike, on this branch. Its README records each step with screenshots, GIFs and
  measurements; this note gathers what it found and what is left to decide.
- **Where it came from.** Seeing ag-grid's integrated charts (AG Charts), the user asked what
  ExGrid would do. The session started from an exposure-profile grid and grew page by page: CDS
  curves, an FX volatility surface, an interest-rate curve.
- **Market screens were studied second-hand.** This cloud session could not reach YouTube or a
  terminal. A Claude session on the user's Windows machine summarised one training video and the
  written sources it could find (§6). Nothing in this note was seen on a terminal.
- **Vocabulary.** Domain terms are `CONTEXT.md`'s (Consumer, Header Group, Row Identity, Window,
  Visual Token, Chrome …). "ExChart" is the working name used in the session for a charting package
  of the family; it is not a term yet.

## 1. The PoC

```sh
nix develop -c dotnet run --project spikes/exposure-chart/ExposureChart.Server -- --urls http://localhost:5410
nix develop -c dotnet run --project spikes/exposure-chart/ExposureChart.Wasm
```

`ExposureChart.Pages` holds the pages, the chart (`Charting/`, `Components/ChartBox.razor`,
`Components/Surface3D.razor`, `wwwroot/*.js`) and the invented analytics (`Analytics/`); the two
hosts only host them, as the DemoHost's do (ADR-0019). Five pages:

| Page | What it charts | What it added to the chart |
|---|---|---|
| `/` Exposure profile | Date × EPE/ENE/CVA/PFE, each Before/After/Diff under Header Groups | chart a selection; panels per Header Group; filter; styles incl. steps; x axis settings; add series; each series its own dates; legend |
| `/cds` CDS and hazard rate | one curve: spreads (left, bp) and bootstrapped hazard rates (right, step) | two axes; units; Consumer colours; axis labels from a column |
| `/curves` CDS curves, tenors across | a row per entity, tenors across | series in rows; a number axis (tenor years) declared per column |
| `/fxvol` FX volatility surface | vol table, 3D, term, smile, deposits/forwards | category axes; shades by date; models a page assembles; 3D in JS |
| `/rates` Rate curve | quotes, zero rates, discount factors, forwards | an editable grid driving the curve; interpolation drawn between pillars |

No line of `src/` changed. The chart reads the selection through the context menu's Consumer
command (ADR-0036), which already carries the ranges and the Row Sequence Version.

## 2. What the user said during the session

Statements of the user's, in the session's order, that the grilling should confirm and record. The
PoC was built on them.

| # | The user's statement (paraphrased from Japanese) | Where the PoC follows it |
|---|---|---|
| U1 | A PoC first; grill afterwards | this note |
| U2 | Show results as screenshots, and GIFs | the spike's README |
| U3 | Excel-like filters on the chart would be good | Filter → series, dates (Chart Filters) |
| U4 | **JavaScript may be added for the tooltip** | `exposure-chart.js`: reports the date under the pointer on change only |
| U5 | Points and lines, marker styles, step functions with open markers where the function does not take the value and filled where it does; CVA integrates a piecewise-constant profile | `SeriesStyle`: Line, LinePoints, Points, StepPreceding (tᵢ₋₁, tᵢ], StepFollowing [tᵢ, tᵢ₊₁) |
| U6 | The horizontal axis should be configurable, e.g. yyyy-mm-dd rotated | `XAxisStyle`: ticks, labels, angle |
| U7 | Add series to a chart already drawn | "+ Add series" |
| U8 | **Each curve should have its own dates** | `SeriesDefinition(Column, RowKeys)` |
| U9 | A CDS example: spread on the left axis, hazard on the right; show the grid too; hazard piecewise constant | `/cds` |
| U10 | Grids with tenors across (ticker, entity, ccy, seniority, doc clause, recovery, 6m … 20y) should chart; x = the tenor's value, y = spread and hazard | `/curves`, `RowChartSpec` |
| U11 | The legend should be flexible | HTML legend: position, list or table, click to hide |
| U12 | **The bootstrap belongs to analytics outside the chart package**, never inside it | `Analytics/`, referenced by no chart code |
| U13 | Measure on WebAssembly too | §5 |
| U14 | FX and IR volatility surfaces | `/fxvol` (FX); IR not built |
| U15 | **JavaScript may be used for 3D rotation**; reproduce the video's screen | `surface3d.js`, `/fxvol` |
| U16 | Zero-rate, discount and market-quote curves, researched against Bloomberg | `/rates`, §6.2 |

U4 and U15 widen ADR-0021's allowlist and have no ADR yet. U8 and U12 are design rules the PoC
leans on everywhere.

## 3. What the PoC chose, that the user did not

These are the PoC's own choices. Each is a candidate decision, not a decision.

1. **Draw from scratch, as SVG written in C#**, rather than wrap a JavaScript library (ApexCharts
   was discussed). The reasons argued in the session: ADR-0021 (drawing SVG is something Blazor can
   do, so a library has no allowlist reason); numbers and dates formatted by the same code as the
   cells; Visual Tokens rather than colour options; deterministic output testable in layers 1–2.
   The chart code is about 600 lines of C# for the first page's scope.
2. **The chart is defined in the data's terms, not the selection's indices**: columns by name,
   rows by their key (a date, a row's key), so a sort or a recalculation does not change what a
   chart means (ADR-0011's reason, applied to charts). A stale selection is refused.
3. **Nothing is guessed** (spine rule 1): headers are not data; series names and panels come from
   Header Groups; the axis is the date column, or the positions the Consumer declares; which
   columns read on the right, their units and their colours are declared. A selection that cannot
   be drawn is refused by name (no date column, a text column, a number column with no position).
4. **A missing value is a gap, never a zero; a line joins only neighbours in the data**; a step's
   interval runs to the data's neighbour.
5. **Points beyond the data are not invented**: no spline; the line between points is labelled as
   drawn, not quoted, where it matters (the surface, the rate curve's interpolation).
6. **Panel per Header Group by default**: one axis made CVA (tens of thousands) a flat line beside
   EPE (millions).
7. **The page's legend is HTML** (clickable), and the downloaded SVG carries its own.
8. **CSV**: invariant, unformatted, every digit, a BOM for Excel; a wide file and a long one (the
   wide file cannot tell "missing" from "not charted").

## 4. What was found by building

- **No core change was needed** to chart a selection: ADR-0036's Consumer command carries the
  ranges and the Row Sequence Version.
- **The grid refused a page by name** when a Header Group straddled the pinned column (ADR-0032)
  — the behaviour working as designed.
- **Two of CLAUDE.md's traps were hit again**: a page class named like an analytics class
  (`FxVol`) hid it; columns built inside markup are a new list every render.
- **A race found by driving at machine speed** (spine rule 6): style controls that sent the whole
  style they were rendered with put a first change back when a second came within one round trip
  on the Server host. Each control now changes its one field on the state as it is.
- **A right-hand axis for Diff costs half a panel** when the zeros are aligned.
- **A per-period amount drawn as a line misleads**: CVA by bucket jumps where the bucket width does.
- **Linear zero-rate interpolation saw-tooths the forwards**; log-linear discount factors make them
  a step — the chart shows the convention, so the convention must be declared.
- **Time-proportional tenor axes crowd the short end** (1Y's label is left out beside 6M); market
  screens draw term structures and smiles on category axes (§6.1), and draw rate curves
  time-proportionally (§6.2).

## 5. What it costs (measured 2026-10-13)

Exposure page, 12 series, this cloud container (4 vCPUs, Linux), headless Chromium on the same
machine — no network latency or bandwidth limit. WebAssembly: published Release, no AOT.

| Dates | Redraw, Server | Redraw, WebAssembly | Hover to the next date, Server | Hover, WebAssembly | Bytes per redraw (Server) |
|---|---|---|---|---|---|
| 67 | 72 ms | 199 ms | 27 ms | 30 ms | 428 KB |
| 1,566 | 644 ms | 1.8 s | 62 ms | 282 ms | 9.0 MB |
| 10,959 | 7.0 s | 13.5 s | 228 ms | 1.7 s | 61 MB |

What it says: the server's build is milliseconds (5 ms at 67 dates) and the circuit and the DOM are
the cost; on WebAssembly the same build is ~15× slower; every redraw resends the whole drawing and
the download links as `data:` URLs; the hover layer is dates × series; a hover re-renders the whole
chart component. Fixes identified, not applied: the tooltip as its own component with a
hand-written `ShouldRender`; files written when asked for; hover dots drawn by Blazor; SVG per
series so a redraw diffs; min/max decimation past the plot's pixels (a decision, §8); AOT measured.

## 6. Market screens

### 6.1 FX volatility (one training video, summarised from its transcript and 23 stills)

Source: "Bloomberg Training: Bloomberg Option volatility Surface", Fintute, YouTube
`wpcvGhYN4eQ`, 6:39, AUDUSD as of 2013-06-07. Summarised by the session on the user's machine.

- Five tabs: Vol Table, 3D Surface, Term, Smile, Dep and Fwd Rates; a header with the asset, the
  source, the vol calendar ("Weekdays"), the date, and Settings (expiries, deltas, RR/BF or
  Put/Call, Bid/Ask or Mid/Spread).
- **Vol Table**: expiries down; two-level headers, each pillar over Bid and Ask (Header Groups);
  values to 3 decimals; selecting a vol drills down; a quick pricer strip.
- **3D Surface**: delta across (5P … ATM … 5C), expiry along, vol up; green → red with a scale
  bar; a wireframe; rotated with the mouse; an expiry range; a delta-convention footnote ("forward
  delta without premium adjustment").
- **Term** and **Smile**: line charts with markers on **category axes** (expiries, deltas evenly
  spaced); the vol axis on the right only; up to two comparison dates, drawn as shades of the same
  colour, today brightest.
- **Dep and Fwd Rates**: a table and a chart with two y axes.
- Not in the video: interpolation, sticky strike or delta.

### 6.2 Interest-rate curves (written sources; no terminal screen seen)

Researched by the same session; its confidence notes are kept.

- **(A)** Bloomberg SWPM user manual ("Date printed 01/12/2015", mirrored at pdfcoffee.com), the
  Curves tab read in full — the most authoritative. **(B)** "FWCV Forward Analysis Matching
  Output", Bloomberg L.P., 2012 (idoc.pub mirror). **(C)** A. Nematnejad, "Learning Curve: an
  introduction to the use of the Bloomberg system in swaps analysis", *Journal of Bond Trading &
  Management* 1(2), 2002 (stat.purdue.edu mirror), with SWDF and FWCV screenshots. **(D)**
  "Building the Bloomberg Interest Rate Curve – Definitions and Methodology", read through
  snippets only. **(E)** CME, "Bloomberg Analytics for CME Swap Clearing", a search summary only.
- **SWPM's Curves tab has no view toggle**: one table, "Tickers and Rates", with Term, Market Rate,
  Shift (editable per term or for the whole curve), Shifted Rate, Zero Rate, Discount — the last a
  discount factor below 1 though (A) calls it a rate — and a chart of zero rates only (A).
- Bid/Ask/Mid is a **source setting**, not a column (C, B).
- Zero rates are continuously compounded by default (D); FWCV lets the user choose the day count
  and compounding, and the same forward reads differently under swap and cash conventions (B).
- Charts are **time-proportional** with tenor labels, pillars as markers joined by lines; FWCV
  overlays several dates as dash styles with a checkbox per series, and overlays the spot and
  coupon curves on the forwards' axis (C).
- Interpolation methods named (D, snippet): linear on simple zero rates; smooth forward (quadratic);
  step-function forwards (linear in log DF); linear on continuously compounded zero rates.
- Pillars stand at the instruments' maturities and the curve reprices every input (A, D); futures
  carry a convexity adjustment; FRAs use interpolated stub rates (D).
- **[Unverified]** modern ICVS's member table (ticker, type, tenor, maturity, bid, ask, source,
  an include checkbox) — the researching session's own background knowledge, seen in no source.

### 6.3 What other screens in the domain plot (the session's background, not researched)

Risk positions as tenor ladders and vega grids; PL as daily and cumulative series and PL-explain
waterfalls; PLA (FRTB) as HPL-vs-RTPL scatter and time series; volatility surfaces most often as
coloured number matrices and day-on-day change matrices, with smile and term slices.

## 7. What the PoC does not do

- No IR volatility (swaption ATM matrix, cube); no PL-explain waterfall, scatter, bar or heat-map
  chart types; no category axis for non-vol data; no futures, FRAs, convexity or dual-curve.
- No grid cell colouring by value (a heat map in the grid — §8).
- Chart state (filters, styles, legend, axis) is lost when a chart is made again; not in a Saved View.
- Hover dots stay at the data's positions even where a step's open marker stands elsewhere.
- The 3D surface's values show through a native `<title>` (about a second, unstyled).
- Text is never measured: legends and tick labels are fitted on an estimate per character.
- No layer-1/2 tests and no layer-3 spec: everything was verified by driving the pages with
  Playwright from scratch scripts, on both hosts, with no console warnings.

## 8. Questions for the grilling

Grouped; each names where in the spike it shows.

**The package and its seam**
1. Is there a charting product (working name ExChart) — a package of its own beside ExSheet and
   ExPivot, reading the grid's data the way they do — and does it depend on ExGrid, on
   `ExGrid.Data` only, or on neither?
2. Scratch SVG, as the PoC, or a wrapped library? (§3.1)
3. The input shape: the PoC has three — series in columns along a date, series in rows along
   declared positions, and a model a page assembles. Is there one "series tree" underneath, and is
   ExPivot's pivot chart the same shape (rows/columns fields ↔ Header Groups/rows)?
4. What the grid gives: the context-menu command was enough. Should "Chart selection" be a core
   command, or stay the Consumer's?

**Honesty rules (spine rule 1)**
5. A series' kind declared by the Consumer: level vs amount per period vs rate; how each is drawn
   (line, step, bars) and whether an amount per period is normalised by its length. (§4, CVA)
6. Which axes must reach zero (exposure must, a forward rate need not) — declared per series, as
   `FromZero`, or per kind?
7. Interpolation between points is a claim: when a line or a face is drawn between quotes, how is
   it said? Never splines by default?
8. Decimation past the plot's pixels: min/max per pixel; and what a tooltip says for a pixel that
   holds several dates.
9. A selection over other rows, a missing value, a series absent at a date — the PoC's answers
   (refuse, gap, "—") as rules.

**Axes and conventions**
10. Category axis vs time-proportional: chosen by the Consumer per chart, or by the data's kind?
11. Positions declared per column (tenor years) — the seam's name and shape.
12. Conventions shown on the chart (delta convention, compounding, day count): a footnote the
    Consumer declares?

**JavaScript (ADR-0021)**
13. The tooltip listener (U4) and the 3D module (U15) as allowlist entries: their exact scope, and
    whether the 3D module may draw (it does in the PoC).

**Appearance**
14. Colour: who assigns it — leaf, row, the Consumer's slot? A palette as Visual Tokens.
15. A heat map in the grid's own cells (vol matrices, day-on-day change, vega grids): it meets
    ADR-0027/0028's "no colour is a C# parameter" — a bucketed scale of Visual Tokens?
16. 3D at all, and if so how much (rotation, presets, flat = heat map).

**State and files**
17. Filters, styles, legend and axis settings: kept in a Saved View?
18. CSV of what is shown or of the whole definition; wide and long; SVG/PNG files.

**Performance (§5)**
19. Apply the four identified fixes before anything else, and set a measured target per host?

**Scope of a first version**
20. Which pages' features are the first version: the exposure profile, the CDS and rate curves, the
    vol surface? IR vol, waterfalls, scatter later?
