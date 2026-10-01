using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// Show Details (ADR-0058/0062): the records behind a value cell go to the Consumer when it listens
// to OnShowDetails, and otherwise to a tab at the report's foot or a dialog — each an ExGrid of the
// source's fields, paged under the report's Source Version.
public partial class ExPivot
{
    private readonly List<PivotDetailsSheet> _sheets = [];
    private readonly EventCallback _closeDialog;
    private readonly string _idPrefix;
    private PivotDetailsSheet? _selectedSheet;
    private PivotDetailsSheet? _dialog;
    private int _dialogFocus;
    private int _sheetSequence;

    // The tab whose button takes the keyboard next — null for the report's own — and the request
    // that asks it; zero while none is asked.
    private (PivotDetailsSheet? Sheet, int Request) _tabFocus;

    /// <summary>
    /// The records behind a value cell (ADR-0058/0062): an empty cell has none. They are asked of the
    /// source the report came from, under its Source Version, so they add up to the cell. The
    /// Consumer takes them when it listens; otherwise they open in a tab at the report's foot, or in
    /// a dialog when the Consumer asked for one.
    /// </summary>
    private async Task ShowDetailsAsync(PivotReportRow row, int valueColumn)
    {
        if (_report is not { } report || !ReferenceEquals(row.Report, report) || _reportSource is not { } source)
            return;
        if (row.ValueAt(valueColumn) is null)
            return;
        var rowItems = Items(report, report.RowPath(row));
        var columnItems = Items(report, report.ColumnPath(valueColumn));
        var valueField = report.ValueFieldAt(row, valueColumn) is var vf and >= 0 ? report.ValueCaptions[vf] : null;
        var path = rowItems.Concat(columnItems).Select(item => item.Item).ToArray();
        var title = PivotWords.Fill(Word("details-title"), path.Length == 0 ? Word(PivotWords.GrandTotal) : string.Join(" / ", path));
        var details = new PivotDetails(source, report.DetailsQuery(row, valueColumn), rowItems, columnItems, valueField, title);
        if (OnShowDetails.HasDelegate)
        {
            await OnShowDetails.InvokeAsync(details);
            return;
        }
        var sheet = new PivotDetailsSheet($"{_idPrefix}-details-{++_sheetSequence}", details, _culture, SheetChanged);
        if (DetailsView == PivotDetailsView.Dialog)
        {
            _dialog?.Dispose();
            _dialog = sheet;
            _dialogFocus = ++_focusSequence;
        }
        else
        {
            _sheets.Add(sheet);
            _selectedSheet = sheet;
            // The records now cover the report, so the keyboard leaves it for the new tab: keys
            // left in a report nobody sees would move and copy what is not on screen.
            _tabFocus = (sheet, ++_focusSequence);
        }
        StateHasChanged();
    }

    // A details grid's source answered, failed or was refused: whatever shows it repaints.
    private void SheetChanged() => _ = InvokeAsync(Repaint);

    private void SelectSheet(PivotDetailsSheet? sheet)
    {
        if (ReferenceEquals(sheet, _selectedSheet))
            return;
        _selectedSheet = sheet;
        StateHasChanged();
    }

    private void CloseSheet(PivotDetailsSheet sheet)
    {
        var at = _sheets.IndexOf(sheet);
        if (at < 0)
            return;
        _sheets.RemoveAt(at);
        sheet.Dispose();
        // Excel's next sheet: the one after the closed tab, or before it, or the report.
        if (ReferenceEquals(_selectedSheet, sheet))
            _selectedSheet = _sheets.Count == 0 ? null : _sheets[Math.Min(at, _sheets.Count - 1)];
        // The close button that held the keyboard is gone: the selected tab takes it.
        _tabFocus = (_selectedSheet, ++_focusSequence);
        StateHasChanged();
    }

    private void CloseDialog()
    {
        if (_dialog is not { } dialog)
            return;
        _dialog = null;
        dialog.Dispose();
        StateHasChanged();
    }

    private RenderFragment DetailsTabs() => builder =>
    {
        var (focused, request) = _tabFocus;
        var report = new PivotDetailsTab($"{_idPrefix}-report", Word("report-tab"), _selectedSheet is null, () => SelectSheet(null), null, null)
        {
            FocusRequest = focused is null ? request : 0,
        };
        var tabs = _sheets.Select(sheet => new PivotDetailsTab(
            sheet.Id,
            sheet.Details.Title,
            ReferenceEquals(sheet, _selectedSheet),
            () => SelectSheet(sheet),
            () => CloseSheet(sheet),
            PivotWords.Fill(Word("close-tab"), sheet.Details.Title))
        {
            FocusRequest = ReferenceEquals(focused, sheet) ? request : 0,
        }).ToArray();
        var context = new PivotDetailsTabsContext(Word("sheets"), report, tabs, Word);
        if (PivotChrome?.DetailsTabs(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotDetailsTabsView>(1);
        builder.AddComponentParameter(2, nameof(PivotDetailsTabsView.Context), context);
        builder.CloseComponent();
    };

    /// <summary>A details sheet's records: an ExGrid of the source's fields, paged from the source,
    /// or the sentence that says why they cannot be shown.</summary>
    private RenderFragment Records(PivotDetailsSheet sheet) => builder =>
    {
        builder.OpenComponent<PivotDetailsGrid>(0);
        builder.AddComponentParameter(1, nameof(PivotDetailsGrid.Sheet), sheet);
        builder.AddComponentParameter(2, nameof(PivotDetailsGrid.Version), sheet.Version);
        builder.AddComponentParameter(3, nameof(PivotDetailsGrid.Problem), sheet.Problem is { } problem ? DetailsProblemText(problem) : null);
        builder.AddComponentParameter(4, nameof(PivotDetailsGrid.Chrome), GridChrome);
        builder.AddComponentParameter(5, nameof(PivotDetailsGrid.CommandLabel), _commandLabel);
        builder.AddComponentParameter(6, nameof(PivotDetailsGrid.RowHeight), RowHeight);
        builder.AddComponentParameter(7, nameof(PivotDetailsGrid.Density), Density);
        builder.AddComponentParameter(8, nameof(PivotDetailsGrid.CellMetrics), CellMetrics);
        builder.CloseComponent();
    };

    private string DetailsProblemText(SourceProblem problem) => ProblemText(problem);

    private RenderFragment DialogContent(PivotDetailsSheet sheet) => builder =>
    {
        var context = new PivotDetailsDialogContext(sheet.Details.Title, Records(sheet), CloseDialog, _dialogFocus, Word);
        if (PivotChrome?.DetailsDialog(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotDetailsDialogView>(1);
        builder.AddComponentParameter(2, nameof(PivotDetailsDialogView.Context), context);
        builder.CloseComponent();
    };
}
