using global::ExChart.Engine;
using Microsoft.AspNetCore.Components;

namespace ExChart.Chrome;

/// <summary>
/// ExChart's Chrome (ADR-0281): what draws the chart's toolbar — with Chart Filters' range, the
/// axis switch and the downloads — and its context menu. Each member returns the content to draw,
/// or null for ExChart's built-in plain markup. It renders and calls back; which commands there
/// are, what a typed range means and where the menu opens are ExChart's, so swapping it changes no
/// behaviour (spine rule 4). The legend, the tooltip and the plot are always ExChart's own.
/// </summary>
public interface IChartChrome
{
    /// <summary>The toolbar above the plot: in this order, Chart Filters' toggle, Reset range, View
    /// as table and the downloads, then, while Chart Filters is open, its range and the axis switch.</summary>
    RenderFragment? Toolbar(ChartToolbarContext context) => null;

    /// <summary>The context menu's content: exactly the context's commands, in its order. The frame
    /// around it, where it opens and what closes it are ExChart's.</summary>
    RenderFragment? Menu(ChartMenuContext context) => null;
}

/// <summary>The ids of ExChart's own commands, for a Chrome's icons and a Consumer's words.</summary>
public static class ChartCommandIds
{
    /// <summary>Opens or closes Chart Filters (ADR-0273).</summary>
    public const string Filters = "exchart.filters";

    /// <summary>Returns the range narrowed by a drag or by Chart Filters to the whole axis (ADR-0273).</summary>
    public const string ResetRange = "exchart.reset-range";

    /// <summary>Shows or hides the chart's data as a table (ADR-0275).</summary>
    public const string Table = "exchart.table";

    /// <summary>Saves the visible series as CSV (ADR-0282).</summary>
    public const string DownloadCsv = "exchart.download.csv";

    /// <summary>Saves the drawing as SVG (ADR-0282).</summary>
    public const string DownloadSvg = "exchart.download.svg";

    /// <summary>Saves the drawing painted as PNG (ADR-0282).</summary>
    public const string DownloadPng = "exchart.download.png";
}

/// <summary>One command ExChart decides (ADR-0281): what it is called, whether it is available, and what it does.</summary>
/// <param name="Id">Stable, for an icon (<see cref="ChartCommandIds"/>).</param>
/// <param name="Label">What it is called.</param>
/// <param name="Enabled">Whether it is available: Reset range is not while nothing is narrowed, a download is not while the chart's script is not loaded.</param>
/// <param name="Invoke">Runs it; from the context menu, it closes the menu as well.</param>
public sealed record ChartCommand(string Id, string Label, bool Enabled, Func<Task> Invoke)
{
    /// <summary>For a toggle — Chart Filters, View as table — whether what it opens is open; null for a plain command.</summary>
    public bool? Expanded { get; init; }
}

/// <summary>What a Chrome draws the toolbar from (ADR-0281). A new context is built only when something in it changed, so a Chrome compares it to skip its render.</summary>
/// <param name="Name">The toolbar's accessible name: the chart's title, or "Chart".</param>
/// <param name="Commands">Chart Filters' toggle, Reset range, View as table, then CSV, SVG and PNG.</param>
/// <param name="Filters">Chart Filters while it is open; null while it is closed.</param>
public sealed record ChartToolbarContext(string Name, EquatableList<ChartCommand> Commands, ChartFiltersContext? Filters);

/// <summary>
/// Chart Filters (ADR-0273): the range typed in the axis' own values, and the axis switch. The
/// Chrome holds what is being typed; ExChart reads it when it is applied, and says what it could not
/// read rather than guess (spine rule 1).
/// </summary>
/// <param name="Values">What the axis holds: a date axis takes a date picker, a category axis one of <paramref name="Labels"/>.</param>
/// <param name="Labels">The categories of a category axis, in order; empty otherwise.</param>
/// <param name="From">The range's start as it stood when Chart Filters opened, in the axis' text (dates as yyyy-MM-dd); null for the axis' start.</param>
/// <param name="To">The range's end, likewise; null for the axis' end.</param>
/// <param name="Hint">What a value looks like on this axis.</param>
/// <param name="Error">Why the last range applied was not taken; null when it was.</param>
/// <param name="Apply">Applies a typed range: either end empty or null is the axis' own end.</param>
/// <param name="Scale">How positions are spaced, when the axis can be switched; null when it cannot.</param>
/// <param name="SetScale">Switches the axis.</param>
public sealed record ChartFiltersContext(
    AxisValues Values,
    EquatableList<string> Labels,
    string? From,
    string? To,
    string Hint,
    string? Error,
    Func<string?, string?, Task> Apply,
    AxisScale? Scale,
    Func<AxisScale, Task> SetScale);

/// <summary>What a Chrome draws the context menu from (ADR-0281).</summary>
/// <param name="Name">The menu's accessible name.</param>
/// <param name="Commands">Reset range, View as table, then CSV, SVG and PNG.</param>
public sealed record ChartMenuContext(string Name, EquatableList<ChartCommand> Commands);
