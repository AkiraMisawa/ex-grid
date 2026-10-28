using Bunit;
using ExChart.Engine;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExChart.Components.Tests;

/// <summary>
/// ADR-0258 / ADR-0421: the bridge charts the selections of a grid the Script Core draws as it charts the Razor
/// component's: it reads the rows from the source the grid reads, and puts the Focus on a point's cell through the
/// grid's own placement.
/// </summary>
public class ScriptGridChartBridgeTests : BunitContext
{
    private sealed record Day(DateOnly Date, double Epe);

    private static readonly DateOnly D0 = new(2026, 1, 2);

    private static readonly GridColumn<Day>[] Columns =
    [
        new("date", ColumnType.Date, r => r.Date, "Date"),
        new("epe", ColumnType.Number, r => r.Epe, "EPE"),
    ];

    private readonly List<Day> _rows = [.. Enumerable.Range(0, 4).Select(i => new Day(D0.AddDays(i), 10 + i))];
    private readonly BunitJSModuleInterop _grid;

    public ScriptGridChartBridgeTests()
    {
        var module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        _grid = module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
        _grid.Setup<bool>("placeSelection", _ => true).SetResult(true);
    }

    private GridChartBridge<Day> Bridge(global::ExGrid.Script.ExGrid<Day> grid) => new(new GridChartDeclarations<Day>
    {
        RowKey = r => r.Date,
        RowOf = k => _rows.FirstOrDefault(r => r.Date == (DateOnly)k),
        KindOf = _ => SeriesKind.Level,
    })
    { ScriptGrid = grid };

    private global::ExGrid.Script.ExGrid<Day> Grid() =>
        Render<global::ExGrid.Script.ExGrid<Day>>(ps => ps.Add(g => g.Rows, _rows).Add(g => g.Columns, Columns)).Instance;

    private static int Version(global::ExGrid.Script.ExGrid<Day> grid) => Assert.IsAssignableFrom<IGridSource<Day>>(grid.RowSource).RowSequenceVersion;

    private static ContextMenuContext<Day> Opened(global::ExGrid.Script.ExGrid<Day> grid, Day row, params SelectionRange[] selection) =>
        new(row, "epe", ColumnType.Number, selection, Version(grid), [], () => { });

    [Fact] // ADR-0258 / ADR-0421: "Chart" over the Script grid reads the selected rows from the source it reads, and the chart follows them
    public async Task ADR0421_a_selection_of_the_script_grid_is_charted_from_the_rows_its_source_holds()
    {
        var grid = Grid();
        var bridge = Bridge(grid);
        GridChart? made = null;
        bridge.OnChart = chart => { made = chart; return Task.CompletedTask; };

        var chart = Assert.Single(bridge.Commands(Opened(grid, _rows[0], new SelectionRange(0, 0, 3, 2))));
        Assert.Equal(GridChartCommands.Chart, chart.Id);
        await chart.Invoke();

        Assert.NotNull(made);
        var drawn = bridge.Draw(made);
        Assert.Equal(AxisValues.Dates, drawn.Axis.Values);
        Assert.Equal([10.0, 11.0, 12.0], drawn.Series[0].Points.Select(p => (double)p.Y!.Value.Value));
    }

    [Fact] // ADR-0258 M3-Q6: a click on a point puts the Focus on its cell through the Script grid's own placement
    public async Task ADR0258_a_point_is_shown_in_the_script_grid_through_its_placement()
    {
        var grid = Grid();
        var bridge = Bridge(grid);

        var placed = await bridge.ShowInGridAsync(new GridCell(D0.AddDays(2), "epe"));

        Assert.True(placed);
        var asked = Assert.Single(_grid.Invocations["placeSelection"]);
        Assert.Equal(Version(grid), asked.Arguments[2]);
    }

    [Fact] // ADR-0421: a bridge charts one grid, and says so when handed two
    public void ADR0421_a_bridge_handed_both_grids_is_refused()
    {
        var grid = Grid();
        var bridge = Bridge(grid);
        bridge.Grid = new global::ExGrid.Components.ExGrid<Day>();

        Assert.Throws<InvalidOperationException>(() => bridge.Commands(Opened(grid, _rows[0], new SelectionRange(0, 0, 1, 1))).ToList());
    }
}
