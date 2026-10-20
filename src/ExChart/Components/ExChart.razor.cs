using System.Globalization;
using System.Text.Json;
using global::ExChart.Engine;
using global::ExGrid;
using global::ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExChart.Components;

/// <summary>
/// A chart of a <see cref="ChartDefinition"/>, drawn as SVG elements Blazor diffs (ADR-0252): a
/// component per panel's axes, per series and per legend, each rendering only when its piece of the
/// scene changed, so a move of the pointer renders the tooltip and the guide and nothing else.
/// <para>
/// It holds no view state (ADR-0272): what the user changes — a series hidden from the legend, the
/// range narrowed by a drag or under Chart Filters, the axis switched — is raised as an event and
/// taken back as a parameter, in the data's terms, so it applies to new data as it is.
/// </para>
/// </summary>
public partial class ExChart
{
    /// <summary>The chart, in the data's terms.</summary>
    [Parameter, EditorRequired] public ChartDefinition Definition { get; set; } = null!;

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
    /// Raised when the user clicks a point: pressed and released at one position, on the series
    /// nearest the pointer that has a value there (ADR-0258). A grid bridge shows its cell.
    /// </summary>
    [Parameter] public EventCallback<ChartPointClick> OnPointClick { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = null!;

    private static readonly IReadOnlySet<string> NoneHidden = new HashSet<string>();

    private ElementReference _root;
    private readonly string _summaryId = "ex-chart-" + Guid.NewGuid().ToString("N")[..12];
    private readonly EventCallback<string> _toggle;

    // What the user changed, as last taken from a parameter or a gesture.
    private IReadOnlySet<string> _hidden = NoneHidden;
    private ChartRange? _range;
    private AxisScale? _scale;
    private (IReadOnlySet<string>? Hidden, ChartRange? Range, AxisScale? Scale, bool Taken) _parameters;

    // The scene and what it was built from.
    private ChartMetrics _metrics = new(800, 400);
    private ChartView _view = ChartView.All;
    private ChartScene _scene = null!;
    private string _summary = "";
    private ChartTable? _table;
    private object?[] _builtFrom = [];

    // The drawing's version (ADR-0255, ADR-0272): a report or a drag taken against another is refused.
    private int _version;
    private bool _drawPending;

    /// <summary>The drawing's version, as the pointer entry's reports must carry it.</summary>
    internal int Version => _version;

    internal ChartScene Scene => _scene;

    // The pointer, as the script last reported it, and a drag's press.
    private int? _pointerIndex;
    private int _pointerPanel;
    private ChartReading? _reading;
    private EquatableList<(double Y, SeriesLook Look)> _dots = EquatableList<(double, SeriesLook)>.Empty;
    private (int Version, int Index)? _press;

    private bool _filtersOpen;
    private bool _tableOpen;
    private string? _fromText;
    private string? _toText;
    private string? _filterError;

    /// <summary>Creates the chart.</summary>
    public ExChart() => _toggle = EventCallback.Factory.Create<string>(this, ToggleAsync);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Definition);
        // Taken back as plain parameters (ADR-0272): a parameter that changed replaces what the
        // user last did; one that did not leaves it.
        if (!_parameters.Taken || !ReferenceEquals(_parameters.Hidden, HiddenSeries)) _hidden = HiddenSeries ?? NoneHidden;
        if (!_parameters.Taken || _parameters.Range != Range) _range = Range;
        if (!_parameters.Taken || _parameters.Scale != Scale) _scale = Scale;
        _parameters = (HiddenSeries, Range, Scale, true);
        Rebuild();
    }

    private void Rebuild()
    {
        var builtFrom = new object?[] { Definition, Width, Height, Density, Legend, AxisStyle, IndependentScales, ColourSlots, _hidden, _range, _scale, YScale };
        if (builtFrom.SequenceEqual(_builtFrom)) return;
        var definitionChanged = _builtFrom.Length == 0 || !ReferenceEquals(_builtFrom[0], Definition);
        _builtFrom = builtFrom;

        var grid = GridMetrics.Resolve(Density);
        var font = grid.FontSizePx;
        var cells = grid.CellMetrics;
        // Two device pixels a CSS pixel: Thinning keeps every peak at up to twice the resolution
        // the chart is told nothing about, and no more (ADR-0254).
        _metrics = new ChartMetrics(Width, Height, DevicePixelRatio: 2, LineHeight: Math.Ceiling(font * 1.4), Padding: Math.Round(font * 0.6),
            PanelGap: Math.Round(font * 1.2), TextWidth: s => cells.TextWidthPx(s), ColourSlots: ColourSlots);
        _view = new ChartView(_hidden, _range?.From, _range?.To, _scale, _range?.YFrom, _range?.YTo, YScale);
        var previous = _scene;
        _scene = ChartScene.Build(Definition, _metrics, _view, AxisStyle, IndependentScales, Legend);
        _summary = ChartSummary.Of(Definition, _scene, _view, Title, Description);
        _table = _tableOpen ? ChartCsv.WideTable(Definition, _metrics, _view) : null;

        if (definitionChanged || previous is null || !previous.Positions.Equals(_scene.Positions) || !previous.Marks.Equals(_scene.Marks)
            || !previous.Panels.Select(p => p.Plot).SequenceEqual(_scene.Panels.Select(p => p.Plot)))
        {
            _version++;
            _drawPending = true;
            ClearPointer();
        }
        else if (_pointerIndex is { } i)
        {
            Read(i, _pointerPanel, _readingY);
        }
    }

    private double _readingY;

    private void ClearPointer()
    {
        _pointerIndex = null;
        _reading = null;
        _dots = EquatableList<(double, SeriesLook)>.Empty;
    }

    private void Read(int index, int panel, double y)
    {
        if (_scene.Scatter)
        {
            ReadMark(index);
            return;
        }
        if (index < 0 || index >= _scene.Positions.Count || panel < 0 || panel >= _scene.Panels.Count)
        {
            ClearPointer();
            return;
        }
        _pointerIndex = index;
        _pointerPanel = panel;
        _readingY = y;
        _reading = ChartReadings.At(Definition, _scene, _metrics, _view, _scene.Positions[index].Position, panel, y);
        _dots = _reading.Lines.Where(l => l.Y is not null).Select(l => (l.Y!.Value, l.Look)).ToEquatableList();
    }

    // On a scatter the pointer is over the nearest mark, if any (ADR-0290).
    private void ReadMark(int index)
    {
        if (index < 0 || index >= _scene.Marks.Count)
        {
            ClearPointer();
            return;
        }
        var m = _scene.Marks[index];
        _pointerIndex = index;
        _pointerPanel = m.Panel;
        _readingY = m.Y;
        _reading = ChartReadings.OfMark(Definition, _scene, index);
        _dots = _reading.Lines.Select(l => (m.Y, l.Look)).ToEquatableList();
    }

    // The pointer entry's reports (ADR-0255), through ChartPointer. Each carries the drawing's
    // version, and one taken against another drawing is dropped (spine rule 6).
    internal void OnPointer(int version, int index, int panel, double y)
    {
        if (version != _version) return;
        if (index < 0) ClearPointer();
        else Read(index, panel, y);
        StateHasChanged();
    }

    /// <summary>Runs a report on the renderer's own context, as any event of the chart's runs.</summary>
    internal Task Dispatch(Func<Task> work) => InvokeAsync(work);

    internal void OnPress(int version, int index, double x = 0, double y = 0, int panel = 0)
    {
        if (version != _version) return;
        if (_scene.Scatter)
        {
            if (panel < 0 || panel >= _scene.Panels.Count) return;
            _pressAt = (version, index, x, y, panel);
            _dragAt = null;
            return;
        }
        if (index < 0 || index >= _scene.Positions.Count) return;
        _press = (version, index);
    }

    // A scatter's press, at the lattice cell the pointer entry snapped it to, and where a drag from it
    // has come to (ADR-0290).
    private (int Version, int Index, double X, double Y, int Panel)? _pressAt;
    private (double X, double Y)? _dragAt;

    internal void OnDrag(int version, double x, double y)
    {
        if (version != _version || _pressAt is not { } p || p.Version != version) return;
        _dragAt = (x, y);
        StateHasChanged();
    }

    // A scatter's release (ADR-0290): on the mark it was pressed on, a click on that point; elsewhere,
    // the rectangle from the press narrows both axes. Against another drawing, nothing (spine rule 6).
    private async Task OnScatterReleaseAsync(int version, int index, double x, double y)
    {
        var press = _pressAt;
        _pressAt = null;
        _dragAt = null;
        if (press is not { } p) return;
        // Released outside the plot, or against another drawing: nothing (spine rule 6).
        if (p.Version != _version || version != _version || x < 0 || y < 0)
        {
            StateHasChanged();
            return;
        }
        // Within one cell of the lattice it was pressed on, a click; a slip of a pixel across a cell's
        // edge is still a click (ADR-0290).
        if (Math.Abs(x - p.X) <= DragCell && Math.Abs(y - p.Y) <= DragCell)
        {
            StateHasChanged();
            if (index >= 0 && index == p.Index && index < _scene.Marks.Count)
            {
                var m = _scene.Marks[index];
                var point = Definition.Series.First(s => s.Id == m.SeriesId).Points[m.Point];
                await OnPointClick.InvokeAsync(new ChartPointClick(m.SeriesId, point.X, point.Row));
            }
            return;
        }
        var panel = _scene.Panels[p.Panel];
        var (x0, x1) = (_scene.PositionAt(Math.Min(p.X, x)), _scene.PositionAt(Math.Max(p.X, x)));
        // The left axis is narrowed only where it is the chart's one vertical axis that a rectangle
        // can mean: one panel. Elsewhere the rectangle narrows the axis across alone (ADR-0290).
        if (panel.Left is not { } left || _scene.Panels.Count > 1)
        {
            await SetRangeAsync(new ChartRange(x0, x1));
            return;
        }
        // Down the page is down the axis: the lower edge is the larger pixel.
        await SetRangeAsync(new ChartRange(x0, x1, left.ValueAt(Math.Max(p.Y, y), panel.Plot), left.ValueAt(Math.Min(p.Y, y), panel.Plot)));
    }

    // A drag ends (ADR-0273): narrowed to the positions it began and ended on, unless the drawing
    // it began against is no longer the one shown (ADR-0272).
    internal async Task OnReleaseAsync(int version, int index, double x = 0, double y = 0)
    {
        if (_scene.Scatter)
        {
            await OnScatterReleaseAsync(version, index, x, y);
            return;
        }
        var press = _press;
        _press = null;
        if (press is not { } p) return;
        if (p.Version != _version || version != _version || index < 0 || index >= _scene.Positions.Count)
        {
            StateHasChanged();
            return;
        }
        if (index == p.Index)
        {
            // A click, read from the reading the pointer's position holds: one taken at another
            // position, or none, names no point, and nothing is raised (spine rule 6).
            if (_pointerIndex == index && _reading?.Lines.Where(l => l.Y is not null && l.Value is not null)
                    .MinBy(l => Math.Abs(l.Y!.Value - _readingY)) is { } line)
                await OnPointClick.InvokeAsync(new ChartPointClick(line.Id, _scene.Positions[index].Position));
            return;
        }
        var a = _scene.Positions[Math.Min(p.Index, index)].Position;
        var b = _scene.Positions[Math.Max(p.Index, index)].Position;
        await SetRangeAsync(new ChartRange(a, b));
    }

    private async Task SetRangeAsync(ChartRange? range)
    {
        _range = range;
        _filterError = null;
        Rebuild();
        StateHasChanged();
        await OnRangeChanged.InvokeAsync(range);
    }

    private Task ResetRangeAsync() => _range is null ? Task.CompletedTask : SetRangeAsync(null);

    private async Task ToggleAsync(string key)
    {
        var hidden = new HashSet<string>(_hidden);
        var nowHidden = hidden.Add(key) || !hidden.Remove(key);
        _hidden = hidden;
        Rebuild();
        await OnSeriesVisibilityChanged.InvokeAsync(new SeriesVisibility(key, nowHidden));
    }

    private void ToggleFilters()
    {
        _filtersOpen = !_filtersOpen;
        if (!_filtersOpen) return;
        _fromText = _range?.From is { } f ? Invariant(f) : null;
        _toText = _range?.To is { } t ? Invariant(t) : null;
        _filterError = null;
    }

    private void ToggleTable()
    {
        _tableOpen = !_tableOpen;
        _table = _tableOpen ? ChartCsv.WideTable(Definition, _metrics, _view) : null;
    }

    // The range typed under Chart Filters (ADR-0273), in the axis' own values. A value that does
    // not read as one is said, never guessed (spine rule 1).
    private async Task ApplyTypedRangeAsync()
    {
        if (!TryRead(_fromText, out var from) || !TryRead(_toText, out var to))
        {
            var bad = !TryRead(_fromText, out _) ? _fromText : _toText;
            _filterError = $"“{bad}” is not {AxisKind} on this axis.";
            return;
        }
        if (from > to)
        {
            _filterError = "From is after To.";
            return;
        }
        await SetRangeAsync(from is null && to is null ? null : new ChartRange(from, to));
    }

    private async Task SetScaleAsync(string value)
    {
        if (!Enum.TryParse<AxisScale>(value, out var scale) || scale == (_scale ?? Definition.Axis.EffectiveScale)) return;
        _scale = scale;
        Rebuild();
        await OnScaleChanged.InvokeAsync(scale);
    }

    private string ScaleText => (_scale ?? Definition.Axis.EffectiveScale).ToString();

    private bool ScaleSwitchable => Definition.Axis.Values != AxisValues.Labels && Definition.Series.All(s => s.Sampler is null);

    private string AxisKind => Definition.Axis.Values switch
    {
        AxisValues.Dates => "a date (yyyy-MM-dd)",
        AxisValues.Labels => "a category",
        _ => "a number",
    };

    private string RangeHint => Definition.Axis.Values switch
    {
        AxisValues.Dates => "yyyy-MM-dd",
        AxisValues.Labels => "category",
        _ => "number",
    };

    private bool TryRead(string? text, out double? position)
    {
        position = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = text.Trim();
        var axis = Definition.Axis;
        switch (axis.Values)
        {
            case AxisValues.Dates:
                if (DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) position = d.DayNumber;
                break;
            case AxisValues.Labels:
                var i = axis.Labels.ToList().FindIndex(l => string.Equals(l, t, StringComparison.Ordinal));
                if (i < 0) i = axis.Labels.ToList().FindIndex(l => string.Equals(l, t, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) position = i;
                break;
            default:
                var named = axis.Named.FirstOrDefault(n => string.Equals(n.Label, t, StringComparison.OrdinalIgnoreCase));
                if (named.Label is not null) position = named.Position;
                else if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)) position = n;
                break;
        }
        return position is not null;
    }

    private string Invariant(double position) => Definition.Axis.Values switch
    {
        AxisValues.Dates => DateOnly.FromDayNumber((int)position).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        AxisValues.Labels when position >= 0 && position < Definition.Axis.Labels.Count => Definition.Axis.Labels[(int)position],
        _ => position.ToString("R", CultureInfo.InvariantCulture),
    };

    // The parts are internal components, which markup cannot name, so they are opened here.
    private RenderFragment Drawing => b =>
    {
        var lh = _metrics.LineHeight;
        for (var i = 0; i < _scene.Panels.Count; i++)
        {
            var panel = _scene.Panels[i];
            b.OpenComponent<ChartAxesView>(0);
            b.SetKey("axes:" + panel.Name);
            b.AddComponentParameter(1, nameof(ChartAxesView.Panel), panel);
            b.AddComponentParameter(2, nameof(ChartAxesView.XTicks), _scene.XTicks);
            b.AddComponentParameter(3, nameof(ChartAxesView.Last), i == _scene.Panels.Count - 1);
            b.AddComponentParameter(4, nameof(ChartAxesView.LineHeight), lh);
            b.AddComponentParameter(5, nameof(ChartAxesView.Angle), AxisStyle?.Angle ?? 0);
            b.CloseComponent();
            foreach (var piece in panel.Series)
            {
                b.OpenComponent<ChartSeriesView>(6);
                b.SetKey("series:" + piece.Id);
                b.AddComponentParameter(7, nameof(ChartSeriesView.Piece), piece);
                b.AddComponentParameter(8, nameof(ChartSeriesView.Plot), panel.Plot);
                b.AddComponentParameter(9, nameof(ChartSeriesView.LineHeight), lh);
                b.CloseComponent();
            }
            b.OpenComponent<ChartLegendView>(10);
            b.SetKey("legend:" + panel.Name);
            b.AddComponentParameter(11, nameof(ChartLegendView.Entries), panel.Legend);
            b.AddComponentParameter(12, nameof(ChartLegendView.LineHeight), lh);
            b.AddComponentParameter(13, nameof(ChartLegendView.Toggle), _toggle);
            b.CloseComponent();
        }
        b.OpenComponent<ChartBandView>(14);
        b.AddComponentParameter(15, nameof(ChartBandView.Span), BandSpan);
        b.AddComponentParameter(16, nameof(ChartBandView.Top), BandTop);
        b.AddComponentParameter(17, nameof(ChartBandView.Bottom), BandBottom);
        b.CloseComponent();
        b.OpenComponent<ChartGuideView>(18);
        b.AddComponentParameter(19, nameof(ChartGuideView.X), GuideX);
        b.AddComponentParameter(20, nameof(ChartGuideView.Top), GuideTop);
        b.AddComponentParameter(21, nameof(ChartGuideView.Bottom), GuideBottom);
        b.AddComponentParameter(22, nameof(ChartGuideView.Dots), _dots);
        b.AddComponentParameter(23, nameof(ChartGuideView.LineHeight), lh);
        b.CloseComponent();
    };

    private RenderFragment Tooltip => b =>
    {
        b.OpenComponent<ChartTooltipView>(0);
        b.AddComponentParameter(1, nameof(ChartTooltipView.Reading), _reading);
        b.AddComponentParameter(2, nameof(ChartTooltipView.Left), TooltipLeft);
        b.AddComponentParameter(3, nameof(ChartTooltipView.Top), TooltipTop);
        b.AddComponentParameter(4, nameof(ChartTooltipView.Flip), TooltipFlip);
        b.AddComponentParameter(5, nameof(ChartTooltipView.LineHeight), _metrics.LineHeight);
        b.CloseComponent();
    };

    // The lattice, in CSS pixels, a drag across a scatter is reported on: one report a cell, not one a
    // pointer move (ADR-0255, ADR-0290).
    internal const double DragCell = 4;

    private double PlotsTop => _scene.Panels.Count == 0 ? 0 : _scene.Panels[0].Plot.Y;

    private double PlotsBottom => _scene.Panels.Count == 0 ? 0 : _scene.Panels[^1].Plot.Y + _scene.Panels[^1].Plot.Height;

    private double? GuideX => _pointerIndex is { } i ? (_scene.Scatter ? _scene.Marks[i].X : _scene.Positions[i].X) : null;

    private double GuideTop => _scene.Scatter && _pointerIndex is { } i ? _scene.Marks[i].Y : PlotsTop;

    private double GuideBottom => _scene.Scatter && _pointerIndex is { } i ? _scene.Marks[i].Y : PlotsBottom;

    private (double From, double To)? BandSpan => _scene.Scatter
        ? _pressAt is { } q && q.Version == _version && _dragAt is { } d ? (q.X, d.X) : null
        : _press is { } p && p.Version == _version && _pointerIndex is { } i && i != p.Index ? (_scene.Positions[p.Index].X, _scene.Positions[i].X) : null;

    private double BandTop => _scene.Scatter && _pressAt is { } q && _dragAt is { } d ? Math.Min(q.Y, d.Y) : PlotsTop;

    private double BandBottom => _scene.Scatter && _pressAt is { } q && _dragAt is { } d ? Math.Max(q.Y, d.Y) : PlotsBottom;

    private double TooltipLeft => GuideX is { } x ? (TooltipFlip ? x - _metrics.LineHeight / 2 : x + _metrics.LineHeight / 2) : 0;

    private double TooltipTop => _pointerIndex is null ? 0 : _scene.Panels[_pointerPanel].Plot.Y;

    private bool TooltipFlip => GuideX is { } x && x > Width / 2;

    private string RootStyle => string.Create(CultureInfo.InvariantCulture,
        $"width:{Svg.N(Width)}px;font-size:{Svg.N(GridMetrics.Resolve(Density).FontSizePx)}px;--ex-chart-line-height:{Svg.N(_metrics.LineHeight)}px");

    private string StageStyle => string.Create(CultureInfo.InvariantCulture, $"width:{Svg.N(Width)}px;height:{Svg.N(Height)}px");

    private static string HitStyle(Box plot) => string.Create(CultureInfo.InvariantCulture,
        $"left:{Svg.N(plot.X)}px;top:{Svg.N(plot.Y)}px;width:{Svg.N(plot.Width)}px;height:{Svg.N(plot.Height)}px");

    private string ToolbarName => string.IsNullOrWhiteSpace(Title) ? "Chart" : Title;

    private static string StylesheetNotice => BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet);

    private static string ScriptNotice => BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script);

    // ---- The pointer entry (ADR-0255, eleventh): imported at its versioned address, refused when
    // its stamp is not this build's (ADR-0123).

    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private DotNetObjectReference<ChartPointer>? _pointer;
    private bool _scriptRefused;
    private bool _disposed;

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
            var version = _version;
            var xs = _scene.Positions.Select(p => Math.Round(p.X, 2)).ToArray();
            // A scatter's marks, as x, y and panel in turn, with how far from one the pointer still
            // finds it and the lattice a drag is reported on (ADR-0290).
            var marks = _scene.Scatter ? _scene.Marks.SelectMany(m => new[] { Math.Round(m.X, 2), Math.Round(m.Y, 2), m.Panel }).ToArray() : null;
            try
            {
                await _handle.InvokeVoidAsync("draw", version, xs, marks, Math.Round(_metrics.LineHeight * 0.6, 2), DragCell);
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

    // Busy until the pointer entry holds its first drawing, as a grid is while Prerendered (A11Y-20):
    // a press or a move before then reaches no listener and is lost, so a user, or a script, waits
    // for the mark to go (spine rule 6). A refused script ends it too: nothing more is coming.
    private bool _told;

    private bool Busy => !_told && !_scriptRefused;

    private async Task AttachAsync()
    {
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModuleAddress);
            if (_module is null || !await ScriptIsOwnAsync(_module))
            {
                await RefuseScriptAsync();
                return;
            }
            _pointer = DotNetObjectReference.Create(new ChartPointer(this));
            _handle = await _module.InvokeAsync<IJSObjectReference>("attach", _root, _pointer);
        }
        catch (JSException)
        {
            await RefuseScriptAsync();
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
        catch (JsonException)
        {
            return false;
        }
        catch (JSException)
        {
            return false;
        }
    }

    // The chart still draws without its script; only the tooltip and the drag need it, and the
    // page says it holds another version's script (ADR-0123).
    private Task RefuseScriptAsync()
    {
        if (_disposed || _scriptRefused) return Task.CompletedTask;
        _scriptRefused = true;
        StateHasChanged();
        return Task.CompletedTask;
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
        _pointer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>What the pointer entry calls (ADR-0255). Internal, so its methods are no part of the public API.</summary>
internal sealed class ChartPointer(ExChart chart)
{
    [JSInvokable]
    public Task OnPointerAsync(int version, int index, int panel, double y) => Dispatch(() => { chart.OnPointer(version, index, panel, y); return Task.CompletedTask; });

    [JSInvokable]
    public Task OnPressAsync(int version, int index, double x, double y, int panel) => Dispatch(() => { chart.OnPress(version, index, x, y, panel); return Task.CompletedTask; });

    [JSInvokable]
    public Task OnDragAsync(int version, double x, double y) => Dispatch(() => { chart.OnDrag(version, x, y); return Task.CompletedTask; });

    [JSInvokable]
    public Task OnReleaseAsync(int version, int index, double x, double y) => Dispatch(() => chart.OnReleaseAsync(version, index, x, y));

    private Task Dispatch(Func<Task> work) => chart.Dispatch(work);
}
