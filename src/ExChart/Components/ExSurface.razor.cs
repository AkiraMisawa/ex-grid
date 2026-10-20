using System.Globalization;
using System.Text.Json;
using global::ExChart.Engine;
using global::ExGrid;
using global::ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExChart.Components;

/// <summary>
/// A 3D surface of a <see cref="SurfaceDefinition"/> (ADR-0274): turned by dragging or with the arrow
/// keys, zoomed with the wheel or + and -, panned with Shift; the tooltip names the grid point nearest
/// the pointer, and a click on a tenor or a strike draws that slice as a 2D chart beside the surface,
/// where its values are read exactly. A hole in the grid is drawn as a hole.
/// <para>
/// The surface is projected and drawn in the browser while it is turned (ADR-0255's 3D entry); .NET
/// decides the mesh, its colour bands and its ticks. It holds no view state (ADR-0272): the view and
/// the slice are raised as events and taken back as parameters, in the data's terms.
/// </para>
/// </summary>
public partial class ExSurface
{
    /// <summary>The surface, in the data's terms.</summary>
    [Parameter, EditorRequired] public SurfaceDefinition Definition { get; set; } = null!;

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

    /// <summary>Raised when the user clicks a tenor or a strike, with the slice it names (ADR-0274).</summary>
    [Parameter] public EventCallback<SurfaceSlice> OnSliceChanged { get; set; }

    /// <summary>The name a saved file takes, without its extension; the title when not set, then "surface" (ADR-0282).</summary>
    [Parameter] public string? FileName { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = null!;

    private ElementReference _box;

    // What the user changed, as last taken from a parameter or a gesture.
    private SurfaceView _view = SurfaceView.Default;
    private SurfaceSlice? _slice;
    private (SurfaceView? View, SurfaceSlice? Slice, bool Taken) _parameters;

    // The mesh and what it was built from.
    private SurfaceMesh _mesh = null!;
    private string _summary = "";
    private object?[] _builtFrom = [];
    private ChartDefinition? _sliceChart;
    private string? _sliceNote;

    // The mesh's version (ADR-0255, ADR-0272): a report or a gesture taken against another is refused.
    private int _version;
    private bool _drawPending;
    private int _viewRevision;

    /// <summary>The mesh's version, as the 3D entry's reports must carry it.</summary>
    internal int Version => _version;

    private (int K, double X, double Y)? _point;
    private bool _tableOpen;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Definition);
        if (!_parameters.Taken || _parameters.View != View)
        {
            // A view the 3D entry reported comes back as an echo, equal to what is held, and keeps
            // the revision; only a view changed here is one the entry must take (spine rule 6).
            var view = View ?? SurfaceView.Default;
            if (!_parameters.Taken || view != _view) _viewRevision++;
            _view = view;
        }
        if (!_parameters.Taken || _parameters.Slice != Slice) _slice = Slice;
        if (_parameters.Taken && (_parameters.View != View || _parameters.Slice != Slice)) _drawPending = true;
        _parameters = (View, Slice, true);

