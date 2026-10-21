using System.Globalization;
using ExChart;
using ExChart.Chrome;
using ExChart.Engine;
using ExGrid;
using ExGrid.Columns;
using ExGrid.Chrome;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExBoard.Components;

/// <summary>
/// Draws a Board (ADR-0340): its views laid out in its rows, each input bound to the Snapshot
/// <see cref="Data"/> holds under its name, or to the source held where the data lives
/// <see cref="Sources"/> holds (ADR-0343), and checked against its declaration first (ADR-0341). A
/// Board that does not bind draws nothing but the refusal, naming the input or the path. Running a
/// Board never rewrites it.
/// </summary>
public partial class ExBoard : IDisposable
{
    /// <summary>The gap between views and between rows, in CSS pixels.</summary>
    public const double Gap = 12;

    /// <summary>The height a view's title takes, in CSS pixels, emitted on the title itself so the
    /// view under it is drawn at exactly what is left.</summary>
    public const double TitleHeight = 28;

    private BoardReading? _boundBoard;
    private IReadOnlyDictionary<string, Snapshot>? _boundData;
    private IReadOnlyDictionary<string, BoardSource>? _boundSources;
    private BoardDiff? _pendingDiff;
    private int _binding;
    private CultureInfo? _boundCulture;
    private BoundBoard? _bound;
    private string? _refusal;
    private BoardDiff? _boundDiff;
    private BoardDiff? _raised;
    private Action? _sourceChanged;
    private readonly Dictionary<string, GridChanges> _gridChanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChartChanges> _chartChanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventCallback<PivotLayout>> _layoutChanged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventCallback<GridSelection>> _selectionChanged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _confirmStatus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _linkStatus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, global::ExPivot.Components.ExPivot> _pivots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, global::ExGrid.Components.ExGrid<global::ExGrid.ShapedRow>> _grids = new(StringComparer.Ordinal);

    /// <summary>The Board to draw, as <see cref="BoardSpec.Read(string)"/> read it.</summary>
    [Parameter, EditorRequired]
    public BoardReading Board { get; set; } = null!;

    /// <summary>The Snapshot each input is bound to, by the input's name. A new dictionary binds
    /// again; the same one is not read twice.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<string, Snapshot> Data { get; set; } = null!;

    /// <summary>
    /// The source each input held where the data lives is bound to, by the input's name (ADR-0343): its
    /// shape is asked and checked before anything is drawn, and its views ask it for what they draw. A new
    /// dictionary binds again; the same one is not read twice.
    /// </summary>
    [Parameter]
    public IReadOnlyDictionary<string, BoardSource>? Sources { get; set; }

    /// <summary>The Board's width in CSS pixels, from which a chart's width is its share of its row
    /// (a chart is drawn at a width it is told). Grids and pivots stretch to their share.</summary>
    [Parameter]
    public double Width { get; set; } = 1200;

    /// <summary>The culture a grid's text is written in; the invariant culture by default.</summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>The grid Chrome the grids and pivots are drawn with; the built-in one by default.</summary>
    [Parameter]
    public IGridChrome? Chrome { get; set; }

    /// <summary>The pivot Chrome; the built-in one by default.</summary>
    [Parameter]
    public IPivotChrome? PivotChrome { get; set; }

    /// <summary>Raised when the user confirms an output the Board declares (ADR-0340, Q6), and only
    /// then: a selection on the way raises nothing.</summary>
    [Parameter]
    public EventCallback<BoardOutput> OnOutput { get; set; }

    /// <summary>
    /// What the user changed last time, laid over the Board as it binds (ADR-0340, Q7), as
    /// <see cref="BoardDiff.Read"/> reads it back. What no longer fits the Board is dropped and said above
    /// its views, and the rest applies; the Diff that did is then raised (<see cref="OnDiff"/>), so a host
    /// keeping each one raised keeps no part that will not apply. It is read when it changes: a new one
    /// binds the Board again, unless it is the one already shown, as when the host hands back each Diff
    /// raised, and the same instance is not read twice. A Board bound again for new data keeps what the
    /// user has changed since.
    /// </summary>
    [Parameter]
    public BoardDiff? Diff { get; set; }

