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
2026-09-24.)* *(2026-10-12, decided with the user,
[ADR-0190](adr/0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md) and
[ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
a release candidate, `0.1.0-rc.N`, is a prerelease too, and no release either: the sign-off is made
against it. Its notes rehearse the release's. They carry the blocks a version without a suffix must —
its known limitations, the SHOULD criteria accepted failing with their reasons, what is out of its scope
and what it supports — and a Signed off block in a pending form of its own, saying that the sign-off is
being made against this candidate. They open with the candidate's note, not the prerelease note. An
alpha's or a beta's notes carry none of the blocks.)*

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

- `layers-1-2.log`, the console output of section 22's Step 1, and one `<test project>.trx` per test
  project beside it — `ExGrid.Tests.trx`, `ExGrid.Components.trx`, `ExSheet.Engine.Tests.trx` and so
  on, one for each test project in `ExGrid.slnx` *(it said "twelve in all" until 2026-10-12, when there
  were fourteen)* — each holding every test of that project with its outcome, and every skip
  with its reason; `layer3.log` — full test output. *(Restated 2026-10-11, decided with the user: it
  named `layer1.log` and `layer2.log`, one per ExGrid suite. Steps 1 and 2 are now one run of every
  product's suites, since `0.1.0` carries all ten packages
  ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), and its
  output is kept per project so that each skip can be read by name.)*
- `console.json` — every console message and page error captured during the layer-3 run
- `metrics.json` — the OBSERVATIONAL numbers, in the shape section 22 defines
- `results.md` — this document's checklist with each ID marked pass / fail / blocked / n-a, and
  for every SHOULD failure the written reason

Without that directory, the verification did not happen.

---

## 2. What "finished" covers

**In scope: the `ExGrid` package** — the core component and its bundled `GridSource`. That is what
`CONTEXT.md` calls the specified product.

**Out of scope for this document:** `ExSheet` and `ExSheet.Engine` as a release, and the Wrapper packages
`ExGrid.MudBlazor` / `ExGrid.Fluxor`. The criteria that describe the Wrapper boundary (§4) are
written as properties of the *core's contract*, verifiable with a stub Wrapper, and do not require
a real one. **`ExGrid.MudBlazor`'s own release is judged by §23**, which holds the Wrapper to the
core's criteria with a real design system and adds the criteria only a real one can fail
(added 2026-09-24). **Whether ExGrid may be said to run on Blazor Server is judged by §24**
(added 2026-09-25): the core's criteria are the same on both hosts, and §24 says how they are
run on the second one and adds the ones only a circuit can fail.
**ExSheet's own criteria are §27** (added 2026-09-27). ExSheet is built alongside ExGrid and was
**not part of the release** — decided with the user, 2026-09-27 — so §27 judges ExSheet and never
gates ExGrid. *(Changed 2026-10-03, decided with the user: ExSheet, ExPivot and the data packages
**ship as prereleases with ExGrid**, at its version and from its tag
([ADR-0042](adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)).
"Release" in this document still means a version without a suffix. §27, §29 and §30 still judge
their own products and never gate ExGrid; which of them a stable version carries is decided at
ExGrid's sign-off.)* **The declarations ExSheet asks of ExGrid's core are ExGrid code, and they do gate
it**: they ship in the `ExGrid` package whether or not ExSheet ships, so §26 is part of "finished"
like any other section (decided the same day; the alternatives — shipping them unverified because
they are opt-in, or holding them off `main` — were refused).
**ExPivot's own criteria are §29**, added 2026-09-30 with ADR-0059 to ADR-0063. The user decided
those ADRs the same day, in a grilling that also added ADR-0064 to ADR-0069.
- **Not gating the release.** ExPivot, its engine and its MudBlazor Wrapper are built alongside
  ExGrid, and so are the family's data packages, `ExGrid.Data` and `ExGrid.Data.Arrow`, whose own
  criteria are §30. §29 and §30 judge those packages and never gate ExGrid. *(Since 2026-10-03
  they ship as prereleases with ExGrid, as ExSheet does; above.)*
- **In the release.** The declarations ExPivot asks of the core are ExGrid code, and gate it like
  the rest of §26: `OnCellDoubleClick` (DC-63), the Change Highlight (DC-64 to DC-66), and
  `ReturnKeyboardAsync()` and `HandKeyboardToAsync()` with `OnLeave` (DC-61, DC-62).

**The Selection Summary is §31**, added 2026-10-05 with
[ADR-0130](adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md). Its ExGrid
criteria gate the release; SM-12 and SM-13 judge ExSheet and ExPivot and do not. ExGrid now
references `ExGrid.Data` for the Aggregations' definitions, so **DA-18 gates the release too**, and
the rest of §30 still does not.

**ExGrid's live data is §32** (added 2026-10-05, with ADR-0140 to ADR-0142; ADR-0142 rewritten on
2026-10-07): the Row Key, the bundled sources' live data, what a write over live data does — it lands
as the user entered it, on the row it was aimed at — and ExPivot's report Windows over live data
(ADR-0151 to ADR-0153). It is ExGrid code, and §32 gates the release like §26. *(Narrowed
2026-10-11, ADR-0200: the rows on ExPivot's report Windows are ExPivot's code, and judge ExPivot.)*

**"Finished" means every ADR from 0001 to 0030 is implemented** — and, since 2026-09-27, the
ExGrid half of [ADR-0050](adr/0050-what-exsheet-asks-of-exgrids-core.md) and
[ADR-0051](adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md), judged by §26. This was asked as an open
question and answered deliberately: the narrower alternatives — shipping the display-only slice
first, or shipping everything except the Cell Editor and `Density` — were both on the table and
both refused. The consequence is accepted rather than discovered: **the whole of §21 blocks the
release**, including the ARIA surface that ADR-0029 records as not designed and the two gestures
(column resize, column reordering) that no ADR specifies. Nothing here may be marked done while
any of them is open.

**What gates `0.1.0`** *(added 2026-10-11, decided with the user: [ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md))*. The first stable version
is `0.1.0`, and it carries all ten packages stable at that one version. One tag publishes them all,
so the tag waits for every product's judgement, not only ExGrid's:

- ExGrid's sections as above (the preconditions, §3–§20, §25, §26, §28, §31 but SM-12 and SM-13,
  §32 but its ExPivot rows, and DA-18);
- **§23**, for `ExGrid.MudBlazor`;
- **§24**, because `0.1.0` claims Blazor Server: its declaration ADR is written once every §24 MUST
  passes, before the tag;
- **§27**, for ExSheet's three packages; **§29**, for ExPivot's three, with §32's rows on ExPivot's
  report Windows (LV-24 to LV-31, LV-34), which judge ExPivot and not ExGrid; **§30**, for
  `ExGrid.Data` and `ExGrid.Data.Arrow`.

Each product section gains, for its own packages, the preconditions this section has for ExGrid, and
is signed off in the same Step 7. Where a sentence below says a section "never gates ExGrid's
release", it still never gates ExGrid's *finished*; it gates the tag that carries its product.

### Preconditions — checked first, because everything else assumes them

