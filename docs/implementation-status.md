# Implementation status

**A snapshot, not a contract.** `docs/definition-of-done.md` says what finished means; this says
how far along it is. Taken on **2026-09-01** (end of the core-completion run), branch
`dev/claude-code`, with:

```sh
nix develop -c dotnet test ExGrid.slnx     # 412 + 306 pass, 0 failed, exit 0
cd tests/ExGrid.Browser && npx playwright test --project=chrome   # 38 pass (Edge absent here)
```

**Later the same day, ADR-0035** (paste and fill respect the Editable declaration) added 7 layer-1
and 4 layer-2 tests: **419 + 310 pass**. Its two layer-3 tests have since been run, and pass:
layer 3 came up on the machine the drop was made on — Linux (WSL2) — once both browsers were
installed: **80 pass, 0 failed, exit 0**, being 40 on `chrome` (152.0.7977.64) and 40 on
`msedge` (152.0.4191.53), each headed. ADR-0017's both-browsers clause is discharged for the
first time; every earlier run had one browser only.

That is also the first layer-3 run on a platform whose scrollbars occupy layout: **15 CSS px**
measured, against the 0 of the macOS machine the 2026-09-01 record was taken on. The zoom loop
that `scrollbar.spec.mjs` calls "where the answer lives" passes at device scale 1, 1.25 and 2.
**VZ-10 is discharged.** Its Pass column used to ask for "Windows and Linux" where §22 asked for
either, and §22 stated the reason: the test is a tautology against overlay scrollbars. The
criterion now names that property rather than the platform pair. What the run does not give is
the case behind the test's own "run it on Windows" comment — a real Windows desktop at 125%,
where the native scrollbar is a non-integer number of CSS pixels and the OS, not CDP, does the
scaling. That is **VZ-14**. It was discharged on 2026-09-24, on Windows at 125% (the Windows
paragraph below).

