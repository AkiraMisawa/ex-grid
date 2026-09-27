using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The per-cell kind reaching the DOM (ADR-0050, item 6; DC-26), and the price of the rule
/// that gets it there: the lookup's identity is the change signal (ADR-0003/0006).
/// </summary>
public class CellTypeTests : GridTestContext
{
    private const string Heading = "A long heading that cannot fit";

    // Alpha's cell is a heading in a Number column; Beta's is a number in a Text column.
    private static GridColumn<TestRow>[] MixedColumns() =>
    [
        new("Amount", ColumnType.Number, r => r.Book == "Alpha" ? Heading : r.Amount,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(60))),
        new("Label", ColumnType.Text, r => r.Book == "Beta" ? 123456789.25m : r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(60))),
    ];

    private static Func<TestRow, GridColumn<TestRow>, ColumnType> KindsFor(string textRow, string numberRow)
        => (row, column) =>
            column.Name == "Amount" && row.Book == textRow ? ColumnType.Text
            : column.Name == "Label" && row.Book == numberRow ? ColumnType.Number
            : column.Type;

    private static IReadOnlyList<AngleSharp.Dom.IElement> CellsOf(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-cell");

    [Fact] // ADR-0050 item 6 / DC-26: a text cell in a Number column is left-aligned and never ####
    public void A_text_cell_in_a_number_column_is_not_numeric_and_never_hashes()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, MixedColumns())
            .Add(g => g.CellType, KindsFor(textRow: "Alpha", numberRow: "Beta")));

        var heading = CellsOf(cut)[0];
        Assert.Equal(Heading, heading.TextContent);
        Assert.DoesNotContain("ex-cell-numeric", heading.ClassName);
        // Its neighbour below is still the column's kind.
        Assert.Contains("ex-cell-numeric", CellsOf(cut)[2].ClassName);
    }

    [Fact] // ADR-0050 item 6 / DC-26: a number in a Text column is right-aligned and becomes #### when it does not fit
    public void A_number_cell_in_a_text_column_is_numeric_and_hashes()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, MixedColumns())
            .Add(g => g.CellType, KindsFor(textRow: "Alpha", numberRow: "Beta")));

        var number = CellsOf(cut)[3];
        Assert.Contains("ex-cell-numeric", number.ClassName);
        Assert.NotEmpty(number.TextContent);
        Assert.DoesNotContain(number.TextContent, c => c != '#');
        // The accessible name of a #### cell is the real value (ADR-0016/0033).
        Assert.Equal("123456789.25", number.GetAttribute("aria-label"));
        // Its neighbours in the Text column are still text.
        Assert.DoesNotContain("ex-cell-numeric", CellsOf(cut)[1].ClassName);
    }

    [Fact] // ADR-0050 item 6 / DC-1: without the lookup the column's kind decides, exactly as before
    public void Without_a_lookup_each_cell_takes_its_columns_kind()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, MixedColumns()));

        var cells = CellsOf(cut);
        // The heading in the Number column is hashed as a Number would be...
        Assert.Contains("ex-cell-numeric", cells[0].ClassName);
        Assert.DoesNotContain(cells[0].TextContent, c => c != '#');
        // ...and the number in the Text column is left as text.
        Assert.DoesNotContain("ex-cell-numeric", cells[3].ClassName);
        Assert.Equal("123456789.25", cells[3].TextContent);
    }

    [Fact] // ADR-0050 item 6: an explicit column alignment still beats the kind's derivation (ADR-0016)
    public void An_explicit_alignment_still_wins()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount, align: CellAlign.Right),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.CellType, (Func<TestRow, GridColumn<TestRow>, ColumnType>)((_, _) => ColumnType.Text)));

        foreach (var cell in CellsOf(cut))
        {
            Assert.DoesNotContain("ex-cell-numeric", cell.ClassName);
            Assert.Contains("ex-align-right", cell.ClassName);
        }
    }

    [Fact] // ADR-0050 item 6: a Pinned Column's cell follows its kind too — it is a cell like any other
    public void A_pinned_cell_follows_its_kind()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, MixedColumns())
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.CellType, KindsFor(textRow: "Alpha", numberRow: "Beta")));

        var heading = cut.FindAll(".ex-cell.ex-pinned")[0];
        Assert.Equal(Heading, heading.TextContent);
        Assert.DoesNotContain("ex-cell-numeric", heading.ClassName);
    }

    [Fact] // ADR-0050 item 6 / ADR-0003: a lookup held in a field lets every row skip its render
    public void A_held_lookup_keeps_row_memoisation()
    {
        var rows = TestRows.Window();
        var columns = MixedColumns();
        var kinds = KindsFor(textRow: "Alpha", numberRow: "Beta");
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellType, kinds));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellType, kinds));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0050 item 6 / ADR-0006: a new lookup is the change signal, and it repaints
    public void Handing_over_a_new_lookup_repaints_the_kinds()
    {
        var rows = TestRows.Window();
        var columns = MixedColumns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellType, KindsFor(textRow: "Alpha", numberRow: "Beta")));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellType, KindsFor(textRow: "Nobody", numberRow: "Nobody")));

        var cells = CellsOf(cut);
        Assert.Contains("ex-cell-numeric", cells[0].ClassName);
        Assert.DoesNotContain("ex-cell-numeric", cells[3].ClassName);
    }
}
