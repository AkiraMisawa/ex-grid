namespace ExGrid.Docs;

/// <summary>
/// The Feature matrix's rows written by hand (ADR-0110): where the family runs, and what a reader calls each
/// seam a Wrapper can draw. Every row cites the record that decides it, and the build reads this class: a row
/// that is not text, cites nothing or cites a record that does not exist, a seam of a Wrapper's Chrome with no
/// title here, and a title for no seam stop it. A row says only what its record says: change the record first,
/// and this row with it (CLAUDE.md, "The Docs Site moves with the change").
/// </summary>
internal static class FeatureMatrixByHand
{
    /// <summary>
    /// Where the family runs. The hosting rows say what the README and the Installation page say, and no more:
    /// a claim about Blazor Server is made by its own decision (Definition of Done §24).
    /// </summary>
    public static IReadOnlyList<MatrixFact> WhereItRuns { get; } =
    [
        new(".NET", "The packages target net10.0, and every newer runtime loads them as they are.", ["ADR-0022"]),
        new("Browsers", "Chrome and Edge. Safari and Firefox are out of scope.", ["ADR-0017"]),
        new("Devices", "A desktop, with a keyboard and a mouse, on Windows, macOS or Linux. Touch, pen, phones, the iPhone and the iPad are out of scope.",
            ["ADR-0045"]),
        new("Direction", "Left to right. Right-to-left text in a cell is shown as Excel shows it on a left-to-right sheet; the columns never flow from the right.",
            ["ADR-0031"]),
        new("WebAssembly", "ExGrid is verified under WebAssembly.", ["ADR-0041"]),
        new("Blazor Server", "Every browser test also runs under Blazor Server.", ["DoD §24"]),
        new("Licence", "MIT, for every package.", ["ADR-0042"]),
        new("Maturity", "Prerelease: 0.1.0-beta.N, or a release candidate, 0.1.0-rc.N, each published before the Definition of Done is signed off, and its API may change from one to the next. The first stable version, 0.1.0, waits for the sign-off; being 0.x, it promises the sign-off, not a frozen API.",
            ["ADR-0042", "ADR-0200", "ADR-0205", "DoD Step 7"]),
    ];

    /// <summary>
    /// The Wrappers whose Chrome the matrix reads, each for its own interface's seams, with the records that
    /// decide what it draws. ExSheet.MudBlazor's grid seams are ExGrid.MudBlazor's, which its Chrome hands them to.
    /// </summary>
    public static IReadOnlyList<MatrixWrapper> Wrappers { get; } =
    [
        new("ExGrid.MudBlazor", "ExGrid.MudBlazor.MudGridChrome", "ExGrid.Chrome.IGridChrome", ["ADR-0030", "DoD §23"]),
        new("ExSheet.MudBlazor", "ExSheet.MudBlazor.MudSheetChrome", "ExSheet.ISheetChrome", ["ADR-0071"]),
        new("ExPivot.MudBlazor", "ExPivot.MudBlazor.MudPivotChrome", "ExPivot.Chrome.IPivotChrome", ["ADR-0062"]),
    ];

    /// <summary>What a reader calls each seam, and the decision that made it.</summary>
    public static IReadOnlyList<MatrixSurface> Surfaces { get; } =
    [
        new("IGridChrome.FilterPanel", "The filter panel", ["ADR-0009", "ADR-0044"]),
        new("IGridChrome.ColumnMenu", "The column menu", ["ADR-0010", "ADR-0044"]),
        new("IGridChrome.ContextMenu", "The Context Menu", ["ADR-0036"]),
        new("IGridChrome.CellMessage", "The popover that says why a cell is flagged", ["ADR-0034"]),
        new("IGridChrome.CellEditor", "The Cell Editor", ["ADR-0010", "ADR-0030"]),
        new("IGridChrome.FindPanel", "The find panel", ["ADR-0055"]),
        new("IGridChrome.LoadingIndicator", "The loading indicator", ["ADR-0010"]),
        new("IGridChrome.NameBox", "The Formula Bar's Name Box", ["ADR-0051"]),
        new("IGridChrome.FormulaBarText", "The Formula Bar's text field", ["ADR-0051"]),
        new("IGridChrome.EditorCompletion", "The completion list and argument hint", ["ADR-0051"]),
        new("IGridChrome.SelectionSummary", "The Selection Summary in the status line", ["ADR-0130"]),
        new("IGridChrome.Words", "The words a refusal is announced in", ["ADR-0203"]),
        new("ISheetChrome.FormatCells", "Format Cells", ["ADR-0071"]),
        new("ISheetChrome.ToolbarItem", "The Sheet Toolbar's items", ["ADR-0100"]),
        new("IPivotChrome.GridChrome", "The report's grid, dressed by ExGrid.MudBlazor's Chrome", ["ADR-0061", "ADR-0062"]),
        new("IPivotChrome.FieldList", "The PivotTable Fields pane", ["ADR-0061"]),
        new("IPivotChrome.ReportFilters", "The report filter band", ["ADR-0061"]),
        new("IPivotChrome.Toolbar", "The Pivot Toolbar", ["ADR-0061"]),
        new("IPivotChrome.Menu", "A placed field's menu, and the Layout menu", ["ADR-0061"]),
        new("IPivotChrome.ItemFilter", "Filter…, a field's Items to tick", ["ADR-0061"]),
        new("IPivotChrome.FieldSettings", "Field Settings…", ["ADR-0061"]),
        new("IPivotChrome.ValueFieldSettings", "Value Field Settings…", ["ADR-0061"]),
        new("IPivotChrome.DetailsTabs", "Show Details' tabs at the report's foot", ["ADR-0059"]),
        new("IPivotChrome.DetailsDialog", "Show Details' dialog", ["ADR-0059"]),
        new("IPivotChrome.StaleReport", "The Stale Report's notice", ["ADR-0067"]),
    ];
}
