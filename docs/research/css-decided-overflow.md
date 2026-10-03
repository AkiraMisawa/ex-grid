# Can the browser decide `####`? — CSS-detected overflow instead of the C# glyph-width estimate

Status: **research, not a decision** (2026-10-03). Nothing here changes an ADR, `CONTEXT.md` or
`docs/definition-of-done.md`. The proposal at the end is for the user to decide.

## Why this was asked

In the design grilling for a CDS marking sample app, the user asked whether ExGrid could let a
Consumer return its own cell classes per cell, as ag-grid's `cellClassRules` does. The blocker is
[ADR-0016](../adr/0016-column-width-and-overflow.md): `####` is decided in C# from known glyph
widths (`CellTextMetrics`, ADR-0016's measured tables). A Consumer class that changes typography
(`font-weight`, `font-size`, `letter-spacing`, a family) makes that decision quietly wrong — the
core judges a number as fitting and the stylesheet clips it. If the browser decided `####` at
layout time, the guarantee would not depend on the estimate.

The candidate technique: a scroll-driven animation on a scroll container is only active while the
container has scrollable overflow, and `overflow: hidden` is a scroll container
([css-tip](https://css-tip.com/overflow-detection/),
[bram.us](https://www.bram.us/2023/09/16/solved-by-css-scroll-driven-animations-detect-if-an-element-can-scroll-or-not/),
[MDN](https://developer.mozilla.org/en-US/docs/Web/CSS/Guides/Scroll-driven_animations/Timelines)).

## Answer, in short

- **It works.** Both mechanisms tried detect overflow on `.ex-cell` exactly as the stylesheet
  defines it today, paint Excel's `####` (as many as fit, no partial glyph, no ellipsis), hide the
  digits, keep the accessible name, and are right **on the first painted frame** after any change
  — a recycled or rewritten cell never shows the previous state. On the real DemoHost grid they
  hash exactly the cells C# hashes (12/12 and 13/13), and the selection Overlay paints over the run.
- **It is too expensive to put on every numeric cell.** The browser's per-frame cost rises with
  the number of cells carrying the switch, and — the finding that rules it out — **it keeps rising
  with the number of cells that have scrolled past**, until a garbage collection. An idle frame
  costs 0.6 ms on a fresh page with the scroll-state version and 47 ms after three flings; 1.8 ms
  and 122 ms with the scroll-timeline version. Without the switch, idle frames stay at 0.1 ms
  however far you scroll. That breaks the premise of [ADR-0004](../adr/0004-cap-the-cells-touched-per-frame.md)
  and P1 — cost as a function of the Viewport — in the browser, where no test of ours looks.
- **A scroll-state container query does the same job without any animation**, so P8 / UX-6 hold
  to the letter. It is the cheaper of the two (a slow scroll costs 18 ms against 7 ms today; the
  timeline costs 27 ms), but a fling is still 5× today's cost and it accumulates in the same way.
  Through Blazor's real render path a fling costs 81 ms (B) and 367 ms (A) of browser time per
  frame, against 13 ms today; the C# decision itself costs nothing measurable.
- **Recommendation: keep the C# decision.** Answer the `cellClassRules` question with a contract
  instead — a Consumer cell class may change appearance, and changing glyph width is
  metrics-bearing, exactly as it already is for a Theme
  ([ADR-0027](../adr/0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). The ADR-0016
  change proposed at the end records the measurement and that conclusion; it does not adopt the
  technique.

## Method and environment

| | |
|---|---|
| Browser | Chromium **141.0.7390.37** (Playwright's build, `/opt/pw-browsers`), Linux, headed under xvfb, software rendering; the first-frame probe uses the same build's headless shell |
| Not run | Branded **Chrome and Edge** are not installed in this container, so ADR-0017's "both browsers" is not discharged here. Edge is the same Blink; the mechanisms are Blink's, but this is a reasoned expectation, not a measurement. **Windows and macOS** were not run either — their scrollbars and fonts differ, and the cost numbers are software-rasterised |
| Font | `system-ui` → DejaVu Sans, 14px, `tabular-nums` (ADR-0016's Linux table) |
| Stylesheet | `src/ExGrid/wwwroot/ex-grid.css` **unmodified**, plus a candidate stylesheet on top |

Everything lives under `spikes/render-bench` (see "Reproducing" at the end):

| file | what it is |
|---|---|
| `css-overflow/candidate.css` | candidate **A**: scroll-driven animation (`animation-timeline: scroll(self inline)`) |
| `css-overflow/candidate-scroll-state.css` | candidate **B**: scroll-state container query (`@container scroll-state(scrollable: inline-end)`) — nothing animates |
| `css-overflow/probe.html` + `tools/css-overflow-probe.mjs` | feasibility: detection boundary against geometry, the fill, the accessible name, forced colors |
| `tools/css-overflow-frames.mjs` | first-frame correctness, frame by frame (`HeadlessExperimental.beginFrame`) |
| `tools/css-overflow-demohost.mjs` | the candidates on the real DemoHost grid |
| `css-overflow/frames.html` + `tools/css-overflow-cost.mjs` | browser-side frame cost with no Blazor, every pair on a fresh page, ablations, the share of cells carrying the switch, and the accumulation experiment |
| `tools/css-overflow-gc.mjs`, `tools/css-overflow-gc-blazor.mjs` | which garbage collection gives the accumulated cost back, without and with Blazor |
| `Bench.Client` — **Measure overflow paint (frame cost)** | the bench mode: `OverflowPaint` None / CSharp / Css / CssScrollState through Blazor's real render path |
| `results/css-overflow/*.json`, `results/*.json` | the recorded results this document quotes |

## The two mechanisms

**A — scroll-driven animation** (`candidate.css`). The cell runs a one-keyframe animation on its
own inline scroll timeline. The timeline is only active while the cell has a scroll range, so the
animation only applies while the content overflows. The keyframe sets the cell's
`-webkit-text-fill-color: transparent` (hiding the digits) and `--ex-hash-display: block`, which
the cell's `::after` inherits to show the run. No container style query is needed — a custom
property inherited from the animated cell does the switching.

**B — scroll-state container query** (`candidate-scroll-state.css`). The cell is its own
`container-type: scroll-state`. A pseudo-element's query container is looked up among the
*inclusive* ancestors of its originating element, so the cell's own `::after` and `::first-line`
can ask `scroll-state(scrollable: inline-end)`. The text node cannot, so the digits are hidden
through `::first-line { color: transparent }` — `-webkit-text-fill-color` on `::first-line` does
**not** work (measured: both the digits and the run painted).

**The run** in both: `::after { content: "####…" / ""; position: absolute; inset: 0; padding:
inherit; overflow: hidden; white-space: normal; word-break: break-all }`. `break-all` wraps the run
at a character boundary, so the first line holds exactly as many `#` as fit and the rest wrap out
of the clipped box. Numeric cells get `text-overflow: clip`: if detection and the ellipsis ever
disagreed by a fraction of a pixel, `1,234,56…` is exactly the quietly wrong case.

## 1. Feasibility in Chrome (Chromium 141)

### Detection, against layout geometry

48 (typography × value) cases, each swept across cell widths from 4px too narrow to 4px to spare in
⅛px steps (65 widths each, 3,120 cells per candidate). "Overflow" is the laid-out text width
(a `Range`'s box) minus the content box. Both candidates gave identical answers
(`results/css-overflow/probe-candidate-*.json`):

| | result |
|---|---|
| A value that fits, hashed | **0 of 3,120** |
| An overflow of 1px or more, missed | **0** |
| Smallest overflow detected | 0.125px |
| Largest overflow missed | **0.875px** — the scroll range is snapped to whole pixels, so how much is missed depends on where the fractions fall |
| Typography covered | regular; total row (600); a Consumer class at 700, at 17px, with `letter-spacing: 1px`, in a serif family; `ex-state-stale` (italic); a pinned (sticky) cell |
| Values covered | `1,234.56`, `123,456,789,012.50`, `-98.7%`, `2026-10-02`, `(1,234,567.00)`, `¥1,234,567` |

**A missed sub-pixel overflow is painted whole.** `overflow: hidden` clips at the padding box, not
the content box, so a value up to 0.875px too wide runs into the 8px end padding and loses nothing:
measured, the text's right edge stays ≥ 6.98px inside the cell at every overflow up to 1.01px.
This is the same tolerance as ADR-0016's "a value estimating exactly at the resolved width fits",
widened by under a pixel, and it errs the safe way: the value is shown complete.

This is the property the estimate cannot have: **a Consumer class that makes the text bold,
larger, wider-spaced or a different family is detected exactly as the regular text is.**

### The run: Excel's `####`

25 hashed cells (5 typographies × widths 40–133px): the first line held exactly
`max(1, floor(content width / # width))` hashes in every case — the number ADR-0016's C# fill
computes — every glyph inside the content box, `text-overflow: clip`. Painted evidence:
`results/css-overflow/fill-candidate.png` (and `-scroll-state.png`): right-aligned runs, whole
glyphs, no ellipsis, in regular, bold, total, 17px, spaced, error and negative-tone cells.

### Hiding the digits, and what still sees them

| surface | today | with CSS deciding |
|---|---|---|
| Copy ([ADR-0005](../adr/0005-copy-refuses-rather-than-truncates.md)) | built from the values in C# (`ClipboardData`), never from the DOM | unchanged — and the DOM text is now the real value too |
| Selection painting ([ADR-0008](../adr/0008-selection-is-painted-by-an-overlay.md)) | the Overlay over the cells | unchanged: on the DemoHost, the Overlay (`z-index: 1`) and the Focus outline paint over the run (`results/css-overflow/demohost-*.png`). The run is a positioned box with `z-index: auto`, below the Overlay and below pinned cells |
| Accessible name ([ADR-0033](../adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md), A11Y-7) | text `####`, `aria-label` = the value | text = the value, so the name is the value with **no `aria-label` at all** |
| … but only with the alt text | | the `/ ""` in `content` is load-bearing: without it CDP's accessibility tree names the cell `"123,456,789,012.50 ########"` |
| Forced colors (UX-7) | `####` is text | **broken without a fix**: forcing turns `transparent` back into a visible colour and the cell reads `1#####7` — digits between the hashes (seen in the probe's screenshot before the fix). Fixed in both candidates by `forced-color-adjust: none; color: CanvasText` on numeric cells under `forced-colors: active`, measured afterwards to ink exactly what a literal `#####` inks (`forced-colors-*.png`, after the fix) |
| Cell State painted as decoration | the wavy error underline sits under `####` | **A drops it** on a hashed cell (the decoration follows the transparent fill); B keeps it under the hidden digits. A would need every decorating state restated on `::after` |

### On the real grid

`tools/css-overflow-demohost.mjs` loads the DemoHost, injects a candidate, and gives every cell C#
had hashed its real text back (from its `aria-label`) — the markup as it would be if C# stopped
deciding. On `/features` CSS hashed the same 12 cells C# had, and on `/sizing` (bold total row,
Japanese header, narrowed column) the same 13; nothing hashed that C# showed and nothing showed
that C# hashed. The default metrics and the browser agree at the default typography, which is
what ADR-0016's measured tables were for.

## 2. The invariants

### First-frame correctness — the recycled-row worry

A scroll timeline and a scroll-state query are both fed from layout, which runs after style. If a
frame were painted with the state from *before* its own layout, a cell that has just started to
overflow would show its clipped digits for a frame. `tools/css-overflow-frames.mjs` drives
Chromium one frame per `HeadlessExperimental.beginFrame`, changes the DOM with no frame in
between, and reads the pixels of the **first** frame containing the change (digits blue, run red):

| change | A | B |
|---|---|---|
| a new row mounted, already overflowing | hashed | hashed |
| a new row mounted, fitting | value | value |
| text rewritten in place, fits → overflows (churn; a cell reused for another column) | hashed | hashed |
| text rewritten in place, overflows → fits | value | value |
| inline width narrowed (a resize applied) | hashed | hashed |
| inline width widened | value | value |
| a Consumer class adding bold to a value that just fits | hashed | hashed |
| the row becoming a total row (600 from the row class) | hashed | hashed |
| a row reused for another row, mixed outcomes | hashed / value | hashed / value |
| **control**: a 1s stepped colour transition | value (not arrived) | value (not arrived) |

All correct on the first frame and five frames later, for both. The control proves the method can
see a stale frame: a transition that has not arrived reads as not arrived. The outcome is
consistent with the scroll-driven animations specification's provision for re-running style and
layout within a frame when a timeline's state goes stale after layout; Blink's mechanism was not
traced, only its outcome measured.

**A script, however, sees the switch one frame late**: reading `getComputedStyle` synchronously
after inserting a cell returned "not hashed" for every overflowing cell; after two
`requestAnimationFrame`s it was right. Nothing in the grid reads it, but a layer-3 assertion must
wait for a frame.

### P1–P9 ([ADR-0027](../adr/0027-appearance-travels-in-css-geometry-travels-in-csharp.md))

| | A (timeline) | B (scroll-state) |
|---|---|---|
| **P1** DOM nodes a function of the Viewport | holds for **nodes** — `::after` is a box, not a node. But the **cost** stops being a function of the Viewport: see §3, accumulation | same |
| **P2** component instances | holds | holds |
| **P3** appearance re-renders nothing in .NET | holds, and improves: a Theme's typography change (a token on the root) re-hashes in CSS with no render and no new `CellMetrics` for `####` | same |
| **P4** no per-cell interop, no layout read on the path to a paint | holds — no JavaScript; the browser's own layout is the read | holds |
| **P5** per-cell strings interned | holds, and the interned `####` runs and the conditional `aria-label` go away | same |
| **P6** one Row Height | holds | holds |
| **P7** Selection/Focus outlive DOM elements | holds | holds |
| **P8** nothing inside the Viewport animates | **broken to the letter**: one `CSSAnimation` per numeric cell, `animation-name: ex-overflowing`, so UX-6 fails by construction. Not broken in its reason: the keyframe is constant, nothing interpolates over time, and the first-frame table shows no previous-row state | **holds**: `animation-name: none`, no transitions; UX-6 passes unchanged |
| **P9** a Wrapper adds no geometry/JS/component/state | holds; a Wrapper's typography no longer owes metrics **for `####`** — it still owes them for Auto and Size to fit (§5) | same |
| Row memoisation ([ADR-0003](../adr/0003-cells-are-plain-markup-by-default-not-components.md)) | holds — rows still skip; nothing is added to `ShouldRender`. (`ExGridRow` compares `Metrics` today because the decision depends on it; the width estimate still would) | same |
| No JavaScript ([ADR-0021](../adr/0021-javascript-is-allowlisted-not-minimised.md)) | **none needed** — no new ADR on that account | none needed |

## 3. Cost

**No claim here is from reasoning; each number is from a recorded run.** Absolute numbers are
software-rendered (xvfb, no GPU), so they are higher than real hardware; the ratios between modes
on the same machine are the result. The bench's own rule applies: never gate on these.

The existing bench stops its clock at the DOM update, which is exactly the part a CSS-decided
`####` does not touch — its cost, if any, is in the browser's style, layout and animation update.
So both harnesses are driven from `requestAnimationFrame` and time **the rest of the frame**: from
the end of the rAF callback to a message posted from it, which runs once the frame's rendering is
done.

### 3a. Browser-side cost, no Blazor, each pair on a fresh page

`css-overflow/frames.html`: 40 rows × 20 numeric cells at 90px (800 cells), the markup `ExGridRow`
paints, with the real `ex-grid.css`. 600 frames per pair, or 20s, whichever first. Milliseconds of
main thread per frame, p50 / p95 (`results/css-overflow/cost-2026-10-03T00-54-48-812Z.json`):

| scenario | none (today's CSS) | **A** timeline | A **inert** (A without the animation) | **B** scroll-state |
|---|---|---|---|---|
| Idle (frames run, nothing changes) | 0.2 / 0.3 | **1.8 / 2.6** | 0.2 / 0.3 | 0.6 / 1.0 |
| Slow scroll (1 row in, 1 out per frame) | 6.9 / 9.7 | **26.9 / 35.4** | 7.2 / 10.4 | 18.1 / 24.4 |
| Fling (every row replaced per frame) | 17.2 / 26.5 | **188.8 / 271.9** (66 frames in 20s) | 21.4 / 31.3 | **92.9 / 163.8** (155 frames) |
| Live feed, burst: 300 cells once a second — the burst frames, p95 | 13.9 | 32.5 | 12.7 | 16.9 |
| Live feed, trickle: 5 cells every frame | 4.0 / 5.5 | 8.4 / 12.1 | 2.0 / 3.2 | 4.8 / 7.0 |

At 90px, 95% of these cells overflow, which is far more hashing than a real grid shows. Two
ablations separate the switch from the run (`cost-width_160-*.json`, `cost-pseudo_off-*.json`,
`cost-hashes_40-*.json`; p50):

| | idle | slow scroll | fling | trickle |
|---|---|---|---|---|
| none, 160px (nothing overflows) | 0.1 | 7.0 | 16.1 | 4.3 |
| **A**, 160px — the switch alone, nothing hashed | 1.7 | 16.5 | 138.7 | 4.8 |
| **B**, 160px — the switch alone, nothing hashed | 0.6 | 8.5 | 44.1 | 3.3 |
| A, 90px, run box removed | 1.8 | 17.0 | 145.8 | 5.8 |
| B, 90px, run box removed | 0.5 | 8.7 | 54.1 | 2.7 |
| A, 90px, run shortened to 40 `#` | 1.8 | 23.5 | 184.4 | 8.5 |
| B, 90px, run shortened to 40 `#` | 0.6 | 17.1 | 76.9 | 4.7 |

Read together:

- **The scroll timeline is itself the cost of A.** The same stylesheet without the animation
  ("inert") costs what no candidate costs; with it, a slow scroll more than doubles even when no
  cell overflows.
- **B's switch is nearly free on a slow scroll** (8.5 vs 7.0 ms) and costs 3× on a fling.
- **Each hashed cell's run box costs about 10 ms per slow-scroll frame at 95% hashed**, in both,
  and shortening the run does not help — it is the extra positioned box, not its length. Today's
  C# paints `####` as the cell's own text: no extra box.

### 3b. The accumulation: cost grows with every cell that scrolls past

The first full run of the Blazor bench (all modes in one page, one after the other) showed the CSS
mode's *unchanged* frames costing 39 ms in a scenario that came after the fling, against 1.6 ms in
the first scenario. *(That run was read off the page and not saved — its driver timed out before
pressing Save — so it is quoted only as what prompted this experiment; §3c's runs are recorded.)*
So a page was flung, and its idle frames measured before, between and after
(`cost-2026-10-03T00-54-48-812Z.json`, `accumulation`; p50 of 300 idle frames):

| | fresh page | after fling 1 | after fling 2 | after fling 3 | after a forced GC |
|---|---|---|---|---|---|
| none | 0.1 | 0.1 | 0.1 | 0.1 | 0.1 |
| **A** timeline | 1.8 | **53.3** | **82.3** | **121.9** | 4.2 |
| **B** scroll-state | 0.6 | **16.2** | **28.3** | **46.6** | 2.5 |

The flings were time-capped, so they replaced different numbers of cells (A: 74,400, 43,200,
34,400; B: 146,400, 96,800, 80,800); the JS heap stayed small (1–9 MB), which is why a collection
was not triggered on its own. **A cell that has left the DOM goes on costing every frame until it
is garbage-collected.** That is the shape of a leak to the reader: the grid gets slower the longer
it is scrolled, and recovers when the collector happens to run. Without a candidate there is no
such growth. Which object Blink keeps servicing (the timeline, the scroll-state container, its
snapshot client) was not traced; the effect, and its disappearance on collection, were measured.

### 3c. Through Blazor: the bench mode

`Bench.Client` → **Measure overflow paint (frame cost)**: the settled design (`RowComponent`), 40 ×
20 cells at 90px, 13px font, and `OverflowPaint` = **None** (value painted, clipped), **CSharp**
(today: a per-class estimate, `####` painted as interned text), **Css** (A), **CssScrollState**
(B). Each mode was run on its own fresh page (`?overflow=…`), with a `gc()` before every
scenario (Chromium started with `--js-flags=--expose-gc`), meant to keep one scenario's departed
cells from inflating the next — it did not, as the second table shows. `.NET` is Blazor's render
and DOM update; `Browser` is the rest of the frame.

Browser milliseconds per frame, p50 / p95 (`results/20261003-011713-232.json` None,
`-011828-004` CSharp, `-013634-555` Css, `-014103-131` CssScrollState):

| scenario | None | **CSharp** (today) | **Css** (A, timeline) | **CssScrollState** (B) |
|---|---|---|---|---|
| Idle | 0.2 / 0.3 | 0.2 / 0.3 | 1.6 / 2.2 | 0.6 / 0.9 |
| Slow scroll | 6.4 / 8.0 | 6.5 / 8.4 | **21.7 / 31.0** | **14.2 / 19.6** |
| Fling | 13.7 / 22.5 | 13.0 / 20.1 | **367.0 / 535.6** | **81.0 / 141.6** |
| *.NET, fling* | *22.0* | *22.8* | *47.3* | *23.5* |
| Burst, after the fling | 0.2 / 0.3 | 0.2 / 0.3 | **197.7 / 288.8** | **59.2 / 87.1** |
| Trickle, after the fling | 4.3 / 6.4 | 4.1 / 5.9 | **269.6 / 347.7** | **74.9 / 102.5** |

The last two rows ran after the fling, on the same page, with a `gc()` before each. They are
what the accumulation does through Blazor, so they were run again in isolation
(`results/20261003-014330-854.json` A burst and trickle, `-015337-804` A fling then idle,
`-015707-904` B fling then idle):

| | Css (A) | CssScrollState (B) | today |
|---|---|---|---|
| Burst on a fresh page — quiet frames p50; burst frames, total p95 | 1.6; 46.2 | — | 0.2; 40.5 |
| Trickle on a fresh page, p50 | 8.3 | — | 4.1 |
| **Idle right after one fling, `gc()` between** | **230.6** | **88.8** | 0.2 |

Read together:

- **C# deciding costs nothing measurable.** CSharp and None are within noise of each other in
  every scenario, .NET side included — the estimate is not where the time goes.
- **Through Blazor the CSS modes are worse than in the plain-DOM harness**, and A also doubles
  the .NET side of a fling (47 against 22 ms): inserting a cell that carries an animation costs
  more in the DOM update itself.
- **The accumulation is the same through Blazor, and so is the cure.** After one fling, idle
  frames cost 231 ms (A) and 89 ms (B). The bench's own `gc()`, issued straight after the
  scenario switch replaced the cells and before any frame had run, did not bring that down. A
  `window.gc()` issued after the fling's frames had run did: 177.5 → 3.8 ms (A) and
  91.4 → 4.0 ms (B), with the mode left on screen (`?hold=1`,
  `tools/css-overflow-gc-blazor.mjs`, `results/css-overflow/gc-blazor-2026-10-03.txt`). A CDP
  collection on top changed nothing further. In the plain-DOM harness the same holds
  (`results/css-overflow/gc-*.json`: A 31 → 6 ms, B 13 → 1.1 ms after `window.gc()`). So the
  departed cells are not retained by Blazor; they are simply not yet collected, and an idle
  grid allocates too little to prompt a collection. Which Blink object keeps being serviced
  until then was not traced.

### 3d. CSS on a fraction of the cells — the price of a backstop

Option 3 below puts the switch only on cells that carry a Consumer class. `?fraction=` gives the
candidate's class to that share of the 800 cells (`cost-fraction_0.05-*.json`,
`cost-fraction_0.25-*.json`; browser p50, ms):

| | idle | slow scroll | fling | trickle | idle after 3 flings (then after a forced GC) |
|---|---|---|---|---|---|
| none | 0.1–0.2 | 6.9–7.0 | 16–17 | 4.0–4.3 | 0.1 (0.1) |
| **B**, 5% of cells | 0.2 | 7.9 | 22.7 | 3.9 | 5.1 (0.4) |
| **B**, 25% | 0.2 | 10.3 | 37.1 | 4.2 | 22.9 (1.4) |
| B, 100% (§3a) | 0.6 | 18.1 | 92.9 | 4.8 | 46.6 (2.5) |
| **A**, 5% | 0.2 | 8.1 | 31.8 | 4.0 | 13.3 (0.4) |
| **A**, 25% | 0.3 | 12.0 | 65.6 | 5.0 | 65.4 (1.5) |

*(The flings were time-capped, so "3 flings" replaced 720,000 cells at 5%, and between 324,000 and
666,000 at 25% and 100%; the column is a direction, not a rate.)* The cost scales with the number of
cells carrying the switch, and the accumulation scales with it too: at 5%, B's fling is a third
dearer than today and idle frames reach 5 ms after a long session of scrolling. A Consumer rule
that classes one column in twenty pays about the 5% row; one that classes every row pays the
100% row.

## 4. Tests

### What layer 2 (bUnit) would lose

bUnit has no layout, so it cannot know whether a value overflows. Every assertion that pins the
**outcome** of the decision moves to layer 3:

| today, layer 2 | under CSS |
|---|---|
| `OverflowRenderingTests.An_overflowing_number_cell_paints_hashes` — text is all `#` | **lost**: the text is always the value. What remains in layer 2 is that a numeric cell carries `ex-cell-numeric` and its raw text — i.e. the classification, not the decision |
| `AccessibilityTests.A_hashed_cells_accessible_name_is_the_value` (A11Y-7) — `aria-label` = value, text `####` | becomes **true by construction** (the text is the value), so the test shrinks to "no numeric cell carries an `aria-label`"; the real A11Y-7 check moves to layer 3 (the alt text is what keeps `####` out of the name) |
| `The_focused_value_is_the_raw_value_never_the_hashes` (FN-20) | keeps its `GetFocusedValue` half; loses the `StartsWith("#")` precondition |
| `RenderAllocationTests` (PF-3) — the `####` run interned | the hashed case disappears; nothing to intern |
| `OverflowRuleTests` (layer 1) for `Decide` | lost with `Decide`. `HashesWhenOverflowing` (the classification) and every width-estimate test stay — the estimate still sizes columns (§5) |
| `ActionAndTemplateColumnTests` — a template never hashes | becomes "a template cell never carries `ex-cell-numeric`" |

### What layer 3 (Playwright) would have to become

- **The decision is read from the browser, after a frame.** `getComputedStyle(cell, '::after').display
  === 'block'` (or the timeline's state) is right only after a frame has run — two
  `requestAnimationFrame`s in the probe. `expect.poll` covers it.
- **"Does it overflow?" inverts.** FN-12e asserts `scrollWidth > clientWidth` is **false** for a
  `####` cell today. Under CSS a hashed cell **always** overflows (that is the trigger), so the
  assertion becomes "overflowing ⇔ the run is shown", plus "the run's glyphs lie inside the content
  box" (read from a replica with a `Range`, as the probe does — pseudo-element text cannot be
  ranged).
- **Hiding is a paint, so it is a pixel assertion.** That the digits are not visible cannot be
  read from the DOM — the text is there by design. The probe's method: paint digits and run in two
  distinguishable colours and count pixels. Needed under forced colors too, which is where it broke.
- **A11Y-7 becomes a CDP accessibility-tree read** (`Accessibility.getFullAXTree`), since the
  name now depends on the CSS alt text, not on an attribute.
- **First-frame correctness needs a frame-exact harness** (`beginFrame` in the headless shell, as
  here). The headed CI run cannot see a single wrong frame; without this the recycled-row case has
  no test at all.
- **UX-6** fails with A by construction (it reads `animation-name`); with B it stands as is.
- **The cost of §3 is outside every layer.** A test cannot see "idle frames get slower the longer
  you scroll", and performance never gates — the strongest objection is exactly the kind CI does
  not catch.

## 5. What still needs C# widths, and where C# and CSS would disagree

The estimate does not go away. Auto width, Size to fit, ExSheet's General-format fitting and the
widening of a column on entry all **choose a width before anything is painted**, and CSS cannot
choose a width for a column it has not laid out. *(ExSheet's General fitting and widen-on-entry are
named in the brief; no ADR on `main` specifies them yet — ADR-0047 on `main` is Find. Excel's
behaviour is assumed below.)*

| where | C# says fits, CSS says overflows | C# says overflows, CSS says fits |
|---|---|---|
| **Auto width** (grows to the estimate, clamped to `MaxWidth`) | Only when typography is wider than the metrics (a Consumer class above weight 600, a larger size, letter-spacing, a family the Wrapper did not pay for). The reader sees **`####` in a column the grid sized itself** — safe (the focused-value display and copy still give the value) but surprising, and Auto never fixes it because Auto only grows from the estimate. Today the same case is a clipped number | the column is a few px wider than needed — harmless; nothing hashes |
| **Size to fit** | `####` remains right after an explicit fit — the "wrong surprise" ADR-0016 already names, now in a safe form | a little slack |
| **ExSheet General format** (round, then exponent, to fit) | C# chooses `1234.568` believing it fits, CSS hashes it: **`####` where Excel would have shown a rounded number** — safe, but General's whole point lost on that cell | C# rounds harder than needed: **fewer digits than the width allows**. The value shown is a correct rounding of the real one, as Excel's are — not quietly wrong, just less precise than it could be |
| **Widen on entry** (a committed number widens the column) | the column widens to the estimate and the value just typed shows **`####`** immediately after Enter | the column widens more than needed |
| **Header estimate** | headers are Text, cut with an ellipsis by CSS already; untouched | — |

Two seams appear that do not exist today:

- **Nothing in C# knows which cells are hashed any more.** ADR-0016's "tooltip on hover" for a
  hashed cell, or any Chrome affordance shown only behind `####`, cannot be keyed on it without a
  layout read (ADR-0021). Such an affordance would have to apply to every numeric cell.
- **The two halves stop being checked against each other.** Today the estimate both sizes and
  decides, so a column the estimate fits never hashes. With CSS deciding, a too-small estimate is
  no longer quietly wrong — it shows as `####` in a fitted column — which is better on the danger
  axis and worse on the "the grid's own fit contradicts itself" axis.

The one combination that is safer than today on every row of the table above is **both**: C#
keeps deciding (and sizing) as now, and CSS acts as a backstop that can only add `####`, never
remove it. Its price is §3's, paid on whichever cells carry it.

## Options

1. **Replace the C# decision with CSS on every numeric cell (A or B).** Correct, and robust to any
   typography. Not recommended, on cost: a fling at 5–10× today's browser time, and per-frame cost that
   grows with the number of cells scrolled past until a garbage collection. A also breaks P8/UX-6
   and drops decorating states from hashed cells. Both need a forced-colors opt-out the C# design
   does not.
2. **C# decides; CSS (B) as a backstop on every numeric cell.** Safer than today everywhere in §5,
   same cost as option 1 with B. Not recommended, for the same cost.
3. **C# decides; CSS (B) as a backstop only on cells that carry a Consumer cell class.** The cost
   scales with the classed cells (§3d: at 5% of cells, a slow scroll costs 7.9 ms against 6.9 and
   a fling 22.7 against 17). Keeps the Consumer API open to typography. It
   is a second mechanism alongside the first, carrying the accumulation in proportion to how many
   cells a Consumer highlights: one column in twenty pays §3d's 5% row, and a rule that classes
   every numeric cell pays option 2's price.
4. **Keep the C# decision and make Consumer cell classes a contract (recommended).** A per-cell
   class from the Consumer is **appearance**, as a Theme is, and may set colour, background,
   decoration and `font-weight` up to 600 — the weight the defaults already cover, because
   ADR-0016 measured them for the bold group and total rows. A class that changes glyph width
   beyond that (weight above 600, size, letter-spacing, family) is **metrics-bearing** and owes
   `CellMetrics`, exactly as ADR-0027 already obliges a Theme. No new mechanism, no frame cost, and
   the existing obligation simply gains one more party. Its weakness is the one ADR-0027 accepted
   for Themes: the core cannot see a class break the contract. Layer 3 can — the probe's method
   (overflow read from geometry against the core's decision) is a ready assertion for a
   Consumer-classed demo page.

## Proposal: the ADR-0016 change (not applied)

A new section for ADR-0016, after "The estimate charges per character class":

> ### The browser could decide `####` — measured, and not adopted *(2026-10-03)*
>
> Asked because a Consumer cell class that changes typography would make the estimate quietly
> wrong. Two CSS mechanisms detect overflow at layout time on `.ex-cell` as it stands — a
> scroll-driven animation on the cell's own scroll timeline, and a `scroll-state(scrollable)`
> container query — and both work: no value that fits is hashed, every overflow of a pixel or more
> is caught, a sub-pixel miss is painted whole inside the end padding, the run is Excel's (as many
> `#` as fit, whole glyphs), the accessible name stays the value (with `content: "…" / ""`), and
> the first painted frame after any change is already right. Measured in Chromium 141
> (`docs/research/css-decided-overflow.md`).
>
> **Not adopted, on cost.** The browser's time per frame grows with every cell that carries the
> switch — a fling costs 5× (container query) to 10× (timeline) what it does today — and keeps
> growing with every cell that has scrolled past until the garbage collector runs: idle frames
> went from 0.6 ms to 47 ms after three flings. That is cost as a function of the history, not of
> the Viewport, which is what ADR-0004 exists to refuse. The timeline form also animates inside the
> Viewport (P8), and both forms need forced colors switched off on numeric cells to keep the
> digits hidden.
>
> **So the estimate stays the decision, and typography stays metrics-bearing wherever it comes
> from.** A Consumer's per-cell class is appearance, like a Theme: it may change colour,
> background, decoration, and weight up to 600, which the defaults already cover. A class that
> changes glyph width beyond that owes `CellMetrics`, under the obligation ADR-0027 already states
> for a Theme. Revisit if Blink stops servicing detached scroll timelines and scroll-state
> containers between collections — the measurement in `spikes/render-bench` reruns in minutes.

If the user prefers option 3, the change would instead say that a cell carrying a Consumer class
is also checked by the browser, that the backstop can only add `####`, and what it costs per
classed cell — and UX-6 stands, because option 3 uses the container query.

## Reproducing

```sh
cd tests/ExGrid.Browser && npm ci          # Playwright, borrowed by the spike tools
cd ../../spikes/render-bench

# feasibility (both candidates), first frame, real grid
xvfb-run -a node tools/css-overflow-probe.mjs
CANDIDATE=candidate-scroll-state.css xvfb-run -a node tools/css-overflow-probe.mjs
node tools/css-overflow-frames.mjs
CANDIDATE=candidate-scroll-state.css node tools/css-overflow-frames.mjs
# with the DemoHost on :5310
xvfb-run -a node tools/css-overflow-demohost.mjs http://localhost:5310/features

# browser-side cost without Blazor (fresh page per pair, then accumulation)
xvfb-run -a node tools/css-overflow-cost.mjs
EXTRA="&width=160" ACCUMULATION=0 xvfb-run -a node tools/css-overflow-cost.mjs

# through Blazor: the bench mode, one fresh page per overflow mode
nix develop -c dotnet run -c Release --project Bench.Host --urls http://0.0.0.0:5199
#  … open http://localhost:5199/?overflow=Css and press "Measure overflow paint (frame cost)",
#  or drive it: tools/cdp-run.mjs "http://127.0.0.1:5199/?overflow=Css" null 2400 null null "Measure overflow paint" save
```
