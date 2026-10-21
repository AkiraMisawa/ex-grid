using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The ranges a step changed (ADR-0331 §3; exsheet-workbook ticket 08): what an undo or a redo of it
/// selects, as Excel's do, each operation reporting the cells it wrote, the ranges it formatted, or the
/// whole rows and columns it inserted or deleted. An operation on the Sheets themselves reports none.
/// </summary>
public class WorkbookStepRangesTests
{
    private static CellRange R(string text) => CellRange.Parse(text);

    private static IReadOnlyList<CellRange> RangesOf(SheetEdit edit, Workbook? workbook = null)
    {
        workbook ??= new Workbook(EnUs, ["One", "Two"]);
        return workbook.Do("One", edit).Ranges;
    }

    [Fact] // ADR-0331 §3: an entry's step names the cell it wrote
    public void ADR0331_an_entry_names_its_cell() =>
        Assert.Equal([R("C4")], RangesOf(SheetEdit.Enter(CellAddress.Parse("C4"), "7")));

    [Fact] // ADR-0331 §3: cells named one by one name the smallest range that holds them
    public void ADR0331_cells_named_one_by_one_name_the_range_that_holds_them() =>
        Assert.Equal([R("B2:D5")], RangesOf(SheetEdit.SetEntries([new(CellAddress.Parse("D2"), null), new(CellAddress.Parse("B5"), null)])));

    [Fact] // ADR-0331 §3: Ctrl+Enter names its ranges, the one it was typed in first
    public void ADR0331_ctrl_enter_names_its_ranges_the_typed_one_first() =>
        Assert.Equal([R("E5:F6"), R("A1:B2")], RangesOf(SheetEdit.EnterInto([R("A1:B2"), R("E5:F6")], CellAddress.Parse("E5"), "1")));

    [Fact] // ADR-0331 §3: a paste names the cells it wrote: from its origin the block's size, or each target
    public void ADR0331_a_paste_names_the_cells_it_wrote()
    {
        var workbook = new Workbook(EnUs, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "1"));
        var block = workbook.Sheets[0].Copy(R("A1:B3")).Block!;

        Assert.Equal([R("D4:E6")], RangesOf(SheetEdit.Paste(block, CellAddress.Parse("D4")), workbook));
        Assert.Equal([R("D4:E9")], RangesOf(SheetEdit.Paste(block, R("D4:E9")), workbook));
        Assert.Equal([R("G1:H3"), R("J1:K3")], RangesOf(SheetEdit.Paste(block, [R("G1:H3"), R("J1:K3")]), workbook));
        Assert.Equal([R("C2:D3")], RangesOf(SheetEdit.PasteText([["a", "b"], ["c", "d"]], CellAddress.Parse("C2")), workbook));
    }

    [Fact] // ADR-0331 §3: a fill names its source with what it filled
    public void ADR0331_a_fill_names_its_source_and_what_it_filled()
    {
        var workbook = new Workbook(EnUs, ["One"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "x"));

        Assert.Equal([R("A1:A4")], workbook.Do("One", SheetEdit.Fill(R("A1"), R("A2:A4"), FillDirection.Down)).Ranges);
        Assert.Equal([R("A1:C1")], workbook.Do("One", SheetEdit.FillCopy(R("A1"), R("B1:C1"), FillDirection.Right)).Ranges);
    }

    [Fact] // ADR-0331 §3: a Cell Format names the ranges it was set on, whole columns among them
    public void ADR0331_a_cell_format_names_its_ranges() =>
        Assert.Equal([R("B2:C3"), CellRange.WholeColumns(4, 5)],
            RangesOf(SheetEdit.SetCellFormat([R("B2:C3"), CellRange.WholeColumns(4, 5)], new CellFormatChange { Bold = true })));

    [Fact] // ADR-0331 §3: a width names the whole columns it was set on
    public void ADR0331_a_width_names_its_columns() =>
        Assert.Equal([CellRange.WholeColumns(1, 2)], RangesOf(SheetEdit.SetColumnWidth(CellRange.WholeColumns(1, 2), 20)));

    [Fact] // ADR-0331 §3: an insertion or deletion names the whole rows or columns inserted or deleted
    public void ADR0331_an_insertion_or_deletion_names_its_rows_or_columns()
    {
        Assert.Equal([CellRange.WholeRows(2, 3)], RangesOf(SheetEdit.InsertRows(2, 2)));
        Assert.Equal([CellRange.WholeRows(4, 4)], RangesOf(SheetEdit.DeleteRows(4)));
        Assert.Equal([CellRange.WholeColumns(1, 3)], RangesOf(SheetEdit.InsertColumns(1, 3)));
        Assert.Equal([CellRange.WholeColumns(0, 0)], RangesOf(SheetEdit.DeleteColumns(0)));
    }

    [Fact] // ADR-0331 §3: an operation on the Sheets themselves names no range
    public void ADR0331_a_sheet_operation_names_no_range()
    {
        Assert.Empty(RangesOf(SheetEdit.InsertSheet()));
        Assert.Empty(RangesOf(SheetEdit.CopySheet()));
        Assert.Empty(RangesOf(SheetEdit.MoveSheet(1)));
        Assert.Empty(RangesOf(SheetEdit.Rename("First")));
        Assert.Empty(RangesOf(SheetEdit.DeleteSheet()));
    }
}
