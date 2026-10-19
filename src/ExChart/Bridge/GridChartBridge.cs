using System.Runtime.CompilerServices;
using global::ExChart.Engine;
using global::ExGrid.Chrome;
using global::ExGrid.Components;
using global::ExGrid.Selection;

namespace ExChart;

/// <summary>The stable identities of the commands a <see cref="GridChartBridge{TRow}"/> adds to a grid's menu (ADR-0036).</summary>
public static class GridChartCommands
{
    /// <summary>"Chart": the selection in the one orientation its columns declare.</summary>
    public const string Chart = "exchart.chart";

    /// <summary>"Series in columns", offered beside <see cref="InRows"/> only when a selection declares both.</summary>
    public const string InColumns = "exchart.chart.columns";

    /// <summary>"Series in rows", offered beside <see cref="InColumns"/> only when a selection declares both.</summary>
    public const string InRows = "exchart.chart.rows";

    /// <summary>The prefix of "Add to chart: <i>name</i>"; the target's name follows it.</summary>
    public const string AddPrefix = "exchart.add:";

    /// <summary>The wording of one of this bridge's commands, for the grid's <c>CommandLabel</c>; null for any other.</summary>
    public static string? Label(string id) => id switch
    {
        GridChartCommands.Chart => "Chart",
        GridChartCommands.InColumns => "Series in columns",
        GridChartCommands.InRows => "Series in rows",
        _ when id.StartsWith(GridChartCommands.AddPrefix, StringComparison.Ordinal) => $"Add to chart: {id[GridChartCommands.AddPrefix.Length..]}",
        _ => null,
    };
}