*(Widened 2026-10-11, decided with the user: PRE-2, PRE-3, PRE-4 and PRE-6 are checked for each of
the ten packages `0.1.0` carries ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)),
which is what the paragraph above asks of each product section. The dependencies each package is
allowed are the ones its own rows already state; the rows below cite them rather than restate them.)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PRE-1** | MUST | The whole solution builds with zero warnings | `nix develop -c dotnet build ExGrid.slnx` | `0 Warning(s)` and `0 Error(s)` |
| **PRE-2** | MUST | The shipped packages — the ten under `src/` — target `net10.0` only, and raise no `LangVersion` (ADR-0022). *(Widened 2026-10-11, decided with the user, ADR-0200: it inspected `src/ExGrid/ExGrid.csproj` alone.)* | `grep -n "<TargetFramework" src/*/*.csproj`, and `grep -rn --include='*.csproj' --include='*.props' --include='*.targets' "<LangVersion" src/ Directory.Build.props` | one `<TargetFramework>net10.0</TargetFramework>` in each of the ten project files, and no `<TargetFrameworks>`; the second grep finds nothing |
| **PRE-3** | MUST | No shipped package takes a package dependency its rows do not name, and `ExGrid` takes no third-party one: its `.nuspec` declares exactly `Microsoft.AspNetCore.Components.Web` at ADR-0022's floor (10.0.0) and `ExGrid.Data` at exactly the same version (ADR-0019; ADR-0130, DA-18). Every dependency on a package of the family is on exactly the same version (ADR-0042), and the other nine declare what these rows state: `ExGrid.MudBlazor` — `ExGrid` and MudBlazor (PRE-4, SH-47, DA-1); `ExGrid.Data` — nothing (DA-1); `ExGrid.Data.Arrow` — `ExGrid.Data` and `Apache.Arrow` within a stated range (DA-1); `ExSheet.Engine` — nothing (SH-1); `ExSheet` — `ExSheet.Engine` and `ExGrid` (SH-1); `ExSheet.MudBlazor` — `ExSheet`, `ExGrid.MudBlazor` and MudBlazor (SH-47); `ExPivot.Engine` — `ExGrid.Data` (PV-1); `ExPivot` — `ExPivot.Engine` and `ExGrid` (PV-1); `ExPivot.MudBlazor` — `ExPivot`, `ExGrid.MudBlazor` and MudBlazor (PV-1). Each Wrapper takes MudBlazor in the one range [ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md) sets, `[9.6.0,10.0.0)`. *(Restated 2026-10-11, decided with the user. It said "`ExGrid` has no package dependency", checked with `dotnet list … package` for "no top-level package other than framework references", which a correct tree fails twice: `Microsoft.AspNetCore.Components.Web` is a package reference, at the floor ADR-0022 fixes, and `ExGrid.Data` has been one since ADR-0130. What it guarded — no third-party dependency in the core — is unchanged. Widened the same day to the ten packages, ADR-0200, with the Wrappers' MudBlazor range ADR-0205 decided.)* *(2026-10-12, decided with the user: the range's floor is 9.6.0, raised from 9.0.0 (ADR-0205's note of that date), and `ExPivot.MudBlazor`'s entry no longer says "from 9.0.0", which read as a floor with no ceiling.)* | `nix develop -c tests/ExGrid.PackageSmoke/check.sh`, which packs the ten packages and reads each `.nuspec`'s dependencies against these lists | it ends `package check: <version> passed` |
| **PRE-4** | MUST | The core, `src/ExGrid/`, references no Wrapper and no design system (ADR-0019/0030); the Wrapper packages beside it in `src/` depend on the core, never the reverse. Each of the ten packages references exactly the projects its rows state: `ExGrid` — `ExGrid.Data` (DA-1, DA-18); `ExGrid.MudBlazor` — `ExGrid`; `ExGrid.Data` — none (DA-1); `ExGrid.Data.Arrow` — `ExGrid.Data` (DA-1); `ExSheet.Engine` — none (SH-1); `ExSheet` — `ExSheet.Engine` and `ExGrid` (SH-1); `ExSheet.MudBlazor` — `ExSheet` and `ExGrid.MudBlazor` (SH-47); `ExPivot.Engine` — `ExGrid.Data` (PV-1); `ExPivot` — `ExPivot.Engine` and `ExGrid` (PV-1); `ExPivot.MudBlazor` — `ExPivot` and `ExGrid.MudBlazor` (PV-1). Only the three Wrappers use MudBlazor | `dotnet list <project> reference` for each of the ten (§22 Step 0), and grep the seven packages that are not Wrappers for a `using` of `MudBlazor` or `Fluxor` (§22 Step 3) | the references exactly as listed — for the core, the only project reference is `ExGrid.Data`; no match. *(Rewritten 2026-09-24: it used to grep all of `src/` for the bare words, which `src/ExGrid.MudBlazor/` — the Wrapper, living there by ADR-0019 — matches throughout, and so does a comment in the core naming `MudDataGrid`. What it asks is unchanged: that the dependency points one way)* *(Restated 2026-10-11, decided with the user: its pass said "no project reference", which a correct tree fails — the core has referenced `ExGrid.Data` since ADR-0130, for the Aggregations' one definition (DA-18). Widened the same day to the ten packages, ADR-0200; the grep for a `using` stays, over every package that is not a Wrapper.)* |
| **PRE-5** | MUST | The layer-3 project declares **both** target browsers (ADR-0017, ADR-0026) | inspect `tests/ExGrid.Browser/playwright.config.mjs` | a `projects` array naming `chrome` and `msedge`; a single-browser config fails this outright |
| **PRE-6** | MUST | Every file the build of the ten packages needs is git-tracked. *(Widened 2026-10-11, decided with the user, ADR-0200: it said "the build". All ten live under `src/`, so the check already read them; the statement now says so, and it is not ExGrid's alone.)* | `git status --porcelain -- src/` after a clean build | no untracked (`??`) file under `src/` |

---

## 3. Functional requirements (FN)

The feature set the ADRs specify. Each row is a capability that must exist and behave as its ADR
says; the areas that need more than one line get their own section below.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **FN-1** | MUST | The push entry point works: the grid paints the `Window` it is given, at `WindowStart`, of `TotalCount`, and holds no data of its own (ADR-0001) | Layer 2 | rows painted correspond to the pushed Window; no field in the component retains a Window after the parameter changes |
| **FN-2** | MUST | Passing both `Window` and `Source`, or neither, is **refused by name** (ADR-0001) | Layer 1/2 | an exception whose message names both parameters; not a silent winner |
| **FN-3** | MUST | A Range Request is raised for the visible range, after a render, never during one, and is not repeated until the Window moves (ADR-0001) | Layer 2, counting invocations | exactly one request per Window position; a Consumer answering synchronously does not re-enter |
| **FN-3a** | MUST | With a Range Request Margin of M rows, the grid raises its Range Request as soon as the rows on screen plus M either side, clamped into the total, are not all in the Window, while the rows on screen may still be held. The request names the rows on screen and is not repeated until the Window moves. At M = 0 the grid behaves as FN-3. `GridSource.Fetch` sets no margin (ADR-0183) | Layer 2, counting invocations at M = 0 and M = 10 near both edges and at the total's ends | as stated |
| **FN-4** | MUST | `GridSource.From` and `GridSource.Fetch` drive the same push path — one behavioural path, not two (ADR-0001) | Layer 2: same scenario through both entry points | identical painted output at every step, and identical row render counts wherever both sources hold the rows; the grid's own renders may exceed `From`'s by the states `Fetch` pushes that `From` does not (the loading state of a sort, a filter, and the restart when either is cleared, and the answer to a Range Request past what it read ahead), and nothing else. *(Restated 2026-10-11, decided with the user: it said "identical render counts", which a correct tree fails by exactly those states — they are the asynchronous fetch itself, not a second path. The same day, also decided with the user: the answer to a Range Request past the read-ahead is named among them. `From`, holding every row, never needs it, and an asynchronous source cannot answer without a state of its own.)* |
| **FN-5** | MUST | The bundled fetching Source breaks its own cold start, coalesces, and discards superseded answers (ADR-0025) | Layer 1 | as ADR-0025's clauses, each with its own test |
| **FN-6** | MUST | Pinned Columns are the leading N, always painted, and never virtualised away (ADR-0004) | Layer 2 + Layer 3 | pinned cells present at every scroll offset including mid-fling |
| **FN-6a** | MUST | While the Pinned block leaves the scrollable columns a band narrower than 40px, every column scrolls together; pinning returns when the box widens; the pinned count in View State is untouched (ADR-0045) | Layer 1 (`ColumnGeometry`) + Layer 3 narrowing the page | a Focus arrowed into a scrollable column is on screen; no `OnColumn…` or View State notification is raised by the suspension |
| **FN-7** | MUST | Cell State (`Normal/Stale/Missing/Error/Modified`) reaches the DOM as the closed class vocabulary and nothing else (ADR-0006/0029) | Layer 2 | exactly the `ex-state-*` classes; no Consumer vocabulary crosses the boundary |
| **FN-7a** | MUST | The Column's tone rule (`None/Positive/Negative`) reaches the DOM as the closed class vocabulary and nothing else; the grid derives no tone of its own, a null value is never offered to the rule, and the text, copy and raw forms are untouched by it (ADR-0006/0029) | Layer 2; Layer 3 under the Wrapper | exactly the `ex-tone-*` classes; a negative in a column without a rule is an ordinary cell; under the Wrapper a `Negative` cell is the palette's error colour and its neighbour the ink |
| **FN-8** | MUST | Row Kind (`Detail/Group/Total`) paints as a declared role carrying no depth and no aggregate (ADR-0024) | Layer 2 | `ex-row-group` / `ex-row-total` classes; no hierarchy API exists |
| **FN-9** | MUST | Action and Template Columns render as plain markup with one handler per cell region, not a component per cell (ADR-0020/0003) | Layer 2 render-count test | component instance count is a function of painted rows only |
| **FN-10** | MUST | `####` for numbers and dates that do not fit; ellipsis for text and boolean; never a truncated number (ADR-0016) | Layer 1 (`OverflowRules`) + Layer 2 | the decision is total over column types and matches ADR-0016's table |
| **FN-11** | MUST | Auto width only ever grows, is clamped to `[MinWidth, MaxWidth]` (defaults 40 / 400), and is never persisted (ADR-0016) | Layer 1 + Layer 2 | monotone under any observation order; a narrower screen never narrows a column |
| **FN-12** | MUST | A `Fixed` width below its own `MinWidth` is refused when the column is constructed, not clamped; one above `MaxWidth` is the user's and is kept, since `MaxWidth` bounds only what the grid computes (ADR-0016, rewritten 2026-09-25) | Layer 1 + Layer 2 | throws below, naming the column, for every kind of column; a drag past `MaxWidth`, recorded as `Fixed(px)` with the default bounds, paints at that width |
| **FN-12a** | MUST | A double-click on a column's grip is Size to fit: measured over the whole Window, not only the painted rows, and bounded by `[MinWidth, MaxWidth]` (ADR-0016, 2026-09-25) | Layer 2 + Layer 3 | a value in the Window but outside the painted rows sets the width; a column dragged past `MaxWidth` comes back to `MaxWidth` |
| **FN-12b** | MUST | A press and release on the grip with 4px of movement or less — the reorder gesture's threshold — reports no width (ADR-0016, 2026-09-25) | Layer 2 | no `ColumnWidthChange`; an Auto column stays Auto |
| **FN-12c** | MUST | When the grabbed column is covered by a whole-column range, a drag gives every column covered by a whole-column range the new width, and a double-click fits each to its own content (ADR-0016, 2026-09-25) | Layer 2 | one `ColumnWidthChange` per covered column; a range not spanning every row takes no part |
| **FN-12d** | MUST | A header's required width counts its label, the menu button's band and, for a sortable column, the sort indicator, in both Auto and Size to fit; full-width characters are charged at 1em; a header carries one full-width em of slack (ADR-0016, 2026-09-25) | Layer 1 + Layer 3 in both Chromes | a Japanese header and a sorted header paint whole, with no ellipsis, at their fitted width |
| **FN-12e** | MUST | Each `CellTextMetrics` default is at least the widest glyph measured for its class on Windows, macOS and Linux, and the measurements are recorded in ADR-0016; `−`, `+` and `#` are charged in the wide class, and the `####` fill counts `#` at its own width (2026-09-25). *(2026-10-02, ADR-0016: its own width in the face the cell is painted in. The browser cuts the run at the last whole `#` that fits, so the cell is full of `#`, as Excel's is.)* | by hand on each platform; Layer 3 on Linux | a bold amount estimated to fit paints without an ellipsis on each platform; `####` itself fits its cell, with no room left in it for one more `#` |
| **FN-13** | MUST | Sort and Filter travel as a serialisable structured model; no `Expression<Func<TRow,bool>>` appears in public API (ADR-0002) | inspect public surface; round-trip a `GridQuery` through `System.Text.Json` | round-trips equal; no `Expression` type in any public signature *(2026-10-11, [ADR-0207](adr/0207-the-view-state-travels-as-json-through-an-entry-point-of-exgrids-own-each-operand-with-its-type.md): through ExGrid's own JSON entry point, each operand with its type.)* |
| **FN-14** | MUST | `GridSource.From` is the reference implementation of filter and sort semantics, pinned exhaustively for null ordering, case sensitivity and culture (ADR-0001/0023) | Layer 1 | every clause of ADR-0023 has a named test |
| **FN-15** | MUST | The Consumer's sort and filter state travels in and the grid never sorts or filters (ADR-0001) | inspect: no ordering or predicate evaluation inside `ExGrid.razor` | grep finds no `OrderBy` / `Where` over `Window` in the component |
| **FN-16** | MUST | A pager exists when `PageSize` is passed, and rides the same Range Request machinery (ADR-0015) | Layer 2 | a page click raises the same notification shape as a scroll |
| **FN-17** | MUST | The Chrome seams exist and are substitutable: filter panel, column menu, Context Menu, cell editor, the cell's message, loading indicator (ADR-0009/0010/0034/0036) | Layer 2 with a stub `IGridChrome` | swapping Chrome changes rendering and **nothing** about behaviour — the same key and mouse tests pass against both |
| **FN-18** | MUST | Chrome renders and calls back; the core decides the menu items and the allowed operators (ADR-0009/0010) | inspect the contexts | `ColumnMenuContext.Commands` and `FilterPanelContext.Allowed` are produced by the core; Chrome has no way to add or reinterpret one |
| **FN-19** | MUST | The selected-cell count is available without data, and an off-screen selection is reported as such (ADR-0014/0015) | Layer 1 + Layer 2 | count equals the sum of rectangle areas; the off-screen flag is the rectangle/visible-range intersection test |
| **FN-20** | SHOULD | The focused cell's full value is available to Chrome for the formula-bar role behind `####` (ADR-0016) | Layer 2 | the raw value, never the `####` string |
| **FN-21** | MUST | A Chrome seam's contents may hold an **Inner Popup** drawn outside the instance root. While one is open: Escape closes the Inner Popup first and the next Escape the popover; under the design system's default (`ModalOverlay = false`) a pointer-down elsewhere in the instance closes both and keeps its own meaning, and under `ModalOverlay = true` only the Inner Popup closes; closing the popover any way removes the Inner Popup; DOM focus returns to the root (ADR-0039) | Layer 2: the contents report the popup through `InnerPopupChanged`, and the gate is told; Layer 3 with `ExGrid.MudBlazor`'s filter panel — a `MudSelect` and a `MudDatePicker` — by pointer and by key, under both settings, with two grids on the page and with one inside a `MudDialog` | every clause observed; the other grid unaffected |
| **FN-22** | MUST | A `Columns`, `HeaderGroups` or `Sorts` list handed again as the same instance with other elements is taken as a new list — a column added, replaced, removed or moved in place repaints header and cells when the list is handed again, never before (not through a render of the grid's own in between), since the grid draws from the read-only copy it took when handed the list — and the column state the grid holds (an open column popover, a resize in progress, a measured Auto width of the same instance) follows its column by Name; the same list holding the same elements renders nothing (ADR-0003, note of 2026-10-13; ADR-0027 P3) | Layer 2: a Consumer changing its lists in place, the grid rendering for its own reasons, then the Consumer rendering | the old state painted until the list is handed again, then each change; the open menu on its column after a move; no render when nothing changed |

---

## 4. UI / UX and the presentation contract (UX)

ADR-0027–0030 make this area unusually checkable: the contract is a *list* of classes and tokens,
so conformance is a set comparison rather than a judgement of taste.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **UX-1** | MUST | No colour is a C# parameter, and no pixel is only a stylesheet value (ADR-0027) | grep the public surface for colour-typed parameters; grep `ex-grid.css` for length literals. Layer 2 holds both: `NoColourParameterTests` and `StylesheetLengthTests` *(2026-10-12, decided with the user: the row gains the tests it lacked. The second reads every length in `ex-grid.css` against the Geometry Tokens the root emits and an allowlist of non-geometric visual values, each with its reason)* | zero colour parameters; every length in the stylesheet is a `var(--ex-*)` read or a non-geometric visual value |
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
| **UX-11a** | MUST | A popover whose room falls below one `RowHeight` when the box shrinks closes as a Cancel, and the keyboard returns to the root (ADR-0040, 2026-09-25) | Layer 2 with a reported size change; Layer 3 narrowing the page with the column menu open | the popover is gone; the next key reaches the grid |
| **UX-11b** | MUST | Columns narrower than the box are not stretched; a resize changes no column's painted width (ADR-0045) | Layer 3 resizing the page | every column's painted width equals its resolved width before and after |
| **UX-12** | MUST | Accessibility semantics as ADR-0033 specifies them | see **§4.1** | every row of §4.1 passes |
| **UX-13** | MUST | The row under the pointer is highlighted by an overlay band filled with `--ex-row-hover-background`, on hover alone — no row carries a class, and the band vanishes on leave (ADR-0029, ADR-0021 fifth entry) | Layer 3: real mouse moved down a column of two grids | one band, in the hovered instance only, following the pointer row; none after `mouseleave` |
| **UX-14** | MUST | The chosen action of an Interactive cell is tellable — outlined through `--ex-focus-outline`, and restated in the forced-colors block like every other state (ADR-0029/0037) | Layer 3 on `/cells`, with and without forced colors | the chosen button's computed outline is non-`none` in both, and no other button's is |
| **UX-15** | MUST | With `StripeRows` on, every row at an odd position in the whole result carries `ex-row-stripe` and is filled with `--ex-row-stripe-background`, **pinned cells included**; off by default; the stripe stays with its row across a scroll, across a pager's pages and on Placeholder rows (ADR-0038) | Layer 2: the class by absolute index at three scroll offsets and under a pager; Layer 3: a pinned and a scrollable cell of one striped row paint the same ground | exact parity at every offset; equal colours |
| **UX-16** | MUST | Meaning paints over a stripe: Row Kind's and Cell State's grounds win, a group or total row still counts in the parity, the overlays paint above, and the forced-colors block paints no stripe (ADR-0038/0029) | Layer 3, including forced-colors emulation | each ground as stated; the row after a group row keeps its parity |
| **UX-17** | MUST | The header's rule runs under every header cell: under a Pinned Column's header and under the Headings' corner as under any other (ADR-0029/0050) | Layer 3: the band's bottom pixel read under a pinned header, an unpinned header and the corner, under both Chromes | the rule's colour in all three |
| **UX-18** | MUST | The Focus outline is as wide on each of its four sides wherever the Focus stands: beside a Pinned Column, beside the Headings, under the header, and in the open (ADR-0008, 2026-09-29) | Layer 3: each edge read in device pixels at a cell beside a Pinned Column, a cell beside the Headings, a cell in the first row, and a cell away from all three | four equal edges at every one |
| **UX-19** | MUST | Excel's look for the Focus, a single range and several ranges: the selection's tint never covers the Focus cell (the Focus band and the hover band are other overlays and keep theirs); a one-cell selection shows the Focus outline and no tint; a single range shows one outline around the whole range and no outline around the Focus inside it; several ranges are each tinted and the Focus cell among them is untinted *(2026-10-12, decided with the user: among several ranges no range is outlined and no fill handle is shown, and the Focus takes a border of one Device Pixel in `--ex-focus-among-ranges-outline`, which is the Focus outline's colour unless set)*, lying inside the cell a Device Pixel in from its edges and its gridline, as the readings of 2026-10-12 measured Excel's (R4), and on Device Pixels wherever the cell's edges are (ADR-0090; ADR-0008, 2026-09-29 and 2026-10-12) | Layer 2 for which rectangles are painted, the hole's geometry, the Focus's class among several ranges and the border's token; Layer 3 by pixel under both Chromes, the border among several ranges read in device pixels at 100% and at a real 150% on `/wide`, on the Paper and under `ExGrid.MudBlazor` | as stated, in each of the four cases |
| **UX-20** | MUST | The Modified mark is a triangle in the cell's top right corner alone, painted in `--ex-state-modified-mark`, which is a colour — so a Wrapper that sets the token to a colour paints it — and a Pinned Column's Modified cell stays sticky (ADR-0029's fourth correction, 2026-10-07; ADR-0006) | Layer 3 on `/tones`: the corner's pixel against the Normal cell's, the top and right edges halfway and the pixels just outside the triangle against the same spots of the Normal cell, the token set to a colour above the grid, and the pinned cell's computed `position` | the corner differs; every other spot matches; the corner takes the token's colour; `sticky` |

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
| **A11Y-4** | MUST | One tab stop: the root takes focus, no cell does. On a grid that edits, the tab stop is its Keyboard Field instead, and the root is not one (ADR-0080, 2026-10-02) | Layer 3: Tab from before the grid, Tab again, then Escape and Tab; and the same with Shift+Tab from after it; on a display-only grid and on one that edits | focus enters the root, or the field on a grid that edits; the second Tab or Shift+Tab is the grid's own key (it moves the Focus, ADR-0012), and no cell and no column's ▾ takes DOM focus; after Escape the next one leaves the grid entirely, either way; no ▾ is reached by Tab or Shift+Tab on any grid (ADR-0080). *(Reading corrected with the user, 2026-10-02: it said the second Tab left the grid, which ADR-0012's cycle never did; the sixteenth Windows run, Part C.)* |
| **A11Y-5** | MUST | `aria-activedescendant` names the Focus cell's id, and ids are instance-prefixed (ADR-0018). It is carried by the element that holds the keyboard: the root, or on a grid that edits its Keyboard Field, and not by both (ADR-0080) | Layer 2 with two grids on one page | ids differ between instances; the attribute resolves to an element that exists |
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
| **A11Y-16** | MUST | A **refusal** reaches the root's live region once, in the Chrome's words (ADR-0203); anything else that shows it on the page is not a live region, or silences the grid's words for that reason *(2026-10-12: it read "Whatever Chrome renders a refusal into is a live region — the grid holds no string for it and cannot announce it (ADR-0035)"; ADR-0203 gave the grid the built-in Chrome's words, so the root says it, and a second live region would say it twice)* | Layer 3 against the reference Chrome | after Ctrl+V onto a column that cannot be edited, the grid's live region holds the words and changes again on a repeat, and the DemoHost's refusal line has no live-region role |
| **A11Y-17** | MUST | The grid's own action buttons are outside the page's tab sequence (`tabindex="-1"`), so the root stays the one tab stop with an Action Column on screen (ADR-0033/0037) | Layer 2 + Layer 3 on `/cells`: Shift+Tab from the element after the grid | every `.ex-action` carries `tabindex="-1"`; focus lands on the root, not on a button |
| **A11Y-18** | MUST | While a cell with several actions is Interactive, `aria-activedescendant` names the chosen action's button, whose accessible name is its declared label; leaving restores the Focus cell's id, and the attribute is cleared while that button is not painted (ADR-0033/0037) | Layer 2 | the id resolves to the chosen `.ex-action`; after Escape it resolves to the cell again |
| **A11Y-19** | MUST | A popover that takes DOM focus is named: the menus are `role="menu"` with `menuitem` items; a column's popover is `role="dialog"` holding its commands' menu where a filter stands below them, and the menu alone where none does; the column's dialog and menu carry `aria-label` set to that column's own header text — no sentence of the core's (ADR-0033/0036/0039/0044) | Layer 2, under the built-in Chrome and `ExGrid.MudBlazor`'s | roles and names as stated under both |
| **A11Y-20** | MUST | A **Prerendered** grid carries `aria-busy="true"` on its root, has no `tabindex`, and wears `ex-loading` — from the prerender until its key listener is attached, which on a circuit is after the first interactive render; the render that follows the attach lifts all three (ADR-0029/0033). *(Extended 2026-10-03:)* Focus that reaches the grid's scroller meanwhile — a press on rows painted before that render — goes to the root once that render has gone out, under the hand-back's condition: whichever comes first, the press or the attach, the keyboard ends on the root (ADR-0033's note of 2026-10-03) | Layer 2 with the renderer reporting it is not interactive yet, and with an interactive renderer whose module has not come back; the scroller's hand-off owed until that render, then asked once; Layer 3 on the Server host, reading the page before its circuit connects; PV-41's tests, the records' scroller focused in the task that draws them | the three present while Prerendered, none of them after; the root holding the keyboard |
| **A11Y-21** | MUST | While the Keyboard Field holds the keyboard, Chromium's accessibility tree resolves its `aria-activedescendant` to the Focus cell, or to the chosen action's button (ADR-0033/0037/0080) | Layer 3 over the DevTools protocol (`Accessibility`), under both Chromes, on `/sheet` and `/cells?editable=1` (the demo's `/cells` edits nothing unless asked, so it has no field) | the focused node is the field, and its active descendant is the `gridcell` (or the `button`) the attribute names |

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
| **HG-13** | MUST | Labels are centred by default; there is **no vertical alignment option anywhere in the grid** | Layer 2 (`ShippedStylesheetTests.The_shipped_stylesheet_aligns_nothing_vertically`, which reads the sources) + `grep -rniE -e "vertical-align" -e "align-items: *start" -e "align-items: *end" src/ExGrid/Assets/` | centred by arithmetic; no vertical alignment parameter exists; the grep finds nothing. *(Restated 2026-10-11, decided with the user: it grepped `src/ExGrid/wwwroot/`, which since ADR-0123 holds only the minified build and its source map (§22). And its alternatives were joined by escaped bars, as a table needs: read rendered, the bars are bare, and basic `grep` takes a bare bar literally. One `-e` per pattern reads the same rendered and raw.)* |
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
| **DIR-4** | SHOULD | No CSS logical property has crept in — the geometry is physical by decision, not by omission | `grep -nE -e inline-start -e inline-end -e margin-inline -e padding-inline -e inset-inline -e "text-align: *start" -e "text-align: *end" src/ExGrid/Assets/ex-grid.css` | no match. *(Restated 2026-10-11, decided with the user: it read `src/ExGrid/wwwroot/ex-grid.css`, which since ADR-0123 is the minified build (§22). And its alternatives were joined by escaped bars, as a table needs: read raw, `grep -E` takes an escaped bar literally, so the text as written matched nothing whatever the stylesheet held. One `-e` per pattern reads the same rendered and raw.)* |

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
| **VZ-8** | MUST | A result whose scrollable height would exceed 33,554,428 px (the scale-1 Layout Ceiling, whatever the scale; ADR-0053) is **refused by name**, header included, rather than silently clamped (ADR-0013) | Layer 1 + Layer 2 | throws, naming the row count and the ceiling; 1,000,000 rows at 28px passes, at 40px throws |
| **VZ-9** | MUST | The Scrollbar Gutter is subtracted before any geometry is computed, on both axes (ADR-0013/0021) | Layer 1 `ViewportBox`, Layer 2, Layer 3 `scrollbar.spec.mjs` | a gutter of 0 is bit-for-bit today's behaviour; the Focus never lands behind a bar |
| **VZ-10** | MUST | Scrolling to the far corner and back, at every zoom level, keeps the Focus inside the readable area (ADR-0012/0021) | Layer 3 `scrollbar.spec.mjs` at DPR 1 / 1.25 / 2 | passes on a platform **whose scrollbars occupy layout** — Windows or Linux — and not only where they are overlays. §22 states the reason: the test is a tautology against overlay scrollbars, so macOS alone does not discharge it |
| **VZ-11** | MUST | A geometry change re-anchors on the first visible row, not on the pixel offset (ADR-0028) | Layer 2: change density at scroll position P | the first visible row index is unchanged; the Focus's visibility is unchanged |
| **VZ-12** | MUST | Under `ViewportHeight=Stretch`, a reported size of 0 paints nothing and throws nothing; a *declared* 12px still throws (ADR-0028) | Layer 2 | the refusal keys on declared-vs-reported, not on the number |
| **VZ-12a** | MUST | Under a Stretch height, the grid takes a definite parent's height: in a sized box and as the rest of a flex column under a toolbar, the Viewport is the parent's content box and P1 holds (ADR-0028, 2026-09-26) | Layer 2 (the inline values) + Layer 3 on a Stretch page | rows painted to the box's bottom; the DOM row count is a function of the box, not of the result |
| **VZ-12b** | MUST | A Stretch-height grid whose parent has no definite height paints nothing and writes one warning naming the parent (ADR-0028, 2026-09-26) | Layer 2 with a reported band-height; Layer 3 on an auto-height parent | exactly one warning per instance; a real height afterwards paints normally and writes nothing more |
| **VZ-13** | MUST | Under `PageSize`, the scroll-ceiling refusal measures **one page**, and a paged result of any total binds; a `Density`/`RowHeight` change re-anchors on the first visible row **page-locally** (ADR-0013/0015/0028) | Layer 2 | a 2M-row paged result renders; the re-anchor writes local-row × height, never absolute-row × height |
| **VZ-14** | MUST | On a **real Windows desktop at fractional display scaling**, the Focus still lands inside the readable area (ADR-0012/0021). *(Rewritten 2026-10-01, decided with the user. It first named 125%, where the native scrollbar was a non-integer number of CSS pixels: 15.203125 on 2026-09-23. The grid now draws its own bar, `--ex-scrollbar-width`, 12 CSS px unless a Consumer sets it (b6777e0), so under Chrome and Edge the gutter is a whole number of CSS px at any scale, and the native bar's fraction no longer arises; a Consumer's own width can still be any length, which is what this row still guards.)* | Layer 3 `scrollbar.spec.mjs`, run on Windows itself at a fractional scale: 125% with the native bar (2026-09-23), 150% with the grid's own (the fifteenth run, 2026-10-01) | passes with the OS doing the scaling. CDP's `Emulation.setDeviceMetricsOverride` does **not** discharge this: it scales the page, and whether it reproduces what the OS does to the browser's own scrollbar is exactly the unknown. *(2026-09-27)* Emulation also misses the Layout Ceiling's clamp (ADR-0053) |
| **VZ-15** | MUST | Above the Layout Ceiling the scroll height is compressed, and the last row, the Focus at every edge and a whole-column selection are painted as at 100%; below it the geometry is bit-for-bit ADR-0013's (ADR-0053) | Layer 1 mapping; Layer 3 with Chrome launched at `--force-device-scale-factor=1.5` | BIG-1/5, SH-2, DC-2/3/7 and the scrollbar tests pass at 1.5; k = 1 at scale 1 |
| **VZ-16** | MUST | The grid is told its Device Pixel at attach and whenever the resolution changes, and lines drawn in Device Pixels are exact at a ratio between the stylesheet's steps: at `devicePixelRatio` 2.25 a thin line is one Device Pixel and a gridline two (its 1px rounded down to whole Device Pixels, as at 200%), not the step's 2.25 blended over three (ADR-0090, ADR-0021) | Layer 2 (the inline token from a report, and a change of ratio); Layer 3: the report at attach and a gridline's pixels at an emulated 2.25; by hand on Windows, a real browser zoom told as it changes and a thin line at a real 225% (CDP's override fires no change in branded Chrome and Edge, and emulation blends a thin line at any scale) | as stated; nothing per render reaches JavaScript |
| **VZ-17** | MUST | Every column edge, the Row Headings' included, lies on a Device Pixel, and the edges are the declared positions rounded once, so the total never drifts; a column's rule is one Device Pixel at 150%; every reader of the column geometry (cells, header, Selection, Cell Editor, hit test) reads the same edges; the View State, a resize's report and the `####` decision keep the declared widths, so an Auto column never turns to `####` with the scale (ADR-0090) | Layer 1 (`ColumnGeometry` at 1, 1.25, 1.5 and 2.25); Layer 3 pixels at 150% | as stated |
| **VZ-18** | MUST | A reveal is repainted: in the frame after it the offset moves one pixel away from the edge it stands at, and in the frame after that it moves back; the core paints no slice for either move and reads neither; a scroll the user makes between them stands, and the grid paints where it stands. Under Citrix with the browser's hardware acceleration off, Ctrl+↓, Ctrl+↑ and PageDown paint the rows they reveal without a further scroll (ADR-0012, 2026-10-08; ADR-0021) | Layer 3 `virtualisation.spec.mjs` for the mechanism; by hand on the Citrix PC where the white Viewport was seen, from the Docs Site's blotter, since no layer-3 run reaches it (that PC also shows the site as a remote browser isolation mirror, so the check covers both layers until ADR-0012's run of 2026-10-09 separates them) | the scroll events after PageDown read T, T−1, T and the painted slice changes once; a scroll made at the second frame is where the scroller and the painted rows end; on the Citrix PC the last rows show at once *(2026-10-12, decided with the user: **the check on the remote-isolation PC is a Known limitation of `0.1.0`.** It is not run for `0.1.0`, and the observation of 2026-10-08, which names no commit, is not taken as its record. The layer-3 half, `virtualisation.spec.mjs`, still gates. ADR-0202's Known limitations and `0.1.0`'s Release Notes say so.)* |
| **VZ-19** | MUST | A cell that shows a number may use one Device Pixel of its right padding, so a number the `####` decision shows is painted whole, with no ellipsis, however its column's edges round, at every scale and zoom; no `####` decision, General fit or Auto width changes; text is not given the pixel (ADR-0090, 2026-10-11) | Layer 2 (the inline geometry); Layer 3 pixels, with `system-ui` in DejaVu Sans (SH-41's sweep of eight offsets) and at 150% | as stated |

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
| **FL-10** | MUST | With search text in the box, OK applies the checked values among those that match, and no hidden value (ADR-0009, 2026-09-26) | Layer 1 (`FilterPanelChoices`) + Layer 2 under both Chromes | a checked value the search hides is absent from the applied `In` list |
| **FL-11** | MUST | The value list opens with "(Select All)" — checked, clear or mixed — which reads "(Select All Search Results)" and acts on the matching values while a search is active (ADR-0009, 2026-09-26) | Layer 2 under both Chromes | toggling it checks or clears exactly the values it names |
| **FL-12** | MUST | `Alt+↓` and the ▾ open one popover: the column's commands above its filter; no "Filter" command; OK, Cancel, a command, Escape or a dismissing press closes the whole; Tab wraps through both halves (ADR-0044) | Layer 2 + Layer 3 under both Chromes | one popover element; the filter's controls are reachable by Tab from the commands and back |
| **FL-13** | MUST | While a search is active on a column with a value filter, "Add current selection to filter" is offered, off by default; on, OK applies the filter in force together with the matching checked values (ADR-0009, 2026-09-26) | Layer 1 (`FilterPanelChoices`) + Layer 2 under both Chromes | off: replaced by the matches; on: the union; absent without a search or without a value filter |
| **FL-14** | MUST | The built-in condition form offers a second condition joined by AND or OR, applied as one `FilterSpec` of two clauses (ADR-0009, 2026-09-26) | Layer 2 | the applied spec has both clauses and the chosen combinator |
| **FL-15** | MUST | In the one popover, S, O, C and E sort ascending, sort descending, clear the column's filter and move to the search box — the condition's value where no value list stands — while focus is on a command or on the value list; in a text field a letter is text; the built-in labels show each letter underlined or as "(S)" (ADR-0044) | Layer 2 through `ResolveKey`; Layer 3 under both Chromes | each letter does its one thing; typing "e" in the search box types it |
| **FL-16** | MUST | "Clear filter" is a command, enabled only while the column has a filter; the filter half shows OK and Cancel only (ADR-0044) | Layer 2 under both Chromes | disabled with no filter; running it clears and closes |

---

## 7. Sort (SR)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SR-1** | MUST | Clicking a column header sorts; it does not select the column (ADR-0012) | Layer 3 | one click raises `OnSortChanged`; the selection is untouched by the click itself |
| **SR-2** | MUST | Column selection remains reachable by Ctrl+Space, Ctrl+Shift+Down, and the corner (ADR-0012) | Layer 2 | all three produce a whole-column selection |
| **SR-2a** | MUST | Shift+click on a header selects whole columns from the Focus's column to the clicked one, the Focus staying where it is, and does not sort (ADR-0012, 2026-09-25; ADR-0052) | Layer 3 | a whole-column range over the span; no `OnSortChanged` |
| **SR-2b** | MUST | Shift+← / → on a range spanning every row keeps it spanning every row; Shift+↑ / ↓ on a range spanning every column keeps it spanning every column (ADR-0012, 2026-09-25) | Layer 1 | Ctrl+Space then Shift+→ gives a two-column range over every row |
| **SR-2c** | MUST | While extending, the grid keeps the Extent in view only on an axis the range holding the Focus does not span end to end, judged after the move: with the view at the top, a header Shift+click or Ctrl+Space on row 1 followed by Shift+→ never scrolls down; Shift+↑ from there, which leaves the column short of its last row, scrolls to show the Extent. Under a pager a page turn is that scroll: Shift+→ over whole columns turns no page, and Ctrl+Shift+↓ from the first row stays on its page (ADR-0052, 2026-09-29; ADR-0015) | Layer 2 + Layer 3 | `scrollTop` unchanged after Shift+→; the Extent's row on screen after Shift+↑ |
| **SR-2d** | MUST | A press on a header released on the same column, having crossed no other, is a click and sorts (SR-1). A press that reaches another column — its header or its cells — selects whole columns from the pressed column to the pointer's, with the Focus on the first visible row, and never sorts, even when released back over the pressed column. Where a column reorder is wired, a header drag reorders instead, and Shift+click and Ctrl+click still select (ADR-0012, 2026-09-29; ADR-0050, item 1; ADR-0011) | Layer 3 | a whole-column range over the span; `OnSortChanged` not invoked by the drag |
| **SR-2e** | MUST | Ctrl+click on a header adds the whole column as a new range with the Focus on its first visible row, or takes a wholly selected column out under ADR-0052's take-out rule; Ctrl+drag adds the columns crossed as one range; neither sorts; Meta counts as Ctrl only on an Apple platform (ADR-0012, 2026-09-29; ADR-0050, item 1) | Layer 1 + Layer 3 | as stated; no `OnSortChanged` |
| **SR-3** | MUST | The grid never reorders rows itself; sort state travels out and a new Window travels in (ADR-0001) | inspect + Layer 2 | the painted order is exactly the pushed order, always |
| **SR-4** | MUST | A sort that changes the visible order drops the Selection; one that leaves the sequence identical keeps it (ADR-0011/0023) | Layer 2 | driven by the Row Sequence Version, both directions tested |
| **SR-5** | MUST | Sort semantics — null ordering, stability, culture, mixed types — match the reference implementation exactly (ADR-0023) | Layer 1 | every clause has a named test |
| **SR-6** | MUST | `aria-sort` reflects the applied sort on the header (ADR-0033) | Layer 2 | `ascending` / `descending` on the sorted header, `none` elsewhere |

---

## 8. Editing (ED)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **ED-1** | MUST | There is exactly one editor element, floating over the focused cell — never an input inside a row (ADR-0010) | Layer 2 | `document.querySelectorAll('input')` under the root ≤ 1 while editing; `ExGridRow` render counts unchanged by editing |
| **ED-2** | MUST | Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, and its Shift+arrows, Shift+Home and Primary Modifier's arrows commit and then move or extend the Selection as they do with no edit open, or, where a Reference can go, the Shift keys point; **Caret** moves the caret, and the Shift forms select text (ADR-0010, note of 2026-10-12) | Layer 2 + Layer 3, real keys | the two are distinguishable by observable behaviour, and F2 moves between them |
| **ED-3** | MUST | Esc / Enter / Tab are always the core's, in both states and while a descendant holds focus (ADR-0010) | Layer 3 | the editor never sees them |
| **ED-4** | MUST | The character that started Overwrite mode is not lost (ADR-0010) | Layer 3: type `5` onto a selected cell | the editor opens containing `5` |
| **ED-5** | MUST | A commit leaves the grid as an intent; the grid holds no committed value (ADR-0007) | Layer 2 | the painted value does not change until the Consumer returns a new row instance |
| **ED-6** | MUST | Uncommitted text is the grid's and survives a re-render of the cell underneath (ADR-0007/0027 P7) | Layer 2: push a new Window mid-edit | the editor's text is unchanged |
| **ED-7** | MUST | A density change mid-edit moves the editor with its cell and changes nothing else (ADR-0028) | Layer 2 | text preserved; box re-derived |
| **ED-8** | MUST | Editing does not start on a Placeholder — the Focus over unfetched data waits for the row (ADR-0012) | Layer 2 | no editor appears |
| **ED-9** | MUST | The editor's box is exactly `RowHeight` × the resolved column width (ADR-0028/0030) | Layer 3 | measured equal |
| **ED-10** | MUST | A bulk paste and a Ctrl+Enter fill are each **one** Edit Intent (ADR-0007/0011) | Layer 2 | one notification carrying all cells, not one per cell |
| **ED-11** | MUST | An IME composition is never taken by the core (ADR-0010) | Layer 3 with composition events | `isComposing` and keyCode 229 both pass through |
| **ED-12** | MUST | A press that is not on the editor commits first — Excel's click-away — and the press keeps its own meaning **unless the verdict Rejects** (ADR-0010/0034); a press inside the editor never reaches the delegated viewport. One commit is made at a time: a press made while the Consumer hears a commit waits for it to land and is then answered as if the Consumer had answered at once, so one commit raises one Edit Intent (ADR-0142's "One commit at a time", decided with the user 2026-10-09) | Layer 2 | another cell, and the header, both commit; the editor's input stops propagation; presses on the rows, a header and a column menu during a slow `OnEdit`, answered at once, in order and reversed, raise one Edit Intent, and a Source replaced afterwards still discards the next editor |
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
| **ED-21** | MUST | Text typed and not committed that the grid throws away is **announced with the reason that is true of it** — the columns changed, the row left the Window, the column stopped being Editable, the Source was replaced, or the keys were aimed with a Selection an order move dropped before they reached the grid (ADR-0011/0035). *(Narrowed 2026-10-07: an order move no longer discards an open editor, ADR-0011's note of that day; `OrderChanged` is not raised for one. Widened 2026-10-08: `SourceChanged` for an edit open when the Source was replaced, and `OrderMoved` for keys aimed with a Selection an order move dropped, ADR-0011's note of that day.)* Typing a commit handed over is never announced as thrown away: a Source replaced, or columns changed, by the commit's own handler while its Edit Intent is heard are that commit's. Accepted, the edit ends as committed with no discard; refused, it is discarded once, with the reason that is true of it (ADR-0142's section of 2026-10-08) | Layer 2, one per path; a commit whose handler replaces the Source or changes the columns, accepted and refused | `OnEditDiscarded` carries `ColumnsChanged` / `RowLeftTheWindow` / `ColumnNoLongerEditable` / `SourceChanged` / `OrderMoved`, never a neighbour's reason; no Edit Intent is raised in any of them, except a commit its own handler refused while replacing the Source or changing the columns, whose refused intent comes before its one discard; an accepted one is followed by none |
| **ED-22** | MUST | Keys typed while a mode-changing key, or a press on the rows while an edit is open, is being answered are **neither lost nor reordered**: they are held and replayed against the mode the answer leaves (ADR-0010, widened 2026-09-29). *(Widened 2026-10-02: a clipboard key among them — Ctrl+C, Ctrl+V — is neither, too. The copy or paste it fires is taken in its place and done at its turn, where the keyboard is then; ADR-0010)* | Layer 3 on the Server host with a 150 ms round trip injected: type `1500` onto an editable cell at full speed, then Enter; type `150` Enter `200` Enter; type a printable key then ArrowDown onto a cell that is not Editable; type `99` over a cell, click another cell and type `7` at once, on a plain editable grid and on a Sheet; on `/sheet`, `=` typed into a cell, then `A1+`, the Formula Bar pressed and `B1` typed at once, then Enter (ADR-0021, 2026-10-01); on `/sheet`, an edit ended by Enter and Ctrl+V at once; an edit ended by Enter, another cell clicked and Ctrl+V at once; an edit ended by Tab, another cell clicked and Ctrl+C at once; F2 and Ctrl+V at once (ADR-0010, 2026-10-02) | `1500` committed; `150` and `200` in consecutive cells; the Focus moved down one row and no editor opened; `99` committed and `7` typed into an edit opened on the clicked cell; `=A1+B1` in the bar before Enter, and committed; the paste lands in the cell the Focus moved to, and in the clicked cell; the clipboard holds the clicked cell's value, not what it held before; the pasted text stands in the edit F2 opened, at its caret |
| **ED-23** | MUST | Backspace on an editable cell opens Overwrite with an **empty** editor; nothing is written until a commit, and Escape leaves the cell as it was; on a cell that does not edit it opens nothing (ADR-0035, 2026-09-26) | Layer 2 + Layer 3 | the editor's text is `""`; zero `OnEdit` after Escape |
| **ED-24** | MUST | Delete raises **one** `GridClearIntent` over the whole selection — its ranges and the Row Sequence Version, no value — judged by the paste gate: a selection covering a non-editable column is refused whole as `TargetNotEditable` and raises no intent; no verdict function runs (ADR-0054) | Layer 2 | one intent whose targets equal the selection; under a refusal zero intents and one `OnPasteRefused`; zero validate calls |
| **ED-25** | MUST | Delete, Backspace, Ctrl+D and Ctrl+R are claimed only on a grid with an editable column and never while editing — inside the editor Delete and Backspace edit the text (ADR-0054/0035) | Layer 1 for the table, Layer 3 on `/cells` (display-only) and in the editor | the keydown reaches the page unprevented on a display-only grid; the editor's text loses one character |
| **ED-26** | MUST | An edit left standing when DOM focus leaves the root, for another grid, a control on the page or nothing, is neither committed nor discarded; keys pressed in another grid are that grid's; a press on the grid's rows or headings brings the keyboard back to the edit's surface first, then points or commits as it would have (ADR-0018 section 6, ADR-0021, 2026-09-29) | Layer 2: losing focus changes nothing; Layer 3 on `/sheet` (the positions grid, once the keyboard has left the Sheet for a control: the grid is in the Sheet's Pointing Scope, ADR-0058) and `/sheets` (two sheets), on both hosts | the edit's text survives; Escape in the other grid leaves it open; after a press back on the rows, `document.activeElement` is the edit's surface (pointing) or the root, or its Keyboard Field on a grid that edits (committed; ADR-0080), and the next key is this grid's |
| **ED-27** | MUST | An edit whose keyboard is elsewhere is told apart: while DOM focus is outside the root, the Cell Editor's outline is 1px wide, and full width again once the keyboard returns; its colour is `--ex-editor-outline`'s (ADR-0018 section 6, 2026-09-29) | Layer 3 on `/sheets` under both Chromes: an edit opened in each sheet, focus moved between them | the computed outline width of each editor follows where DOM focus is |
| **ED-28** | MUST | An edit that opens takes the keyboard only while the keyboard is still this grid's: the core's request that the editor surface take DOM focus, the built-in editor's and a Chrome's alike, is granted only while DOM focus is inside the root or on nothing, and never takes the keyboard from the Formula Bar or the Name Box the user pressed since (2026-10-01). If the keyboard has gone to another grid or a control on the page, the edit is left standing (ED-26), and keys typed before it left go into the edit in order (ADR-0021 and ADR-0018 section 6, 2026-09-30) | Layer 2: the editor's focus goes through the module, under both Chromes; Layer 3 on `/sheets` on the Server host with 40 ms injected, under both Chromes: `=` on the left Sheet and a press on the right Sheet at once, 30 times; `=1` typed at full speed on the left and a press on the right at once; on `/sheet` with 150 ms injected, a character typed onto a cell and the Formula Bar pressed in the task that paints the Cell Editor | the right Sheet holds DOM focus every time; the left edit stands with `=` or `=1` in it; the next press on the left's rows brings the keyboard back to it; the Formula Bar keeps the keyboard, and the next key goes into it |
| **ED-29** | MUST | While the edit is in the Formula Bar, typing keeps it in Caret: typing that ends Point there returns to Caret, an edit that moves from the cell into the bar goes into Caret, and in Caret `Home`, `End`, ← and → move the caret without committing. F2 in the bar does what it does in the cell, as Excel's bar goes from Edit to Enter and back: from Caret it points where a Reference can go and goes to Overwrite elsewhere, where `Home` enters the Formula and moves; F2 again returns to Caret (ADR-0051, 2026-09-30; Part B of the eighth Windows run; the tenth run, cases 14–24) | Layer 2; Layer 3 on `/sheet` under both Chromes: `=A1+B1` typed into the bar, `Home`, →, three Deletes; the same after pointing from the bar, and after an edit begun in the cell is pressed into the bar; and case `7k` (`=A1+B1` into the bar, F2, `Home`) | `=B1` in the bar, the edit open, the Focus on D10, in the first three; in `7k`, the Formula entered into D10 and the Focus on A10 |
| **ED-30** | MUST | On a selected cell of a grid that edits, an IME composes from the first key (ADR-0080, 2026-10-02): the composition is drawn over the Focus cell, and nothing commits, moves or points while it lasts; its end opens the Cell Editor in Overwrite holding its text, with the keyboard; a cancelled composition opens an empty edit; a press during a composition puts its text in the cell it was composed on, then commits there and selects what it pressed; keys typed after a composition's end follow it in order; over a cell that does not edit, nothing composes and nothing opens | Layer 2 for the core; Layer 3 composing through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) on `/sheet`, under both Chromes, on both hosts and behind 150 ms, with the key the IME takes told through `Input.dispatchKeyEvent` (its keydown and its keyup, `Process` with keyCode 229, as the sixteenth run recorded them) where one key ends a composition and starts the next (that run's k6, and a third clause while the editor's request waits), and that run's k15, Alt+Tab mid-composition: a keydown whose keyup never comes, the end, then the keyboard leaving the field, after which the Cell Editor has the keyboard holding the text; a real IME on Windows by hand (the sixteenth run) | D10 shows the composition while no edit is open; after its end the Cell Editor holds the text and has DOM focus; Enter commits it and moves to D11; after a press on D12 mid-composition, D10 holds the text and D12 is selected |
| **ED-31** | MUST | A press on the rows lands on the cell it was made on, however late the core hears it: a press held behind keys that moved the view is resolved against the rows, scroll, column widths and row height painted at the press, never against those at its replay, and the view is brought back to it; a press made under another row order, or under columns since renamed, reordered, added or removed, selects nothing and keeps its other meanings (ADR-0021, note of 2026-10-02; ADR-0010; ADR-0011; ADR-0012) | Layer 3 on both hosts at 0 and 150 ms: on `/sheet`, F1 pressed, `1` Enter PageDown `9` typed and F6's place pressed at once, then `2` Enter. Layers 1 and 2: a press told an earlier slice, an earlier horizontal scroll, an earlier layout, another row order, columns since reordered, a layout no longer kept | `2` in the row painted under the pointer at the press (F6 where the page had not moved yet), shown; the earlier slice, scroll and layout resolved as painted, and the view brought back only where it had moved; another order, reordered columns or a layout no longer kept select nothing and move no Focus |

---

## 9. Cell and range selection (SL)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SL-1** | MUST | Selection is a list of rectangles in position space; Ctrl+A over 10⁶ × 50 is **one** rectangle (ADR-0011) | Layer 1 | `Ranges.Count == 1`; no per-cell allocation |
| **SL-2** | MUST | There is no cap on selection (ADR-0011) | Layer 1 | selecting the whole result succeeds at any size |
| **SL-3** | MUST | Selection is dropped when the Row Sequence Version changes, and when the visible-column set changes (ADR-0011) | Layer 2 | both triggers, and the negative case (values changed, order did not) |
| **SL-4** | MUST | Columns are compared by name in order, never by array identity (ADR-0011) | Layer 2: hand a fresh but equal array every render | selection survives |
| **SL-5** | MUST | Disjoint ranges via Ctrl+click; Ctrl+click on a selected cell subtracts it, and the Focus goes to the first remaining cell, by rows, of the range made last; the fragments are ordered bottom to top as Excel lists them; the only selected cell cannot be taken out; the range holding the Focus is stored, not inferred (ADR-0012/0052) *(Rewritten 2026-09-28, ADR-0052 "What the third run settled": the Focus first stayed on its cell, or went to its Tab successor)* | Layer 1 | a rectangle splits into at most four; the Focus's range is a field; taking out the only cell changes nothing; every take-out in ADR-0052's third-run section gives Excel's Focus and fragment order |
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
| **SL-16** | MUST | Ctrl+A — paged or not — names a region and does not move the Focus (ADR-0012/0015/0052) | Layer 1 + Layer 2 | under a pager the page rectangle is selected with the active cell unmoved |

---

## 10. Keyboard (KB)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **KB-1** | MUST | The listener is capture-phase, on the **instance root**, never on `document` (ADR-0010/0018) | inspect `ex-grid.js`; Layer 3 with two grids | only the focused grid reacts |
| **KB-2** | MUST | The core decides which keys it claims; JS is a Set lookup only, and a disagreement shows as a key that does nothing (ADR-0010) *(Widened 2026-09-27: one exception, recorded in ADR-0010 — Ctrl+F in the find panel's field selects the field's text in the listener itself, ADR-0055)* | inspect: the table is in `GridKeys`; JS holds no meaning | no key semantics in `ex-grid.js` |
| **KB-3** | MUST | Control is the Primary Modifier everywhere; Meta is primary **only** on an Apple platform, and a Meta held elsewhere keeps the key out of the table (ADR-0012) — on **every** key path, the editor's included | Layer 1 `GridKeys.Canonical`; Layer 2 through `OnKeyAsync` with the editor open; Layer 3 on macOS | `Win+ArrowDown` does not canonicalise to `ArrowDown`; Cmd+Enter fills where Meta is Command and Meta+Enter does nothing where it is not. *(Widened 2026-09-23: the function was right and the editing path never called it, so Cmd+Enter on a Mac committed one cell and moved on — found by the first macOS run of CP-16's layer-3 test)* |
| **KB-4** | MUST | The keyboard and the mouse read the same answer about which modifier adds a range (ADR-0012) | Layer 2 | Ctrl+click and Ctrl+A agree on every platform value |
| **KB-5** | MUST | Arrows, Shift+arrow, Ctrl+arrow, Ctrl+Shift+arrow, Home/End and their Ctrl and Shift forms behave as ADR-0012's table says: the plain forms move the Focus, the Shift forms move the Extent and leave the Focus (ADR-0052) | Layer 1 + Layer 2 | every row of the table has a named test |
| **KB-6** | MUST | Enter runs down columns, Tab runs across rows, both wrap, and **neither ever leaves the selection** (ADR-0012) | Layer 1 | the exact cycle in ADR-0012's diagram |
| **KB-7** | MUST | With a single cell, Enter and Tab clamp at the last row/column and invent no wrap target (ADR-0012) | Layer 1 | Focus stays |
| **KB-8** | MUST | Escape with nothing left to dismiss releases Tab, so a keyboard user can tab out — after first closing an open popover, which is the inner layer — and keeps DOM focus: the next Tab or Shift+Tab goes to the browser, and any other key, or a press on the grid, keeps its meaning and ends the release, and so does DOM focus leaving the grid (ADR-0012, rewritten 2026-10-01; ADR-0080) | Layer 3, ExGrid and ExSheet, both hosts | after Escape the root, or its Keyboard Field, still holds DOM focus; the next Tab reaches the next page element; Escape, Escape, then a character opens an edit in the selected cell; Escape, an arrow, then Tab cycles inside the selection; Escape, a press on a cell, then Tab cycles inside it; Escape, a press on a control elsewhere on the page, then Tab back into the grid and Tab again cycles inside the selection (the release ends when DOM focus leaves the grid, ADR-0012 and ADR-0080, 2026-10-02) |
| **KB-9** | MUST | With focus but no selection, the first key only places the Focus on the first visible cell, without moving the Viewport (ADR-0012) | Layer 2 | one keystroke, no scroll |
| **KB-10** | MUST | A key that names the start of the row or the result returns the Viewport to the start, **with or without Pinned Columns** (ADR-0012) | Layer 2 `GoToStartTests` + Layer 3 | pinned and unpinned agree |
| **KB-11** | MUST | The grid is one tab stop; keys aimed at a focusable descendant are not taken (ADR-0010/0020) | Layer 3 with a Template Column input | typing in the input works; arrows move the caret, not the selection |
| **KB-12** | MUST | `:focus-visible` on the root makes the live grid tellable (ADR-0018/0029). On a grid that edits, its Keyboard Field holds the keyboard and matches `:focus-visible` on every focus, so the root's ring is drawn from the script's mark of a keyboard that did not arrive by a press (ADR-0080) | Layer 3 | the outline appears for keyboard focus and not for a click |
| **KB-13** | MUST | PageUp / PageDown move Focus **and** Viewport by the same N — the Focus keeps its position on screen (ADR-0012) | Layer 2 at a viewport of known height, pressed three times | N = fully visible rows; the Focus's offset within the Viewport is identical after presses 2 and 3 |
| **KB-14** | MUST | At the top and bottom the scroll clamps and the transition degrades to a reveal — the Focus is still fully visible (ADR-0012) | Layer 2, PageDown until the end | Focus inside the visible box at every step; never behind the header or a gutter |
| **KB-14a** | MUST | A shrinking box does not chase the Focus: the scroll offset stays, and the next key that moves the Focus reveals it; a scrollbar appearing over the Focus still reveals it (ADR-0012, 2026-09-25) | Layer 2 with reported size changes | no scroll on the shrink; a reveal on the next arrow; a reveal on a gutter appearing |
| **KB-15** | MUST | `Ctrl`+PageUp / `Ctrl`+PageDown are neither handled nor `preventDefault`-ed (ADR-0012) | Layer 3, observe `defaultPrevented` | `false`; the selection does not move |
| **KB-16** | MUST | No identifier in the keyboard surface is named "page" — `CONTEXT.md` puts it on the `_Avoid_` list under **Window** | `grep -ri "page" src/ExGrid/Keys/`, with comment lines and the browser's own key strings taken out — the command is in §22 Step 3 | matches only the browser's own `PageUp`/`PageDown` key strings: the command prints nothing. *(Restated 2026-10-11, decided with the user: the bare grep matches, on a correct tree, the doc comments in `GridKeys.cs` and `GridKeyClaims.cs` that speak of the web page a key is taken from — prose, not identifiers. A comment line is left out, and so is each of `"PageUp"`, `"PageDown"`, `"Shift+PageUp"` and `"Shift+PageDown"`, removed from its line rather than the line dropped, so an identifier beside one is still found.)* |
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
| **KB-36** | MUST | Firing an action — by pointer or by Space — moves no focus: the keyboard stays where it was, or in whatever the handler opened (ADR-0037, amended 2026-09-26) | Layer 2 counting focus requests; Layer 3 on `/inspectors` (RI-4 … RI-7, RI-26) | zero focus calls from the core per fire; the inspector opened by the action is `document.activeElement`'s ancestor, modal or floating, by click or by Space |
| **KB-28** | MUST | `Alt+↓` opens the column menu of the Focus's column, and does nothing on a column with no menu (ADR-0039) | Layer 1 for the table, Layer 2 for the open, Layer 3 for the browser taking no action of its own | the menu is open for that column |
| **KB-29** | MUST | Opening a column's popover or the Context Menu — by key or by pointer — puts DOM focus on its first enabled command, asked through the menu context's `FocusRequest`, once per opening, never by the core reaching into content it did not render. The filter below a column's commands is asked only when Tab, Shift+Tab or E moves the keyboard there — its `FocusRequest` / `FocusLastRequest`, from zero at each opening (ADR-0039/0037/0044) | Layer 2: the requests count up once per opening and once per move; Layer 3: `document.activeElement` is inside the popover, under both Chromes | as stated |
| **KB-30** | MUST | In a menu, ↑ / ↓ move among the **enabled** items and wrap, Home / End go to the first and last, Enter / Space run the item and close the menu, Tab / Shift+Tab close it as a Cancel — in the Context Menu and on a column with no filter below its commands; over a filter they move to its first / last control instead (ADR-0039/0044) | Layer 3, the same test against the built-in Chrome and against `ExGrid.MudBlazor`'s (FN-17) | identical outcomes under both |
| **KB-31** | MUST | In a column's popover, Tab / Shift+Tab move among the filter's controls and wrap through both halves while it stands — off either end of the filter to the first command, and from a command to the filter's first / last control; Enter in a value field applies (ADR-0039/0044) | Layer 3 under both Chromes | focus never leaves the popover by Tab; the filter applied equals the one OK applies |
| **KB-32** | MUST | However a popover closes — Escape, its ▾, a pointer-down elsewhere in the instance, a command run (Clear filter among them, ADR-0044), Apply, Cancel — DOM focus returns to the root, **including from inside an Inner Popup**; a dismissing pointer-down keeps its own meaning (ADR-0039/0010) | Layer 3 under both Chromes | `document.activeElement` is the root and the next arrow moves the Focus |
| **KB-33** | MUST | Keys typed straight after a key that opens a popover (`Alt+↓`, `Shift+F10`, `ContextMenu`) reach the popover, in order, once it holds DOM focus — never the grid underneath (ADR-0010/0039) | Layer 3 on the Server host with a 150 ms round trip: `Alt+↓` `↓` `Enter` typed together | the column is sorted descending — the menu's second item, which only the ↓ reaching the menu chooses |
| **KB-34** | MUST | A menu resolves each key against the place the keys have moved it to, not the item holding DOM focus: `↓` `Enter` typed together run the second item (ADR-0039) | Layer 2 through a context's `ResolveKey`; Layer 3 on the Server host with a 150 ms round trip, under both Chromes | `Copy with headers` ran, never `Copy` |
| **KB-35** | MUST | An Escape pressed straight after an Inner Popup opens is **not taken by the grid**: it reaches the control, and the popover stands (ADR-0039). Whether the control closes its popup on it is its design system's *(rewritten the day it was written: it first said "closes that popup", which promised the design system's half — MudBlazor's date picker ignores an Escape while DOM focus is still on the button that opened its calendar, and a round trip in, it is)* | Layer 3 on the Server host with a 150 ms round trip, under the mud Chrome, for the operator list and the date calendar | the panel stands four round trips later; the list closes |
| **KB-40** | MUST | While a range is extended the grid keeps the Extent in view, not the Focus; a move back inside the view scrolls nothing; Shift+PageDown moves the view a page with the Extent (ADR-0052) | Layer 2 + Layer 3 | as stated |
| **KB-41** | MUST | After Enter or Tab has moved the Focus inside a range, Shift+arrow moves the edge opposite the Focus, and does nothing on an axis where the Focus is on neither edge (ADR-0052) | Layer 1 + Layer 3 | Excel's case 3: A3 active in A1:C3 gives A1:D3 then A2:D3 |
| **KB-42** | MUST | Ctrl+. walks the corners of the Focus's range clockwise; Ctrl+Backspace reveals the Focus and changes nothing else; Shift+Backspace collapses the Selection to the Focus; none is taken from an open editor (ADR-0052) | Layer 1 + Layer 2 + Layer 3 | as stated |
| **KB-43** | MUST | Enter and Tab cycle through disjoint ranges in the order they were made, wrapping, and the Shift forms backwards; Tab after Enter continues from the cell Enter reached; after a take-out, Enter visits the fragment holding the Focus first, then the others in their listed order (ADR-0052, third run) | Layer 1 | the sequences in `verification/2026-09-28-windows-excel-3/active-cell.md` items 2 and 4, key for key |
| **KB-44** | MUST | A held Escape is one press: the press peels its one layer, and its repeats dismiss nothing more — a held Escape that closes a Formula Entry's list leaves the edit standing, one that closes a popover keeps the grid's keyboard, and one with nothing left to dismiss releases Tab with its press, its repeats leaving the release standing (KB-8) (ADR-0012, refined 2026-10-01 with ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` (held to close the dialog, the report keeps the keyboard) and on `/features` (held with nothing to dismiss, the next Tab leaves for the page) | as stated |
| **KB-45** | MUST | The page follows what a move reveals: after a move the grid makes (arrows, Ctrl+arrows, Home/End, PageUp/PageDown, Enter/Tab cycling, Ctrl+Backspace) and when the editor opens, a revealed cell outside the window is brought in at the nearest edge; a cell in the window moves nothing; nothing is followed when the box changes, or once the keyboard has left the grid or the Focus has moved on; a scripted popover or Template focus is not taken once DOM focus is elsewhere ([ADR-0204](adr/0204-the-page-follows-what-a-move-reveals-and-a-scripted-focus-asks-whether-the-keyboard-is-still-here.md)) | Layer 3 on both hosts, a grid taller than the window | the cell in the window after each move; the page still after a resize; no focus taken back from another grid |
| **KB-39** | MUST | Ctrl+Z raises `OnUndo`; Ctrl+Y and Ctrl+Shift+Z raise `OnRedo` — on every platform, Command folding into Control where it is Command; each key is claimed **only** when its notification has a listener, and never while editing (ADR-0007, 2026-09-26) *(numbered KB-37 on `main`; renumbered when the ExSheet branch, which had taken KB-37, was merged on 2026-09-28)* | Layer 1 for the table, Layer 2 through `OnKeyAsync`, Layer 3 for the page seeing an unclaimed Ctrl+Z | one notification per press; the selection unchanged; unwired, the keydown is not prevented |

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
| **CP-21** | MUST | A paste crosses to .NET as a stream, so a payload past a Blazor Server hub's default receive limit (32 KB) arrives whole and the circuit stays up (ADR-0005) | Layer 3 on **both** hosts: paste an Excel-shaped payload of about 200 KB | the intent covers the whole payload; no reconnect; CON-1..6 clean |
| **CP-22** | MUST | A paste past `PasteByteCap` (16 MB across both flavours by default) is **refused whole** with `TooLarge`, before .NET reads it, and never falls back to the `text/plain` flavour (ADR-0005) | Layer 2 with streams whose declared lengths straddle the cap | one past the cap: `OnPasteRefused(TooLarge)`, no intent, neither stream read; exactly on the cap: parsed |
| **CP-23** | MUST | A clipboard write the browser rejects is a Refusal: `OnCopyRefused(ClipboardUnavailable)` is raised and nothing lands (ADR-0005) | Layer 2 through the handle's report; Layer 3 with `clipboard-write` denied | the reason is raised once; the clipboard is unchanged |
| **CP-24** | MUST | Ctrl+D fills a range's rows below its top row from that row, Ctrl+R its columns right of its left column from that column; a range one row tall (one column wide) fills from the row above (column to the left); each is **one** `GridPasteIntent` carrying the raw, locale-free source values (ADR-0035, 2026-09-26) | Layer 1 for the plan, Layer 2 through the key | the intent's targets exclude the source row/column; values equal the raw form, never `####` |
| **CP-25** | MUST | Ctrl+D / Ctrl+R refuse **with a reason**, and raise no intent: `NothingToFillFrom` at the first row or column, `MultipleRanges` over more than one range, `SourceUnavailable` when the source rows cannot be had whole, `TargetNotEditable` over a non-editable target column — reported before the others, each range judged on its own target — and a non-editable **source** column is not refused (ADR-0035) *(Widened 2026-09-27: the order was first unstated, and the implementation reported the shape first)* | Layer 1 + Layer 2 | each reason named; the Ctrl+R source column may be read-only |
| **CP-26** | MUST | A copy that lands outlines each rectangle it copied with dashes, the **Copied Range**: once the `copy` event has set its data, or the asynchronous write has resolved — never when the payload is only built. A refused copy and a rejected write land nothing and leave the outline they found (ADR-0170) | Layer 2 (`CopiedRangeTests`) + Layer 3 (`copied-range.spec.mjs`) | no `ex-copied-range` before the landing; one per rectangle after it; an earlier build landing after a later one outlines nothing; a refusal keeps the previous outline |
| **CP-27** | MUST | The outline goes when the clipboard changes by any write but the grid's own copy: another grid's copy, a text field's, a script's, another application's (ADR-0170) | Layer 3, the real `clipboardchange`; Layer 2 for what the grid does when told | after the second instance copies, the first's outline is gone; after a page script writes, the second's is |
| **CP-28** | MUST | Escape removes the outline as a layer of its own, after a popover, a control and an Interactive cell; only the next Escape releases Tab and raises `OnLeave` (ADR-0170/0012/0070) | Layer 2 + Layer 3 | first Escape: no outline, `OnLeave` not raised, no release, the grid keeps DOM focus; second: both |
| **CP-29** | MUST | An edit opening, a new Row Sequence Version and a change of the visible columns each remove the outline (ADR-0170/0011) | Layer 2 | none after F2, after the version moves, after the columns change |
| **CP-30** | MUST | A copied cell in a row the grid holds that comes to paint other text removes the outline; a row replaced by an instance whose copied cells read the same, or a change outside the copied cells, keeps it (ADR-0170/0142) | Layer 2 + Layer 3 (`/features?upstream=1`, F9) | the Trader of a replaced row keeps it; its moved Notional drops it; the same for a copy gathered beyond the Window |
| **CP-31** | MUST | A paste into the grid leaves the outline (ADR-0170) | Layer 2 + Layer 3 | the outline after Ctrl+V is the one before |
| **CP-32** | MUST | A browser without `clipboardchange` draws no outline, and its copy lands as before; the dashes never move and read over the Selection's own outline (ADR-0170, UX-6) | Layer 3: the event's interface removed before attach; the `::after`'s computed style; the top edge's pixels before and after the copy | no `ex-copied-range`, the clipboard written; `dashed`, `none`, `0s`; the solid edge breaks into ink and ground |
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
| **BIG-4** | MUST | A row count whose scroll height would exceed the browser's 2^25 px (33,554,428 CSS px) is refused, not clamped | Layer 2 | VZ-8 |
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
| **ERR-2** | MUST | A refusal message says what to do next where one exists (ADR-0014: "reselect a target of the same shape"). *(Restated 2026-10-11, decided with the user, [ADR-0203](adr/0203-a-refusal-is-worded-by-the-built-in-chrome-unless-someone-else-words-it.md): the built-in Chrome words every paste and copy refusal reason, the words for a reason with a next step say it, and they reach the root's live region once — replaced, or silenced for a reason, through the Chrome's label function. It said "message asserted", written on 2026-09-02, before ADR-0035 put the words in Chrome.)* | Layer 1 for the words per reason; Layer 2 for the announcement, with and without a Consumer's words | every reason worded; the next step in the words where one exists; one announcement per refusal |
| **ERR-3** | MUST | A Window contradicting its own `TotalCount` or `WindowStart` is refused rather than painted at the wrong place (ADR-0001) | Layer 2 | throws |
| **ERR-4** | MUST | A null row, or the same row instance twice in one Window, is refused — Row Identity cannot tell them apart (ADR-0003) | Layer 2 | throws, naming the position |
| **ERR-5** | MUST | A Source that throws does not take the grid down: the failure surfaces and the grid keeps painting what it has (ADR-0025) | Layer 2 | the exception reaches the Consumer; the last good Window stays on screen |
| **ERR-6** | MUST | A missing or broken JS module does not take the Range Request down with it | Layer 2 | rows still requested; the grid does not sit on Placeholders forever |
| **ERR-7** | MUST | Disposal racing an in-flight interop call is not a fault and is not logged as one | Layer 2 + Layer 3 | no error; no exception |
| **ERR-8** | MUST | A configuration that cannot work is refused at the earliest point it is knowable, naming the parameter the user actually wrote | Layer 1/2 | e.g. the gutter refusal names the gutter, not `ViewportWidth` |
| **ERR-9** | MUST | The grid imports its script, and each UI package links its stylesheet, at an address that changes with the shipped file's content: the host's fingerprinted address where `Assets` knows it, else the plain one with the first twelve hex digits of the file's SHA-256 (or the hash in the file's name, if ADR-0123's measurement of GitHub Pages' CDN so decides). A deploy of a new version is therefore met with the new files on a normal reload, in a Blazor Web App and in a standalone WebAssembly app on GitHub Pages' headers ([ADR-0123](adr/0123-shipped-scripts-and-stylesheets-are-minified-and-each-package-asks-for-the-files-it-was-built-with.md), 2026-10-11) | Layer 1/2: the address per hosting model, the hash `ShippedAssetTests` checks; Layer 3: a standalone app served with `max-age=600`, its files swapped for another version's, then a normal reload | the new script and the new stylesheet run beside the new assemblies; no hard refresh |
| **ERR-10** | MUST | A script that is not the one the grid was built with, or none, is refused before anything attaches: the grid shows, in the built-in Chrome's words and in its live region, that the page holds another version's script and is to be reloaded, and never sits busy and silent (ADR-0123, ADR-0021's note of 2026-10-11) | Layer 2: a module answering another stamp, no stamp, and no module; Layer 3: the same with a stale script served | the refusal shown and announced once; nothing attached; no console exception |
| **ERR-11** | MUST | A stylesheet of another version shows the product's notice, by the stylesheet's own rules and with no script; the matching stylesheet hides it, and a stylesheet not yet loaded shows nothing (ADR-0123) | Layer 2: the notice element and its stamp per product; Layer 3: the notice hidden with the matching stylesheet, shown with another version's | as stated, for each of the six UI packages |

---

## 15. Console and runtime exceptions (CON)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **CON-1** | MUST | Across the whole layer-3 run, the browser console emits **zero** `error` messages | Playwright `page.on('console')` filtered to `error`, written to `console.json` | empty |
| **CON-2** | MUST | Zero uncaught page errors | `page.on('pageerror')` | empty |
| **CON-3** | MUST | Zero `warning` messages originating from ExGrid's own code | same capture, filtered by source file | empty; third-party warnings listed in `results.md` |
| **CON-4** | MUST | No `ResizeObserver loop completed with undelivered notifications` at any point, including during a window resize and a density change (ADR-0013) | same capture | absent |
| **CON-5** | MUST | The Blazor error UI never appears | Layer 3: `#blazor-error-ui` computed `display` | `none` throughout |
| **CON-6** | MUST | No unhandled exception reaches the host log during any scenario | inspect the host's output captured during the run — the WebAssembly DemoHost's is the browser console; the Server host's is its own process output, where a circuit's exceptions go *(generalised from "the DemoHost output" on 2026-09-25, when a second host arrived)* | no `Unhandled exception` line |
| **CON-7** | MUST | No unobserved `Task` exception | a `TaskScheduler.UnobservedTaskException` handler installed in the test host, plus a forced `GC.Collect(); WaitForPendingFinalizers()` at the end of Layer 2 | zero events |
| **CON-8** | MUST | Nothing is logged to the console by ExGrid on a healthy path — and a **failed copy is never silent**: a genuine .NET failure on either copy route is reported, distinguished from "no sync channel" by the attach-time probe, never by catching (ADR-0005) | Layer 3 | the only permitted console writes are the `console.error` failure paths in `ex-grid.js` (a key, a viewport report, a paste, a copy that the core failed to build or write), and none fires on a healthy path |

---

## 16. Performance (PF)

Structural invariants gate; milliseconds do not (§1).

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PF-1** | MUST | No per-cell JS interop, and no layout read on the path to a paint (ADR-0021 P4) | grep the sources under `src/` — the scripts' and stylesheets' sources in `Assets/`, the C# and the Razor; not `wwwroot`'s minified builds and source maps, nor `bin` or `obj` — for `getBoundingClientRect`, `clientWidth`, `offsetWidth`, `scrollIntoView`, comment lines left out, and ADR-0204's one page-follow line allowed (the commands are in §22 Step 3); count interop calls per frame in Layer 3 via CDP | zero matches in the component but ADR-0204's page-follow line, `element.scrollIntoView({ block: 'nearest', inline: 'nearest' })` in `bringIntoWindow` in `src/ExGrid/Assets/ex-grid.js`, which is that file's only `scrollIntoView` — both commands print nothing; interop calls per scroll frame ≤ 1. *(Restated 2026-10-11, decided with the user: the grep read all of `src/`, which on a correct tree matches the source map of `ex-grid.min.js` (it carries the script's comments), three doc-comment lines in `ViewportBox.cs` and `ColumnGeometry.cs`, and a comment in `ex-grid.js`, each saying why the grid does not read layout. A use is a line of code, which the narrowed grep still finds; a comment after code on the same line is still printed, and read.)* *(2026-10-12, decided with the user: the grep allows exactly ADR-0204's page-follow line and nothing else. `scrollIntoView` there is no read: the browser places the element at the nearest edge and the grid measures nothing ([ADR-0204](adr/0204-the-page-follows-what-a-move-reveals-and-a-scripted-focus-asks-whether-the-keyboard-is-still-here.md); ADR-0021's "the grid never measures"). The grep printed that line on `main` from #86 on, so the row failed on a correct tree. Any other `scrollIntoView`, and any `getBoundingClientRect`, `clientWidth` or `offsetWidth`, still fails.)* |
| **PF-2** | MUST | The script uses JavaScript for the entries of ADR-0021's table and for nothing else: every use in `ex-grid.js` is one of the table's entries, as its dated notes widen them, and none lies outside them. *(Restated 2026-10-11, decided with the user: it said "exactly the five entries ADR-0021 names, and `ex-grid.js` uses no sixth". The table has grown by ADR since — nine entries on 2026-10-11 — so the row names no count, and a count in this document can no longer fall behind the ADR.)* | read `src/ExGrid/Assets/ex-grid.js` against the table; Layer 2: `ShippedStylesheetTests`, whose `The_module_installs_only_the_listeners_the_allowlist_names` lists every listener the script installs, and whose tests named for each entry hold its shape | exact match; those tests pass |
| **PF-3** | MUST | Per-cell strings are interned or cached alongside the geometry that produced them, never composed in the render loop (ADR-0027 P5) | inspect `CellClasses` / `RowClasses` / `ColumnStyles`; Layer 2 allocation test (`RenderAllocationTests`) | zero string allocation per painted cell per render by the grid's own code. Measured as the slope of a re-render's bytes between 4 and 16 columns — for text, `####`, a sorted grid and header groups, where it is zero bytes of any kind. The attribute names Blazor composes for an event directive (`@on…:stopPropagation`, `:preventDefault`) are the framework's and outside P5 (ADR-0027 says why), so where a cell or header cell carries one — an action, a menu button — its class is pinned as a difference between two grids alike in every directive, and its handlers as ids that survive a render. *(Scoped 2026-09-24)* |
| **PF-4** | MUST | Scroll offsets are read once per frame, both axes in one call (ADR-0021) | Layer 2 `OffsetReads` | one read per scroll event, never two |
| **PF-5** | MUST | Selection painting cost is a function of the number of rectangles, not of the number of cells or the size of the selection (ADR-0008) | Layer 2, with and without Pinned Columns *(widened 2026-10-11, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md))* | element count and render count independent of selection area |
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
| **MEM-6** | OBSERVATIONAL | Managed heap after the same scripted run | Layer 3 / host counters — on the Server host, the server process's heap with the one test circuit open | recorded, with the host named |
| **MEM-7** | OBSERVATIONAL | Bytes allocated per scroll frame | Layer 2 | recorded |

---

## 18. DOM size (DOM)

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **DOM-1** | MUST | Element count under the instance root is independent of `TotalCount` | Layer 3 | VZ-1 |
| **DOM-2** | MUST | Element count under the root is independent of the size of the selection | Layer 3: 1 cell vs the whole result selected | equal but for a tint and a box per range in each layer it reaches, and the pinned shade layer while it holds a tint *(restated 2026-10-11, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): it read "one overlay element per range")* |
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
| **RR-5** | MUST | Handing over a fresh-but-equal Window does not re-render surviving rows; a `Columns` array is a change signal by its instance, so the same instance re-renders nothing and a new one re-renders every surviving row (ADR-0003). *(Restated 2026-10-11, decided with the user. It said a fresh-but-equal `Columns` array re-renders nothing, which the code, `RowMemoisationTests`, `GridColumn`'s documentation and the Docs Site's Columns page all contradicted. "Equal" would need a definition, and record equality calls two `Format` delegates that capture only `this` equal, so a column rebuilt for another culture would keep painting the old format — quietly wrong. The instance is the signal, as for rows.)* | Layer 2 | Window: render counts unchanged; `Columns`: unchanged for the same instance, every surviving row once for a new one |
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
| **ASY-6** | MUST | No test in any layer hangs: every awaited task in the suite completes or is cancelled | layers 1 and 2: within five minutes per layer, where a layer is one test project — one layer of one product — and §22 Step 1 runs each under Microsoft.Testing.Platform's `--timeout 5m`, which stops a run still going at five minutes and fails it; layer 3: within the 300 s per-test timeout of `tests/ExGrid.Browser/playwright.config.mjs`, or the shorter one a test sets for itself, in every run of §22 Step 4 — but the runs asked for by name, MEM-5's soak and the `measure*.spec.mjs` measurements, whose tests run for minutes by design and set a longer bound of their own (`test.setTimeout`), within that bound | all complete: no project's run stopped by its timeout in `layers-1-2.log`, and no `Test timeout of … exceeded` in Step 4's logs. *(Restated 2026-10-11, decided with the user: it said "run each layer with a 5-minute wall clock", which named no tool to apply it, and which layer 3 — tens of minutes on one worker — could never meet as a whole.)* |
| **ST-1** | MUST | After **any** sequence of operations, these hold: every selection rectangle lies inside the extent; the Focus and the Extent lie in the range that holds the Focus; the painted rows equal `ViewportGeometry.SliceAt`; both scroll offsets are within `[0, max]`; the displayed cell count equals the sum of rectangle areas | a scripted randomised sequence of ≥ 500 operations (click, drag, every key, scroll, sort, filter, page, resize, density change), asserting all five invariants after each step | zero violations; the seed is recorded in `results.md` |
| **ST-2** | MUST | The grid holds no committed data: after any sequence, removing the Consumer's state leaves the grid with nothing to paint (ADR-0001/0007). *(2026-10-11, decided with the user: an editor whose text is still the committed value — F2, nothing typed — closes when its row leaves the Window; an editor with typed text stays, as ADR-0142 says, and is not committed data.)* | Layer 2: push an empty Window at the end, including with an unedited editor open | the grid paints nothing and throws nothing |
| **ST-3** | MUST | The arithmetic's Row Height and the painted Row Height are the same number after every geometry change (ADR-0027 P6) | Layer 3 after each density change | equal |
| **ST-4** | MUST | View State is serialisable and round-trips: widths, order, pinned count, sort, filter (`CONTEXT.md`, ADR-0002) | Layer 1 through `System.Text.Json` | round-trips equal; Auto widths are **not** present, only the Auto intent (ADR-0016) *(2026-10-11, [ADR-0207](adr/0207-the-view-state-travels-as-json-through-an-entry-point-of-exgrids-own-each-operand-with-its-type.md): through ExGrid's own JSON entry point; a width is `"Auto"` or pixels; anything else is refused by name.)* |
| **ST-5** | MUST | A bundled Grid Source attached by a grid on another dispatcher while its first grid is live is refused by name; two grids on one dispatcher share it as before; once the first grid is disposed, another dispatcher may attach it (ADR-0018) | Layer 2 with two renderers | the exception names the cause; the same-renderer case and the after-disposal case attach without one |

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
rule this whole document exists to enforce, and §21.10 and §21.11 are what it looks like when the rule
is followed. *(Corrected 2026-10-11: it read "§21.10–§21.12"; there is no §21.12.)*

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
- **Whether `Stretch` needs debouncing** — ADR-0028. *(Sharpened 2026-09-25, ADR-0045.)* The case
  that needs measuring is a window drag on a **Server circuit with latency**, where the frame
  painted from the previous size lasts a round trip: drag the window under injected latency and
  record how long the stale band shows. *(Corrected 2026-09-26.)* `spikes/render-bench` cannot
  hold it, since it renders its own markup and not the grid, and the DemoHost is WebAssembly
  only. No host in the repository runs the grid on a Server circuit, so the measurement waits
  on one. *(Measured 2026-09-26, once the Server host existed:
  `EXGRID_HOSTING=server EXGRID_MEASURE=stretch` in `measure.spec.mjs`, on `/stretch?parent=window`,
  the window dragged from 420px to 820px tall in 20px steps over about 0.75 s, bundled
  Chromium under xvfb on Linux, three runs.)* At a 0 ms round trip, 3 or 4 of the drag's
  ~45 frames showed a gap below the painted rows, at most 12px, gone as the drag stopped. At
  50 ms, 18 to 28 frames, at most 32 to 40px, gone as it stopped. At 150 ms, 37 to 41 frames,
  at most 92 to 96px (four rows), gone 55 to 69 ms after it stopped. The circuit carried 35 to
  38 frames from the browser during the drag, whatever the round trip. So the band is the
  round trip times the drag's speed, and outlives the drag by about half a round trip. A
  debounce would hold the render back for its delay as well as the round trip, so on these
  numbers it lengthens the band rather than shortening it, and what it saves is those ~37
  frames. *(Decided with the user, 2026-09-26: no debounce — ADR-0028. The item stays here
  as the record of what was measured; it no longer waits on anything.)*
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
| **The fill handle** — neither the gesture nor the fill semantics | **settled with ExSheet, the editing work that was its trigger**: the core paints the handle, owns the drag and raises a Fill Intent; the Consumer decides what a fill means ([ADR-0050](adr/0050-what-exsheet-asks-of-exgrids-core.md), item 5). Its criteria are §26, DC-12..DC-14 and DC-27. *(Corrected 2026-10-11: it cited DC-17..DC-21, which are formula entry's; the fill handle's rows are DC-12 to DC-14, and DC-27 for the Selection after a fill.)* |
| **The Overlay application and the bundled undo stack** — ADR-0007 promises both as single, library-provided implementations; `InMemoryGridSource.ReplaceRow` covers only the Consumer that owns its rows in memory, and is recorded there as *not* being either | the first scenario-backed Consumer ([ADR-0007](adr/0007-edits-are-an-overlay-owned-by-the-consumer.md)'s "what wiring the editor settled"). *(Confirmed unfired on 2026-10-11, with the user: no scenario-backed page or Consumer exists in the repository, and the user's first Consumer has not adopted the grid. ExGrid hands Ctrl+Z to `OnUndo`; ExSheet keeps its own stack (ADR-0048), which ADR-0007 says is not this one.)* |
| **Right-click** — what a secondary click does to the selection | **settled with the Context Menu, its trigger**: a secondary click outside the selection collapses it onto the cell it lands on before the menu opens, and inside it the selection stands ([ADR-0036](adr/0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)); the criteria are CTX-1..CTX-5. Double-click arrived with the editor, as ADR-0008 said it would: it opens the Cell Editor in Caret ([ADR-0010](adr/0010-chrome-seams-column-menu-editor-loading.md)), and where no edit opens a Consumer may listen to it (DC-63). *(Restated 2026-10-11, decided with the user. It read "a secondary click leaves the selection alone today", with the trigger "the context menu, a Chrome seam ADR-0010 has not specified (ADR-0008)" — stale since ADR-0036 specified the menu.)* |
| **A copy over an Action Column** — **settled with the wiring that was its trigger**: an empty cell, because the column has no value by declaration and a refusal would fail an ordinary row copy for a column the user cannot sensibly unselect | recorded in [ADR-0005](adr/0005-copy-refuses-rather-than-truncates.md)'s "what wiring the routes settled" ([ADR-0020](adr/0020-action-and-template-columns.md) → ADR-0005) |
| **`ex-editing` on the root, `ex-editor`, the `--ex-editor-*` tokens** | **settled with the Cell Editor, its trigger**: all three exist as ADR-0029 named them ([ADR-0029](adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md), ADR-0010) |
| **The Cell Editor's own ARIA semantics** | the editor exists; its input is a bare, focused `<input>` whose interaction semantics are the platform's. A richer announced contract (mode announcements) stays open against a real screen reader, with the live-region wording ([ADR-0033](adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)). *(2026-10-11, decided with the user: **the richer contract is accepted in writing as out of `0.1.0`'s scope.** What the sixteenth Windows run heard — ↓ read as "Scan", DOM focus on `body` — may be a keyboard defect rather than wording, so the sign-off's Windows run checks where DOM focus is after ↓, with and without a Focus, under NVDA (recorded through its Speech Viewer) and Narrator (heard, with no copy command sent); a defect is fixed before the tag.)* |
| **The year ▸ month ▸ day tree over a date column's value list** — presentation only over the distinct dates already fetched | a Consumer declaring a value list on a date column (ADR-0044, 2026-09-26) |
| **`--ex-selection-outline`** — the border Excel draws round a range's perimeter | **settled with the selection paint polish, its trigger**, on 2026-09-29: the outline of a single range reads it, and it defaults to `--ex-focus-outline` ([ADR-0029](adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md), taken up by [ADR-0008](adr/0008-selection-is-painted-by-an-overlay.md); `.ex-range-single` in `ex-grid.css`). The criterion is UX-19. *(Restated 2026-10-11, decided with the user: it still named the polish as a trigger to come.)* |
| **Which Chrome seams `ExGrid.MudBlazor` implements first** | **settled with the package's start, its trigger** — a verification order: no seam, the Cell Editor, the loading bar, then the filter panel, column menu and Context Menu with Inner Popups allowed ([ADR-0030](adr/0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md), [ADR-0039](adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)); criteria in §23 |
| **Whether ExSheet is a sibling of ExGrid or a Consumer of it** | **settled 2026-09-27: a Consumer** ([ADR-0046](adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)). What ExSheet asks of the core is five opt-in declarations ([ADR-0050](adr/0050-what-exsheet-asks-of-exgrids-core.md)) plus formula entry ([ADR-0051](adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md)); being ExGrid features, their criteria are §26; ExSheet's own are §27, which does not gate ExGrid's *finished* and gates the `0.1.0` tag, which carries ExSheet stable. *(Restated 2026-10-11, decided with the user: it said §27 "does not gate the release", which [ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md) changed for `0.1.0` (§2).)* |
| **Find All and Replace (Ctrl+H)** — Replace is a bulk write over a set of cells that is not a rectangle, and needs its own refusals, cap and undo; Find All needs a result list the Consumer pages | **no trigger is named** ([ADR-0055](adr/0055-find-is-asked-of-the-consumer-like-sort-and-filter.md)'s "not decided here"). Recorded, not open: nothing depends on either, and neither key is claimed. *(2026-10-11, decided with the user: **accepted in writing as out of `0.1.0`'s scope**; its trigger is named after `0.1.0`.)* |
| **Dragging a Reference Outline to rewrite its Reference, and the corner squares that are its handles** — Excel moves a Reference by its outline's edge and resizes it by a corner; the squares are not drawn until the drag exists, so as not to offer a drag that does nothing | **no trigger is named** ([ADR-0057](adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md), "Not done", decided with the user 2026-09-30). Held in [ticket 31](specs/exsheet/issues/31-drag-a-reference-outline.md) with what the eighth Windows run saw, so the squares arrive with the gesture. *(2026-10-11, decided with the user: **accepted in writing as out of `0.1.0`'s scope** — a known limitation of `0.1.0`, as §27 says ([ADR-0202](adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)); its trigger is named after `0.1.0`.)* |
| **A range of columns pointed at through a Pointing Scope** — a range would be written as `T[[A]:[B]]`; until then a range writes nothing. *(A key of several columns, open here until 2026-10-03, is settled: SH-61.)* | **the engine gaining column ranges** ([ADR-0058](adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md), "Not in the first version"). SH-37's remaining test fails on that day and says what to do |
| **The arrow keys after pointing into a registered grid, and completion after `Table[`, F3 and Backspace** | **settled by the ninth Windows run, its trigger** ([ADR-0058](adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md), "What the ninth Windows run settled"): the arrow keys point inside the grid, `Table[` lists the columns, Backspace lists again, and F3 is left to the browser. Criteria SH-35, SH-36 and DC-55 |
| **ExGrid reading a Snapshot** — ExGrid's bundled sources hold the Consumer's objects, named by a Row Key, and a screen that shows one feed in ExGrid and ExPivot applies each change twice | a screen showing one live feed in ExGrid and ExPivot, or a measurement showing a user's sort of 10⁶ rows too slow in the browser ([ADR-0141](adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md); [ADR-0064](adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)'s "each after measuring"). *(Trigger confirmed unfired on 2026-10-11, with the user: the only page that holds an ExGrid beside an ExPivot is `PivotPage.razor`, and its ExGrid shows Show Details' records, not the pivot's live feed. Nor has the other trigger's measurement been made: ADR-0141 records that the browser was not measured, and no `verification/` record measures a user's sort of 10⁶ rows there.)* |
| **ExPivot keeping the report rows a redraw did not change**, with one `CellChangedAt` held across data versions so that a kept row skips its render | **settled 2026-10-07, after its trigger was met** (ADR-0140 built, the gain measured: ticket 01), **and settled again on 2026-10-08** when the two tracks of live data continued were compared: published Report Versions share the structure a batch did not change, the display rows handed to ExGrid hold no report, and unchanged rows keep their instance ([ADR-0153](adr/0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md)). The Claude Code track's ADR-0161 made the next report from the last instead; its out-of-memory rule is what remains of it. Criteria LV-29, LV-30, PV-45 and PV-47 |
| **Which layer VZ-18's repaint answers** — the white Viewport was seen on a PC that, it turned out on 2026-10-09, showed the site through a remote browser isolation mirror, so whether Citrix's painting or the mirror needed the repaint is not known. The repaint stays, harmless elsewhere, and VZ-18 is checked as written, across both layers | **the localhost run on that PC** that `verification/2026-10-09-windows-remote-isolation/README.md` describes: the Docs Site served from the PC itself, the repaint off, Ctrl+↓ pressed ([ADR-0012](adr/0012-anchor-focus-and-keyboard-navigation.md), 2026-10-09). Made at the sign-off's visit to that PC if the PC can serve it (reserved 2026-10-11, decided with the user) *(2026-10-12, decided with the user: VZ-18's check on that PC is a Known limitation of `0.1.0` and is not run for it, so neither is this run at `0.1.0`'s sign-off; the reservation stands for after it.)* |
| **A column band** — the Focus band's mechanism turned sideways | **no trigger is named.** ADR-0008 says only "reserved, not specified". With Find All and Replace and with dragging a Reference Outline it is one of the three entries here whose *when* nobody has written down; nothing depends on it, and it is recorded rather than quietly promoted to open. *(Restated 2026-10-11, decided with the user: it said "one of the two entries", which left out dragging a Reference Outline, recorded on 2026-09-30. All three are **accepted in writing as out of `0.1.0`'s scope**, and their triggers are named after `0.1.0`.)* |

*(Rewritten 2026-10-11, decided with the user. It read: "None of these blocks a criterion above. That
is checked, not assumed: no row in §3–§20 asserts a fill handle, a context menu, an editor class, a
selection outline, a Wrapper's seam order, or a column band, and the one clipboard case (an Action
Column inside a copied range) is deliberately absent from §11 rather than written as a guess." Half of
that had stopped being true as the entries it named were settled and their criteria written.)*

**No reservation still standing blocks a criterion.** That is checked, not assumed. The entries
settled since this table was begun have their criteria, and those gate like any other: the fill
handle §26's (DC-12..DC-14, DC-27), the Context Menu §11's (CTX-1..CTX-5), the editor's classes and tokens
ADR-0029's table (UX-4) and ED-27, the selection outline UX-19, the Wrapper's seam order §23's. Of
the entries still reserved, no row asserts what they leave undecided — the Overlay application or the
bundled undo stack, a richer announced contract for the Cell Editor, the year ▸ month ▸ day tree, a range of
columns through a Pointing Scope (SH-37 pins today's refusal and passes), ExGrid reading a Snapshot,
which layer VZ-18's repaint answers (VZ-18 is checked as written), Find All or Replace, dragging a
Reference Outline, or a column band.
*(2026-10-12, decided with the user: VZ-18's check on the remote-isolation PC is a Known limitation of
`0.1.0`, not run for it; the row's layer-3 half still gates.)*

**Three entries name no trigger** — Find All and Replace, dragging a Reference Outline, and a column
band. By the rule above, a question nobody has decided and no ADR schedules would be open; the user
accepted all three in writing as out of `0.1.0`'s scope on 2026-10-11 — the form Step 7 gives for a
§21 item that is not resolved — and their triggers are to be named after `0.1.0`.

**The copy over an Action Column**, kept out of §11 while it was undecided rather than written as a
guess, is settled in ADR-0005 (an empty cell), and no row of §11 asserts it yet.

---

## 22. The final verification checklist

Run in this order. Each step names the criteria it discharges. Stop and record — do not skip —
when a step cannot be run.

**A check that reads a shipped script or stylesheet reads its source, in `src/<Package>/Assets/`**
— `ex-grid.js` and `ex-grid.css` are `src/ExGrid/Assets/ex-grid.js` and `src/ExGrid/Assets/ex-grid.css`
wherever a row names them. `wwwroot` holds only the minified builds and their source maps
([ADR-0123](adr/0123-shipped-scripts-and-stylesheets-are-minified-and-each-package-asks-for-the-files-it-was-built-with.md)),
which `ShippedAssetTests` holds to those sources: grepped, a minified file is one long line and a
source map carries the comments, so a check there misses or mis-finds. *(Added 2026-10-11, decided
with the user, when the rows that read `wwwroot` were restated.)*

### Step 0 — Preconditions

*(Restated 2026-10-11, decided with the user: PRE-2, PRE-3, PRE-4 and PRE-6 are checked for the ten
packages, ADR-0200. It ran `dotnet list … package` and `dotnet list … reference` on
`src/ExGrid/ExGrid.csproj` alone, and read that project file for PRE-2.)*

```sh
nix develop -c dotnet build ExGrid.slnx                 # PRE-1
grep -n "<TargetFramework" src/*/*.csproj               # PRE-2
grep -rn --include='*.csproj' --include='*.props' --include='*.targets' "<LangVersion" src/ Directory.Build.props   # PRE-2
nix develop -c tests/ExGrid.PackageSmoke/check.sh       # PRE-3
nix develop -c bash -c 'for p in src/*/*.csproj; do echo "== $p"; dotnet list "$p" reference; done'   # PRE-4
grep -n "projects" tests/ExGrid.Browser/playwright.config.mjs   # PRE-5
git status --porcelain -- src/                          # PRE-6
```

### Step 1 — Layers 1 and 2, one run of every suite

*(Restated 2026-10-11, decided with the user. Steps 1 and 2 ran `dotnet test` on
`tests/ExGrid.Tests` and on `tests/ExGrid.Components`, ExGrid's two suites, into `layer1.log` and
`layer2.log`. `0.1.0` carries all ten packages (ADR-0200), so the run is the whole solution's — its
twelve test projects, every product's layers 1 and 2 and the demo API's tests — once, with each
project's results kept apart.)* *(2026-10-12: "twelve" is every test project in `ExGrid.slnx`, thirteen
on `main` that day with `tests/ExGrid.Docs.Generator.Tests`, and fourteen with
`tests/ExGrid.Release.Tests`; the run takes them all, whatever their count.)*

```sh
rm -rf tests/*/bin/Debug/net10.0/TestResults
nix develop -c dotnet test ExGrid.slnx \
  -p:TestingPlatformCommandLineArguments="--report-xunit-trx --report-xunit-trx-filename results.trx --timeout 5m" \
  2>&1 | tee verification/<date>/layers-1-2.log
for trx in tests/*/bin/Debug/net10.0/TestResults/results.trx; do
  project=${trx#tests/}; cp "$trx" "verification/<date>/${project%%/*}.trx"
done
```

The suites run on Microsoft.Testing.Platform (xUnit v3 with `TestingPlatformDotnetTestSupport`), so
VSTest's `--logger trx` does not reach them. The xUnit runner's own TRX report does: its options go
through `TestingPlatformCommandLineArguments`, as CI passes its coverage options, and each project
writes `results.trx` into its own `bin/Debug/net10.0/TestResults/`, which the loop copies out under
the project's name. `--timeout 5m` is ASY-6's bound for each project.

Discharges the Layer 1 and Layer 2 rows of every section — for ExGrid, the Layer 1 rows of FN, FL,
SR, SL, KB, CP, ERR, ASY, ST-4, and VZ, RR, MEM-1/3/4, ASY, ST-1/2 and the other Layer 2 rows; for the
other products, §23's and §25–§32's — and ASY-6's first half.

### Step 2 — Reading the run

**Pass:** zero failures in every project, and `CON-7`'s unobserved-exception handler reports zero.
**Zero skips but these, each found by name in its project's `.trx`** — a test that did not run,
skipped or explicit and not asked for, is a failure unless it is one of them:

- **ST-1's 10⁶-row case** (`ExGrid.Components`, `ConsistencyTests`), skipped by name and run in Step 5;
- **the ten `[Fact(Explicit = true)]` measurements**, which xUnit reports as not run by explicit
  test filtering, and Step 6 runs: DA-17's two in `ExGrid.Data.Tests` (`MeasureTests`), ADR-0065's
  one in `ExGrid.Data.Arrow.Tests` (`MeasureTests`), PV-21's four in `ExPivot.Engine.Tests`
  (`Measurements`), and SH-19's three in `ExSheet.Engine.Tests` (`Measurements`:
  `SH19_a_full_recalculation`, `SH19_one_edits_propagation`, `SH19_a_row_inserted_near_the_top`);
- **`CsvClockTests`' one test** (`ExGrid.Data.Tests`), and only on a machine where a process cannot
  move its time zone through `TZ` — Windows — whose own zone has a base offset of zero, UTC's or
  London's: the test reads under Tokyo's zone where `TZ` moves it, and skips by name where its
  reading would prove nothing.

*(Restated 2026-10-11, decided with the user. Step 2's rule was "zero skips but one — ST-1's 10⁶-row
case", and Step 1's "zero skips; a skipped test is a failure unless `results.md` records why". The
one run holds every product's suites, and the rule names each skip it allows rather than leaving
any to a note.)*

*(2026-10-12: "seven" became "ten" with SH-19's three. SH-19 is the case the user decided on
2026-10-12, a 10,000 × 20 block with its twenty whole-column totals beside it (ADR-0202's note of
that date); its measurements are explicit tests as PV-21's are, and the variant with a whole-column
`SUM` on every row runs as `spikes/exsheet-sh19-whole-column`, outside the suite.)*

*(2026-10-12, the release tool's reader of this step; the rule above is unchanged.)* The run is read from
the `.trx` files Step 1 copied into `verification/<date>/`:

```sh
nix develop -c dotnet run --project tools/release/ExGrid.Release -- skips --trx verification/<date>
```

It refuses each test that failed, and each that did not run and is not one of those above, by its project
and its name. It refuses an allowed test left out otherwise than this step says: an explicit measurement
is written `NotRunnable` in its `.trx`, and a skip `NotExecuted` with the reason its test gives, which must
be that reason. It refuses a test project of `ExGrid.slnx` with no `.trx`, a `.trx` named for no test
project, and a `.trx` it cannot read whole. The list it holds the run to is written in its code as this
step names the tests, and a layer-1 test holds the two together and to the tests in the source. It names
`CsvClockTests`' skip with its reason; the `.trx` does not say the machine, so whoever signs off confirms
from the run that it was one where `TZ` cannot move the zone. Nor does it read CON-7's count of unobserved
exceptions, which is read from `layers-1-2.log`. The release workflow does not run Step 1, so it does not read
the run: the sign-off does.

### Step 3 — Static inspection

Grep and read, recording each result:

```sh
grep -rnE "getBoundingClientRect|clientWidth|offsetWidth|scrollIntoView" src/ \
  --exclude-dir=wwwroot --exclude-dir=bin --exclude-dir=obj \
  | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*|@\*)' \
  | grep -vxE "src/ExGrid/Assets/ex-grid\.js:[0-9]+:[[:space:]]*element\.scrollIntoView\(\{ block: 'nearest', inline: 'nearest' \}\);"   # PF-1: prints nothing
awk '/^[[:space:]]*(\/\/|\/\*|\*)/{next} /^    const bringIntoWindow = /{f=1} /scrollIntoView/{all++; if(f)n++} f&&/^    };$/{f=0}
  END{if(n!=1||all!=1)print "PF-1: scrollIntoView is not ADR-0204'\''s one page-follow line in bringIntoWindow"}' \
  src/ExGrid/Assets/ex-grid.js                                                     # PF-1: prints nothing
grep -rnE "using +(global::)?(ExGrid\.)?(MudBlazor|Fluxor)\b" src/ExGrid/ src/ExGrid.Data/ \
  src/ExGrid.Data.Arrow/ src/ExSheet.Engine/ src/ExSheet/ src/ExPivot.Engine/ src/ExPivot/   # PRE-4: no match
grep -rn "OrderBy\|Where(" src/ExGrid/Components/                                 # FN-15
grep -o -- "--ex-[a-z0-9-]*" src/ExGrid/Assets/ex-grid.css | sort -u              # UX-1, UX-4
grep -rni "page" src/ExGrid/Keys/ | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*)' \
  | sed -E 's/"(Shift\+)?Page(Up|Down)"//g' | grep -i "page"                       # KB-16: prints nothing
```

Read `src/ExGrid/Assets/ex-grid.js` against the entries of ADR-0021's table (PF-2), and `ExGrid.razor`
for RR-9/RR-10.

*(Restated 2026-10-11, decided with the user. PF-1's grep read all of `src/`, which on a correct tree
matches `ex-grid.min.js`'s source map and comments that say why the grid does not read layout; it
reads the sources now, comment lines left out — a line that opens with `//`, `/*`, `*` or `@*`. The
`using` grep read the core alone; it reads the seven packages that are not Wrappers (PRE-4). The token
grep read `wwwroot`, and stopped at a digit, so `--ex-reference-1-pointed` came out cut; it reads the
source, digits included. KB-16's grep is new here, with its row. PF-2 read a "five-entry table";
ADR-0021's table has grown since, and the check names no count.)*
*(2026-10-12, decided with the user: PF-1's grep allows exactly ADR-0204's page-follow line,
`element.scrollIntoView({ block: 'nearest', inline: 'nearest' })` in `bringIntoWindow` in
`src/ExGrid/Assets/ex-grid.js`, and nothing else; the `awk` beside it prints unless that line is the
file's only `scrollIntoView` and sits in `bringIntoWindow`. Any other `scrollIntoView`,
`getBoundingClientRect`, `clientWidth` or `offsetWidth` still prints.)*

### Step 4 — Layer 3, real browsers

```sh
cd tests/ExGrid.Browser
npm ci
npx playwright test 2>&1 | tee ../../verification/<date>/layer3.log
```

**On Chrome and on Edge** (ADR-0017/0026 — the `chrome` and `msedge` projects), and **on Windows or Linux at least once** — VZ-10 is a
tautology where scrollbars are overlays, and macOS alone does not discharge it.

*(2026-10-11, decided with the user,
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md):
**the rows checked by hand are run again only when the code they cover has changed.** They are VZ-14,
VZ-16's real zoom and 225%, FN-12e's measurements, ED-30's real IME, SH-42's, SH-51's and SH-52's
browser keys, KB-3 on macOS, VZ-18 on the remote-isolation PC, and §21.11's screen-reader check. Each
names the paths whose change could change its result. A row's latest record holds while those paths
are unchanged, and the release workflow checks that. The table of rows and paths is written here with
that check. Every automated command in this step runs in full, every time. A question to Excel is asked
when its feature is built, never at a release, and the corpus may hold no `uncertain` or `documented`
case without a written reason.)*

**Rows checked by hand, and the code each covers**

*(Added 2026-10-12, decided with the user: the table
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)
of 2026-10-11 asks for, written with the release workflow's check, and what that check reads, confirmed the
same day: FN-12e as two rows, a record's commit kept fetchable by a tag named for the record's directory, and
WR-10's floor run indexed beside the rows.)* Each row a person checks, where it is checked, and the paths whose
change could change its result.

- **A row's latest record holds while its paths are unchanged.** That means unchanged between the
  commit the record was made at and the commit being signed off, or, for a release candidate, the
  commit it is tagged on.
- **The release workflow compares them** (`git diff --quiet`). It refuses a version without a suffix,
  and a release candidate, when:
  - a path changed;
  - the row has no record, and is not marked a Known limitation of the version being released *(2026-10-12,
    how the release workflow reads a row put out of a version by decision: such a row is named in a notice and
    never holds; see the index below)*;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
  - the record's tag is not `verified/` followed by the name of the record's directory;
  - that commit cannot be fetched through the record's tag, or the tag names another commit, so whether
    its paths changed cannot be checked;
  - a path names nothing;
  - for WR-10, a record its entry names besides the floor record is refused as the floor record would be,
    its paths compared from its own commit; or a stable MudBlazor version within the range the Wrappers
    declare, listed on nuget.org and published before X's commit time by its registration index or given no
    date there, is not among the versions its records' layer-2 runs covered; or that index cannot be read
    *(2026-10-12, how the release workflow checks the WR-10 row's notes below)*.
- **The records are indexed in `verification/by-hand.json`.** For each row it names the same paths as
  this table, the row's latest record, the commit that record was made at, and the record's tag. *(2026-10-12,
  how the release workflow checks the WR-10 row: for WR-10 each record names besides the MudBlazor versions
  its layer-2 runs covered, and the entry names the records of later layer-2 runs, by the row's notes,
  besides the floor record.)* *(2026-10-12, how the release workflow reads a row put out of a version by
  decision: the index marks it `knownLimitation`, naming the version without a suffix, the day of the decision
  and its words, and gives it no record, and this table's row says "Known limitation of" that version. The
  mark reaches that version's release candidates and the version, and no other; the workflow refuses it on a
  row that also has a record, without its date or its words, or where this table does not say the same.)* A
  layer-1 test (`tests/ExGrid.Release.Tests`) fails when the index and the table differ, and the release
  workflow refuses a tag where they do.

**The paths are drawn wide** (ADR-0208). A row names whole files, and a directory where its behaviour
crosses several:

- the grid's script and stylesheet sources, and their minified builds, which are what a browser runs;
- the keyboard, editor and toolbar files;
- the measuring code;
- the spec a row runs;
- the DemoHost's or the Docs Site's page, only where the row reads that page.

When in doubt, the path goes in. The list is reviewed with the row, and in any change that moves the
code it names. A path is a file, a directory ending in `/`, or a pattern git reads as a glob, written
`:(glob)<pattern>`, where `*` stays within a directory and `**` crosses them: `:(glob)src/*.MudBlazor/**` is
every file below each Wrapper.

**A run that records a row** writes its record under `verification/`, in a directory named for the run, as
`verification/2026-10-11-windows-17/`, naming the commit it verified in full or by seven digits or more. It
tags that commit `verified/` followed by the name of that directory *(2026-10-12, decided with the user: the
tag is `verified/` followed by the name of the record's directory under `verification/`, and nothing else, one
tag for each record directory, whatever the platform or the run's purpose; a record whose tag is not exactly
that is refused)*: `verification/2026-10-01-windows-browser-11/keys.md` is tagged
`verified/2026-10-01-windows-browser-11`, and a later `verification/2026-11-02-macos-kb3/`,
`verified/2026-11-02-macos-kb3`. The session that made the run pushes the tag, which is never moved, so a squash
merge or a deleted branch cannot take the commit away. In the same commit as the record, it replaces the row's
record in `verification/by-hand.json`, naming the commit, the tag, the file and the verdict. The release
workflow fetches the commit through the tag.

**FN-12e is two rows.** On Windows it keeps the measurement as well as the painting while
`tests/GlyphWidths` holds no `system-ui.windows.json`, so that its MUST, the widest glyph measured on each
platform, never passes on a painting alone; once that record is committed and held by layer 1, the Windows
row narrows to the painting in the same change. On macOS, whose widths are recorded and held by layer 1, it
checks the painting.

**WR-10's floor run is indexed beside the rows**
([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)'s
notes of 2026-10-12). It is automated, but a person makes it once, not CI, and its record carries it
forward while the Wrappers and the demo pages are unchanged, so the release workflow reads its paths as a
row's and refuses a tag whose Wrappers or demo pages changed since the run.

| Row | Where | Checked by hand | The code it covers |
|---|---|---|---|
| **VZ-14** | Windows | `scrollbar.spec.mjs`'s VZ-14 block at a fractional display scale, headed, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `tests/ExGrid.Browser/fixtures.mjs`, `tests/ExGrid.Browser/scrollbar.spec.mjs` |
| **VZ-16** | Windows | A real browser zoom told to the grid as it changes, on a page that is only displayed. A thin line and a gridline in device pixels at a real 2.25: the display at 150%, the browser's zoom at 150%, on `/sheet?case=11` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Components/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetAppearance.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **FN-12e** | Windows | The widest glyph of each `CellTextMetrics` class, measured in Chrome and Edge, while the Windows widths are not recorded (no `system-ui.windows.json` in `tests/GlyphWidths`). On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **FN-12e** | macOS | On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell. The macOS widths are measured already, `tests/GlyphWidths/*.macos.json` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **ED-30** | Windows | A real IME on `/sheet` and `/sheet?chrome=mud`, on Chrome and Edge, on both hosts and behind 150 ms: the sixteenth run's cases k1 to k16, and DC-47's | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormulaAids.cs`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-42** | Windows | Ctrl+1 to Ctrl+5 reach `/sheet` before the browser's tab switching, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-51** | Windows | Ctrl+F1 reaches `/sheet`, and the toolbar hides and shows | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-52** | Windows | Alt released alone, and F10, show the KeyTips on `/sheet` under both Chromes, and the browser's menu does not open. Each letter is read beside Excel's Home tab. Nothing starts while an edit is open | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **KB-3** | macOS | Layer 3 on a Mac, where `ControlOrMeta` presses Command: Cmd+Enter fills, on every key path, the editor's included | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `src/ExSheet/SheetStructureKeys.cs`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `tests/ExGrid.Browser/` |
| **VZ-18** | the remote-isolation PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll. *(2026-10-12, decided with the user: VZ-18, the check on the remote-isolation PC, is a Known limitation of `0.1.0`. It is not run, and the 2026-10-08 observation, which names no commit, is not taken as its record.)* | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **WR-10** | Windows or Linux | Step 4's floor run: the layer-3 tests that run under a Wrapper, against a DemoHost built with the MudBlazor every Wrapper declares as its floor, on both hosts, in Chrome and in Edge; and beside it each Wrapper's layer-2 suite at every published version of its declared range. *(2026-10-12, decided with the user: a MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version only; the floor run, layer 3 at 9.6.0, holds while its paths are unchanged.)* *(2026-10-12, decided with the user: an unlisted version is not asked for; the release check reports it as out of the check because it is unlisted.)* *(2026-10-12, decided with the user: a layer-2 run at such a version is a run of its own; its record goes in its own `verification/<date>-<name>/` directory, the run tags the commit it verified `verified/` followed by that directory's name, and WR-10's entry in `verification/by-hand.json` names that record besides the floor record. The release check reads every record the entry names: the floor record's paths hold since its commit, and each later record's tag names its commit.)* | `:(glob)src/*.MudBlazor/**`, `samples/ExGrid.DemoPages/` |

The run must capture `page.on('console')` and `page.on('pageerror')` into `console.json` for
CON-1..4 and CON-8, and read `Performance.getMetrics` into `metrics.json` for MEM-2/5, DOM-5,
BIG-6/7, PF-6/7.

**MEM-5's ten-minute soak runs only when asked for** — `EXGRID_SOAK=1` (decided 2026-09-24).
An ordinary run skips it by name, and MEM-6 is read at its end. Sign-off needs one run with it
set, on each browser and on each host:

```sh
EXGRID_SOAK=1 npx playwright test memory.spec.mjs 2>&1 | tee ../../verification/<date>/soak.log
EXGRID_HOSTING=server EXGRID_SOAK=1 npx playwright test memory.spec.mjs 2>&1 \
  | tee ../../verification/<date>/soak-server.log
```

*(Widened 2026-10-11, decided with the user: it asked for the soak on each browser, on the
WebAssembly host. MEM-5 is no criterion that is WebAssembly's by definition, so it is not among
SRV-2's exceptions, and `0.1.0` claims Blazor Server (ADR-0200): it is recorded on both hosts, and
MEM-6 is read at the end of each, on the Server host from the server process.)*

A skipped soak is `not run` in `results.md`, never a pass.

**Then the same suite against the Blazor Server host** (§24):

```sh
EXGRID_HOSTING=server npx playwright test 2>&1 | tee ../../verification/<date>/layer3-server.log
EXGRID_HOSTING=server EXGRID_MEASURE=pointer npx playwright test measure.spec.mjs 2>&1 \
  | tee ../../verification/<date>/measure-server.log
```

Discharges UX-2..11/15/16, VZ-1/10, ED-2/3/4/9/11, KB-1/8/11/12/28..32, CP-4/5/6/10/14, BIG,
PST-3/5, CON-1..6/8, DOM, ST-3, FN-21, and §23's layer-3 rows — the Wrapper's specs run in the
same command, against the proof-of-concept page.
*(2026-10-12, decided with the user: §23's layer-3 rows but WR-10, which the floor run below
discharges.)*

**The Wrappers' MudBlazor floor** ([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md), WR-10).
*(Added 2026-10-12, decided with the user, when the floor was raised to 9.6.0.)* Once before the
sign-off, the layer-3 tests that run under a Wrapper run against a DemoHost built with the MudBlazor
every Wrapper declares as its floor, 9.6.0, on both hosts, in Chrome and in Edge, and at 150 % where
`chrome-150` runs some of them. Every other run stays on the version the demo pages pin. The run is made
at a commit, `C` below, from a clean tree, and its record goes in a directory of its own under
`verification/`, `R` below. The demo pages are pinned for the run only, both hosts are built from
clean, and the version each will load is read back before the run:

```sh
C=$(git rev-parse HEAD)            # the commit the run verifies
R=verification/<date>-<name>       # the record's own directory
mkdir -p "$R"
sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\19.6.0"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
rm -rf samples/ExGrid.DemoHost/bin samples/ExGrid.DemoHost/obj/Debug \
       samples/ExGrid.DemoHost.Server/bin samples/ExGrid.DemoHost.Server/obj/Debug
nix develop -c dotnet build samples/ExGrid.DemoHost
nix develop -c dotnet build samples/ExGrid.DemoHost.Server
nix develop -c dotnet build samples/ExGrid.DemoApi
# both name 9.6.0
strings -el samples/ExGrid.DemoHost/bin/Debug/net10.0/wwwroot/_framework/MudBlazor.*.wasm | grep -A1 '^FileVersion'
strings -el samples/ExGrid.DemoHost.Server/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'

cd tests/ExGrid.Browser
G="[Mm]ud|is not the grid's \(KB-35|apply the condition \(KB-31, SRV-5\)|Escape, Escape straight after an Inner Popup|a menu taking the keyboard a round trip late|with and without a Wrapper|SH-46/DC-59: the Selection's outline|the palette reaches the pane|RI-3: every page opens|inspectors\.spec\.mjs|WR-7a|DC-58: a bold number that fits"
# every project: chrome, msedge, and chrome-150's Wrapper tests
npx playwright test --grep "$G" 2>&1 | tee "../../$R/mudblazor-floor.log"
EXGRID_HOSTING=server npx playwright test --grep "$G" 2>&1 \
  | tee "../../$R/mudblazor-floor-server.log"
cd ../..
git checkout -- samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj
```

`G` is the tests that run under a Wrapper: those whose file, `describe` or title names MudBlazor or
`mud` (the `?chrome=mud` variants among them), and, by name, those that open a page under a Wrapper
without saying so. A test of the second kind joins `G` when it is written. On a machine without GNU
`strings`, each `MudBlazor` file's version is read back another way; it is never assumed.

Beside it, each Wrapper's layer-2 suite runs with MudBlazor pinned in its test project to every
published version of the declared range: each 9.x from 9.6.0 on nuget.org at the time of the run, which
the record names. The pin is the nearest reference, so NuGet takes it over the Wrapper's range, and the
version each run loaded is read back. Layer 2 sees markup and a component's state, not focus or timing:
it adds to the floor run and replaces nothing. Each version's output goes to a log of its own, which
names the commit it ran at.

```sh
versions=$(curl -s https://api.nuget.org/v3-flatcontainer/mudblazor/index.json \
  | grep -oE '"9\.[0-9]+\.[0-9]+"' | tr -d '"' | awk -F. '$2 >= 6' | sort -V)
for v in $versions; do
  for t in ExGrid.MudBlazor.Tests ExSheet.MudBlazor.Tests ExPivot.MudBlazor.Tests; do
    sed -i "s|</Project>|  <ItemGroup><PackageReference Include=\"MudBlazor\" Version=\"$v\" /></ItemGroup>\n</Project>|" \
      tests/$t/$t.csproj
    echo "== $t, MudBlazor $v, at $(git rev-parse HEAD)"
    nix develop -c dotnet test tests/$t
    strings -el tests/$t/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'   # names $v
    git checkout -- tests/$t/$t.csproj
  done 2>&1 | tee "$R/mudblazor-layer2-$v.log"
done
```

**The run tags the commit it verified.** *(2026-10-12, decided with the user: a verified commit's tag
is `verified/` followed by the name of the record's directory under `verification/`, and nothing else,
one tag per record directory.)* The record names `C` in full. In the same commit as the record, WR-10's
entry in `verification/by-hand.json` names the commit, the tag, the record's path, the verdict and the
MudBlazor versions its layer-2 runs covered, and empties `laterRecords`: a new floor run's layer-2 runs
cover every published version of the range, and earlier later records were made on the Wrappers it
replaces. The session that made the run then pushes the tag:

```sh
git add "$R" verification/by-hand.json
git commit -m "WR-10: the Wrappers' MudBlazor floor run"
git tag -a "verified/${R#verification/}" "$C" -m "The commit the Wrappers' MudBlazor floor run verified"
git push origin "verified/${R#verification/}"
```

**The floor run's record, with the layer-2 runs beside it, holds while `src/*.MudBlazor/` and
`samples/ExGrid.DemoPages/` are unchanged from the commit it was made at up to X** (ADR-0205's note of
2026-10-12, the shape of ADR-0208's rows checked by hand). It is the one automated run of this step that
a record carries forward, an exception to the note of 2026-10-11 above. *(2026-10-12, decided with the
user: a later layer-2 run's record, a run of its own, is carried forward too.)* Whether a record still holds is
read with git, from the commit its tag names, the glob spelled out so that it reaches every file below
each Wrapper:

```sh
git diff --quiet <the record's tag> X -- ':(glob)src/*.MudBlazor/**' samples/ExGrid.DemoPages/ \
  && echo "the floor record holds"
```

A change to either path asks for the floor run and the layer-2 runs again, at the floor then declared.
*(2026-10-12, decided with the user: **a MudBlazor 9.x published after the floor record and before X
asks for the layer-2 runs at that version only.** The floor run (layer 3 at 9.6.0) holds while its paths
are unchanged.)* *(2026-10-12, decided with the user: **an unlisted version is not asked for.** The
release check reports it as out of the check because it is unlisted.)*

**A layer-2 run at a later 9.x is a run of its own.** *(2026-10-12, decided with the user: its record
goes in its own `verification/<date>-<name>/` directory, and the run tags the commit it verified
`verified/` followed by that directory's name, as every run does. WR-10's entry in
`verification/by-hand.json` names that record (commit, tag, path, verdict, the version it covered)
besides the floor record. The release check reads every record WR-10's entry names: the floor record's
paths hold since its commit, and each later record's tag names its commit.)* The run is the loop above,
with `C`, `R` and `versions` set for it first:

```sh
C=$(git rev-parse HEAD)                  # the commit the run verifies
R=verification/<date>-<name>             # a directory of its own
mkdir -p "$R"
versions=<the new version>
```

In the same commit as its record, WR-10's entry names the record in `laterRecords`, and the floor record
stays in `record`; the session that made the run then pushes its tag. The later record holds, as every
record checked by hand does (ADR-0208) and as the floor record does, while `src/*.MudBlazor/` and
`samples/ExGrid.DemoPages/` are unchanged between its commit and X; a run made where they differ from X's
does not count.

```sh
git add "$R" verification/by-hand.json
git commit -m "WR-10: the Wrappers' layer-2 runs at MudBlazor $versions"
git tag -a "verified/${R#verification/}" "$C" -m "The commit the Wrappers' layer-2 runs at MudBlazor $versions verified"
git push origin "verified/${R#verification/}"
```

### Step 5 — The randomised consistency run

Run ST-1's scripted sequence with a recorded seed, at 10³ and at 10⁶ rows, and record the seed in
`results.md` whether it passed or not.

**The 10⁶ case runs only when asked for** — `EXGRID_ST1_MILLION=1` (decided 2026-09-24). It takes
about a minute, nearly all of it the reference source sorting and filtering a million rows, so
Step 1's run skips it by name, Step 2 allows that skip, and this step is the run that sets it
*(Step 1 and Step 2 named as they stand since 2026-10-11)*:

```sh
EXGRID_ST1_MILLION=1 nix develop -c dotnet tests/ExGrid.Components/bin/Debug/net10.0/ExGrid.Components.dll \
  -class "ExGrid.Components.Tests.ConsistencyTests" 2>&1 | tee verification/<date>/st1.log
```

Pass: every case passes and none is skipped.

### Step 6 — Observational numbers

Record `metrics.json` and add a `spikes/render-bench` entry (PF-8). MEM-7 is layer 2's: after
Step 1's build *(Step 2's until 2026-10-11, when the two became one run)*, read it from `nix develop -c dotnet tests/ExGrid.Components/bin/Debug/net10.0/ExGrid.Components.dll -method
"ExGrid.Components.Tests.AllocationTests.Bytes_per_scroll_frame_are_recorded" -showLiveOutput` and
copy it into `metrics.json` by hand — layer 2 runs too often to write into `verification/` itself. Compare with the previous
verification directory and **write one sentence per number that moved more than 20%** — not as a
gate, as a note.

**The other products' OBSERVATIONAL rows are recorded here too**, each as its row says:

- **SH-19** — ExSheet's recalculation time at a large Sheet: on CoreCLR, its three explicit
  measurements in a Release build (the loop below); and completion's keystroke-to-list time on a
  circuit.
- **PV-21** and **DA-17** — on CoreCLR, their explicit measurements Step 2 allows as skips, in a
  Release build (the loop below); in the browser, `measure-pivot.spec.mjs` on a published WebAssembly build without
  AOT, against a host serving one and a demo API server holding a million trades, as the spec's
  header says (`EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=<the trade export> npx playwright test
  measure-pivot.spec.mjs`):

  ```sh
  for t in ExGrid.Data.Tests ExGrid.Data.Arrow.Tests ExPivot.Engine.Tests ExSheet.Engine.Tests; do
    nix develop -c dotnet build tests/$t -c Release
    nix develop -c dotnet tests/$t/bin/Release/net10.0/$t.dll -explicit only -showLiveOutput
  done 2>&1 | tee verification/<date>/measure-coreclr.log
  ```

- **PV-48** — ticket 01's ExPivot measurement repeated, with PV-21 beside it, into a `verification/`
  record.
- **LV-15** and **LV-23** — the live-data measurements, `measure-live.spec.mjs` and the live ExPivot's
  heap over redraws, as their rows say.

*(Added 2026-10-11, decided with the user: this step named ExGrid's numbers only. `0.1.0` carries
ExSheet, ExPivot and the data packages (ADR-0200), and Step 7 asks for every OBSERVATIONAL number.)*

### Step 7 — Sign-off

`results.md` is complete when:

- every **MUST** is `pass`, or the release does not happen. A row checked by hand passes on its latest
  record while the code it covers is unchanged since (Step 4, ADR-0208);
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
  in the same change that rewrites ADR-0042. *(Restated 2026-10-11, decided with the user,
  [ADR-0201](adr/0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md) and
  [ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md):
  the surface is decided by ADR-0201 and its baseline files hold it. The refusal is not dropped but
  **replaced by a check the workflow makes**: an unsuffixed tag is refused unless its Release Notes
  name this `results.md` and the commit it was made against, and the tag differs from that commit
  only under `verification/` and `docs/releases/`. ADR-0042 is rewritten in the change on the release
  branch that adds that check.)* *(2026-10-12, decided with the user: that change goes in on `main`
  before `release/0.1` is cut, and the branch carries it from its cut.)* *(2026-10-12, decided with the
  user, ADR-0190 and ADR-0205: the sign-off is made against a release candidate, and the commit it was
  made against is that candidate's. The candidate's notes carry a Signed off block in a pending form of
  its own, saying that the sign-off is being made against it, with the blocks of the SHOULD criteria
  accepted failing and of what is out of scope, which the Docs Site's build checks, so that what the
  release's notes will say is read before its tag. The release workflow reads this `results.md` and that
  commit only on the stable tag.)*

**A criterion that could not be verified is not passed.** Write `blocked` and say why.

---

## 23. The Wrapper — `ExGrid.MudBlazor` (WR)

*(Added 2026-09-24, when the Wrapper's remaining seams were decided — ADR-0030's verification
order, ADR-0038, ADR-0039.)* §2 keeps the Wrappers out of the core's release; this section is the
Wrapper's own. *(2026-10-11, decided with the user, [ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md): `0.1.0` carries `ExGrid.MudBlazor` stable, so this
section gates the `0.1.0` tag. §2 still keeps the Wrapper out of ExGrid's *finished*, not out of the
tag.)* It does not restate the core's criteria — **every criterion above that a Chrome or a
Theme can affect must hold with the Wrapper loaded** (WR-5) — and adds only what a real design
system can fail and a stub cannot.

The scenario for the layer-3 rows is the DemoHost's **proof-of-concept page**: an ordinary
MudBlazor application — `MudLayout` with an AppBar and a Drawer, `MudTabs`, a `MudDialog`, a
`MudSelect` in the toolbar, a light/dark switch, two grids — shaped like a Consumer's app and
holding nothing of any real Consumer's domain.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **WR-1** | MUST | The filter panel is the Wrapper's own, built from MudBlazor controls inside the core's popover, and uses the whole `FilterPanelContext`: a value list with a search and a Blank entry where the column declares one and the answer arrives; the condition form where it declares that, or where the answer is TooMany; Apply and Cancel — clearing is the "clear-filter" command above it (ADR-0009/0030/0044) | Layer 2 on `MudGridChrome.FilterPanel`; Layer 3 on the proof-of-concept page | the same choices produce the same `FilterSpec` through the Wrapper's panel as through the built-in one |
| **WR-2** | MUST | A condition's operator is a `MudSelect` offering exactly `Allowed`, in the core's order; its value is a `MudTextField` for text, a `MudTextField` for a number, read in the current culture *(2026-10-12, decided with the user: a number's control is a `MudTextField` read in the current culture, as ticket 96 made it. The row said a `MudNumericField`, which reopened `1234.5` as `1234,5` under de-DE and read it back as 12345; ticket 96 reads the text through `FilterPanelChoices.ReadOperand` in the culture the form shows numbers in, refusing by name a text that reads two ways, and both Chromes read it so. `MudFilterPanelTests` pins the `MudTextField`.)*, an editable `MudDatePicker` for a date — a `MudTextField` for a `DateTimeOffset` column *(amended 2026-10-02, ticket 98: a calendar's day has no offset, which such a column refuses, ADR-0023)* — and a `MudSelect` of true / false **with nothing chosen at first** for a boolean; Apply is unavailable until the operator's value is given (ADR-0009/0039) | Layer 2 per column type | the controls and the gating as stated |
| **WR-3** | MUST | The wording is MudBlazor's localised text wherever MudBlazor has a key, and the Chrome's own label function — English by default — for the rest; the Blank entry and its operator are the Chrome's own words and never say "empty" (CONTEXT.md **Blank**) | Layer 2 with a non-English `MudLocalizer` registered | the keyed words change; the Blank wording is the Chrome's |
| **WR-4** | MUST | The column menu and the Context Menu list exactly the core's commands, in its order, each with its `Enabled` state, as `MudButton`s with `role="menuitem"`; each core command id has a Material icon, and a Consumer's command has none unless the Chrome's icon function supplies one; a key on an item is answered through the context's `ResolveKey`, never against the Wrapper's own idea of the current item (ADR-0010/0036/0039) | Layer 2 | items, order, state, roles and icons as stated; the keys follow `ResolveKey` |
| **WR-5** | MUST | Behaviour is the core's: every layer-3 test of KB-17, KB-28..32, FN-21, A11Y-19, UX-11, CTX-1..4 and FD-3/5/6 passes with `MudGridChrome` exactly as it does with the built-in Chrome (FN-17, ADR-0010) | Layer 3, each such test run under both Chromes | identical outcomes |
| **WR-6** | MUST | `Striped` on `MudExGridPaper` turns Row Stripes on as a default the grid's own `StripeRows` beats, and the stripe takes the palette's table-stripe colour in both schemes; a scheme switch re-renders no row (ADR-0030/0038, RR-1) | Layer 2 for the cascade; Layer 3 for the colour and the render count | as stated |
| **WR-7** | MUST | The proof-of-concept page holds: a grid in a tab that was hidden paints correctly once shown; a Drawer toggle resizes a `Stretch` grid and its geometry follows; a grid in a `MudDialog` opens its popovers whole — inside the grid's box, never cut by the dialog — and its Inner Popups above the dialog; the toolbar's `MudSelect` and a grid panel's never interfere; the two grids stay independent (ADR-0018/0028/0030/0039) | Layer 3 | every clause observed |
| **WR-7a** | MUST | Inside `MudExGridPaper` a Stretch-height grid takes the paper's height below the toolbar; a declared-height grid lays out as before (ADR-0030, 2026-09-26) | Layer 3 on the MudBlazor pages | the grid's bottom meets the paper's content bottom; the declared grids' boxes are unchanged |
| **WR-8** | MUST | The Wrapper adds no script: no `.js` in the package and no interop call of its own; what MudBlazor's components run for themselves is MudBlazor's (ADR-0021 as narrowed by ADR-0039) | inspect the package; grep for `IJSRuntime`, `IJSObjectReference`, `.js` | none |
| **WR-9** | MUST | The Wrapper's own suites hold the core's console rules: zero errors, zero page errors, zero warnings from ExGrid's or the Wrapper's code, no unhandled exception (CON-1/2/3/6) | the shared layer-3 fixture; layer 2's unobserved-exception handler | as the core's |
| **WR-10** | MUST | Each MudBlazor Wrapper works at the floor it declares, 9.6.0, and across its declared range: the layer-3 tests run under `ExGrid.MudBlazor`, `ExSheet.MudBlazor` and `ExPivot.MudBlazor` pass against a DemoHost built with the floor's MudBlazor, on both hosts, in Chrome and in Edge; and each Wrapper's layer-2 suite passes with MudBlazor pinned to every published version of its declared range ([ADR-0205](adr/0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md)). *(Added 2026-10-12, decided with the user, when the floor was raised to 9.6.0: the Wrappers' layer-3 tests, run that day on Playwright's Chromium against a DemoHost at 9.0.0, failed on both hosts, and bisected over 9.0.0 to 9.9.0, the lowest version at which they passed, but for two failures that are not MudBlazor's, was 9.6.0. That record is not this row's floor run.)* | Step 4's floor run and its layer-2 runs, recorded under `verification/`. The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from the commit it was made at up to X (ADR-0205's note of 2026-10-12, the shape of ADR-0208). *(2026-10-12, decided with the user: a MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version only. The floor run (layer 3 at 9.6.0) holds while its paths are unchanged.)* *(2026-10-12, decided with the user: an unlisted version is not asked for; the release check reports it as out of the check because it is unlisted.)* *(2026-10-12, decided with the user: a layer-2 run at such a version is a run of its own, whose record goes in its own `verification/<date>-<name>/` directory, and the run tags the commit it verified `verified/` followed by that directory's name; WR-10's entry in `verification/by-hand.json` names that record besides the floor record; the release check reads every record the entry names.)* Between floor runs, Step 1 holds the floor on every run: the Wrappers' suites resolve 9.6.0, and their WR-10 tests pin the role and name on the focusable element of every MudSelect a Wrapper draws, and a refused operand's words | zero failures; a layer-3 failure that also fails at the demo pages' version is that test's, and is judged by the run at that version |

---

## 24. Blazor Server (SRV)

*(Added 2026-09-25, when the grid was first run under Blazor Server — ADR-0005, ADR-0010,
ADR-0018, ADR-0019, ADR-0021.)* The criteria above are the grid's on any host. This section says
how they are run on the second host, which of them are WebAssembly's by definition, and adds what
only a circuit can fail. **The claim that ExGrid runs on Blazor Server is made by an ADR, and only
once every MUST here passes**; until then the Server host is a fixture, not a promise.
*(2026-10-11, [ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md): `0.1.0` makes the claim, so this section gates it, and the declaration is written
before the tag.)*

The host is `samples/ExGrid.DemoHost.Server`: the same pages as the WebAssembly DemoHost, from
`samples/ExGrid.DemoPages`, in `InteractiveServer` render mode with prerendering on. Layer 3 is
pointed at it with `EXGRID_HOSTING=server`.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SRV-1** | MUST | Both hosts serve the same pages from `ExGrid.DemoPages`; the Server host runs `InteractiveServer` with prerendering on, and neither host references the other (ADR-0019) | inspect the three projects | one set of pages; the render mode and prerender as stated; no project reference between hosts |
| **SRV-2** | MUST | Every layer-3 test passes against the Server host, on both browsers, with the same outcome as on WebAssembly — except where a criterion is WebAssembly's by definition, and those are only: **CP-6**'s "the `copy` event route" (on Server every copy takes the asynchronous route, ADR-0005; its no-prompt property is asserted instead); the host counters behind **MEM-6** (read from the server process, as MEM-6 says); and the two measurements whose rows define them in the browser, on a published build without AOT, which on a circuit would time the server's .NET instead: `measure-live.spec.mjs`, **LV-15**'s in-process half, and `measure-pivot.spec.mjs`, **PV-21**'s and **DA-17**'s browser half; and **ERR-10**'s stale script, which a host that pins the script by integrity, as the published Server host does, refuses in the browser before the grid's own stamp can: `shipped-files.spec.mjs` asserts the grid's refusal there through its integrity-pinned test, and through the stamp's tests on a host that does not pin. *(Restated 2026-10-11, decided with the user, [ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md). It named two exceptions and held every test skipped on Server to them, which the tree failed: the two measurements skipped there unnamed, and so did DC-47's per-keystroke colours in `reference-text.spec.mjs` and the test named RI-22 in `inspector-edits.spec.mjs`. The measurements are named, being WebAssembly's by their rows' own words; the other two are to run on the Server host instead. MEM-5's soak is no exception and is recorded on both hosts (§22 Step 4).)* *(2026-10-13, decided with the user: ERR-10's pair is the fifth exception, named once it was checked against hosts published in Release: on WebAssembly, which pins no script, the stamp's two tests run and the integrity-pinned one skips; on the Server host the reverse. The integrity-pinned test asserts what the stamp's do — the refusal shown and announced once per grid, nothing attached, never busy — for a body the browser refuses first.)* | `EXGRID_HOSTING=server npx playwright test`, §22 Step 4; CI's Server-host layer-3 job (ADR-0041) | all pass; a test that runs on WebAssembly and skips on the Server host names one of these five exceptions. A test that skips on both hosts — VZ-14's run on a Windows desktop, an ordering guard whose earlier test did not run, a `test.fixme` — is judged by its own row's criterion, not by this one |
| **SRV-3** | MUST | Two users, one store: on the `/shared` page, a change to the Consumer's store reaches both circuits' grids; a sort in one leaves the other's order and selection untouched; and one bundled Grid Source attached from both circuits is refused by name (ADR-0018) | Layer 3, two browser contexts, Server host only | as stated |
| **SRV-4** | MUST | The console rules hold on the Server host, reading CON-6 from the Server host's own output (CON-1..8) | the shared fixture during SRV-2's run | as the core's |
| **SRV-5** | MUST | Keys, paste and the clipboard hold under a real round trip: ED-22, KB-33..35, CP-21 and CP-23 pass with 150 ms injected between the browser and the Server host (ADR-0005/0010/0039). *(Widened 2026-10-11, [ADR-0204](adr/0204-the-page-follows-what-a-move-reveals-and-a-scripted-focus-asks-whether-the-keyboard-is-still-here.md): and KB-45's last clause — a popover's opening focus, under either Chrome, and a Template cell's focus through `HandKeyboardToAsync` are not taken once the keyboard has moved to another grid during the round trip.)* | Layer 3 through the loopback latency proxy | as those criteria; the second grid keeps the keyboard |
| **SRV-6** | OBSERVATIONAL | What the pointer reports cost on a circuit: calls crossing per second during a sweep while scrolling, and the lag from crossing a row to the band repainting, at 0, 50 and 150 ms round trip (ADR-0021's owed number) | `EXGRID_MEASURE=pointer`, written to `metrics.json` | recorded per round trip; if the cost is bad, ADR-0021's answer is the switch each report already has |
| **SRV-7** | MUST | Text typed at full speed into the grid's own fields — the Cell Editor, the filter panel's fields, the find field — arrives whole on a circuit: no render writes the server's older copy back over what the user has typed since (§24, 2026-09-27; the trap is in CLAUDE.md) | Layer 3 on the Server host with a 50 ms round trip | the field holds every character typed, after every round trip has landed |

---

## 25. Row Marks (MK)

*(Added 2026-09-26, with
[ADR-0043](adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md).)* The Mark
Column and the Row Marks it shows. The marks are the Consumer's; these criteria hold the core to
what it decides — what a gesture means and what it paints — and hold the bundled Grid Sources to
the Consumer's half of the contract, since they are the reference a Consumer copies. The public
shape of the notification and of the per-row question is still to be implemented; nothing here
depends on which shape it takes.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **MK-1** | MUST | Space with the Focus in the Mark Column brings every row with a selected cell in that column into line: all marked if any was unmarked, all unmarked only if all were marked; with the Focus elsewhere Space keeps its ADR-0020 meaning (ADR-0043) | Layer 1, over all-marked, none-marked and mixed selections, and with the Focus on a data cell | as stated; a mixed selection never comes out mixed |
| **MK-2** | MUST | The header's three states come from the counts the Consumer answers — marked Detail rows in the current result against Detail rows in it — never from the Window; Group and Total rows carry no checkbox and are neither marked nor counted (ADR-0043/0024) | Layer 1 for the state; Layer 2 with a Window of fully marked rows inside a larger, partly marked result, and with Group and Total rows in the Window | the header shows "some", not "all"; no checkbox is painted on a Group or Total row |
| **MK-3** | MUST | A Space, a header press and one checkbox each raise **one** notification; Space's carries the rectangles and the Row Sequence Version they were made under, and the bundled Sources refuse to resolve it under a different version (ADR-0043/0011/0014) | Layer 2, counting invocations over a 10⁶-row selection; Layer 1 on the Sources with a bumped version | one invocation each; the stale intent marks nothing |
| **MK-4** | MUST | A changed mark repaints only the rows whose mark changed; every other row still skips its render (ADR-0043/0003) | Layer 2, `RenderCount` per row around one checkbox press | one row re-rendered; the rest unchanged |
| **MK-5** | MUST | Marks survive a sort: the same rows are marked after the Row Sequence Version changes, at their new positions (ADR-0043) | Layer 2 through `GridSource.From` | the marked rows by identity are unchanged; the selection is dropped as ADR-0011 says, the marks are not |
| **MK-6** | MUST | After "mark all", a row scrolled into view for the first time is painted marked — at 10⁶ rows, through `GridSource.Fetch`, far from where the header was pressed (ADR-0043) | Layer 3, on both browsers | every painted Detail row in the new Viewport shows its checkbox marked |
| **MK-7** | MUST | Marks outside the current filter are counted aloud: after marking and narrowing the filter, the count display names how many marks lie outside it (ADR-0043/0014) | Layer 3 | the display reads the total and the outside count; widening the filter back shows the same rows marked |
| **MK-8** | MUST | "All" is the result as it stood when the header was pressed: a row that arrives afterwards is not marked, and the header turns to "some" (ADR-0043) | Layer 2, pushing a Window with a new row after the press | the new row answers unmarked; the header shows "some" |
| **MK-9** | MUST | A press on a mark is judged against the paint it was made on, and what it can still name is honoured: a row's checkbox marks its row by identity across an order move; under a pager, the header's checkbox on a page turned since marks the page it was pressed on, as `Positions` under the order it was pressed in; a told press on the header's checkbox marks what it named in the mode in force at the press — the page it was pressed on, with the rows that page held, though the pager has since been removed or resized; with no pager, the whole result, though a pager has since been added — and a press told no paint names what the header names now. A press made on what a replaced Source painted, a row's checkbox included, marks nothing and raises `OnMarkRefused(MarkRefusalReason.SourceChanged)`; the header's checkbox and "Mark all N rows" under an order moved since mark nothing and raise `OnMarkRefused(MarkRefusalReason.OrderMoved)` — once per press, with no Row Mark intent. A row's checkbox whose click never comes is answered by the grid, and a told press never reaches the next. A press told the current paint, or told none, marks as before (ADR-0043's note, rewritten 2026-10-09 with the user; ADR-0142; ADR-0021) | Layer 2, with and without a Row Key, under a pager and not, through `GridSource.From` for a turned page, a pager added, removed and resized between a press and its click, a click that never comes | as stated |

---

## 26. Consumer declarations (DC)

*(Added 2026-09-27, with [ADR-0050](adr/0050-what-exsheet-asks-of-exgrids-core.md) and
[ADR-0051](adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md).)* What ExSheet
asks of the core, as ExGrid features any Consumer may declare — and, since 2026-09-30, what ExPivot
asks of it (DC-61 to DC-66: ADR-0063, ADR-0068 and ADR-0070). **Every one gates the release**
(§2). The first criterion is the one that makes the rest safe to ship: a Consumer who declares
nothing sees nothing change.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **DC-1** | MUST | With no declaration made, the grid behaves exactly as before ADR-0050/0051: every existing criterion of §3–§25 still passes, and no new band, handle or heading is painted (ADR-0050/0051) | the full suite, all three layers; Layer 2 grep of the default markup | green; no `ex-formula-bar`, `ex-row-headings` or `ex-fill-handle` element |
| **DC-2** | MUST | Declared, a plain click on a column header selects the whole column, Shift+click extends from the Focus's column and the Focus stays, and nothing sorts (ADR-0050, item 1; ADR-0052) | Layer 2 | the Selection spans every row of the column(s); `OnSortChanged` is not invoked |
| **DC-3** | MUST | Row Headings are painted beside every painted row with the Consumer's label; a click selects the whole row with the Focus in the first column on screen, and Shift+click extends with the Focus staying; the corner selects all with the Focus on the top-left cell on screen, and nothing scrolls (ADR-0050/0052) | Layer 2 + Layer 3 | as stated |
| **DC-4** | MUST | Row Headings are outside the column index space: they are never in the Selection, a copy, Ctrl+A or the Enter/Tab cycle (ADR-0050/0011/0012) | Layer 1 + Layer 2 | Ctrl+A then copy yields no heading label; Tab never lands on a heading |
| **DC-5** | MUST | Either Heading can be hidden; hiding the column header removes the header band and the geometry follows (ADR-0050/0028) | Layer 2 | no header band; the first row starts at the Viewport's top |
| **DC-6** | MUST | The Row Headings' width is resolved geometry emitted inline, with no stylesheet literal paired with a C# constant (ADR-0027/0028) | Layer 2 + grep of the stylesheets' sources, `src/*/Assets/*.css` *(restated 2026-10-11, decided with the user: it read `wwwroot`, which since ADR-0123 holds only the minified builds; §22)* | inline width present; no literal |
| **DC-7** | MUST | With an edge answer supplied, Ctrl+arrow moves the Focus to the answer and Ctrl+Shift+arrow extends the range to it; without one, both go to the grid's edge as ADR-0012 says (ADR-0050, item 2) | Layer 2 with a stub answer | as stated |
| **DC-8** | MUST | With spilling declared, an m×n source pasted onto one cell writes the m×n block from that cell, as one paste notification, and the block becomes the Selection with the Focus at its top-left, and nothing scrolls to show it (ADR-0050, item 3; ADR-0052) | Layer 1 for the shape; Layer 2 for the Selection | one notification; the Selection equals the block |
| **DC-9** | MUST | Without the declaration, range → one cell is refused as ADR-0014 says (ADR-0014/0050) | Layer 1 | the existing refusal reason |
| **DC-10** | MUST | A spill past the grid's extent is refused by name, and `Editable` is checked on the spilled block before anything is written (ADR-0050/0035) | Layer 1 | distinct refusal reasons; nothing written |
| **DC-11** | MUST | A Consumer's request to place the Selection and the Focus places them, scrolls the Focus into view and announces the Selection once; a request made under an older Row Sequence Version is dropped (ADR-0050, item 4; ADR-0011/0033) | Layer 2 | as stated; the stale request changes nothing |
| **DC-12** | MUST | With the fill handle declared, it is painted at the bottom-right of the Selection's last range, in the selection overlay; for a range spanning every row (whole columns) at its first row's top-right corner, and for one spanning every column (whole rows) at its first column's bottom-left, beside the Heading, held at the readable area's edge while that row or column is scrolled away; a range spanning both, and a disjoint Selection, show none (ADR-0050, item 5 and "The handle on a whole axis"; ADR-0008) | Layer 2 + Layer 3 | one handle element, positioned by the overlay at the corner the range's shape names; none for every cell or a disjoint Selection; on `/sheet`, a whole column's and a whole row's handle painted beside the Heading and uncovered, and a whole column's at the top of the part in view with row 1 scrolled away |
| **DC-13** | MUST | Dragging the handle extends along one axis only, paints the target outline, and on release raises exactly one Fill Intent carrying source, target and direction; the grid writes nothing (ADR-0050) | Layer 2 for the intent; Layer 3 for the drag | one intent; no Edit Intent |
| **DC-14** | MUST | A Fill Intent whose target covers a column that is not `Editable` is refused whole before it is raised (ADR-0050/0035) | Layer 1 + Layer 2 | refusal reason; no intent |
| **DC-15** | MUST | The drag, the header click, the Headings and the edge answer add no JavaScript (ADR-0021) | inspect `src/ExGrid/Assets/ex-grid.js` against ADR-0021's list *(restated 2026-10-11, decided with the user: it read `wwwroot`, which since ADR-0123 holds only the minified build; §22)* | the allowlisted uses only |
| **DC-16** | MUST | With an opening text supplied, the Cell Editor opens on it (Caret and Overwrite alike keep their meanings); without it, on the value (ADR-0051) | Layer 2 | as stated |
| **DC-17** | MUST | With text reporting on, the editor's text and caret reach the Consumer as the user types; its candidates are painted by Chrome as the editor's Inner Popup, inside the grid's box; ↑/↓ choose, Tab accepts, Escape closes the list and leaves the edit open (ADR-0051/0039/0040) | Layer 2 under the built-in Chrome and `ExGrid.MudBlazor`'s; Layer 3 for the box | as stated under both Chromes |
| **DC-18** | MUST | A candidate list or hint that answers text which has since changed is never shown (ADR-0051) | Layer 2 with a delayed answer | the stale list is dropped |
| **DC-19** | MUST | With a Point predicate supplied, while it answers true for the text and caret the key carries, arrows and clicks move a pointing outline painted in the selection overlay and the Consumer's Reference text is written at the caret; Shift extends; F2 toggles with Caret; the Selection and the Focus do not move (ADR-0051) | Layer 2; Layer 3 by real keys and mouse | as stated |
| **DC-20** | MUST | Point is decided from the text and caret the key message carries, never from an earlier answer: typed quickly on a Server circuit with 150 ms injected, no arrow points where the text forbids it (ADR-0051, SRV-5's proxy) | Layer 3 on the Server host | no pointing after a non-operator character |
| **DC-21** | MUST | Declared, the Formula Bar is a band inside the root above the header, showing the Name Box's label and the Focus cell's text; on a display grid that text is the full value behind a `####` (ADR-0051/0016) | Layer 2 | as stated |
| **DC-22** | MUST | The Formula Bar is the Cell Editor's second surface: typing in either updates both, a commit from either raises one Edit Intent, Escape from either cancels once, and keys typed in it reach the root's capture listener (ADR-0051/0007/0018) | Layer 2 + Layer 3 | one intent; both surfaces agree after every keystroke |
| **DC-23** | MUST | The Formula Bar's height is Grid Metrics geometry and the rows take what it leaves; switched off, the geometry is as before (ADR-0028) | Layer 2 | the Viewport arithmetic includes the band exactly |
| **DC-24** | MUST | The JavaScript for ADR-0051 stays inside the existing module and its allowlisted keyboard/editor listener, and reads no layout. It covers the key message's text and caret; the gate sets for pointing and for an open completion list; the caret reported with each input, on `selectionchange`, and once when an edit opens; and the caret set after the core rewrites the text *(amended 2026-09-27 with ADR-0051's second and third rounds)* (ADR-0021/0051) | inspect `src/ExGrid/Assets/ex-grid.js` *(restated 2026-10-11, decided with the user: it read `wwwroot`, the minified build since ADR-0123; §22)*; the script-shape tests | nothing outside those; no layout read |
| **DC-26** | MUST | With a per-cell kind supplied, alignment, the numeric class, `####` and the default format follow the cell's kind; without it, the column's (ADR-0050, item 6) | Layer 1 + Layer 2 | a text cell in a Number column is left-aligned and never `####`; a number in a Text column is right-aligned and becomes `####` when it does not fit |
| **DC-27** | MUST | After a Fill Intent the Consumer accepts, the Selection is source and target together with the Focus unmoved in the source; after one it refuses, it stays on the source (ADR-0050, item 5; ADR-0052) | Layer 2 | as stated |
| **DC-28** | MUST | Keys held during an editing-mode round trip are typed into the surface holding DOM focus — the Formula Bar when the user was typing there (ADR-0051) | Layer 3 on the Server host with 150 ms injected: F2 then typing into the bar at once | every character lands in the bar's text, in order |
| **DC-29** | MUST | With a per-cell alignment supplied, a cell is painted with it; without it, the column's and then the kind's (ADR-0050, item 7) | Layer 2 | as stated |
| **DC-30** | MUST | With undo and redo declared, Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z raise them while no edit is open, and stay the editor's own while one is; without the declaration they stay the browser's (ADR-0050, item 8; ADR-0007) *(since 2026-09-28 ADR-0007's forwarding, KB-39, is the one definition; this row holds ExSheet's use of it)* | Layer 2 + Layer 3 | as stated |
| **DC-31** | MUST | While pointing, Shift+arrows extend the outline; while the completion list is open, ← and → move the caret and only ↑/↓/Tab/Escape are claimed; the caret is reported with each input and whenever it moves (`selectionchange`), placed when an edit opens and after the core rewrites the text (ADR-0051) | Layer 2 for the C# side; Layer 3 with real keys, including repeated letters (`=SS` then completion) | the replaced span is the one at the caret; the caret sits after the inserted text |
| **DC-32** | MUST | With a copy answer declared, both copy routes ask it with the range, write what it returns, or refuse with the Consumer's sentence; without it, ADR-0005's copy is unchanged (ADR-0050, item 9) | Layer 2 | as stated |
| **DC-33** | MUST | The paste notification marks each field as an invariant value (a number, an ISO date, or TRUE/FALSE) or shown text; Excel's `x:num` and ExGrid's own HTML (marked `data-ex-grid="invariant"`) are invariant (ADR-0050, item 10) | Layer 1 over the clipboard parser; Layer 3 with a real Excel copy | as stated |
| **DC-34** | MUST | A click into the Formula Bar's text keeps the caret where it was clicked; moving the caret while pointing enters Caret (ADR-0051, third round) | Layer 2 + Layer 3 | as stated |
| **DC-35** | MUST | With a painted text supplied, the grid paints it and, where it differs from the value's text, gives the value's text as the accessible name (ADR-0050, item 11) | Layer 2 | as stated |
| **DC-36** | MUST | Resize grips can be declared without the column-menu button; double-clicking an edge still sizes to fit (ADR-0050, item 12) | Layer 2 + Layer 3 | no menu button; grips work |
| **DC-37** | MUST | With copy with headers left out, the Context Menu offers only Copy on every route, and no other route copies with headers; without the declaration, unchanged (ADR-0050, item 13) | Layer 2 | as stated |
| **DC-38** | MUST | A spilled paste the Consumer refuses (`GridPasteIntent.Refuse()`) writes nothing and leaves the Selection where it was (ADR-0050, item 3) | Layer 2 | the Selection is unchanged; no block is selected |
| **DC-39** | MUST | With a size label supplied (`NameBoxSizeLabel`), while a selecting drag's button is down over more than one cell the Name Box shows it (`4R x 3C` on ExSheet), and names the Focus again on release; keyboard extension shows no size (ADR-0052/0051/0021) | Layer 2 + Layer 3 | as stated |
| **DC-40** | MUST | A fill key's paste intent names the range it read (`GridPasteIntent.FillSource`): Ctrl+D's top row, Ctrl+R's left column, or the row above / column to the left of a range one cell deep; a paste from the clipboard and Ctrl+Enter name none (ADR-0050, item 5, 2026-09-28; ADR-0035) | Layer 2 | as stated |
| **DC-41** | MUST | A Ctrl+Enter fill's paste intent names the cell the editor was open on (`GridPasteIntent.EnteredAt`); a paste from the clipboard and a fill key name none (ADR-0050, item 5, 2026-09-28) | Layer 2 | a one-field clipboard paste and a Ctrl+Enter over the same Selection differ only in `EnteredAt` |
| **DC-42** | MUST | Declared, a press on a Column Heading or a Row Heading selects at once, and a drag extends whole columns (rows) to the one under the pointer, including while the pointer is over the cells; Shift+press extends from the Focus's column (row) and the drag goes on; the Focus stays where the press put it; Ctrl+click and Ctrl+drag on either Heading add or take out as SR-2e says (ADR-0050, item 1, 2026-09-29; ADR-0052) | Layer 2 + Layer 3 | as stated |
| **DC-43** | MUST | During a Heading drag the edge band auto-scrolls along the Heading's axis only: sideways for Column Headings, up and down for Row Headings (ADR-0050, item 1, 2026-09-29; ADR-0008) | Layer 2 with `FakeTimeProvider` + Layer 3 | the other axis's offset never changes |
| **DC-44** | MUST | With a size label supplied (`NameBoxSizeLabel`), while a Heading drag covers more than one column or row the Size Tip shows the label at the Extent's Heading, inside the grid's box, and the Name Box is empty; over one column or row, or without a label, there is no Size Tip and the Name Box names the Focus. The Size Tip is redrawn only when the Extent moves to another Heading, and adds no JavaScript (ADR-0052, 2026-09-29; ADR-0021/0029/0040) | Layer 2 + Layer 3 | as stated; renders counted per Heading crossed |
| **DC-45** | MUST | With a Reference cycling function declared, F4 while an edit is open, in the Cell Editor or the Formula Bar, sends the text and selection its key message carries, writes back the text the function answers and places the selection it answers — as a selection where the key carried a selection or a caret the user moved in that text, and as a caret at its end where typing left the caret (ADR-0051, 2026-10-12); while pointing it is asked over the whole Reference the outline wrote, rewrites it, and pointing goes on with the caret at its end. Without the function, or with no edit open, F4 is not claimed. No JavaScript use is added (ADR-0051, 2026-09-29; ADR-0021) | Layer 2 + Layer 3 on the Server host with 150 ms injected | as stated; four F4 presses in a burst give the four forms in order |
| **DC-46** | MUST | With a References function declared, while a Formula edit is open (in Overwrite, Caret or Point; opened by typing, F2, a double click or a press into the Formula Bar), each span the function answers is given a colour: spans naming the same cells, or carrying the same key, share one, handed out in order of first appearance round a palette of seven. Each Reference is outlined in the selection overlay in its colour, one element per Reference in each layer it crosses, as a selected range is (ADR-0008), and never per cell, cut to the painted rows; References naming the same cells share the colour and lay their fills together. Point's outline is the outline of the Reference it writes, with dashes in the Focus outline's colour over it (ADR-0057, the eighth Windows run). A commit or a cancel removes every outline. Without the function nothing changes (ADR-0057; DC-1) | Layer 2; Layer 3 on both hosts | as stated; no element per cell |
| **DC-47** | MUST | The coloured text shows only in the surface the edit is in (the other surface's text stays plain, as the eighth Windows run observed of Excel), and only while it is the field's text. On every editor surface (the Cell Editor and the Formula Bar, under the built-in and the MudBlazor Chrome), whenever the field's own text is transparent, the layer's text equals the field's value. While the field is ahead (typed past on a circuit, or composing), the field's own uncoloured text shows. The caret and any selected text stay visible throughout (ADR-0057) | Layer 3 on the Server host with 150 ms injected: a burst of typing, sampled every animation frame; an IME composition through CDP | no frame with transparent field text over a layer that differs |
| **DC-48** | MUST | The layer scrolls with its field: in a Formula longer than the field, after the caret moves to either end, the layer's horizontal offset equals the field's, and the layer's font, size, padding and letter spacing compute equal to the field's (ADR-0057) | Layer 3 under both Chromes | equal |
| **DC-49** | MUST | With the colour each key was given told to the Consumer: one notification when the set of keys and colours changes, none when it does not, and an empty set when the edit ends (ADR-0057) | Layer 2 | as stated |
| **DC-50** | MUST | Columns asked for: given a list of columns, each with a colour, the grid outlines each column's body across all its rows in that colour, in the selection overlay; an empty list outlines nothing; without the list nothing changes (ADR-0057) | Layer 2; Layer 3 | as stated |
| **DC-51** | MUST | The JavaScript for ADR-0057 stays inside the editor listener and the scroll offsets: comparing the layer's text with the field's value and setting one class, a `MutationObserver` on one attribute of the layer, and the layer's `scrollLeft`. Since 2026-10-01 it also builds one `Range` per Reference over the layer's one text node, from data the core renders, and adds it to highlights named for its own instance, cleared when the layer hides; the `MutationObserver` then watches two attributes of the layer, its text and its colour stretches (ADR-0057, ticket 86). It reads no layout (ADR-0021/0057) | inspect `src/ExGrid/Assets/ex-grid.js` *(restated 2026-10-11, decided with the user: it read `wwwroot`, the minified build since ADR-0123; §22)*; the script-shape tests | nothing outside those; no layout read |
| **DC-52** | MUST | Pointed at from outside: while a Consumer declares the grid pointed at, a primary press on its rows or its column headers moves neither DOM focus nor the Selection and the Focus, and runs no sort, column menu, reorder or Heading drag. The press is handed to the Consumer as the pressed cell (its row's identity and its column), the pressed column header, or a shape the Consumer will refuse (more than one cell by Shift+press or a drag, a Header Group's rectangle). `ex-pointed-at` joins the root, and the pointer over the rows and headers is `cell`. Without the declaration nothing changes (ADR-0058; DC-1) | Layer 2; Layer 3 on both hosts | `document.activeElement` and the Selection unchanged by the press; no sort raised; one hand-over per press |
| **DC-53** | MUST | Dashes when asked: given a cell (a row's identity and a column) or a column, the grid draws one `ex-point-dashes` element over it in the selection overlay, in `--ex-focus-outline`, cut to the painted rows. A row that is not painted draws nothing, and the grid does not scroll. After a reorder the dashes are over the same row. An empty request draws nothing (ADR-0058; ADR-0057) | Layer 2; Layer 3 | as stated; no element per cell |
| **DC-54** | MUST | A press handed on keeps its place among the pointing grid's keys: the pressed grid's capture-phase `mousedown` dispatches one event on the root its render names; keys typed before it that the pointing grid still holds reach its core before the press is answered, and the keys typed after it are held until the press is answered, then replayed in order. The script keeps no state shared between instances, adds no listener on `document` or `window`, and reads no layout (ADR-0058; ADR-0021, note of 2026-09-30; ADR-0010) | Layer 3 on `/sheet`, Server host, 150 ms injected: `=`, then, once the positions grid is pointed at, a press on a positions cell and `*` at once (a press within the round trip after `=` is SH-35's ordinary press; corrected after Part B of the ninth run); `=SUM(1,`, a press, then `)` and Enter at once; `=-(` typed and a press at once, at 0 ms and at 150 ms; on `/pointing` at 150 ms, a press and Shift+↓ at once (found on CI, 2026-10-01). Inspect `src/ExGrid/Assets/ex-grid.js` *(restated 2026-10-11, decided with the user: it read `wwwroot`, the minified build since ADR-0123; §22)*; the script-shape tests | `=XLOOKUP("R-4471", Positions[Id], Positions[PV])*`; `=SUM(1,XLOOKUP(…))` committed, never `=SUM(1,)`; `=-(XLOOKUP(…)`, never `=XLOOKUP(…)` ahead of `-(`, nor `=-(` alone; the Shift+↓ refused as SH-35 refuses it, the reason shown; nothing outside the stated script. *(Corrected 2026-10-11, decided with the user: it typed `=1+`, whose `=1` is no place for a Reference, so on a circuit the positions grid could be painted otherwise for a few milliseconds between the held keys and a press made then was SH-35's ordinary press, as ADR-0058's "On a circuit" decides. CI hit that ordering once. Every prefix of `=-(` points, so every ordering of the press among the held keys has one answer. `=1+`'s ordinary press stays SH-35's.)* |
| **DC-55** | MUST | While pointed at, the grid answers the Consumer's request for the cell one step from a cell it names: up or down by one row in its current order, or left or right to the nearest of the columns the Consumer names; no cell at an edge; a row that has not arrived answered as such. Asked from a column, it answers down with the column's first row and left or right with the nearest named column, as a column; up is an edge (Part B of the ninth run). Asked to, it scrolls a cell into view as Point scrolls to its pointed cell, and a column into view across only, leaving the vertical offset as it is; a column whole in view or pinned moves nothing (2026-10-01). No JavaScript is added (ADR-0058, the ninth Windows run; ADR-0021) | Layer 2; Layer 3, `/pointing?narrow`: `=`, a press on Id's header, → | as stated; PV whole inside the client area, `scrollTop` unchanged |
| **DC-56** | MUST | The Reference Point is writing lies on the grey ground unless it follows the Formula's `=` directly, and its text wears its colour's pointed shade: `--ex-reference-N-pointed`, whose light defaults are Excel's for all seven places (`#0401a2`, `#630101`, `#44007c`, `#003600`, `#550059`, `#531c00`, `#00323f`) and over a dark ground the colour mixed toward white (ADR-0051, ADR-0057, Part B of the eighth Windows run, the tenth run) | Layer 3 under both Chromes: `=D11+`, ↓↓ in the cell, and `=SUM(`, ↓, ↓ | the text's colour `#0401a2` after one Reference, `#630101` for the second |
| **DC-57** | MUST | Declared keys (ADR-0050, item 14): each declared key is claimed and raised with whether an edit is open, in either state; an undeclared key stays the browser's; a declaration naming a key the core answers itself is refused by name (ADR-0050, 2026-09-30; ADR-0010) | Layer 2 | as stated |
| **DC-58** | MUST | A per-cell appearance (ADR-0050, item 15): a supplied Font colour, bold, italic, underline, strikethrough and Fill are painted on that cell alone; a bold cell's `####` decision and its painted-text width use `CellTextMetrics`' bold widths, so a bold number that fits at the regular widths and not at the bold ones is `####`; a row repaints only when its Values or its appearance change; nothing per cell reaches JavaScript and the DOM bound of §18 holds (ADR-0050, 2026-09-30; ADR-0016, ADR-0027 P1–P9) | Layer 2 render counts and markup; Layer 3 painted pixels | as stated |
| **DC-59** | MUST | Supplied Border sides are drawn as Excel draws them: centred on the gridline, a thick line reaching into both cells, above Fills and below the Focus, the Selection and the Reference Outlines; the line drawn for an edge recorded on both sides is the Consumer's answer; only painted rows are drawn (ADR-0050, item 15, 2026-09-30). *(2026-10-02, ADR-0071: on a Sheet's Paper the lines lie above the Selection's shade, which multiplies with them, and below its outline; not yet in a Pinned Column.)* *(2026-10-11, decided with the user: **`0.1.0` needs it in a Pinned Column too.** A pinned cell stands in a layer of its own, so the shade there cannot multiply with its lines today; how the pinned selection is painted is decided in an ADR of its own before it is built, and P1–P9 are checked again after it.)* *(Decided the same day, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): a range's tint is an element of its own, the pinned tints and bands lie in a shade layer of their own, and on the Paper that layer multiplies as one group. It also holds on the scrollable side of a range that crosses the pinned boundary, which a range's clip had isolated. Verified as ADR-0206 lists, the sub-pixel gate included.)* | Layer 3 pixels at 100% and 150% | as stated |
| **DC-60** | MUST | A Consumer's popover (ADR-0050, item 16) opens inside the grid's box and is bounded by it, scrolls when its contents are taller, takes the keyboard and returns it on closing, and closes as a Cancel when the box shrinks below one row; two grids' popovers are independent (ADR-0050, 2026-09-30; ADR-0039, ADR-0040, ADR-0018) | Layer 2 + Layer 3 | as stated |
| **DC-61** | MUST | `ReturnKeyboardAsync()` gives the grid DOM focus — its Keyboard Field where it has one, its root otherwise (ADR-0080) — when DOM focus is on nothing or already inside this grid, and never takes it from another control, another grid, or the grid's own Formula Bar or Name Box. It moves neither the Focus nor the Selection, and scrolls nothing; before the grid is attached it does nothing. It is one method for a Consumer and for a Chrome whose frame lies outside the grid (ADR-0070/0071/0018). *(Extended 2026-10-02:)* `HandKeyboardToAsync(control)` hands the keyboard the grid holds on to a control of the Consumer's own, in place of that control's own focus, under the same condition: a press made before the request lands keeps the keyboard where it put it. It moves neither the Focus nor the Selection and renders nothing; before the grid is attached, and once it is gone, the control takes the keyboard by its own focus (ADR-0070/0021) | Layer 2 (asked for under its condition; the hand-on asked of the handle, or the control's own focus where there is no handle); Layer 3 (a control focused meanwhile keeps the keyboard; a second grid keeps its own; the keyboard put in the records before the hand-on lands stays there, PV-41) | as stated |
| **DC-62** | MUST | With `OnLeave` declared, an Escape pressed on the root with nothing left to dismiss releases Tab as KB-8 says and raises it once, the release first, and keeps the DOM focus. An Escape that closes an Inner Popup or a popover, cancels an edit, closes a Formula Entry's list, leaves an Interactive cell or returns from a control inside a cell does neither, and a held key's repeats do neither again. Without the declaration, Escape does what KB-8 says: it releases Tab (ADR-0070, raised beside the release since 2026-10-02; ADR-0012) | Layer 2; Layer 3 on `/pivot?details=dialog` | as stated |
| **DC-63** | MUST | With `OnCellDoubleClick` listened to, a double click on a cell where no edit opens raises it once, with the cell's position, after the press has placed the Focus there, and changes nothing else; on an Editable cell the edit opens and nothing is raised; while an edit is open nothing is raised; a double click on a control inside an Action, Template or Mark cell stops at that cell and never names another; without a listener nothing changes (ADR-0063; DC-1) | Layer 2; Layer 3 on `/pivot` | as stated |
| **DC-64** | MUST | With `CellChangedAt` declared, a value cell carries `ex-changed` while the Consumer's answer is less than `ChangeHighlightDuration` (1 s by default) before the `Clock`'s time. When a mark ends, the class goes in one step, and no row renders whose mark did not end. A new delegate is the change signal, and rewriting what an unchanged delegate answers changes no mark. It is asked of value cells only. Without the declaration there is no call, no class and no timer (ADR-0068; DC-1) | Layer 2 with a fake `TimeProvider` and render counts | as stated |
| **DC-65** | MUST | A mark is keyed by row and column: after a scroll recycles the row elements, the mark stays on its cell and never moves with an element to another row (ADR-0068/0027) | Layer 2; Layer 3 on `/grid-live` | as stated |
| **DC-66** | MUST | A mark never animates: UX-6 holds with marks painted, under the core's stylesheet and under a Wrapper's. The forced-colors block restates the mark. No live region announces it (ADR-0068/0027/0033) | Layer 3 on `/grid-live` (UX-6's check with marks showing); inspect the stylesheets' sources, `src/ExGrid/Assets/ex-grid.css` and each Wrapper's under `src/*.MudBlazor/Assets/` *(restated 2026-10-11, decided with the user: it read `wwwroot`, which since ADR-0123 holds only the minified builds; §22)* | no transition or animation; the forced-colors rule present; the live region unchanged |
| **DC-25** | MUST | The declarations are per instance: two grids on one page, one declaring and one not, behave each as its own declarations say (ADR-0018) | Layer 3 | independent |

---

## 27. ExSheet (SH)

*(Added 2026-09-27, with ADR-0046 to ADR-0049.)* **These criteria judge ExSheet and never gate
ExGrid's release** (§2). They are written now so that "ExSheet works" has a meaning while it is
built; when ExSheet is proposed for a release of its own, this section becomes its gate and gains
the preconditions §2 has for ExGrid. *(That happened on 2026-10-11: `0.1.0` carries ExSheet stable,
so this section gates the tag ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)).)*

*(Added 2026-10-11, decided with the user, [ADR-0202](adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md).)* Before `0.1.0`:
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
  *(2026-10-12, decided with the user: one exception, `PMT`, kept though its result may differ from
  Excel's in its last digits, by ADR-0047's note of that date. Its cases that differ are recorded as
  differences by that decision, with Excel's double beside the engine's, and it is a known limitation.)*
  *(2026-10-12, decided with the user: a refusal is never caught, anywhere. An argument refused so
  refuses the Formula, and every Formula that reads its cell, whatever it does with it; ADR-0047's note
  of that date.)*
- **SH-28's edge cases, and SH-51 and SH-52's by-hand halves, are run on Windows** (the seventh run's
  Parts A and B, ticket 160), and a disagreement is settled in ADR-0051 or ADR-0100 first. *(SH-28's
  were run in the seventeenth, Part C, and the readings of 2026-10-12, R1, and settled in ADR-0051 on
  2026-10-12.)*
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations. *(2026-10-12: so is `NPV`, refused
  by ADR-0202's note of that date, and so are `PMT`'s last digits, kept by ADR-0047's.)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SH-1** | MUST | `ExSheet.Engine` references nothing of ours and no Blazor package; `ExSheet` references it and `ExGrid`; no package references `ExSheet` but its Wrapper, `ExSheet.MudBlazor` (SH-47) (ADR-0046/0047). *(Restated 2026-10-11: it said "nothing references `ExSheet`", which its Wrapper, decided by SH-47, and the demo pages and tests, which ship nothing, already did; a correct tree failed it as written.)* | `dotnet list … reference` / `package` | as stated |
| **SH-2** | MUST | A Sheet shows 1,048,576 rows × 16,384 columns; the DOM does not grow with the extent (ADR-0046, DOM-*) | Layer 2 node count at the top and at XFD1048576 | the same bound as §18 |
| **SH-3** | MUST | A row height at which the full extent exceeds the scroll ceiling is refused by name (ADR-0046, VZ-8) | Layer 1/2 | throws naming the height and the ceiling; 28 px passes |
| **SH-4** | MUST | An edit repaints only the rows whose Values changed; every other row skips its render (ADR-0046/0003) | Layer 2 render counts | as stated |
| **SH-5** | MUST | Inserting a row keeps the Row Sequence Version and the Selection where they were, and rewrites every Reference to keep naming the same cells; deleting a referenced cell yields `#REF!` (ADR-0046/0047) | Layer 1 + Layer 2 | as stated |
| **SH-6** | MUST | The Reference grammar — `A1`, `$A$1`, `A$1`, `A1:B2`, `A:A`, `1:1`, a Sheet qualifier, structured references — parses and round-trips in the invariant syntax in every culture (ADR-0047) | Layer 1 | exact round trip |
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047), and a call of a function Excel has that ExSheet does not declare is a Refused Formula, which shows `#NAME?` *(2026-10-13, decided with the user)*. *(2026-10-12, decided with the user: `PMT` but in its last digits, ADR-0047's exception for `PMT` alone; its rows that differ are recorded as differences by that decision.)* | Layer 1 | every row of every table |
| **SH-8** | MUST | Only dependents of a change recompute, and no pushed Window carries Values from an unfinished recalculation (ADR-0047) | Layer 1 (counted) + Layer 2 | as stated |
| **SH-9** | MUST | A cycle is `#CIRC!` in every member and every dependent, and breaking it recovers (ADR-0047) | Layer 1 | as stated |
| **SH-10** | MUST | Date serials match Excel's 1900 system, 29 February 1900 included; display is at most 15 significant digits and `####` when it does not fit (ADR-0047/0016) | Layer 1 + Layer 2 | as Excel |
| **SH-11** | MUST | Typed constants are parsed under the Sheet's culture and recorded parsed; a document saved under `en-US` reads the same numbers under `de-DE` (ADR-0048) | Layer 1 | as stated |
| **SH-12** | MUST | A Sheet Document holds no Values, round-trips, and a document of an unknown version is refused (ADR-0048) | Layer 1 | as stated |
| **SH-13** | MUST | One undo step per user operation (edit, paste, fill, insertion, deletion); a Consumer command lands on the same stack in order; replacing the document clears it; two ExSheets keep separate stacks (ADR-0048/0018) | Layer 2 | as stated |
| **SH-14** | MUST | Inside ExSheet a copy carries Entries and shifts relative References; outward it carries Values; inward each field is parsed as typed (ADR-0048) | Layer 1 + Layer 3 with the real clipboard | as stated |
| **SH-15** | MUST | Fill: copy with References shifted, a linear series from two or more numbers, dates by day, and from whole columns or whole rows a series by one from a single number in a line, each as Excel; any other pattern refused, never filled with copies; a fill from whole columns or whole rows records its source's column (row) format on the target's columns (rows) and writes only the cells that hold something (ADR-0050, "The handle on a whole axis") | Layer 1 | as stated |
| **SH-16** | MUST | A Linked Table is read by structured reference and `XLOOKUP`; before its first snapshot readers show `#GETTING_DATA`, which propagates and which `IFERROR`/`ISERROR` do not catch; a snapshot replaces the last in one step; a copy reaching a waiting cell is refused; an undeclared name is `#NAME?` (ADR-0049) | Layer 1 + Layer 2 | as stated |
| **SH-17** | MUST | A saved document's Values computed by `ExSheet.Engine` alone equal the Values on screen (ADR-0047/0048). *(2026-10-10: for a Sheet that draws, when the engine is given the same Sheet Seed and count of changes, ADR-0180.)* | Layer 1 against a Layer 2 render | equal |
| **SH-18** | MUST | ExSheet on the DemoHost, both hosts and both browsers: typing, Point, completion, the Formula Bar, fill, paste, Ctrl+arrow, Headings and insertion, with a clean console (CON-*) | Layer 3 | green; no console message |
| **SH-20** | MUST | General fits the column as Excel's does (`=1/3` in a default column reads `0.333333`), and a number or date typed into a default-width column that does not fit widens it (ADR-0047) | Layer 1 against the case corpus; Layer 2 | as Excel |
| **SH-21** | MUST | Formats at cell, row and column level, cell over row over column; formatting a whole column records one entry (ADR-0047) | Layer 1 | as stated |
| **SH-22** | MUST | Column widths are recorded in the Sheet Document and move with inserted and deleted columns; a default width is not recorded (ADR-0046) | Layer 1 + Layer 2 | as stated |
| **SH-23** | MUST | Ctrl+D and Ctrl+R copy the source's Entries over the target with relative References shifted, formats and alignment with them, as one undo step, leaving the Selection; a date or number is copied, never continued; a source scrolled out of the Window is still read (ADR-0035; ADR-0050, item 5, 2026-09-28) | Layer 1 + Layer 2 | as Excel |
| **SH-24** | MUST | Delete clears every Entry in the Selection as one undo step, keeps formats, and leaves the Selection and the Focus where they were; over empty cells it records no step (ADR-0054; ADR-0052, case 14) | Layer 2 | as stated |
| **SH-25** | MUST | Ctrl+F opens the grid's find panel, and a step searches the Sheet's cells by their displayed text, every row, by rows from the cell after the Focus, wrapping, with Match case, Match entire cell contents and a selected scope honoured as `GridFind.Step` honours them (ADR-0055) | Layer 2 | as stated |
| **SH-26** | MUST | A recorded column width is one of three kinds: default (not recorded), widened by entry (recorded, marked custom as Excel's file marks it, and widened again by a longer entry), or set by the user (recorded, marked, never widened by an entry); a user's width replaces an entry's, never the reverse; a column inserted to the right of a recorded width takes it as set by the user, as Excel's file holds it (CW-023) (ADR-0046, 2026-09-28 and 2026-10-12; ADR-0047) | Layer 1 against the case corpus (CW-018, 021 to 029); Layer 2 | as Excel |
| **SH-27** | MUST | Ctrl+Enter with a Formula over a range writes it as entered in the Focus and shifts its relative References into every other cell, as one undo step (ADR-0050, 2026-09-28) | Layer 2; Layer 3 beside Excel | `=A1` over B2:C3 from B2 gives `=A1`, `=B1`, `=A2`, `=B2` |
| **SH-28** | MUST | F4 cycles the Reference at the caret as ADR-0051 says: `A1` → `$A$1` → `A$1` → `$A1` → `A1`; of a cell range, the end at the caret, from its own form (`=SUM(A1:B2)`, the caret after `B2`, gives `=SUM(A1:$B$2)`); every Reference, and every end of a range, a selection covers, overlaps or touches at either end, to the first one's next form (`+` selected in `=A1+B1` gives `=$A$1+$B$1`), and a range being pointed at whole (`=D11:D12` gives `=$D$11:$D$12`); whole columns and rows in two forms; the Sheet qualifier kept; the rewritten Reference's column letters in upper case (`=b2` gives `=$B$2`); a structured reference, a function name, a number and text that is not a Formula unchanged; the rewritten Reference selected where the caret had been moved or a selection made, and a caret at its end where typing left the caret, in the cell and in the Formula Bar. The edge cases are Excel's as the seventeenth Windows run (Part C) and the readings of 2026-10-12 (R1) observed them (ADR-0051, 2026-10-12) | Layer 1 + Layer 2 + Layer 3 | as stated |
| **SH-29** | MUST | While an edit is open, `DoAsync`, `UndoAsync`, `RedoAsync`, `SetNumberFormatAsync` and `SetAlignmentAsync` are refused by name and change nothing; Linked Table declarations and snapshots are not refused; replacing the Sheet Document while an edit is open discards the edit and announces it with that reason; ExSheet says whether an edit is open and raises a notification when that changes, from the grid's own notification of an edit opening and ending (ADR-0048, ADR-0050 section 6, 2026-09-29) | Layer 2, one per command and per way an edit ends; Layer 3 on `/sheet`: `99` over C4, *Insert a row above row 2*, Enter | each command refused with the open edit named; a replaced Document announces the discarded edit and writes nothing; the Sheet Document unchanged by the refused command; `99` lands in C4's Plums row as typed, never in another row; the page's buttons follow the notification |
| **SH-30** | MUST | ExSheet answers the References function as ADR-0057 reads: each Reference in the text with its span and the cells it names, in a finished or an unfinished Formula, a range typed as far as its colon answering its first corner; `Sheet1!A1` with this Sheet's own name as a Reference, and one with another name not at all; whole columns and rows; nothing inside a string, and no function name; a structured reference with its table and column as the key, when both are declared (one naming an undeclared table or column not at all); text that is not a Formula answered with nothing, where a Formula begins with `=`, or with `+` or `-` as Excel colours it. The eighth Windows run settled the edges (ADR-0057) | Layer 1 | as stated |
| **SH-31** | MUST | While a Formula that reads a Linked Table is edited, ExSheet tells its Consumer each column it reads and that column's colour, and an empty list when the edit ends; `/sheet` passes it to its positions grid through its Pointing Scope (ADR-0058), which outlines those columns (ADR-0057, ADR-0049) | Layer 2; Layer 3 on both hosts | the positions grid's outline has the colour of the text `Positions[PV]` wears in the editor |
| **SH-32** | MUST | A Pointing Scope: while a Sheet of the Scope points, a press on a registered grid writes, at the caret, `XLOOKUP(<key>, T[<key column>], T[<column>])` for a cell and `T[<column>]` for a column header, in the table's column names, never the header labels. The key is written as Excel writes a constant of its kind (text quoted, with `"` doubled; a number invariant; `TRUE`/`FALSE`). More than one cell, several columns, a Header Group's rectangle, a column the table does not have, a table declared without a key, or a blank key write nothing and tell the Consumer the reason; a drag that reaches another cell takes back what its press wrote. A further press, on a registered grid or on the Sheet, replaces what this Point wrote. A grid in no Scope, a registered grid with an open edit of its own, and another Sheet are never pointed at. `/sheet` registers its positions grid in a Scope; `/sheets` gives each side its own (ADR-0058) | Layer 2, one per row of ADR-0058's table; Layer 3 on `/sheet` and `/sheets`, both hosts | as stated; on `/sheets` a press on the right's positions grid while the left Sheet points is an ordinary press |
| **SH-33** | MUST | A Linked Table declared with a key column records it in the Sheet Document. Every snapshot of a keyed table is checked: a key that appears twice, compared as `XLOOKUP`'s exact match compares (case does not tell keys apart), refuses the snapshot by name, naming the table, the key column and a repeated value; the table waits again and every reader shows `#GETTING_DATA`, which `IFERROR` does not catch; no earlier snapshot's value is shown. Blank keys are not compared. A declaration with another key replaces the held one (ADR-0049, 2026-09-30) | Layer 1 | as stated; `IFERROR(XLOOKUP(…), 0)` shows `#GETTING_DATA`, never 0 |
| **SH-34** | MUST | In a Scope's grid, while a Formula is edited, the Linked Table columns it reads are outlined in their colours without the page wiring `OutlinedColumns`. A pressed cell gets only the dashes (DC-53), over the row with that key, and they go when Point ends while the column outlines stay until the edit ends; a pressed column header dashes the column. The written `XLOOKUP(...)` lies on the grey ground as a whole, unless it follows the `=` directly, with its column references in their darker shades (ADR-0058; ADR-0057, 2026-09-30) | Layer 2; Layer 3 on both hosts | as stated; after the positions grid is sorted, the dashes are on the row with the written key |
| **SH-35** | MUST | Only the Sheet that holds the keyboard points; when the keyboard leaves it, no grid is pointed at and the edit stands (ED-26). The first press on a registered grid points. After it, ↑ and ↓ point one row further in the grid's current order and ← and → to the next column the table has, rewriting what this Point wrote and moving the dashes; at an edge nothing moves; the grid scrolls the pointed cell into view; a row that has not arrived writes nothing and tells why; Shift+arrow and Ctrl+arrow write nothing and tell why. After a press on a column header, ↓ points at the column's first row in the grid's current order, ← and → at the next column the table has as a column (`T[<column>]`, its body dashed), and ↑ is an edge (Part B of the ninth run, Q52). After a drag took back what its press wrote, the arrows are the Sheet's own Point. F4 changes nothing, and the Name Box is empty. On a circuit, a press within the round trip after `=` is an ordinary press, and the edit stands (ADR-0058, and what the ninth Windows run settled, Parts A and B) | Layer 2; Layer 3 on both hosts, and on the Server host with 150 ms injected | as stated; `=`, a press on R-1's PV, ↓ gives `=XLOOKUP("R-2", Positions[Id], Positions[PV])`; → from Id passes over Book to PV; `=`, a press on PV's header, ↓ gives `=XLOOKUP("R-1", Positions[Id], Positions[PV])`, and ← from PV's header gives `=Positions[Id]` on `/pointing` |
| **SH-36** | MUST | At an argument whose values are a fixed list, completion lists the values with Excel's texts, as observed: `XLOOKUP`'s `match_mode` (`0 - Exact match`, `-1 - Exact match or next smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`, `3 - Regex match`) and `search_mode` (`1 - Search first-to-last`, `-1 - Search last-to-first`, `2 - Binary search (sorted ascending order)`, `-2 - Binary search (sorted descending order)`); the value list opens as the argument begins, before anything is typed, and only while nothing of the argument stands after the caret (with the caret before or inside a value nothing is listed; Part B of the ninth run, Q49; white space after the caret counts, the thirteenth run, Q55); a value typed whole lists that value alone, a number that is no value lists nothing, and any other text lists every value with the first selected, letters included, where no function or table is listed (the thirteenth run, Q54); while it is open ↑/↓ choose in it, Tab writes the value and closes the list, Escape closes the list first, and ↓ then points; where Point can go, ←, →, `Home`, `End` and the Shift+arrows close it and do what Point does with them, and elsewhere they stay the editor's (the tenth run; Part B of the ninth run, Q51); at a Reference's place with no outline standing, list or no list, `Home` points at the row's first column and `End` writes nothing and commits nothing (Q53); a press on the grid points while it is open. After `Table[` the list offers the table's column names only. Tab on a name or a column closes the list, and it is not opened again on what Tab wrote. Backspace back into a name lists again. Nothing is listed after `=`, an operator, `(` or `,`, and F3 is not claimed (ADR-0058; ADR-0051, 2026-09-30; the ninth and tenth Windows runs) | Layer 1 + Layer 2; Layer 3 | as stated |
| **SH-37** | MUST | A test pins today's refusal and fails when it goes: `T[[A]:[B]]` is refused by the grammar. Its name cites ADR-0058, and its failure message says to open ADR-0058's "Not in the first version", decide with the user, and not change the expectation. *(Its twin, `(T[C]="x")` over two rows is `#VALUE!`, failed as meant when ADR-0125 arrived; the decision is SH-61.)* (ADR-0058) | Layer 1 | passes today; the message names the next step |
| **SH-38** | MUST | A Cell Format holds Number Format, Alignment, Font (colour, bold, italic, single underline, strikethrough), Fill (one solid colour) and Border (four sides, Excel's thirteen line styles, a colour; the line between two cells is one line, set and cleared from either side, the later winning, and where both cells record one the upper or left cell's is shown); a colour is Automatic or RGB. Font, Fill and Border are recorded at cell, row and column level as SH-21 says, move with insertions and deletions, are copied into an inserted row or column from the one before (Borders excepted, as Excel), travel in an ExSheet-to-ExSheet copy, are carried by Ctrl+D, Ctrl+R and the fill handle, survive Delete, and are one undo step per operation. The Sheet Document round-trips them; a document of an older version reads with none; a colour of another kind is refused by name (ADR-0071, ADR-0048) | Layer 1 | as stated |
| **SH-39** | MUST | The Paper is white and the Ink black under the light and the dark scheme; the Focus, the Selection, Reference Outlines and the Cell Editor on the Paper keep their light-scheme appearance, while the Headings, the Name Box, the Formula Bar and popovers follow the scheme; setting `--ex-sheet-paper` / `--ex-sheet-ink` changes them; `ExGrid.MudBlazor`'s stylesheet does not set them (ADR-0071, ADR-0027, ADR-0030) | Layer 3 under both schemes and both Chromes; inspect the Wrapper's stylesheet | as stated |
| **SH-40** | MUST | A Number Format's named colour (`[Red]` and the seven others) is painted in Excel's colour for that name and wins over the Font colour; `[ColorN]` is refused by name (ADR-0071, ADR-0047) | Layer 1 + Layer 2; Excel's colours from the eleventh Windows run | as stated |
| **SH-41** | MUST | A bold number is `####` exactly when it does not fit at the bold widths, and General fits a bold number to the bold widths; an italic number that fits is not cut at either edge (ADR-0071, ADR-0016) | Layer 2 at the edge width; Layer 3 pixels | as stated |
| **SH-42** | MUST | Each formatting key Excel has (ADR-0071's list) does what Excel does, under each Sheet culture the eleventh Windows run observed, as one undo step; a toggle key's direction follows the Focus cell; a key Excel does not have is not claimed; Ctrl+1 to Ctrl+5 reach the page before the browser's tab switching in Chrome and Edge (ADR-0071) | Layer 2; Layer 3 beside Excel after the run; by hand on Windows for the browser's own keys (the run's part B) | as Excel |
| **SH-43** | MUST | While an edit is open, a formatting key changes nothing, is announced through the live region with its reason and raised to the Consumer, and Ctrl+U does not open the page's source; `SetCellFormatAsync` and `OpenFormatCellsAsync` are refused as SH-29 says (ADR-0071, ADR-0048) | Layer 2; Layer 3 for Ctrl+U | as stated |
| **SH-44** | MUST | `SetCellFormatAsync` sets only the parts its change names, over the Selection, as one undo step; borders in a change apply to each selected range, outline per range as in Excel; `CellFormatAt` answers cell over row over column; `SetNumberFormatAsync` and `SetAlignmentAsync` behave as the change they abbreviate (ADR-0071) | Layer 1 + Layer 2 | as stated |
| **SH-45** | MUST | Format Cells opens from Ctrl+1, from the Context Menu's "Format Cells…" and from `OpenFormatCellsAsync`; it offers Number, Alignment, Font, Border and Fill; Accounting and Fraction are shown disabled with the reason; a Custom code ExSheet does not read is refused by name; it opens on the Focus cell's Cell Format; OK sets only what the user touched as one undo step, and Cancel or Esc sets nothing; the tabs switch with the arrow keys; on closing the keyboard is the Sheet's again. Under the built-in Chrome it is a popover inside the Sheet's box (DC-60); under `ExSheet.MudBlazor` it is a `MudDialog` (ADR-0071, ADR-0010) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes | as stated |
| **SH-46** | MUST | Borders and Fills on the DemoHost match Excel's for the eleventh and the fourteenth Windows runs' cases: the line drawn where both sides of an edge are recorded, a Fill over the gridlines, the gridline between two filled cells, a thick and a double line between two cells, a double line over a Fill, and the dashes in device pixels at 100% and 150% (ADR-0071) | Layer 3 beside Excel's screenshots from the runs | as Excel |
| **SH-47** | MUST | `ExSheet.MudBlazor` references `ExSheet`, `ExGrid.MudBlazor` and MudBlazor; `ExGrid.MudBlazor` references no ExSheet package; the package smoke check packs it; no script is added (ADR-0019, ADR-0021, ADR-0071) | `dotnet list … reference` / `package`; `tests/ExGrid.PackageSmoke/check.sh`, which also refuses a script in the packed package; inspect `src/ExSheet.MudBlazor/Assets/` *(restated 2026-10-11, decided with the user: it read `wwwroot`, which since ADR-0123 holds only the minified builds of `Assets/`; §22)* | as stated |
| **SH-48** | MUST | A Sheet shows no Sheet Toolbar unless `ShowToolbar` is set; set, it stands above the Formula Bar inside the Sheet's box and outside ExGrid's instance root, and `ShowToolbar` shows and hides every Toolbar Row together, the default row included. The Sheet's `Height` includes it; every Toolbar Row is one height resolved in C# from the Density, and the Viewport loses exactly rows × that height (ADR-0100, ADR-0028) | Layer 2 (the inline values); Layer 3 on both hosts | as stated; a key pressed on a Toolbar Item never reaches the grid's key listener |
| **SH-49** | MUST | `ToolbarContent` lays out its Toolbar Rows and Toolbar Items in markup order, and a Consumer's own item stands among ExSheet's; without it, the default row is Bold, Italic, Underline, Strikethrough, Font colour, Fill, Borders, Left, Center, Right, Number Format, Percent, Comma, Format Cells… in that order. Each item calls only public commands, which a test holds by finding no internal member referenced from an item type (ADR-0100, ADR-0071) | Layer 1 (the reference check) + Layer 2 | as stated |
| **SH-50** | MUST | Whenever the Selection changes, each formatting item shows the Focus cell's `CellFormatAt` (Bold pressed over a bold cell, `aria-pressed`); a press acts on the grid's Selection as it is now, as one undo step; while an edit is open every Toolbar Item, the Consumer's included, is unavailable; a pointer press leaves DOM focus on the Sheet, the toolbar is one Tab stop with ← and → among its items, and a command run from the keyboard gives the keyboard back to the Sheet. The same markup draws plain buttons under the built-in Chrome and MudBlazor's controls under `ExSheet.MudBlazor`, with the same behaviour; its dropdowns take Format Cells' frames; no script is added (ADR-0100, ADR-0071, ADR-0010, ADR-0021) | Layer 2 under both Chromes; Layer 3 on both hosts under both Chromes, with a clean console | as stated; a press straight after Shift+ArrowDown on the Server host formats the extended range |
| **SH-51** | MUST | Where the Consumer binds `ShowToolbarChanged`, Ctrl+F1 raises it with the other value and the Sheet holds nothing; unbound, Ctrl+F1 is not claimed (ADR-0100, ADR-0050) | Layer 2; Layer 3, and by hand on Windows that the key reaches the page | as stated |
| **SH-52** | MUST | While the Sheet holds the keyboard, its Sheet Toolbar is shown and no edit is open, Alt (Option on macOS) released alone, or F10, shows a KeyTip per Toolbar Row (the formatting row's is H); a row's letter shows its items' letters, Excel's Home tab letters where Excel has one and ExSheet's own otherwise (Strikethrough's 4, never a letter Excel's Home tab uses); a letter runs its item and leaves the keyboard on the Sheet; Escape goes back one level and ends from the top; an unmatched key ends it doing nothing; Alt with another key stays a chord (Alt+↓, Alt+Enter); a Consumer's letters are only those declared, and two colliding at one level are refused at render naming both; while an edit is open they start nothing, and with the toolbar hidden Alt and F10 stay the browser's; on Windows the browser's menu does not open from a Sheet whose toolbar is shown (ADR-0100, ADR-0021) | Layer 2; Layer 3; beside Excel and by hand on Windows for the browser's keys (a Windows run) | as Excel, where Excel has the letter |
| **SH-53** | MUST | Ctrl with `+` (with Shift or without) and Ctrl with `-` insert and delete the rows a Selection of whole rows spans, and the columns a Selection of whole columns spans, each as one undo step, on the Selection the key carries; any other Selection, and a key while an edit is open, changes nothing and says why; the page does not zoom from inside a Sheet (ADR-0050 item 14, ADR-0071) | Layer 2; Layer 3 on both hosts | as stated |
| **SH-54** | MUST | On a Sheet's Paper, a single range's outline and the Focus's when one cell is selected are 2 Device Pixels on the gridline and one outside it, with a white line of one Device Pixel inside that is not drawn while an edit is open; under the built-in Chrome the outline is `#217346` and the shade `#C7C7C7` over white; a black line inside the shade stays `#000000`; the Focus cell is unshaded; under `ExSheet.MudBlazor` the outline is the palette's primary and the shade the primary mixed into white (ADR-0071, 2026-10-02) *(2026-10-11, [ADR-0206](adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md): also in a Pinned Column and across the pinned boundary, under both Chromes; it had been checked only where nothing is pinned.)* *(2026-10-12, decided with the user: among several ranges no range is outlined, and the Focus takes a thin border of one Device Pixel, `#217346` on the Paper; ADR-0008.)* That border lies inside the Focus's cell, a Device Pixel in from its edges and its gridline, in the outline's colour, so the palette's primary under `ExSheet.MudBlazor`, and has no white line of its own: between it and the gridline lies the cell's ground, white on white Paper, as the readings of 2026-10-12 observed (R4) | Layer 3 pixels at 100% and 150% under both Chromes | as stated |
| **SH-55** | MUST | Format Cells' Font tab has a *Normal font* box under both Chromes: checking it sets the colour Automatic and every emphasis off, OK records them on each selected cell over a row's or column's Font, and it shows checked exactly while every part is the default (ADR-0071, 2026-10-02) | Layer 2 under both Chromes | as stated |
| **SH-56** | MUST | `TODAY()` answers the Sheet Day: a fixed `Today`, else the day now in `TimeZone`, else in the browser's zone; until one is known it is `#GETTING_DATA`, which `IFERROR` does not catch and which a Formula calling `TODAY` shows whichever branch it takes. The server's zone is never used. The engine reads no clock: `Sheet.SetToday` sets the day, and recalculates only the Formulas that call `TODAY` and what reads them (ADR-0121) | Layer 1 + Layer 2 | as stated |
| **SH-57** | MUST | ExSheet moves the Sheet Day on at midnight in its zone, reading at most an hour apart, on the registered `TimeProvider`; a reading that finds the same day recalculates nothing; a disposed Sheet reads no clock (ADR-0121) | Layer 2 on a fake clock | as stated |
| **SH-58** | MUST | The grid reads the browser's time zone once at attach, only when `OnBrowserTimeZone` has a delegate, and passes on a name that is non-empty and at most 100 characters; on both hosts, a browser in a zone on another date than the server's shows its own date in `TODAY()` (ADR-0122, ADR-0021's ninth entry) | Layer 2 + Layer 3 on both hosts, in two browser zones 25 hours apart | as stated |
| **SH-59** | MUST | A Formula that calls a volatile function (`OFFSET`, `NOW`; and, since 2026-10-11, `INDIRECT`, `RAND` and `RANDBETWEEN`) is recalculated in every recalculation, with what reads it; a Reference it computes is read as it stands after the recalculation, a spill's layout included, a cell still to compute being computed first, and a Formula that reaches itself that way is `#CIRC!`; the engine reads the moment from `Sheet.NowSource` once per recalculation (ADR-0124) | Layer 1 | as stated |
| **SH-60** | MUST | ExSheet gives `NOW()` the moment in the Sheet's zone (the Consumer's, else the browser's), `#GETTING_DATA` until one is known, and moves it on as each minute turns on the registered `TimeProvider`; a Sheet with no volatile Formula is not recalculated by the clock (ADR-0124) | Layer 2 on a fake clock + Layer 3 on both hosts | as stated |
| **SH-61** | MUST | A Linked Table's key may be several columns, declared as a list and recorded in the Sheet Document as one at version 9. A press on a cell of such a table writes `XLOOKUP(1, (T[K1]=v1)*(T[K2]=v2), T[C])`, which reads the row whose parts all match, and `#N/A` when none does; a snapshot in which two rows hold the same Values in every key column, compared as `=` compares, is refused as a repeated key; a row with a blank or Error part has no key (ADR-0058, amended 2026-10-03) | Layer 1 + Layer 2 | as stated |
| **SH-62** | MUST | A Formula whose result is an array of more than one Value spills from its Anchor; an Entry or another spill in its way, or the Sheet's edge, makes the Anchor `#SPILL!` and nothing is overwritten; clearing the obstacle lets it spill; of two spills that would overlap, the Anchor first in address order spills, whichever was entered first; what reads a spilled cell follows the spill; a spill is not recorded in a Sheet Document; an array of more than 2^24 Values is `#NUM!` (ADR-0125) | Layer 1 | as stated |
| **SH-63** | MUST | `A1#` reads the Spill Range of the Formula in A1, follows it as it grows and shrinks, moves with A1 on an insertion or deletion, and is `#REF!` where A1 does not spill; `#` after a range and the `@` operator are refused on entry (ADR-0125) | Layer 1 | as stated |
| **SH-64** | MUST | `FILTER`, `UNIQUE`, `SORT`, `SORTBY`, `SEQUENCE` and `TRANSPOSE` spill, and `SUMPRODUCT` reduces its arrays, each to its corpus cases; `INDEX` with a row or a column of 0, `XLOOKUP` with a return array of several columns, and `ROW` and `COLUMN` over several rows or columns spill as Excel does (ADR-0125, ADR-0047) | Layer 1 (the case corpus) | as stated |
| **SH-65** | MUST | Through the component, a spill paints its Values; selecting a spilled cell shows its Anchor's Formula in the Formula Bar, dimmed (`ex-formula-bar-borrowed`) and read-only, and a press on the bar opens no edit; typing into a spilled cell enters an Entry there and the Anchor shows `#SPILL!` until it is cleared; part of a spill copied without its Anchor pastes as Values, and with it the Formula spills again (ADR-0125, ADR-0050's `FormulaBarBorrowedTextOf`) | Layer 1 + Layer 2 | as stated |
| **SH-66** | MUST | `RAND` draws in [0,1) and `RANDBETWEEN` a whole number between its bounds, both bounds included. Each draw is a function of the Sheet Seed, the cell, the call's place in its Formula and the count of changes since the Sheet was opened, so the same Entries, seed and count give the same draws whatever order cells are computed in. Two calls draw twice, a `LET` binding draws once, and everything that reads a draw in one recalculation reads the same Value. An edit, an insertion or deletion, a rename of the Sheet, the undo or redo of one, a Linked Table's snapshot whatever its rows, and a Linked Table's declaration draw again; a recalculation by the clock, a change of format and a rename to the Sheet's own name draw nothing. A Sheet with no seed answers `#GETTING_DATA`. ExSheet takes the Consumer's `Seed`, or chooses one when it opens a Sheet Document, which records neither seed nor draw. `RANDBETWEEN` gives `#VALUE!` for a fractional bound and for one above 2^53 in magnitude, and otherwise answers as observed (ADR-0180) | Layer 1 + Layer 2 | as stated |
| **SH-67** | MUST | A `LET` name is seen by the values bound after it and by its own calculation only; an inner `LET` may bind it again, and a name read before its binding or inside its own value is `#NAME?`. Names are compared without regard to case, start with a letter or `_`, go on with letters, digits and `_`, and are at most 249 characters; a `LET` holds at most 126 pairs. Refused on entry: a name that reads as an A1 Reference, `TRUE`, `FALSE`, a name bound twice, an even number of arguments and a missing calculation. A function's name, declared or not, and an R1C1 shape that reads as no A1 Reference are names, and a value left empty is bound (`LET(x,,x)` is 0), as Excel's keyboard was observed to take them (the seventeenth run). `Table[Column]` inside a `LET` reads the Linked Table whatever is bound. Each binding is computed at most once, when first read, and an unused binding's Error Value does not reach the result. A `LET` depends on everything its text names, so an unused binding over a cycle is `#CIRC!` and one over a waiting Linked Table is `#GETTING_DATA` (ADR-0181) | Layer 1 (the case corpus, the observed `LET` cases included) + Layer 2 (refusal on entry) | as stated |
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet; R1C1 text naming a cell, a whole row or column, the rectangle two parts of the same kind span, or one cell's Spill Range with `#`, each R and C a number, an offset in brackets from the Formula's own cell, or none, the own row or column; a Linked Table's column as structured text (waiting while the table does), whatever `a1` says; and `A1#`. It keeps its text through an insertion or deletion; gives `#VALUE!`, until a run observes them, for R1C1 text with a row or column number 0 or past the Sheet's edge or an offset past the Sheet's edge, and for a `#` after a range, a row or a column, or inside a join, in either style, whatever the parts joined, and, by decision, for a declared Linked Table's bare name (ADR-0049's rule 2); and `#REF!` for text that names no place, a table or a column that does not exist, A1 text past the Sheet's edge and R1C1 parts of two kinds joined included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-12, decided with the user: a declared Linked Table's bare name stays `#VALUE!`, in either style; in R1C1 style, a row or column number past the Sheet's edge is `#VALUE!` until Excel is asked; A1 text past the edge stays `#REF!`, as `XFE1` was observed to be.)* *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
| **SH-19** | OBSERVATIONAL | Recalculation time at a large Sheet, and completion's keystroke-to-list time on a circuit. *(The case, decided with the user on 2026-10-11: a synthetic Sheet from a fixed seed, 10,000 rows by 20 columns with about 50,000 Formulas — per-row arithmetic, a running total chained down a column, and `SUM` over whole columns. Recorded: a full recalculation, one edit's propagation, and a row inserted near the top; completion's keystroke-to-list time on the Server host with 150 ms injected. Until then no case was named and nothing measured it.)* *(2026-10-12, decided with the user: the whole-column `SUM`s are the twenty totals beside the block. A whole-column `SUM` on every row instead is a Known limitation of `0.1.0` (ADR-0202's note of that date), measured by `spikes/exsheet-sh19-whole-column`, not by this row.)* | recorded in `metrics.json` | recorded, never gated |

---

## 28. Find (FD)

*(Added 2026-09-26, with
[ADR-0055](adr/0055-find-is-asked-of-the-consumer-like-sort-and-filter.md).)* The grid asks and
moves the Focus; the Consumer searches. These criteria hold the core to the asking and the
moving, and `GridSource.From` to what a match is, because it is the reference a remote Source is
held to. Like §26, these are ExGrid criteria and gate the release (§2). *(Numbered §26 on `main`; it became §28 when the ExSheet branch, which had taken §26 and §27, was merged on 2026-09-28.)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **FD-1** | MUST | Ctrl+F is claimed whenever the root holds DOM focus, on every grid, and inside the grid's own popovers — in the find field it selects the field's text, in any other popover it closes that one and opens Find; in the Cell Editor it is taken and does nothing; in a Template cell's control it is the control's (ADR-0055) *(Widened 2026-09-27, ADR-0055's review section: it first stopped at the root, and Ctrl+F in the find field opened the browser's search)* | Layer 1 for the table, Layer 3 | the browser's find bar never opens from a focused grid; the editor's text is unchanged |
| **FD-2** | MUST | With no search wired — no Source that can find and no `OnFind` — Ctrl+F opens nothing and raises `OnFindRefused(Unavailable)` (ADR-0055) | Layer 2 | one refusal; no popover |
| **FD-3** | MUST | With a search wired, Ctrl+F opens the find panel through the Chrome seam, which takes the keyboard by its `FocusRequest` and returns it to the root on Escape or a pointer-down elsewhere (ADR-0055/0039) | Layer 2 + Layer 3 under both Chromes | `document.activeElement` is the panel's field; after Escape it is the root |
| **FD-4** | MUST | A step asks with the text, both options, the direction, the Focus, the scope — the selected ranges when more than one cell is selected, none otherwise — the visible columns in the current order, and the Row Sequence Version (ADR-0055) | Layer 2 with a recording `OnFind` | every field as stated |
| **FD-5** | MUST | A found cell becomes the Focus and is revealed; with a scope the selection stands, without one it collapses onto the cell (ADR-0055) | Layer 2 | Focus equals the answer; the selection as stated; a row outside the Window is scrolled to |
| **FD-6** | MUST | "Not found" raises `OnFindRefused(NotFound)` and the panel's outcome says so; an answer under a stale Row Sequence Version moves nothing and raises `OrderChanged`; a step asked while one is out cancels it and its answer is discarded (ADR-0055/0011/0025) | Layer 2 | as stated; the cancelled token observed |
| **FD-7** | MUST | `GridSource.From` finds by the displayed text, row by row in the given column order, from the cell after `From`, wrapping, with `OrdinalIgnoreCase` unless `MatchCase`, containment unless `WholeCell`, within the scope when there is one, and a lone match finds itself (ADR-0055/0023) | Layer 1 | every clause a named test |
| **FD-8** | MUST | The panel is a popover by every popover rule: inside the grid's box, one per instance, Enter is next and Shift+Enter previous, and the text survives a close and reopen (ADR-0055/0040/0039) | Layer 2 + Layer 3 | as stated |
| **FD-9** | MUST | `GridSource.Fetch` answers through its `find` delegate and reports `CanFind` only when one is given; an existing `IGridSource` without `FindAsync` compiles and reports that it cannot (ADR-0055) | Layer 1 | as stated |
| **FD-10** | MUST | `OnFind` beside a bound `Source` is refused by name, as `Window` beside `Source` is (ADR-0055, 2026-09-27) | Layer 2 | an exception naming both parameters |
| **FD-11** | MUST | An answer outside the request — a column not among its `Columns`, a row outside the result — throws, naming the request; `OrderChanged` is raised only when the version moved or a requested column left the grid (ADR-0055, 2026-09-27) | Layer 2 | as stated |

---

## 29. ExPivot (PV)

*(Added 2026-09-30 with ADR-0059 to ADR-0063. Revised the same day, when the user decided them in a
grilling that added ADR-0064 to ADR-0069.)*

**These criteria judge ExPivot, ExPivot.Engine and ExPivot.MudBlazor, and never gate ExGrid's
release** (§2). Like §27, they give "ExPivot works" a meaning while it is built. When ExPivot is
proposed for a release of its own, this section becomes its gate and gains the preconditions §2 has
for ExGrid. *(That happened on 2026-10-11: `0.1.0` carries ExPivot stable, so this section gates the
tag, together with §32's rows on ExPivot's report Windows ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)).)*

Where a criterion says "as Excel", the behaviour was read from Excel's documentation and has not
yet been run beside Excel on Windows. `docs/specs/expivot/excel-behaviours.md` lists each such
reading until a run settles it. *(2026-10-11, decided with the user, [ADR-0202](adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md): the run is made before
`0.1.0`, on the sign-off's Windows run (ticket 07). The twelve readings that change a number on screen —
items 2 to 8 and 15 to 17 — gate the tag: a disagreement is settled in the ADR first and the code after
it. A disagreement in layout or wording is a known limitation of `0.1.0`. PV-21's numbers near the
combination cap are recorded in Step 6 and said as a known limitation; they do not gate.)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **PV-1** | MUST | `ExPivot.Engine` references `ExGrid.Data` exactly and no package. `ExPivot` references it and `ExGrid`, both exactly. `ExPivot.MudBlazor` references `ExPivot` and `ExGrid.MudBlazor` exactly, and MudBlazor `[9.6.0,10.0.0)` (ADR-0205). Nothing references ExPivot but its Wrapper, the demo pages and the tests (ADR-0059/0062/0064/0205) *(restated 2026-10-12, decided with the user: it said "MudBlazor from 9.0.0", a floor with no ceiling, which ADR-0205 replaced with a range whose floor is now 9.6.0)* | the package check (`tests/ExGrid.PackageSmoke/check.sh`) | as stated |
| **PV-2** | MUST | The report is one ExGrid. Local and server report sources supply the requested Window with the report's full extent through the same asynchronous contract (ADR-0151/0153, LV-25). The label columns are pinned Template Columns, and the column Items are Header Groups over the value columns. Group rows paint as Group and totals as Total. No column is Editable, and the header click selects. There is no column menu, sort or filter (ADR-0059/0151/0153) | Layer 2 | as stated |
| **PV-3** | MUST | Items: text is told apart ignoring case and labelled by the first spelling to arrive among the records present; numbers by value, across Integer, Decimal and Double columns; dates by their clock value; a Blank as `(blank)`. They are ordered Number, Date, Text, Boolean, Error, Blank, with Blank last in either direction and a declared order first (ADR-0060) | Layer 1 | every clause a named test |
| **PV-4** | MUST | The eleven Aggregations answer as Excel's, with blanks, text and errors among the records included. A total is aggregated from its records, never from the totals below it. Integer and Decimal columns are summed exactly. Only the parts the Value Fields ask for are accumulated (ADR-0060/0066) | Layer 1 | every row of each table |
| **PV-5** | MUST | A Hidden Item's records are left out of every cell and every total. Hiding every Item of a field is refused with ExPivot's reason, and OK stays disabled (ADR-0060/0061) | Layer 1 + Layer 2 | as stated |
| **PV-6** | MUST | These lay out as Excel's do: the Compact, Outline and Tabular forms; subtotals at the top, at the bottom or none; grand totals either way; collapsed Items; Σ Values in rows or columns; several Value Fields; and Show Values As (ADR-0060) | Layer 1 | the report's rows, labels and headers per case |
| **PV-7** | MUST | A value paints its engine text in its number format, and a copy carries its full-precision number. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 | as stated |
| **PV-8** | MUST | The Field List's rules are functions from one layout to the next: tick, untick, drop at an index, move, remove, and Σ Values only between Rows and Columns. Each gives the layout Excel's pane gives. A command that would change nothing is disabled (ADR-0061) | Layer 1 + Layer 2 | as stated |
| **PV-9** | MUST | Swapping the Chrome changes no behaviour: the same gestures under the built-in markup, a substituted `IPivotChrome` and `MudPivotChrome` make the same layout (ADR-0061/0062) | Layer 2 under the three; Layer 3 under the built-in markup and MudBlazor on both hosts | the same `PivotLayoutJson` |
| **PV-10** | MUST | Drag and drop uses Blazor's own events, with no JavaScript of ExPivot's or its Wrapper's. Every drop target prevents `dragover`'s default, and a drop that means nothing changes nothing. In a real browser, a field dragged onto an Area lands there, an entry dropped on another lands before it, and an entry dropped on the list of fields or its own report (including the empty state) removes only that placement. Details grids, other pivots, list fields, external or cancelled drags and Σ Values remove nothing. A replaced source or pane layout invalidates the drag; a late target cannot reuse an old index (ADR-0061/0021) | Layer 3 under both Chromes on both hosts; inspect `src/ExPivot/Assets/` and `src/ExPivot.MudBlazor/Assets/`, and `find src/ExPivot src/ExPivot.MudBlazor -name '*.js' -not -path '*/bin/*' -not -path '*/obj/*'` for a script beside a component *(restated 2026-10-11, decided with the user: it read `wwwroot`, which since ADR-0123 holds only the minified builds of `Assets/`; §22)*; the Wrapper's script tests | green; no `.js` |
| **PV-11** | MUST | A menu or panel opens directly under its entry, as wide as the pane and over what follows, one at a time per ExPivot. It scrolls with its entry in the full-width body, which keeps every action reachable while the heading stays in view. Escape, Cancel and a command close it, and the keyboard goes back to the entry. A Field List menu also closes on a press or DOM focus elsewhere in the same ExPivot, outside the menu and its opener: the outside operation keeps its meaning and the keyboard is not taken back. A disabled menu item or frame padding can be pressed without losing Escape. A late dismissal cannot close a replacement surface. While a select's list inside is open, Escape is the list's (ADR-0061/0039) | Layer 2 + Layer 3 under both Chromes | as stated |
| **PV-12** | MUST | The report filter band, on the Pivot Toolbar's left, shows `(All)`, the one Item shown or `(Multiple Items)`. Its Filter… opens under the Pivot Toolbar with a backdrop that closes it, and OK filters the report (ADR-0061) | Layer 2 + Layer 3 | as stated |
| **PV-13** | MUST | An outer Item expands and collapses from its ± button, the Context Menu or a double click on its label, and the Focus stays on it. A refresh that changes only values keeps the Row Sequence Version, and so the Selection (ADR-0059/0011) | Layer 2 + Layer 3 | as stated |
| **PV-14** | MUST | Show Details, from a double click (DC-63) or the Context Menu, shows the records behind a value cell: none hidden, in the Snapshot's order. It opens a tab at the report's foot by default: closable, titled by the cell, not in the Pivot Layout, and paged from the source. It opens a dialog when the Consumer asks for one. When the Consumer listens to `OnShowDetails`, it hands the records to the Consumer, and neither the tab nor the dialog opens. An empty cell shows nothing (ADR-0059/0063) | Layer 2 + Layer 3 | as stated |
| **PV-15** | MUST | A Field List interaction renders no grid row and not the grid. The report grid's Chrome is asked for once per Pivot Chrome and kept (ADR-0003/0061) | Layer 2 render counts | counts unchanged; the same instance |
| **PV-16** | MUST | A Pivot Layout round-trips through `PivotLayoutJson`, and every Pivot Source question and answer round-trips through `PivotJson`. A document of an unknown version is refused (ADR-0059/0066) | Layer 1 | as stated |
| **PV-17** | MUST | Two ExPivots on one page share nothing: a menu open in one is not open in the other, and a move in one leaves the other's layout alone (ADR-0018) | Layer 2 | as stated |
| **PV-18** | MUST | `mud-ex-pivot.css` maps every Visual Token ExPivot reads onto the MudBlazor palette under the paper, and follows the scheme. It sets no Geometry Token and writes no rule against a class of ExPivot's or ExGrid's. The ± button in a label cell stays ExPivot's plain markup (ADR-0062/0030/0003) | Layer 2 stylesheet tests; Layer 3 light and dark | as stated |
| **PV-19** | MUST | The report grid's Context Menu under `MudPivotChrome` speaks ExPivot's words, the Consumer's `Label` included, and never a command's id (ADR-0061/0062) | Layer 2 | as stated |
| **PV-20** | MUST | The six pages — `/pivot`, `/pivot-csv`, `/pivot-db`, `/pivot-live`, `/pivot-risk` and `/grid-live` — work on both hosts and in both browsers, under both Chromes wherever the page has a pivot. The run starts the demo API server, and the console stays clean (CON-*) (ADR-0069) | Layer 3 | green; no console message |
| **PV-21** | OBSERVATIONAL | The targets the user set as "snappy" (Q52), in the browser (a published build without AOT) over 1,000,000 records: the first visual answer to any gesture ≤ 0.1 s; collapse, sort or a change of form ≤ 0.1 s; a change that needs a new question ≤ 0.3 s; 1,000 changes ≤ 0.2 s; a CSV of a million rows read ≤ 4 s; the page blocked ≤ 50 ms at a time. On CoreCLR, about a tenth of each. The default cap on leaves (200,000, provisional) is settled here too *(settled 2026-10-01 at 200,000, with the work after an answer sliced: ADR-0066, PV-40)* | recorded in `metrics.json` | recorded, never gated |
| **PV-22** | MUST | `PivotSource.From` over a Snapshot answers every question by the engine's rules. A source answering through `PivotSource.Fetch` from the same Snapshot gives the same Leaf Aggregates, Items and Details, question for question. The demo's SQL source does too, over the same trades (ADR-0066) | Layer 1; `tests/ExGrid.DemoApi.Tests` | equal answers |
| **PV-23** | MUST | Source Version: a field's Items and a cell's records are asked for under the report's version. A source that cannot answer under it refuses, and ExPivot says that the data has changed rather than show records that do not add up. The bundled source answers under any version it holds (ADR-0066) | Layer 1 + Layer 2 | as stated |
| **PV-24** | MUST | A source's features name the Aggregations it answers. Value Field Settings… offers the others disabled, with the reason, and ExPivot never asks a source for an Aggregation it does not offer (ADR-0066) | Layer 2 | as stated |
| **PV-25** | MUST | Asking never blocks. While a question is out, the Field List shows the new layout, and the report stays under `IsLoading` as it was. A further change cancels the question in flight. An answer to a superseded question is never painted (ADR-0066/0025) | Layer 2 with a source that answers on demand | as stated |
| **PV-26** | MUST | A change that needs no new question asks none. Collapse, order, the form, subtotals, grand totals, Show Values As, captions and formats lay out the held answer. An Aggregation change asks again only for parts the answer lacks (ADR-0060/0066) | Layer 2 with a counting source | the count unchanged |
| **PV-27** | MUST | The bundled source works in slices and yields between them, and a cancelled question stops at the next slice (ADR-0066) | Layer 1 | as stated |
| **PV-28** | MUST | Defer Layout Update: while it is ticked, the pane's changes build a pending layout, and the report and the source are left alone. Update, or unticking, applies the pending layout in one change. `LayoutChanged` is raised only for the layout the report shows (ADR-0061) | Layer 2 | as stated |
| **PV-29** | MUST | Caps: a question carries `MaxLeaves`, and a source that would exceed it refuses. A layout whose report would pass 1,048,576 rows or 16,384 columns, or whose source refuses for its leaves, is refused by name, and the report stays on the layout before. The Consumer can change each cap (ADR-0066) | Layer 1 + Layer 2 | as stated |
| **PV-30** | MUST | The Pivot Toolbar holds the report filter band on the left. On the right it holds Layout ▾, then Refresh (only when the source can be refreshed), then the pane's toggle, whose state the Consumer can bind. The pane's heading has a close icon at its right, still reachable when narrow or when the body scrolls; it uses the same binding, closes the pane's popup and keeps its layout and pending edits. The Layout menu offers Excel's Design tab choices, marks the current one, and disables a choice that would change nothing (ADR-0061) | Layer 2 + Layer 3 | as stated |
| **PV-31** | MUST | Order Key: Items are ordered by their key, ascending, with ties falling back to the label. An Item with no key comes after the keyed ones. A key function that throws is refused, naming the field and the value. Two values with one key stay two Items. The key orders the Item lists of Filter… and of the report filter band, and does not touch a sort by value (ADR-0060) | Layer 1 | every clause a named test |
| **PV-32** | MUST | Date parts: a field declared as a Date column's year, quarter or month is labelled as Excel labels it, in the report's words, and ordered by the calendar. The part of a Blank is a Blank (ADR-0060) | Layer 1 | as stated |
| **PV-33** | MUST | The Japanese words, chosen in one line, replace every word ExPivot paints and the ExGrid commands in the report's Context Menu, and every sentence the grid says for it: a refused Copy or Summary, with a source's own sentence inside `source-refused` (2026-10-09). They never follow the culture on their own (ADR-0060) | Layer 2 | no English word left on `/pivot` in Japanese |
| **PV-34** | MUST | Live data in the process: a Change Batch applied to the bundled source makes the next Snapshot. After any sequence of batches, every leaf equals a fresh aggregation of the resulting Snapshot, to the last bit (ADR-0067) | Layer 1 property test | equal, over random batches |
| **PV-35** | MUST | The screen never shows half a batch. Changes are gathered and redrawn at most every 250 ms by default; the Consumer may set the interval, and 0 redraws on every change. A change of values alone keeps the Row Sequence Version, the Selection and an open menu or panel. An Item appearing or leaving drops the Selection (ADR-0067/0011) | Layer 2 with a fake `TimeProvider` | as stated |
| **PV-36** | MUST | The report's Change Highlight: a value cell whose painted text changed with the data is marked, and every cell of a row that appears is marked. A layout change, a sort or a collapse marks nothing. A change the format hides is not marked (ADR-0067/0068) | Layer 2; Layer 3 on `/pivot-live` | as stated |
| **PV-37** | MUST | When the newest data cannot be shown — a cap, or a source that failed or refused — the report stays on the last version it could compute. A notice above it says what happened and as of when, and offers Retry (ADR-0067) | Layer 2 | as stated |
| **PV-38** | MUST | A server's source: when the Consumer says its data changed, or Refresh is pressed, ExPivot asks again for the whole answer (ADR-0066/0067) | Layer 2 | as stated |
| **PV-39** | MUST | Show Details' dialog closes on an Escape its grid has nothing left to dismiss. However the dialog closes — Escape, Close, the backdrop — the report's grid takes the keyboard back. A details tab that closes while selected hands the keyboard to the tab selected next: the report's grid, when that is the report's tab. Escape in a details tab's grid closes nothing (ADR-0070) | Layer 2; Layer 3 on `/pivot?details=dialog` and `/pivot` | as stated, under both Chromes |
| **PV-40** | MUST | The work after a question's pass yields to the browser at least every 30 ms, as the pass does: the bundled source assembling its answer, and ExPivot making the cube and laying out the report. A gesture made meanwhile supersedes the question, and the report on screen stays until the new one is complete (ADR-0066/0067) | Layer 2 (the yields counted for a large answer; a gesture superseding a question mid-build); observed in `measure-pivot.spec.mjs` (the longest task near the cap) | as stated |
| **PV-41** | MUST | The controls that take the keyboard from the report as a details view opens — the dialog's Close, the tab Show Details opens — and the tab selected when the one holding it closes, take it through the report grid's `HandKeyboardToAsync()` (DC-61): only while it is still the report's. A press the user made before the request landed — into the records, another grid, a control of the page's — keeps the keyboard where it put it, however long the round trip took; in the records, their grid then hands it to its root (A11Y-20) (ADR-0070, 2026-10-02; ADR-0021, ADR-0033) | Layer 2 (each request goes through the report's grid, under both Chromes); Layer 3 on `/pivot?details=dialog` and `/pivot`, both hosts: DOM focus put in the records in the task that draws them, before the control's request can land on the Server host | as stated, under both Chromes |
| **PV-42** | MUST | ExPivot declares a Row Key for its report rows — a row's role, Value Field and Items — so a live redraw re-renders the rows that changed in place, and builds no row component for a key it painted before (ADR-0140) | Layer 2 counting row components across live redraws on `/pivot-live`'s generator | no row component built for a key already painted |
| **PV-43** | MUST | A report row's key is made once, when the engine builds the row, with its hash: reading it allocates nothing and hashes nothing, so the grid's check of the painted rows' keys allocates no more per row than its pass over the instances did (ADR-0140, D10 of 2026-10-06) | Layer 1 allocation test: reading every row's key over a report | zero bytes |
| **PV-45** | MUST | On `/pivot-live`'s generator, the rows that render per live redraw are exactly the painted rows whose painted text changed, and no row component is built for a key painted before (ADR-0153; PV-42; M3). *(Restated 2026-10-08 for ADR-0153's display rows; PV-44 and PV-46, the Claude Code track's criteria for ADR-0161's next report and its Change Highlight, are LV-29 and LV-30 now.)* | Layer 2 counting row renders and mounts per redraw | as stated |
| **PV-47** | MUST | A live or data redraw whose report computation throws `OutOfMemoryException` leaves the report on screen as a Stale Report with the out-of-memory reason and Retry, and the next change asks again. A user's layout change that runs out of memory is refused: the layout goes back and the Pivot Toolbar says so; before a first report it is a refusal. Nothing reaches the renderer as an unhandled exception (ADR-0161/0067; the layout change's refusal confirmed by the user, 2026-10-09) | Layer 2 with a computation step that throws (an Order Key, a slicer's yield), under both Chromes | as stated |
| **PV-48** | OBSERVATIONAL | Ticket 01's ExPivot measurement repeated after the report-Window design: the update step by step on CoreCLR and Apply to the painted frame in the browser (a published build without AOT) at about 10⁴, 10⁵ and 4×10⁵ report rows, with the collector's pauses and the managed heap; PV-21 recorded beside it (ADR-0153). *(Recorded on the Codex track's code before the merge, in `verification/2026-10-06-macos-live-report-after`, and on the merged code against `main` in `verification/2026-10-10-linux-merged-live-costs`: the whole update 3 to 147 times faster than `main`'s, every step in single-digit milliseconds at 4×10⁵ rows, and in the browser a redraw timed to the changed text rather than the painted frame. PV-21 beside it, on both: a new question over a million trades 16 to 60% slower than `main`'s in the browser, mostly the branch's .NET code under the interpreter, and the live update about even. After the fixes of that afternoon, `234da46` and `f2bc6a4`, which leave the chains and each total's list of leaves to the first update that needs them, a new question is even with `main`'s up to 1,350 combinations and 10–35% slower from 27,000 up, and level with it or faster on CoreCLR except at 198,450.)* | recorded in `metrics.json` and a `verification/` record | recorded, never gated |
| **PV-49** | MUST | While the report is a Stale Report (PV-37), its value cells carry the stale appearance beside the notice — `--ex-pivot-stale-value-color` on the report's `ex-pivot-report-stale`, static and readable at a contrast of at least 4.5:1 (UX-8) under the default and the MudBlazor wrapper, nothing animated (P8), no row rendered again for it — and lose it when the report recovers. Copy from a Stale Report is refused in ExPivot's words (`copy-stale-report`), saying the time of the version shown and to Retry first; the Selection Summary still answers (ADR-0067's section of 2026-10-09; decided with the user) | Layer 2: each cause of a Stale Report, then recovery; Copy and the Summary while stale, the refusal's Japanese and its replacement by id; Layer 3 on `/pivot-live`: a server cut off, its value cells' paint and contrast, built-in and MudBlazor | as stated |

## 30. The Snapshot and its packages (DA)

*(Added 2026-09-30 with ADR-0064 and ADR-0065.)*

**These criteria judge `ExGrid.Data` and `ExGrid.Data.Arrow`, which ship with ExPivot and never
gate ExGrid's release** (§2). When ExGrid adopts the Snapshot, by an ADR of its own, the criteria it
then relies on move into the release. *(2026-10-11: `0.1.0` carries both packages stable, so this
section gates the tag ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)).)*

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **DA-1** | MUST | `ExGrid.Data` references no package and nothing of ours. `ExGrid.Data.Arrow` references `ExGrid.Data` exactly and `Apache.Arrow` within a stated range. Nothing references either but `ExPivot.Engine` and `ExGrid` (`ExGrid.Data` only), the demo and the tests (ADR-0064/0065; `ExGrid` since ADR-0130) | the package check | as stated |
| **DA-2** | MUST | A Snapshot is immutable: no public member changes one. A Change Batch yields a new Snapshot, and the old one reads exactly as before (ADR-0064) | Layer 1 | as stated |
| **DA-3** | MUST | The kinds hold their values as ADR-0064 says. Text is exact, with its dictionary in order of first appearance. Decimal is exact, and `1.5` and `1.50` read back alike. Double keeps non-finite values. Integer is 64-bit. Date is a clock value: a `DateTime`'s ticks with its `Kind` ignored, a `DateOnly`'s midnight, a `DateTimeOffset`'s clock without its offset. Boolean is true or false. A Blank is possible in every kind and differs from `""` and 0 (ADR-0064) | Layer 1 | every clause a named test |
| **DA-4** | MUST | Built from objects with typed accessors, a Snapshot keeps the objects, by reference and in order, and boxes no value (ADR-0064) | Layer 1, with an allocation bound | as stated |
| **DA-5** | MUST | Every builder takes a `CancellationToken`, reports progress and yields between slices. A cancelled build yields nothing (ADR-0064) | Layer 1 | as stated |
| **DA-6** | MUST | A load that meets a value it cannot read fails whole, naming the row and the column. No Snapshot is yielded (ADR-0064) | Layer 1 | as stated |
| **DA-7** | MUST | CSV under a Schema. Quoting is RFC 4180's: a quoted separator, a doubled quote, a line break inside quotes. The header is matched per declared column: a missing column is refused by name, and an undeclared one is skipped. The separator, the header row, the decimal point, the thousands separator, the culture and the date format are as declared. An empty field, and each declared blank string, is a Blank in every kind. Nothing is guessed (ADR-0064) | Layer 1 | every clause a named test |
| **DA-8** | MUST | UTF-8 is read with or without its byte-order mark, and Shift-JIS when the Schema declares it. Only the opt-in encoding refers to the code pages, so an application that does not ask for them does not load them (ADR-0064) | Layer 1; inspect `src/ExGrid.Data` | as stated |
| **DA-9** | MUST | A suggested Schema is built from a file's first rows. It marks each column whose kind is not clear, and is never applied unless it is handed back (ADR-0064) | Layer 1 | as stated |
| **DA-10** | MUST | A `DbDataReader` is read by its own column types. A type outside ADR-0064's list is refused by name, unless the Consumer declares how to read it (ADR-0064) | Layer 1 | as stated |
| **DA-11** | MUST | Record Key and Change Batch. Two records under one key are refused, naming the key. A batch is applied whole or refused whole, with nothing applied, when it changes or removes a missing key or adds an existing one. A changed record keeps its place, and an added one goes at the end. A dictionary only grows. A Snapshot without a key takes only adding batches (ADR-0064) | Layer 1 | every clause a named test |
| **DA-12** | MUST | The next Snapshot shares every part a batch did not touch: applying 1,000 changes to 1,000,000 records allocates in proportion to the changes, not the records (ADR-0064) | Layer 1, with an allocation bound | as stated |
| **DA-13** | MUST | A Snapshot written and read back through `ExGrid.Data.Arrow` is equal column by column and Blank by Blank, with its captions, its Record Key and its version (ADR-0065) | Layer 1 | as stated |
| **DA-14** | MUST | Reading Arrow. Other producers' dictionaries — out of order, one value in two cases, a null entry, null indices — are read under the Snapshot's rules. The type table is honoured, and a type or value outside it is refused by name. A compressed stream without a codec is refused, naming the codec; with the codec, it is read (ADR-0065) | Layer 1 | every clause a named test |
| **DA-15** | MUST | Writing Arrow gives an uncompressed IPC stream. A Date column is written as `date32` when every value is a midnight, and otherwise as the coarsest `timestamp` unit that holds every value exactly (ADR-0065) | Layer 1 | as stated |
| **DA-16** | MUST | The package check reads and writes an Arrow stream through the packed packages (ADR-0065/0042) | `tests/ExGrid.PackageSmoke/check.sh` | green |
| **DA-17** | OBSERVATIONAL | A million records built from objects, read from a CSV and read from Arrow, on CoreCLR and in the browser | recorded in `metrics.json` | recorded, never gated |
| **DA-18** | MUST | The eleven Aggregations' definitions — what each counts and includes, and how a result is finished from its parts — live in `ExGrid.Data`, and both `ExPivot.Engine` and ExGrid's Selection Summary answer through them. **Gates ExGrid's release** (ADR-0130/0060) | Layer 1; inspect the references | one definition; PV-4's tables pass through it |

---

## 31. Selection Summary (SM)

*(Added 2026-10-05, with
[ADR-0130](adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md).)* The grid asks
and shows; whoever holds the data answers. These criteria hold the core to asking only the current
question and showing only its answer, and `GridSource.From` to what each figure means. SM-1 to
SM-11 and SM-14 gate ExGrid's release (§2); SM-12 and SM-13 judge ExSheet and ExPivot.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **SM-1** | MUST | A request carries the selected ranges, the Row Sequence Version, the visible columns in the current order and the figures shown, and nothing else (ADR-0130) | Layer 2 with a recording `OnSummarize` | every field as stated |
| **SM-2** | MUST | A change of selection clears the figures at once and shows the pending mark; the request in flight is cancelled and its answer discarded; an answer under a stale Row Sequence Version shows nothing. The previous selection's figures are never shown beside a new selection (ADR-0130/0025/0011) | Layer 2 with a gated answerer | at no render are figures shown that answer another question; the cancelled token observed |
| **SM-3** | MUST | A new Window, a Source's change or an edit under a standing selection clears the figures and asks again; the grid does not throttle (ADR-0130) | Layer 2 | one new request per change |
| **SM-4** | MUST | A declined request shows its reason in place of the figures; no partial figure is ever shown. `InMemoryGridSource` never declines, at any size (ADR-0130) | Layer 1 + Layer 2 | as stated; a whole column of 1,000,000 rows summed |
| **SM-5** | MUST | With nobody to answer, no figure, mark or refusal is shown, and `CanSummarize` is false. `OnSummarize` beside a bound `Source` is refused by name. `GridSource.Fetch` reports `CanSummarize` only with a `summarize` delegate; an existing `IGridSource` compiles and reports that it cannot (ADR-0130/0055) | Layer 1 + Layer 2 | as stated |
| **SM-6** | MUST | `GridSource.From` answers each figure by ADR-0130's table: numbers in every figure; text, Booleans, dates and errors in Count only; blanks in none; a cell under two ranges once; hidden columns never; an Action Column blank; a Template Column by its value accessor; Integer and Decimal summed exactly (ADR-0130/0060) | Layer 1 | every row of the table a named test |
| **SM-7** | MUST | An error among the values leaves Count and Numerical Count, and no other figure, as Excel's status bar does (ADR-0130). *(2026-10-12: Excel's rule recorded in `verification/2026-10-11-windows-17/`, Part B's b5 and b6; the criterion read "leaves Count alone … once a Windows run has read Excel's answer" until then.)* | Layer 1 (`AggregateAccumulatorTests`, `GridSummaryTests`), Layer 2 (`SheetSummaryTests`), Layer 3 (`selection-summary.spec.mjs`, the Sheet case); the Windows record | as recorded |
| **SM-8** | MUST | The demo API server's SQL `summarize` gives, question for question, the answer `GridSource.From` gives over the same rows (ADR-0130) | `tests/ExGrid.DemoApi.Tests` | equal answers |
| **SM-9** | MUST | The built-in Chrome shows Average, Count and Sum by default in the status line whenever two or more cells are selected — the line standing whenever the grid can summarise, so selecting never moves the Viewport — with every figure but the counts in the Focus's format, as Excel's are in the active cell's whatever the other cells' formats (the Focus cell's on a Sheet and a report, the Focus's column's on a grid); the right-click menu reports a change of figures and the grid holds none (ADR-0130/0010) *(2026-10-12: Excel's rule recorded in the seventeenth Windows run, Part B's b7 and b8, and pinned in `SheetSummaryTests`.)* | Layer 2 + Layer 3 under both Chromes | as stated |
| **SM-10** | MUST | The figures are not written to a live region (ADR-0130/0033) | Layer 2 | no announcement on a selection change |
| **SM-11** | MUST | Behaviour is the core's: SM-2, SM-4 and SM-9 pass with `MudGridChrome` exactly as with the built-in Chrome (ADR-0130/0010) | Layer 3, under both Chromes | identical outcomes |
| **SM-14** | MUST | `ShowSelectionSummary` is on by default; off, no strip and no figure on the grid, and no request unless `OnSelectionSummaryChanged` has a delegate, which is still told; switched on again, the standing selection is asked about. ExSheet and ExPivot carry it to their grid (ADR-0130, 2026-10-06) | Layer 2 | as stated |
| **SM-12** | MUST | ExSheet answers from its own cells: a formula's value, every cell of a spilled array, by the same table (ADR-0130). *(2026-10-13, decided with the user: a Selection holding a cell whose Formula ExSheet refused shows no numeric figure, and says why.)* *(2026-10-13: it is declined, no figure shown, Count included, its reason naming the cell in `SheetCommandIds.SummaryRefused`'s words.)* | Layer 1; Layer 2 (`SheetSummaryTests`) | as stated |
| **SM-13** | MUST | ExPivot answers from the cells it lays out, subtotals and grand totals summed with the rest (ADR-0130) | Layer 2 | as stated |
| **SM-15** | MUST | The grid walks a new Window to learn whether the rows moved only while figures stand or are being asked for, and compares only the Selection's positions: with no figure the walk is not made, and a change outside the Selection moves no figure (ADR-0130, 2026-10-07) | Layer 2 counting the row comparisons | as stated |

---

## 32. Live data (LV)

*(Numbered §31 until 2026-10-06, when the Selection Summary reached `main` first and kept it (#65).
Commit messages before then use the old number.)*

*(Rewritten in part on 2026-10-07, decided with the user: a write now lands as the user entered it
([ADR-0142](adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md), rewritten), so LV-11 to LV-14, LV-16 and LV-17 were
restated and LV-19 to LV-21 added; the grid holds no row beyond its Window
([ADR-0160](adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)), LV-22 and LV-23; LV-10 refined for a pushed Window's vouch.)*

*(Merged on 2026-10-08, decided with the user when the two tracks of live data continued were compared.
The grid's criteria are the Claude Code track's: LV-10 to LV-23 above. ExPivot's report Windows are the
Codex track's ([ADR-0151](adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md) to
[ADR-0153](adr/0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md)): its LV-20 to
LV-28 are LV-24 to LV-31 here, its LV-19 (ExPivot's vouch) is LV-10's last clause, and its LV-22 is this
LV-22's ExPivot clause. LV-32 and LV-33 are the merge's own, and LV-12, LV-16, LV-17, LV-20, LV-22 and
LV-27 to LV-31 were restated with what the comparison found.)*

*(Added 2026-10-05 with [ADR-0140](adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md),
[ADR-0141](adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md) and
[ADR-0142](adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md), decided with the
user the same day.)* The Row Key, the bundled sources' live data, and what a write over live data
does: it lands as the user entered it, on the row it was aimed at. **They are ExGrid code, and they
gate the release** (§2), as §26 does. *(Narrowed 2026-10-11, decided with the user ([ADR-0200](adr/0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)): the rows on
ExPivot's report Windows, LV-24 to LV-31 and LV-34, are ExPivot's code, in `ExPivot.Engine` and
`ExPivot`. They judge ExPivot with §29, not ExGrid; `0.1.0` carries ExPivot, so they still gate the
tag.)* Without a Row Key declared, the Row Key's criteria change nothing
(DC-1); the rules for a write over live data hold for every grid.

| ID | Level | Statement | Verification | Pass |
|---|---|---|---|---|
| **LV-1** | MUST | With a Row Key declared, a row whose instance changes under the same key keeps its row component: the component is not disposed, renders once, and no other row renders; the edits it sends are the changed text and attributes only. Without the declaration, a changed row's component is replaced, as before (ADR-0140; DC-1) | Layer 2: the row component's instance kept and its render count up by one; a render-batch capture inserts no element for that row | as stated |
| **LV-2** | MUST | A Row Key that repeats within a Window, or a null key, is refused by name, naming the key and its positions, before Blazor's own exception for clashing keys (ADR-0140) | Layer 2 | the grid's exception, never Blazor's |
| **LV-3** | MUST | `GridSource.From` with a Row Key applies a Change Batch whole: a changed row keeps its place in the base order, and an added one goes at the end. A key that repeats, or a changed or removed key the source does not hold, refuses the whole batch by name, with nothing applied (ADR-0141) | Layer 1 | every clause a named test |
| **LV-4** | MUST | A whole new list handed to `From` is paired by Row Key: a new key is added, a missing key removed, a different instance under a key changed, and the same instance unchanged. The source's own order follows the list, and the Row Sequence Version moves when the sequence moved (ADR-0141, D7 of 2026-10-06) | Layer 1 | every clause a named test; a list in a new order is shown in that order |
| **LV-5** | MUST | The incremental requery's result equals `GridQueryEngine.Apply` over the same rows, Filter and Sorts exactly, the stable tie order included, and equals the same changes made one `ReplaceRow` at a time (ADR-0141/0023) | Layer 1 property test over random batches (adds, changes, removes), sorts (Blanks, ties, case, culture) and filters; the seed recorded | zero differences |
| **LV-6** | MUST | Gathering: a change after a quiet interval reaches the grid at once; the changes within an interval reach it as one, at the interval's end, as the newest version. The interval is 250 ms by default, the Consumer may set it, and 0 passes every change. No Window is computed from part of a batch, and `ReplaceRow` is not gathered (ADR-0141/0067) | Layer 1 + Layer 2 with a fake `TimeProvider` | as stated |
| **LV-7** | MUST | Under live data the Row Sequence Version moves exactly when the result's sequence moved anywhere: a batch that moves no row keeps it, and with it the Selection; a batch that moves one row anywhere moves it (ADR-0141/0011) | Layer 1 + Layer 2 | both directions tested |
| **LV-8** | MUST | `GridSource.Fetch`, told that its data moved on, reads the Window again, pairs its rows by Row Key and marks the cells that changed. The Row Sequence Version moves when the answer's order token differs, and with every change when the server sends none. Told so with no Row Key, it refuses by name (ADR-0141) | Layer 1 + Layer 2; Layer 3 on `/grid-live`: a trade cancelled after the Window, with a Selection reaching past it, drops the Selection | as stated |
| **LV-9** | MUST | A bundled source's Change Highlight: a cell is marked when a changed row's painted text differs from its previous version's, and every cell of an added row is marked — under `GridSource.Fetch`, a key the Consumer named as added and not painted before; a key not named is not marked whole, and with no keys named the source guesses as ADR-0141 says (D6 of 2026-10-06); a sort, a filter or a column change marks nothing; a change the format hides is not marked. One delegate is held across versions, so only the rows with new instances ask again (ADR-0141/0067/0068) | Layer 2 with a fake `TimeProvider` | as stated |
| **LV-10** | MUST | A Window that is vouched for — by a bundled source, by a Consumer's own source through `VouchesDistinctRows`, or by a Consumer that pushes its Window and declares it beside its Row Key — is not passed through the grid's check of the whole Window; a Window not vouched for is. On every render the Row Keys of the painted rows are checked, so a repeat that reaches the screen is refused by name, before Blazor's exception, vouched or not. A bundled source refuses a repeated Row Key by name, and ExPivot vouches for its report (ADR-0141, refined 2026-10-07) | Layer 1 + Layer 2 counting the key function's calls: a vouched Window of 10⁵ rows asks only the painted rows' keys; an unvouched one asks every row's; a vouched Window with a repeat among its painted rows is refused by name | as stated |
| **LV-11** | MUST | *(Rewritten 2026-10-07.)* A Cell Editor commit lands with the text typed even when the cell's painted text changed while the editor was open. Then the grid raises an Overwrite Notice naming the cell, the text the editor opened on and the text replaced, and the Edit Intent carries both texts. A commit over an unchanged cell raises no notice; Escape writes nothing (ADR-0142) | Layer 2; Layer 3 on the Server host through the latency proxy at 150 ms: a change made under the open editor, then Enter | the commit lands; one notice, with both texts |
| **LV-12** | MUST | *(Rewritten 2026-10-07.)* An Action press acts on the row it was pressed on, even when that row's values changed after it was painted: with a Row Key, the row under that key; without one, a press whose row component was disposed acts on the row at the told position while the Row Sequence Version is the one the press was taken under. It fires once, and no press is lost to Blazor. A press that no click follows — a macOS Ctrl+click, which opens the context menu — fires nothing, then or later *(2026-10-08)*; the press's row, column and action are read at the mousedown, with the paint (ADR-0142/0020/0021) | Layer 2 (a press told an earlier render, on a changed row, with and without a Row Key; a press told with no click); Layer 3 on the Server host through the latency proxy | fires once, on the row pressed |
| **LV-13** | MUST | *(Rewritten 2026-10-07.)* A paste, a Ctrl+Enter fill, a fill-handle drag, Delete, Ctrl+D and Ctrl+R land as entered over cells whose painted text changed between the render they were taken against and their handling. Nothing compares the target's or the source's painted text. The existing refusals still hold, and so does the refusal when the order moved, raised as `OrderMoved` for each of them, a fill-handle release included (ADR-0142; ADR-0011/0014/0035) | Layer 2; Layer 3 on the Server host through the latency proxy | as stated |
| **LV-14** | MUST | *(Rewritten 2026-10-07.)* A keyboard gesture carries the render it was taken against — its row order and layout — read by the capture-phase key listener or the clipboard read, and the grid uses it for where the gesture lands and which order it was aimed under, never to judge a value. No new JavaScript use is added, and nothing per cell reaches JavaScript (ADR-0142/0021; ADR-0027 P4) | inspect `ex-grid.js`; Layer 3 on the Server host at 150 ms: a Ctrl+V pressed on a paint that a newer render replaced before it landed, under the same order | the paste lands where it was aimed |
| **LV-16** | MUST | *(Rewritten 2026-10-07; widened 2026-10-08.)* Before a commit lands, the grid asks its bound source to put out what it has gathered: the commit is made on, and its Overwrite Notice compared with, the newest version. It asks again once the handler of any write it raised — an Edit, paste, fill or Clear Intent — has completed, refused or not, so a gathering source's write is in the Window before the next gesture is handled. `GridSource.From`'s `ReplaceRow` takes a commit made while it was gathering (ADR-0141/0142, D5 of 2026-10-06) | Layer 2 with a fake `TimeProvider`: a commit inside a gather interval, with the gathered change on another cell and on the edited cell; the publication after the handler | both land; the second raises the notice with the new text |
| **LV-17** | MUST | *(Rewritten 2026-10-07; its mechanism settled 2026-10-08.)* The user's own writes count as seen for the Overwrite Notice. A write the grid raised settles at a point in the grid's order of events, never a time or a text: once its handler has completed unrefused and a new Window has been taken in since it was raised — for a bound source, what the source puts out after the handler. An editor opened over a cell while one of the user's own writes to it was unsettled takes the cell's painted text at that write's settling as what it saw. So the same keys give the same outcome on both hosts, a write that leaves the text as it was never silences a later change, and the Edit Intent's `SeenText` and `ReplacedText` differ exactly when a notice is raised. What the editor saw is only ever read from the edited row; while the Window does not hold that row, the editor keeps what it saw before (2026-10-09). A write a Consumer declines without `Refuse` counts as accepted; ADR-0142's four accepted limits were accepted by the user on 2026-10-09 (ADR-0142, D1 of 2026-10-06; principle 6) | Layer 2: both orderings of `5` Enter ↑ `7` with an upstream change, a value retyped as it was, F2+Enter unchanged, a refused write, a pushed Window, a gathering and a fetching source, a Window arriving during the handler; Layer 3 on both hosts, the Server host at 0 and 150 ms: `5` Enter ↑ `7` Enter typed at once | both commits land and no notice is raised, on both hosts; a change made after the write settled is told |
| **LV-18** | MUST | `/grid-live-local` — `GridSource.From` keyed over a million trades, fed Change Batches in the browser — works on both hosts and in both browsers: a batch marks the cells it changed, a pause stops the batches, under a sort the painted rows stay in order, the code shown is the code run, and the console stays clean (CON-*) (ADR-0141, D8 of 2026-10-06) | Layer 3 (`grid-live-local.spec.mjs`) | as stated |
| **LV-19** | MUST | `GridEditIntent.Refuse(message)`, called before the `OnEdit` handler completes, refuses the commit: the editor stays open with the text typed, the message is shown at the editor and in the root's live region as a Reject's is, and a later commit raises the intent again. A handler that completes without calling it has accepted (ADR-0142/0034) | Layer 2 | as stated |
| **LV-20** | MUST | An open editor outlives an order move: with a Row Key, the editor and the Focus follow the row they were opened on, a commit lands on it, and the grid does not scroll to follow it; without one, the editor stays where it is and a commit is refused as `OrderMoved`. A commit whose Row Key is not in the Window is refused as `RowLeftTheWindow`, and lands once the key is back in the Window. Refused, no Edit Intent is raised, the editor stays open with the text typed, and Escape writes nothing. An Action press is refused for the same two reasons; with a Row Key, a press whose order moved before it was heard and whose row component is gone is refused as `OrderMoved`, never as `RowLeftTheWindow` (ADR-0142; ADR-0011's note of 2026-10-07). *(`RowLeftTheWindow` was `RowGone` until 2026-10-08: a row scrolled out of a pushed or fetched Window is not gone, and the name said it was.)* | Layer 2 | every clause a named test |
| **LV-21** | MUST | Chrome words an Overwrite Notice into the root's live region, as it words a refusal; the grid holds no string for it, and nothing is added to a row (ADR-0142; A11Y-16; ADR-0013) | Layer 2; Layer 3 against the reference Chrome | the notice is in the live region |
| **LV-22** | MUST | The grid holds no Consumer row instance, nor its Row Key, beyond the Window it was last given, apart from the Window last measured for Auto widths while the measure is deferred, the row of an Action press in flight and the Row Key of the row under an open editor — and the rows the render before the newest painted, which Blazor's renderer keeps as its previous frames: after a run of new Windows and a full collection, weak references to every earlier Window's rows are dead. Under ExPivot, after a run of live redraws, no engine report is alive but those its report source keeps by design: the `versionsKept` newest (two by default), and the reports the Change Highlight compares (each Source Version published within `ChangeHighlightDuration` of the newest, and the one before them). No display row is alive but those of the Window on screen and of the Window last served under the Report Version before it, the Baseline Window of the next Window Changes. Once the pivot renders again, nothing of an earlier Window is alive but the rows the grid painted from the Window before the newest, which Blazor's renderer keeps until the grid's next real render: a Consumer render that changes no parameter no longer renders the grid (UX-5), and the bound stays one render's painted rows (ADR-0160/0153/0152; restated 2026-10-08, when its test was built, decided with the user on 2026-10-09, and restated with the user on 2026-10-12) | Layer 2: weak references, with `CellAppearance` declared and not, a Row Key declared and not; ExPivot on `/pivot-live`'s generator, redraws a second and 250 ms apart, local and with its Windows crossing JSON | none alive but the named exceptions; under ExPivot the exact set kept, and after the pivot renders again no earlier Window's row but those the grid painted from the Window before the newest, with a Viewport shorter than the report |
| **LV-23** | OBSERVATIONAL | A live ExPivot of 101,001 report rows over 20 redraws in the browser (a published build without AOT): the managed heap after a full collection after each redraw, recorded beside the run of 2026-10-06 that grew by 41 MB a redraw (ADR-0160). *(On the merged code, `verification/2026-10-10-linux-merged-live-costs`: 94 MiB loaded, 137.6 MiB after the 20th redraw, flat from the 6th once the Change Highlight's reports reach ADR-0153's bound; `main` grew by 41.2 MiB a redraw in the same run.)* | recorded in `metrics.json` | recorded, never gated |
| **LV-24** | MUST | Local and server report computation use the same incremental engine and preserve the reference engine's results for the supported Aggregations and layouts. Local CSV works without uploading its data to a server. One complete batch is published at a time, and incremental results equal a fresh computation (ADR-0060/0067/0151) | Layer 1: local/server parity and a fresh-computation oracle over batch sequences; Layer 2: publication is atomic; targeted Layer 3: local CSV and server report paths | equal values, labels and row order; no partial batch |
| **LV-25** | MUST | The server report path sends the requested report Window and its changes, with the metadata needed to interpret them, rather than requiring the browser to receive all Leaf Aggregates and construct the whole Report. Report computation remains server-side; the complete report extent is represented even when its rows are outside the Window. The Consumer owns transport. A report that a layout change, a collapse, a filter or newer data shrinks below the Window asked for is shown at that Window clamped into it, and the grid is never handed a Window past the report's row count (ADR-0151; its section of 2026-10-09) | Layer 1 + Layer 2: inspect requested ranges and received payloads, including a batch affecting no displayed value; each shrink after scrolling, locally and over JSON; targeted Layer 3: scroll and apply changes through the server path, and `pivot-db`'s shrink under the Window scrolled to | correct Window and extent; no whole-report client construction; a shrunken report's last rows shown, no exception |
| **LV-26** | MUST | A remote custom Order Key selects a server-registered function; local computation uses its local function. Ordering semantics remain ADR-0060's. Unknown or unsupported policies are refused by name, never silently replaced with label order; a registered function that throws is refused by name across the wire (`PivotReportRefusalKind.OrderKeyFailed`). Culture, formats and display words are explicit and consistent across the report and its operations; Culture does not select the words; a change of `OrderKeyPolicies` on a shown report asks for the report again, and the same policies ask nothing (ADR-0152) | Layer 1: local/server ordering and settings parity, null keys, ties, Blank/error and policy refusals, a throwing function over JSON; Layer 2: the refusal is surfaced, and a changed policy reorders a shown report; serialization tests contain settings/policy identifiers, not delegates | equal semantics; no implicit fallback or language switch |
| **LV-27** | MUST | Window Changes are applied atomically only to the Report Version and request they advance, and only once verified: they carry the digest of the Window they produce (`PivotReportUpdate.WindowDigest`, over every row of the Window — its key, labels and shown texts — and the Window's extent and Report Version), and the client checks it before the result becomes current. A missing or mismatching digest, like a missing baseline, triggers automatic acquisition of a complete current Window for the active report; nothing of unverified Window Changes is painted. Obsolete replies are discarded; failed recovery leaves the last complete report identified as stale with Retry. Source Version equality alone does not admit Window Changes for a different report/layout/settings, and row order changes retain their existing Selection rules (ADR-0152; the digest decided with the user, 2026-10-08) | Layer 1 + Layer 2: gaps, duplicate/obsolete responses, discarded server state, changed settings under the same Source Version, superseding and failed recovery; Window Changes missing a subtotal's change, Window Changes with no digest, the digest across a JSON round trip; targeted Layer 3 through the remote source | no mixed-version, unverified or stale Window painted; deterministic recovery or named stale state |
| **LV-28** | MUST | Copy and the Selection Summary are answered against the Report Version captured for the operation, including targets outside the Window; an unavailable version is refused, never rebound to newer data. Details are answered against the Source Version the cell was shown under — the Source Records behind it — and stay available while the source holds that version, whatever layout gestures came since; refused only when that data is gone, and then as such (confirmed by the user, 2026-10-09). Window Changes that change nothing visible still advance version metadata and invalidates a Summary when its off-screen selected values changed (ADR-0152/0151; existing Copy, Summary and Details rules) | Layer 1 + Layer 2: off-screen selections, version advance with unchanged visible cells, unavailable old versions, racing replies, Details paged across a collapse and a sort; targeted Layer 3: remote Copy/Summary/Details | complete results under the requested version, or their named refusal; no truncation to the Window |
| **LV-29** | MUST | Incremental Pivot Sources update ordinary additions/replacements/removals, new/disappearing Items, spelling changes, value order, totals and percentage dependencies without unconditional whole-answer/Cube/Report reconstruction. Published versions remain immutable and share unchanged structure. All existing Aggregations preserve fresh-computation values, labels and order; broad numerical dependencies are recomputed in reference order. A computation a gesture cancels leaves the incremental state whole: the next update is still incremental and equals a fresh computation, wherever the cancellation landed (ADR-0153) | Layer 1 oracle over random batches and layouts, seeds recorded, plus named cases for non-invertible parts, denominator changes, Tabular boundary labels and column spans; a cancellation at every yield; public work/identity observations distinguish partial work from a fresh rebuild | exact reference equality; old versions unchanged; unrelated structure reused; rows read after a cancel bounded by the change |
| **LV-30** | MUST | Display rows and keys own no whole Report/Cube/branching axis graph. Unchanged rows keep their instance and skip rendering through a stable Change Highlight lookup; value-only version changes preserve Row Sequence Version, and version-only changes do not recreate rows. Semantic value changes hidden by formatting remain available without false highlights. The time a change is marked is the component's own, taken when it adopts a report that lists the change (`ChangeMarks`); a server's clock moves no mark (ADR-0153/0068) | Layer 1 detached row/key and old-version behavior; Layer 2 render counts, key reads, highlight onset/expiry with server clocks behind and ahead, metadata-only updates; LV-22 retention | every clause exercised; no report-wide repaint for a local change; the same onset and expiry whatever the server's clock |
| **LV-31** | MUST | Pivot Sources declare incremental changes or full refresh explicitly (`PivotReportUpdateMode`). The bundled Snapshot source is incremental. A full-refresh source is asked to refresh on a notice of newer data (confirmed by the user, 2026-10-09) and may recompute the complete aggregate result, but retains the same reference semantics, atomic/version behavior and Window-only client transport; it does not claim partial-recomputation guarantees (ADR-0153) | Layer 1 + Layer 2 with both declared capabilities; targeted remote browser scenario; public docs/examples explain the capability | correct equal answers and honest capability; no whole report transported to the browser |
| **LV-32** | MUST | A Source replaced by another instance drops what was aimed at it, whatever the two sources' Row Sequence Versions: the Selection is dropped, as for an order move; a paste, Delete, Ctrl+D, Ctrl+R, a fill-handle release or a fill key told a paint of the old source is refused as `PasteRefusalReason.SourceChanged`, and an Action press as `ActionRefusalReason.SourceChanged`; an open Cell Editor or Formula Bar edit is discarded as `EditDiscardReason.SourceChanged`; a replacement that a commit's own handler makes while its Edit Intent is heard is that commit's: accepted, the edit ends as committed with no discard and no Overwrite Notice, and refused, it is discarded once as `SourceChanged`; a Find answer from the old source is refused as `FindRefusalReason.SourceChanged`; a placement, the Selection Summary and a Mark intent from the old source move nothing in the new one; and a mark press made on what the old source painted marks nothing and raises `OnMarkRefused(MarkRefusalReason.SourceChanged)` (MK-9). ExSheet moves its Row Sequence Version when it opens a Sheet Document that is not one it emitted, so a write aimed at the old document is refused and the Selection dropped, and a document the Consumer hands in as its answer to a commit is that commit's (ADR-0142's note of 2026-10-08; ADR-0011; ADR-0048; decided with the user) | Layer 2: every gesture, with and without a Row Key, both sources at Row Sequence Version 0; a commit whose handler hands over another source, accepted and refused, through the Cell Editor, the Formula Bar and Ctrl+Enter; ExSheet's paste, Delete and Ctrl+D across a replaced document, its own document coming back, and a document handed in as a commit's answer | as stated |
| **LV-33** | MUST | A printable key, F2, Backspace or a composition's text, told a paint under which the Selection it was aimed with has since been dropped — by an order move or a replaced Source — opens nothing and writes nothing anywhere, and the run is said once through `OnEditDiscarded` as `OrderMoved` or `SourceChanged`. A key that moves or selects, aimed the same way, moves nothing and says nothing. None of them is taken as a first key on the empty Selection. With nothing selected, a key told a paint of a Source since replaced goes the same way; across an order move under the same Source it keeps the first-key rule (2026-10-09). Keys told a newer paint follow the first-key rule (ADR-0011's and ADR-0012's notes of 2026-10-08; decided with the user) | Layer 2 | `5` `0` `0` Enter aimed under a moved order writes nothing, anywhere, and is said once; the same for a replaced Source and for a composition's text; a key that moves, aimed the same way, moves nothing; keys told the new paint open an edit by the first-key rule; with nothing selected, `5` Enter at a replaced Source's paint writes nothing and is said once |
| **LV-34** | MUST | ExPivot over a remote report source reads its Window ahead by the source's `readAheadRows` either side, 60 by default and 0 for a local source, clamped into the report. A scroll within the Window held asks nothing; a scroll past it asks once. A question whose Window covers the range the grid needs stays in flight while newer ranges arrive. An answer that does not reach the range the grid needs, checked before widening, is refused by name. Window Changes and their Window Digest cover the whole widened Window. ExPivot sets its grid's Range Request Margin to the smaller of 10 and the read-ahead (ADR-0182, ADR-0183) | Layer 1 + Layer 2; Layer 3 counting the report source's questions while scrolling, never reading a time | as stated |
| **LV-15** | OBSERVATIONAL | 1,000 changes to 1,000,000 rows, from the source's apply to the frame that shows them, in the browser (a published build without AOT) on the in-process page (D8), against PV-21's 0.2 s; the grid's pass over a report's Row Keys against its pass over the instances, at 10⁴, 10⁵ and 4×10⁵ report rows; the bytes per live update on the Server host; the requery and the grid's pass per update at 10⁵ and 10⁶ rows (ADR-0140/0141) | recorded in `metrics.json` | recorded, never gated |
