# Definition of Done — when ExGrid is finished

This document exists for one reason: **"I wrote the code and the build is green" is not done.**
It states, for every area of the component, what has to be true before ExGrid can be put in front
of a user who is reading money and risk numbers off it, and it states each of those things in a
form somebody else can check without asking the author what they meant.

It is written so that **an implementing agent who has read nothing else can execute the final
verification from this file alone.** Section 22 is the run order.

It settles nothing. Every criterion here is traceable to an ADR; where the ADRs do not decide
something, it is not decided here either — those are collected in section 21, and **the criteria
that depend on them are marked `BLOCKED` rather than guessed at.**

---

## 1. How to read a criterion

| Level | Meaning |
|---|---|
| **MUST** | Release is not possible while this fails. No exceptions, no sign-off. |
| **SHOULD** | Expected to hold. A failure needs a written reason in the release notes naming which criterion and why it was accepted. |
| **OBSERVATIONAL** | Recorded and compared against the previous run. **No fixed threshold, and never a gate.** A large move is a prompt to investigate, not a failure. |

**"Release" means a version without a prerelease suffix.** A prerelease — `0.1.0-beta.N` — ships
before the sign-off, on the terms of
[ADR-0042](adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md): every
layer green in CI, the commit on `main`, and release notes naming what this document still owes.
It is not a release in this document's sense, and nothing here is relaxed for it. *(Added
2026-09-24.)*

Every criterion has an **ID**, a **statement**, a **verification** (the exact command or scenario)
and a **pass condition** (what result counts). If a verification cannot be run, the criterion is
**not** passed — "could not check" is a fail, recorded as such.

### The one rule that shapes the whole document: timing never gates

`AGENTS.md` says it outright — *"Never gate on performance. It swings with the environment — the
same measurement gave a maximum of 11.7 ms headless and 19.7 ms on real hardware."* ADR-0004 says
its own measured table is *"Not a gate"*.

So **no criterion in this document fails a release because a number of milliseconds was too
large.** What gates instead are the **structural invariants that produce the timing** — how many
DOM nodes exist, how many components render, whether interop happens per cell, whether allocation
scales with the data. Those are exact, environment-independent and mechanically checkable.
Milliseconds are recorded as OBSERVATIONAL beside them.

This is not a softening. A component can be slow on a slow machine and still be correct; a
component whose DOM node count grows with the row count is broken everywhere, and that is the
thing being tested.

### Evidence

A run of this document produces one directory, `verification/<date>/`, holding:

- `layer1.log`, `layer2.log`, `layer3.log` — full test output
- `console.json` — every console message and page error captured during the layer-3 run
- `metrics.json` — the OBSERVATIONAL numbers, in the shape section 22 defines
- `results.md` — this document's checklist with each ID marked pass / fail / blocked / n-a, and
  for every SHOULD failure the written reason

Without that directory, the verification did not happen.

---

## 2. What "finished" covers

**In scope: the `ExGrid` package** — the core component and its bundled `GridSource`. That is what
`CONTEXT.md` calls the specified product.

**Out of scope for this document:** `ExSheet` (future), and the Wrapper packages
`ExGrid.MudBlazor` / `ExGrid.Fluxor`. The criteria that describe the Wrapper boundary (§4) are
written as properties of the *core's contract*, verifiable with a stub Wrapper, and do not require
a real one. **`ExGrid.MudBlazor`'s own release is judged by §23**, which holds the Wrapper to the
core's criteria with a real design system and adds the criteria only a real one can fail
(added 2026-09-24).

**"Finished" means every ADR from 0001 to 0030 is implemented.** This was asked as an open
question and answered deliberately: the narrower alternatives — shipping the display-only slice
first, or shipping everything except the Cell Editor and `Density` — were both on the table and
both refused. The consequence is accepted rather than discovered: **the whole of §21 blocks the
release**, including the ARIA surface that ADR-0029 records as not designed and the two gestures
(column resize, column reordering) that no ADR specifies. Nothing here may be marked done while
any of them is open.

### Preconditions — checked first, because everything else assumes them

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PRE-1** | MUST | The whole solution builds with zero warnings | `nix develop -c dotnet build ExGrid.slnx` | `0 Warning(s)` and `0 Error(s)` |
| **PRE-2** | MUST | The shipped package targets `net8.0` only, and raises no `LangVersion` (ADR-0022) | inspect `src/ExGrid/ExGrid.csproj` | single `<TargetFramework>net8.0` and no `LangVersion` above the default for the SDK's C# 12 |
| **PRE-3** | MUST | `ExGrid` has no package dependency (ADR-0019) | `dotnet list src/ExGrid/ExGrid.csproj package` | no top-level package other than framework references |
| **PRE-4** | MUST | The core, `src/ExGrid/`, references no Wrapper and no design system (ADR-0019/0030); the Wrapper packages beside it in `src/` depend on the core, never the reverse | `dotnet list src/ExGrid/ExGrid.csproj reference`, and grep `src/ExGrid/` for a `using` of `MudBlazor` or `Fluxor` | no project reference; no match. *(Rewritten 2026-09-24: it used to grep all of `src/` for the bare words, which `src/ExGrid.MudBlazor/` — the Wrapper, living there by ADR-0019 — matches throughout, and so does a comment in the core naming `MudDataGrid`. What it asks is unchanged: that the dependency points one way)* |
| **PRE-5** | MUST | The layer-3 project declares **both** target browsers (ADR-0017, ADR-0026) | inspect `tests/ExGrid.Browser/playwright.config.mjs` | a `projects` array naming `chrome` and `msedge`; a single-browser config fails this outright |
| **PRE-6** | MUST | Every file the build needs is git-tracked | `git status --porcelain` after a clean build | no untracked file under `src/` |

---

## 3. Functional requirements (FN)

The feature set the ADRs specify. Each row is a capability that must exist and behave as its ADR
says; the areas that need more than one line get their own section below.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **FN-1** | MUST | The push entry point works: the grid paints the `Window` it is given, at `WindowStart`, of `TotalCount`, and holds no data of its own (ADR-0001) | Layer 2 | rows painted correspond to the pushed Window; no field in the component retains a Window after the parameter changes |
| **FN-2** | MUST | Passing both `Window` and `Source`, or neither, is **refused by name** (ADR-0001) | Layer 1/2 | an exception whose message names both parameters; not a silent winner |
| **FN-3** | MUST | A Range Request is raised for the visible range, after a render, never during one, and is not repeated until the Window moves (ADR-0001) | Layer 2, counting invocations | exactly one request per Window position; a Consumer answering synchronously does not re-enter |
| **FN-4** | MUST | `GridSource.From` and `GridSource.Fetch` drive the same push path — one behavioural path, not two (ADR-0001) | Layer 2: same scenario through both entry points | identical painted output and identical render counts |
| **FN-5** | MUST | The bundled fetching Source breaks its own cold start, coalesces, and discards superseded answers (ADR-0025) | Layer 1 | as ADR-0025's clauses, each with its own test |
| **FN-6** | MUST | Pinned Columns are the leading N, always painted, and never virtualised away (ADR-0004) | Layer 2 + Layer 3 | pinned cells present at every scroll offset including mid-fling |
| **FN-7** | MUST | Cell State (`Normal/Stale/Missing/Error/Modified`) reaches the DOM as the closed class vocabulary and nothing else (ADR-0006/0029) | Layer 2 | exactly the `ex-state-*` classes; no Consumer vocabulary crosses the boundary |
| **FN-7a** | MUST | The Column's tone rule (`None/Positive/Negative`) reaches the DOM as the closed class vocabulary and nothing else; the grid derives no tone of its own, a null value is never offered to the rule, and the text, copy and raw forms are untouched by it (ADR-0006/0029) | Layer 2; Layer 3 under the Wrapper | exactly the `ex-tone-*` classes; a negative in a column without a rule is an ordinary cell; under the Wrapper a `Negative` cell is the palette's error colour and its neighbour the ink |
| **FN-8** | MUST | Row Kind (`Detail/Group/Total`) paints as a declared role carrying no depth and no aggregate (ADR-0024) | Layer 2 | `ex-row-group` / `ex-row-total` classes; no hierarchy API exists |
| **FN-9** | MUST | Action and Template Columns render as plain markup with one handler per cell region, not a component per cell (ADR-0020/0003) | Layer 2 render-count test | component instance count is a function of painted rows only |
| **FN-10** | MUST | `####` for numbers and dates that do not fit; ellipsis for text and boolean; never a truncated number (ADR-0016) | Layer 1 (`OverflowRules`) + Layer 2 | the decision is total over column types and matches ADR-0016's table |
| **FN-11** | MUST | Auto width only ever grows, is clamped to `[MinWidth, MaxWidth]` (defaults 40 / 400), and is never persisted (ADR-0016) | Layer 1 + Layer 2 | monotone under any observation order; a narrower screen never narrows a column |
| **FN-12** | MUST | A `Fixed` width below its own `MinWidth` is refused when the column is constructed, not clamped; one above `MaxWidth` is the user's and is kept, since `MaxWidth` bounds only what the grid computes (ADR-0016, rewritten 2026-09-25) | Layer 1 + Layer 2 | throws below, naming the column, for every kind of column; a drag past `MaxWidth`, recorded as `Fixed(px)` with the default bounds, paints at that width |
| **FN-12a** | MUST | A double-click on a column's grip is Size to fit: measured over the whole Window, not only the painted rows, and bounded by `[MinWidth, MaxWidth]` (ADR-0016, 2026-09-25) | Layer 2 + Layer 3 | a value in the Window but outside the painted rows sets the width; a column dragged past `MaxWidth` comes back to `MaxWidth` |
| **FN-12b** | MUST | A press and release on the grip with less than 4px of movement reports no width (ADR-0016, 2026-09-25) | Layer 2 | no `ColumnWidthChange`; an Auto column stays Auto |
| **FN-12c** | MUST | When the grabbed column is covered by a whole-column range, a drag gives every column covered by a whole-column range the new width, and a double-click fits each to its own content (ADR-0016, 2026-09-25) | Layer 2 | one `ColumnWidthChange` per covered column; a range not spanning every row takes no part |
| **FN-12d** | MUST | A header's required width counts its label, the menu button's band and, for a sortable column, the sort indicator, in both Auto and Size to fit; full-width characters are charged at 1em (ADR-0016, 2026-09-25) | Layer 1 + Layer 3 in both Chromes | a Japanese header and a sorted header paint whole, with no ellipsis, at their fitted width |
| **FN-12e** | MUST | Each `CellTextMetrics` default is at least the widest glyph measured for its class on Windows, macOS and Linux, and the measurements are recorded in ADR-0016 (2026-09-25) | by hand on each platform; Layer 3 on Linux | a bold amount estimated to fit paints without an ellipsis on each platform; `####` itself fits its cell |
| **FN-13** | MUST | Sort and Filter travel as a serialisable structured model; no `Expression<Func<TRow,bool>>` appears in public API (ADR-0002) | inspect public surface; round-trip a `GridQuery` through `System.Text.Json` | round-trips equal; no `Expression` type in any public signature |
| **FN-14** | MUST | `GridSource.From` is the reference implementation of filter and sort semantics, pinned exhaustively for null ordering, case sensitivity and culture (ADR-0001/0023) | Layer 1 | every clause of ADR-0023 has a named test |
| **FN-15** | MUST | The Consumer's sort and filter state travels in and the grid never sorts or filters (ADR-0001) | inspect: no ordering or predicate evaluation inside `ExGrid.razor` | grep finds no `OrderBy` / `Where` over `Window` in the component |
| **FN-16** | MUST | A pager exists when `PageSize` is passed, and rides the same Range Request machinery (ADR-0015) | Layer 2 | a page click raises the same notification shape as a scroll |
| **FN-17** | MUST | The Chrome seams exist and are substitutable: filter panel, column menu, Context Menu, cell editor, the cell's message, loading indicator (ADR-0009/0010/0034/0036) | Layer 2 with a stub `IGridChrome` | swapping Chrome changes rendering and **nothing** about behaviour — the same key and mouse tests pass against both |
| **FN-18** | MUST | Chrome renders and calls back; the core decides the menu items and the allowed operators (ADR-0009/0010) | inspect the contexts | `ColumnMenuContext.Commands` and `FilterPanelContext.Allowed` are produced by the core; Chrome has no way to add or reinterpret one |
| **FN-19** | MUST | The selected-cell count is available without data, and an off-screen selection is reported as such (ADR-0014/0015) | Layer 1 + Layer 2 | count equals the sum of rectangle areas; the off-screen flag is the rectangle/visible-range intersection test |
| **FN-20** | SHOULD | The focused cell's full value is available to Chrome for the formula-bar role behind `####` (ADR-0016) | Layer 2 | the raw value, never the `####` string |
| **FN-21** | MUST | A Chrome seam's contents may hold an **Inner Popup** drawn outside the instance root. While one is open: Escape closes the Inner Popup first and the next Escape the popover; under the design system's default (`ModalOverlay = false`) a pointer-down elsewhere in the instance closes both and keeps its own meaning, and under `ModalOverlay = true` only the Inner Popup closes; closing the popover any way removes the Inner Popup; DOM focus returns to the root (ADR-0039) | Layer 2: the contents report the popup through `InnerPopupChanged`, and the gate is told; Layer 3 with `ExGrid.MudBlazor`'s filter panel — a `MudSelect` and a `MudDatePicker` — by pointer and by key, under both settings, with two grids on the page and with one inside a `MudDialog` | every clause observed; the other grid unaffected |

