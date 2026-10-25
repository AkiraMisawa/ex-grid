using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>Level Aggregates (ADR-0370 section 3): the question says the groups a layout shows, a server's
/// <c>GROUPING SETS</c> answer is built with the cells of those groups, and what an answer holds is either
/// leaves or cells. The shared cases (<c>tests/cases/pivot/source-levels.json</c>) hold what both engines
/// answer; these hold the C# surface around them.</summary>
public class LevelAggregateTests
{
    // Six leaves of Region and Product, and five groups shown: the grand total and each Region.
    private static PivotLayout Collapsed => new()
    {
        Rows = [P("Region") with { Collapsed = true }, P("Product")],
        Values = [Sum("Amount"), Sum("Quantity")],
    };

    [Fact] // ADR-0370 (section 3): the question for Level Aggregates carries the layout's collapse, one rule a field
    public void The_question_carries_what_the_layout_shows()
    {
        var toggled = Collapsed with { Rows = [P("Region") with { Collapsed = true, ToggledItems = [PivotItemKey.Text("west")] }, P("Product")] };
        var query = PivotQuery.ForLevels(toggled, maxLeaves: 5);

        Assert.Null(PivotQuery.For(toggled).Levels);
        var levels = Assert.IsType<PivotLevels>(query.Levels);
        Assert.Equal(2, levels.Rows.Count);
        Assert.Empty(levels.Columns);
        Assert.False(levels.Rows[0].IsExpanded(PivotItemKey.Text("East")));
        Assert.True(levels.Rows[0].IsExpanded(PivotItemKey.Text("WEST")));
        Assert.Same(PivotLevelRule.Expanded, levels.Rows[1]);
        // Levels are part of what is asked: the same fields under other levels are another question.
        Assert.NotEqual(PivotQuery.For(toggled, 5), query);
        Assert.Equal(PivotQuery.ForLevels(toggled, 5), query);
        Assert.NotEqual(query, PivotQuery.ForLevels(Collapsed, 5));
        Assert.Throws<ArgumentException>(() => new PivotQuery(rows: [new("Region")]) { Levels = new PivotLevels() });
    }

    [Fact] // ADR-0370 (section 3): a server's GROUPING SETS answer is the bundled source's level answer, cell for cell
    public async Task A_servers_grouping_sets_are_the_bundled_answer()
    {
        var query = PivotQuery.ForLevels(Collapsed, maxLeaves: 5);
        var bundled = await PivotSource.From(Sales, Fields).AggregateAsync(query, TestContext.Current.CancellationToken);
        Assert.True(bundled.IsLevels);

        // SELECT Region, Product, COUNT(*), SUM(Amount), SUM(Quantity)
        // GROUP BY GROUPING SETS ((), (Region))
        var builder = new PivotAnswerBuilder(query, "sql-1");
        void Add(IReadOnlyList<PivotItemKey> rows, IEnumerable<Sale> sales)
        {
            var group = sales.ToArray();
            var cell = builder.AddCell(rows, [], group.Length);
            builder.SetCounts(cell, 0, group.Length, group.Length);
            builder.SetSum(cell, 0, PivotNumber.Exact(group.Sum(s => s.Amount)));
            builder.SetCounts(cell, 1, group.Length, group.Length);
            builder.SetSum(cell, 1, PivotNumber.Exact(group.Sum(s => s.Quantity)));
        }
        Add([], Sales);
        foreach (var region in Sales.GroupBy(s => s.Region))
            Add([PivotItemKey.For(region.Key)], region);
        var server = builder.Build();

        Assert.True(server.IsLevels);
        Assert.Null(server.Mismatch(query));
        Assert.Equal(Cells(bundled), Cells(server));
    }

    [Fact] // ADR-0370 (section 3): an answer holds leaves or cells, never both, and cells only to a question with levels
    public void An_answer_holds_leaves_or_cells()
    {
        var leaves = new PivotAnswerBuilder(PivotQuery.For(Collapsed), "1");
        Assert.Throws<InvalidOperationException>(() => leaves.AddCell([], [], 1));

        var query = PivotQuery.ForLevels(Collapsed, maxLeaves: 2);
        var mixed = new PivotAnswerBuilder(query, "1");
        mixed.AddLeaf([PivotItemKey.Text("East"), PivotItemKey.Text("Apples")], 2);
        Assert.Throws<InvalidOperationException>(() => mixed.AddCell([], [], 1));

        var cells = new PivotAnswerBuilder(query, "1");
        var total = cells.AddCell([], [], 7);
        Assert.Throws<InvalidOperationException>(() => cells.AddLeaf([PivotItemKey.Text("East"), PivotItemKey.Text("Apples")], 2));
        Assert.Throws<ArgumentException>(() => cells.AddCell([], [], 1));
        Assert.Throws<ArgumentException>(() => cells.AddCell([PivotItemKey.Text("East"), PivotItemKey.Text("Apples"), PivotItemKey.Blank], [], 1));
        Assert.Throws<InvalidOperationException>(() => leaves.SetSum(0, 0, PivotNumber.Double(1), 1));
        // A double sum is carried with its magnitudes, or the answer cannot be checked against its parent.
        Assert.Throws<ArgumentOutOfRangeException>(() => cells.SetSum(total, 0, PivotNumber.Double(285.5), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => cells.SetSum(total, 0, PivotNumber.Double(285.5), -285.5));
        cells.SetSum(total, 0, PivotNumber.Double(285.5));
        Assert.Contains("no sum of its magnitudes", Assert.Throws<InvalidOperationException>(() => cells.Build()).Message);

        var over = new PivotAnswerBuilder(query, "1");
        over.AddCell([], [], 7);
        over.AddCell([PivotItemKey.Text("East")], [], 3);
        over.AddCell([PivotItemKey.Text("West")], [], 2);
        Assert.Equal(PivotSourceRefusalKind.TooManyLeaves, over.Build().Refusal!.Kind);
    }

    [Fact] // ADR-0370 (section 3): this engine lays no report out from Level Aggregates; the TypeScript engine does
    public async Task The_cube_is_not_made_from_cells()
    {
        var query = PivotQuery.ForLevels(Collapsed, maxLeaves: 5);
        var answer = await PivotSource.From(Sales, Fields).AggregateAsync(query, TestContext.Current.CancellationToken);

        Assert.Contains("Level Aggregates", Assert.Throws<InvalidOperationException>(() => PivotEngine.Cube(query, answer, Fields)).Message);
    }

    // Each cell by its Items: its records and each value's sum.
    private static Dictionary<string, string> Cells(PivotAnswer answer)
    {
        var axes = answer.Rows.Concat(answer.Columns).ToArray();
        var cells = new Dictionary<string, string>();
        for (var cell = 0; cell < answer.LeafCount; cell++)
        {
            var path = string.Join(" / ", axes.Select(a => a.ItemAt(cell) < 0 ? "*" : a.Items[a.ItemAt(cell)].ToString()));
            cells.Add(path, $"{answer.RecordsAt(cell)}: " + string.Join(", ", answer.Values.Select(v => v.SumAt(cell) is var sum && sum.IsExact ? $"{sum.ExactValue}" : $"{sum.Value}d")));
        }
        return cells;
    }
}
