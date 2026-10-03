using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Consumer's painted text reaching the DOM (ADR-0050, item 11; DC-35): painted, still
/// under the <c>####</c> rule (ADR-0016), named by the value's own text where it differs
/// (ADR-0016/0033), and held so rows keep skipping their render (ADR-0003/0006).
/// </summary>
public class PaintedTextTests : GridTestContext
{
    private static GridColumn<TestRow>[] Columns(double amountWidthPx = 120) =>
    [
        new("Book", ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
        new("Amount", ColumnType.Number, r => r.Amount,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(amountWidthPx))),
    ];

    // The named book's Amount is painted as "~" and its whole part; every other cell answers null.
    private static PaintedTextOf<TestRow> Tilde(string book)
        => (row, column, _, _) => row.Book == book && column.Name == "Amount" ? "~" + (int)row.Amount : null;

    private static IReadOnlyList<AngleSharp.Dom.IElement> CellsOf(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-cell");

    [Fact] // ADR-0050 item 11 / DC-35: the painted text is painted, and the value's text names the cell
    public void The_painted_text_is_painted_and_the_value_is_its_accessible_name()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PaintedText, Tilde("Alpha")));

        var amount = CellsOf(cut)[1];
        Assert.Equal("~100", amount.TextContent);
        Assert.Equal("100.5", amount.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11 / ADR-0016: a null answer paints the value's text, which names itself
    public void A_null_answer_paints_the_value_and_adds_no_label()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PaintedText, Tilde("Alpha")));

        var beta = CellsOf(cut).Skip(2).Take(2).ToArray();
        Assert.Equal("Beta", beta[0].TextContent);
        Assert.Equal("-7", beta[1].TextContent);
        Assert.Null(beta[0].GetAttribute("aria-label"));
        Assert.Null(beta[1].GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11 / ADR-0033: an answer equal to the value's text needs no separate name
    public void An_answer_equal_to_the_value_adds_no_label()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((row, _, _, _) => row.Book == "Alpha" ? "Alpha" : null)));

        var alpha = CellsOf(cut)[0];
        Assert.Equal("Alpha", alpha.TextContent);
        Assert.Null(alpha.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11 / DC-35: the delegate is given the column's resolved content width and the metrics
    public void The_delegate_is_given_the_content_width_and_the_metrics()
    {
        var asked = new List<(string Column, double ContentWidthPx, CellTextMetrics Metrics)>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns(amountWidthPx: 90))
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((_, column, width, metrics) =>
            {
                asked.Add((column.Name, width, metrics));
                return null;
            })));

        Assert.NotEmpty(asked);
        var (_, bookWidth, metrics) = asked.Last(a => a.Column == "Book");
        var (_, amountWidth, _) = asked.Last(a => a.Column == "Amount");
        Assert.Equal(cut.Instance.CellMetrics, metrics);
        Assert.Equal(metrics.ContentWidthPx(120), bookWidth);
        Assert.Equal(metrics.ContentWidthPx(90), amountWidth);
    }

    [Fact] // ADR-0050 item 11 / ADR-0016: the #### rule decides on what is painted for a Number
    public void A_painted_number_that_does_not_fit_becomes_hashes()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns(amountWidthPx: 45))
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((row, column, _, _) =>
                row.Book == "Alpha" && column.Name == "Amount" ? "123456789012" : null)));

        var amount = CellsOf(cut)[1];
        Assert.StartsWith("#", amount.TextContent);
        Assert.DoesNotContain("1", amount.TextContent);
        // Behind ####, the name is still the value's own text, not the painted answer.
        Assert.Equal("100.5", amount.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11 / ADR-0016: a fitted text is painted where the value itself would hash
    public void A_fitted_text_is_painted_where_the_value_would_hash()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(70))),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, (TestRow[])[new() { Amount = 1234567890123m }])
            .Add(g => g.Columns, columns)
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((_, _, _, _) => "1E+8")));

        var cell = cut.Find(".ex-cell");
        Assert.Equal("1E+8", cell.TextContent);
        Assert.Equal("1234567890123", cell.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11: a Pinned Column's cell paints its painted text too
    public void A_pinned_cell_paints_its_painted_text()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PinnedColumnCount, 2)
            .Add(g => g.PaintedText, Tilde("Alpha")));

        var pinned = cut.FindAll(".ex-cell.ex-pinned");
        Assert.Equal("~100", pinned[1].TextContent);
        Assert.Equal("100.5", pinned[1].GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11 / DC-1: without the delegate every cell is painted exactly as before
    public void Without_a_delegate_nothing_changes()
    {
        var with = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((_, _, _, _) => null)));
        var without = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns()));

        Assert.Equal(
            CellsOf(without).Select(c => (c.TextContent, c.GetAttribute("aria-label"))),
            CellsOf(with).Select(c => (c.TextContent, c.GetAttribute("aria-label"))));
        Assert.Equal(["Alpha", "100.5"], CellsOf(without).Take(2).Select(c => c.TextContent));
    }

    [Fact] // ADR-0050 item 11 / ADR-0003: a delegate held in a field lets every row skip its render
    public void A_held_delegate_keeps_row_memoisation()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var painted = Tilde("Alpha");
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.PaintedText, painted));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.PaintedText, painted));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0050 item 11 / ADR-0006: a new delegate is the change signal, and it repaints
    public void Handing_over_a_new_delegate_repaints_the_texts()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.PaintedText, Tilde("Alpha")));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.PaintedText, Tilde("Beta")));

        var cells = CellsOf(cut);
        Assert.Equal("100.5", cells[1].TextContent);
        Assert.Null(cells[1].GetAttribute("aria-label"));
        Assert.Equal("~-7", cells[3].TextContent);
        Assert.Equal("-7", cells[3].GetAttribute("aria-label"));
    }
}