---

## 4. UI / UX and the presentation contract (UX)

ADR-0027–0030 make this area unusually checkable: the contract is a *list* of classes and tokens,
so conformance is a set comparison rather than a judgement of taste.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **UX-1** | MUST | No colour is a C# parameter, and no pixel is only a stylesheet value (ADR-0027) | grep the public surface for colour-typed parameters; grep `ex-grid.css` for length literals | zero colour parameters; every length in the stylesheet is a `var(--ex-*)` read or a non-geometric visual value |
| **UX-2** | MUST | Geometry Tokens are emitted **inline on the instance root** and are read-only (ADR-0027/0028) | Layer 3: set `--ex-row-height` on an ancestor and in a stylesheet | painted row height is unchanged; the inline declaration wins |
| **UX-3** | MUST | The painted row height equals the declared `RowHeight`; likewise header height and cell padding-x (ADR-0027 P6, ADR-0030) | Layer 3 with a stub Wrapper stylesheet loaded: `getComputedStyle(.ex-row).height` etc. | equal to the resolved Grid Metrics, exactly, at every density preset |
| **UX-4** | MUST | The stable class list is exactly ADR-0029's table; internal classes carry no contract | Layer 3: enumerate classes present under the root | every class matches the stable table or the internal list; nothing unnamed appears |
| **UX-5** | MUST | Visual Tokens set on an ancestor element reach the grid, with **zero .NET renders** (ADR-0027 P3) | Layer 2: change the wrapping element's `style`; Layer 3: recolour and count | `RenderCount` unchanged; the paint changes |
| **UX-6** | MUST | Nothing inside `.ex-viewport` transitions or animates (ADR-0027 P8) | Layer 3: for every element under `.ex-viewport`, read `transition-property` / `animation-name` | `none` throughout, with the core's stylesheet and with a Wrapper's loaded |
| **UX-7** | MUST | A `@media (forced-colors: active)` block restates every Cell State and Row Kind in painted system colours (ADR-0027/0029) | Layer 3 with `Emulation.setEmulatedMedia` forcing forced-colors | every state remains distinguishable; no state relies on a discarded background image alone |
| **UX-8** | MUST | An untouched grid follows the host's `color-scheme`, and the core declares none of its own (ADR-0027) | Layer 3 under `prefers-color-scheme: dark` | text and ground both readable; contrast ratio of body text ≥ 4.5:1 measured from the computed colours |
| **UX-9** | MUST | The focus outline and the selection fill remain visible under the default Theme and under a stub Wrapper Theme (ADR-0030) | Layer 3: contrast of `ex-focus` outline against the cell ground | ≥ 3:1 |
| **UX-10** | SHOULD | `--ex-scrollbar-width` narrows the bar, the gutter changes, and the geometry follows (ADR-0029) | Layer 3 | the Focus stays inside the readable area after the change (the `scrollbar.spec.mjs` invariant) |
| **UX-11** | MUST | Popovers (filter panel, column menu, Context Menu) are never cut — not by the scroll container, and not by any ancestor that shows the grid whole: each stays inside its grid's box and scrolls within itself when its contents are taller; they do not tangle across instances (ADR-0017/0018/0040) | Layer 2: the inline `max-height` from the geometry; Layer 3: open the filter on the rightmost column of two grids, and a menu in a short grid inside a `MudDialog` | fully visible; each grid's popover is its own; a menu taller than the grid scrolls |
| **UX-12** | MUST | Accessibility semantics as ADR-0033 specifies them | see **§4.1** | every row of §4.1 passes |
| **UX-13** | MUST | The row under the pointer is highlighted by an overlay band filled with `--ex-row-hover-background`, on hover alone — no row carries a class, and the band vanishes on leave (ADR-0029, ADR-0021 fifth entry) | Layer 3: real mouse moved down a column of two grids | one band, in the hovered instance only, following the pointer row; none after `mouseleave` |
| **UX-14** | MUST | The chosen action of an Interactive cell is tellable — outlined through `--ex-focus-outline`, and restated in the forced-colors block like every other state (ADR-0029/0037) | Layer 3 on `/cells`, with and without forced colors | the chosen button's computed outline is non-`none` in both, and no other button's is |
| **UX-15** | MUST | With `StripeRows` on, every row at an odd position in the whole result carries `ex-row-stripe` and is filled with `--ex-row-stripe-background`, **pinned cells included**; off by default; the stripe stays with its row across a scroll, across a pager's pages and on Placeholder rows (ADR-0038) | Layer 2: the class by absolute index at three scroll offsets and under a pager; Layer 3: a pinned and a scrollable cell of one striped row paint the same ground | exact parity at every offset; equal colours |
| **UX-16** | MUST | Meaning paints over a stripe: Row Kind's and Cell State's grounds win, a group or total row still counts in the parity, the overlays paint above, and the forced-colors block paints no stripe (ADR-0038/0029) | Layer 3, including forced-colors emulation | each ground as stated; the row after a group row keeps its parity |

---

### 4.1 Accessibility (A11Y) — ADR-0033

The surface is the root's. Nothing here may be satisfied by putting selection state on a cell:
A11Y-8 exists to catch exactly that.

