using System.Globalization;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// A Board with every input bound to its data (ADR-0341): each input's shape checked against its
/// declaration, and each view made what it draws. Built by <see cref="Bind"/>; drawn by the
/// <c>ExBoard</c> component.
/// </summary>
public sealed class BoundBoard
{
    private readonly Dictionary<BoardLink, LinkCondition> _conditions = [];
    private readonly Dictionary<string, LinkCondition> _taken = new(StringComparer.Ordinal);

    private BoundBoard(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, IReadOnlyList<BoundView> views)
    {
        Board = board;
        Data = data;
        Views = views;
    }

    /// <summary>The Board.</summary>
    public BoardReading Board { get; }

    /// <summary>The Snapshot each input is bound to.</summary>
    public IReadOnlyDictionary<string, Snapshot> Data { get; }

    /// <summary>The views, in the Board's order.</summary>
    public IReadOnlyList<BoundView> Views { get; }

    /// <summary>The view whose id is <paramref name="id"/>.</summary>
    public BoundView this[string id] => Views.First(v => v.View.Id == id);

    /// <summary>The condition <paramref name="link"/> narrows by now, or null while its view selects nothing.</summary>
    public LinkCondition? ConditionOf(BoardLink link) => _conditions.GetValueOrDefault(link);

    /// <summary>
    /// The condition <paramref name="selection"/>, made in <paramref name="link"/>'s grid, narrows by:
    /// the values in the link's columns of every row the selection covers, in any column, read from
    /// the rows the grid shows; null when it selects nothing, which narrows nothing (ADR-0011).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="link"/> is not one of the Board's.</exception>
    public LinkCondition? ConditionOf(BoardLink link, GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return Condition(GridOf(link).Grid, link.On, selection);
    }

    private static LinkCondition? Condition(ShapedGrid grid, IReadOnlyList<string> on, GridSelection selection)
    {
        if (selection.Ranges.Count == 0)
            return null;
        var shown = grid.Source.Window;
        var snapshot = grid.Snapshot;
        var columns = on.Select(n => snapshot[n]).ToArray();
        var taken = new bool[shown.Count];
        var tuples = new List<object?[]>();
        foreach (var range in selection.Ranges)
        {
            // A selection taken against other rows than these is dropped, never read against them.
            if (range.BottomRow >= shown.Count)
                return null;
            for (var r = range.TopRow; r <= range.BottomRow; r++)
            {
                if (taken[r])
                    continue;
                taken[r] = true;
                tuples.Add([.. columns.Select(c => snapshot.ValueAt(shown[r].At, c))]);
            }
        }
        return new LinkCondition(on, tuples);
    }

    /// <summary>
    /// Narrows every view <paramref name="link"/> reaches by <paramref name="condition"/>, or, given
    /// null, takes the link's narrowing away (ADR-0340, Q5). A view narrowed by several links shows
    /// the rows all of them let through. A grid keeps its rows' sort and filter, and the user's; a
    /// pivot is given a source over the rows let through, and a chart its data read again from them; a
    /// chart its spec cannot draw from them says why in <see cref="BoundChartView.Refusal"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="link"/> is not one of the Board's, or
    /// <paramref name="condition"/> compares other columns than it does.</exception>
    public void Narrow(BoardLink link, LinkCondition? condition)
    {
        if (Set(link, condition))
        {
            foreach (var target in link.To)
                NarrowView(target);
        }
    }

