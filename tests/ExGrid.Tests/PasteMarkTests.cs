using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A cell's mark in the family's own HTML (<see cref="ClipboardData.MarkAttribute"/>, ADR-0050 item 9):
/// handed to the Consumer beside the field, through the plan's tiling, and read from no other table.
/// ExSheet marks a refused cell with it (ADR-0047, 2026-10-12).
/// </summary>
public class PasteMarkTests
{
    private static GridPasteIntent IntentFor(string html, SelectionRange target)
    {
        var block = ClipboardParse.ParseBlock(html, null)!;
        var grid = new GridExtent(100, 10);
        var selection = GridSelection.Empty.Click(new(target.TopRow, target.LeftColumn), grid)
            .ExtendTo(new(target.BottomRow, target.RightColumn), grid);
        var plan = ClipboardRules.PlanPaste(selection, new PasteShape(block.Values.Count, block.Values[0].Count), _ => true);
        Assert.False(plan.IsRefused);
        return new GridPasteIntent(plan.Plan, block.Values, 0, block.Origins, marks: block.Marks);
    }

    [Fact] // ADR-0050 item 9: a mark in the family's own table is handed over beside its field, the field unchanged
    public void ADR0050_a_mark_in_the_familys_table_is_handed_over_beside_its_field()
    {
        var intent = IntentFor(
            "<table data-ex-grid=\"invariant\"><tr><td>1</td><td data-ex-mark=\"exsheet-refused:C1\">#VALUE!</td></tr></table>",
            new SelectionRange(4, 4, 1, 2));

        Assert.Null(intent.MarkFor(new CellPosition(4, 4)));
        Assert.Equal("exsheet-refused:C1", intent.MarkFor(new CellPosition(4, 5)));
        Assert.Equal("#VALUE!", intent.ValueFor(new CellPosition(4, 5)));
    }

    [Fact] // ADR-0050 item 9: a single marked field repeated over a range carries its mark to every cell
    public void ADR0050_a_mark_follows_the_tiling()
    {
        var intent = IntentFor(
            "<table data-ex-grid=\"invariant\"><tr><td data-ex-mark=\"m\">#VALUE!</td></tr></table>",
            new SelectionRange(0, 0, 3, 1));

        Assert.Equal("m", intent.MarkFor(new CellPosition(2, 0)));
    }

    [Fact] // ADR-0050 item 9: another program's table is not the family's, and no mark is read from it
    public void ADR0050_no_mark_is_read_from_another_programs_table()
    {
        var intent = IntentFor(
            "<table><tr><td data-ex-mark=\"exsheet-refused:C1\">#VALUE!</td></tr></table>",
            new SelectionRange(0, 0, 1, 1));

        Assert.Null(intent.MarkFor(new CellPosition(0, 0)));
    }
}
