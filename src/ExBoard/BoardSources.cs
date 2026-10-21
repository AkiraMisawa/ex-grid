using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// An input held where the data lives (ADR-0343): the host's own code, which says the input's shape,
/// fetches a grid's rows and, if it can, makes a pivot's report source. The Board never talks to a server
/// itself; each call here is handed the narrowing the Board's links put on the view, for the host to
/// execute where the data lives.
/// </summary>
/// <param name="shape">Answers the input's shape, before any row (ADR-0341).</param>
/// <param name="fetch">Answers a grid view's query, narrowed by its links, with a page of rows.</param>
public sealed class BoardSource(Func<CancellationToken, ValueTask<DataShape>> shape, Func<BoardQuery, CancellationToken, ValueTask<ShapedPage>> fetch)
    : IShapedSource
{
    private readonly Func<CancellationToken, ValueTask<DataShape>> _shape = shape ?? throw new ArgumentNullException(nameof(shape));

    /// <summary>Answers a grid view's query, narrowed by its links, with a page of rows.</summary>
    public Func<BoardQuery, CancellationToken, ValueTask<ShapedPage>> Fetch { get; } = fetch ?? throw new ArgumentNullException(nameof(fetch));

    /// <summary>
    /// Makes a pivot view's report source over the input's rows that the given conditions let through
    /// (all of them; none for the whole input), or null where the host cannot. Called again each time
    /// the view's narrowing changes. A Board with a pivot over this input is refused when null.
    /// </summary>
    public Func<IReadOnlyList<LinkCondition>, PivotReportSource>? Report { get; init; }

    /// <summary>
    /// Answers a chart view's series with the points it draws, thinned where the data lives (ADR-0344), as
    /// <see cref="ChartSource.Thin"/> thins, under the narrowing the Board's links put on the view; or null
    /// where the host cannot. A Board with a chart over this input is refused when null.
    /// </summary>
    public Func<BoardChartQuery, CancellationToken, ValueTask<ChartSourceAnswer>>? Thin { get; init; }

    /// <summary>
    /// Answers a link from a pivot view over this input with the values of the link's columns among the
    /// rows under the selected cells (ADR-0345), as <see cref="ValuesUnder"/> reads them; or null where the
    /// host cannot. A Board with such a link is refused when null.
    /// </summary>
    public Func<BoardCellsQuery, CancellationToken, ValueTask<LinkCondition>>? Under { get; init; }

    /// <summary>
    /// The reference implementation of <see cref="Under"/> (ADR-0345), and the reading the Board makes of
    /// a pivot over a Snapshot: the values of <see cref="BoardCellsQuery.On"/> of every row of
    /// <paramref name="data"/> the narrowing lets through and any of the cells stands for. A cell is over
    /// the rows whose values are the Items its paths name, so a cell on a subtotal stands for every Item
    /// below it and a grand total for every row; an Item the layout hides is under none.
    /// </summary>
    /// <exception cref="ArgumentException">A cell names a field, or the link a column, that the data does not hold.</exception>
    public static LinkCondition ValuesUnder(Snapshot data, BoardCellsQuery query)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(query);
        var snapshot = data;
        // The rows the narrowing lets through, read in place rather than copied.
        IEnumerable<int> through = query.Narrowing.Count == 0 ? Enumerable.Range(0, data.Rows.Count) : Narrowing.Positions(data, query.Narrowing);
        var hidden = PivotQuery.For(query.Layout).Placed.Where(f => f.HiddenItems.Count > 0 && snapshot.TryGetColumn(f.Field, out _))
            .Select(f => (Column: snapshot[f.Field], Hidden: f.HiddenItems.ToHashSet())).ToArray();
        // The cells grouped by the fields their paths name, each group a set of the Items they name
        // there, so a row's keys are read once and looked up, not compared with every cell. A step naming
        // no column of the data would constrain nothing, and so let through rows the cell does not stand for.
        var groups = new Dictionary<string, (SnapshotColumn[] Columns, HashSet<PivotItemKey[]> Items)>(StringComparer.Ordinal);
        foreach (var cell in query.Cells)
        {
            var steps = cell.Items.ToArray();
            var fields = string.Join("\u001f", steps.Select(i => i.Field));
            if (!groups.TryGetValue(fields, out var group))
            {
                group = (steps.Select(i => snapshot.TryGetColumn(i.Field, out var column) ? column
                    : throw new ArgumentException($"A cell names field '{i.Field}', which pivot '{query.View}' does not read from its data.", nameof(query))).ToArray(),
                    new HashSet<PivotItemKey[]>(BoundBoard.ItemsComparer.Instance));
                groups.Add(fields, group);
            }
            group.Items.Add([.. steps.Select(i => i.Item)]);
        }
        var columns = query.On.Select(n => snapshot.TryGetColumn(n, out var column) ? column
            : throw new ArgumentException($"The link compares column '{n}', which pivot '{query.View}' does not read from its data.", nameof(query))).ToArray();
        var tuples = new List<object?[]>();
        foreach (var position in through)
        {
            var row = snapshot.Rows[position];
            if (hidden.Any(h => h.Hidden.Contains(PivotItemKey.For(snapshot.ValueAt(row, h.Column)))))
                continue;
            if (groups.Values.Any(g => g.Items.Contains([.. g.Columns.Select(c => PivotItemKey.For(snapshot.ValueAt(row, c)))])))
                tuples.Add([.. columns.Select(c => snapshot.ValueAt(row, c))]);
        }
        return new LinkCondition(query.On, tuples);
    }

    /// <inheritdoc />
    public async ValueTask<DataShape?> ShapeAsync(CancellationToken cancellationToken = default) => await _shape(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// What a grid view over a <see cref="BoardSource"/> asks (ADR-0343): the grid's own query, its range, sort
/// and filter (ADR-0025), and the conditions the Board's links narrow the view by now, every one of which
/// a row must meet. Empty while nothing narrows it.
/// </summary>
/// <param name="View">The id of the grid view asking.</param>
/// <param name="Grid">The grid's query.</param>
/// <param name="Narrowing">The links' conditions, each over its link's columns.</param>
public sealed record BoardQuery(string View, GridQuery Grid, IReadOnlyList<LinkCondition> Narrowing);

/// <summary>
/// What a chart view over a <see cref="BoardSource"/> asks for each of its series (ADR-0344): the Chart
/// Source's question, and the conditions the Board's links narrow the view by now, every one of which a
/// row must meet. Empty while nothing narrows it.
/// </summary>
/// <param name="View">The id of the chart view asking.</param>
/// <param name="Chart">The column, the range and the pixels asked for.</param>
/// <param name="Narrowing">The links' conditions, each over its link's columns.</param>
public sealed record BoardChartQuery(string View, ChartSourceQuery Chart, IReadOnlyList<LinkCondition> Narrowing);

/// <summary>
/// What a link from a pivot view over a <see cref="BoardSource"/> asks as its cells are selected
/// (ADR-0345): the values of <see cref="On"/> among the rows under any of <see cref="Cells"/>.
/// </summary>
/// <param name="View">The id of the pivot view the cells are selected in.</param>
/// <param name="Layout">The layout the cells were read from, with the Items it hides.</param>
/// <param name="Cells">The cells selected, by the Items their paths name.</param>
/// <param name="On">The link's columns.</param>
/// <param name="Narrowing">The conditions the pivot's own links narrow it by now, every one of which a row must meet.</param>
public sealed record BoardCellsQuery(string View, PivotLayout Layout, IReadOnlyList<PivotCellItems> Cells, IReadOnlyList<string> On, IReadOnlyList<LinkCondition> Narrowing);
