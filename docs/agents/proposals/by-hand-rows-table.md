# Proposal: the Definition of Done's table of rows checked by hand

From `claude/release-workflow-checks`, which builds ADR-0208's release check. A background agent does
not edit the Definition of Done, so the table is written here for the orchestrator to commit.

## Where it goes

In `docs/definition-of-done.md`, §22, Step 4, directly after the dated note of 2026-10-11 that cites
ADR-0208. That note ends "The table of rows and paths is written here with that check"; this is the
table. Everything under "The text to commit" goes in as it stands.

## What reads it

- **The release check**, `tools/release/ExGrid.Release` (`ByHand`, `ByHandTable`), on a version without
  a suffix and on a release candidate. It refuses the tag when the table and the record index,
  `verification/by-hand.json`, name different rows or different paths.
- **A layer-1 test**, `tests/ExGrid.Release.Tests` (`RepositoryTests`), on every build. It fails when they
  differ, and when a path names nothing in the working tree.

Both read the table under its heading, word for word, and the four columns by name. Every code span in
the last column is a path, and a path ending in `/` is a directory. They read it from
`docs/definition-of-done.md` once the heading is there, and from this file until then. **Once the table
is committed, delete this file.** The Definition of Done is read first, so a stale copy here would
change nothing, but it would mislead a reader.

## Readings for the orchestrator to confirm

- **FN-12e is two rows, Windows and macOS.** The row reads "by hand on each platform; Layer 3 on Linux".
  The glyph widths are questions already answered and held by layer 1 (`tests/GlyphWidths/*.json`), so
  what a person checks is the painting: a bold amount without an ellipsis, and `####` filling its cell.
  Linux is layer 3's, in CI. The seventeenth run's Part G covers Windows only, so FN-12e on macOS needs
  a run of its own on a Mac, which KB-3 needs anyway.
- **KB-3 on macOS is a layer-3 run on a Mac**, the suite's `ControlOrMeta` presses then being Command.
  Its paths therefore include `tests/ExGrid.Browser/`, the suite that run is.
- **SH-42's latest record is the eleventh run's Part B**, on a standalone `keys.html`, which is the run
  the row names. Its code has changed since, so the check asks for the seventeenth run's Part D.
- **ED-30's latest record, the sixteenth run, is seeded as `fail`.** That run gave no verdict by row.
  Its differences 1 and 3 read against ED-30's statement.
- **A record names the commit it verified**, in full or by seven digits or more, and the check holds the
  index to it. A slip in the index's commit would otherwise move a row's baseline unseen.

## The text to commit

#### Rows checked by hand, and the code each covers

*(Added 2026-10-11, decided with the user,
[ADR-0208](adr/0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md).)*
Each row a person checks, where it is checked, and the paths whose change could change its result.

- **A row's latest record holds while its paths are unchanged.** That means unchanged between the
  commit the record was made at and the commit being signed off.
- **The release workflow compares them** (`git diff --quiet`). It refuses a version without a suffix,
  and a release candidate, when:
  - a path changed;
  - the row has no record;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
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
| **FN-12e** | Windows | The widest glyph of each `CellTextMetrics` class, measured in Chrome and Edge. On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **FN-12e** | macOS | On `/sizing` and `/sheet?case=3c`, a bold amount estimated to fit painted without an ellipsis, and `####` filling its cell. The macOS widths are measured already, `tests/GlyphWidths/*.macos.json` | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Columns/`, `src/ExGrid/Components/`, `src/ExGrid/DisplayText.cs`, `src/ExGrid/GridDensity.cs`, `src/ExGrid/GridMetrics.cs`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetCellText.cs`, `src/ExSheet.Engine/NumberFormat.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/Pages/Sizing.razor`, `samples/ExGrid.DemoPages/SheetCases.cs`, `tests/GlyphWidths/classes.mjs`, `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/measure.mjs` |
| **ED-30** | Windows | A real IME on `/sheet` and `/sheet?chrome=mud`, on Chrome and Edge, on both hosts and behind 150 ms: the sixteenth run's cases k1 to k16, and DC-47's | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormulaAids.cs`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-42** | Windows | Ctrl+1 to Ctrl+5 reach `/sheet` before the browser's tab switching, on Chrome and Edge, on both hosts | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-51** | Windows | Ctrl+F1 reaches `/sheet`, and the toolbar hides and shows | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **SH-52** | Windows | Alt released alone, and F10, show the KeyTips on `/sheet` under both Chromes, and the browser's menu does not open. Each letter is read beside Excel's Home tab. Nothing starts while an edit is open | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `samples/ExGrid.DemoPages/DemoChrome.cs`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **KB-3** | macOS | Layer 3 on a Mac, where `ControlOrMeta` presses Command: Cmd+Enter fills, on every key path, the editor's included | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Cells/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid.MudBlazor/`, `src/ExSheet/Components/`, `src/ExSheet/SheetFormatKeys.cs`, `src/ExSheet/SheetStructureKeys.cs`, `src/ExSheet/Toolbar/`, `src/ExSheet.MudBlazor/`, `tests/ExGrid.Browser/` |
| **VZ-18** | the Citrix PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
