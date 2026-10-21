using Bunit;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// What a selected cell of the report stands for (ADR-0340, Q5): the Items its row's path and its
/// column's path name, as a Board's link from the pivot reads them.
/// </summary>
public class CellItemsTests : PivotTestContext
{
    // East, North, West, (blank), Grand Total; columns FALSE, TRUE, Grand Total after the label.
    private static readonly PivotLayout ByOnline = new() { Rows = [P("Region")], Columns = [P("Online")], Values = [Sum("Amount")] };

    private static string Text(PivotCellItems cell) => string.Join(" ", cell.Items);

    private static GridSelection Select(SelectionRange range)
        => GridSelection.Empty.Place(range, new CellPosition(range.TopRow, range.LeftColumn), new GridExtent(100, 10));

    [Fact] // ADR-0340 Q5: a value cell names its row's and its column's Items; a total names fewer
    public void ADR0340_each_cell_names_the_items_of_its_row_and_column()
    {
        var cut = RenderPivot(ByOnline);
        var pivot = cut.Instance;

        var cells = pivot.ItemsOf(Select(new SelectionRange(0, 1, 1, 2)))!;
        Assert.Equal(["Region=Text:East Online=Boolean:FALSE", "Region=Text:East Online=Boolean:TRUE"], cells.Select(Text));

        // A label cell names its row only; the grand total row and column name nothing.
        Assert.Equal(["Region=Text:North"], pivot.ItemsOf(Select(new SelectionRange(1, 0, 1, 1)))!.Select(Text));
        Assert.Equal([""], pivot.ItemsOf(Select(new SelectionRange(4, 3, 1, 1)))!.Select(Text));
        Assert.Equal(["Region=Blank"], pivot.ItemsOf(Select(new SelectionRange(3, 3, 1, 1)))!.Select(Text));
        Assert.Empty(pivot.ItemsOf(GridSelection.Empty)!);
    }

    [Fact] // ADR-0340 Q5 / spine rule 1: a selection reaching past the report names nothing, not part of it
    public void ADR0340_a_selection_past_the_report_names_nothing()
    {
        var cut = RenderPivot(ByOnline);

        Assert.Null(cut.Instance.ItemsOf(Select(new SelectionRange(0, 1, 50, 1))));
        Assert.Null(cut.Instance.ItemsOf(Select(new SelectionRange(0, 4, 1, 1))));
    }

    [Fact] // ADR-0340 Q5: in the tabular form an outer field's label stands for its own Item, not the row's leaf
    public void ADR0340_a_tabular_label_stands_for_its_own_field()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")], Form = PivotReportForm.Tabular });
        var cells = cut.Instance.ItemsOf(Select(new SelectionRange(0, 0, 1, 3)))!.Select(Text).ToArray();

        Assert.Equal(3, cells.Length);
        Assert.Equal("Region=Text:East", cells[0]);
        Assert.StartsWith("Region=Text:East Product=Text:", cells[1], StringComparison.Ordinal);
        Assert.Equal(cells[1], cells[2]);
    }
}
