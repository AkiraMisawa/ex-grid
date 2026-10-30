using Microsoft.AspNetCore.Components;

namespace ExChart.Chrome;

/// <summary>
/// ExChart's Chrome (ADR-0281, ADR-0410): what draws the chart's toolbar — with Chart Filters' range, the
/// axis switch and the downloads — and its context menu, in the slots the Script Core leaves to Razor. Each member
/// returns the content to draw, or null for the Script Core's own. It renders and calls back; which commands there
/// are, what a typed range means and where the menu opens are ExChart's, so swapping it changes no
/// behaviour (spine rule 4). The legend, the tooltip and the plot are always ExChart's own.
/// </summary>
public interface IChartChrome
{
    /// <summary>The slots this Chrome fills with Razor when the Script Core draws the chart (ADR-0410): each is asked
    /// for through its view overload, and the Script Core draws its own part for every slot not named.</summary>
    ChartScriptSlots ScriptSlots => ChartScriptSlots.None;

    /// <summary>The toolbar over the Script Core: the view's commands, in its order, then, while Chart Filters is open,
    /// its fields and the axis switch, each control marked with a <see cref="ChartScriptSlotAttributes"/> attribute;
    /// or null for the Script Core's own toolbar.</summary>
    RenderFragment? Toolbar(ChartToolbarView view) => null;

    /// <summary>The context menu's content over the Script Core: exactly the view's commands, in its order, each marked
    /// <see cref="ChartScriptSlotAttributes.Command"/>; or null for the Script Core's own items.</summary>
    RenderFragment? Menu(ChartMenuView view) => null;
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