    /// <summary>
    /// Narrows by <paramref name="selection"/>, made in grid view <paramref name="from"/>: each link
    /// from it narrows by the condition the selection makes (<see cref="ConditionOf(BoardLink, GridSelection)"/>),
    /// and each view they reach is narrowed once. A selection that moves within the same values, an
    /// arrow key inside the selected rows, narrows nothing again.
    /// </summary>
    /// <returns>Whether any view was narrowed again.</returns>
    public bool Select(string from, GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(selection);
        if (Board.View(from) is BoardGridView)
        {
            var grid = ((BoundGridView)this[from]).Grid;
            foreach (var output in Board.Outputs.Where(o => o.From == from))
            {
                var condition = Condition(grid, output.On, selection);
                if (condition is null)
                    _taken.Remove(output.Name);
                else
                    _taken[output.Name] = condition;
            }
        }
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in Board.Links.Where(l => l.From == from))
        {
            if (Set(link, ConditionOf(link, selection)))
                targets.UnionWith(link.To);
        }
        foreach (var target in targets)
            NarrowView(target);
        return targets.Count > 0;
    }

    /// <summary>Whether output <paramref name="name"/> can be confirmed: its grid selects something.</summary>
    public bool CanConfirm(string name) => _taken.ContainsKey(name);

    /// <summary>
    /// What confirming output <paramref name="name"/> hands on (ADR-0340, Q6): the values its grid's
    /// selection holds in its columns, on its input, with the version of the input's data they were
    /// taken against; null while its grid selects nothing. Nothing is raised by a selection on the
    /// way: only this, called when the user confirms.
    /// </summary>
    /// <exception cref="ArgumentException">The Board declares no output <paramref name="name"/>.</exception>
    public BoardOutput? Confirm(string name)
    {
        var declared = Board.Outputs.FirstOrDefault(o => o.Name == name)
            ?? throw new ArgumentException($"The Board declares no output '{name}'.", nameof(name));
        if (!_taken.TryGetValue(name, out var condition))
            return null;
        var input = ((BoardGridView)Board.View(declared.From)!).Grid.Data;
        var shape = Board.Input(input)!.Shape;
        return new BoardOutput(name, input, Data[input], condition, [.. declared.On.Select(c => shape.Columns.First(k => k.Name == c).Kind)]);
    }

    // Records the link's condition; false when it is the one already held, which narrows nothing again.
    private bool Set(BoardLink link, LinkCondition? condition)
    {
        GridOf(link);
        if (condition is not null && !condition.On.SequenceEqual(link.On, StringComparer.Ordinal))
            throw new ArgumentException($"The link compares {string.Join(", ", link.On)}, and the condition {string.Join(", ", condition.On)}.", nameof(condition));
        var held = _conditions.GetValueOrDefault(link);
        if (held is null ? condition is null : held.LetsThroughAs(condition))
            return false;
        if (condition is null)
            _conditions.Remove(link);
        else
            _conditions[link] = condition;
        return true;
    }

    private BoundGridView GridOf(BoardLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!Board.Links.Contains(link))
            throw new ArgumentException("The link is not one of the Board's.", nameof(link));
        return (BoundGridView)this[link.From];
    }

    private void NarrowView(string id)
    {
        var conditions = _conditions.Where(c => c.Key.To.Contains(id)).Select(c => c.Value).ToArray();
        switch (this[id])
        {
            case BoundGridView grid:
                var snapshot = grid.Grid.Snapshot;
                var rows = grid.Grid.Rows;
                grid.Grid.Source.ReplaceAll(conditions.Length == 0 ? rows : [.. Narrowing.Positions(snapshot, conditions).Select(p => rows[p])]);
                // Its rows changed, so its selection is dropped (ADR-0011): what the selection held is
                // let go now, not when the grid next raises it, so nothing is confirmed or narrowed by
                // a selection the grid no longer shows.
                foreach (var output in Board.Outputs.Where(o => o.From == id))
                    _taken.Remove(output.Name);
                foreach (var link in Board.Links.Where(l => l.From == id))
                    Narrow(link, null);
                break;
            case BoundPivotView pivot:
                var data = Data[pivot.Input];
                pivot.Source = conditions.Length == 0 ? pivot.Unnarrowed : PivotSource.From(Narrowing.Copy(data, Narrowing.Positions(data, conditions)));
                break;
            case BoundChartView chart:
                var path = $"views[{Board.Views.ToList().FindIndex(v => v.Id == id)}].chart";
                try
                {
                    chart.Reading = ReadChart((BoardChartView)chart.View, Data, Board.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal), path,
                        conditions.Length == 0 ? null : name => Narrowing.Positions(Data[name], conditions));
                    chart.Refusal = null;
                }
                catch (BoardRefusedException refused)
                {
                    chart.Refusal = $"{refused.Path}: {refused.Message}";
                }
                break;
        }
    }

    /// <summary>
    /// Binds each of <paramref name="board"/>'s inputs to the Snapshot <paramref name="data"/> holds
    /// under its name, and makes each view. Nothing is drawn unless every input binds and every view
    /// can be made: an input with no data, a Snapshot whose shape is not the declaration, and a chart
    /// its spec cannot draw from the data are refused, naming the input or the path.
    /// </summary>
    /// <param name="board">The Board.</param>
    /// <param name="data">The Snapshot for each input, by the input's name. Data for a name the Board
    /// does not declare is not read.</param>
    /// <param name="culture">The culture a grid's text is written in; the invariant culture by default.</param>
    /// <exception cref="BoardRefusedException">An input or a view is refused.</exception>
    public static BoundBoard Bind(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(data);
        var bound = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        foreach (var input in board.Inputs)
        {
            if (!data.TryGetValue(input.Name, out var snapshot) || snapshot is null)
                throw new BoardRefusedException($"inputs.{input.Name}", $"Input '{input.Name}' is bound to no data.");
            bound[input.Name] = input.Bind(snapshot);
        }
        var inputs = board.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal);
        var views = new List<BoundView>();
        for (var i = 0; i < board.Views.Count; i++)
        {
            views.Add(board.Views[i] switch
            {
                BoardGridView grid => new BoundGridView(grid, GridOf(bound[grid.Grid.Data], grid.Grid, culture)),
                BoardPivotView pivot => new BoundPivotView(pivot, PivotSource.From(bound[pivot.Input])),
                BoardChartView chart => new BoundChartView(chart, ReadChart(chart, bound, inputs, $"views[{i}].chart", null)),
                var other => throw new InvalidOperationException($"Unknown view {other.GetType().Name}."),
            });
        }
        return new BoundBoard(board, bound, views);
    }

    private static ShapedGrid GridOf(Snapshot snapshot, GridViewSpecReading spec, CultureInfo? culture)
    {
        var grid = ShapedGrid.Of(snapshot, spec, culture);
        // Its rows change only when a link narrows them, a step the user took, which reaches the grid
        // at once rather than at the end of a live feed's gathering interval.
        grid.Source.GatherInterval = TimeSpan.Zero;
        return grid;
    }

    private static ChartSpecReading ReadChart(BoardChartView chart, IReadOnlyDictionary<string, Snapshot> bound, Dictionary<string, BoardInput> inputs, string path,
        Func<string, List<int>>? narrowed)
    {
        try
        {
            // A chart reads only what its inputs declare: a column the source has beside them is
            // not there for it, as it is not for a grid or a pivot (ADR-0341).
            return ChartSpec.Read(chart.Spec, name => bound.TryGetValue(name, out var snapshot) && inputs.TryGetValue(name, out var input)
                ? new DeclaredChartData(new SnapshotChartData(snapshot), input, narrowed?.Invoke(name))
                : null);
        }
        catch (ChartSpecRefusedException e)
        {
            throw new BoardRefusedException(e.Path.Length == 0 ? path : $"{path}.{e.Path}", e.Message);
        }
    }
}

