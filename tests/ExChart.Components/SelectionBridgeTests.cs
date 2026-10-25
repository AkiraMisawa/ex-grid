using ExChart.Engine;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Selection;
using Xunit;

namespace ExChart.Components.Tests;

/// <summary>The selection bridge (ADR-0251, ADR-0258), read against a grid handed in as its state.</summary>
public class SelectionBridgeTests
{
    // A curve history: a date down the side, tenors across, and an exposure profile beside them.
    private sealed record Day(DateOnly Date, string Note, decimal Epe, decimal Ene, decimal Diff, double Y1, double Y5, decimal Recovery);

    private static readonly DateOnly D0 = new(2026, 1, 2);

    private static readonly GridColumn<Day>[] Columns =
    [
        new("date", ColumnType.Date, r => r.Date, "Date"),
        new("note", ColumnType.Text, r => r.Note, "Note"),
        new("epe", ColumnType.Number, r => r.Epe, "EPE"),
        new("ene", ColumnType.Number, r => r.Ene, "ENE"),
        new("diff", ColumnType.Number, r => r.Diff, "Diff"),
        new("y1", ColumnType.Number, r => r.Y1, "1Y"),
        new("y5", ColumnType.Number, r => r.Y5, "5Y"),
        new("recovery", ColumnType.Number, r => r.Recovery, "Recovery"),
    ];

    private static readonly HeaderGroup[] Groups =
    [
        new("Exposure", ["epe", "ene", "diff"]),
        new("Rates", ["y1", "y5"]),
    ];

    private readonly List<Day> _rows = Enumerable.Range(0, 4)
        .Select(i => new Day(D0.AddDays(i), $"n{i}", 10 + i, -(5 + i), i % 2 == 0 ? 1 : -1, 1 + i / 10.0, 2 + i / 10.0, 0.4m))
        .ToList();

    private int _version = 7;

    private GridChartDeclarations<Day> Declarations(ChartOrientation? orientation = null) => new()
    {
        RowKey = r => r.Date,
        RowOf = k => _rows.FirstOrDefault(r => r.Date == (DateOnly)k),
        KindOf = _ => SeriesKind.Level,
        PositionOf = c => c switch { "y1" => 1, "y5" => 5, _ => null },
        PositionTitle = "Tenor (years)",
        RowName = r => r.Date.ToString("yyyy-MM-dd"),
        Orientation = orientation,
    };

    private GridState<Day> State() => new(Columns, Groups, _version, _rows, 0, null);

    private static SelectionRange R(int top, int left, int rows, int columns) => new(top, left, rows, columns);

    private Task<(GridChart? Chart, string? Refusal)> Read(IReadOnlyList<SelectionRange> ranges, ChartOrientation? orientation = null, ChartTarget? target = null, int? version = null) =>
        SelectionReader.ReadAsync(State(), ranges, version ?? _version, Declarations(), orientation, target);

    private ChartDefinition Draw(GridChart chart) => SelectionReader.Draw(Columns, Groups, chart, Declarations());

    [Fact] // ADR-0258: an axis column charts series in columns, each a column at the Row Keys it was made from
    public async Task ADR0258_an_axis_column_charts_series_in_columns_by_row_key()
    {
        var (chart, refusal) = await Read([R(0, 0, 3, 1), R(0, 2, 3, 2)]);
        Assert.Null(refusal);
        Assert.Equal(ChartOrientation.SeriesInColumns, chart!.Orientation);
        Assert.Equal("date", chart.AxisColumn);
        var epe = Assert.IsType<ColumnSeries>(chart.Series[0]);
        Assert.Equal("epe", epe.Column);
        Assert.Equal("Exposure", epe.Panel);
        Assert.Equal([D0, D0.AddDays(1), D0.AddDays(2)], epe.Rows.Select(r => (DateOnly)r.Key));
        var definition = Draw(chart);
        Assert.Equal(AxisValues.Dates, definition.Axis.Values);
        Assert.Equal("Exposure · EPE", definition.Series[0].Name);
        Assert.Equal(10m, definition.Series[0].Points[0].Y!.Value.Value);
    }