    /// <summary>
    /// Raised when the user changes a view — a sort, a filter, a column's width, order, pinning or
    /// hiding, a pivot's layout, a chart's series, range or axis — with the whole Board Diff: every
    /// view's part that differs from the Board. The Board is never rewritten; the host keeps this
    /// (<see cref="BoardDiff.ToJson"/>) and hands it back as <see cref="Diff"/> next time.
    /// </summary>
    [Parameter]
    public EventCallback<BoardDiff> OnDiff { get; set; }

    /// <summary>The chart Chrome; the built-in one by default.</summary>
    [Parameter]
    public IChartChrome? ChartChrome { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Board);
        ArgumentNullException.ThrowIfNull(Data);
        if (ReferenceEquals(Board, _boundBoard) && ReferenceEquals(Data, _boundData) && ReferenceEquals(Sources, _boundSources) && Equals(Culture, _boundCulture)
            && (ReferenceEquals(Diff, _boundDiff) || (_bound is not null && (Diff ?? BoardDiff.Empty).SameAs(_bound.Diff))))
            return;
        // Bound again for a new Board or new data, the user's changes since are kept: only a new Diff
        // replaces them.
        var laid = ReferenceEquals(Diff, _boundDiff) && _bound is not null ? _bound.Diff : Diff;
        _boundBoard = Board;
        _boundData = Data;
        _boundSources = Sources;
        _boundCulture = Culture;
        _boundDiff = Diff;
        _binding++;
        Unsubscribe();
        _layoutChanged.Clear();
        _gridChanges.Clear();
        _chartChanges.Clear();
        _selectionChanged.Clear();
        _confirmStatus.Clear();
        _linkStatus.Clear();
        _grids.Clear();
        _pivots.Clear();
        _bound = null;
        _pendingDiff = null;
        if (Sources is { Count: > 0 })
        {
            // The sources are asked their shapes first, a round trip away (OnParametersSetAsync).
            _pendingDiff = laid;
            _refusal = null;
            return;
        }
        try
        {
            Adopt(BoundBoard.Bind(Board, Data, Culture, laid));
        }
        catch (BoardRefusedException refused)
        {
            Refuse(refused);
        }
    }

    private void Refuse(BoardRefusedException refused)
    {
        _bound = null;
        _refusal = refused.Path.Length == 0 ? refused.Message : $"{refused.Path}: {refused.Message}";
    }

    // Wires up a bound Board: its callbacks cached, its grids' sources heard.
    private void Adopt(BoundBoard bound)
    {
        _bound = bound;
        _refusal = null;
        // What is laid over is not the user's change this time: only a change from it is raised, and the
        // Diff that applied, when some of it did not (OnParametersSetAsync).
        _raised = _bound.Dropped.Count > 0 ? null : _bound.Diff;
        // Each cached, so a render does not hand a view a new delegate (AGENTS.md).
        foreach (var view in _bound.Views)
        {
            var id = view.View.Id;
            switch (view)
            {
                case BoundGridView grid:
                    _gridChanges[id] = new GridChanges(
                        EventCallback.Factory.Create<ColumnWidthChange>(this, change => Changed(() => grid.Resize(change))),
                        EventCallback.Factory.Create<IReadOnlyList<string>>(this, order => Changed(() => grid.Reorder(order))),
                        EventCallback.Factory.Create<string>(this, column => Changed(() => grid.Hide(column))),
                        EventCallback.Factory.Create<int>(this, count => Changed(() => grid.Pin(count))));
                    break;
                case BoundPivotView pivot:
                    // A new layout is a new report, so what its cells narrowed is let go now, as a narrowing
                    // of it lets go (ADR-0011), not when its grid next raises the dropped selection.
                    _layoutChanged[id] = EventCallback.Factory.Create<PivotLayout>(this, layout => Changed(() =>
                    {
                        pivot.ChangeLayout(layout);
                        // An empty selection asks no source, so it is done here and now.
                        _ = Select(id, GridSelection.Empty);
                    }));
                    break;
                case BoundChartView chart:
                    _chartChanges[id] = new ChartChanges(
                        EventCallback.Factory.Create<SeriesVisibility>(this, change => Changed(() => chart.ChangeVisibility(change))),
                        EventCallback.Factory.Create<ChartRange?>(this, range => Changed(() => chart.ChangeRange(range))),
                        EventCallback.Factory.Create<AxisScale>(this, scale => Changed(() => chart.ChangeScale(scale))));
                    break;
            }
        }
        // A grid's sort and filter are its source's (ADR-0001), which says when they move; a narrowing
        // moves only its rows, and raises no Diff.
        _sourceChanged = () => _ = InvokeAsync(async () =>
        {
            try
            {
                await Changed(() => { });
            }
            catch (Exception e)
            {
                // Heard outside a Blazor event, so handed to the renderer, never dropped.
                await DispatchExceptionAsync(e);
            }
        });
        foreach (var grid in _bound.Views.OfType<BoundGridView>())
            grid.Source.StateChanged += _sourceChanged;
        // A chart over a server input is drawn when its sources answer (ADR-0344).
        foreach (var chart in _bound.Views.OfType<BoundChartView>())
            chart.Answered += _sourceChanged;
        foreach (var from in _bound.Board.Links.Select(l => l.From).Concat(_bound.Board.Outputs.Select(o => o.From)).Distinct())
            _selectionChanged[from] = EventCallback.Factory.Create<GridSelection>(this, selection => Select(from, selection));
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_bound is null && _refusal is null && _boundSources is { Count: > 0 } sources)
        {
            // A binding carries the parameters it was asked for: one answered after they changed is
            // dropped, never drawn over the new ones (spine rule 6).
            var binding = _binding;
            try
            {
                var bound = await BoundBoard.BindAsync(_boundBoard!, _boundData!, sources, _boundCulture, _pendingDiff);
                if (binding != _binding)
                {
                    Dispose(bound);
                    return;
                }
                Adopt(bound);
            }
            catch (BoardRefusedException refused) when (binding == _binding)
            {
                Refuse(refused);
                return;
            }
        }
        await Changed(() => { });
    }

    private EventCallback<GridSelection> SelectionChangedOf(string view) => _selectionChanged.GetValueOrDefault(view);

    // A grid's selection narrows the views its links reach; a dropped selection, which the grid raises
    // as an empty one, takes the narrowing away with it (ADR-0011).
    private async Task Select(string from, GridSelection selection)
    {
        if (_bound is not { } bound)
            return;
        if (bound[from] is BoundPivotView)
        {
            // A pivot's cells narrow by the Items they stand for, read as the selection is raised, from
            // the report its grid holds then. One the pivot cannot read narrows nothing, and says so.
            var cells = selection.Ranges.Count == 0 ? [] : _pivots.GetValueOrDefault(from)?.ItemsOf(selection);
            if (cells is null)
                _linkStatus[from] = "The selection cannot be read from the report as drawn, so it narrows nothing.";
            else
                _linkStatus.Remove(from);
            try
            {
                // Over a server, the source is asked for the values under the cells (ADR-0345).
                await bound.SelectCellsAsync(from, cells);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (ReferenceEquals(bound, _bound))
                    _linkStatus[from] = $"The source did not answer for the cells selected, so they narrow nothing: {e.Message}";
            }
            if (!ReferenceEquals(bound, _bound))
                return;
        }
        else
        {
            bound.Select(from, selection);
            foreach (var output in bound.Board.Outputs.Where(o => o.From == from))
                _confirmStatus.Remove(output.Name);
            // Over a source held on a server, a selection reaching rows not fetched is said, not read.
            if (((BoundGridView)bound[from]).Unread is { } unread)
                _linkStatus[from] = unread;
            else
                _linkStatus.Remove(from);
        }
        // A pivot narrowed again has let go of its selection, so what was said of it goes too.
        foreach (var reached in Reached(bound.Board, from))
            _linkStatus.Remove(reached);
    }

    // The views a selection in <paramref name="from"/> narrows, along links, itself not included.
    private static HashSet<string> Reached(BoardReading board, string from)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var next = new Queue<string>([from]);
        while (next.TryDequeue(out var view))
        {
            foreach (var target in board.Links.Where(l => l.From == view).SelectMany(l => l.To))
            {
                if (reached.Add(target))
                    next.Enqueue(target);
            }
        }
        return reached;
    }

    private async Task ConfirmAsync(string output)
    {
        if (_bound is not { } bound)
            return;
        // The grid raises its selection after the render that paints it, a round trip later on the
        // Server host, so a press that follows a click at machine speed would find the selection before
        // it. It is read from the grid once the grid has answered every input it received before the
        // press: the selection the press was made against (spine rule 6).
        var from = bound.Board.Outputs.First(o => o.Name == output).From;
        if (_grids.GetValueOrDefault(from) is { } grid)
        {
            var selection = await grid.SelectionAfterInputsAsync();
            if (!ReferenceEquals(bound, _bound))
                return;
            await Select(from, selection);
        }
        if (bound.Confirm(output) is not { } raised)
        {
            _confirmStatus[output] = ((BoundGridView)bound[from]).Unread ?? "Select the rows to hand on first.";
            return;
        }
        _confirmStatus.Remove(output);
        await OnOutput.InvokeAsync(raised);
    }

    // Applies the user's change, and raises the Diff when it now differs from the one last raised.
    private Task Changed(Action change)
    {
        if (_bound is not { } bound)
            return Task.CompletedTask;
        change();
        var diff = bound.Diff;
        if (diff.SameAs(_raised))
            return Task.CompletedTask;
        _raised = diff;
        return OnDiff.InvokeAsync(diff);
    }

    private void Unsubscribe()
    {
        if (_bound is not null && _sourceChanged is not null)
        {
            foreach (var grid in _bound.Views.OfType<BoundGridView>())
                grid.Source.StateChanged -= _sourceChanged;
            foreach (var chart in _bound.Views.OfType<BoundChartView>())
                chart.Answered -= _sourceChanged;
        }
        _sourceChanged = null;
        if (_bound is not null)
            Dispose(_bound);
    }

    // A fetched grid's source holds its questions to the server; it is let go with the Board bound.
    private static void Dispose(BoundBoard bound)
    {
        foreach (var grid in bound.Views.OfType<BoundGridView>())
            grid.Fetched?.Source.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
        GC.SuppressFinalize(this);
    }

    private sealed record GridChanges(EventCallback<ColumnWidthChange> Width, EventCallback<IReadOnlyList<string>> Order,
        EventCallback<string> Hidden, EventCallback<int> Pinned);

    private sealed record ChartChanges(EventCallback<SeriesVisibility> Visibility, EventCallback<ChartRange?> Range, EventCallback<AxisScale> Scale);

    private static string RefusalStyle(BoardRow row) => FormattableString.Invariant($"height: {row.Height}px");

    private static string RootStyle => FormattableString.Invariant($"display: flex; flex-direction: column; gap: {Gap}px");

    // A row's height is each view's own: a grid's or a pivot's viewport, a chart's stage. A chart's
    // toolbar sits above its stage at the height its stylesheet gives it, so the row grows to hold
    // it rather than pair a pixel here with one there; nothing is clipped.
    private static string RowStyle(BoardRow row) =>
        FormattableString.Invariant($"display: flex; gap: {Gap}px; align-items: flex-start");

    private static string CellStyle(BoardCell cell) =>
        FormattableString.Invariant($"flex: {cell.Width} 1 0; min-width: 0; display: flex; flex-direction: column");

    private static string ConfirmStyle => FormattableString.Invariant($"margin-top: {Gap / 2}px; display: flex; gap: {Gap / 2}px; align-items: center");

    private static string TitleStyle => FormattableString.Invariant(
        $"margin: 0; height: {TitleHeight}px; line-height: {TitleHeight}px; font-size: inherit; overflow: hidden; white-space: nowrap; text-overflow: ellipsis");

    private double ChartWidth(BoardRow row, BoardCell cell, int shares) =>
        Math.Max(120, Math.Floor((Width - (Gap * (row.Cells.Count - 1))) * cell.Width / shares));

}
