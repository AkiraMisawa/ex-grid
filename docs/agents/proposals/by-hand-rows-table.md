# The Definition of Done's table of rows checked by hand, as the release check reads it

From `claude/release-workflow-checks`. The table belongs in `docs/definition-of-done.md`, §22, Step 4, which a
background agent does not edit: its edit in `docs/agents/proposals/release-mechanics-apply.md` puts it
there, word for word as below. The readings it rests on are that list's.

**Until then the release check reads it here.** `tools/release/ExGrid.Release` (`ByHandTable`) and its layer-1
test (`tests/ExGrid.Release.Tests`, `RepositoryTests`) read the table under its heading line from
`docs/definition-of-done.md`, and from this file while the Definition of Done has none. They hold it to the
record index, `verification/by-hand.json`: the same rows, the same paths. **Delete this file in the change
that applies that edit.** The Definition of Done is read first, so a stale copy here would change nothing, but it
would mislead a reader.

## The text to commit

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
  - the row has no record;
  - its latest record is not a pass;
  - its record does not name the commit the index gives it;
  - the record's tag is not `verified/` followed by the name of the record's directory;
  - that commit cannot be fetched through the record's tag, or the tag names another commit, so whether
    its paths changed cannot be checked;
  - a path names nothing;
  - for WR-10, a record its entry names besides the floor record is refused as the floor record would be,
    its paths compared from its own commit; or a stable MudBlazor version within the range the Wrappers
    declare, listed on nuget.org and published before X's commit time by its registration index or given no
    date there, is not among the versions its records' layer-2 runs covered; or that index cannot be read *(2026-10-12, how the release workflow checks
    the WR-10 row's notes below)*.
- **The records are indexed in `verification/by-hand.json`.** For each row it names the same paths as
  this table, the row's latest record, the commit that record was made at, and the record's tag. *(2026-10-12,
  how the release workflow checks the WR-10 row: for WR-10 each record names besides the MudBlazor versions
  its layer-2 runs covered, and the entry names the records of later layer-2 runs, by the row's notes,
  besides the floor record.)* A layer-1 test
  (`tests/ExGrid.Release.Tests`) fails when the index and the table differ, and the release workflow
  refuses a tag where they do.

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
| **VZ-18** | the Citrix PC | Ctrl+↓, Ctrl+↑ and PageDown on the Docs Site's blotter paint the rows they reveal, without a further scroll | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExGrid/GridMetrics.cs`, `src/ExGrid/ViewportBox.cs`, `src/ExGrid/ViewportGeometry.cs`, `src/ExGrid/ViewportSize.cs`, `src/ExGrid.MudBlazor/`, `samples/ExGrid.Docs/Examples/Grid/Blotter/`, `samples/ExGrid.Docs/Showcases/Blotter.razor` |
| **§21.11** | Windows | Where DOM focus is after ↓, → and ↓ ↓ on `/sheet`, with and without a Focus. Read under NVDA, under Narrator, and with neither, on Chrome and Edge | `src/ExGrid/Assets/`, `src/ExGrid/wwwroot/`, `src/ExGrid/Chrome/`, `src/ExGrid/Components/`, `src/ExGrid/Keys/`, `src/ExGrid/Selection/`, `src/ExSheet/Assets/`, `src/ExSheet/wwwroot/`, `src/ExSheet/Components/`, `samples/ExGrid.DemoPages/Pages/SheetPage.razor`, `samples/ExGrid.DemoPages/SheetCases.cs` |
| **WR-10** | Windows or Linux | Step 4's floor run: the layer-3 tests that run under a Wrapper, against a DemoHost built with the MudBlazor every Wrapper declares as its floor, on both hosts, in Chrome and in Edge; and beside it each Wrapper's layer-2 suite at every published version of its declared range. *(2026-10-12, decided with the user: a MudBlazor 9.x published after the floor record and before X asks for the layer-2 runs at that version only; the floor run, layer 3 at 9.6.0, holds while its paths are unchanged.)* *(2026-10-12, decided with the user: an unlisted version is not asked for; the release check reports it as out of the check because it is unlisted.)* *(2026-10-12, decided with the user: a layer-2 run at such a version is a run of its own; its record goes in its own `verification/<date>-<name>/` directory, the run tags the commit it verified `verified/` followed by that directory's name, and WR-10's entry in `verification/by-hand.json` names that record besides the floor record. The release check reads every record the entry names: the floor record's paths hold since its commit, and each later record's tag names its commit.)* | `:(glob)src/*.MudBlazor/**`, `samples/ExGrid.DemoPages/` |