**These are the structural half only.** Interaction semantics — what the Cell Editor and
Interactive mode announce — are settled inside their own tasks (ADR-0029/0010/0020), on the
grounds that ARIA describing an interaction that does not exist yet is an accessibility tree that
lies. A release that ships the editor ships its criteria with it; this table does not stand in for
them. *(Interactive mode's were settled with the mode, ADR-0037: A11Y-17 and A11Y-18.)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **A11Y-1** | MUST | `role="grid"` on the root; `row` / `gridcell` / `columnheader` beneath | Layer 2 | present on every painted element of that kind |
| **A11Y-2** | MUST | `aria-rowcount` / `aria-colcount` state the **total**, not the DOM slice | Layer 2 at `TotalCount` 1,000,000 with ~40 rows painted | `aria-rowcount="1000000"` |
| **A11Y-3** | MUST | `aria-rowindex` / `aria-colindex` are **absolute** on both axes | Layer 2 scrolled to row 500,000 and column 30 | first painted row's index is its absolute one, not 1 |
| **A11Y-4** | MUST | One tab stop: the root takes focus, no cell does | Layer 3, Tab from before the grid then Tab again | focus enters the root, then leaves the grid entirely |
| **A11Y-5** | MUST | `aria-activedescendant` names the Focus cell's id, and ids are instance-prefixed (ADR-0018) | Layer 2 with two grids on one page | ids differ between instances; the attribute resolves to an element that exists |
| **A11Y-6** | MUST | When a wheel scroll takes the Focus out of the Window, `aria-activedescendant` is **cleared**, and restored when the Focus is painted again | Layer 3: scroll by wheel until the Focus row leaves, then back | never names a missing id; never forces a scroll back to the Focus |
| **A11Y-7** | MUST | A `####` cell's accessible name is the real value (ADR-0016) | Layer 2 with a narrowed numeric column | accessible name is the number; text content is `####` |
| **A11Y-8** | MUST | **No element carries `aria-selected`, in any selection state** (ADR-0033) | Layer 2 after a 400-cell drag: `grep` the rendered markup | zero occurrences |
| **A11Y-9** | MUST | The selection extent is announced once per **settled** selection, not per intermediate rectangle | Layer 3, drag across 200 rows, count live-region writes | exactly one write after the drag settles |
| **A11Y-10** | MUST | A Focus move that changes no selection announces nothing | Layer 3, arrow keys with no Shift | live region unchanged |
| **A11Y-11** | MUST | Placeholder rows carry `aria-busy="true"` and are not read as values (ADR-0004) | Layer 2 before the Window arrives | attribute present on every Placeholder row |
| **A11Y-12** | MUST | Selection Overlay elements are `aria-hidden="true"` (ADR-0008) | Layer 2 | no unnamed `div` appears as a child of `role="grid"` |
| **A11Y-13** | MUST | A Header Group cell carries `aria-colspan` equal to its member count (ADR-0032 hands this to ADR-0033) | Layer 2 with a three-member group | `aria-colspan="3"` |
| **A11Y-14** | MUST | Announcing costs no row render — A11Y is not a way back into ADR-0008's rejected path | Layer 2 render counts across a drag | identical to the counts RR-4 asserts without a live region |
| **A11Y-15** | MUST | A **Reject** is announced: the verdict's message is relayed once into the root's `polite` live region, and the editor carries `aria-invalid` (ADR-0033/0034) | Layer 2 | one live-region write per Reject, carrying the Consumer's text verbatim; nothing written on Accept or Flag |
| **A11Y-16** | MUST | Whatever Chrome renders a **refusal** into is a live region — the grid holds no string for it and cannot announce it (ADR-0035) | Layer 3 against the reference Chrome | the DemoHost's refusal status is a live region and a refusal writes into it |
| **A11Y-17** | MUST | The grid's own action buttons are outside the page's tab sequence (`tabindex="-1"`), so the root stays the one tab stop with an Action Column on screen (ADR-0033/0037) | Layer 2 + Layer 3 on `/cells`: Shift+Tab from the element after the grid | every `.ex-action` carries `tabindex="-1"`; focus lands on the root, not on a button |
| **A11Y-18** | MUST | While a cell with several actions is Interactive, `aria-activedescendant` names the chosen action's button, whose accessible name is its declared label; leaving restores the Focus cell's id, and the attribute is cleared while that button is not painted (ADR-0033/0037) | Layer 2 | the id resolves to the chosen `.ex-action`; after Escape it resolves to the cell again |
| **A11Y-19** | MUST | A popover that takes DOM focus is named: the menus are `role="menu"` with `menuitem` items, the filter panel `role="dialog"`, and a column's menu and panel carry `aria-label` set to that column's own header text — no sentence of the core's (ADR-0033/0036/0039) | Layer 2, under the built-in Chrome and `ExGrid.MudBlazor`'s | roles and names as stated under both |

### 4.2 Header Groups (HG) — ADR-0032

A Header Group is a **declared rectangle over leaf columns**, not a column tree. Every criterion
below is a property of that model; ADR-0032 states that the bUnit layer can pin all of it, because
the positions are strings computed from `ColumnGeometry`.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **HG-1** | MUST | Membership is a **set**: the painted left-to-right order comes from the flat `Columns` order, never the declaration's | Layer 2, declare members in a scrambled order | the rectangle spans the same columns, in `Columns` order |
| **HG-2** | MUST | Member count is the colspan; `tierSpan` is the rowspan, defaulting to 1 | Layer 2 | `width` spans exactly the members; `height` = `tierSpan × HeaderHeight` |
| **HG-3** | MUST | A column no tier covers stretches its leaf header the **full band**, with nothing declared | Layer 2 with one uncovered column beside a two-tier group | the uncovered header is band-tall, label centred |
| **HG-4** | MUST | A member name no column carries is **refused at declaration** (ADR-0032) | Layer 1 | throws, and the message names the unknown member |
| **HG-5** | MUST | Members that are **not adjacent in the current order** are refused | Layer 1, and Layer 2 after a reorder that separates them | throws, naming adjacency — this is the check that replaced index declarations |
| **HG-6** | MUST | Overlapping rectangles are refused | Layer 1 | throws |
| **HG-7** | MUST | A rectangle **straddling the pinned boundary** is refused | Layer 1 with `PinnedColumnCount` splitting a group | throws, naming the boundary — never drawn half-sticky |
| **HG-8** | MUST | The band is `(1 + tierCount) × HeaderHeight`, and `first row = floor(scrollTop / RowHeight)` is unchanged by it | Layer 2 at 0, 1 and 2 tiers | the first painted row index is identical at every tier count |
| **HG-9** | MUST | The band comes off the scroll budget, and `ViewportHeight` must exceed **band + one row** — the refusal names the band | Layer 2 with a viewport just under the band | throws, and the message says band, not height |
| **HG-10** | MUST | A rectangle stands correctly **whether or not its leaves are in the DOM** — the collision ADR-0004 recorded as open | Layer 2 scrolled so a group's first members are virtualised away | `left` and `width` unchanged from the unscrolled case, minus the scroll |
| **HG-11** | MUST | Members resolve to indices **once per push**, off the render path | Layer 2 render counts across 20 scroll steps | resolution count is 1, not 20 |
| **HG-12** | MUST | A group label **never hashes and never feeds Auto width**; an over-long label gets the Text ellipsis (ADR-0016) | Layer 2 with a long label over narrow columns | no `####`; the member columns keep the widths their own contents earned |
| **HG-13** | MUST | Labels are centred by default; there is **no vertical alignment option anywhere in the grid** | Layer 2 + `grep -ri "vertical-align\|align-items: \(start\|end\)" src/ExGrid/wwwroot/` | centred by arithmetic; no vertical alignment parameter exists |
| **HG-14** | MUST | Grabbing a group moves the whole group; grabbing a leaf reorders **within** its group and the indicator clamps at the group's edge (ADR-0011/0032) | Layer 3 | a leaf drag never escalates into a group move, at any overshoot |
| **HG-15** | MUST | **Membership never changes by gesture** — `HeaderGroups` survives every drag unchanged | Layer 3: reorder within a group, then a whole group | the declaration is byte-identical afterwards |
| **HG-16** | MUST | Nothing below the band knows tiers exist: headers stay unselectable, Focus never enters the header, no header row is ever copied | Layer 2 + Layer 1 | unchanged from the untiered case |
| **HG-17** | MUST | Cost is **per rectangle**, never per column (ADR-0008's economy) | Layer 2 render counts, 3 groups over 40 columns | element count scales with groups; row renders unchanged |

### 4.3 Layout direction (DIR) — ADR-0031

RTL **content** is supported; RTL **layout** is refused. The distinction is the criterion.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **DIR-1** | MUST | The instance root carries `dir="ltr"` explicitly | Layer 2 | the attribute is present on the root |
| **DIR-2** | MUST | Inside an ancestor with `dir="rtl"` the grid stays an LTR island — the selection overlay still lands on its cells | Layer 3 with `<div dir="rtl">` wrapping the grid | overlay rectangles coincide with their cells to within 1px; columns still flow left to right |
| **DIR-3** | MUST | An Arabic or Hebrew value renders correctly **inside** that LTR context | Layer 3 | the value reads right-to-left within its cell; the cell does not move |
| **DIR-4** | SHOULD | No CSS logical property has crept in — the geometry is physical by decision, not by omission | `grep -nE "inline-start\|inline-end\|margin-inline\|padding-inline\|inset-inline\|text-align: *(start\|end)" src/ExGrid/wwwroot/ex-grid.css` | no match |

## 5. Virtualisation (VZ)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **VZ-1** | MUST | The number of DOM elements under the instance root is a function of the Viewport, never of `TotalCount` (ADR-0004 P1) | Layer 3: same Viewport, `TotalCount` = 10³, 10⁵, 10⁶ | element count identical across all three |
| **VZ-2** | MUST | The number of component instances likewise; the memoisation boundary is the row and only the row (ADR-0003 P2) | Layer 2: `FindComponents<ExGridRow<T>>().Count` | equals rows per Viewport, at every `TotalCount` |
| **VZ-3** | MUST | Rows per Viewport = what fits plus the row straddling the bottom edge, computed from the **visible** height (ADR-0013) | Layer 1 `ViewportGeometry` | exact, including fractional row heights |
| **VZ-4** | MUST | Horizontal virtualisation excludes columns wholly under the Pinned block, and Pinned Columns are painted through a fling (ADR-0004) | Layer 2 | slice matches `ColumnGeometry.ScrollableSliceAt`; pinned cells present in every frame |
| **VZ-5** | MUST | A fling — a move of more than one Viewport on either axis — paints Placeholders, and they are filled in after the settle delay (ADR-0004) | Layer 2 with `FakeTimeProvider` | Placeholders during; real cells exactly one settle period after stillness |
| **VZ-6** | MUST | `VirtualiseColumns=false` travels the same rendering path, and the horizontal fling does not engage (ADR-0004) | Layer 2 | same painted output as on, minus the slicing; no blanking on horizontal movement |
| **VZ-7** | MUST | The scrollbar spans the whole result: header height + total rows × row height (ADR-0013) | Layer 2 | `.ex-spacer` height exact |
| **VZ-8** | MUST | A result whose scrollable height would exceed 33,554,432 px is **refused by name**, header included, rather than silently clamped (ADR-0013) | Layer 1 + Layer 2 | throws, naming the row count and the ceiling; 1,000,000 rows at 28px passes, at 40px throws |
| **VZ-9** | MUST | The Scrollbar Gutter is subtracted before any geometry is computed, on both axes (ADR-0013/0021) | Layer 1 `ViewportBox`, Layer 2, Layer 3 `scrollbar.spec.mjs` | a gutter of 0 is bit-for-bit today's behaviour; the Focus never lands behind a bar |
| **VZ-10** | MUST | Scrolling to the far corner and back, at every zoom level, keeps the Focus inside the readable area (ADR-0012/0021) | Layer 3 `scrollbar.spec.mjs` at DPR 1 / 1.25 / 2 | passes on a platform **whose scrollbars occupy layout** — Windows or Linux — and not only where they are overlays. §22 states the reason: the test is a tautology against overlay scrollbars, so macOS alone does not discharge it |
| **VZ-11** | MUST | A geometry change re-anchors on the first visible row, not on the pixel offset (ADR-0028) | Layer 2: change density at scroll position P | the first visible row index is unchanged; the Focus's visibility is unchanged |
| **VZ-12** | MUST | Under `ViewportHeight=Fill`, a reported size of 0 paints nothing and throws nothing; a *declared* 12px still throws (ADR-0028) | Layer 2 | the refusal keys on declared-vs-reported, not on the number |
| **VZ-13** | MUST | Under `PageSize`, the scroll-ceiling refusal measures **one page**, and a paged result of any total binds; a `Density`/`RowHeight` change re-anchors on the first visible row **page-locally** (ADR-0013/0015/0028) | Layer 2 | a 2M-row paged result renders; the re-anchor writes local-row × height, never absolute-row × height |
| **VZ-14** | MUST | On a **real Windows desktop at fractional display scaling** (125%), where the native scrollbar is a non-integer number of CSS pixels, the Focus still lands inside the readable area (ADR-0012/0021) | Layer 3 `scrollbar.spec.mjs`, run on Windows itself | passes with the OS doing the scaling. CDP's `Emulation.setDeviceMetricsOverride` does **not** discharge this: it scales the page, and whether it reproduces what the OS does to the browser's own scrollbar is exactly the unknown |

---

## 6. Filter (FL)

The grid renders the filter UI and never evaluates a filter.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **FL-1** | MUST | `FilterPanelContext` carries everything a substitute needs; no substitute reaches into internals (ADR-0009) | build a stub Chrome that uses only the context | it compiles against the public surface alone |
| **FL-2** | MUST | The value list reflects all applied filters **except the column's own** (ADR-0009) | Layer 1 against `GridSource.From` | narrowing column A then opening A still offers A's full domain; opening B offers only what A left |
| **FL-3** | MUST | `DistinctValues` can answer **TooMany**, and the panel degrades to a search box without breaking (ADR-0009) | Layer 2 with a Source answering TooMany | condition mode with search; no exception, no empty list |
| **FL-4** | MUST | The filter applies on OK, not per checkbox; Cancel discards; the list is not re-fetched while open (ADR-0009) | Layer 2, counting `RequestDistinctValues` calls | exactly one fetch per opening; zero Consumer notifications until OK |
| **FL-5** | MUST | Columns combine with AND; OR exists only within a column (ADR-0009) | Layer 1 | `GridFilters` composition matches |
| **FL-6** | MUST | Operator semantics — case sensitivity, null ordering, culture — are pinned exhaustively against the reference implementation (ADR-0023) | Layer 1 | every operator × every column type has a test |
| **FL-7** | MUST | An Opaque Filter passes straight through and the grid renders no UI for it (ADR-0002) | Layer 1 + Layer 2 | present in the Query, absent from the panel |
| **FL-8** | MUST | Applying a filter clears the Selection (ADR-0009/0011) | Layer 2 | selection empty after the new Window with a moved Row Sequence Version |
| **FL-9** | MUST | OK applies what the panel shows: "everything checked" is set membership over the fetched domain, never a count, and the seed intersects the applied In-list with that domain (ADR-0009) | Layer 2 | a domain shifted by another column's filter narrows the In-list; it never silently removes the filter |

---

## 7. Sort (SR)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SR-1** | MUST | Clicking a column header sorts; it does not select the column (ADR-0012) | Layer 3 | one click raises `OnSortChanged`; the selection is untouched by the click itself |
| **SR-2** | MUST | Column selection remains reachable by Ctrl+Space, Ctrl+Shift+Down, and the corner (ADR-0012) | Layer 2 | all three produce a whole-column selection |
| **SR-3** | MUST | The grid never reorders rows itself; sort state travels out and a new Window travels in (ADR-0001) | inspect + Layer 2 | the painted order is exactly the pushed order, always |
| **SR-4** | MUST | A sort that changes the visible order drops the Selection; one that leaves the sequence identical keeps it (ADR-0011/0023) | Layer 2 | driven by the Row Sequence Version, both directions tested |
| **SR-5** | MUST | Sort semantics — null ordering, stability, culture, mixed types — match the reference implementation exactly (ADR-0023) | Layer 1 | every clause has a named test |
| **SR-6** | MUST | `aria-sort` reflects the applied sort on the header (ADR-0033) | Layer 2 | `ascending` / `descending` on the sorted header, `none` elsewhere |

---

## 8. Editing (ED)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **ED-1** | MUST | There is exactly one editor element, floating over the focused cell — never an input inside a row (ADR-0010) | Layer 2 | `document.querySelectorAll('input')` under the root ≤ 1 while editing; `ExGridRow` render counts unchanged by editing |
| **ED-2** | MUST | Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, **Caret** moves the caret (ADR-0010) | Layer 3, real keys | the two are distinguishable by observable behaviour, and F2 moves between them |
| **ED-3** | MUST | Esc / Enter / Tab are always the core's, in both states and while a descendant holds focus (ADR-0010) | Layer 3 | the editor never sees them |
| **ED-4** | MUST | The character that started Overwrite mode is not lost (ADR-0010) | Layer 3: type `5` onto a selected cell | the editor opens containing `5` |
| **ED-5** | MUST | A commit leaves the grid as an intent; the grid holds no committed value (ADR-0007) | Layer 2 | the painted value does not change until the Consumer returns a new row instance |
| **ED-6** | MUST | Uncommitted text is the grid's and survives a re-render of the cell underneath (ADR-0007/0027 P7) | Layer 2: push a new Window mid-edit | the editor's text is unchanged |
| **ED-7** | MUST | A density change mid-edit moves the editor with its cell and changes nothing else (ADR-0028) | Layer 2 | text preserved; box re-derived |
| **ED-8** | MUST | Editing does not start on a Placeholder — the Focus over unfetched data waits for the row (ADR-0012) | Layer 2 | no editor appears |
| **ED-9** | MUST | The editor's box is exactly `RowHeight` × the resolved column width (ADR-0028/0030) | Layer 3 | measured equal |
| **ED-10** | MUST | A bulk paste and a Ctrl+Enter fill are each **one** Edit Intent (ADR-0007/0011) | Layer 2 | one notification carrying all cells, not one per cell |
| **ED-11** | MUST | An IME composition is never taken by the core (ADR-0010) | Layer 3 with composition events | `isComposing` and keyCode 229 both pass through |
| **ED-12** | MUST | A press that is not on the editor commits first — Excel's click-away — and the press keeps its own meaning **unless the verdict Rejects** (ADR-0010/0034); a press inside the editor never reaches the delegated viewport | Layer 2 | another cell, and the header, both commit; the editor's input stops propagation |
| **ED-13** | MUST | An AltGr character (Control+Alt together) opens the editor; either modifier alone opens nothing (ADR-0010) | Layer 2 + inspect the JS gate | both-held admits a printable key in the C# mirror and in `ex-grid.js` |
| **ED-14** | MUST | A Column may carry a validate function returning an Edit Verdict; the grid runs it at commit and never judges a value itself (ADR-0034) | Layer 1/2 | no function means Accept; Accept raises the intent unchanged |
| **ED-15** | MUST | A Reject holds the editor open under **every** commit gesture — Enter, Tab, the Overwrite arrows, click-away — with focus in the editor; Escape remains the only exit without applying, and the rejected press keeps no meaning of its own (ADR-0034) | Layer 2 + Layer 3 | none of the four closes it; the click selects nothing |
| **ED-16** | MUST | A Flag raises the intent unchanged; the error paint and message come only from the display channels, never through the intent (ADR-0034) | Layer 2 | the intent is byte-identical to Accept's; `Error` appears only when the Consumer answers it |
| **ED-17** | MUST | The error message is asked for on demand — at popover open, never per render — and the popover opens after **300 ms of stillness**, never permanently (ADR-0034) | Layer 2, counting delegate calls | zero `CellMessageOf` calls while painting; one per open; a run of arrow keys opens nothing |
| **ED-18** | MUST | A **clipboard** paste is never rejected on value: shape refusals are unchanged, and no verdict function runs on that path (ADR-0014/0034) | Layer 2 | the validate delegate is not called during a clipboard paste, whatever the payload |
| **ED-17b** | MUST | The popover **also** opens on hover, after the same 300 ms, and the pointer leaving closes it (ADR-0021/0034) | Layer 2 for the seam, Layer 3 for a real pointer | resting on a flagged cell opens it and asks once; resting again on the same cell asks nothing more; an open editor's own message outranks it |
| **ED-17c** | MUST | The pointer is heard in JavaScript and only **its stillness, and its moving onto another row**, reach C# — never a call per move, because on a Server circuit that is a wire round trip per frame; and neither report is made while nothing consumes it (ADR-0021's fifth entry) | inspect `ex-grid.js`, and the pinned test that the grid does not listen for moves outside a drag | the module throttles locally and calls `OnPointerRestAsync` once per pause and `OnPointerRowAsync` once per row crossed, each only when switched on; no Blazor `onmousemove` outside `_dragHandler` |
| **ED-19** | MUST | A fill refused for covering a non-editable column holds the editor open with the typed text intact, and the gestures the refusal did not name keep their meaning — Enter still commits the one cell (ADR-0035) | Layer 2 + Layer 3 | the editor survives the refusal; Enter then raises one Edit Intent, and the fill raises none |
| **ED-20** | MUST | A Ctrl+Enter fill **is** judged on value: the verdict runs once, against the row the editor was opened on, and a Reject holds the editor and raises no paste intent — the Editable refusal is asked before it (ADR-0034/0035) | Layer 2 | exactly one validate call per fill; under a Reject the editor survives and `OnPaste` never fires; under a refusal validate is not called at all |
| **ED-21** | MUST | Text typed and not committed that the grid throws away is **announced with the reason that is true of it** — the order changed, the columns changed, the row left the Window, or the column stopped being Editable (ADR-0011/0035) | Layer 2, one per path | `OnEditDiscarded` carries `OrderChanged` / `ColumnsChanged` / `RowLeftTheWindow` / `ColumnNoLongerEditable`, never a neighbour's reason; no Edit Intent is raised in any of them |

---

## 9. Cell and range selection (SL)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SL-1** | MUST | Selection is a list of rectangles in position space; Ctrl+A over 10⁶ × 50 is **one** rectangle (ADR-0011) | Layer 1 | `Ranges.Count == 1`; no per-cell allocation |
| **SL-2** | MUST | There is no cap on selection (ADR-0011) | Layer 1 | selecting the whole result succeeds at any size |
| **SL-3** | MUST | Selection is dropped when the Row Sequence Version changes, and when the visible-column set changes (ADR-0011) | Layer 2 | both triggers, and the negative case (values changed, order did not) |
| **SL-4** | MUST | Columns are compared by name in order, never by array identity (ADR-0011) | Layer 2: hand a fresh but equal array every render | selection survives |
| **SL-5** | MUST | Disjoint ranges via Ctrl+click; Ctrl+click on a selected cell subtracts it, leaving Anchor and Focus **detached** and stored, not inferred (ADR-0012) | Layer 1 | a rectangle splits into at most four; the detached state is a field |
| **SL-6** | MUST | Selection is painted by **one overlay element per range**, never a per-cell class (ADR-0008) | Layer 2 | overlay element count equals visible range count; no cell class changes when the selection moves |
| **SL-7** | MUST | Selection, Focus and uncommitted editor text outlive the DOM elements they are painted over (ADR-0027 P7) | Layer 2: scroll the selection off screen and back | unchanged |
| **SL-8** | MUST | The selected-cell count is the sum of rectangle areas and needs no data (ADR-0014) | Layer 1 | exact for disjoint and overlapping cases |
| **SL-9** | MUST | Under a pager, Ctrl+A selects the page and "select all N rows" is offered explicitly (ADR-0015) | Layer 2 | two distinct results, and the offer is present |
| **SL-10** | MUST | Turning the page keeps the selection, and an off-screen selection is reported (ADR-0015) | Layer 2 | the indicator appears for both paging and scrolling |
| **SL-11** | MUST | A drag never turns the page (ADR-0015) | Layer 3 | the page does not turn under a drag, at either boundary |
| **SL-12** | MUST | A drag inside the edge band auto-scrolls, faster the deeper in, and the selection follows (ADR-0008) | Layer 2 with `FakeTimeProvider`, pointer parked at three depths | rows per tick rises monotonically with depth; the selection's far edge tracks it |
| **SL-13** | MUST | The auto-scroll rate never reaches ADR-0004's fling threshold — no Placeholder row appears **while selecting** | Layer 2 at maximum depth | rows scrolled per tick < one Viewport; no `ex-placeholder` in the painted slice |
| **SL-14** | MUST | The pointer leaving the element **stops** the auto-scroll (ADR-0008) | Layer 3: drag into the band, leave the grid, wait 2s | `scrollTop` is unchanged from the moment of leaving — this is the runaway the decision exists to prevent |
| **SL-15** | MUST | Returning with the button released ends the drag; returning with it held resumes (existing `e.Buttons` path) | Layer 3, both ways | the selection stops growing in the first case and resumes in the second |
| **SL-16** | MUST | Ctrl+A — paged or not — names a region and moves neither Anchor nor Focus (ADR-0012/0015) | Layer 1 + Layer 2 | under a pager the page rectangle is selected with the active cell unmoved |

---

## 10. Keyboard (KB)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **KB-1** | MUST | The listener is capture-phase, on the **instance root**, never on `document` (ADR-0010/0018) | inspect `ex-grid.js`; Layer 3 with two grids | only the focused grid reacts |
| **KB-2** | MUST | The core decides which keys it claims; JS is a Set lookup only, and a disagreement shows as a key that does nothing (ADR-0010) | inspect: the table is in `GridKeys`; JS holds no meaning | no key semantics in `ex-grid.js` |
| **KB-3** | MUST | Control is the Primary Modifier everywhere; Meta is primary **only** on an Apple platform, and a Meta held elsewhere keeps the key out of the table (ADR-0012) — on **every** key path, the editor's included | Layer 1 `GridKeys.Canonical`; Layer 2 through `OnKeyAsync` with the editor open; Layer 3 on macOS | `Win+ArrowDown` does not canonicalise to `ArrowDown`; Cmd+Enter fills where Meta is Command and Meta+Enter does nothing where it is not. *(Widened 2026-09-23: the function was right and the editing path never called it, so Cmd+Enter on a Mac committed one cell and moved on — found by the first macOS run of CP-16's layer-3 test)* |
| **KB-4** | MUST | The keyboard and the mouse read the same answer about which modifier adds a range (ADR-0012) | Layer 2 | Ctrl+click and Ctrl+A agree on every platform value |
| **KB-5** | MUST | Arrows, Shift+arrow, Ctrl+arrow, Ctrl+Shift+arrow, Home/End and their Ctrl and Shift forms behave as ADR-0012's table says | Layer 1 + Layer 2 | every row of the table has a named test |
| **KB-6** | MUST | Enter runs down columns, Tab runs across rows, both wrap, and **neither ever leaves the selection** (ADR-0012) | Layer 1 | the exact cycle in ADR-0012's diagram |
| **KB-7** | MUST | With a single cell, Enter and Tab clamp at the last row/column and invent no wrap target (ADR-0012) | Layer 1 | Focus stays |
| **KB-8** | MUST | Escape releases the grid's DOM focus, so a keyboard user can tab out — after first closing an open popover, which is the inner layer (ADR-0012) | Layer 3 | focus leaves the root; the next Tab reaches the next page element |
| **KB-9** | MUST | With focus but no selection, the first key only places the Focus on the first visible cell, without moving the Viewport (ADR-0012) | Layer 2 | one keystroke, no scroll |
| **KB-10** | MUST | A key that names the start of the row or the result returns the Viewport to the start, **with or without Pinned Columns** (ADR-0012) | Layer 2 `GoToStartTests` + Layer 3 | pinned and unpinned agree |
| **KB-11** | MUST | The grid is one tab stop; keys aimed at a focusable descendant are not taken (ADR-0010/0020) | Layer 3 with a Template Column input | typing in the input works; arrows move the caret, not the selection |
| **KB-12** | MUST | `:focus-visible` on the root makes the live grid tellable (ADR-0018/0029) | Layer 3 | the outline appears for keyboard focus and not for a click |
| **KB-13** | MUST | PageUp / PageDown move Focus **and** Viewport by the same N — the Focus keeps its position on screen (ADR-0012) | Layer 2 at a viewport of known height, pressed three times | N = fully visible rows; the Focus's offset within the Viewport is identical after presses 2 and 3 |
| **KB-14** | MUST | At the top and bottom the scroll clamps and the transition degrades to a reveal — the Focus is still fully visible (ADR-0012) | Layer 2, PageDown until the end | Focus inside the visible box at every step; never behind the header or a gutter |
| **KB-15** | MUST | `Ctrl`+PageUp / `Ctrl`+PageDown are neither handled nor `preventDefault`-ed (ADR-0012) | Layer 3, observe `defaultPrevented` | `false`; the selection does not move |
| **KB-16** | MUST | No identifier in the keyboard surface is named "page" — `CONTEXT.md` puts it on the `_Avoid_` list under **Window** | `grep -ri "page" src/ExGrid/Keys/` | matches only the browser's own `PageUp`/`PageDown` key strings |
| **KB-17** | MUST | An open popover is dismissable three ways: the ▾ that opened it, Escape from wherever focus sits, and a pointer-down anywhere else in the instance — which keeps its own meaning (ADR-0009/0010/0012) | Layer 2 `FilterChromeTests` + Layer 3 | closing without OK discards; the grid is not blurred by the close |
| **KB-18** | MUST | Escape from a focusable descendant — a Template Column control, an action button — returns the keyboard to the grid, never out of it (ADR-0020/0012) | Layer 2 + Layer 3 on `/cells` | the control's other keys are untouched; after Escape the root is focused and not blurred |
| **KB-19** | MUST | A grid with no editable column claims no printable key (ADR-0010/0020) | Layer 3 on `/cells` | the keydown reaches the page unprevented; no editor appears |
| **KB-20** | MUST | Space on a cell with several actions makes it Interactive with the **first** action chosen; DOM focus stays on the root (ADR-0020/0037) | Layer 2 + Layer 3 on `/cells` | the first button carries `ex-action-chosen`; the root still holds DOM focus; nothing fired |
| **KB-21** | MUST | Inside, ← / → choose the neighbouring action and Home / End the first and last, clamped at both ends; Space fires the chosen action **once** and leaves (ADR-0020/0037) | Layer 2 + Layer 3 on `/cells` | the chosen action follows the keys and never leaves the cell by overshoot; one `OnAction` naming the chosen action; Interactive ended |
| **KB-22** | MUST | Inside, Enter and Tab **never fire**: they leave and keep their root meaning, as does every other key the grid claims; Escape leaves without releasing the grid; a pointer press leaves (ADR-0020/0012/0037) | Layer 2 + Layer 3 on `/cells` | zero `OnAction`; the Focus moves exactly as the same key moves it outside; after Escape the root still holds DOM focus |
| **KB-23** | MUST | Space on a Template cell hands **one** focus request to that cell's content — non-zero on one render, zero on every other and in every other cell; it waits for the cell to be painted, is dropped if the Focus moves first, and a row re-created afterwards never sees it (ADR-0037) | Layer 2 with a counting template | exactly one non-zero `FocusRequest` observed; none after scrolling the row out and back |
| **KB-24** | MUST | A Template control that takes the request holds the keyboard: typing lands in it, and Escape brings the keyboard back with the Focus unmoved (ADR-0020/0037) | Layer 3 on `/cells` | the field has the typed text; after Escape the root is focused and `aria-activedescendant` names the same cell |
| **KB-25** | MUST | Space on an editable cell opens Overwrite containing a space; with no Focus, Space only places it and neither fires nor enters (ADR-0020/0010/0012) | Layer 2 | the editor's text is `" "`; from an empty selection one Space selects the first visible cell and raises nothing |
| **KB-26** | MUST | A held Space engages once: a repeated plain Space is taken and dropped by the gate (ADR-0037) | inspect `ex-grid.js`; Layer 3 holding Space on a one-action cell | one `OnAction` for the whole hold |
| **KB-27** | MUST | A grid action button never holds the keyboard: a press dragged off it — the platform's cancel — leaves it unfocused, so Enter afterwards fires nothing (ADR-0020/0037) | Layer 3 on `/cells` with a real mouse | no `.ex-action` is `document.activeElement`; zero `OnAction` after the drag and the Enter |
| **KB-28** | MUST | `Alt+↓` opens the column menu of the Focus's column, and does nothing on a column with no menu (ADR-0039) | Layer 1 for the table, Layer 2 for the open, Layer 3 for the browser taking no action of its own | the menu is open for that column |
| **KB-29** | MUST | Opening a column menu, a filter panel or the Context Menu — by key or by pointer — puts DOM focus on its first enabled item or control, asked through the context's `FocusRequest`, once per opening, never by the core reaching into content it did not render (ADR-0039/0037) | Layer 2: the request counts up once per opening; Layer 3: `document.activeElement` is inside the popover, under both Chromes | as stated |
| **KB-30** | MUST | In a menu, ↑ / ↓ move among the **enabled** items and wrap, Home / End go to the first and last, Enter / Space run the item and close the menu, Tab / Shift+Tab close it as a Cancel (ADR-0039) | Layer 3, the same test against the built-in Chrome and against `ExGrid.MudBlazor`'s (FN-17) | identical outcomes under both |
| **KB-31** | MUST | In the filter panel, Tab / Shift+Tab move among its controls and wrap inside it while it stands; Enter in a value field applies (ADR-0039) | Layer 3 under both Chromes | focus never leaves the panel by Tab; the filter applied equals the one OK applies |
| **KB-32** | MUST | However a popover closes — Escape, its ▾, a pointer-down elsewhere in the instance, a command run, Apply, Cancel, Clear — DOM focus returns to the root, **including from inside an Inner Popup**; a dismissing pointer-down keeps its own meaning (ADR-0039/0010) | Layer 3 under both Chromes | `document.activeElement` is the root and the next arrow moves the Focus |

---

## 11. Copy and paste (CP)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **CP-1** | MUST | Copy past the cap **does not copy at all** — it never copies the first N (ADR-0005) | Layer 1 + Layer 3 | the clipboard is untouched; a refusal reason is raised |
| **CP-2** | MUST | The default cap is 1,000,000 cells, refusal is strictly **past** it, and the Consumer can raise it (ADR-0005) | Layer 1 | exactly 1,000,000 copies; 1,000,001 refuses |
| **CP-3** | MUST | The three refusal grounds are distinguishable, and misalignment outranks the cap (ADR-0005/0011/0014) | Layer 1 | distinct reason values; a selection both misaligned and oversized reports misalignment |
| **CP-4** | MUST | Two formats go on the clipboard in one operation: displayed format in `text/plain`, raw value in `text/html` (ADR-0005) | Layer 3, reading the real clipboard | both present; the raw value is unformatted and locale-free |
| **CP-5** | MUST | `####` never reaches the clipboard — the raw value does (ADR-0016) | Layer 3 with a narrowed numeric column | clipboard holds the number |
| **CP-6** | MUST | A selection inside the Window uses the `copy` event route and raises **no permission prompt** (ADR-0005) | Layer 3 | no prompt; write succeeds |
| **CP-7** | MUST | A selection beyond the Window uses the async route, asking the Consumer for the rows (ADR-0005) | Layer 2 + Layer 3 | the Consumer is asked; the rows are stored nowhere afterwards |
| **CP-8** | MUST | Copy follows the current column order and excludes hidden columns (ADR-0005) | Layer 1 | order matches View State |
| **CP-9** | MUST | Aligned disjoint ranges are emitted in **position order**, never creation order; overlapping or duplicate spans are refused as misaligned (ADR-0011) | Layer 1 | exact ordering; overlap refused |
| **CP-10** | MUST | The browser's own text selection is not what gets copied (ADR-0005) | Layer 3: select text with the mouse, then Ctrl+C | the rectangular selection is what lands |
| **CP-11** | MUST | Paste shape rules: 1×1 fills; m×n into M×N accepts iff m divides M and n divides N; range→1 cell **refused**; ragged refused (ADR-0014) | Layer 1 `PasteRuleTests` | the full table, each case a named test |
| **CP-12** | MUST | A source larger than 1×1 into a disjoint target is refused; 1×1 is the only multi-range paste (ADR-0014) | Layer 1 | refused with its own reason |
| **CP-13** | MUST | Paste never writes outside the selection (ADR-0014) | Layer 1 + Layer 2 | the intent's cell set ⊆ the selection, always |
| **CP-14** | MUST | Paste reads `text/html` as well as `text/plain`, preserving type and precision from Excel (ADR-0005) | Layer 3 with an Excel-shaped clipboard payload | values arrive typed, not as display strings |
| **CP-15** | MUST | An empty selection refuses, with its own reason, for both copy and paste (ADR-0005/0014) | Layer 1 | nothing happens, and the reason says why |
| **CP-16** | MUST | A paste or Ctrl+Enter fill whose target covers a column that is not `Editable` is refused **whole** — with its own reason, and no intent is raised (ADR-0035) | Layer 1 `PasteRuleTests` + Layer 2 | refused with `TargetNotEditable`; `OnPaste` never fires; the refusal outranks the shape rules |
| **CP-17** | MUST | Copy with headers emits the column's **declared `Header`** as one extra row — the first TSV line and a `<th>` row in `text/html` — for a disjoint selection in either orientation, and never the truncated paint (ADR-0005) | Layer 1 for the assembly, Layer 3 for the real clipboard | one header row, in both formats, naming the covered columns in emission order |
| **CP-18** | MUST | The header row **counts against the copy cap**: a selection exactly on the cap copies plainly and refuses with headers (ADR-0005) | Layer 1 at the boundary | `Copy` approves, `Copy with headers` refuses with the cap's own reason |
| **CP-19** | MUST | A clipboard command invoked from a menu writes **without a permission prompt** on Chrome and on Edge, taking the asynchronous route whatever the selection's size (ADR-0005/0017/0036) | Layer 3, both browser projects | the clipboard holds the payload and no prompt was shown; a prompt is a recorded finding, not a pass |
| **CP-20** | MUST | **No delimiter is ever guessed**: a tab and an HTML table cell are the only cell boundaries, a line break the only row boundary — `1,234` is one cell however many lines share its shape (ADR-0005) | Layer 1 `ClipboardParseTests` | comma-grouped numbers parse as one column; a tab in the same text still splits |
| **CTX-1** | MUST | A secondary click **outside** the selection collapses it onto the cell it lands on before the menu opens; **inside**, the selection stands (ADR-0036) | Layer 2 + Layer 3 | the commands act on what the user can see |
| **CTX-2** | MUST | The menu's items are the core's — the clipboard's — with the Consumer's appended, and Chrome only lays them out (ADR-0010/0036) | Layer 2 | `Copy`, `Copy with headers`, then whatever `ContextCommands` returned |
| **CTX-3** | MUST | A command is handed the clicked **row instance**, the column, and the selection as **rectangles plus the Row Sequence Version** — never rows (ADR-0011/0036) | Layer 2 | the context's `Selection` is ranges; no row beyond the Window is resolved |
| **CTX-4** | MUST | The menu is reachable from the keyboard — `ContextMenu` and `Shift+F10`, on the Focus cell — and Escape closes it before it leaves the grid (ADR-0012/0036) | Layer 1 for the table, Layer 2 for the open, Layer 3 for the browser's own menu staying away | both keys open it; the browser menu never appears |
| **CTX-5** | MUST | **The context menu adds no JavaScript use**: the browser's menu is suppressed by `@oncontextmenu:preventDefault` and by the key gate, not by a listener the grid installs (ADR-0021) | inspect `ex-grid.js` | nothing in the module is about the menu |

---

## 12. Large data (BIG)

The scenario for every row here: `samples/ExGrid.DemoHost` `/wide`, **1,000,000 rows × 100
columns**, 28px rows, a 900×600 Viewport, two Pinned Columns.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **BIG-1** | MUST | The grid mounts, paints and scrolls end to end at 10⁶ rows | Layer 3 | Ctrl+End reaches the last row and paints it |
| **BIG-2** | MUST | DOM element count at 10⁶ rows equals the count at 10³ rows | Layer 3 | equal (VZ-1) |
| **BIG-3** | MUST | Ctrl+A at 10⁶ × 100 completes, and the selection is one rectangle | Layer 3 | no freeze; count displayed is 10⁸ |
| **BIG-4** | MUST | A row count whose scroll height would exceed the browser's 2^25 px is refused, not clamped | Layer 2 | VZ-8 |
| **BIG-5** | MUST | Scrolling from the first row to the last and back leaves the painted rows correct at both ends | Layer 3 | first and last row content match the data |
| **BIG-6** | OBSERVATIONAL | Settle repaint after a fling; frame interval during a sustained scroll | Layer 3 with `Performance.getMetrics` | recorded in `metrics.json`; compared with ADR-0004's table (16.3 ms / 8.3 ms) |
| **BIG-7** | OBSERVATIONAL | Time from mount to first painted row at 10⁶ | Layer 3 | recorded |

---

## 13. Large paste (PST)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PST-1** | MUST | Pasting one value into a whole-column selection of 10⁶ rows produces **one** Edit Intent naming the range, not 10⁶ notifications (ADR-0014/0007) | Layer 2 | one invocation |
| **PST-2** | MUST | Rows that are invisible or not yet fetched are included in the paste target — this is intended (ADR-0014) | Layer 2 | the intent covers rows outside the Window |
| **PST-3** | MUST | Pasting into a selection that is entirely off screen still works, and the off-screen indicator was showing beforehand (ADR-0015) | Layer 3 | indicator present; paste applies to the selected rows |
| **PST-4** | MUST | A paste of a clipboard payload far larger than the target is refused by shape before any intent is raised (ADR-0014) | Layer 1 | refusal, zero notifications |
| **PST-5** | MUST | Parsing a large clipboard payload does not block the UI thread past one settle period | Layer 3: paste ~10 MB of TSV | the grid responds to a key within 500 ms of the paste completing |
| **PST-6** | OBSERVATIONAL | Time to parse and raise the intent for 10⁵ and 10⁶ source cells | Layer 3 | recorded |
| **PST-7** | MUST | The spaces Excel preserves as `&nbsp;` in its HTML flavour survive the parse; only the markup's own pretty-printing is trimmed (ADR-0005) | Layer 1 | `a&nbsp;&nbsp;` reads back as `"a  "`-shaped value, never `"a"` |

---

## 14. Error handling (ERR)

The spine's first principle: **rather than be quietly wrong, say it cannot be done.** Every refusal
below must be *observable* — an exception, a refusal reason, or a visible state — never a no-op
that looks like success.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **ERR-1** | MUST | Every refusal names which rule it is: copy cap, misalignment, paste shape, target editability, empty selection, oversized result, contradictory width, both-or-neither Window/Source, gutter larger than the Viewport | Layer 1 for each | a distinct, inspectable reason per rule |
| **ERR-2** | MUST | A refusal message says what to do next where one exists (ADR-0014: "reselect a target of the same shape") | Layer 1 | message asserted |
| **ERR-3** | MUST | A Window contradicting its own `TotalCount` or `WindowStart` is refused rather than painted at the wrong place (ADR-0001) | Layer 2 | throws |
| **ERR-4** | MUST | A null row, or the same row instance twice in one Window, is refused — Row Identity cannot tell them apart (ADR-0003) | Layer 2 | throws, naming the position |
| **ERR-5** | MUST | A Source that throws does not take the grid down: the failure surfaces and the grid keeps painting what it has (ADR-0025) | Layer 2 | the exception reaches the Consumer; the last good Window stays on screen |
| **ERR-6** | MUST | A missing or broken JS module does not take the Range Request down with it | Layer 2 | rows still requested; the grid does not sit on Placeholders forever |
| **ERR-7** | MUST | Disposal racing an in-flight interop call is not a fault and is not logged as one | Layer 2 + Layer 3 | no error; no exception |
| **ERR-8** | MUST | A configuration that cannot work is refused at the earliest point it is knowable, naming the parameter the user actually wrote | Layer 1/2 | e.g. the gutter refusal names the gutter, not `ViewportWidth` |

---

## 15. Console and runtime exceptions (CON)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **CON-1** | MUST | Across the whole layer-3 run, the browser console emits **zero** `error` messages | Playwright `page.on('console')` filtered to `error`, written to `console.json` | empty |
| **CON-2** | MUST | Zero uncaught page errors | `page.on('pageerror')` | empty |
| **CON-3** | MUST | Zero `warning` messages originating from ExGrid's own code | same capture, filtered by source file | empty; third-party warnings listed in `results.md` |
| **CON-4** | MUST | No `ResizeObserver loop completed with undelivered notifications` at any point, including during a window resize and a density change (ADR-0013) | same capture | absent |
| **CON-5** | MUST | The Blazor error UI never appears | Layer 3: `#blazor-error-ui` computed `display` | `none` throughout |
| **CON-6** | MUST | No unhandled exception reaches the host log during any scenario | inspect the DemoHost output captured during the run | no `Unhandled exception` line |
| **CON-7** | MUST | No unobserved `Task` exception | a `TaskScheduler.UnobservedTaskException` handler installed in the test host, plus a forced `GC.Collect(); WaitForPendingFinalizers()` at the end of Layer 2 | zero events |
| **CON-8** | MUST | Nothing is logged to the console by ExGrid on a healthy path — and a **failed copy is never silent**: a genuine .NET failure on either copy route is reported, distinguished from "no sync channel" by the attach-time probe, never by catching (ADR-0005) | Layer 3 | the only permitted console writes are the `console.error` failure paths in `ex-grid.js` (a key, a viewport report, a paste, a copy that the core failed to build or write), and none fires on a healthy path |

---

## 16. Performance (PF)

Structural invariants gate; milliseconds do not (§1).

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PF-1** | MUST | No per-cell JS interop, and no layout read on the path to a paint (ADR-0021 P4) | grep `src/` for `getBoundingClientRect`, `clientWidth`, `offsetWidth`, `scrollIntoView`; count interop calls per frame in Layer 3 via CDP | zero matches in the component; interop calls per scroll frame ≤ 1 |
| **PF-2** | MUST | The JS allowlist has exactly the five entries ADR-0021 names, and `ex-grid.js` uses no sixth | read `ex-grid.js` against the ADR table | exact match |
| **PF-3** | MUST | Per-cell strings are interned or cached alongside the geometry that produced them, never composed in the render loop (ADR-0027 P5) | inspect `CellClasses` / `RowClasses` / `ColumnStyles`; Layer 2 allocation test (`RenderAllocationTests`) | zero string allocation per painted cell per render by the grid's own code. Measured as the slope of a re-render's bytes between 4 and 16 columns — for text, `####`, a sorted grid and header groups, where it is zero bytes of any kind. The attribute names Blazor composes for an event directive (`@on…:stopPropagation`, `:preventDefault`) are the framework's and outside P5 (ADR-0027 says why), so where a cell or header cell carries one — an action, a menu button — its class is pinned as a difference between two grids alike in every directive, and its handlers as ids that survive a render. *(Scoped 2026-09-24)* |
| **PF-4** | MUST | Scroll offsets are read once per frame, both axes in one call (ADR-0021) | Layer 2 `OffsetReads` | one read per scroll event, never two |
| **PF-5** | MUST | Selection painting cost is a function of the number of rectangles, not of the number of cells or the size of the selection (ADR-0008) | Layer 2 | element count and render count independent of selection area |
| **PF-6** | OBSERVATIONAL | Settle repaint, fling frame interval, ordinary scroll frame interval, at 220 and 2,200 cells | Layer 3, median of ≥ 8 | recorded; compared with ADR-0004's table |
| **PF-7** | OBSERVATIONAL | Selection drag frame cost | Layer 3 | recorded; compared with ADR-0008's 2.10 ms / 19.70 ms pair |
| **PF-8** | OBSERVATIONAL | `spikes/render-bench` result JSON accumulated for the trend | run the harness | a new entry committed under the spike's results |

---

## 17. Memory and allocation (MEM)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **MEM-1** | MUST | Steady-state scrolling allocates nothing that scales with `TotalCount` | Layer 2: `GC.GetAllocatedBytesForCurrentThread()` around 100 simulated scroll steps at 10³ and 10⁶ rows | the two totals differ by < 5% |
| **MEM-2** | MUST | Mounting and disposing the grid 50 times returns the CDP `Nodes` and `JSEventListeners` metrics to their baseline | Layer 3: `Performance.getMetrics` before and after, with `HeapProfiler.collectGarbage` between | both within ±2 of baseline |
| **MEM-3** | MUST | Every `DotNetObjectReference`, `IJSObjectReference`, `ITimer` and `CancellationTokenSource` the component creates is disposed exactly once | Layer 2 with counting stubs | create count equals dispose count; no double dispose |
| **MEM-4** | MUST | Disposal removes the key listener and disconnects the ResizeObserver (ADR-0018/0021) | Layer 2 asserting the handle's `dispose`; Layer 3 checking listener count | listener count returns to baseline |
| **MEM-5** | MUST | A 10-minute scripted scroll does not grow the JS heap monotonically | Layer 3: sample `JSHeapUsedSize` every 30 s with a forced GC | the last sample is within 20% of the median of the run |
| **MEM-6** | OBSERVATIONAL | Managed heap after the same scripted run | Layer 3 / host counters | recorded |
| **MEM-7** | OBSERVATIONAL | Bytes allocated per scroll frame | Layer 2 | recorded |

---

## 18. DOM size (DOM)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **DOM-1** | MUST | Element count under the instance root is independent of `TotalCount` | Layer 3 | VZ-1 |
| **DOM-2** | MUST | Element count under the root is independent of the size of the selection | Layer 3: 1 cell vs the whole result selected | equal but for one overlay element per range |
| **DOM-3** | MUST | With horizontal virtualisation on, cells in the DOM ≈ painted rows × (pinned + painted scrollable columns) | Layer 3 at the ADR-0004 sample | matches the arithmetic exactly |
| **DOM-4** | MUST | Two grids on one page hold independent DOM, tokens and handles; neither `:root` nor a global is written (ADR-0018) | Layer 3 | no `--ex-*` on `:root`; no `window.*` set by the module |
| **DOM-5** | OBSERVATIONAL | Element count and cell count at the ADR-0004 sample, both settings | Layer 3 | recorded; compared with 279 / 2,326 |

---

## 19. Unnecessary re-rendering (RR)

ADR-0027 P3 states the expectation precisely, which makes this the most mechanical section here.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **RR-1** | MUST | An **appearance** change re-renders nothing in .NET | Layer 2: change a Visual Token on the wrapper | `RenderCount` unchanged, root and rows |
| **RR-2** | MUST | A **row-height-only** geometry change re-renders the root, mounts/unmounts only the rows that entered or left, and re-renders no surviving row (ADR-0028) | Layer 2 counting per-row renders | surviving rows' render counts unchanged |
| **RR-3** | MUST | A change that moves digit width or padding **does** re-render every visible row, because their overflow decisions are stale (ADR-0028) | Layer 2 | every visible row re-renders exactly once |
| **RR-4** | MUST | Scrolling by one row re-renders only the rows that entered or left | Layer 2 | surviving rows skip |
| **RR-5** | MUST | Handing over a fresh-but-equal `Columns` array, or a fresh-but-equal Window, does not re-render surviving rows (ADR-0003) | Layer 2 | render counts unchanged |
| **RR-6** | MUST | A mouse move inside the cell it started in renders nothing | Layer 2 | `RenderCount` unchanged |
| **RR-7** | MUST | A keystroke that moves nothing renders nothing, and does not swallow the next render | Layer 2 | the following state change still renders |
| **RR-8** | MUST | A Scrollbar Gutter report carrying no change renders nothing | Layer 2 | `RenderCount` unchanged |
| **RR-9** | MUST | Delegates passed as parameters are cached in fields, never method groups | inspect `ExGrid.razor` | no method group in a parameter position |
| **RR-10** | MUST | `ShouldRender` is hand-written wherever memoisation is claimed (ADR-0003) | inspect | present on `ExGridRow` and on the root |
| **RR-11** | MUST | The pointer-row report (ADR-0021, fifth entry) reaches .NET only when the pointer crosses a row, and a hover-row change re-renders no row — the band is the overlay's (ADR-0029) | Layer 2 counting `RenderCount`; inspect `ex-grid.js` for the row filter | a report onto the row already held renders nothing; row counts unchanged across a hover change |
| **RR-12** | MUST | Interactive re-renders only the engaged row: entering, each choice and leaving render that row and no other, and a template's focus request renders the Focus row at most twice — the request and its clearing (ADR-0037, ADR-0027 P3) | Layer 2 counting per-row renders | every other row's count unchanged |
| **RR-13** | MUST | Row Stripes cost no render: with `StripeRows` on, scrolling by one row re-renders only the rows that entered or left, and PF-3's per-cell slope stays zero (ADR-0038, ADR-0027 P3/P5) | Layer 2 counting per-row renders; `RenderAllocationTests` with stripes on | surviving rows skip; zero bytes per painted cell |

---

## 20. Async, cancellation and state consistency (ASY / ST)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **ASY-1** | MUST | A superseded fetch is cancelled, and its late answer is discarded rather than painted (ADR-0025) | Layer 1 | the stale Window never reaches the grid |
| **ASY-2** | MUST | Disposal cancels every in-flight operation, and the CTS is disposed exactly once | Layer 1 + Layer 2 | no `ObjectDisposedException`; no leak |
| **ASY-3** | MUST | A burst of scroll events applies only the last position, in order | Layer 2 | intermediate offsets never painted out of order |
| **ASY-4** | MUST | A Range Request is raised after a render, never during one; a synchronous Consumer cannot re-enter (ADR-0001) | Layer 2 with a synchronous handler | no re-entrancy, no spin |
| **ASY-5** | MUST | The settle timer fires on the renderer's synchronisation context, and a fire after disposal is harmless | Layer 2 with `FakeTimeProvider` | no exception |
| **ASY-6** | MUST | No test in any layer hangs: every awaited task in the suite completes or is cancelled | run each layer with a 5-minute wall clock | all complete |
| **ST-1** | MUST | After **any** sequence of operations, these hold: every selection rectangle lies inside the extent; the Focus is inside a range or explicitly detached; the painted rows equal `ViewportGeometry.SliceAt`; both scroll offsets are within `[0, max]`; the displayed cell count equals the sum of rectangle areas | a scripted randomised sequence of ≥ 500 operations (click, drag, every key, scroll, sort, filter, page, resize, density change), asserting all five invariants after each step | zero violations; the seed is recorded in `results.md` |
| **ST-2** | MUST | The grid holds no committed data: after any sequence, removing the Consumer's state leaves the grid with nothing to paint (ADR-0001/0007) | Layer 2: push an empty Window at the end | the grid paints nothing and throws nothing |
| **ST-3** | MUST | The arithmetic's Row Height and the painted Row Height are the same number after every geometry change (ADR-0027 P6) | Layer 3 after each density change | equal |
| **ST-4** | MUST | View State is serialisable and round-trips: widths, order, pinned count, sort, filter (`CONTEXT.md`, ADR-0002) | Layer 1 through `System.Text.Json` | round-trips equal; Auto widths are **not** present, only the Auto intent (ADR-0016) |

---

## 21. What was undecided, and where each answer now lives

This section began as a list of questions the ADRs left open, each blocking at least one criterion
above. **The eight it started with are answered, and so is the ninth that a later sweep of all 33
ADRs turned up** (§21.10, the drag at the edge). **No open question remains.**

What that sweep also found is that "open" was being used for two different things. §21.11 states
the rule that separates them and lists every reservation in the repository, because an earlier
draft of this section applied that rule to three items and omitted five — which is how a list of
open questions stops being trustworthy.

The table is kept rather than deleted so that a reader sent here by a "blocked by §21.x" note finds
the decision instead of a gap, and so that the two answers that came back differently from how they
were predicted (§21.7a, §21.8) stay visible.

An implementer who finds a further gap **adds it here rather than deciding it alone** — that is the
rule this whole document exists to enforce, and §21.10–§21.12 are what it looks like when the rule
is followed.

| | Question | Settled as | Recorded in |
|---|---|---|---|
| **21.1** | What ships in the release | every ADR, 0001 onward | §2 of this document |
| **21.2** | The ARIA surface | the root owns it; no element carries `aria-selected` | **ADR-0033**, and §4.1 here |
| **21.3** | PageUp / PageDown | Focus and Viewport move by the same N; named `MoveByViewport` | **ADR-0012** |
| **21.4** | Column resize by dragging | guide line, applied on release; bounded below only | **ADR-0016** |
| **21.5** | Column reordering | drag within a block, never across the pinned boundary; the grid notifies | **ADR-0011** |
| **21.6** | The browser matrix contradiction | ADR-0026 moves: `chrome` and `msedge` projects | **ADR-0026** |
| **21.7** | The unmeasured digit width | measured; the estimate now charges per character class | **ADR-0016** |

### 21.7a What the digit-width measurement actually found

Kept here rather than folded away, because this document asserted the risk and the measurement
came back **worse** than the assertion.

The predicted failure was real but small: at weight 600 a tabular digit is **9.058px** against a
declared 9, so twelve digits fall 0.69px short — on `.ex-row-group` and `.ex-row-total`, the rows
most likely to be read. What had not been predicted at all is that **`%` is 13.836px**, so the
`CellTextMetrics` contract — *"at least as wide as any glyph the column's formats emit"* — breaks
by 54% as soon as a percent column exists. A single grid-wide number cannot serve a twelve-digit
amount column and a percent column at once, which is why the estimate now charges per character
class rather than being retuned.

### 21.8 Tiered headers — settled while this section was being written

`ADR-0013` had named this as the one decision that could overturn it: *"This is the decision that
overturns it — nothing else is expected to."* It was on the point of being recorded here as
knowingly left open, on the reasoning that no Consumer requirement had surfaced it and that
answering it would mean rewriting ADR-0013's cancellation arithmetic whole.

**Both halves of that reasoning were wrong, and
[ADR-0032](adr/0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md) is why.** The
requirement is ordinary in the first Consumer's domain — CVA / FVA / MVA each showing
Before / After / Diff — and the model chosen (declared rectangles over the leaf columns, rather
than a column tree) does not overturn ADR-0013: the band is `(1 + tierCount) × HeaderHeight`, the
cancellation survives as a multiple, the scroll-budget ceiling is checked as before, and a
rectangle straddling the pinned boundary is refused outright rather than drawn wrong. `HeaderHeight`
had already been separated from `RowHeight` by ADR-0028, which is what left the door open.

**So §21 is empty, and "every question in §21 is answered" is available as a release condition.**
That is the stronger place to be, and it is worth recording how nearly it was not: this document
argued for leaving the question open, and the argument rested on a claim about the first Consumer
that the person writing it had not checked.

### 21.9 The numbers that remain OBSERVATIONAL

Declared but unmeasured in their own ADRs. None may become a MUST until measured; none blocks the
release.

- **The fling threshold (one Viewport) and the 150 ms settle delay** — ADR-0004, "both numbers are
  provisional and neither has been measured".
- **The Density preset numbers** — ADR-0028, "declared, not measured", against Excel at several
  zoom levels.
- **Whether `Fill` needs debouncing** — ADR-0028.
- **The auto-scroll band (20px) and its rate range (1 to 8 rows per tick)** — ADR-0008, provisional
  in exactly the way the fling threshold is. The *shape* is decided and gated by SL-12..SL-15; the
  three numbers are recorded and compared, not gated.

### 21.10 What a drag does when it reaches the edge — SETTLED

An edge band with a depth-proportional rate, capped below ADR-0004's fling threshold, and
`@onmouseleave` stopping the timer. Recorded in
[ADR-0008](adr/0008-selection-is-painted-by-an-overlay.md); the criteria are SL-12..SL-15.

Two things are worth carrying forward from how it was answered. **The hard half was not the rate**
— it was that auto-scroll must be timer-driven (a pointer held still in the band still has to
scroll), and outside the element no events arrive at all, so a running timer would scroll to the
end of a million rows with nothing to contradict it. And **the ceiling came from an existing
decision rather than a guess**: one row short of the fling threshold, because scrolling faster
turns the rows the user is selecting across into Placeholders.

`setPointerCapture` — what the gesture really wants — was refused because Blazor has no API for it
and it would be a fifth entry on ADR-0021's allowlist.

### 21.11 Reserved with a named trigger — not open, and not to be mistaken for oversights

**The rule this section runs on**, stated because an earlier draft applied it to three items and
silently omitted five others:

> A question is **open** when nobody has decided it *and* no ADR says when it will be decided.
> It is **reserved** when an ADR names the trigger — the feature whose construction settles it.
> Reserved is a decision about *when*, and it is not a gap.

Everything below is reserved. It is listed in full so that a reader can see there is nothing else,
and so that the release gate can be stated as "**§21 lists no open question**" and mean it.

| Reserved | Trigger named by |
|---|---|
| **The fill handle** — neither the gesture nor the fill semantics | the editing work ([ADR-0008](adr/0008-selection-is-painted-by-an-overlay.md) → [ADR-0007](adr/0007-edits-are-an-overlay-owned-by-the-consumer.md)). ADR-0008 has already reserved the *painting* for it, which is the half-commitment worth keeping visible |
| **The Overlay application and the bundled undo stack** — ADR-0007 promises both as single, library-provided implementations; `InMemoryGridSource.ReplaceRow` covers only the Consumer that owns its rows in memory, and is recorded there as *not* being either | the first scenario-backed Consumer ([ADR-0007](adr/0007-edits-are-an-overlay-owned-by-the-consumer.md)'s "what wiring the editor settled") |
| **Right-click** — a secondary click leaves the selection alone today | the context menu, a Chrome seam ADR-0010 has not specified (ADR-0008). Double-click likewise "arrives with the editor" |
| **A copy over an Action Column** — **settled with the wiring that was its trigger**: an empty cell, because the column has no value by declaration and a refusal would fail an ordinary row copy for a column the user cannot sensibly unselect | recorded in [ADR-0005](adr/0005-copy-refuses-rather-than-truncates.md)'s "what wiring the routes settled" ([ADR-0020](adr/0020-action-and-template-columns.md) → ADR-0005) |
| **`ex-editing` on the root, `ex-editor`, the `--ex-editor-*` tokens** | **settled with the Cell Editor, its trigger**: all three exist as ADR-0029 named them ([ADR-0029](adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md), ADR-0010) |
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)) |
| **`--ex-selection-outline`** — the border Excel draws round a range's perimeter | the selection paint polish (ADR-0029) |
| **Which Chrome seams `ExGrid.MudBlazor` implements first** | **settled with the package's start, its trigger** — a verification order: no seam, the Cell Editor, the loading bar, then the filter panel, column menu and Context Menu with Inner Popups allowed ([ADR-0030](adr/0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md), [ADR-0039](adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)); criteria in §23 |
| **Whether ExSheet is a sibling of ExGrid or a Consumer of it** | building ExGrid ([ADR-0019](adr/0019-one-repository-many-packages.md): *"Do not decide now"*) |
| **A column band** — the Focus band's mechanism turned sideways | **no trigger is named.** ADR-0008 says only "reserved, not specified". It is the one entry here whose *when* nobody has written down; nothing depends on it, and it is recorded rather than quietly promoted to open |

