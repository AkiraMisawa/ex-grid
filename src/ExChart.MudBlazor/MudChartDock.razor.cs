using global::ExChart.Chrome;
using global::ExChart.Engine;
using global::MudBlazor;
using Microsoft.AspNetCore.Components;

namespace ExChart.MudBlazor;

/// <summary>
/// The Chart Dock (ADR-0258): the grid, and a panel beside it or below it holding the charts made
/// from its selection as tabs. Give it the grid as its content and the grid's
/// <see cref="GridChartBridge{TRow}"/>; it takes the bridge's events, so the context menu's "Chart
/// selection" opens a tab directly, "Add to chart" offers every open tab, and a click on a point
/// shows its cell.
/// </summary>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public partial class MudChartDock<TRow>
    where TRow : class
{
    private readonly List<ChartTarget> _charts = [];
    private readonly Dictionary<string, EventCallback<ChartPointClick>> _clicks = [];
    private int _made;
    private int _active;
    private string? _refusal;

    /// <summary>The grid, and anything to stand beside the charts.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The bridge of the grid in <see cref="ChildContent"/>. The dock sets its <c>OnChart</c>, <c>OnAdded</c>, <c>OnRefused</c> and <c>Targets</c>.</summary>
    [Parameter, EditorRequired] public GridChartBridge<TRow> Bridge { get; set; } = null!;

    /// <summary>Where the charts go: beside the grid (the default) or below it.</summary>
    [Parameter] public ChartDockPlacement Placement { get; set; } = ChartDockPlacement.Right;

    /// <summary>Each chart's width in CSS pixels.</summary>
    [Parameter] public double ChartWidth { get; set; } = 640;

    /// <summary>Each chart's height in CSS pixels.</summary>
    [Parameter] public double ChartHeight { get; set; } = 320;

    /// <summary>What draws each chart's toolbar and context menu; <see cref="MudChartChrome.Default"/> unless set.</summary>
    [Parameter] public IChartChrome? Chrome { get; set; } = MudChartChrome.Default;

    /// <summary>The open charts, by name, in their tabs' order.</summary>
    public IReadOnlyList<ChartTarget> Charts => _charts;

    // Beside the grid, the panel's content is as wide as its charts and the grid takes what is left, so a
    // chart is never clipped by a narrow page. Content-box, so the stylesheet's padding is not paired here.
    private string? PanelStyle => Placement == ChartDockPlacement.Right
        ? FormattableString.Invariant($"flex: 0 0 auto; box-sizing: content-box; width: {ChartWidth}px;")
        : null;

    private string Classes => Placement == ChartDockPlacement.Bottom
        ? "mud-ex-chart mud-ex-chart-dock mud-ex-chart-dock-bottom"
        : "mud-ex-chart mud-ex-chart-dock mud-ex-chart-dock-right";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Bridge);
        Bridge.OnChart = OpenAsync;
        Bridge.OnAdded = AddedAsync;
        Bridge.OnRefused = RefusedAsync;
    }

    /// <summary>Opens a chart in a new tab, named "Chart N", and selects it.</summary>
    public Task OpenAsync(GridChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        _refusal = null;
        _charts.Add(new ChartTarget($"Chart {++_made}", chart));
        _active = _charts.Count - 1;
        return RetargetAsync();
    }

    private Task AddedAsync(ChartTarget target, GridChart chart)
    {
        _refusal = null;
        var at = _charts.FindIndex(c => c.Name == target.Name);
        if (at >= 0)
        {
            _charts[at] = target with { Chart = chart };
            _active = at;
        }
        return RetargetAsync();
    }

    private Task RefusedAsync(string why)
    {
        _refusal = why;
        return InvokeAsync(StateHasChanged);
    }

    private void Dismiss() => _refusal = null;

    private Task Close(MudTabPanel panel)
    {
        var at = _charts.FindIndex(c => c.Name == (string?)panel.ID);
        if (at < 0) return Task.CompletedTask;
        _clicks.Remove(_charts[at].Name);
        _charts.RemoveAt(at);
        _active = Math.Clamp(_active > at ? _active - 1 : _active, 0, Math.Max(0, _charts.Count - 1));
        return RetargetAsync();
    }

    private Task RetargetAsync()
    {
        Bridge.Targets = _charts.ToList();
        return InvokeAsync(StateHasChanged);
    }

    // A chart that can no longer be drawn — its axis column left the grid — says why in its tab,
    // rather than showing what it last drew as if it were current (spine rule 1).
    private ChartDefinition? DefinitionOf(GridChart chart, out string? why)
    {
        try
        {
            why = null;
            return Bridge.Draw(chart);
        }
        catch (ChartRefusedException refused)
        {
            why = refused.Message;
            return null;
        }
    }

    // Held per chart, never made per render: a new delegate is a changed parameter (AGENTS.md).
    private EventCallback<ChartPointClick> ClickOf(string name)
    {
        if (!_clicks.TryGetValue(name, out var click))
            _clicks[name] = click = EventCallback.Factory.Create<ChartPointClick>(this, async c =>
            {
                if (_charts.Find(x => x.Name == name) is { } chart)
                    await Bridge.ShowInGridAsync(chart.Chart, c);
            });
        return click;
    }
}
