using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>Level Aggregates (ADR-0370 section 3): the question says the groups a layout shows, a server's
/// <c>GROUPING SETS</c> answer is built with the cells of those groups, and what an answer holds is either
/// leaves or cells; a sibling set ordered by a value and past the window's limit is answered as a window. The
/// shared cases (<c>tests/cases/pivot/source-levels.json</c>, <c>source-windows.json</c>) hold what both
/// engines answer; these hold the C# surface around them.</summary>
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

    // East's books B0…B(n−1) ordered by their sum, descending: a window of `limit` from where it is asked.
    private static PivotQuery Windowed(int limit, params PivotWindowStart[] starts) => new(
        rows: [new("Region"), new("Book")], values: [new("Amount", PivotParts.Counts | PivotParts.Sum)], maxLeaves: 50)
    {
        Levels = new PivotLevels(
            [PivotLevelRule.Expanded, new PivotLevelRule(false, [], new PivotLevelOrder(0, PivotAggregation.Sum, descending: true))],
            [],
            new PivotLevelWindow(limit, starts)),
    };

    private static readonly PivotItemKey East = PivotItemKey.Text("East");

    // A server's answer for East's ten books, B(i) summing to 9 − i: the books from `from` to `to` − 1 shown, a
    // margin each side, and a rest cell beyond each margin unless `rests` leaves it out.
    private static PivotAnswer Books(PivotQuery query, int from, int to, bool rests = true)
    {
        const int n = 10;
        var builder = new PivotAnswerBuilder(query, "sql-1");
        void Cell(int at, long records, long sum)
        {
            builder.SetCounts(at, 0, records, records);
            builder.SetSum(at, 0, PivotNumber.Exact(sum));
        }
        Cell(builder.AddCell([], [], n), n, 45);
        Cell(builder.AddCell([East], [], n), n, 45);
        // SELECT … ROW_NUMBER() OVER (PARTITION BY Region ORDER BY SUM(Amount) DESC): the books shown and the
        // margins as cells, those beyond the margins summed by the side they lie on.
        var window = builder.AddWindow([East], from, n - to);
        for (var i = Math.Max(0, from - 1); i < Math.Min(n, to + 1); i++)
            Cell(builder.AddCell([East, PivotItemKey.Text($"B{i}")], [], 1), 1, 9 - i);
        if (rests && from >= 2)
            Cell(builder.AddRest(window, PivotRestSide.Before, [], from - 1), from - 1, Enumerable.Range(0, from - 1).Sum(i => 9 - i));
        if (rests && n - to >= 2)
            Cell(builder.AddRest(window, PivotRestSide.After, [], n - to - 1), n - to - 1, Enumerable.Range(to + 1, n - to - 1).Sum(i => 9 - i));
        return builder.Build();
    }

    [Fact] // ADR-0370 (section 3): a server's window answers its question, a start past the end taken back to the last limit
    public void A_servers_window_answers_its_question()
    {
        var asked = Windowed(3, new PivotWindowStart([East], 4));
        var answer = Books(asked, 4, 7);

        Assert.Null(answer.Mismatch(asked));
        var window = Assert.Single(answer.Windows);
        Assert.Equal((4, 3), (window.Before, window.After));
        var restAfter = Enumerable.Range(0, answer.LeafCount).Single(c => answer.RestAt(c) is { Side: PivotRestSide.After });
        Assert.Equal(2, answer.RecordsAt(restAfter));
        Assert.Null(answer.RestAt(0));
        var past = Windowed(3, new PivotWindowStart([East], 40));
        Assert.Null(Books(past, 7, 10).Mismatch(past));
    }

    [Fact] // ADR-0370 (section 3): a window not where it was asked, without the rest beyond its margin, or of a set the limit answers whole is named
    public void A_window_that_is_not_the_answer_is_named()
    {
        Assert.Equal("its window 0 under [Region=Text:East] shows siblings 4 to 6, and the question asks for 0 to 2",
            Books(Windowed(3), 4, 7).Mismatch(Windowed(3)));
        Assert.Equal("its window 0 under [Region=Text:East] is of 10 siblings, which the limit of 12 answers whole",
            Books(Windowed(12), 4, 7).Mismatch(Windowed(12)));
        var asked = Windowed(3, new PivotWindowStart([East], 4));
        Assert.Equal("its window 0 has 4 siblings before it and no rest cell for those beyond its margin",
            Books(asked, 4, 7, rests: false).Mismatch(asked));
    }

    [Fact] // ADR-0370 (section 3): a builder takes one window a parent and one rest a side and column group
    public void A_builder_refuses_a_second_window_or_rest()
    {
        var builder = new PivotAnswerBuilder(Windowed(3), "1");
        builder.AddCell([], [], 10);
        var window = builder.AddWindow([East], 4, 3);
        Assert.Throws<ArgumentException>(() => builder.AddWindow([East], 0, 7));
        builder.AddRest(window, PivotRestSide.After, [], 2);
        Assert.Throws<ArgumentException>(() => builder.AddRest(window, PivotRestSide.After, [], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddRest(1, PivotRestSide.Before, [], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddRest(window, PivotRestSide.Before, [], 0));
    }

    [Fact] // ADR-0370 (section 3): a window is part of what is asked, its starts in any order
    public void A_window_is_part_of_the_question()
    {
        var west = PivotItemKey.Text("West");
        Assert.Equal(
            Windowed(3, new PivotWindowStart([East], 4), new PivotWindowStart([west], 1)),
            Windowed(3, new PivotWindowStart([west], 1), new PivotWindowStart([East], 4)));
        Assert.NotEqual(Windowed(3, new PivotWindowStart([East], 4)), Windowed(3, new PivotWindowStart([East], 5)));
        Assert.NotEqual(Windowed(3), Windowed(4));
        Assert.Contains("starts twice", Assert.Throws<ArgumentException>(() => new PivotLevelWindow(3, [new([East], 1), new([PivotItemKey.Text("EAST")], 2)])).Message);
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
