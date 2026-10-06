using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// <c>GridSource.From</c>'s side of LV-16 (ADR-0141/0142, D5): <c>PublishGathered</c> puts out what
/// the source has gathered, at once, on the grid's context, and raises <c>StateChanged</c> for it as
/// every publication does. The grid that called it reads the Window itself straight after, so for
/// that grid the event is redundant: these tests hold it to costing no repaint of every row, and to
/// reaching a grid that did not call it.
/// </summary>
public class PublishGatheredTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly GridColumn<TestRow>[] Columns =
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100,
            format: value => ((decimal)value).ToString("0.0", CultureInfo.InvariantCulture)),
    ];

    private static TestRow With(TestRow row, decimal amount) => new() { Book = row.Book, Amount = amount, AsOf = row.AsOf, Active = row.Active };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(IGridSource<TestRow> source)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 200));

    private static string AmountText(IRenderedComponent<ExGrid<TestRow>> cut, string book)
        => cut.FindComponents<ExGridRow<TestRow>>().Single(r => r.Instance.Row.Book == book)
            .FindAll(".ex-cell")[1].TextContent;

    private static Dictionary<string, int> RowRenders(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(r => r.Instance.Row.Book, r => r.RenderCount);

    [Fact] // ADR-0141/0142 / LV-16: PublishGathered on the grid's context puts out the gathered change; only the row it replaced renders, once
    public async Task PublishGathered_repaints_only_the_rows_it_replaced()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        var cut = RenderGrid(source);
        source.Apply(new(changed: [With(rows[2], 1)]));
        cut.WaitForAssertion(() => Assert.Equal("1.0", AmountText(cut, "Gamma")));
        // Within the interval: gathered, and not on screen.
        source.Apply(new(changed: [With(rows[0], 5)]));
        Assert.Equal("100.5", AmountText(cut, "Alpha"));
        var before = RowRenders(cut);

        // As the grid calls it before it judges a write: on its own context.
        await cut.InvokeAsync(source.PublishGathered);

        cut.WaitForAssertion(() => Assert.Equal("5.0", AmountText(cut, "Alpha")));
        var after = RowRenders(cut);
        Assert.Equal(before["Alpha"] + 1, after["Alpha"]);
        Assert.Equal(before["Beta"], after["Beta"]);
        Assert.Equal(before["Gamma"], after["Gamma"]);
    }

    [Fact] // ADR-0141 / LV-16: a second grid bound to the same source hears the publication another grid asked for
    public async Task A_second_grid_hears_the_publication()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        var first = RenderGrid(source);
        var second = RenderGrid(source);
        source.Apply(new(changed: [With(rows[2], 1)]));
        source.Apply(new(changed: [With(rows[1], 2)]));

        await first.InvokeAsync(source.PublishGathered);

        second.WaitForAssertion(() => Assert.Equal("2.0", AmountText(second, "Beta")));
        // Nothing is left for the interval's end.
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("2.0", AmountText(second, "Beta"));
    }
}
