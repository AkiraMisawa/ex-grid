# 02: A test for each criterion that has none of its own

Status: ready-for-agent
Roadmap: internal

**Why:** 24 MUST criteria passed the 0.2.0 sign-off with no test of their own: the code holds them as it reads, or
the test that held them went with the C# core's removal (#494 to #512) and none was written over the Script Core.
The user accepted them as they stood on 2026-11-07. The record is [`verification/2026-11-09-linux-signoff/results.md`](../../../../verification/2026-11-09-linux-signoff/results.md), where each reads
`accepted as is, no test`.

**What:** a test for each row below, in the lowest layer that can see it, carrying the row's ID in its name. A row
whose criterion is a measurement (MEM-1, LV-22) gets the harness that measures it, recorded and never gated. A
row that inherits another's verdict (UX-12) passes once those rows do. A test that fails is a defect in the product.
Each note is the record's, its line numbers 0326ce27's.

- **FN-2**: No test. Code refuses: `grid-view.ts:2612` (TS), `ExGrid.cs:475` (Blazor)
- **FN-4**: No test runs one scenario through both `from` and `fetch`. Structurally one path (`grid-view.ts:2617`, `rows` → `InMemoryGridSource.from`)
- **UX-12**: Inherits §4.1: A11Y-8 and A11Y-14 are gaps
- **A11Y-8**: No test greps for `aria-selected` after a drag (the bUnit check went with #501). Code writes it only on listbox options (completion, find-all list, filter values), never on a cell
- **A11Y-14**: No test counts row paints across a drag with the live region; `core/grid-selection.test.ts:119` checks the announcement, not paints
- **HG-10**: No test scrolls a group's first members out of the DOM and reads its `left`/`width`
- **HG-11**: No test counts member resolution across scroll steps; `core/grid-header-groups.test.ts:113` keeps the rectangle across a re-push and a Sort but scrolls nothing
- **HG-12**: No test of a long group label over narrow columns (no `####`, ellipsis, member Auto widths kept)
- **HG-17**: No test counts elements per rectangle or row paints with groups (3 groups over 40 columns)
- **VZ-6**: No test sets `virtualiseColumns: false` (none in `packages/*/test`, `tests/browser`, `dotnet/tests`)
- **ED-7**: no test changes the density while an edit is open. `#paintEditor` re-derives the box from `#metrics` on each pass, so the code appears to do it
- **ED-8**: no test opens an edit over a Placeholder. `#isEditableCell` (grid-view.ts:4772) refuses a row outside the Window and refuses during placeholder mode
- **ED-30**: composition held: core/grid-edit.test.ts:489, :508; browser/key-field.spec.mjs:187, :225, :249 (+9, incl. k6 :337, k15 :475, 150 ms @blazor); browser/windows-ime.spec.mjs:85 … :374 (CI `windows-ime`). **Missing:** a composition finished while a popover opens (Ctrl+F, Ctrl+1, Format Cells…). The 2026-10-30 note removed its tests and put nothing in their place, but the Statement and Pass columns still name the clause
- **KB-13**: only the key table: cases/keys/grid-keys.json:109 (`moveByViewport`); cases/selection/keyboard.json:149 (the Focus moves by the rows passed). **Missing:** Focus and Viewport moving by the same N, and the Focus's offset unchanged after presses 2 and 3
- **KB-14**: parts at layer 1: cases/selection/keyboard.json:160, :168 (the Focus clamps); cases/viewport/geometry.json:91, :105 (reveal). **Missing:** PageDown to the end at layer 2, with the Focus fully visible at every step
- **KB-14a**: no test reports a shrinking box (or a scrollbar appearing over the Focus) and checks scroll and reveal. core/grid-selection.test.ts:572 covers only a box that grows back
- **MEM-1**: No measurement: the C# `GC.GetAllocatedBytesForCurrentThread` scroll test went with the core (#501) and nothing in `packages/*/test` measures allocation at 10³ vs 10⁶ rows
- **WR-3**: No ExGrid.MudBlazor test registers a non-English localizer, or checks the Blank wording is the Chrome's (`MudScriptFilterPanelTests.cs:40` checks the core's "(Blanks)"); only ExSheet's (`ExSheet.MudBlazor.Tests/MudSheetPackageTests.cs:159`, `:185`)
- **SH-29**: sheet/create-sheet.test.ts:1866 'every command is refused by name … onEditingChanged', :469; browser/sheet.spec.mjs:594, :615 @blazor, :644; gap: replacing the Sheet Document while an edit is open
- **SH-49**: sheet/create-sheet.test.ts:1084 (default row order); ExSheet.Components/ScriptSheetTests.cs:870 'ToolbarContent crosses as the rows it declares, in markup order'; gap: the reference check (no internal member from an item type) was removed in #501
- **PV-15**: No count of report-grid updates per Field List interaction, nor of the grid Chrome asked once; Razor render-count tests removed
- **LV-22**: blazor/bridge.test.ts:175 (release protocol only). Weak-reference retention tests removed with Razor; nothing measures retention by the Script Core or the ExPivot report source
- **CH-10**: browser/chart-files.spec.mjs:58, :70 (chrome-150: PNG and 3D surface). Scale 1.25 never run; no check that 2D plot lines lie on Device Pixels
- **CH-11**: cases chart/appearance.json:943 (line-style keys only). No forced-colors test for ExChart
