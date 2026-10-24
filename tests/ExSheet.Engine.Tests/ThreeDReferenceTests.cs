using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// 3D References (ADR-0331 §7, ticket 11 of exsheet-workbook): <c>Sheet1:Sheet3!B2</c> reads the same range on
/// every Sheet from the first named to the last in tab order. <see cref="ThreeDExcelReadingsTests"/> holds every
/// case ticket 12 put to Excel; these are the engine's own around them: recalculation, undo, documents.
/// </summary>
public class ThreeDReferenceTests
{
    private static Workbook NewWorkbook(params string[] names) => new(EnUs, names);

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))?.Formula;

    private static string[] Names(Workbook workbook) => [.. workbook.Sheets.Select(s => s.Name)];

    /// <summary>A Workbook of Sheet1 to Sheet4 and Summary, with 1, 2, 4 and 8 in B2 of Sheet1 to Sheet4.</summary>
    private static Workbook Quarters()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2", "Sheet3", "Sheet4", "Summary");
        Enter(workbook, "Sheet1", "B2", "1");
        Enter(workbook, "Sheet2", "B2", "2");
        Enter(workbook, "Sheet3", "B2", "4");
        Enter(workbook, "Sheet4", "B2", "8");
        return workbook;
    }

    private static Sheet Summary(Workbook workbook) => workbook.Find("Summary")!;

    [Fact] // ADR-0331 §7: SUM over Sheet1:Sheet3 adds B2 of each Sheet from the first to the last in tab order
    public void A_3d_reference_reads_every_sheet_of_its_span()
    {
        var workbook = Quarters();

        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");
        Enter(workbook, "Summary", "A2", "=SUM(Sheet2:Sheet4!B2,100)");
        Enter(workbook, "Summary", "A3", "=SUM(Sheet3:Sheet1!B2)");
        Enter(workbook, "Summary", "A4", "=SUM(Sheet2:Sheet2!B2)");

        var summary = Summary(workbook);
        Assert.Equal(7, summary.Number("A1"));
        Assert.Equal(114, summary.Number("A2"));
        // Named last to first, the span is the same Sheets.
        Assert.Equal(7, summary.Number("A3"));
        Assert.Equal(2, summary.Number("A4"));
    }

    [Fact] // ADR-0331 §7: a rectangle, whole columns and a span that holds the Formula's own Sheet
    public void A_3d_reference_reads_a_rectangle_on_each_sheet()
    {
        var workbook = Quarters();
        Enter(workbook, "Sheet1", "C3", "10");
        Enter(workbook, "Sheet3", "B3", "20");
        Enter(workbook, "Sheet2", "A1", "=SUM(Sheet1:Sheet3!B2:C3)");
        Enter(workbook, "Sheet4", "A1", "=COUNT(Sheet1:Sheet3!B:C)");

        Assert.Equal(37, workbook.Sheets[1].Number("A1"));
        Assert.Equal(5, workbook.Sheets[3].Number("A1"));
    }

    [Fact] // ADR-0331 §7, ticket 12: the functions Excel reads one in across its span
    public void The_functions_excel_reads_one_in_take_a_3d_reference()
    {
        var workbook = Quarters();
        Enter(workbook, "Sheet2", "C2", "text");
        var summary = Summary(workbook);

        Enter(workbook, "Summary", "A1", "=AVERAGE(Sheet1:Sheet4!B2)");
        Enter(workbook, "Summary", "A2", "=MIN(Sheet1:Sheet4!B2)");
        Enter(workbook, "Summary", "A3", "=MAX(Sheet1:Sheet4!B2)");
        Enter(workbook, "Summary", "A4", "=COUNT(Sheet1:Sheet4!B2:C2)");
        Enter(workbook, "Summary", "A5", "=COUNTA(Sheet1:Sheet4!B2:C2)");
        Enter(workbook, "Summary", "A6", "=PRODUCT(Sheet1:Sheet4!B2)");
        Enter(workbook, "Summary", "A7", "=VAR.P(Sheet1:Sheet2!B2)");
        Enter(workbook, "Summary", "A8", "=VAR.S(Sheet1:Sheet2!B2)");
        Enter(workbook, "Summary", "A9", "=STDEV.P(Sheet1:Sheet2!B2)");
        Enter(workbook, "Summary", "A10", "=STDEV.S(Sheet1:Sheet2!B2)");

        Assert.Equal(3.75, summary.Number("A1"));
        Assert.Equal(1, summary.Number("A2"));
        Assert.Equal(8, summary.Number("A3"));
        Assert.Equal(4, summary.Number("A4"));
        Assert.Equal(5, summary.Number("A5"));
        Assert.Equal(64, summary.Number("A6"));
        Assert.Equal(0.25, summary.Number("A7"));
        Assert.Equal(0.5, summary.Number("A8"));
        Assert.Equal(0.5, summary.Number("A9"));
        Assert.Equal(Math.Sqrt(0.5), summary.Number("A10"), 15);
    }

    [Fact] // ADR-0331 §7: an Error Value on any Sheet of the span is the aggregate's, as in a range of one Sheet
    public void An_error_on_one_sheet_of_the_span_is_the_result()
    {
        var workbook = Quarters();
        Enter(workbook, "Sheet3", "B2", "=1/0");
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet4!B2)");
        Enter(workbook, "Summary", "A2", "=COUNTA(Sheet1:Sheet4!B2)");

        Assert.Equal(ErrorValue.Div0, Summary(workbook).Error("A1"));
        Assert.Equal(4, Summary(workbook).Number("A2"));
    }

    [Fact] // ADR-0331 §7: a change on any Sheet of the span recalculates the Formula, and only a Sheet of it does
    public void A_change_on_a_sheet_of_the_span_recalculates()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");
        Enter(workbook, "Sheet2", "B2", "20");
        Assert.Equal(25, Summary(workbook).Number("A1"));

        Enter(workbook, "Sheet4", "B2", "1000");
        Assert.Equal(25, Summary(workbook).Number("A1"));

        // A Formula on a Sheet of its own span reads the others' cells, and a cycle through its own is #CIRC!.
        Enter(workbook, "Sheet1", "A1", "=SUM(Sheet1:Sheet3!B2)");
        Assert.Equal(25, workbook.Sheets[0].Number("A1"));
        Enter(workbook, "Sheet2", "A1", "=SUM(Sheet1:Sheet3!A1)");
        Assert.Equal(ErrorValue.Circ, workbook.Sheets[1].Error("A1"));
    }

    [Fact] // ADR-0331 §7, ticket 12: a span naming a Sheet the Workbook does not have is #NAME?, and resolves once it has one
    public void A_span_naming_a_missing_sheet_is_name()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet9!B2)");
        Assert.Equal(ErrorValue.Name, Summary(workbook).Error("A1"));
        Assert.False(Summary(workbook).Value("A1")!.Value.IsRefused);

        workbook.Do("Sheet4", SheetEdit.Rename("Sheet9"));
        Assert.Equal(15, Summary(workbook).Number("A1"));
    }

    [Fact] // ADR-0331 §7, ticket 12's answer 6: written as Excel writes it: the ends in tab order and their own names, bare or quoted whole, one Sheet as that Sheet
    public void A_3d_reference_is_written_as_excel_writes_it()
    {
        var workbook = NewWorkbook("Sheet1", "Q1 Sales", "Sheet3");
        Enter(workbook, "Sheet1", "A1", "=sum('sheet3:q1 sales'!b2)");
        Enter(workbook, "Sheet1", "A2", "=SUM('Q1 Sales:Sheet3'!$B$2:C3)");
        Enter(workbook, "Sheet1", "A3", "=SUM(sheet3:Sheet1!B2)");
        Enter(workbook, "Sheet1", "A4", "=SUM('Sheet1:Sheet3'!B2)");
        Enter(workbook, "Sheet1", "A5", "=SUM(sheet3:Sheet3!B2)");
        Enter(workbook, "Sheet1", "A6", "=SUM(Sheet1:Nope!B2)");

        var sheet = workbook.Sheets[0];
        Assert.Equal("=SUM('Q1 Sales:Sheet3'!B2)", Formula(sheet, "A1"));
        Assert.Equal("=SUM('Q1 Sales:Sheet3'!$B$2:C3)", Formula(sheet, "A2"));
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(sheet, "A3"));
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(sheet, "A4"));
        Assert.Equal("=SUM(Sheet3!B2)", Formula(sheet, "A5"));
        // A name no Sheet has is left as typed: it is #NAME?.
        Assert.Equal("=SUM(Sheet1:Nope!B2)", Formula(sheet, "A6"));
    }

    [Fact] // ADR-0331 §7, ticket 12's answer 6: a Formula entered into a General cell takes no format from a 3D Reference, typed as one Sheet's or not
    public void A_3d_reference_gives_no_format()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2", "Sheet3");
        foreach (var name in new[] { "Sheet2", "Sheet3" })
        {
            workbook.Do(name, SheetEdit.SetNumberFormat(CellRange.Parse("B2"), NumberFormat.Parse("0%")));
            Enter(workbook, name, "B2", "50%");
        }

        Enter(workbook, "Sheet1", "A1", "=SUM(Sheet2:Sheet3!B2)");
        Enter(workbook, "Sheet1", "A2", "=SUM(Sheet2:Sheet2!B2)");
        Enter(workbook, "Sheet1", "A3", "=SUM(Sheet2!B2)");

        var sheet = workbook.Sheets[0];
        Assert.True(sheet.GetNumberFormat(CellAddress.Parse("A1")).IsGeneral);
        Assert.Equal("=SUM(Sheet2!B2)", Formula(sheet, "A2"));
        Assert.True(sheet.GetNumberFormat(CellAddress.Parse("A2")).IsGeneral);
        Assert.True(sheet.GetNumberFormat(CellAddress.Parse("A3")).IsPercent);
    }

    [Fact] // ADR-0331 §7, ADR-0048: a copy shifts a 3D Reference's relative parts, as any Reference's
    public void A_copy_shifts_a_3d_reference()
    {
        var workbook = Quarters();
        Enter(workbook, "Sheet1", "B3", "100");
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");

        workbook.Do("Summary", SheetEdit.Fill(CellRange.Parse("A1"), CellRange.Parse("A1:A2"), FillDirection.Down));

        Assert.Equal("=SUM(Sheet1:Sheet3!B3)", Formula(Summary(workbook), "A2"));
        Assert.Equal(100, Summary(workbook).Number("A2"));
    }

    [Fact] // ADR-0331 §3, §7: a rename rewrites either end of a span, and undoes exactly
    public void A_rename_rewrites_the_ends_of_a_span()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(sheet1:Sheet3!B2)*2");

        var first = workbook.Do("Sheet1", SheetEdit.Rename("Q1 Sales"));
        workbook.Do("Sheet3", SheetEdit.Rename("Q3"));

        Assert.Equal("=SUM('Q1 Sales:Q3'!B2)*2", Formula(Summary(workbook), "A1"));
        Assert.Equal(14, Summary(workbook).Number("A1"));

        // Renaming a Sheet inside the span changes nothing it holds.
        workbook.Do("Sheet2", SheetEdit.Rename("Q2"));
        Assert.Equal("=SUM('Q1 Sales:Q3'!B2)*2", Formula(Summary(workbook), "A1"));
    }

    [Fact] // ADR-0331 §7: a Workbook Document keeps a 3D Reference, and the Workbook it opens reads it
    public void A_3d_reference_round_trips_through_the_document()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM('Sheet1:Sheet3'!B2)");

        var reopened = Workbook.Open(workbook.ToDocument());

        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(reopened.Find("Summary")!, "A1"));
        Assert.Equal(7, reopened.Find("Summary")!.Number("A1"));
    }

    [Fact] // ADR-0331 §3, §7, ticket 12's answer 2: an end moved past the other hands its end inward, and the undo puts the text back
    public void Moving_an_end_past_the_other_rewrites_the_span_and_undoes()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");

        var moved = workbook.Do("Sheet1", SheetEdit.MoveSheet(3));
        Assert.Equal(["Sheet2", "Sheet3", "Sheet4", "Sheet1", "Summary"], Names(workbook));
        Assert.Equal("=SUM(Sheet2:Sheet3!B2)", Formula(Summary(workbook), "A1"));
        Assert.Equal(6, Summary(workbook).Number("A1"));

        moved.Undo();
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(Summary(workbook), "A1"));
        Assert.Equal(7, Summary(workbook).Number("A1"));
        moved.Redo();
        Assert.Equal("=SUM(Sheet2:Sheet3!B2)", Formula(Summary(workbook), "A1"));
    }

    [Fact] // ADR-0331 §3, §7, ticket 12's answer 2: deleting an end hands it inward, down to one Sheet, then #REF!; each undoes
    public void Deleting_the_ends_of_a_span_narrows_it_and_undoes()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet2!B2)*2");

        var first = workbook.Do("Sheet1", SheetEdit.DeleteSheet());
        Assert.Equal("=SUM(Sheet2!B2)*2", Formula(Summary(workbook), "A1"));
        Assert.Equal(4, Summary(workbook).Number("A1"));
        var second = workbook.Do("Sheet2", SheetEdit.DeleteSheet());
        Assert.Equal("=SUM(#REF!)*2", Formula(Summary(workbook), "A1"));

        second.Undo();
        first.Undo();
        Assert.Equal("=SUM(Sheet1:Sheet2!B2)*2", Formula(Summary(workbook), "A1"));
        Assert.Equal(6, Summary(workbook).Number("A1"));
    }

    [Fact] // ADR-0331 §7, ticket 12's answer 4: a row inserted on a Sheet of a span recalculates what reads it, the text kept; the undo too
    public void Rows_inserted_under_a_span_are_read_where_they_now_are()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");
        Enter(workbook, "Sheet2", "A1", "=SUM(Sheet1:Sheet3!B2)");

        var inserted = workbook.Do("Sheet2", SheetEdit.InsertRows(0));
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(Summary(workbook), "A1"));
        Assert.Equal(5, Summary(workbook).Number("A1"));
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(workbook.Sheets[1], "A2"));
        Assert.Equal(5, workbook.Sheets[1].Number("A2"));

        inserted.Undo();
        Assert.Equal(7, Summary(workbook).Number("A1"));
        Assert.Equal(7, workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331 §7, ticket 12's answer 2: a Sheet inserted between the ends is read, and so is one moved there, with no rewrite
    public void A_sheet_between_the_ends_is_read()
    {
        var workbook = Quarters();
        Enter(workbook, "Summary", "A1", "=SUM(Sheet1:Sheet3!B2)");

        var inserted = workbook.Do("Sheet1", SheetEdit.InsertSheet("New"));
        Enter(workbook, "New", "B2", "100");
        Assert.Equal(107, Summary(workbook).Number("A1"));
        workbook.Do("Sheet4", SheetEdit.MoveSheet(2));
        Assert.Equal(115, Summary(workbook).Number("A1"));
        Assert.Equal("=SUM(Sheet1:Sheet3!B2)", Formula(Summary(workbook), "A1"));
        Assert.Equal(["Sheet1", "New", "Sheet4", "Sheet2", "Sheet3", "Summary"], Names(workbook));
        _ = inserted;
    }
}
