using System.Globalization;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// A Board with every input bound to its data (ADR-0341): each input's shape checked against its
/// declaration, and each view made what it draws. Built by <see cref="Bind"/>; drawn by the
/// <c>ExBoard</c> component.
/// </summary>
public sealed class BoundBoard
{
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
                BoardGridView grid => new BoundGridView(grid, ShapedGrid.Of(bound[grid.Grid.Data], grid.Grid, culture)),
                BoardPivotView pivot => new BoundPivotView(pivot, PivotSource.From(bound[pivot.Input])),
                BoardChartView chart => new BoundChartView(chart, ReadChart(chart, bound, inputs, $"views[{i}].chart")),
                var other => throw new InvalidOperationException($"Unknown view {other.GetType().Name}."),
            });
        }
        return new BoundBoard(board, bound, views);
    }

    private static ChartSpecReading ReadChart(BoardChartView chart, Dictionary<string, Snapshot> bound, Dictionary<string, BoardInput> inputs, string path)
    {
        try
        {
            // A chart reads only what its inputs declare: a column the source has beside them is
            // not there for it, as it is not for a grid or a pivot (ADR-0341).
            return ChartSpec.Read(chart.Spec, name => bound.TryGetValue(name, out var snapshot) && inputs.TryGetValue(name, out var input)
                ? new DeclaredChartData(new SnapshotChartData(snapshot), input)
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
        Source = source;
        Layout = view.Layout;
    }

    /// <summary>The pivot's source.</summary>
    public SnapshotPivotSource Source { get; }

    /// <summary>The layout the Board writes.</summary>
    public PivotLayout Layout { get; }
}

/// <summary>A chart view, its Chart Spec read from its inputs' data.</summary>
public sealed class BoundChartView : BoundView
{
    internal BoundChartView(BoardChartView view, ChartSpecReading reading)
        : base(view) => Reading = reading;

    /// <summary>The chart's title and definition.</summary>
    public ChartSpecReading Reading { get; }
}

/// <summary>A chart's table showing only the columns its input declares.</summary>
internal sealed class DeclaredChartData(IChartData data, BoardInput input) : IChartData
{
    public IReadOnlyList<string> Columns { get; } = [.. input.Columns.Select(c => c.Name)];

    public int RowCount => data.RowCount;

    public object? Value(string column, int row) => input.Shape.TryGetColumn(column, out _)
        ? data.Value(column, row)
        : throw new ArgumentException($"Input '{input.Name}' does not declare column '{column}'.", nameof(column));
}
