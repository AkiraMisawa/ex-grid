# The Definition of Done's table of rows checked by hand, as the release check reads it

From `claude/release-workflow-checks`. The table belongs in `docs/definition-of-done.md`, §22, Step 4, which a
background agent does not edit: edit 17 of `docs/agents/proposals/release-workflow-checks-apply.md` puts it
there, word for word as below. The readings it rests on are that list's.

**Until then the release check reads it here.** `tools/release/ExGrid.Release` (`ByHandTable`) and its layer-1
test (`tests/ExGrid.Release.Tests`, `RepositoryTests`) read the table under its heading line from
`docs/definition-of-done.md`, and from this file while the Definition of Done has none. They hold it to the
record index, `verification/by-hand.json`: the same rows, the same paths. **Delete this file in the change
that applies edit 17.** The Definition of Done is read first, so a stale copy here would change nothing, but it
would mislead a reader.

## The text to commit

**Rows checked by hand, and the code each covers**

*(Added 2026-10-11, decided with the user,
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md).)*
Each row a person checks, where it is checked, and the paths whose change could change its result.

- **A row's latest record holds while its paths are unchanged.** That means unchanged between the
  commit the record was made at and the commit being signed off, or, for a release candidate, the
  commit it is tagged on.
- **The release workflow compares them** (`git diff --quiet`). It refuses a version without a suffix,
  and a release candidate, when:
  - a path changed;
  - the row has no record;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
  - that commit cannot be fetched, so whether its paths changed cannot be checked;
  - a path names nothing.
- **The records are indexed in `verification/by-hand.json`.** For each row it names the same paths as
  this table, the row's latest record, and the commit that record was made at. A layer-1 test
  (`tests/ExGrid.Release.Tests`) fails when the two differ, and the release workflow refuses a tag
  where they do.

**The paths are drawn wide** (ADR-0208). A row names whole files, and a directory where its behaviour
crosses several:

- the grid's script and stylesheet sources, and their minified builds, which are what a browser runs;
- the keyboard, editor and toolbar files;
- the measuring code;
- the spec a row runs;
- the DemoHost's or the Docs Site's page, only where the row reads that page.

When in doubt, the path goes in. The list is reviewed with the row, and in any change that moves the
code it names.

**A run that records a row** writes its record under `verification/<date>/`, naming the commit it
verified. In the same commit, it replaces the row's record in `verification/by-hand.json`.

| Row | Where | Checked by hand | The code it covers |
|---|---|---|---|
| **VZ-14** | Windows | `scrollbar.spec.mjs`'s VZ-14 block at a fractional display scale, headed, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `tests/ExGrid.Browser/fixtures.mjs`, `tests/ExGrid.Browser/scrollbar.spec.mjs` |
| **VZ-16** | Windows | A real browser zoom told to the grid as it changes, on a page that is only displayed. A thin line and a gridline in device pixels at a real 2.25: the display at 150%, the browser's zoom at 150%, on `/sheet?case=11` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Components/`, `src/ExGrid/ColumnGeometry.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetAppearance.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Wide.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **FN-12e** | Windows | The widest glyph of each `CellTextMetrics` class, measured in Chrome and Edge, while the Windows widths are not recorded (no `system-ui.windows.json` in `tests/GlyphWidths`). On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **FN-12e** | macOS | On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell. The macOS widths are measured already, `tests/GlyphWidths/*.macos.json` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/GridPresentationDefaults.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet/SheetColumns.cs`, `src/ExSheet/SheetRow.cs`, `src/ExSheet.Engine/CellDisplay.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `src/ExSheet.Engine/NumberText.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **ED-30** | Windows | A real IME on `/sheet` and `/sheet?chrome=mud`, on Chrome and Edge, on both hosts and behind 150 ms: the sixteenth run's cases k1 to k16, and DC-47's | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormulaAids.cs`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-42** | Windows | Ctrl+1 to Ctrl+5 reach `/sheet` before the browser's tab switching, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-51** | Windows | Ctrl+F1 reaches `/sheet`, and the toolbar hides and shows | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-52** | Windows | Alt released alone, and F10, show the KeyTips on `/sheet` under both Chromes, and the browser's menu does not open. Each letter is read beside Excel's Home tab. Nothing starts while an edit is open | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **KB-3** | macOS | Layer 3 on a Mac, where `ControlOrMeta` presses Command: Cmd+Enter fills, on every key path, the editor's included | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `src/ExSheet/SheetStructureKeys.cs`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `tests/ExGrid.Browser/` |
| **VZ-18** | the Citrix PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