/// <summary>
/// The grid's side of a chart, ready-made (ADR-0258): context-menu commands a Consumer adds to an
/// <see cref="ExGrid{TRow}"/> in one line, which turn the selection into a <see cref="GridChart"/>, and
/// the reading of that chart, by Row Key from the Consumer's data, into the Chart Definition an
/// <c>ExChart</c> draws. ExGrid does not know it exists; where the chart is shown is the Consumer's.
/// </summary>
/// <remarks>
/// <para>The callbacks run where the grid's menu runs them; a Consumer that shows what they hand over
/// calls <c>StateHasChanged</c> on its own page.</para>
/// </remarks>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public sealed class GridChartBridge<TRow>
    where TRow : class
{
    private readonly ConditionalWeakTable<GridChart, ChartDefinition> _drawn = [];

    /// <summary>Creates a bridge that reads selections by <paramref name="declarations"/>.</summary>
    public GridChartBridge(GridChartDeclarations<TRow> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        Declarations = declarations;
    }

    /// <summary>What the Consumer declared for the grid's selections.</summary>
    public GridChartDeclarations<TRow> Declarations { get; }

    /// <summary>The grid whose selections are charted, set with <c>@ref</c>.</summary>
    public ExGrid<TRow>? Grid { get; set; }

    /// <summary>The open charts the menu offers "Add to chart" for (ADR-0258, U7), in the menu's order.</summary>
    public IReadOnlyList<ChartTarget> Targets { get; set; } = [];

    /// <summary>Raised with a chart made from the selection; the Consumer decides where it is shown.</summary>
    public Func<GridChart, Task>? OnChart { get; set; }

    /// <summary>Raised with a target and the chart it becomes with the selection's series added.</summary>
    public Func<ChartTarget, GridChart, Task>? OnAdded { get; set; }

    /// <summary>Raised with the sentence that says why a selection was not charted, or a point not shown in the grid.</summary>
    public Func<string, Task>? OnRefused { get; set; }

    /// <summary>
    /// The commands for the grid's <c>ContextCommands</c>, worded by <see cref="GridChartCommands.Label"/>: "Chart", or "Series in columns" and "Series
    /// in rows" when the selection declares both, and "Add to chart" for each of <see cref="Targets"/>.
    /// </summary>
    public IEnumerable<GridCommand> Commands(ContextMenuContext<TRow> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var enabled = context.Selection.Count > 0;
        var both = Declarations.Orientation is null && State() is { } g
            && SelectionReader.Declared(g, context.Selection, Declarations).Count == 2;
        if (both)
        {
            yield return new GridCommand(GridChartCommands.InColumns, enabled, () => MakeAsync(context, ChartOrientation.SeriesInColumns, null));
            yield return new GridCommand(GridChartCommands.InRows, enabled, () => MakeAsync(context, ChartOrientation.SeriesInRows, null));
        }
        else
        {
            yield return new GridCommand(GridChartCommands.Chart, enabled, () => MakeAsync(context, null, null));
        }
        foreach (var target in Targets)
            yield return new GridCommand(GridChartCommands.AddPrefix + target.Name, enabled, () => MakeAsync(context, null, target));
    }

    /// <summary>
    /// The Chart Definition <paramref name="chart"/> stands for now, read by Row Key from
    /// <see cref="GridChartDeclarations{TRow}.RowOf"/>: a recalculated value is redrawn, and a row that
    /// no longer exists is a gap with a Note (ADR-0258). While nothing it reads has changed, the
    /// definition last returned for the same chart is returned again, so the chart skips its render.
    /// </summary>
    /// <exception cref="ChartRefusedException">The chart can no longer be drawn, such as when its axis column left the grid.</exception>
    public ChartDefinition Draw(GridChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var g = State() ?? throw new InvalidOperationException("The bridge has no grid; set Grid with @ref before drawing.");
        var drawn = SelectionReader.Draw(g.Columns, g.Groups, chart, Declarations);
        if (_drawn.TryGetValue(chart, out var last) && last.Axis == drawn.Axis
            && last.Series.SequenceEqual(drawn.Series) && last.Notes.SequenceEqual(drawn.Notes))
            return last;
        _drawn.AddOrUpdate(chart, drawn);
        return drawn;
    }

    /// <summary>The grid cell a click on a point of <paramref name="chart"/> stands for, or null when its row no longer exists.</summary>
    public GridCell? CellOf(GridChart chart, ChartPointClick click)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(click);
        var d = Declarations;
        switch (chart.Series.FirstOrDefault(s => s.Id == click.SeriesId))
        {
            case ColumnSeries s:
                var axis = State()?.Columns.FirstOrDefault(c => c.Name == chart.AxisColumn);
                foreach (var r in s.Rows)
                {
                    if (d.RowOf(r.Key) is not { } row) continue;
                    var x = axis is null ? r.X : SelectionReader.AxisX(axis.Value(row)) ?? r.X;
                    if (x == click.Position) return new GridCell(r.Key, s.Column);
                }
                return null;
            case RowSeries s:
                if (d.RowOf(s.RowKey) is null) return null;
                var column = s.Columns.FirstOrDefault(c => d.PositionOf?.Invoke(c) == click.Position);
                return column is null ? null : new GridCell(s.RowKey, column);
            default:
                return null;
        }
    }

    /// <summary>
    /// Scrolls the grid to the cell a click on a point stands for and puts the Focus on it (ADR-0258,
    /// M3-Q6), through the grid's own placement, which drops a request made under another order.
    /// </summary>
    /// <returns>Whether the Focus was placed; when the row is not among the grid's rows, <see cref="OnRefused"/> says so.</returns>
    public async Task<bool> ShowInGridAsync(GridChart chart, ChartPointClick click)
    {
        if (Grid is not { } grid || CellOf(chart, click) is not { } cell || State() is not { } g) return false;
        var column = -1;
        for (var i = 0; i < g.Columns.Count; i++) if (g.Columns[i].Name == cell.Column) column = i;
        var row = -1;
        for (var i = 0; i < g.Window.Count && row < 0; i++) if (Equals(Declarations.RowKey(g.Window[i]), cell.RowKey)) row = g.WindowStart + i;
        if (row < 0 || column < 0)
        {
            await Refuse(row < 0 ? "That row is not among the rows the grid shows: it may be filtered out." : $"{cell.Column} is not among the grid's columns.");
            return false;
        }
        var at = new CellPosition(row, column);
        return await grid.PlaceSelectionAsync(new SelectionRange(row, column, 1, 1), at, g.RowSequenceVersion);
    }

    /// <summary>The grid as tests hand it in, in place of <see cref="Grid"/>.</summary>
    internal Func<GridState<TRow>>? StateOf { get; set; }

    private GridState<TRow>? State()
    {
        if (StateOf is { } of) return of();
        if (Grid is not { } grid) return null;
        var source = grid.Source;
        return new GridState<TRow>(
            grid.Columns,
            grid.HeaderGroups ?? [],
            source?.RowSequenceVersion ?? grid.RowSequenceVersion,
            source?.Window ?? grid.Window,
            source?.WindowStart ?? grid.WindowStart,
            source is null ? null : range => source.GetRowsAsync(range, CancellationToken.None));
    }

    private async Task MakeAsync(ContextMenuContext<TRow> context, ChartOrientation? orientation, ChartTarget? target)
    {
        if (State() is not { } g) return;
        var (chart, refusal) = await SelectionReader.ReadAsync(g, context.Selection, context.RowSequenceVersion, Declarations, orientation, target);
        if (refusal is not null) await Refuse(refusal);
        else if (target is not null) { if (OnAdded is { } added) await added(target, chart!); }
        else if (OnChart is { } made) await made(chart!);
    }

    private Task Refuse(string why) => OnRefused?.Invoke(why) ?? Task.CompletedTask;
}