        var builtFrom = new object?[] { Definition, Width, Height, Title, Description };
        if (!builtFrom.SequenceEqual(_builtFrom))
        {
            var definitionChanged = _builtFrom.Length == 0 || !ReferenceEquals(_builtFrom[0], Definition);
            _builtFrom = builtFrom;
            if (definitionChanged) _mesh = SurfaceMesh.Of(Definition);
            _summary = SurfaceMesh.Summary(Definition, Title, Description);
            _version++;
            _drawPending = true;
            _point = null;
        }
        BuildSlice();
    }

    // The slice, in the data's terms: one the surface does not hold is said, not drawn (ADR-0272).
    private void BuildSlice()
    {
        _sliceChart = _slice is { } s ? Definition.SliceChart(s) : null;
        _sliceNote = _slice is { } t && _sliceChart is null
            ? $"No slice at {AxisName(t.Axis == SurfaceSliceAxis.X ? Definition.X.Title : Definition.Y.Title, t.Axis == SurfaceSliceAxis.X ? "x" : "y")} {(t.Axis == SurfaceSliceAxis.X ? Definition.X : Definition.Y).Write(t.Value)}: "
              + (Definition.IsMesh ? "no triangle of the mesh crosses there." : "this surface has no such value.")
            : null;
    }

    private object? SliceForScript
    {
        get
        {
            if (_slice is not { } s || _sliceChart is null) return null;
            return SurfaceMesh.SlicePath(Definition, s);
        }
    }

    /// <summary>
    /// The drawing as it stands — turned, zoomed and panned as the user left it — as SVG text that
    /// carries its size and its colours, for a file (ADR-0282). Null while the 3D entry is not attached.
    /// </summary>
    public async ValueTask<string?> SvgTextAsync()
    {
        var handle = _handle;
        if (handle is null || _disposed) return null;
        try
        {
            return await handle.InvokeAsync<string?>("svgText");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the drawing as it stands as an SVG or a PNG file, the PNG painted from that SVG at the
    /// screen's Device Pixel (ADR-0282). One call is one save. False when the 3D entry is not attached,
    /// which the surface says on its face, or the circuit has gone. A CSV is not a surface's file: its
    /// values are read in "View as table".
    /// </summary>
    public async Task<bool> DownloadAsync(ChartFileFormat format)
    {
        var module = _module;
        if (module is null || _scriptRefused || _disposed || format == ChartFileFormat.Csv) return false;
        var name = ChartFiles.Name(FileName, string.IsNullOrWhiteSpace(Title) ? "surface" : Title, format);
        var handle = _handle;
        if (handle is null) return false;
        try
        {
            // The drawing's text stays in the browser, which paints and saves it (ADR-0282).
            return await module.InvokeAsync<bool>("saveDrawing", handle, name, format == ChartFileFormat.Png ? "png" : "svg", Width, Height);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            return false;
        }
    }

    private Task DownloadSvgAsync() => DownloadAsync(ChartFileFormat.Svg);

    private Task DownloadPngAsync() => DownloadAsync(ChartFileFormat.Png);

    // A file needs the 3D entry: until it holds its first drawing, and when it is refused, the buttons
    // say so by being unavailable rather than doing nothing (spine rule 1).
    private bool FilesUnavailable => !_told || _scriptRefused;

    internal Task Dispatch(Func<Task> work) => InvokeAsync(work);

    // The 3D entry's reports (ADR-0255), through SurfaceReports. Each carries the mesh's version,
    // and one taken against another mesh is dropped (spine rule 6).
    internal void OnPoint(int version, int node, double x, double y)
    {
        if (version != _version) return;
        _point = node >= 0 && node < Definition.NodeCount && Definition.Node(node).Z is not null ? (node, x, y) : null;
        StateHasChanged();
    }

    // A click on a point slices along the line it fell on: a grid's tenor or strike, or on a mesh the
    // position of the node it fell on, which cuts its triangles (ADR-0311).
    internal async Task OnSliceAsync(int version, string axis, int node)
    {
        if (version != _version) return;
        if (node < 0 || node >= Definition.NodeCount) return;
        var point = Definition.Node(node);
        var slice = axis == "x" ? new SurfaceSlice(SurfaceSliceAxis.X, point.X) : new SurfaceSlice(SurfaceSliceAxis.Y, point.Y);
        _slice = slice;
        BuildSlice();
        _drawPending = true;
        StateHasChanged();
        await OnSliceChanged.InvokeAsync(slice);
    }

    internal async Task OnViewAsync(int version, double yaw, double pitch, double zoom, double panX, double panY)
    {
        if (version != _version) return;
        var view = new SurfaceView(Math.Round(yaw, 2), Math.Round(pitch, 2), Math.Round(zoom, 4), Math.Round(panX, 4), Math.Round(panY, 4));
        if (view == _view) return;
        _view = view;
        StateHasChanged();
        await OnViewChanged.InvokeAsync(view);
    }

    private async Task ResetViewAsync()
    {
        _view = SurfaceView.Default;
        _viewRevision++;
        _drawPending = true;
        await OnViewChanged.InvokeAsync(_view);
    }

    private void ToggleTable() => _tableOpen = !_tableOpen;

    private static string AxisName(string title, string fallback) => string.IsNullOrWhiteSpace(title) ? fallback : title;

    private string RootStyle => string.Create(CultureInfo.InvariantCulture,
        $"font-size:{Svg.N(GridMetrics.Resolve(Density).FontSizePx)}px;--ex-chart-line-height:{Svg.N(Math.Ceiling(GridMetrics.Resolve(Density).FontSizePx * 1.4))}px");

    private string StageStyle => string.Create(CultureInfo.InvariantCulture, $"width:{Svg.N(Width)}px;height:{Svg.N(Height)}px");

    private string TooltipStyle((int K, double X, double Y) p)
    {
        var gap = Math.Ceiling(GridMetrics.Resolve(Density).FontSizePx);
        var left = p.X > Width / 2 ? p.X - gap : p.X + gap;
        return string.Create(CultureInfo.InvariantCulture, $"left:{Svg.N(left)}px;top:{Svg.N(p.Y + gap)}px");
    }

    // The slice is read on the surface's vertical scale (ADR-0312).
    private AxisScale? SliceScale => Definition.EffectiveZScale == AxisScale.Logarithmic ? AxisScale.Logarithmic : null;

    private string ToolbarName => string.IsNullOrWhiteSpace(Title) ? "Surface" : Title;

    private static string StylesheetNotice => BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet);

    private static string ScriptNotice => BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script);

    // ---- The 3D entry (ADR-0255, fourteenth on ADR-0021's list): in the chart's module, imported at
    // its versioned address and refused when its stamp is not this build's (ADR-0123).

    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private DotNetObjectReference<SurfaceReports>? _reports;
    private bool _scriptRefused;
    private bool _disposed;
    private bool _told;

    // Busy until the 3D entry holds its first drawing (A11Y-20's shape): a drag before then reaches
    // no listener, so a user, or a script, waits for the mark to go (spine rule 6).
    private bool Busy => !_told && !_scriptRefused;

    private string ModuleAddress
    {
        get
        {
            var fingerprinted = Assets[ShippedAssets.ScriptPath];
            return "./" + (fingerprinted != ShippedAssets.ScriptPath ? fingerprinted : $"{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}");
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await AttachAsync();
        if (_handle is not null && _drawPending && !_disposed)
        {
            _drawPending = false;
            try
            {
                await _handle.InvokeVoidAsync("draw", _version, _mesh, _view, _viewRevision, SliceForScript, Width, Height);
                if (!_told)
                {
                    _told = true;
                    StateHasChanged();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }
    }

    private async Task AttachAsync()
    {
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModuleAddress);
            if (_module is null || !await ScriptIsOwnAsync(_module))
            {
                RefuseScript();
                return;
            }
            _reports = DotNetObjectReference.Create(new SurfaceReports(this));
            _handle = await _module.InvokeAsync<IJSObjectReference>("attachSurface", _box, _reports);
        }
        catch (JSException)
        {
            RefuseScript();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    private static async Task<bool> ScriptIsOwnAsync(IJSObjectReference module)
    {
        try
        {
            return await module.InvokeAsync<string?>("stamp") == ShippedAssets.ScriptStamp;
        }
        catch (Exception ex) when (ex is JsonException or JSException)
        {
            return false;
        }
    }

    // Without its script the surface cannot be drawn: the box stays empty and the page says it
    // holds another version's script (ADR-0123); the table still reads the values.
    private void RefuseScript()
    {
        if (_disposed || _scriptRefused) return;
        _scriptRefused = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_handle is not null)
            {
                await _handle.InvokeVoidAsync("dispose");
                await _handle.DisposeAsync();
            }
            if (_module is not null)
                await _module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
        _reports?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>What the 3D entry calls (ADR-0255). Internal, so its methods are no part of the public API.</summary>
internal sealed class SurfaceReports(ExSurface surface)
{
    [JSInvokable]
    public Task OnPointAsync(int version, int node, double x, double y) => surface.Dispatch(() => { surface.OnPoint(version, node, x, y); return Task.CompletedTask; });

    [JSInvokable]
    public Task OnSliceAsync(int version, string axis, int node) => surface.Dispatch(() => surface.OnSliceAsync(version, axis, node));

    [JSInvokable]
    public Task OnViewAsync(int version, double yaw, double pitch, double zoom, double panX, double panY) =>
        surface.Dispatch(() => surface.OnViewAsync(version, yaw, pitch, zoom, panX, panY));
}