**None of these blocks a criterion above.** That is checked, not assumed: no row in §3–§20 asserts
a fill handle, a context menu, an editor class, a selection outline, a Wrapper's seam order, or a
column band, and the one clipboard case (an Action Column inside a copied range) is deliberately
absent from §11 rather than written as a guess.

---

## 22. The final verification checklist

Run in this order. Each step names the criteria it discharges. Stop and record — do not skip —
when a step cannot be run.

### Step 0 — Preconditions

```sh
nix develop -c dotnet build ExGrid.slnx                 # PRE-1
dotnet list src/ExGrid/ExGrid.csproj package            # PRE-3
dotnet list src/ExGrid/ExGrid.csproj reference          # PRE-4
grep -n "projects" tests/ExGrid.Browser/playwright.config.mjs   # PRE-5
git status --porcelain                                  # PRE-6
```

Read `src/ExGrid/ExGrid.csproj` for PRE-2.

### Step 1 — Layer 1, pure logic

```sh
nix develop -c dotnet test tests/ExGrid.Tests 2>&1 | tee verification/<date>/layer1.log
```

Discharges the Layer 1 rows of FN, FL, SR, SL, KB, CP, ERR, ASY, ST-4.
**Pass:** zero failures, zero skips. A skipped test is a failure unless `results.md` records why.

