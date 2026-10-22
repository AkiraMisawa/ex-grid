using System.Globalization;
using System.Text.Json;
using global::ExChart.Chrome;
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
    /// <summary>The chart, in the data's terms; or null when it is read from a <see cref="Spec"/> through <see cref="Sources"/>.</summary>
    [Parameter] public ChartDefinition? Definition { get; set; }

    /// <summary>
    /// A Chart Spec the chart reads through <see cref="Sources"/> in place of a <see cref="Definition"/>
    /// (ADR-0322, ADR-0344): asked for the plot's pixels when it is first drawn and again whenever the
    /// range changes, so a narrowed range is drawn at full detail. What it shows stays until the answer
    /// to the newest ask arrives, Busy meanwhile; an answer to an ask a later one replaced is dropped, and
    /// one from data older than the chart shows is said in its place, never drawn.
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
    /// Raised when the user clicks a point: pressed and released at one position, on the series
    /// nearest the pointer that has a value there (ADR-0258). A grid bridge shows its cell.
    /// </summary>
    [Parameter] public EventCallback<ChartPointClick> OnPointClick { get; set; }

    /// <summary>
    /// What draws the toolbar and the context menu (ADR-0281): ExChart.MudBlazor's
    /// <c>MudChartChrome</c>, or null for the built-in markup. The legend, the tooltip and the plot
    /// are ExChart's under any Chrome, and swapping it changes no behaviour.
    /// </summary>
    [Parameter] public IChartChrome? Chrome { get; set; }

    /// <summary>The name a saved file takes, without its extension; the title's when null, else "chart" (ADR-0282).</summary>
    [Parameter] public string? FileName { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Inject] private IServiceProvider Services { get; set; } = null!;

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

    /// <summary>The scene as drawn, for the tests.</summary>
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
    public ExChart()
    {
        _toggle = EventCallback.Factory.Create<string>(this, ToggleAsync);
        // Held once, so a context built from them is equal to the last while nothing changed.
        _toggleFilters = () => { ToggleFilters(); StateHasChanged(); return Task.CompletedTask; };
        _resetRange = ResetRangeAsync;
        _toggleTable = () => { ToggleTable(); StateHasChanged(); return Task.CompletedTask; };
        _csv = DownloadCsvAsync;
        _svg = DownloadSvgAsync;
        _png = DownloadPngAsync;
        _apply = ApplyRangeAsync;
        _setScale = scale => SetScaleAsync(scale.ToString());
        _fromMenu = new Dictionary<string, Func<Task>>
        {
            [ChartCommandIds.ResetRange] = () => FromMenuAsync(_resetRange),
            [ChartCommandIds.Table] = () => FromMenuAsync(_toggleTable),
            [ChartCommandIds.DownloadCsv] = () => FromMenuAsync(_csv),
            [ChartCommandIds.DownloadSvg] = () => FromMenuAsync(_svg),
            [ChartCommandIds.DownloadPng] = () => FromMenuAsync(_png),
        };
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Spec is null && Definition is null)
            throw new InvalidOperationException("An ExChart is given its Definition, or a Spec and the Sources it is read through.");
        if (Spec is not null && (Definition is not null || Sources is null))
            throw new InvalidOperationException("An ExChart read from a Spec is given the Sources it is read through, and no Definition beside them (ADR-0344).");
        // Taken back as plain parameters (ADR-0272): a parameter that changed replaces what the
        // user last did; one that did not leaves it.
        if (!_parameters.Taken || !ReferenceEquals(_parameters.Hidden, HiddenSeries)) _hidden = HiddenSeries ?? NoneHidden;
        if (!_parameters.Taken || _parameters.Range != Range) _range = Range;
        if (!_parameters.Taken || _parameters.Scale != Scale) _scale = Scale;
        var rangeChanged = _asked == 0 || _askedRange != _range;
        _parameters = (HiddenSeries, Range, Scale, true);
        if (Spec is not null && (!ReferenceEquals(_askedSpec, Spec) || !ReferenceEquals(_askedSources, Sources)))
        {
            // Another spec or other sources: what was read from the last is not drawn under the new.
            _read = null;
            _versions = new Dictionary<string, long>();
            _refusal = null;
            _ = AskAsync();
        }
        else if (Spec is not null && rangeChanged)
        {
            _ = AskAsync();
        }
        else if (Spec is null)
        {
            (_read, _askedSpec, _askedSources, _refusal) = (null, null, null, null);
        }
        Rebuild();
    }

    // ---- Read through Chart Sources (ADR-0322, ADR-0344) ----

    // The newest ask, and what it asked; the reading drawn, the version each Data Handle was read at,
    // and what is said in the chart's place when no reading can be drawn.
    private int _asked;
    private bool _asking;
    private string? _askedSpec;
    private Func<string, IChartSource?>? _askedSources;
    private ChartRange? _askedRange;
    private ChartSpecReading? _read;
    private IReadOnlyDictionary<string, long> _versions = new Dictionary<string, long>();
    private string? _refusal;

    // Shown until the first answer: no series, so nothing is drawn as if it were the data.
    private static readonly ChartDefinition Unread = new(ChartAxis.Numbers(), []);

    /// <summary>The definition drawn: the Consumer's, or the one read from the spec's sources.</summary>
    private ChartDefinition Drawn => Definition ?? _read?.Definition ?? Unread;

    // The device pixels a plot of the chart's width could have, at the two device pixels a CSS pixel
    // ExChart counts in (ADR-0254): the source thins to them, and the plot thins on top if it is narrower.
    private int SourcePixels => Math.Max(1, (int)Math.Ceiling(Width * 2));

    private async Task AskAsync()
    {
        var asked = ++_asked;
        var (spec, sources, range) = (Spec!, Sources!, _range);
        (_askedSpec, _askedSources, _askedRange) = (spec, sources, range);
        _asking = true;
        ChartSourceReading? read = null;
        string? refusal = null;
        try
        {
            // Resumed where the ask was made: the reading is the renderer's.
            read = await ChartSpec.ReadAsync(spec, sources, SourcePixels, range?.From, range?.To);
        }
        catch (ChartSpecRefusedException e)
        {
            refusal = e.Message;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            refusal = $"The source did not answer the chart: {e.Message}";
        }
        // Another ask replaced this one, or the chart is gone: dropped (spine rule 6).
        if (asked != _asked || _disposed)
            return;
        _asking = false;
        if (read?.Versions.FirstOrDefault(v => _versions.TryGetValue(v.Key, out var shown) && v.Value < shown) is { Key: not null } older)
        {
            // Never drawn over the newer data shown, nor left showing what was asked before as if it
            // were the answer: said in the chart's place (spine rule 1).
            _refusal = $"The source answered the chart from version {older.Value} of '{older.Key}', older than version {_versions[older.Key]} the chart was drawn from, so it is not drawn.";
        }
        else if (read is not null)
        {
            _read = read.Reading;
            _versions = read.Versions;
            _refusal = null;
        }
        else
        {
            _refusal = refusal;
        }
        Rebuild();
        StateHasChanged();
    }

    private void Rebuild()
    {
        // Read through a source, the scene drawn stays until the newest ask is answered (ADR-0344): the
        // points of another range, drawn at this one, are not what this range holds.
        if (_asking && _read is not null) return;
        var builtFrom = new object?[] { Drawn, Width, Height, Density, Legend, AxisStyle, IndependentScales, ColourSlots, _hidden, _range, _scale, YScale };
        if (builtFrom.SequenceEqual(_builtFrom)) return;
        var definitionChanged = _builtFrom.Length == 0 || !ReferenceEquals(_builtFrom[0], Drawn);
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
        _scene = ChartScene.Build(Drawn, _metrics, _view, AxisStyle, IndependentScales, Legend);
        _summary = ChartSummary.Of(Drawn, _scene, _view, Title, Description);
        _table = _tableOpen && !ThinnedAtSource ? ChartCsv.WideTable(Drawn, _metrics, _view) : null;

        if (definitionChanged || previous is null || !previous.Positions.Equals(_scene.Positions) || !previous.Marks.Equals(_scene.Marks) || !previous.EdgeMarks.Equals(_scene.EdgeMarks)
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
        _reading = ChartReadings.At(Drawn, _scene, _metrics, _view, _scene.Positions[index].Position, panel, y);
        _dots = _reading.Lines.Where(l => l.Y is not null).Select(l => (l.Y!.Value, l.Look)).ToEquatableList();
    }

    // On a scatter the pointer is over the nearest mark, if any (ADR-0290), or else the nearest edge,
    // counted after the marks (ADR-0321).
    private void ReadMark(int index)
    {
        if (index < 0 || index >= _scene.Marks.Count + _scene.EdgeMarks.Count)
        {
            ClearPointer();
            return;
        }
        var (_, y, panel) = MarkAt(index);
        _pointerIndex = index;
        _pointerPanel = panel;
        _readingY = y;
        _reading = index < _scene.Marks.Count
            ? ChartReadings.OfMark(Drawn, _scene, index)
            : ChartReadings.OfEdge(Drawn, _scene, index - _scene.Marks.Count);
        _dots = _reading.Lines.Select(l => (y, l.Look)).ToEquatableList();
    }

    // Where the pointer's mark stands: a marker's centre, or an edge's middle.
    private (double X, double Y, int Panel) MarkAt(int index)
    {
        if (index < _scene.Marks.Count)
        {
            var m = _scene.Marks[index];
            return (m.X, m.Y, m.Panel);
        }
        var e = _scene.EdgeMarks[index - _scene.Marks.Count];
        return ((e.Edge.X1 + e.Edge.X2) / 2, (e.Edge.Y1 + e.Edge.Y2) / 2, e.Panel);
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
                var point = Drawn.Series.First(s => s.Id == m.SeriesId).Points[m.Point];
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
            {
                var position = _scene.Positions[index].Position;
                // A merged candle stands for no one row: a click narrows to its period (ADR-0300, ADR-0302).
                if (_scene.Panels.SelectMany(panel => panel.Series).FirstOrDefault(piece => piece.Id == line.Id && piece.Period != CandlePeriod.Row) is { } merged
                    && merged.Candles.FirstOrDefault(c => c.From <= position && position <= c.To) is { Rows: > 0 } candle)
                    await SetRangeAsync(new ChartRange(candle.From, candle.To));
                else
                    await OnPointClick.InvokeAsync(new ChartPointClick(line.Id, position));
            }
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
        // Chart Filters shows the range as it now stands, however it was set (ADR-0273).
        _fromText = range?.From is { } f ? Invariant(f) : null;
        _toText = range?.To is { } t ? Invariant(t) : null;
        if (Spec is not null)
            _ = AskAsync();
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
        _table = _tableOpen && !ThinnedAtSource ? ChartCsv.WideTable(Drawn, _metrics, _view) : null;
    }

    // The range typed under Chart Filters (ADR-0273), in the axis' own values. A value that does
    // not read as one is said, never guessed (spine rule 1).
    private Task ApplyTypedRangeAsync() => ApplyRangeAsync(_fromText, _toText);

    private async Task ApplyRangeAsync(string? fromText, string? toText)
    {
        _fromText = fromText;
        _toText = toText;
        if (!TryRead(fromText, out var from) || !TryRead(toText, out var to))
        {
            var bad = !TryRead(fromText, out _) ? fromText : toText;
            _filterError = $"“{bad}” is not {AxisKind} on this axis.";
            StateHasChanged();
            return;
        }
        if (from > to)
        {
            _filterError = "From is after To.";
            StateHasChanged();
            return;
        }
        await SetRangeAsync(from is null && to is null ? null : new ChartRange(from, to));
    }

    private async Task SetScaleAsync(string value)
    {
        if (!Enum.TryParse<AxisScale>(value, out var scale) || scale == (_scale ?? Drawn.Axis.EffectiveScale)) return;
        _scale = scale;
        Rebuild();
        StateHasChanged();
        await OnScaleChanged.InvokeAsync(scale);
    }

    private string ScaleText => (_scale ?? Drawn.Axis.EffectiveScale).ToString();

    private bool ScaleSwitchable => Drawn.Axis.Values != AxisValues.Labels && Drawn.Series.All(s => s.Sampler is null);

    private string AxisKind => Drawn.Axis.Values switch
    {
        AxisValues.Dates => "a date (yyyy-MM-dd)",
        AxisValues.Labels => "a category",
        _ => "a number",
    };

    private string RangeHint => Drawn.Axis.Values switch
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
        var axis = Drawn.Axis;
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

    private string Invariant(double position) => Drawn.Axis.Values switch
    {
        AxisValues.Dates => DateOnly.FromDayNumber((int)position).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        AxisValues.Labels when position >= 0 && position < Drawn.Axis.Labels.Count => Drawn.Axis.Labels[(int)position],
        _ => position.ToString("R", CultureInfo.InvariantCulture),
    };

    // The parts are internal components, which markup cannot name, so they are opened here.
    private RenderFragment Drawing => b =>
    {
        var lh = _metrics.LineHeight;
        b.AddContent(0, ChartDrawing.Panels(_scene, lh, AxisStyle?.Angle ?? 0, _toggle));
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

    private double? GuideX => _pointerIndex is { } i ? (_scene.Scatter ? MarkAt(i).X : _scene.Positions[i].X) : null;

    private double GuideTop => _scene.Scatter && _pointerIndex is { } i ? MarkAt(i).Y : PlotsTop;

    private double GuideBottom => _scene.Scatter && _pointerIndex is { } i ? MarkAt(i).Y : PlotsBottom;

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

    // ---- Files (ADR-0252, ADR-0282): written on the click, never during a render, and saved by the
    // files entry, the fifteenth use the JavaScript allowlist permits.

    /// <summary>
    /// Saves the chart as a file, as the user sees it: CSV of the visible series in the range shown,
    /// the SVG of the drawing, or that SVG painted to PNG at the screen's Device Pixel (ADR-0282).
    /// One call is one save. False when the chart's script is not loaded, which the chart says on
    /// its face, or the circuit has gone, and for a CSV of a chart its Chart Source thinned, which
    /// would hold only the points drawn (ADR-0344).
    /// </summary>
    public async Task<bool> DownloadAsync(ChartFileFormat format)
    {
        // Taken now: the file is the drawing on screen at the click, whatever renders meanwhile.
        var module = _module;
        var scene = _scene;
        var definition = Drawn;
        var metrics = _metrics;
        var view = _view;
        var summary = _summary;
        if (module is null || _scriptRefused || _disposed || (format == ChartFileFormat.Csv && ThinnedAtSource)) return false;
        var name = ChartFiles.Name(FileName, Title, format);
        try
        {
            byte[] bytes;
            if (format == ChartFileFormat.Csv)
            {
                bytes = ChartFiles.Csv(definition, metrics, view);
            }
            else
            {
                var font = GridMetrics.Resolve(Density).FontSizePx;
                var angle = AxisStyle?.Angle ?? 0;
                // The theme as the screen shows it, read on the click (ADR-0282).
                var theme = ChartFiles.Theme(await module.InvokeAsync<string[]>("tokens", _root, ChartFiles.TokenNames.Value));
                var svg = await ChartFiles.SvgAsync(Services, new ChartFileDrawing.Input(scene, Width, Height, font, metrics.LineHeight, angle, summary, theme));
                if (format == ChartFileFormat.Png)
                {
                    // Painted and saved in the browser: the PNG's bytes never cross back (ADR-0282).
                    await module.InvokeVoidAsync("savePng", name, svg, Width, Height);
                    return true;
                }
                bytes = System.Text.Encoding.UTF8.GetBytes(svg);
            }
            await module.InvokeVoidAsync("save", name, ChartFiles.MediaType(format), bytes);
            return true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            return false;
        }
    }

    private Task DownloadCsvAsync() => DownloadAsync(ChartFileFormat.Csv);

    private Task DownloadSvgAsync() => DownloadAsync(ChartFileFormat.Svg);

    private Task DownloadPngAsync() => DownloadAsync(ChartFileFormat.Png);

    // A file needs the script: until it is told the first drawing, and when it is refused, the
    // buttons say so by being unavailable rather than doing nothing (spine rule 1).
    private bool FilesUnavailable => !_told || _scriptRefused;

    // A chart whose Chart Source thinned it holds only the points drawn (ADR-0344): a CSV or a table of
    // them would look like the data and not be it (spine rule 1), so neither is offered.
    private bool ThinnedAtSource => Spec is not null || Drawn.Series.Any(s => s.Thinned.Count > 0);

    private bool CsvUnavailable => FilesUnavailable || ThinnedAtSource;

    private string? CsvTitle => ThinnedAtSource ? ThinnedAtSourceWords : null;

    private const string ThinnedAtSourceWords =
        "The rows are where the data lives: this chart holds only the points drawn, each pixel's first, last, lowest and highest, so it saves no CSV and shows no table.";

    // ---- The pointer entry (ADR-0255, thirteenth): imported at its versioned address, refused when
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
        await FocusMenuAsync();
        if (_handle is not null && _drawPending && !_disposed)
        {
            _drawPending = false;
            var version = _version;
            var xs = _scene.Positions.Select(p => Math.Round(p.X, 2)).ToArray();
            // A scatter's marks, as x, y and panel in turn, with how far from one the pointer still
            // finds it and the lattice a drag is reported on (ADR-0290).
            var marks = _scene.Scatter ? _scene.Marks.SelectMany(m => new[] { Math.Round(m.X, 2), Math.Round(m.Y, 2), m.Panel }).ToArray() : null;
            // Its edges, as the two ends and the panel in turn, found where no mark is in reach (ADR-0321).
            var edges = _scene.Scatter && _scene.EdgeMarks.Count > 0
                ? _scene.EdgeMarks.SelectMany(e => new[] { Math.Round(e.Edge.X1, 2), Math.Round(e.Edge.Y1, 2), Math.Round(e.Edge.X2, 2), Math.Round(e.Edge.Y2, 2), e.Panel }).ToArray()
                : null;
            try
            {
                await _handle.InvokeVoidAsync("draw", version, xs, marks, Math.Round(_metrics.LineHeight * 0.6, 2), DragCell, edges);
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

    private bool Busy => (!_told && !_scriptRefused) || _asking;

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
