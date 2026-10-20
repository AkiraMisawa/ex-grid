using ExGrid.Keys;
using ExGrid.Components;
using ExGrid.Selection;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExSheet.Components;

/// <summary>
/// The Sheet Tabs (ADR-0331 §1, §4): one grid draws the Sheet shown, and selecting another Sheet's tab
/// — a press, Ctrl+PageUp or Ctrl+PageDown — shows that Sheet in it. Each Sheet keeps its own Selection
/// and scroll offsets, which the grid hands back as the Sheet is left and takes again with the Row
/// Sequence Version the Sheet is shown under (<see cref="GridView"/>); its column widths are its own,
/// recorded in it. A Sheet's rows are places (ADR-0046), so the version moves only when the grid is
/// given other rows: another document opened, or another Sheet shown. An intent, a press or a key
/// aimed at the Sheet left carries the version it was taken under, and is refused (ADR-0011,
/// ADR-0142): nothing a gesture does depends on when it reaches the Sheet.
/// </summary>
public partial class ExSheet
{
    // The tab list, the frame the keyboard comes back to the grid from.
    private ElementReference _tabList;

    // Each Sheet's tab, focused when the arrow keys select it on the tab list.
    private readonly Dictionary<Sheet, ElementReference> _tabs = [];

    // The tab the arrow keys selected, focused once the render that selects it has landed.
    private Sheet? _tabToFocus;

    /// <summary>A press on a tab (ADR-0331 §4): the user's own action, so the notice it leaves is behind them.</summary>
    private async Task ShowSheetByUserAsync(Sheet sheet)
    {
        if (!RefuseSwitchWhileEditing())
        {
            ClearNotice();
            await ShowSheetAsync(sheet);
        }
    }

    /// <summary>Ctrl+PageUp and Ctrl+PageDown (ADR-0331 §4): the Sheet before or after the one shown, never past the first or the last.</summary>
    private async Task OnSheetTabKeyAsync(GridDeclaredKeyPress press, int step)
    {
        if (press.EditOpen || RefuseSwitchWhileEditing()) return;
        ClearNotice();
        await StepSheetAsync(step);
    }

    /// <summary>The keys on the tab list, where a Tab put the keyboard: ← and → select the tab before or after, Home and End the first and the last, and Escape or Enter gives the keyboard back to the grid.</summary>
    private async Task OnSheetTabKeyAsync(KeyboardEventArgs key)
    {
        switch (key.Key)
        {
            case "ArrowLeft":
            case "ArrowRight":
                if (RefuseSwitchWhileEditing()) return;
                ClearNotice();
                if (await StepSheetAsync(key.Key == "ArrowLeft" ? -1 : 1)) _tabToFocus = _sheet;
                break;
            case "Home":
            case "End":
                if (RefuseSwitchWhileEditing() || _workbook is not { } workbook) return;
                ClearNotice();
                await ShowSheetAsync(key.Key == "Home" ? workbook.Sheets[0] : workbook.Sheets[^1]);
                _tabToFocus = _sheet;
                break;
            case "Escape":
            case "Enter":
                if (_grid is { } grid) await grid.ReturnKeyboardAsync(_tabList, static () => []);
                break;
        }
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => FocusSelectedTabAsync();

    /// <summary>Focuses the tab the arrow keys selected, once the render that selects it has landed.</summary>
    private async Task FocusSelectedTabAsync()
    {
        if (_tabToFocus is not { } sheet) return;
        _tabToFocus = null;
        if (_tabs.TryGetValue(sheet, out var tab)) await tab.FocusAsync(preventScroll: true);
    }

    private async Task<bool> StepSheetAsync(int step)
    {
        if (_workbook is not { } workbook || _sheet is not { } shown) return false;
        var at = IndexOf(workbook, shown) + step;
        if (at < 0 || at >= workbook.Sheets.Count) return false;
        await ShowSheetAsync(workbook.Sheets[at]);
        return true;
    }

    /// <summary>
    /// While an edit is open another Sheet is not shown, and the notice says why: the edit stands over a
    /// cell of the Sheet shown, and a Formula pointing at another Sheet's cells is not built yet (ticket
    /// 09 of exsheet-workbook). Answers whether it refused.
    /// </summary>
    private bool RefuseSwitchWhileEditing()
    {
        if (!IsEditing) return false;
        Tell(SheetWords.SheetSwitchWhileEditing);
        StateHasChanged();
        return true;
    }

    /// <summary>Shows <paramref name="sheet"/>, and tells the Consumer: the Workbook Document records the Sheet shown (ADR-0331 §2).</summary>
    private async Task ShowSheetAsync(Sheet sheet)
    {
        if (!ShowSheet(sheet)) return;
        await RaiseDocumentsAsync();
    }

    /// <summary>
    /// Shows <paramref name="sheet"/> in the grid: the view of the Sheet left is kept, the grid is given
    /// the shown Sheet's rows and widths under a new Row Sequence Version, and the view kept for it —
    /// none for a Sheet not shown before — handed ahead of that version, so the two arrive together.
    /// Answers whether another Sheet is shown.
    /// </summary>
    private bool ShowSheet(Sheet sheet) => ShowSheet(sheet, null);

    /// <summary>Shows <paramref name="sheet"/>, with <paramref name="place"/>, when given, placed as its
    /// Selection and its Focus revealed in place of the Selection kept for it (the Name Box, ADR-0331 §5).</summary>
    private bool ShowSheet(Sheet sheet, (SelectionRange Range, CellPosition Focus)? place)
    {
        if (_workbook is not { } workbook || sheet == _sheet) return false;
        if (_sheet is { } left && _grid is { } leaving) _views[left] = leaving.ReadView();
        workbook.Shown = sheet;
        _sheet = sheet;
        _sequence++;
        _edges = SheetEdges.Of(sheet);
        _resizing = null;
        _columns = SheetColumnList.Of(sheet, CellMetrics);
        _cellAppearance = SheetAppearance.Lookup(++_formatReading);
        var view = _views.TryGetValue(sheet, out var kept)
            ? kept with { RowSequenceVersion = _sequence }
            : new GridView(_sequence, GridSelection.Empty, 0, 0);
        if (place is { } placed)
        {
            _grid?.ShowViewWith(view, placed.Range, placed.Focus);
            _selection = GridSelection.Empty;
        }
        else
        {
            _grid?.ShowViewWith(view);
            _selection = view.Selection;
        }
        _window = [];
        ShowRows(RowHeight > 0 ? (int)(view.ScrollTopPx / RowHeight) : 0, InitialRows);
        RefreshToolbar();
        StateHasChanged();
        return true;
    }

    private static int IndexOf(global::ExSheet.Engine.Workbook workbook, Sheet sheet)
    {
        for (var i = 0; i < workbook.Sheets.Count; i++)
        {
            if (workbook.Sheets[i] == sheet) return i;
        }
        return -1;
    }

    /// <summary>Reads every row of the Sheet shown afresh: a change whose rows on it are not named.</summary>
    private void RefreshEveryRow()
    {
        var sheet = CurrentSheet;
        var rows = new SheetRow[_window.Length];
        for (var i = 0; i < rows.Length; i++) rows[i] = new SheetRow(sheet, _windowStart + i);
        _window = rows;
        _cellAppearance = SheetAppearance.Lookup(++_formatReading);
        StateHasChanged();
    }
}
