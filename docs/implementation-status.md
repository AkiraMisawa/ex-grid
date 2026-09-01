# Implementation status

**A snapshot, not a contract.** `docs/definition-of-done.md` says what finished means; this says
how far along it is. Taken on **2026-09-01**, branch `dev/claude-code`, with:

```sh
nix develop -c dotnet test ExGrid.slnx     # 334 + 178 pass, 0 failed, exit 0
```

**16 of the 33 ADRs are working through to the component.** The rest are below, split by why.

## Working through to the component

| ADR | | Pinned by |
|---|---|---|
| 0001 | The Consumer pushes the Window; `OnRangeNeeded` asks | `RangeRequestTests` |
| 0003 | Cells are plain markup; rows memoise | `RowMemoisationTests` |
| 0004 | The per-frame cell budget, and the fling | `FlingTests` |
| 0004 | Virtualisation on both axes, and Pinned Columns | `VirtualisationTests`, `HorizontalVirtualisationTests`, `PinnedColumnTests` |
| 0006 | The Cell State vocabulary | both layers |
| 0008 | The selection Overlay and the mouse | `SelectionTests` |
| 0011 | Selection in index space, dropped on reorder | `RowIdentityKeyTests` |
| 0012 | The capture-phase keyboard — Move / Extend / Edge / Corner / Cycle / SelectAll / Engage / Leave | `GridKeyboardTests`, `GoToStartTests` |
| 0013 | Fixed row height, `ViewportGeometry`, `ViewportBox` (the Scrollbar Gutter) | `ScrollbarGutterTests` |
| 0016 | Auto width and the `####` decision | `OverflowRenderingTests` |
| 0018 | Instances are independent (a per-instance JS handle) | layer 3 |
| 0020 | Action and Template Columns, and Interactive mode | both layers |
| 0021 | The JS allowlist, three of four entries in use — `keydown`, scroll, `blur`, `ResizeObserver` | — |
| 0022 | `net8.0`, single-target | the project file |
| 0024 | Row Kind | both layers |
| 0025 | `FetchingGridSource`, and the `/server` page | `GridSourceFetchTests` |

## Pure logic only — the types and their tests exist; nothing is wired

The arithmetic is done and pinned in layer 1. What is missing is the parameter surface and the
component wiring, which is why each row names the thing that does not exist yet.

| ADR | | Missing |
|---|---|---|
| 0005 / 0014 | The clipboard — `ClipboardRules`, `CopyDecision`, `PasteDecision`, `PasteShape`, with `CopyRuleTests` and `PasteRuleTests` | **`ExGrid.razor` references none of them, and the JS side is unwritten** — the allowlist's fourth entry is an empty seat |
| 0002 / 0023 | Filtering — `FilterSpec`, `GridFilter`, `GridFilters`, six test files | **there is no `Filters` parameter** |
| 0023 | Sorting — `SortSpec`, `SortSemanticsTests` | **there is no `Sorts` or `OnSortChanged`**, and no header click |
| 0014 | The selected-cell count | the arithmetic is in `SelectionQueryTests`; there is no UI |

## Not started — the type does not exist

- **0007 / 0010** the Cell Editor (Overwrite / Caret)
- **0009** the filter panel
- **0010** the column menu
- **0015** the pager
- **0016** column resize by dragging (a guide line, applied on release), and the three-width `CellTextMetrics`
- **0011** column reordering by dragging (within a block only)
- **0012** PageUp / PageDown — `MoveByViewport` is not in `GridKeyKind`
- **0008** the edge-band auto-scroll (SL-12..15), and the Focus band (`HighlightFocusRow`)
- **0028** `GridMetrics`, `GridDensity`, `ViewportSize.Fill`
- **0029** the `forced-colors` block, `ex-header-group`, `ex-announce`, the editor's tokens
- **0031** `dir="ltr"` — **not on the root**
- **0032** Header Groups
- **0033** ARIA — not even `role="grid"`
- **0019 / 0030** `ExGrid.MudBlazor` and `ExGrid.Fluxor` — the projects do not exist; `src/` holds `ExGrid` alone

## Verification

| Layer | | State |
|---|---|---|
| 1 | `tests/ExGrid.Tests` | 32 files, **334 pass** |
| 2 | `tests/ExGrid.Components` | 17 files, **178 pass** |
| 3 | `tests/ExGrid.Browser` | **one spec (`scrollbar`), `chrome` only** — PRE-5 requires an `msedge` project as well |
| — | the three scratchpad suites (keys, cells, server) | not yet moved in (ADR-0026) |

## What is left, in the order that costs least

1. **Wiring only** — the clipboard, filtering, sorting. The logic is finished and tested; this is
   the highest return per hour in the repository.
2. **Specified, waiting on implementation** — the Page keys, column resize, column reordering, the
   three-width metrics, the edge-band auto-scroll, ARIA. Each has an ADR that says what to build.
3. **Designed, not started** — the editor, the filter panel, the column menu, the pager, Header
   Groups, Density, the Wrapper packages.

**Two that can be closed immediately**, depending on nothing else:

- `dir="ltr"` on the root — ADR-0031, one attribute, criterion DIR-1.
- the `msedge` project in `playwright.config.mjs` — PRE-5, and it is what makes the repository
  stop contradicting ADR-0017.

## Where the exit criteria stand

228 criteria, **0 reading `BLOCKED`**, no broken links, and no ADR without a criterion.

**No open question remains.** `docs/definition-of-done.md` §21.11 states the rule that separates
the two things that were both being called "open":

> A question is **open** when nobody has decided it *and* no ADR says when it will be decided.
> It is **reserved** when an ADR names the trigger — the feature whose construction settles it.

Nine reservations exist, listed in full in §21.11 so that none is omitted. One of them — a column
band (ADR-0008) — is the only entry whose trigger nobody has written down; it is recorded as such
rather than quietly promoted.
