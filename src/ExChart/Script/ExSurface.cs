using System.Globalization;
using System.Text.Json;
using global::ExChart.Engine;
using global::ExGrid;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExChart.Script;

/// <summary>
/// A 3D surface of a <see cref="SurfaceDefinition"/> (ADR-0274), drawn by ExChart's Script Core (ADR-0370 section 1):
/// Blazor's Framework Adapter over <c>@ex-grid/chart</c>'s surface. The component renders one element, mounts the
/// Script Core on it, hands it its parameters as options and its definition as data, and raises the view and the
/// slice as <see cref="EventCallback"/>s. The mesh, its colour bands, its ticks, the turning and the slice's chart are
/// the Script Core's, in the browser, on both hosting models.
/// </summary>
/// <remarks>
/// It stands beside <see cref="Components.ExSurface"/>, the Razor component, until ExChart's Blazor step holds
/// (ADR-0370 section 11). Formats cross as the texts they write each value the surface holds, as
/// <see cref="ExChart"/>'s do: a Format the surface would ask of a tick between its values, on an axis placed by value
/// or on a mesh, is refused by name, a <see cref="NotSupportedException"/> raised as the parameter is set.
/// </remarks>
public sealed class ExSurface : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The surface, in the data's terms. A new instance crosses to the browser again; the same one handed back
    /// crosses nothing.</summary>
    [Parameter, EditorRequired] public SurfaceDefinition Definition { get; set; } = default!;

    /// <summary>The surface's width in CSS pixels; geometry is a C# parameter, never only a stylesheet value (ADR-0027).</summary>
    [Parameter] public double Width { get; set; } = 640;

    /// <summary>The surface's height in CSS pixels, which the slice's chart beside it shares.</summary>
    [Parameter] public double Height { get; set; } = 420;

    /// <summary>The width of the slice's chart, in CSS pixels.</summary>
    [Parameter] public double SliceWidth { get; set; } = 420;

    /// <summary>The density the font size is resolved from, as a grid's is (ADR-0028).</summary>
    [Parameter] public GridDensity Density { get; set; } = GridDensity.Standard;

    /// <summary>The surface's title, which opens its accessible summary.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>A sentence the accessible summary ends with.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>The view — angles, zoom and centre — taken back from <see cref="OnViewChanged"/>; null is <see cref="SurfaceView.Default"/>.</summary>
    [Parameter] public SurfaceView? View { get; set; }

    /// <summary>Raised when the user ends a turn, a zoom or a pan, with the view it left (ADR-0272).</summary>
    [Parameter] public EventCallback<SurfaceView> OnViewChanged { get; set; }

    /// <summary>The slice drawn beside the surface, taken back from <see cref="OnSliceChanged"/>; null draws none.</summary>
    [Parameter] public SurfaceSlice? Slice { get; set; }

    /// <summary>Raised when the user clicks a tenor or a strike, with the slice it names (ADR-0274), its value one of
    /// the axis' own. A click taken on a definition the Consumer has since replaced is not raised.</summary>
    [Parameter] public EventCallback<SurfaceSlice> OnSliceChanged { get; set; }

    /// <summary>The name a saved file takes, without its extension; the title when not set, then "surface" (ADR-0282).</summary>
    [Parameter] public string? FileName { get; set; }

    private ScriptMount? _mount;
    private ElementReference _root;
    private SurfaceDefinition? _definition;
    private string? _definitionJson;
    private SurfaceValues _values = new([], []);
    private int _generation;
    private bool _renderOwed = true;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        _mount = new ScriptMount(JS, InvokeAsync, DispatchExceptionAsync, Prepare);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Definition);
        if (!ReferenceEquals(Definition, _definition))
        {
            // Checked before anything moves: a surface this adapter cannot carry leaves the drawing on the one it had.
            var (json, values) = ChartWire.Surface(Definition);
            _definition = Definition;
            _definitionJson = json;
            _values = values;
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
        if (await mount.StartAsync(Assets, _root, "attachSurface", State))
        {
            _renderOwed = true;
            StateHasChanged();
        }
    }

    /// <summary>
    /// The drawing as it stands — turned, zoomed and panned as the user left it — as SVG text that carries its size and
    /// its colours, for a file (ADR-0282). Null while the surface is not attached.
    /// </summary>
    public async ValueTask<string?> SvgTextAsync() =>
        _mount is { } mount ? await mount.AskTextAsync("svgText") : null;

    /// <summary>
    /// Saves the drawing as it stands, as SVG or painted to PNG at the screen's Device Pixel (ADR-0282). One call is one
    /// save. False for CSV, whose values "View as table" reads, while the surface is not attached, and once the circuit
    /// has gone.
    /// </summary>
    public Task<bool> DownloadAsync(ChartFileFormat format) =>
        _mount is { } mount ? mount.AskAsync("download", false, ChartWire.Camel(format)) : Task.FromResult(false);

    // The options as they stand, and the definition the component holds.
    private DrawingState State() => new(OptionsJson(), _definitionJson ?? throw new InvalidOperationException("Internal: the surface has no definition."), _generation);

    private string OptionsJson() => ChartWire.Json(json =>
    {
        json.WriteStartObject();
        json.WriteNumber("width", Width);
        json.WriteNumber("height", Height);
        json.WriteNumber("sliceWidth", SliceWidth);
        json.WriteString("density", ChartWire.Camel(Density));
        WriteText(json, "title", Title);
        WriteText(json, "description", Description);
        if (View is { } view)
        {
            json.WriteStartObject("view");
            json.WriteNumber("yaw", view.Yaw);
            json.WriteNumber("pitch", view.Pitch);
            json.WriteNumber("zoom", view.Zoom);
            json.WriteNumber("panX", view.PanX);
            json.WriteNumber("panY", view.PanY);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("view");
        }
        if (Slice is { } slice)
        {
            json.WriteStartObject("slice");
            json.WriteString("axis", ChartWire.Camel(slice.Axis));
            json.WritePropertyName("value");
            WriteSliceValue(json, slice);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("slice");
        }
        WriteText(json, "fileName", FileName);
        json.WriteStartArray("hears");
        if (OnViewChanged.HasDelegate)
            json.WriteStringValue("viewChanged");
        if (OnSliceChanged.HasDelegate)
            json.WriteStringValue("sliceChanged");
        json.WriteEndArray();
        json.WriteEndObject();
    });

    // A slice's value as the browser holds it: the axis' own value at its place, else the value as it crosses, which
    // the browser holds no slice at and says so, as the Razor component does.
    private void WriteSliceValue(Utf8JsonWriter json, SurfaceSlice slice)
    {
        var definition = _definition ?? throw new InvalidOperationException("Internal: the surface has no definition.");
        var (axis, values) = slice.Axis == SurfaceSliceAxis.X ? (definition.X, _values.X) : (definition.Y, _values.Y);
        var index = axis.IndexOf(slice.Value);
        var value = index >= 0 ? values[index] : ChartWire.SurfaceValue(axis, slice.Value);
        if (value is double number)
            ChartWire.WriteNumber(json, number);
        else
            json.WriteStringValue((string)value);
    }

    private Func<Task>? Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "viewChanged":
            {
                var view = new SurfaceView(
                    payload.GetProperty("yaw").GetDouble(), payload.GetProperty("pitch").GetDouble(), payload.GetProperty("zoom").GetDouble(),
                    payload.GetProperty("panX").GetDouble(), payload.GetProperty("panY").GetDouble());
                return () => OnViewChanged.InvokeAsync(view);
            }
            case "sliceChanged":
            {
                // Taken on a definition the Consumer has since replaced: its value is read against none (spine rule 6).
                if (payload.GetProperty("generation").GetInt32() != _generation)
                    return null;
                var slice = SliceOf(payload);
                return () => OnSliceChanged.InvokeAsync(slice);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    // The slice the browser raised, its value the axis' own, found by its place among the values the browser holds.
    private SurfaceSlice SliceOf(JsonElement payload)
    {
        var axis = payload.GetProperty("axis").GetString() switch
        {
            "x" => SurfaceSliceAxis.X,
            "y" => SurfaceSliceAxis.Y,
            var other => throw new InvalidOperationException($"Internal: the Script Core raised a slice on the axis '{other}'."),
        };
        // The definition the values were read from, which a parameter set that was refused leaves as it was.
        var definition = _definition ?? throw new InvalidOperationException("Internal: the surface has no definition.");
        var (definitionAxis, values) = axis == SurfaceSliceAxis.X ? (definition.X, _values.X) : (definition.Y, _values.Y);
        var raised = payload.GetProperty("value");
        object wire = raised.ValueKind == JsonValueKind.Number ? raised.GetDouble() : raised.GetString()
            ?? throw new InvalidOperationException("Internal: the Script Core raised a slice with no value.");
        var index = Array.FindIndex(values, v => Equals(v, wire));
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Internal: the Script Core raised a slice at {Convert.ToString(wire, CultureInfo.InvariantCulture)}, which the surface's axis does not hold.");
        }
        return new SurfaceSlice(axis, definitionAxis.Values[index]);
    }

    private static void WriteText(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null)
            json.WriteNull(name);
        else
            json.WriteString(name, value);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_mount is { } mount)
            await mount.DisposeAsync();
    }
}