### Step 2 — Layer 2, component

```sh
nix develop -c dotnet test tests/ExGrid.Components 2>&1 | tee verification/<date>/layer2.log
```

Discharges VZ, RR, MEM-1/3/4, ASY, ST-1/2, and the Layer 2 rows above.
**Pass:** zero failures, zero skips but one — ST-1's 10⁶-row case, skipped by name and run in
Step 5 — and `CON-7`'s unobserved-exception handler reports zero.

### Step 3 — Static inspection

Grep and read, recording each result:

```sh
grep -rn "getBoundingClientRect\|clientWidth\|offsetWidth\|scrollIntoView" src/   # PF-1
grep -rnE "using +(global::)?(ExGrid\.)?(MudBlazor|Fluxor)\b" src/ExGrid/           # PRE-4
grep -rn "OrderBy\|Where(" src/ExGrid/Components/                                 # FN-15
grep -o -- "--ex-[a-z-]*" src/ExGrid/wwwroot/ex-grid.css | sort -u                # UX-1, UX-4
```

Read `ex-grid.js` against ADR-0021's five-entry table (PF-2), and `ExGrid.razor` for RR-9/RR-10.

### Step 4 — Layer 3, real browsers

```sh
cd tests/ExGrid.Browser
npm ci
npx playwright test 2>&1 | tee ../../verification/<date>/layer3.log
```

