using System.Text.Json;
using global::ExChart.Engine;
using global::ExGrid;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExChart.Script;

/// <summary>
/// A chart of a <see cref="ChartDefinition"/>, drawn by ExChart's Script Core (ADR-0370 section 1): Blazor's Framework
/// Adapter over <c>@ex-grid/chart</c>. The component renders one element, the chart's root, mounts the Script Core on
/// it, hands it its parameters as options and its definition as data, and raises the chart's events as
/// <see cref="EventCallback"/>s. It holds no state of the chart's, draws nothing inside the root and decides nothing:
/// the drawing, the tooltip, the legend, Chart Filters, View as table and what each gesture means are the Script
/// Core's, in the browser, on both hosting models. Like the Razor component it holds no view state (ADR-0272): what
/// the user changes is raised, and taken back as a parameter.
/// </summary>
/// <remarks>
/// <para>It stands beside <see cref="Components.ExChart"/>, the Razor component, until ExChart's Blazor step holds
/// (ADR-0370 section 11). It reads no Chart Spec yet, and takes no Chrome: the toolbar and the menu are the built-in
/// ones.</para>
/// <para>A .NET function cannot cross to the browser, so a definition's Formats cross as the texts they write each
/// value the definition holds; what the chart would ask a Format of a value it does not hold, an axis' ticks, a
/// candle's change, is refused by name, never written another way. A function series, an axis Format and a Row Key
/// two points would cross as alike are refused the same way. Each refusal is a
/// <see cref="NotSupportedException"/> raised as the parameter is set, which names what to change.</para>
/// </remarks>
public sealed class ExChart : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The chart, in the data's terms. A new instance is a new definition, and crosses to the browser again;
    /// the same one handed back crosses nothing.</summary>
    [Parameter, EditorRequired] public ChartDefinition Definition { get; set; } = default!;

    /// <summary>The drawing's width in CSS pixels; geometry is a C# parameter, never only a stylesheet value (ADR-0027).</summary>
    [Parameter] public double Width { get; set; } = 800;

    /// <summary>The drawing's height in CSS pixels.</summary>
    [Parameter] public double Height { get; set; } = 400;

    /// <summary>The density whose font size and glyph widths lay out the labels, as ExGrid's (ADR-0016, ADR-0028).</summary>
    [Parameter] public GridDensity Density { get; set; } = GridDensity.Standard;

    /// <summary>The chart's name, which its accessible summary and its table's caption begin with (ADR-0275).</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>A description the Consumer adds after the generated summary.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>Where the legend sits: top (the default), bottom, left or right, aligned, or none (ADR-0271).</summary>
    [Parameter] public LegendPlacement? Legend { get; set; }

    /// <summary>How the horizontal axis places and writes its ticks.</summary>
    [Parameter] public AxisStyle? AxisStyle { get; set; }

    /// <summary>The panels whose left and right axes the Consumer declared independent (ADR-0251).</summary>
    [Parameter] public IReadOnlySet<string>? IndependentScales { get; set; }

    /// <summary>How many colour slots the stylesheet defines: ten, unless a theme declares more (ADR-0256).</summary>
    [Parameter] public int ColourSlots { get; set; } = Appearance.DefaultColourSlots;

    /// <summary>The keys of the series hidden, taken back from <see cref="OnSeriesVisibilityChanged"/>.</summary>
    [Parameter] public IReadOnlySet<string>? HiddenSeries { get; set; }

    /// <summary>Raised when the user hides or shows a series from the legend.</summary>
    [Parameter] public EventCallback<SeriesVisibility> OnSeriesVisibilityChanged { get; set; }

    /// <summary>The range shown, taken back from <see cref="OnRangeChanged"/>; null for the whole range.</summary>
    [Parameter] public ChartRange? Range { get; set; }

    /// <summary>Raised when the user narrows the range or returns it to the whole; null for the whole.</summary>
    [Parameter] public EventCallback<ChartRange?> OnRangeChanged { get; set; }

    /// <summary>The scale the horizontal axis is switched to, taken back from <see cref="OnScaleChanged"/>; null for the declared one.</summary>
    [Parameter] public AxisScale? Scale { get; set; }

    /// <summary>Raised when the user switches the horizontal axis under Chart Filters.</summary>
    [Parameter] public EventCallback<AxisScale> OnScaleChanged { get; set; }

    /// <summary>The vertical axes' scale: <see cref="AxisScale.Logarithmic"/>, or null for proportional (ADR-0290). A value at or below zero keeps them proportional, with a Note naming it.</summary>
    [Parameter] public AxisScale? YScale { get; set; }

    /// <summary>
    /// Raised when the user clicks a point: pressed and released at one position, on the series nearest the pointer
    /// that has a value there (ADR-0258). Its Row is the key the point was handed in with. A click taken on a
    /// definition the Consumer has since replaced is not raised: its Row Key may name a row the chart no longer shows.
    /// </summary>
    [Parameter] public EventCallback<ChartPointClick> OnPointClick { get; set; }

    /// <summary>The name a saved file takes, without its extension; the title's when null, else "chart" (ADR-0282).</summary>
    [Parameter] public string? FileName { get; set; }

    private ScriptMount? _mount;
    private ElementReference _root;
    private ChartDefinition? _definition;
    private string? _definitionJson;
    private RowKeys _rows = new();
    private int _generation;
    private bool _renderOwed = true;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        _mount = new ScriptMount(JS, InvokeAsync, DispatchExceptionAsync, Prepare);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Definition is null)
            throw new InvalidOperationException("The Script Core's ExChart draws a Definition; it reads no Chart Spec yet (ADR-0370 section 1).");
        if (!ReferenceEquals(Definition, _definition))
        {
            // Checked before anything moves: a definition this adapter cannot carry leaves the chart on the one it had.
            var (json, rows) = ChartWire.Definition(Definition);
            _definition = Definition;
            _definitionJson = json;
            _rows = rows;
            _generation += 1;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_mount is { } mount)
            await mount.SendAsync(State());
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _renderOwed;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _renderOwed = false;
        _mount?.Render(builder, element => _root = element);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _mount is not { } mount)
            return;
        if (await mount.StartAsync(Assets, _root, "attachChart", State))
        {
            _renderOwed = true;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Saves the chart as a file, as the user sees it: CSV of the visible series in the range shown, the SVG of the
    /// drawing, or that SVG painted to PNG at the screen's Device Pixel (ADR-0282). One call is one save. False when the
    /// chart's script is not loaded, which the chart says on its face, or the circuit has gone.
    /// </summary>
    public Task<bool> DownloadAsync(ChartFileFormat format) =>
        _mount is { } mount ? mount.AskAsync("download", false, ChartWire.Camel(format)) : Task.FromResult(false);

    // The options as they stand, and the definition the component holds.
    private DrawingState State() => new(OptionsJson(), _definitionJson ?? throw new InvalidOperationException("Internal: the chart has no definition."), _generation);

    private string OptionsJson() => ChartWire.Json(json =>
    {
        json.WriteStartObject();
        json.WriteNumber("width", Width);
        json.WriteNumber("height", Height);
        json.WriteString("density", ChartWire.Camel(Density));
        WriteText(json, "title", Title);
        WriteText(json, "description", Description);
        if (Legend is { } legend)
        {
            json.WriteStartObject("legend");
            json.WriteString("position", ChartWire.Camel(legend.Position));
            json.WriteString("align", ChartWire.Camel(legend.Align));
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("legend");
        }
        if (AxisStyle is { } style)
        {
            json.WriteStartObject("axisStyle");
            json.WriteString("placement", ChartWire.Camel(style.Placement));
            json.WriteString("label", ChartWire.Camel(style.Label));
            json.WriteNumber("angle", style.Angle);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("axisStyle");
        }
        WriteSet(json, "independentScales", IndependentScales);
        json.WriteNumber("colourSlots", ColourSlots);
        WriteSet(json, "hiddenSeries", HiddenSeries);
        if (Range is { } range)
        {
            // An end left open is absent, as the Script Core reads it.
            json.WriteStartObject("range");
            WriteEnd(json, "from", range.From);
            WriteEnd(json, "to", range.To);
            WriteEnd(json, "yFrom", range.YFrom);
            WriteEnd(json, "yTo", range.YTo);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("range");
        }
        WriteScale(json, "scale", Scale);
        WriteScale(json, "yScale", YScale);
        WriteText(json, "fileName", FileName);
        json.WriteStartArray("hears");
        if (OnSeriesVisibilityChanged.HasDelegate)
            json.WriteStringValue("seriesVisibilityChanged");
        if (OnRangeChanged.HasDelegate)
            json.WriteStringValue("rangeChanged");
        if (OnScaleChanged.HasDelegate)
            json.WriteStringValue("scaleChanged");
        if (OnPointClick.HasDelegate)
            json.WriteStringValue("pointClick");
        json.WriteEndArray();
        json.WriteEndObject();
    });

    // What to hand the Consumer for an event the chart raised.
    private Func<Task>? Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "seriesVisibilityChanged":
            {
                var change = new SeriesVisibility(Text(payload.GetProperty("key")), payload.GetProperty("hidden").GetBoolean());
                return () => OnSeriesVisibilityChanged.InvokeAsync(change);
            }
            case "rangeChanged":
            {
                ChartRange? range = payload.ValueKind == JsonValueKind.Null
                    ? null
                    : new ChartRange(End(payload, "from"), End(payload, "to"), End(payload, "yFrom"), End(payload, "yTo"));
                return () => OnRangeChanged.InvokeAsync(range);
            }
            case "scaleChanged":
            {
                var scale = Enum.Parse<AxisScale>(Text(payload), ignoreCase: true);
                return () => OnScaleChanged.InvokeAsync(scale);
            }
            case "pointClick":
            {
                // Taken on a definition the Consumer has since replaced: its Row Key is read against none (spine rule 6).
                if (payload.GetProperty("generation").GetInt32() != _generation)
                    return null;
                var click = new ChartPointClick(
                    Text(payload.GetProperty("seriesId")), payload.GetProperty("position").GetDouble(), _rows.Read(payload.GetProperty("row")));
                return () => OnPointClick.InvokeAsync(click);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    private static double? End(JsonElement range, string name) =>
        range.TryGetProperty(name, out var end) && end.ValueKind != JsonValueKind.Null ? end.GetDouble() : null;

    private static string Text(JsonElement json) => json.GetString() ?? throw new InvalidOperationException("Internal: the Script Core sent null for a text.");

    private static void WriteText(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null)
            json.WriteNull(name);
        else
            json.WriteString(name, value);
    }

    private static void WriteSet(Utf8JsonWriter json, string name, IReadOnlySet<string>? values)
    {
        if (values is null)
        {
            json.WriteNull(name);
            return;
        }
        json.WriteStartArray(name);
        // In order, so a set handed again with the same members writes the same options.
        foreach (var value in values.Order(StringComparer.Ordinal))
            json.WriteStringValue(value);
        json.WriteEndArray();
    }

    private static void WriteEnd(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } end)
            json.WriteNumber(name, end);
    }

    private static void WriteScale(Utf8JsonWriter json, string name, AxisScale? scale)
    {
        if (scale is { } value)
            json.WriteString(name, ChartWire.Camel(value));
        else
            json.WriteNull(name);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_mount is { } mount)
            await mount.DisposeAsync();
    }
}