    [Fact] // ADR-0251, spine rule 6: a selection taken under another Row Sequence Version is refused
    public async Task ADR0251_a_stale_selection_is_refused()
    {
        var (chart, refusal) = await Read([R(0, 0, 3, 3)], version: 6);
        Assert.Null(chart);
        Assert.Equal("The rows were reordered after the selection was made. Select again.", refusal);
    }

    [Fact] // ADR-0258: a selection that declares neither orientation is refused by name
    public async Task ADR0258_neither_an_axis_nor_positions_is_refused()
    {
        var (_, refusal) = await Read([R(0, 2, 3, 2)]);
        Assert.Contains("no axis column", refusal);
    }

    [Fact] // ADR-0251: two axis columns, or a chosen text column, are refused by name
    public async Task ADR0251_two_axes_or_a_text_column_are_refused()
    {
        var two = new GridChartDeclarations<Day>
        {
            RowKey = r => r.Date, RowOf = _ => null, AxisColumns = new HashSet<string> { "epe" },
        };
        var (_, axes) = await SelectionReader.ReadAsync(State(), [R(0, 0, 3, 3)], _version, two, ChartOrientation.SeriesInColumns, null);
        Assert.Contains("2 axis columns (Date, EPE)", axes);
        var (_, text) = await Read([R(0, 0, 3, 3)]);
        Assert.Equal("Note cannot be plotted: not a number column.", text);
    }

    [Fact] // ADR-0251: one cell charts the Window, leaving out the text column nobody chose
    public async Task ADR0251_one_cell_charts_the_window()
    {
        var (chart, refusal) = await Read([R(1, 2, 1, 1)], ChartOrientation.SeriesInColumns);
        Assert.Null(refusal);
        Assert.Equal(["epe", "ene", "diff", "y1", "y5", "recovery"], chart!.Series.Select(s => s.Id));
        Assert.All(chart.Series.OfType<ColumnSeries>(), s => Assert.Equal(4, s.Rows.Count));
    }

    [Fact] // ADR-0258: the menu offers two items only when a selection declares both orientations
    public void ADR0258_two_items_only_when_both_are_declared()
    {
        var bridge = new GridChartBridge<Day>(Declarations()) { StateOf = State };
        string[] Ids(params SelectionRange[] ranges) =>
            bridge.Commands(new ContextMenuContext<Day>(_rows[0], "date", ColumnType.Date, ranges, _version, [], () => { })).Select(c => c.Id).ToArray();
        Assert.Equal([GridChartCommands.Chart], Ids(R(0, 0, 3, 3)));
        Assert.Equal([GridChartCommands.Chart], Ids(R(0, 5, 3, 2)));
        Assert.Equal([GridChartCommands.InColumns, GridChartCommands.InRows], Ids(R(0, 0, 3, 7)));
        bridge.Targets = [new ChartTarget("Exposure", new GridChart(ChartOrientation.SeriesInColumns, "date", EquatableList<GridChartSeries>.Empty))];
        Assert.Equal("Add to chart: Exposure", GridChartCommands.Label(Ids(R(0, 0, 3, 3))[^1]));
        Assert.Equal("Series in rows", GridChartCommands.Label(GridChartCommands.InRows));
    }

    [Fact] // ADR-0253, ADR-0258: a column declared away from zero is read away from zero, in both orientations
    public async Task ADR0253_a_level_declared_away_from_zero_is_read_away_from_zero()
    {
        var declarations = Declarations() with { FromZeroOf = c => c is "y1" or "y5" ? false : null };
        var (columns, _) = await SelectionReader.ReadAsync(State(), [R(0, 0, 3, 1), R(0, 5, 3, 1), R(0, 2, 3, 1)], _version, declarations, ChartOrientation.SeriesInColumns, null);
        var inColumns = SelectionReader.Draw(Columns, Groups, columns!, declarations);
        var byId = inColumns.Series.ToDictionary(s => s.Id);
        Assert.False(byId["y1"].FromZero);
        Assert.False(byId["y1"].ReachesZero);
        Assert.Null(byId["epe"].FromZero);
        Assert.True(byId["epe"].ReachesZero); // EPE keeps the Series Kind's default

        var (rows, _) = await SelectionReader.ReadAsync(State(), [R(1, 5, 2, 2)], _version, declarations, null, null);
        Assert.All(SelectionReader.Draw(Columns, Groups, rows!, declarations).Series, s => Assert.False(s.ReachesZero));
    }

