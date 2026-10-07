using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// Show Details (ADR-0059/0063): the records behind a value cell go to the Consumer when it listens
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

    // The tab whose button takes the keyboard next — a details tab's; the report's tab hands it to
    // the report's grid instead (_keyboardBack) — and the request that asks it; zero while none is
    // asked.
    private (PivotDetailsSheet? Sheet, int Request) _tabFocus;

    // The request for the report's grid to take the keyboard back (ADR-0070), made when the dialog
    // closes, however it closes, and when a details tab closes with the report's tab selected after
    // it; zero while none was made. Acted on after the render that carries it (PivotKeyboardReturn).
    private int _keyboardBack;
    private Func<Task>? _returnKeyboard;

    // How the dialog's and the tabs' controls take the keyboard (TakeKeyboardAsync), held in a
    // field so the contexts carry the same delegate on every render.
    private Func<ElementReference, Task>? _takeKeyboard;

    /// <summary>
    /// The records behind a value cell (ADR-0059/0063), asked from a command made earlier: of the
    /// report on screen's row that stands for <paramref name="row"/> — compared by key, as a row may
    /// be shared by several reports (ADR-0161) — and nothing when the report on screen has none.
    /// </summary>
    private Task ShowDetailsAsync(PivotReportRow row, int valueColumn)
        => _report is { } report && report.RowFor(row.Key) is { } own ? ShowDetailsOfAsync(report, own, valueColumn) : Task.CompletedTask;

    /// <summary>
    /// The records behind a value cell of the report on screen (ADR-0059/0063): an empty cell has
    /// none. They are asked of the source the report came from, under its Source Version, so they add
    /// up to the cell. The Consumer takes them when it listens; otherwise they open in a tab at the
    /// report's foot, or in a dialog when the Consumer asked for one.
    /// </summary>
    private async Task ShowDetailsOfAsync(PivotReport report, PivotReportRow row, int valueColumn)
    {
        if (!ReferenceEquals(report, _report) || _reportSource is not { } source)
            return;
        if (report.ValueAt(row, valueColumn) is null)
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
        // The close button that held the keyboard is gone: the selected tab takes it. The report's
        // tab is its grid, which takes the keyboard back (ADR-0070) — there may be no tab left to
        // hold it, the last one having closed.
        if (_selectedSheet is null)
        {
            _tabFocus = default;
            _keyboardBack = ++_focusSequence;
        }
        else
        {
            _tabFocus = (_selectedSheet, ++_focusSequence);
        }
        StateHasChanged();
    }

    private void CloseDialog()
    {
        if (_dialog is not { } dialog)
            return;
        _dialog = null;
        dialog.Dispose();
        // However it closed — its grid's Escape, an Escape on its frame, Close, the backdrop — the
        // control that held the keyboard went with it, and the report's grid takes it back
        // (ADR-0070).
        _keyboardBack = ++_focusSequence;
        StateHasChanged();
    }

    /// <summary>Asks the report's grid for the keyboard back (ADR-0070), when there is a grid: an
    /// empty report has none, and a grid that has gone does nothing.</summary>
    private Task ReturnKeyboardToReportAsync() => _grid?.ReturnKeyboardAsync() ?? Task.CompletedTask;

    /// <summary>Gives a control of the dialog or the tabs the keyboard the report's grid holds —
    /// Close as the dialog opens, a tab Show Details has just opened, the tab selected when the one
    /// holding the keyboard closed — only while it is still the report's: a press the user made
    /// before the request landed keeps it (ADR-0070's note of 2026-10-02). With no report grid, the
    /// control's own focus.</summary>
    private Task TakeKeyboardAsync(ElementReference control)
        => _grid is { } grid ? grid.HandKeyboardToAsync(control) : control.FocusAsync().AsTask();

    /// <summary>What gives the report's grid the keyboard back, after the render that carries the
    /// request (<see cref="PivotKeyboardReturn"/>).</summary>
    private RenderFragment KeyboardReturn() => builder =>
    {
        builder.OpenComponent<PivotKeyboardReturn>(0);
        builder.AddComponentParameter(1, nameof(PivotKeyboardReturn.Request), _keyboardBack);
        builder.AddComponentParameter(2, nameof(PivotKeyboardReturn.Return), _returnKeyboard ??= ReturnKeyboardToReportAsync);
        builder.CloseComponent();
    };

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
        var context = new PivotDetailsTabsContext(Word("sheets"), report, tabs, Word) { TakeKeyboard = _takeKeyboard ??= TakeKeyboardAsync };
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
    /// or the sentence that says why they cannot be shown. <paramref name="leave"/> is the dialog's
    /// way out, raised by an Escape its grid has nothing left to dismiss (ADR-0070); a tab's grid has
    /// none.</summary>
    private RenderFragment Records(PivotDetailsSheet sheet, EventCallback leave = default) => builder =>
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
        builder.AddComponentParameter(9, nameof(PivotDetailsGrid.OnLeave), leave);
        builder.CloseComponent();
    };

    private string DetailsProblemText(SourceProblem problem) => ProblemText(problem);

    private RenderFragment DialogContent(PivotDetailsSheet sheet) => builder =>
    {
        // The dialog's grid hears the Escape it has nothing left to dismiss, and the dialog closes
        // on it: the grid's capture-phase listener keeps every Escape from the frame (ADR-0070).
        var context = new PivotDetailsDialogContext(sheet.Details.Title, Records(sheet, _closeDialog), CloseDialog, _dialogFocus, Word)
        {
            TakeKeyboard = _takeKeyboard ??= TakeKeyboardAsync,
        };
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
