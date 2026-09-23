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
| 0022 | `net8.0`, single-target | the project file |
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
| 1 | `tests/ExGrid.Tests` | 46 files, **446 pass** (2026-09-23) |
| 2 | `tests/ExGrid.Components` | 41 files, **405 pass** (2026-09-23) — including ST-1's randomised 500-operation run, MEM-1's allocation invariant and ADR-0037's `InteractiveTests` |
| 3 | `tests/ExGrid.Browser` | **6 specs, 140 pass** (2026-09-23) on `chrome` and `msedge` together, on Windows 11 at 150% scaling, including ADR-0037's tests and the new VZ-14 block. `scrollbar.spec.mjs` again at 125%, **8 pass** (2026-09-24) (see the Windows paragraph above). And **69 pass on `chrome`** on macOS (2026-09-23, Chrome 153, headed), after the Cmd+Enter fix the Windows run could not have seen |
| — | `verification/2026-09-01/` | layer logs + `results.md` with the pass/blocked ledger |
| — | `verification/2026-09-23-windows/` | the Windows layer-3 run: `results.md`, the 150% and 125% logs, `metrics.json` |
| — | `verification/2026-09-23-macos/` | the macOS `chrome` run's `metrics.json` |

## What is left, in the order that costs least

1. **The measurement harness** — MEM-2..7, PF-3..8, BIG-2..6, plus a fresh
   `spikes/render-bench` entry (PF-8), and the CON-3/6 instrumentation.
2. ~~**VZ-14 at 125%.**~~ Discharged on 2026-09-24 on a Windows desktop at 125%, on both
   browsers. (The Edge run and VZ-10, which this item used to hold, were discharged on
   2026-09-01.)
3. The hover band's owed numbers — a `spikes/render-bench` mode for the band's paint,
   and a Blazor Server host to measure the pointer report under (ADR-0021, fifth entry;
   none exists in the repository). *(Interactive mode's keyboard entry, which headed this
   item, is built — ADR-0037.)*
4. ~~**ADR-0037's layer 3 on the two target browsers.**~~ Discharged on 2026-09-23 by
   the Windows run: KB-20 to KB-27, A11Y-17 and UX-14 pass on `chrome` and `msedge`.
5. `ExGrid.MudBlazor`'s remaining seams — the filter panel and the column menu as
   content inside the core's popover — and `Striped`, reserved until the core emits a
   row-parity class (ADR-0030).

## Where the exit criteria stand

**No open question in §21.** Settled this run, each with its trigger: the Action-Column
copy (empty cell, ADR-0005), the editor's classes and tokens (with the editor), the
header-click sort cycle (recorded in ADR-0012). Still reserved, triggers unfired: the
fill handle, right-click, `--ex-selection-outline`, the Wrapper seam order, ExSheet's
shape, the column band.