**Every ADR from 0001 to 0035 now works through to the component**, except ADR-0034
(validation — accepted after this snapshot and unimplemented, below) and the two package
projects (0019/0030's `ExGrid.MudBlazor` / `ExGrid.Fluxor`), which the Definition of Done
places out of scope for the core. What remains is verification depth, not features — see
`verification/2026-09-01/results.md` for the honest pass/blocked ledger — that record is the
macOS one; a run now files itself under `verification/<date>-<platform>/`.

**ADR-0034 (validation) and ADR-0036 (the context menu) are built**, along with the three
sections ADR-0005 gained on the way: copy with headers, the rule that a menu copy always takes
the asynchronous clipboard route, and the rule that no delimiter is ever guessed. ADR-0010 is
amended as ADR-0036 asked — `GridCommand` has no `Label`, and the built-in English wording sits
behind a `CommandLabel` seam — so the core holds no UI strings, which ADR-0035 had been claiming
before it was true.

CP-19's claim is no longer unverified: a menu copy writes without a permission prompt on Chrome
and on Edge, and layer 3 says so rather than the ADR assuming it.

The error popover's hover trigger (ED-17b) was left failing for one round and is now built. It
took **ADR-0021's fifth allowlist entry**: JavaScript hears the pointer moves and reports only the
stillness — and, since the Wrapper branch merged, a move onto another row, for the hover band —
because a Blazor handler on the Viewport is a wire round trip per frame on a Server
circuit — which the grid had already pinned as unaffordable in
`The_grid_does_not_listen_for_moves_until_a_drag_begins`, whose comment is the reason ADR-0021
asks for. The module decides nothing: it reports the offsets the browser hands it, and C# resolves
the cell and asks the Consumer.

**Same-day review round:** an adversarial 17-candidate review of this drop confirmed all
17 (pager-vs-pre-pager arithmetic, the editor's missing pointer teardown, and a cluster
of silently-wrong paths — ReplaceRow's value-equality lookup, the filter panel's count
comparison, the &nbsp; trim, the mute copy failure — plus AltGr, the dead interactive
branch, and two re-entered C#/CSS pairings). All 17 are fixed, each with its criterion:
VZ-13, FL-9, ED-12/13, KB-18/19, SL-16, PST-7, and the widened CON-8/KB-8. Stress-running
the zoom suite afterwards surfaced an **18th**, which the review had not seen: a reveal
judged against the scroll-event mirror was dropped while its predecessor's write was
still in flight, stranding the Focus off screen with no recovery — fixed by comparing
against the newer of last-event and last-write (recorded in ADR-0012), and pinned at L2.

**2026-09-23, ADR-0037 — entering a cell by key.** The last item on the "not in the core"
list below that the core itself owed. Space on a cell with several actions makes it
Interactive with the keyboard left on the root; Space on a Template cell hands its content
one focus request through the new `TemplateCellContext`; Space on an editable cell opens
Overwrite with the space in it; a held Space engages once; and the grid's action buttons no
longer hold the keyboard at all — out of the tab sequence, and not focused by a press either.
Run in a Linux cloud container with the .NET 10 SDK installed directly (no nix): layers 1 and 2
**446 + 403 pass**, plus the Wrapper's 17 — twenty of the component tests new, and each of the
rules they name was also broken on purpose once, to see its test fail. Layer 3: **69 pass,
0 failed — on the Playwright-bundled Chromium 1194, headless**, because the container has
neither Chrome nor Edge. Two settings were needed there that are not the project's:
Playwright's headless `--hide-scrollbars` dropped, without which the gutter tests correctly
refuse to pass ("measured 0x0" — confirmed identical on the untouched branch), and
`ignoreHTTPSErrors`, because the container's TLS-intercepting proxy is not a CA the browser
trusts. So KB-20 to KB-27, A11Y-17 and UX-14 are verified on Chromium only, and owe a run on
the `chrome` and `msedge` projects (item 4 below). No observational record was filed under
`verification/`: headless container timings are not comparable with that trend.

**2026-09-23, the first layer-3 run on Windows itself** — Chrome 153 and Edge 153 on
Windows 11, headed, with the DemoHost left in WSL2 and reached through localhost forwarding
(`verification/2026-09-23-windows/`). The full suite: **140 pass, 0 failed, 0 skipped**,
70 per browser. That discharges item 4 below: KB-20 to KB-27, A11Y-17 and UX-14 now pass on
`chrome` and `msedge`. There was a finding first. Playwright's default viewport is a CDP
emulation that pins `deviceScaleFactor` to 1, so on a desktop at 150% every existing test
measured DPR 1.00000003. Run as it stood, `scrollbar.spec.mjs` could never have answered
VZ-14, whatever the OS was set to. A VZ-14 block now turns the emulation off and refuses to
pass unless it is on Windows, at a fractional DPR, with bars that occupy layout. At 150% it
passes on both browsers: DPR 1.5, and the gutter the grid is told is **15.34375 CSS px**
(23 device px), a non-integer as VZ-14 supposes. After a sign-out to apply 125%, it passes
there too: DPR 1.25, gutter **15.203125 CSS px** (19 device px), on both browsers. **VZ-14 is
discharged.** The first 125% attempt lost one Chrome test at launch. Chrome exited with
code 0 before a page existed, and by the evidence it was applying an update that had been
waiting for the first launch after the sign-in. The rerun passed 8 of 8, and the record
keeps both logs.

**Later on 2026-09-23, the `chrome` half — on macOS.** Layer 3 ran on the development Mac
(macOS 26.6.2, Google Chrome 153.0.8010.53, headed, nothing changed in the configuration):
KB-20 to KB-27, A11Y-17 and UX-14 pass on `chrome` there too. The same run failed one older test, deterministically — CP-16's Ctrl+Enter fill,
written and passed on Linux, and never before run on a Mac, where Playwright's
`ControlOrMeta` presses Command. The product was wrong, not the test: the editor's keys
were read from the raw Control flag and never went through `GridKeys.Canonical`, so on a
Mac Cmd+Enter was a plain Enter — one cell committed and the Focus moved on, the rest of
the selection silently left unfilled — and off a Mac a Win+Enter committed too.
`OnEditingKeyAsync` now switches on the canonical form, with two layer-2 tests seen
failing first (`CellEditorTests`, Cmd+Enter fills where Meta is Command; Meta+Enter does
nothing where it is not), and **KB-3** is widened to name every key path, the editor's
included. After the fix: layers 1 and 2 **446 + 405** plus the Wrapper's 17, layer 3
**69 of 69** on `chrome`. `verification/2026-09-23-macos/metrics.json` is the run's own
record; against the 2026-09-01 macOS one, BIG-7 moved from 374 to 481 ms (+29%) on a
machine in ordinary use — observational, recorded as a note and not investigated.

**2026-09-24, the measurement harness's layer-2 half.** Every MUST in it that layer 2 can
check now has a test. Writing them found nine defects, and two rounds of review found four
more, two of them introduced by the fixes. All thirteen are fixed, and all but one with its
test seen failing first; the one is named below. The review also found two tests too weak to
fail — MEM-3 counted disposals but not creations, and nothing counted a fetch's token source —
and both are strengthened.

- **PF-3** — `RenderAllocationTests` measures a re-render's bytes as a slope between 4 and 16
  columns, so fixed costs cancel and only what scales with the painted cells is left. It found
  the grid composing per cell, per render: each cell's **id** (now cached per row, keyed on
  what composes it), **`aria-colindex`** (an int boxed and turned into a string on every cell
  and header cell; now interned), the run of **`####`** (interned by length — filled on demand
  since the review, because the first version filled every shorter run as it grew, some 25 MB
  of characters to reach one very wide column's), and an action's **class**. It also found
  per-cell allocations that are not strings: a **closure per header cell** (a captured index
  declared at the top of the loop, allocated with no button painted), **an enumerator per
  header cell** whenever a sort is applied (`aria-sort` iterated the interface), a closure per
  Action or Template cell (40 bytes; measured, fixed, and **not pinned** — every cell it
  touches also pays Blazor's directive cost, and no pair of grids differs in the closure
  alone), and — the costly one — **a new click handler per action button per render**: a
  changed attribute, so the diff registered a fresh event for every button on every render of
  its row and sent each to the browser (~50 KB per cell per render under bUnit). The review
  found **the header's menu buttons and resize grips doing the same on every render of the
  root** — every vertical scroll frame, on any grid with a sort, filter, width or Source wired
  — and their handlers are now held per column index as well. Held handlers survive a render
  that leaves a button in place; a sideways scroll that shifts the painted columns still
  re-registers the scrollable buttons it moves, because the diff compares by position. The
  second round found **header groups composing a string per leaf and per rectangle on every
  render of the root** (a leaf's height, a rectangle's box, `aria-colspan`); they are now
  cached alongside the geometry, layout and tier height that produce them. It also found that holding the
  action's handler as a bare delegate had dropped the row as its **receiver**, which is how the
  renderer finds an `ErrorBoundary` for a handler that throws: a Consumer's failing `OnAction`
  would have gone unhandled — on Server, fatal to the circuit. The row is its receiver again;
  that costs one small boxed callback per button per render, which is not a string, and the
  held delegate keeps the callback equal to the last, so the diff keeps its event. Pinned by
  `A_failing_action_is_caught_by_the_error_boundary_around_the_grid`, and by handler ids that
  survive a render for both the actions and the header. What an event directive still costs
  is Blazor composing its internal attribute name; that is the framework's, and stays —
  ADR-0027 now records P5's scope and why pre-composing the names would be quietly wrong on a
  newer runtime (ADR-0022). The measurement is the least of ten runs: in the full suite a few
  kilobytes landed in one run about one time in four, and never with tiered compilation off.
- **PF-4** and **MEM-4**'s layer-2 half were already pinned, and now say so in their comments;
  **BIG-4** gained a test at the Definition of Done's own numbers (10⁶ rows fit at 28px and are
  refused by name at 40px).
- **PF-5** — the whole result selected costs what eleven cells cost: the same element count,
  one range, no row re-rendered.
- **MEM-3** — `ResourceDisposalTests`, over a counting JavaScript runtime that answers every
  import and attach with a new reference, and a counting clock: created equals disposed, one
  for one, for every module, handle, .NET reference and all four timers. It found **the module
  leaked when disposal overtook its import** — `DisposeAsync` saw no module yet, and the
  import's continuation returned without releasing it. Fixed. The grid creates no token
  source; a fetching source creates one per fetch, and `GridSourceFetchTests` now shows each
  disposed however its fetch ended — answered, superseded, failed or cut off by disposal —
  which ASY-2 had claimed and nothing had checked.
- **MEM-7** (observational) — **51,014 bytes per one-row scroll frame**, 6 rows × 2 columns,
  under bUnit's renderer; recorded in `verification/2026-09-24-macos/metrics.json` as §22
  Step 6 now says.

MEM-5's ten-minute soak runs only under `EXGRID_SOAK=1` — decided 2026-09-24, and now written
into §22 Step 4 along with the layer-3 half that implements it (below).

**2026-09-24, the Definition of Done's row counts, and the harness's layer-3 half.** The
user decided that the row counts are the Definition of Done's, not the DemoHost's. `/wide`
had been 100,000 rows while §12 names 1,000,000, so every BIG test ran as "BIG-1-shaped" at a
tenth of the scale. VZ-1 compared scroll positions rather than 10³, 10⁵ and 10⁶. ST-1 ran at
200 and 100,000 rows where §22 Step 5 says 10³ and 10⁶. Now:

- **`/wide` is 10⁶ rows**, and takes `?rows=N` for the criteria that compare one Viewport
  across totals. Its rows are made on first request and kept, so a million are never built up
  front and a row answered twice is the same instance (ADR-0003).
- **ST-1 runs at 10³ and 10⁶** (the 200-row seeds stay). The 10⁶ case costs about a minute,
  and layer 2 went from ~15 s to ~80 s. Timed per operation, nearly all of that is the
  reference source re-sorting (~0.55 s) and re-filtering (~0.38 s) a million rows. That is the
  Consumer's work (ADR-0001), and the grid's own steps stay in milliseconds. **Decided the
  same day: it runs only under `EXGRID_ST1_MILLION=1`**, as the soak does. An ordinary layer 2
  skips it by name (4.5 s for the class), and §22 Step 5 is the run that sets it (67 s, 5 of 5).
  Step 2's "zero skips" now names this one exception.
- **`fixtures.mjs`** gives every spec one console capture, writing `console.json`. It covers
  CON-1/2 as before, plus **CON-3** (a warning from ExGrid's own code: served from
  `_content/ExGrid*`, prefixed `[ex-grid]`, or naming an `ExGrid.` category) and **CON-6**
  (an unhandled exception at any level). For CON-6 the DemoHost is WebAssembly, so its host's
  log *is* the browser console. The dev server's own output now goes into the run's output,
  which Step 4 tees. `scrollbar.spec.mjs` had no console check at all and now has one.
- **`virtualisation.spec.mjs`**, at 10⁶ rows, covers:
  - **BIG-1**.
  - **VZ-1 / BIG-2 / DOM-1**: the element count at 10³, 10⁵ and 10⁶ rows is identical,
    not "within 300".
  - **BIG-5**: the first and last rows' data checked against the generator, there and back.
  - **BIG-3**: Ctrl+A shows 10⁸, one rectangle, and the next key is answered.
  - **PF-1's layer-3 half.** Until now it was discharged by the grep alone. Every crossing
    between the module and .NET is counted in both directions, by CDP breakpoints that never
    pause. They are placed from the source the page was served, and a site that cannot be
    placed fails the test. At most one call per scroll frame, for rows, columns and a fling.
    The reading taken: "interop" is the grid's own. Blazor's delegated `@onscroll` dispatch is
    the framework's, one per event by construction, as ADR-0027 scopes P5.
- **`memory.spec.mjs`**, on the new `/lifecycle` page, covers:
  - **MEM-2**: fifty mounts and disposes, nodes and listeners within ±2 — measured 0 and 0.
    Its baseline is taken after one warm-up cycle, and that is checked, not assumed. On a
    page's first use of an event name, Blazor adds one delegated listener to the document and
    keeps it. The test asserts that the warm-up added exactly those (`scroll`, `mouseleave`,
    `mousedown`, `contextmenu` from `blazor.webassembly.js`) and no node.
  - **MEM-4**: the root carries the module's five listeners, has none after dispose, and the
    count returns exactly.
  - **MEM-5/MEM-6**, under `EXGRID_SOAK=1`: a seeded ten-minute scroll, sampled every 30 s
    after a forced GC. The managed heap is read through a `[JSInvokable]` host counter,
    because CDP cannot see WebAssembly's linear memory.
- **`observational.spec.mjs`** records into `metrics.json`: BIG-7 at 10⁶; DOM-5 at both
  settings; PF-6 (the settle repaint from `Performance.getMetrics` deltas, with the fling
  first let paint its Placeholders, plus frame intervals); BIG-6 as its "on" half; and PF-7,
  a 10 × 7 drag edge moved a row a step. PF-7 is 10 × 7, not ADR-0008's 10 × 8, because only
  seven scrollable columns fit whole beside the pinned block. It asserts the selection moved
  on every step. Its first version measured a pointer outside the window and still recorded
  "costs". These tests use a 1280×1000 window: the default 720px one leaves the grid's lower
  rows off screen.

The new checks whose failure is not plain from their assertion were each seen failing, with the product broken on purpose:
- a second offset read per scroll event (PF-1: 120 calls for 60 frames);
- a listener left on `window` per attach (MEM-2's warm-up check);
- the key listener left on dispose (MEM-4);
- an `[ex-grid]` warning (CON-3), where a third-party one passes;
- an "Unhandled exception" logged at info (CON-6).

**Where this ran, and what it therefore does not discharge.** It ran in a Linux cloud
container with the .NET 10 SDK installed directly:
- Layers 1 and 2: **449 + 421** plus the Wrapper's 17.
- Layer 3: **76 pass, 2 skipped** (VZ-14, which is Windows-only, and the soak), 0 failed.
- The soak run on its own: **pass**. The JS heap went 4.01 → 4.11 MB over 35,206 frames,
  levelling off after five minutes. The last sample is 0.4% from the median. The managed
  heap went 9.29 → 9.38 MB.

All of it was on the Playwright-bundled Chromium 1194, headless, with the same two local
settings as the 2026-09-23 container run (`--hide-scrollbars` dropped, `ignoreHTTPSErrors`),
kept in an uncommitted config. So none of the new MUSTs has met `chrome` or `msedge` yet. The
numbers were not filed under `verification/`, because software rendering belongs to no trend
(for scale only: settle repaint 20 ms on / 120 ms off, drag step 5.4 ms median).

## Working through to the component

| ADR | | Pinned by |
|---|---|---|
| 0001 | Push/pull entry points, Range Requests | `RangeRequestTests`, `GridSourceBindingTests` |
| 0002 / 0023 | Filtering — the `Filters` parameter, `OnFilterChanged`, the source path | `FilterChromeTests`, six operator files |
| 0003 | Plain-markup cells, row memoisation | `RowMemoisationTests` |
| 0004 | Both-axis virtualisation, Pinned Columns, the fling | `VirtualisationTests`, `FlingTests` |
| 0005 / 0014 | The clipboard — both routes, both formats, every refusal; the JS allowlist's fourth entry is in use | `ClipboardDataTests`, `ClipboardParseTests`, `ClipboardWiringTests`, `features.spec.mjs` (real clipboard) |
| 0006 / 0024 | Cell State, Row Kind; **the column's display format** — one text for a value, in the cell, copy's `text/plain`, the value list and the editor, never in the raw `text/html`; **the tone rule** — the Consumer's closed-enum answer about a value, painted as `ex-tone-*`, coloured by tokens the bare grid leaves at `inherit` | both layers, `ColumnFormatTests`, `CellToneTests`, `mud.spec.mjs` (FN-7a) |
| 0007 / 0010 | **The Cell Editor** — Overwrite / Caret, F2, the typed-first character, Ctrl+Enter fill, the mode-gated key listener | `CellEditorTests`, `features.spec.mjs` (real keys) |
| 0008 | Selection overlay, the mouse, **the edge-band auto-scroll**, **the Focus band** | `SelectionTests`, `EdgeAutoScrollTests`, `FocusBandTests` |
| 0009 / 0010 | **The Chrome seams** — filter panel, column menu, editor, loading; `IGridChrome`; distinct values with the Excel exclusion rule; popovers dismiss by toggle, Escape and click-away | `FilterChromeTests`, `DistinctValueTests` |
| 0011 | Index-space selection; **column reordering by dragging** (blocks and groups clamp) | `HeaderReorderTests`, `ColumnGestureTests` |
| 0012 | The keyboard — including **PageUp / PageDown** (`MoveByViewport`) and **header-click sorting** with the settled cycle | `GridKeyboardTests`, `MoveByViewportTests`, `SortWiringTests` |
| 0013 / 0021 | Fixed row height, `ViewportBox`, the Scrollbar Gutter | `ScrollbarGutterTests`, `scrollbar.spec.mjs` |
| 0015 | **The pager**, the page-context Ctrl+A, the off-screen-selection status line | `PagerTests` |
| 0016 | Auto width, `####`, **resize by dragging**, **the three-width `CellTextMetrics`**, **the alignment enum** | `AutoWidthTests`, `CellTextMetricsClassTests`, `ColumnGestureTests` |
| 0017 / 0026 | Both browser projects declared (`chrome`, `msedge`) | `playwright.config.mjs` |
| 0018 | Instance independence, per-instance ids | `features.spec.mjs` (two grids) |
| 0020 | Action and Template Columns | both layers |
| 0022 | `net10.0`, single-target (rewritten from `net8.0` on 2026-09-25) | the project file |
| 0025 | `FetchingGridSource` (+ copy rows, + distinct values delegate); `InMemoryGridSource.ReplaceRow` — the in-memory Consumer's apply (deliberately *not* ADR-0007's Overlay application; recorded there) | `GridSourceFetchTests`, `ReplaceRowTests` |
| 0027 / 0028 / 0029 | **`GridMetrics`, `GridDensity`, `ViewportSize.Fill`**, inline Geometry Tokens, the token vocabulary, the forced-colors block | `GridMetricsTests`, `GridMetricsWiringTests` |
| 0031 | `dir="ltr"` on the root | `GridRenderingTests` |
| 0032 | **Header Groups** — rectangles, refusals, the band, group/leaf drag units | `HeaderGroupTests`, `HeaderGroupRenderingTests` |
| 0033 | **ARIA** — the root surface, absolute indices, `aria-activedescendant`, the live region | `AccessibilityTests` |
| 0034 | **The verdict seam** — Accept/Flag/Reject at the commit, the editor holding under a Reject, the message channel and its popover, the fill's verdict, and the bundled ruleset. Not the hover trigger (ED-17b) | `ValidationTests`, `GridRulesetTests`, `features.spec.mjs` |
| 0036 | **The context menu** — the secondary click's meaning, the core's clipboard commands and the Consumer's, the keyboard trigger, and `GridCommand` losing its label | `ContextMenuTests`, `FilterChromeTests`, `features.spec.mjs` |
| 0035 | **The Editable declaration gates writes** — a paste or Ctrl+Enter fill covering a non-editable column is refused whole, and the fill refusal is no longer silent (CP-16) | `PasteRuleTests`, `ClipboardWiringTests`, `CellEditorTests` |
| 0021 (fifth entry) / 0029 | **The hover band** — the pointer reported by JavaScript only when it moves onto another row (offsets; the cell is resolved in C#), the band painted by the overlay like the Focus band, `HighlightHoverRow`, `--ex-row-hover-background` made real; off by default and then not even computed | `HoverBandTests`, `mud.spec.mjs` (UX-13, real mouse) |
| 0037 | **Entering a cell by key** — Space on a cell with several actions makes it Interactive with the keyboard left on the root (the arrows, Home and End choose; Space fires once and leaves; Enter, Tab and every other key leave and keep their meaning; `aria-activedescendant` names the chosen button, painted `ex-action-chosen`); Space on a Template cell hands its content one `FocusRequest` through `TemplateCellContext` and the Consumer's control focuses itself; Space on an editable cell opens Overwrite with the space; a held Space engages once; the action buttons leave the tab sequence | `InteractiveTests`, `ShippedStylesheetTests`, `features.spec.mjs` (real keys on `/cells`) |
| 0030 | **`GridPresentationDefaults`** — the one cascaded value a Wrapper hands down: glyph widths at their measured size, a default Density, a default for the hover switch; an explicit parameter beats each | `GridPresentationDefaultsTests`, `PresentationDefaultsWiringTests` |

## Not in the core, by decision

- **0019 / 0030** `ExGrid.Fluxor` — not started. **`ExGrid.MudBlazor` exists** as the
  proof of the Wrapper boundary (`src/ExGrid.MudBlazor`, `tests/ExGrid.MudBlazor.Tests`,
  the `/mud` page and `mud.spec.mjs`): `MudExGridPaper` (elevation, corners, outline,
  bordered, a toolbar slot, Dense and Hover cascaded as defaults), Roboto's measured
  widths, the Visual Tokens mapped onto MudBlazor's palette variables in the Wrapper's
  stylesheet, and `MudGridChrome` filling two seams — the Cell Editor (a bare input in
  the core's box) and the loading bar. The filter panel and the column menu still fall
  back to the core's. Nothing in `src/ExGrid` references MudBlazor (PRE-4).
- ~~**Interactive mode's keyboard entry**~~ — **built** (2026-09-23), once
  [ADR-0037](adr/0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)
  made the decision this entry said was missing: the core never reaches into content it
  did not render. Over its own actions the keyboard stays on the root; a Template's
  control is asked, and focuses itself. The template's signature changed to receive a
  `TemplateCellContext` for it.
- **`--ex-row-hover-background`** (ADR-0029's hover token) could not work as written —
  rows are `pointer-events: none`, so `:hover` never matches them — and is now real by
  another route: the fifth allowlist entry of ADR-0021 reports the pointer moving onto another
  row, and the token colours an overlay band (the hover row above).
  ADR-0034's popover hover trigger consumes the same listener's other report, the rest.
- *(FN-20 landed late in the run: `GetFocusedValue()` serves the formula-bar role.)*

## Verification

| Layer | | State |
|---|---|---|
| 1 | `tests/ExGrid.Tests` | 46 files, **449 pass** (2026-09-24) |
| 2 | `tests/ExGrid.Components` | 43 files, **420 pass, 1 skipped by name** (2026-09-24): ST-1's 10⁶ case, which runs under `EXGRID_ST1_MILLION=1` (§22 Step 5). Including ST-1's randomised 500-operation run at 10³ rows, MEM-1's allocation invariant, PF-3's `RenderAllocationTests`, MEM-3's `ResourceDisposalTests` and ADR-0037's `InteractiveTests` |
| 3 | `tests/ExGrid.Browser` | **6 specs, 140 pass** (2026-09-23) on `chrome` and `msedge` together, on Windows 11 at 150% scaling, including ADR-0037's tests and the new VZ-14 block. `scrollbar.spec.mjs` again at 125%, **8 pass** (2026-09-24) (see the Windows paragraph above). And on macOS, `chrome` only (Chrome 153, headed): **69 pass** on 2026-09-23 after the Cmd+Enter fix the Windows run could not have seen, and again on 2026-09-24 after the layer-2 harness's fixes, which touch the action buttons, the header and every cell's id — so those fixes have not yet met `msedge`. The harness's layer-3 half (**8 specs, 78 tests**) has run only on the container's bundled Chromium: **76 pass, 2 skipped**, and the soak separately (2026-09-24) |
| — | `verification/2026-09-01/` | layer logs + `results.md` with the pass/blocked ledger |
| — | `verification/2026-09-23-windows/` | the Windows layer-3 run: `results.md`, the 150% and 125% logs, `metrics.json` |
| — | `verification/2026-09-23-macos/`, `verification/2026-09-24-macos/` | the macOS `chrome` runs' `metrics.json` — the second with MEM-7 under `layer2` |

**2026-09-24, PRE-4 rewritten.** Its check grepped all of `src/` for `Mud` and `Fluxor`. Since
`src/ExGrid.MudBlazor/` lives there (ADR-0019), and a comment in the core names `MudDataGrid`,
the check as written could no longer pass, although the property held. It now names the core,
`src/ExGrid/`: no project reference, and no `using` of `MudBlazor` or `Fluxor`. Both come back
clean. The property is unchanged: the dependency points one way.

## What is left, in the order that costs least

1. **The measurement harness's layer-3 half — written (2026-09-24), not yet run where it
   counts.** It has run only on the container's Chromium (above). What discharges it is a
   Step 4 run on `chrome` and `msedge`, on Windows or Linux, with the soak (`EXGRID_SOAK=1`)
   once per browser and `metrics.json` / `console.json` filed. That run also takes the
   2026-09-24 layer-2 fixes to `msedge` for the first time. Then a fresh `spikes/render-bench`
   entry (PF-8), on real hardware.
2. ~~**VZ-14 at 125%.**~~ Discharged on 2026-09-24 on a Windows desktop at 125%, on both
   browsers. (The Edge run and VZ-10, which this item used to hold, were discharged on
   2026-09-01.)
3. The hover band's owed numbers — a `spikes/render-bench` mode for the band's paint,
   and a Blazor Server host to measure the pointer report under (ADR-0021, fifth entry;
   none exists in the repository). *(Interactive mode's keyboard entry, which headed this
   item, is built — ADR-0037.)*
4. ~~**ADR-0037's layer 3 on the two target browsers.**~~ Discharged on 2026-09-23 by
   the Windows run: KB-20 to KB-27, A11Y-17 and UX-14 pass on `chrome` and `msedge`.
5. **`ExGrid.MudBlazor`'s remaining seams, and Row Stripes — decided and built
   2026-09-24; verified on the container's Chromium only.** A grilling session settled them; the decisions are
   [ADR-0038](adr/0038-row-stripes-are-painted-from-the-rows-absolute-position.md) (Row
   Stripes, reversing ADR-0027's "not offered"),
   [ADR-0039](adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)
   (popovers take the keyboard — a core gap: no key opened the column menu, and no popover
   could be used without a pointer — and may hold Inner Popups, replacing ADR-0030's "never a
   `MudPopover`"), and the rewritten passages of ADR-0010/0012/0021/0027/0029/0030/0036. The
   criteria are FN-17/21, UX-15/16, A11Y-19, KB-28..32, RR-13 and the new §23 (WR-1..9); the
   work is ticketed as vertical slices (GitHub issues #3–#12) and built against a
   proof-of-concept MudBlazor page in the DemoHost. **Built so far:** the core half of
   ADR-0039 — `Alt+↓`, `FocusRequest` on the three popover contexts, the built-in Chrome
   focusing its first item, focus returned to the root on every close, the menus' and
   panel's roles and names (KB-28/29/32, A11Y-19), and the keys inside the built-in menus
   and panel (KB-30/31): the table is `MenuKeys`, public so a substituted Chrome answers to
   the same one; the panel's Tab wraps through two focus sentinels, and its Enter is the
   browser's implicit form submission, which leaves a composing IME alone where a Blazor key
   handler could not tell (`MenuKeysTests`, `PopoverKeyboardTests`, `features.spec.mjs`).
   Each popover is now keyed by its opening: one column's menu opened straight over
   another's kept the same buttons through the diff, and nothing took the keyboard. And Row
   Stripes in the core (UX-15/16, RR-13; `RowStripeTests`, `stripes.spec.mjs` on the new
   `/stripes` page, which reads the painted colours from a screenshot). The proof-of-concept
   page, `/mud-app`, and the `/features?chrome=mud` switch are in (T4, `mud-app.spec.mjs`,
   WR-8's `WrapperScriptTests`). `MudGridChrome` fills the column menu and the Context Menu
   with `MudButton` items, a Material icon each, and answers to `MenuKeys` (WR-4;
   `MudMenuTests`), and `popovers.spec.mjs` runs every popover test under both Chromes
   (WR-5 for the menus). Two core defects surfaced on the way and are fixed: a command a
   substituted Chrome invoked never closed its menu — the core now hands out commands that
   close themselves, since closing is its decision (ADR-0010) — and "Pin up to this column"
   was offered where the pin would cut a Header Group, which the grid then refused by
   taking the page down; it is now disabled there (ADR-0032). `MudExGridPaper.Striped`
   cascades Row Stripes and the Wrapper's stylesheet colours them from the palette's
   table-stripe colour (WR-6; `MudExGridPaperTests`, `mud-app.spec.mjs`). `MudGridChrome`
   now fills the filter panel as well (WR-1/2/3). A value list of `MudCheckBox`es with a
   search and a Blank entry. A condition form: a `MudSelect` operator offering exactly
   `Allowed`, and the type's own operand control, with Apply held until the operand is
   given. MudBlazor's words where it has keys, the Chrome's `Label` elsewhere
   (`MudFilterPanelTests`). What a panel's choices mean moved into the core as
   `FilterPanelChoices`, which the built-in panel uses too, so the same choices make the
   same `FilterSpec` under either Chrome. Two more core defects surfaced and are fixed:
   - `In` chosen in the built-in condition form applied a clause the engine refuses.
   - Escape from inside a popover returned focus before the render that removed it, so a
     `MudSelect` pulled focus back and it fell to `<body>`.

   `FilterPanelContext` carries the column's `Format`, so a substituted value list shows
   values as the cells do.

   **Two decisions, taken 2026-09-24 after the browser disagreed with the records:**
   - **Popovers stay inside their grid's box** ([ADR-0040](adr/0040-a-popover-stays-inside-its-grids-box.md)).
     A grid inside a `MudDialog` had its column menu cut off by the dialog's scrolling
     content. ADR-0017/0018/0021 had chosen the Popover API and CSS Anchor Positioning, "no
     script". What had been built was an in-root popover, and nothing recorded the
     difference. Measured: the top layer needs `showPopover()` for key- and right-click
     opens, and buries MudBlazor's popups. `position: fixed` with anchors is still cut by
     `.mud-dialog`'s transform. The core now writes each popover's `max-height` from its own
     geometry, and the Context Menu opens on the side with more room. A panel's value list
     is what gives. WR-7's dialog clause and UX-11 were restated to match.
   - **Escape closes an Inner Popup first**, as ADR-0039 intended. Its prediction of *how*
     was wrong: MudBlazor keeps DOM focus on the control while its popup is open, so the
     grid took the Escape and closed both. The contexts now carry `InnerPopupChanged`, the
     Wrapper's panel reports its selects' lists and its date calendar, and while one is open
     the capture-phase gate leaves a descendant's Escape to it. This is one more state of an
     allowlisted listener, not a new use.

   FN-21 is now observed clause by clause (`popovers.spec.mjs`), `ModalOverlay` included
   (`/features?chrome=mud&modal=1`).

   **A review of the whole change** (the `code-review` pass, 10 findings) fixed:
   - A menu opened while a panel's value list was still loading never took the keyboard.
     When that list's command finally completed, it closed the menu standing by then. A
     command now closes only the popover it ran from, and every opening starts clean.
   - A popover on the rightmost column could hang past the grid's right edge. It is now
     clamped by the widest it may grow, and that 320px moved from the stylesheet to the
     inline style beside its 200px floor. The Wrapper panel's own 240px minimum went.
   - TooMany overwrote an operator the user had picked while the list loaded.
   - A substituted panel's value list was queried twice per opening.
   - A reported Inner Popup could outlive its popover.

   Not taken: a per-opening cache of the command list, since the context was already
   rebuilt on every render.

   **Runs still owed, not passed:**
   - Every layer-3 test added on 2026-09-24 has run only on the container's bundled
     Chromium, headless, with the two local settings that are not the project's. That is
     `popovers.spec.mjs`, `stripes.spec.mjs`, the new `mud-app.spec.mjs` and the ADR-0039
     half of `features.spec.mjs`. They still owe a `chrome` and an `msedge` run on Windows or
     Linux (Step 4), headed.
   - The soak (`EXGRID_SOAK=1`), per browser.
   - A real IME is not reachable from the container: the claim that Enter confirming a
     candidate does not apply a filter rests on the browser's implicit-submission rule, and
     is owed a manual check with a Japanese IME on both browsers.

## Where the exit criteria stand

**No open question in §21.** Settled this run, each with its trigger: the Action-Column
copy (empty cell, ADR-0005), the editor's classes and tokens (with the editor), the
header-click sort cycle (recorded in ADR-0012). Still reserved, triggers unfired: the
fill handle, `--ex-selection-outline`, ExSheet's shape, the column band. *(Since then:
right-click was settled by the Context Menu, ADR-0036; the Wrapper seam order by the
package's start and ADR-0039, 2026-09-24.)*