    [Fact] // ADR-0258, spine rule 1: a curve's columns declared both from zero and away from it are refused by name
    public async Task ADR0258_a_curve_mixing_from_zero_declarations_is_refused()
    {
        var declarations = Declarations() with { FromZeroOf = c => c == "y1" ? false : null };
        var (chart, refusal) = await SelectionReader.ReadAsync(State(), [R(1, 5, 2, 2)], _version, declarations, null, null);
        Assert.Null(chart);
        Assert.Equal("Rates are declared both from zero (5Y) and away from it (1Y), so they are not one curve.", refusal);
    }

    [Fact] // ADR-0253, ADR-0258: declarations are compared as they are drawn: null beside true is alike for a Level
    public async Task ADR0258_declarations_that_read_zero_alike_are_one_curve()
    {
        var declarations = Declarations() with { FromZeroOf = c => c == "y1" ? true : null };
        var (chart, refusal) = await SelectionReader.ReadAsync(State(), [R(1, 5, 2, 2)], _version, declarations, null, null);
        Assert.Null(refusal);
        Assert.All(SelectionReader.Draw(Columns, Groups, chart!, declarations).Series, s => Assert.True(s.ReachesZero));
    }

    [Fact] // ADR-0258, spine rule 1: a curve whose declarations changed after it was made is refused when drawn, not drawn by one column's
    public async Task ADR0258_a_curve_whose_declarations_changed_is_refused_when_drawn()
    {
        var (chart, _) = await SelectionReader.ReadAsync(State(), [R(1, 5, 2, 2)], _version, Declarations() with { FromZeroOf = _ => false }, null, null);
        var changed = Declarations() with { FromZeroOf = c => c == "y1" ? false : null };
        var refused = Assert.Throws<ChartRefusedException>(() => SelectionReader.Draw(Columns, Groups, chart!, changed));
        Assert.Contains("now declared both from zero and away from it", refused.Message);
    }

    [Fact] // ADR-0253: an amount per period declared away from zero is refused by name when the selection is read
    public async Task ADR0253_an_amount_per_period_away_from_zero_is_refused()
    {
        var declarations = Declarations() with { KindOf = _ => SeriesKind.AmountPerPeriod, FromZeroOf = _ => false };
        var (chart, refusal) = await SelectionReader.ReadAsync(State(), [R(0, 0, 3, 1), R(0, 2, 3, 1)], _version, declarations, ChartOrientation.SeriesInColumns, null);
        Assert.Null(chart);
        Assert.StartsWith("'Exposure · EPE' is an Amount per period", refusal);
    }

    [Fact] // ADR-0258: series in rows, a row across the columns at declared positions
    public async Task ADR0258_positions_chart_series_in_rows()
    {
        var (chart, refusal) = await Read([R(1, 5, 2, 2)]);
        Assert.Null(refusal);
        Assert.Equal(ChartOrientation.SeriesInRows, chart!.Orientation);
        var definition = Draw(chart);
        Assert.Equal("Tenor (years)", definition.Axis.Title);
        Assert.Equal("2026-01-03 · Rates", definition.Series[0].Name);
        Assert.Equal([1.0, 5.0], definition.Series[0].Points.Select(p => p.X));
        Assert.Equal("5Y", definition.Axis.Write(5));
    }

    [Fact] // ADR-0251: a number column with no declared position is refused by name in rows
    public async Task ADR0251_an_unpositioned_number_is_refused_in_rows()
    {
        var (_, refusal) = await Read([R(0, 5, 2, 3)]);
        Assert.Equal("Recovery has no declared position on the Tenor (years) axis, so it cannot be drawn here.", refusal);
    }

