using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Row Identity is reference identity (ADR-0003), and the row diffing key must follow
/// it even when TRow overrides Equals — value-equal rows are still different rows.
/// </summary>
public class RowIdentityKeyTests : GridTestContext
{
    private sealed record RecordTrade(string Book, decimal Amount);

    private static readonly GridColumn<RecordTrade>[] RecordColumns =
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
    ];

    [Fact] // ADR-0003: value-equal record rows are different rows — both render, nothing throws
    public void Two_value_equal_record_rows_render_as_two_rows()
    {
        RecordTrade[] window = [new("Alpha", 1m), new("Alpha", 1m)];

        var cut = Render<ExGrid<RecordTrade>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.Columns, RecordColumns));

        Assert.Equal(2, cut.FindAll(".ex-row").Count);
    }

    [Fact] // ADR-0003: replacing a record row instance still repaints only that row
    public void Replacing_a_record_row_instance_repaints_only_that_row()
    {
        RecordTrade[] window = [new("Alpha", 1m), new("Beta", 2m)];
        var cut = Render<ExGrid<RecordTrade>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.Columns, RecordColumns));

        RecordTrade[] next = [window[0], new("Gamma", 3m)];
        cut.Render(ps => ps
            .Add(g => g.Window, next)
            .Add(g => g.Columns, RecordColumns));

        foreach (var row in cut.FindComponents<ExGridRow<RecordTrade>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
        Assert.Contains("Gamma", cut.Markup);
    }

    [Fact] // ADR-0003: the same instance at two positions makes Row Identity ambiguous — refused
    public void The_same_row_instance_twice_is_refused()
    {
        var rows = TestRows.Window();
        TestRow[] window = [rows[0], rows[1], rows[0]];

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.Columns, TestRows.Columns())));
        Assert.Contains("Row Identity", ex.Message);
    }

    [Fact] // ADR-0001: a null is not a row — refused, not rendered as a gap
    public void A_null_row_is_refused()
    {
        TestRow?[] window = [TestRows.Window()[0], null];

        Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window!)
            .Add(g => g.Columns, TestRows.Columns())));
    }

    [Fact] // ERR-4, ADR-0003: the refusal of a repeated instance names the position that repeats
    public void ERR4_a_repeated_row_instance_is_refused_naming_its_position()
    {
        var rows = TestRows.Window();
        TestRow[] window = [rows[0], rows[1], rows[2], rows[1]];

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.Columns, TestRows.Columns())));
        Assert.Contains("Window[3]", ex.Message);
    }

    [Fact] // ERR-4, ADR-0003: the refusal of a null row names the position that holds it
    public void ERR4_a_null_row_is_refused_naming_its_position()
    {
        var rows = TestRows.Window();
        TestRow?[] window = [rows[0], rows[1], null, rows[2]];

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window!)
            .Add(g => g.Columns, TestRows.Columns())));
        Assert.Contains("Window[2]", ex.Message);
    }

    [Fact] // ADR-0018/0033, widened-run ticket 08: grids of two row types on one page never share a root id
    public void Grids_of_two_row_types_have_their_own_root_ids()
    {
        var trades = Render<ExGrid<RecordTrade>>(ps => ps
            .Add(g => g.Window, new RecordTrade[] { new("Alpha", 1m) })
            .Add(g => g.Columns, RecordColumns));
        var rows = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book)]));

        Assert.NotEqual(trades.Find(".ex-grid").Id, rows.Find(".ex-grid").Id);
    }
}
