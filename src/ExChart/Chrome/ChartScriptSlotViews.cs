using global::ExChart.Engine;

namespace ExChart.Chrome;

/// <summary>
/// The Chrome slots an <see cref="IChartChrome"/> fills with Razor when the Script Core draws the chart
/// (<c>ExChart.Script.ExChart</c>, ADR-0410): each named here is asked for through its view overload, and the Script
/// Core draws its own part for every slot not named.
/// </summary>
/// <remarks>
/// It stands beside the Razor component's contexts until that component is removed (ADR-0370 section 11, ADR-0410
/// section 4's note of 2026-10-28): a Chrome answers both while both draw.
/// </remarks>
[Flags]
public enum ChartScriptSlots
{
    /// <summary>No slot: the Script Core draws every part.</summary>
    None = 0,

    /// <summary>The toolbar, with Chart Filters, through <see cref="IChartChrome.Toolbar(ChartToolbarView)"/>.</summary>
    Toolbar = 1,

    /// <summary>The context menu's content, inside the chart's own frame, through
    /// <see cref="IChartChrome.Menu(ChartMenuView)"/>.</summary>
    Menu = 2,
}

/// <summary>
/// The attributes a slot's Razor writes on its controls when the Script Core draws the chart (ADR-0410 section 2): the
/// adapter's script wires each control it finds by them, in the browser, so a press means what the chart now says and
/// a download is saved inside the click that asked for it. The contents carry no handler of their own for what an
/// attribute says.
/// </summary>
public static class ChartScriptSlotAttributes
{
    /// <summary>On a control that runs one command, the command's <see cref="ChartCommandView.Id"/>: the script runs it
    /// on a press, and refuses it where the chart says it is not enabled.</summary>
    public const string Command = "data-ex-command";

    /// <summary>On Chart Filters' start: a text or date <c>input</c>, or a native <c>select</c> of the categories with
    /// an empty option for the axis' own start. The script writes the range the chart holds into it whenever that
    /// changes, and reads it as Apply is pressed; draw it with no value bound.</summary>
    public const string From = "data-ex-chart-from";

    /// <summary>On Chart Filters' end, as <see cref="From"/>.</summary>
    public const string To = "data-ex-chart-to";

    /// <summary>On the control that applies the range typed in <see cref="From"/> and <see cref="To"/>.</summary>
    public const string Apply = "data-ex-chart-apply";

    /// <summary>On the axis switch: a native <c>select</c> whose options' values are <c>proportional</c> and
    /// <c>categories</c>. The script shows the chart's scale in it and switches the axis as the browser fires its
    /// change.</summary>
    public const string Scale = "data-ex-chart-scale";
}

/// <summary>One command the chart decides (ADR-0281), as a slot's contents draw it.</summary>
/// <param name="Id">Stable, for an icon (<see cref="ChartCommandIds"/>), and what the control's
/// <see cref="ChartScriptSlotAttributes.Command"/> names.</param>
/// <param name="Label">What it is called, in the chart's words.</param>
/// <param name="Enabled">Whether it is available.</param>
public sealed record ChartCommandView(string Id, string Label, bool Enabled)
{
    /// <summary>For a toggle — Chart Filters, View as table — whether what it opens is open; null for a plain command.</summary>
    public bool? Expanded { get; init; }

    /// <summary>Why it is not available, where the chart says why (CSV on a thinned chart, ADR-0344); null otherwise.</summary>
    public string? Title { get; init; }
}

/// <summary>What a Chrome draws the toolbar from over the Script Core (ADR-0281, ADR-0410). A new view is made only
/// when something in it changed, so a Chrome compares it to skip its render.</summary>
/// <param name="Name">The toolbar's accessible name: the chart's title, or its word for a chart.</param>
/// <param name="Commands">Chart Filters' toggle, Reset range, View as table, then CSV, SVG and PNG.</param>
/// <param name="Filters">Chart Filters while it is open; null while it is closed.</param>
/// <param name="Words">The chart's words for the toolbar's own controls.</param>
public sealed record ChartToolbarView(string Name, EquatableList<ChartCommandView> Commands, ChartFiltersView? Filters, ChartToolbarWords Words);

/// <summary>
/// Chart Filters over the Script Core (ADR-0273): what its fields offer. The range itself is not here: the script
/// writes it into the fields marked <see cref="ChartScriptSlotAttributes.From"/> and
/// <see cref="ChartScriptSlotAttributes.To"/>, so a view a round trip behind never writes over what is being typed.
/// </summary>
/// <param name="Values">What the axis holds: a date axis may take a date input, a category axis one of <paramref name="Labels"/>.</param>
/// <param name="Labels">The categories of a category axis, in order; empty otherwise.</param>
/// <param name="Hint">What a value looks like on this axis.</param>
/// <param name="Error">Why the last range applied was not taken; null when it was.</param>
/// <param name="Scale">How positions are spaced, where the axis can be switched; null where it cannot.</param>
public sealed record ChartFiltersView(AxisValues Values, EquatableList<string> Labels, string Hint, string? Error, AxisScale? Scale);

/// <summary>The chart's words for the toolbar's own controls: the Consumer's, else the built-in English.</summary>
/// <param name="From">Chart Filters' start.</param>
/// <param name="To">Chart Filters' end.</param>
/// <param name="Apply">The control that applies the typed range.</param>
/// <param name="Axis">The axis switch.</param>
/// <param name="Proportional">A proportional axis.</param>
/// <param name="Categories">An axis of categories.</param>
/// <param name="Downloads">The downloads' group.</param>
/// <param name="Filters">Chart Filters' own name.</param>
public sealed record ChartToolbarWords(string From, string To, string Apply, string Axis, string Proportional, string Categories, string Downloads, string Filters);

/// <summary>What a Chrome draws the context menu's content from over the Script Core (ADR-0281, ADR-0410). The frame
/// around it, where it opens and what closes it are the chart's.</summary>
/// <param name="Name">The menu's accessible name.</param>
/// <param name="Commands">Reset range, View as table, then CSV, SVG and PNG.</param>
public sealed record ChartMenuView(string Name, EquatableList<ChartCommandView> Commands);
