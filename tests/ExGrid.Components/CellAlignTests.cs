using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The per-cell alignment reaching the DOM (ADR-0050, item 7; DC-29), and the price of the
/// rule that gets it there: the lookup's identity is the change signal (ADR-0003/0006).
/// </summary>
public class CellAlignTests : GridTestContext
{
    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
        new("Active", ColumnType.Boolean, r => r.Active, align: CellAlign.Right),
    ];

    // Alpha's cells are centred; every other cell answers Auto.
    private static Func<TestRow, GridColumn<TestRow>, CellAlign> CentreRow(string book)
        => (row, _) => row.Book == book ? CellAlign.Center : CellAlign.Auto;

    private static IReadOnlyList<AngleSharp.Dom.IElement> CellsOf(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-cell");

    [Fact] // ADR-0050 item 7 / DC-29: a cell with an alignment of its own is painted with it
    public void A_cell_is_painted_with_its_own_alignment()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.CellAlign, CentreRow("Alpha")));

        var cells = CellsOf(cut);
        for (var column = 0; column < 3; column++)
        {
            Assert.Contains("ex-align-center", cells[column].ClassName);
        }
        // The kind still decides the numeric class; only the alignment moved.
        Assert.Contains("ex-cell-numeric", cells[1].ClassName);
    }

    [Fact] // ADR-0050 item 7 / DC-29: Auto from the lookup leaves the column's, then the kind's
    public void Auto_leaves_the_column_and_then_the_kind()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.CellAlign, CentreRow("Alpha")));

        var beta = CellsOf(cut).Skip(3).Take(3).ToArray();
        Assert.DoesNotContain("ex-align-", beta[0].ClassName);
        Assert.DoesNotContain("ex-align-", beta[1].ClassName);
        Assert.Contains("ex-cell-numeric", beta[1].ClassName);
        Assert.Contains("ex-align-right", beta[2].ClassName);
    }

    [Fact] // ADR-0050 item 7 / DC-1: without the lookup every cell is painted exactly as before
    public void Without_a_lookup_nothing_changes()
    {
        var with = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.CellAlign, (Func<TestRow, GridColumn<TestRow>, CellAlign>)((_, _) => CellAlign.Auto)));
        var without = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns()));

        Assert.Equal(
            CellsOf(without).Select(c => c.ClassName),
            CellsOf(with).Select(c => c.ClassName));
        Assert.DoesNotContain(CellsOf(without), c => c.ClassName!.Contains("ex-align-center"));
    }

    [Fact] // ADR-0050 item 7: a Pinned Column's cell follows its alignment too
    public void A_pinned_cell_follows_its_alignment()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.CellAlign, CentreRow("Alpha")));

        Assert.Contains("ex-align-center", cut.FindAll(".ex-cell.ex-pinned")[0].ClassName);
    }

    [Fact] // ADR-0050 item 7 / ADR-0003: a lookup held in a field lets every row skip its render
    public void A_held_lookup_keeps_row_memoisation()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var aligns = CentreRow("Alpha");
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellAlign, aligns));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellAlign, aligns));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0050 item 7 / ADR-0006: a new lookup is the change signal, and it repaints
    public void Handing_over_a_new_lookup_repaints_the_alignments()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellAlign, CentreRow("Alpha")));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellAlign, CentreRow("Beta")));

        var cells = CellsOf(cut);
        Assert.DoesNotContain("ex-align-center", cells[0].ClassName);
        Assert.Contains("ex-align-center", cells[3].ClassName);
    }
}
