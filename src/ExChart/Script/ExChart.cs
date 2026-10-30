using System.Text.Json;
using global::ExChart.Chrome;
using global::ExChart.Engine;
using global::ExGrid;
using global::ExGrid.Script;
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
/// Core's, in the browser, on both hosting models. It holds no view state (ADR-0272): what the user changes is
/// raised, and taken back as a parameter.
/// </summary>
/// <remarks>
/// <para>Its <see cref="Chrome"/> fills the toolbar and the menu with Razor where it names them in
/// <see cref="IChartChrome.ScriptSlots"/> (ADR-0410); every other part is the Script Core's own. A Chart Spec is read
/// through its Chart Sources in .NET, where the sources are (ADR-0344), and the reading crosses as a definition does.</para>
/// <para>A .NET function cannot cross to the browser, so a definition's Formats cross as the texts they write each
/// value the definition holds; what the chart would ask a Format of a value it does not hold, an axis' ticks, a
/// candle's change, is refused by name, never written another way. A function series, an axis Format and a Row Key
/// two points would cross as alike are refused the same way. Each refusal is a
/// <see cref="NotSupportedException"/> raised as the parameter is set, which names what to change.</para>
/// </remarks>
public sealed class ExChart : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The chart, in the data's terms; or null when it is read from a <see cref="Spec"/> through
    /// <see cref="Sources"/>. A new instance is a new definition, and crosses to the browser again; the same one handed
    /// back crosses nothing.</summary>
    [Parameter] public ChartDefinition? Definition { get; set; }

    /// <summary>
    /// A Chart Spec the chart reads through <see cref="Sources"/> in place of a <see cref="Definition"/> (ADR-0344):
    /// each series asks its Data Handle's source for its column, thinned to the chart's pixels over the range shown, and
    /// asks again when the range changes. Until the first answer nothing is drawn; a spec the sources cannot answer,
    /// or an answer older than the data drawn, is said in the chart's place.
    /// </summary>
    [Parameter] public string? Spec { get; set; }

    /// <summary>The Chart Source of each Data Handle <see cref="Spec"/> names, null for a handle the host does not know.</summary>
    [Parameter] public Func<string, IChartSource?>? Sources { get; set; }

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

    /// <summary>
    /// What draws the toolbar and the context menu's content, where its <see cref="IChartChrome.ScriptSlots"/> names
    /// them (ADR-0410): the Razor renders in this component's tree, under the Consumer's cascading values, and the
    /// Script Core places it in the chart. What its controls do is wired in the browser from the
    /// <see cref="ChartScriptSlotAttributes"/> they write. Null, or a slot not named, is the Script Core's own part.
    /// </summary>
    [Parameter] public IChartChrome? Chrome { get; set; }

    private ScriptMount? _mount;
    private ElementReference _root;
    private bool _disposed;
    // The definition that crossed last, as JSON with its Row Keys, and its generation.
    private ChartDefinition? _definition;
    private string? _definitionJson;
    private RowKeys _rows = new();
    private int _generation;
    private bool _renderOwed = true;
    // The Chrome slots the script mounted, and the Chrome they were last drawn by.
    private readonly RazorSlotHolders _slots = new(ChartRazorSlot.ViewOf);
    private IChartChrome? _renderedChrome;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        _mount = new ScriptMount(JS, InvokeAsync, DispatchExceptionAsync, Prepare, _slots, RenderSlots);

    private void RenderSlots()
    {
        _renderOwed = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Spec is null && Definition is null)
            throw new InvalidOperationException("An ExChart is given its Definition, or a Spec and the Sources it is read through.");
        if (Spec is not null && (Definition is not null || Sources is null))
            throw new InvalidOperationException("An ExChart read from a Spec is given the Sources it is read through, and no Definition beside them (ADR-0344).");
        if (Chrome is not null)
            // A slot this adapter does not fill is refused as the Chrome is handed, not as the script first mounts it.
            _ = ChartRazorSlot.NamesOf(Chrome.ScriptSlots);
        if (!ReferenceEquals(Chrome, _renderedChrome))
        {
            // Another Chrome draws the slots standing, and the script wires what it drew.
            _renderedChrome = Chrome;
            _slots.Redrawn();
            _renderOwed |= _slots.Any;
        }
        // The range a spec is read over: the parameter when it changed, else what the user last narrowed it to, as the
        // Script Core takes it back (ADR-0272).
        if (!_rangeTaken || _rangeParameter != Range)
            _range = Range;
        (_rangeParameter, _rangeTaken) = (Range, true);
        // Compared by value: a spec built again on each render, or a method group's new delegate, is the same ask and is
        // not read again.
        var ask = false;
        if (Spec is not null && (!string.Equals(_askedSpec, Spec, StringComparison.Ordinal) || !Equals(_askedSources, Sources)))
        {
            // Another spec or other sources: what was read from the last is not drawn under the new.
            _read = null;
            _versions = new Dictionary<string, long>();
            SetRefusal(null);
            ask = true;
        }
        else if (Spec is not null && (_askedRange != _range || _askedPixels != SourcePixels))
        {
            // Another range, or another width, whose pixels the source thins to.
            ask = true;
        }
        else if (Spec is null)
        {
            // An ask still out is dropped when it answers: the Consumer's Definition is drawn now.
            if (_askedSpec is not null)
            {
                _asked++;
                _renderOwed = true;
            }
            (_read, _askedSpec, _askedSources, _asking, _held) = (null, null, null, false, null);
            SetRefusal(null);
        }
        if (Definition is { } definition)
            Take(definition);
        else if (!_asking || _read is null)
            // Read through a source, the definition drawn stays until the newest ask is answered (ADR-0344).
            Take(_read?.Definition ?? Unread);
        // Asked once a definition is held: an ask a spec refuses at once answers before this method returns.
        if (ask)
            _ = AskObservedAsync();
    }

    // Checked before anything moves: a definition this adapter cannot carry leaves the chart on the one it had.
    private void Take(ChartDefinition definition)
    {
        if (ReferenceEquals(definition, _definition))
            return;
        var (json, rows) = ChartWire.Definition(definition);
        _definition = definition;
        _definitionJson = json;
        _rows = rows;
        _generation += 1;
    }

    // ---- Read through Chart Sources (ADR-0322, ADR-0344) ----

    // The newest ask, and what it asked; the reading drawn, the version each Data Handle was read at, and what is said
    // in the chart's place when no reading can be drawn.
    private int _asked;
    private bool _asking;
    private string? _askedSpec;
    private Func<string, IChartSource?>? _askedSources;
    private ChartRange? _askedRange;
    private int _askedPixels;
    // What crossed last, and, while an ask is out over a reading drawn, what stays drawn until it is answered.
    private DrawingState? _lastState;
    private DrawingState? _held;
    private ChartSpecReading? _read;
    private IReadOnlyDictionary<string, long> _versions = new Dictionary<string, long>();
    private string? _refusal;
    // The range read over, and the parameter it was last taken from.
    private ChartRange? _range;
    private ChartRange? _rangeParameter;
    private bool _rangeTaken;

    // Drawn until the first answer: no series, so nothing is drawn as if it were the data.
    private static readonly ChartDefinition Unread = new(ChartAxis.Numbers(), []);

    // The device pixels a plot of the chart's width could have, at the two device pixels a CSS pixel ExChart counts in
    // (ADR-0254): the source thins to them, and the plot thins on top if it is narrower.
    private int SourcePixels => Math.Max(1, (int)Math.Ceiling(Width * 2));

    // Asked from OnParametersSet, which does not wait for the answer: what the answer throws fails the component, as a
    // handler's exception does.
    private async Task AskObservedAsync()
    {
        try
        {
            await AskAsync();
        }
        catch (Exception e)
        {
            await DispatchExceptionAsync(e);
        }
    }

    private async Task AskAsync()
    {
        var asked = ++_asked;
        var (spec, sources, range, pixels) = (Spec!, Sources!, _range, SourcePixels);
        (_askedSpec, _askedSources, _askedRange, _askedPixels) = (spec, sources, range, pixels);
        // The points of another range, drawn at this one, are not what this range holds: the drawing, its options with
        // it, stays as it crossed until the answer (ADR-0344).
        if (_read is not null && !_asking)
            _held = _lastState;
        _asking = true;
        _renderOwed = true;
        ChartSourceReading? read = null;
        string? refusal = null;
        try
        {
            // Resumed where the ask was made: the reading is the renderer's.
            read = await ChartSpec.ReadAsync(spec, sources, pixels, range?.From, range?.To);
        }
        catch (ChartSpecRefusedException e)
        {
            refusal = e.Message;
        }
        catch (Exception e)
        {
            // A source's own cancellation or timeout too: the chart says it was not answered, and is never left waiting
            // on an ask that ended.
            refusal = $"The source did not answer the chart: {e.Message}";
        }
        // Another ask replaced this one, or the chart is gone: dropped (spine rule 6).
        if (asked != _asked || _disposed)
            return;
        _asking = false;
        _held = null;
        _renderOwed = true;
        if (read?.Versions.FirstOrDefault(v => _versions.TryGetValue(v.Key, out var shown) && v.Value < shown) is { Key: not null } older)
        {
            // Never drawn over the newer data shown, nor left showing what was asked before as if it were the answer:
            // said in the chart's place (spine rule 1).
            SetRefusal($"The source answered the chart from version {older.Value} of '{older.Key}', older than version {_versions[older.Key]} the chart was drawn from, so it is not drawn.");
        }
        else if (read is not null)
        {
            try
            {
                Take(read.Reading.Definition);
                _read = read.Reading;
                _versions = read.Versions;
                SetRefusal(null);
            }
            catch (NotSupportedException e)
            {
                // A reading this adapter cannot carry is said in the chart's place, never drawn another way.
                SetRefusal(e.Message);
            }
        }
        else
        {
            SetRefusal(refusal);
        }
        if (_mount is { } mount)
            await mount.SendAsync(State());
        StateHasChanged();
    }

    private void SetRefusal(string? refusal)
    {
        if (refusal == _refusal)
            return;
        _refusal = refusal;
        _renderOwed = true;
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
        _mount?.Render(builder, element => _root = element, _refusal, _asking, Width, Height);
        _slots.Render(builder, 20, (name, view) => ChartRazorSlot.Fragment(Chrome, name, view));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_mount is not { } mount)
            return;
        if (!firstRender)
        {
            await mount.ReportSlotsAsync();
            return;
        }
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
    private DrawingState State()
    {
        if (_held is { } held)
            return held;
        var state = new DrawingState(OptionsJson(), _definitionJson ?? throw new InvalidOperationException("Internal: the chart has no definition."), _generation);
        _lastState = state;
        return state;
    }

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
        if (Chrome?.ScriptSlots is { } slots and not ChartScriptSlots.None)
        {
            json.WriteStartArray("razorSlots");
            foreach (var name in ChartRazorSlot.NamesOf(slots))
                json.WriteStringValue(name);
            json.WriteEndArray();
        }
        else
        {
            json.WriteNull("razorSlots");
        }
        json.WriteStartArray("hears");
        if (OnSeriesVisibilityChanged.HasDelegate)
            json.WriteStringValue("seriesVisibilityChanged");
        // Read through a source, the range the user narrows to is asked for again, whoever else hears it.
        if (OnRangeChanged.HasDelegate || Spec is not null)
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
                // Narrowed on a definition the Consumer has since replaced: the data moved under the gesture before its
                // release reached .NET, so it is refused, and the browser returns to the whole range (ADR-0272, spine rule 6).
                var narrowedOn = payload.GetProperty("generation").GetInt32();
                if (narrowedOn != _generation)
                {
                    _ = _mount?.TellAsync("dropRange", narrowedOn);
                    return null;
                }
                var ends = payload.GetProperty("range");
                ChartRange? range = ends.ValueKind == JsonValueKind.Null
                    ? null
                    : new ChartRange(End(ends, "from"), End(ends, "to"), End(ends, "yFrom"), End(ends, "yTo"));
                return async () =>
                {
                    var handed = Range;
                    await OnRangeChanged.InvokeAsync(range);
                    // A Consumer that handed a range back, the user's or one of its own, has had it asked for.
                    if (Spec is null || handed != Range || _range == range)
                        return;
                    _range = range;
                    // Not awaited: the events after it are not held behind a source's answer.
                    _ = AskObservedAsync();
                };
            }
            case "scaleChanged":
            {
                var scale = ChartWire.Member<AxisScale>(Text(payload));
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
        _disposed = true;
        if (_mount is { } mount)
            await mount.DisposeAsync();
    }
}
