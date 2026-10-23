using global::ExChart.Chrome;
using global::ExChart.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExChart.Components;

// The toolbar and the context menu as a Chrome draws them (ADR-0281): the commands, Chart Filters and
// the menu's frame are decided here, the same under either Chrome.
public partial class ExChart
{
    private readonly Func<Task> _toggleFilters;
    private readonly Func<Task> _resetRange;
    private readonly Func<Task> _toggleTable;
    private readonly Func<Task> _csv;
    private readonly Func<Task> _svg;
    private readonly Func<Task> _png;
    private readonly Func<string?, string?, Task> _apply;
    private readonly Func<AxisScale, Task> _setScale;
    private readonly Dictionary<string, Func<Task>> _fromMenu;

    // The context menu: where it was asked for, in the viewport's own coordinates, which the
    // browser gave with the event. Nothing is measured.
    private (double X, double Y)? _menu;
    private ElementReference _menuFrame;
    private bool _focusMenu;

    private ChartToolbarContext ToolbarContext => new(
        ToolbarName,
        new EquatableList<ChartCommand>(
        [
            new(ChartCommandIds.Filters, "Chart Filters", true, _toggleFilters) { Expanded = _filtersOpen },
            new(ChartCommandIds.ResetRange, "Reset range", _range is not null, _resetRange),
            new(ChartCommandIds.Table, "View as table", true, _toggleTable) { Expanded = _tableOpen },
            new(ChartCommandIds.DownloadCsv, "CSV", !CsvUnavailable, _csv),
            new(ChartCommandIds.DownloadSvg, "SVG", !FilesUnavailable, _svg),
            new(ChartCommandIds.DownloadPng, "PNG", !FilesUnavailable, _png),
        ]),
        _filtersOpen ? FiltersContext : null);

    private ChartFiltersContext FiltersContext => new(
        Drawn.Axis.Values,
        Drawn.Axis.Values == AxisValues.Labels ? new EquatableList<string>(Drawn.Axis.Labels) : EquatableList<string>.Empty,
        _fromText,
        _toText,
        RangeHint,
        _filterError,
        _apply,
        ScaleSwitchable ? _scale ?? Drawn.Axis.EffectiveScale : null,
        _setScale);

    private ChartMenuContext MenuContext => new(
        ToolbarName,
        new EquatableList<ChartCommand>(
        [
            new(ChartCommandIds.ResetRange, "Reset range", _range is not null, _fromMenu[ChartCommandIds.ResetRange]),
            new(ChartCommandIds.Table, _tableOpen ? "Hide table" : "View as table", true, _fromMenu[ChartCommandIds.Table]) { Expanded = _tableOpen },
            new(ChartCommandIds.DownloadCsv, "Download CSV", !CsvUnavailable, _fromMenu[ChartCommandIds.DownloadCsv]),
            new(ChartCommandIds.DownloadSvg, "Download SVG", !FilesUnavailable, _fromMenu[ChartCommandIds.DownloadSvg]),
            new(ChartCommandIds.DownloadPng, "Download PNG", !FilesUnavailable, _fromMenu[ChartCommandIds.DownloadPng]),
        ]));

    // Asked for by the right button or by the keyboard (Shift+F10, the menu key), both of which the
    // browser raises as contextmenu on the chart. Its own menu replaces the browser's.
    private void OpenMenu(MouseEventArgs e)
    {
        _menu = (e.ClientX, e.ClientY);
        _focusMenu = true;
    }

    private void CloseMenu() => _menu = null;

    private async Task FromMenuAsync(Func<Task> command)
    {
        _menu = null;
        StateHasChanged();
        await command();
    }

    private void OnMenuKey(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") _menu = null;
    }

    private string MenuStyle => _menu is { } at
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"left:{Svg.N(at.X)}px;top:{Svg.N(at.Y)}px")
        : "";

    private async Task FocusMenuAsync()
    {
        if (!_focusMenu || _menu is null) return;
        _focusMenu = false;
        try
        {
            await _menuFrame.FocusAsync();
        }
        catch (Exception ex) when (ex is Microsoft.JSInterop.JSException or Microsoft.JSInterop.JSDisconnectedException or InvalidOperationException or OperationCanceledException)
        {
        }
    }
}