    [Fact] // ADR-0258: a recalculated value is redrawn; a row gone is a gap with a Note; a new row is not added
    public async Task ADR0258_the_chart_follows_its_row_keys()
    {
        var (chart, _) = await Read([R(0, 0, 3, 1), R(0, 2, 3, 1)]);
        _rows[0] = _rows[0] with { Epe = 99 };
        _rows.RemoveAt(1);
        _rows.Insert(1, _rows[0] with { Date = D0.AddDays(-1) });
        var definition = Draw(chart!);
        var points = definition.Series[0].Points;
        Assert.Equal(3, points.Count);
        Assert.Equal(99m, points[0].Y!.Value.Value);
        Assert.Null(points[1].Y);
        Assert.Equal(D0.AddDays(1).DayNumber, points[1].X);
        Assert.Equal(["1 of the selected rows no longer exists."], definition.Notes);
    }

    [Fact] // ADR-0258, M3-Q7: values are read by Row Key from the Consumer's data, not from the Window
    public async Task ADR0258_a_row_filtered_out_of_the_grid_is_not_gone()
    {
        var (chart, _) = await Read([R(0, 0, 3, 1), R(0, 2, 3, 1)]);
        var narrowed = new GridState<Day>(Columns, Groups, _version + 1, [_rows[0]], 0, null);
        var definition = SelectionReader.Draw(narrowed.Columns, narrowed.Groups, chart!, Declarations());
        Assert.Empty(definition.Notes);
        Assert.All(definition.Series[0].Points, p => Assert.NotNull(p.Y));
    }

    [Fact] // ADR-0251: a Diff, negatives beside positives, goes to a panel of its own
    public async Task ADR0251_a_diff_goes_to_a_panel_of_its_own()
    {
        var (chart, _) = await Read([R(0, 0, 4, 1), R(0, 2, 4, 3)]);
        Assert.Equal(["Exposure", "Exposure", "Exposure · Diff"], chart!.Series.Select(s => s.Panel));
    }

    [Fact] // ADR-0258, U7, U8: an added series keeps its own rows; one already in the chart, or of another axis, is refused
    public async Task ADR0258_adding_a_series()
    {
        var (made, _) = await Read([R(0, 0, 2, 1), R(0, 2, 2, 1)]);
        var target = new ChartTarget("Exposure", made!);
        var (added, refusal) = await Read([R(2, 3, 2, 1)], target: target);
        Assert.Null(refusal);
        Assert.Equal(["epe", "ene"], added!.Series.Select(s => s.Id));
        Assert.Equal(2, ((ColumnSeries)added.Series[1]).Rows.Count);
        Assert.Equal(D0.AddDays(2), ((ColumnSeries)added.Series[1]).Rows[0].Key);

        var (_, again) = await Read([R(2, 2, 2, 1)], target: target);
        Assert.Equal("EPE is already in 'Exposure'.", again);
        var (_, curve) = await Read([R(0, 5, 2, 2)], target: target);
        Assert.Equal("The selection charts curves across declared positions, but 'Exposure' is drawn along Date.", curve);
    }

    [Fact] // ADR-0252: while nothing it reads changed, the bridge hands back the same definition, so the chart skips its render
    public async Task ADR0252_an_unchanged_chart_draws_the_same_definition()
    {
        var bridge = new GridChartBridge<Day>(Declarations()) { StateOf = State };
        var (chart, _) = await Read([R(0, 0, 3, 1), R(0, 2, 3, 1)]);
        var first = bridge.Draw(chart!);
        Assert.Same(first, bridge.Draw(chart!));
        _rows[0] = _rows[0] with { Epe = 1 };
        Assert.NotSame(first, bridge.Draw(chart!));
    }

    [Fact] // ADR-0258, M3-Q6: a click on a point names its row and column
    public async Task ADR0258_a_point_names_its_cell()
    {
        var bridge = new GridChartBridge<Day>(Declarations()) { StateOf = State };
        var (history, _) = await Read([R(0, 0, 3, 1), R(0, 3, 3, 1)]);
        Assert.Equal(new GridCell(D0.AddDays(1), "ene"), bridge.CellOf(history!, new ChartPointClick("ene", D0.AddDays(1).DayNumber)));
        var (curves, _) = await Read([R(2, 5, 1, 2)]);
        Assert.Equal(new GridCell(D0.AddDays(2), "y5"), bridge.CellOf(curves!, new ChartPointClick(curves!.Series[0].Id, 5)));
    }
}