/// <summary>A view of a bound Board.</summary>
public abstract class BoundView
{
    private protected BoundView(BoardView view) => View = view;

    /// <summary>The view as the Board writes it.</summary>
    public BoardView View { get; }
}

/// <summary>A grid view, its columns built from its input's shape (ADR-0342).</summary>
public sealed class BoundGridView : BoundView
{
    internal BoundGridView(BoardGridView view, ShapedGrid grid)
        : base(view) => Grid = grid;

    /// <summary>The grid.</summary>
    public ShapedGrid Grid { get; }
}

/// <summary>A pivot view over its input's Snapshot.</summary>
public sealed class BoundPivotView : BoundView
{
    internal BoundPivotView(BoardPivotView view, SnapshotPivotSource source)
        : base(view)
    {
        Source = Unnarrowed = source;
        Input = view.Input;
        Layout = view.Layout;
    }

    /// <summary>The pivot's source: over its input's rows, or, while a link narrows it, over the rows
    /// let through.</summary>
    public SnapshotPivotSource Source { get; internal set; }

    internal SnapshotPivotSource Unnarrowed { get; }

    internal string Input { get; }

    /// <summary>The layout the Board writes.</summary>
    public PivotLayout Layout { get; }
}

/// <summary>A chart view, its Chart Spec read from its inputs' data.</summary>
public sealed class BoundChartView : BoundView
{
    internal BoundChartView(BoardChartView view, ChartSpecReading reading)
        : base(view) => Reading = reading;

    /// <summary>The chart's title and definition, read from the rows its links let through.</summary>
    public ChartSpecReading Reading { get; internal set; }

    /// <summary>Why the chart is not drawn, naming the path, when its spec cannot draw the rows its links
    /// let through; null while it is drawn. <see cref="Reading"/> is then of other rows, and not shown.</summary>
    public string? Refusal { get; internal set; }
}

/// <summary>A chart's table showing only the columns its input declares, and, while a link narrows
/// it, only the rows at <c>rows</c>.</summary>
internal sealed class DeclaredChartData(IChartData data, BoardInput input, IReadOnlyList<int>? rows) : IChartData
{
    public IReadOnlyList<string> Columns { get; } = [.. input.Columns.Select(c => c.Name)];

    public int RowCount => rows?.Count ?? data.RowCount;

    public object? Value(string column, int row) => input.Shape.TryGetColumn(column, out _)
        ? data.Value(column, rows is null ? row : rows[row])
        : throw new ArgumentException($"Input '{input.Name}' does not declare column '{column}'.", nameof(column));
}