**On Chrome and on Edge** (ADR-0017/0026 — the `chrome` and `msedge` projects), and **on Windows or Linux at least once** — VZ-10 is a
tautology where scrollbars are overlays, and macOS alone does not discharge it.

The run must capture `page.on('console')` and `page.on('pageerror')` into `console.json` for
CON-1..4 and CON-8, and read `Performance.getMetrics` into `metrics.json` for MEM-2/5, DOM-5,
BIG-6/7, PF-6/7.

**MEM-5's ten-minute soak runs only when asked for** — `EXGRID_SOAK=1` (decided 2026-09-24).
An ordinary run skips it by name, and MEM-6 is read at its end. Sign-off needs one run with it
set, on each browser:

```sh
EXGRID_SOAK=1 npx playwright test memory.spec.mjs 2>&1 | tee ../../verification/<date>/soak.log
```

A skipped soak is `not run` in `results.md`, never a pass.

Discharges UX-2..11/15/16, VZ-1/10, ED-2/3/4/9/11, KB-1/8/11/12/28..32, CP-4/5/6/10/14, BIG,
PST-3/5, CON-1..6/8, DOM, ST-3, FN-21, and §23's layer-3 rows — the Wrapper's specs run in the
same command, against the proof-of-concept page.

### Step 5 — The randomised consistency run

