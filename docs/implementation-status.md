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
scaling. That is **VZ-14**, and it is open.

**Every ADR from 0001 to 0035 now works through to the component**, except ADR-0034
(validation — accepted after this snapshot and unimplemented, below) and the two package
projects (0019/0030's `ExGrid.MudBlazor` / `ExGrid.Fluxor`), which the Definition of Done
places out of scope for the core. What remains is verification depth, not features — see
`verification/2026-09-01/results.md` for the honest pass/blocked ledger.

**ADR-0034 (validation) is accepted and unimplemented** — decided after this snapshot was
taken. Its criteria (ED-14…ED-18 and ED-20, and ED-12's Reject clause) stand failing by design: the
verdict seam, the message channel, the error popover and the bundled ruleset are not built.

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

## Working through to the component

| ADR | | Pinned by |
|---|---|---|
| 0001 | Push/pull entry points, Range Requests | `RangeRequestTests`, `GridSourceBindingTests` |
| 0002 / 0023 | Filtering — the `Filters` parameter, `OnFilterChanged`, the source path | `FilterChromeTests`, six operator files |
| 0003 | Plain-markup cells, row memoisation | `RowMemoisationTests` |
| 0004 | Both-axis virtualisation, Pinned Columns, the fling | `VirtualisationTests`, `FlingTests` |
| 0005 / 0014 | The clipboard — both routes, both formats, every refusal; the JS allowlist's fourth entry is in use | `ClipboardDataTests`, `ClipboardParseTests`, `ClipboardWiringTests`, `features.spec.mjs` (real clipboard) |
| 0006 / 0024 | Cell State, Row Kind | both layers |
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
| 0035 | **The Editable declaration gates writes** — a paste or Ctrl+Enter fill covering a non-editable column is refused whole, and the fill refusal is no longer silent (CP-16) | `PasteRuleTests`, `ClipboardWiringTests`, `CellEditorTests` |
| 0021 (fifth entry) / 0029 | **The hover band** — the cell under the pointer reported by JavaScript only when it changes, the band painted by the overlay like the Focus band, `HighlightHoverRow`, `--ex-row-hover-background` made real; off by default and then not even computed | `HoverBandTests`, `mud.spec.mjs` (UX-12, real mouse) |
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
- **Interactive mode's keyboard entry** (Space into a multi-action or Template cell,
  ADR-0020) is partial: Space fires a single action; entering *into* a cell's control by
  key is not wired (focus into plain-markup cells needs a decision the ADRs do not make).
  The way **out** is wired: Escape from any focusable descendant returns the keyboard to
  the grid (KB-18), read off the event target rather than a mode flag.
- **`--ex-row-hover-background`** (ADR-0029's hover token) could not work as written —
  rows are `pointer-events: none`, so `:hover` never matches them — and is now real by
  another route: the fifth allowlist entry of ADR-0021 reports the cell under the
  pointer on change, and the token colours an overlay band (the hover row above).
  ADR-0034's popover hover trigger is meant to consume the same report.
- *(FN-20 landed late in the run: `GetFocusedValue()` serves the formula-bar role.)*

## Verification

| Layer | | State |
|---|---|---|
| 1 | `tests/ExGrid.Tests` | 45 files, **412 pass** |
| 2 | `tests/ExGrid.Components` | 33 files, **306 pass** — including ST-1's randomised 500-operation run and MEM-1's allocation invariant |
| 3 | `tests/ExGrid.Browser` | **5 specs, 38 pass on `chrome`** — scrollbar, features, presentation, gestures (real-mouse reorder/resize, off-screen paste, 10MB paste, the IME guard), virtualisation, and the /cells key-gate pair (KB-18/19). `msedge` declared, unrunnable on this machine (no Edge) |
| — | `verification/2026-09-01/` | layer logs + `results.md` with the pass/blocked ledger |

## What is left, in the order that costs least

1. **The measurement harness** — MEM-2..7, PF-3..8, BIG-2..6, plus a fresh
   `spikes/render-bench` entry (PF-8), and the CON-3/6 instrumentation.
2. **The other machine** — an Edge run, and a Windows or Linux run for VZ-10's real
   clause; both are hard DoD requirements the development Mac cannot discharge.
3. Interactive mode's keyboard entry; the hover band's owed numbers — a
   `spikes/render-bench` mode for the band's paint, and a Blazor Server host to measure
   the pointer report under (ADR-0021, fifth entry; none exists in the repository).
4. `ExGrid.MudBlazor`'s remaining seams — the filter panel and the column menu as
   content inside the core's popover — and `Striped`, reserved until the core emits a
   row-parity class (ADR-0030).

## Where the exit criteria stand

**No open question in §21.** Settled this run, each with its trigger: the Action-Column
copy (empty cell, ADR-0005), the editor's classes and tokens (with the editor), the
header-click sort cycle (recorded in ADR-0012). Still reserved, triggers unfired: the
fill handle, right-click, `--ex-selection-outline`, the Wrapper seam order, ExSheet's
shape, the column band.
