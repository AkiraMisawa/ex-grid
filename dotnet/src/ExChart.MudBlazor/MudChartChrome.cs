using global::ExChart.Chrome;
using global::ExGrid.MudBlazor;
using global::MudBlazor;
using Microsoft.AspNetCore.Components;

namespace ExChart.MudBlazor;

/// <summary>
/// ExChart's Chrome under MudBlazor (ADR-0281, ADR-0410), in the chart's slots: the toolbar — Chart Filters'
/// toggle, Reset range and View as table as text <c>MudButton</c>s, the downloads as a <c>MudButtonGroup</c>, and
/// Chart Filters' ends as native fields in MudBlazor's outline, which the chart's script writes and reads (text in the
/// axis' own writing, or a select of the categories on a category axis), with the axis switch — and the content of
/// the chart's context menu.
///
/// <para>It draws what ExChart hands it and calls back. Which commands there are, what a typed range
/// means, what is refused and where the menu opens are ExChart's; swapping this Chrome for the
/// built-in markup changes no behaviour. The legend, the tooltip and the plot stay ExChart's own: a
/// second rendering path for them would drift from the pointer entry (ADR-0255).</para>
/// </summary>
public sealed class MudChartChrome : IChartChrome
{
    /// <summary>The one every Consumer can share.</summary>
    public static MudChartChrome Default { get; } = new();

    /// <summary>
    /// The grid's Chrome for a grid whose selection is charted: ExGrid.MudBlazor's
    /// <see cref="MudGridChrome"/>, wording the selection bridge's commands ("Chart", "Add to …") in
    /// ExChart's words, which the grid Wrapper would otherwise paint as their ids, with a chart's
    /// icon. Set it as the grid's <c>Chrome</c> (ADR-0258).
    /// </summary>
    public static MudGridChrome GridChrome { get; } = new()
    {
        Label = GridChartCommands.Label,
        Icon = id => id.StartsWith(GridChartCommands.Chart, StringComparison.Ordinal) || id.StartsWith(GridChartCommands.AddPrefix, StringComparison.Ordinal)
            ? Icons.Material.Filled.ShowChart
            : null,
    };

    /// <summary>A Material icon for a command's id (<see cref="ChartCommandIds"/>), or null for the Wrapper's own.</summary>
    public Func<string, string?>? Icon { get; init; }

    /// <summary>The toolbar and the context menu's content are this Chrome's (ADR-0410).</summary>
    public ChartScriptSlots ScriptSlots => ChartScriptSlots.Toolbar | ChartScriptSlots.Menu;

    /// <summary>The toolbar (<see cref="MudChartScriptToolbar"/>): MudBlazor's controls, wired in the browser.</summary>
    public RenderFragment? Toolbar(ChartToolbarView view) => b =>
    {
        b.OpenComponent<MudChartScriptToolbar>(0);
        b.AddComponentParameter(1, nameof(MudChartScriptToolbar.View), view);
        b.AddComponentParameter(2, nameof(MudChartScriptToolbar.Chrome), this);
        b.CloseComponent();
    };

    /// <summary>The context menu's content (<see cref="MudChartScriptMenu"/>).</summary>
    public RenderFragment? Menu(ChartMenuView view) => b =>
    {
        b.OpenComponent<MudChartScriptMenu>(0);
        b.AddComponentParameter(1, nameof(MudChartScriptMenu.View), view);
        b.AddComponentParameter(2, nameof(MudChartScriptMenu.Chrome), this);
        b.CloseComponent();
    };

    internal string? IconFor(string id) => Icon?.Invoke(id) ?? id switch
    {
        ChartCommandIds.Filters => Icons.Material.Filled.FilterList,
        ChartCommandIds.ResetRange => Icons.Material.Filled.ZoomOutMap,
        ChartCommandIds.Table => Icons.Material.Filled.TableChart,
        ChartCommandIds.DownloadCsv => Icons.Material.Filled.Description,
        ChartCommandIds.DownloadSvg => Icons.Material.Filled.Image,
        ChartCommandIds.DownloadPng => Icons.Material.Filled.Image,
        _ => null,
    };
}