Run ST-1's scripted sequence with a recorded seed, at 10³ and at 10⁶ rows, and record the seed in
`results.md` whether it passed or not.

**The 10⁶ case runs only when asked for** — `EXGRID_ST1_MILLION=1` (decided 2026-09-24). It takes
about a minute, nearly all of it the reference source sorting and filtering a million rows, so
Step 2 skips it by name and this step is the run that sets it:

```sh
EXGRID_ST1_MILLION=1 nix develop -c dotnet tests/ExGrid.Components/bin/Debug/net8.0/ExGrid.Components.dll \
  -class "ExGrid.Components.Tests.ConsistencyTests" 2>&1 | tee verification/<date>/st1.log
```

Pass: every case passes and none is skipped.

### Step 6 — Observational numbers

Record `metrics.json` and add a `spikes/render-bench` entry (PF-8). MEM-7 is layer 2's: after
Step 2's build, read it from `nix develop -c dotnet tests/ExGrid.Components/bin/Debug/net8.0/ExGrid.Components.dll -method
"ExGrid.Components.Tests.AllocationTests.Bytes_per_scroll_frame_are_recorded" -showLiveOutput` and
copy it into `metrics.json` by hand — layer 2 runs too often to write into `verification/` itself. Compare with the previous
verification directory and **write one sentence per number that moved more than 20%** — not as a
gate, as a note.

### Step 7 — Sign-off

`results.md` is complete when:

- every **MUST** is `pass`, or the release does not happen;
- every **SHOULD** is `pass` or carries a written reason naming the criterion;
- every **OBSERVATIONAL** number is recorded, with its comparison;
- **§21 lists no open question, and no criterion reads `BLOCKED`.** Both are true as this document
  stands, so the gate is that they stay true: a gap found during verification belongs in §21 with
  its answer, never in a commit that quietly decides it. A `BLOCKED` row must name its §21 item, and
  that item must be resolved or accepted in writing as out of scope for this release;
- **every reservation in §21.11 still has its trigger unfired, or has been settled with the feature
  that fired it.** A reservation whose feature shipped without settling it is a gap wearing a
  trigger's clothes;
- the toolchain, OS and browser versions used are recorded;
- ADR-0042's own prerequisites for a stable version hold: the public C# surface has been
  reviewed and decided in an ADR, and the release workflow's prerelease-only refusal comes out
  in the same change that rewrites ADR-0042.

**A criterion that could not be verified is not passed.** Write `blocked` and say why.

---

## 23. The Wrapper — `ExGrid.MudBlazor` (WR)

*(Added 2026-09-24, when the Wrapper's remaining seams were decided — ADR-0030's verification
order, ADR-0038, ADR-0039.)* §2 keeps the Wrappers out of the core's release; this section is the
Wrapper's own. It does not restate the core's criteria — **every criterion above that a Chrome or a
Theme can affect must hold with the Wrapper loaded** (WR-5) — and adds only what a real design
system can fail and a stub cannot.

The scenario for the layer-3 rows is the DemoHost's **proof-of-concept page**: an ordinary
MudBlazor application — `MudLayout` with an AppBar and a Drawer, `MudTabs`, a `MudDialog`, a
`MudSelect` in the toolbar, a light/dark switch, two grids — shaped like a Consumer's app and
holding nothing of any real Consumer's domain.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **WR-1** | MUST | The filter panel is the Wrapper's own, built from MudBlazor controls inside the core's popover, and uses the whole `FilterPanelContext`: a value list with a search and a Blank entry where the column declares one and the answer arrives; the condition form where it declares that, or where the answer is TooMany; Apply, Cancel, Clear (ADR-0009/0030) | Layer 2 on `MudGridChrome.FilterPanel`; Layer 3 on the proof-of-concept page | the same choices produce the same `FilterSpec` through the Wrapper's panel as through the built-in one |
| **WR-2** | MUST | A condition's operator is a `MudSelect` offering exactly `Allowed`, in the core's order; its value is a `MudTextField` for text, a `MudNumericField` for a number, an editable `MudDatePicker` for a date, and a `MudSelect` of true / false **with nothing chosen at first** for a boolean; Apply is unavailable until the operator's value is given (ADR-0009/0039) | Layer 2 per column type | the controls and the gating as stated |
| **WR-3** | MUST | The wording is MudBlazor's localised text wherever MudBlazor has a key, and the Chrome's own label function — English by default — for the rest; the Blank entry and its operator are the Chrome's own words and never say "empty" (CONTEXT.md **Blank**) | Layer 2 with a non-English `MudLocalizer` registered | the keyed words change; the Blank wording is the Chrome's |
| **WR-4** | MUST | The column menu and the Context Menu list exactly the core's commands, in its order, each with its `Enabled` state, as `MudButton`s with `role="menuitem"`; each core command id has a Material icon, and a Consumer's command has none unless the Chrome's icon function supplies one (ADR-0010/0036) | Layer 2 | items, order, state, roles and icons as stated |
| **WR-5** | MUST | Behaviour is the core's: every layer-3 test of KB-17, KB-28..32, FN-21, A11Y-19, UX-11 and CTX-1..4 passes with `MudGridChrome` exactly as it does with the built-in Chrome (FN-17, ADR-0010) | Layer 3, each such test run under both Chromes | identical outcomes |
| **WR-6** | MUST | `Striped` on `MudExGridPaper` turns Row Stripes on as a default the grid's own `StripeRows` beats, and the stripe takes the palette's table-stripe colour in both schemes; a scheme switch re-renders no row (ADR-0030/0038, RR-1) | Layer 2 for the cascade; Layer 3 for the colour and the render count | as stated |
| **WR-7** | MUST | The proof-of-concept page holds: a grid in a tab that was hidden paints correctly once shown; a Drawer toggle resizes a `Fill` grid and its geometry follows; a grid in a `MudDialog` opens its popovers whole — inside the grid's box, never cut by the dialog — and its Inner Popups above the dialog; the toolbar's `MudSelect` and a grid panel's never interfere; the two grids stay independent (ADR-0018/0028/0030/0039) | Layer 3 | every clause observed |
| **WR-8** | MUST | The Wrapper adds no script: no `.js` in the package and no interop call of its own; what MudBlazor's components run for themselves is MudBlazor's (ADR-0021 as narrowed by ADR-0039) | inspect the package; grep for `IJSRuntime`, `IJSObjectReference`, `.js` | none |
| **WR-9** | MUST | The Wrapper's own suites hold the core's console rules: zero errors, zero page errors, zero warnings from ExGrid's or the Wrapper's code, no unhandled exception (CON-1/2/3/6) | the shared layer-3 fixture; layer 2's unobserved-exception handler | as the core's |

