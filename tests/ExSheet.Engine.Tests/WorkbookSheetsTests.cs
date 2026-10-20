using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class WorkbookSheetsTests
{
    private static Workbook NewWorkbook(params string[] names) => new(EnUs, names);

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))?.Formula;

    private static string[] Names(Workbook workbook) => [.. workbook.Sheets.Select(s => s.Name)];

    [Fact] // ADR-0331 §3, §4: a new Sheet takes the next SheetN, beside the Sheet the edit is done on
    public void A_new_sheet_takes_the_next_number()
    {
        var workbook = NewWorkbook("Sheet1", "Data", "Sheet3");

        var after = workbook.Do("Sheet1", SheetEdit.InsertSheet());
        var before = workbook.Do("Data", SheetEdit.InsertSheet(before: true));

        Assert.Equal(["Sheet1", "Sheet4", "Sheet5", "Data", "Sheet3"], Names(workbook));
        Assert.Equal("Sheet4", after.Sheet.Name);
        Assert.True(after.Change.ChangesSheets);
        Assert.Equal("Sheet5", before.Sheet.Name);
    }

    [Fact] // ADR-0331 §3: an insertion is a step; undone, the Sheet goes; redone, it comes back
    public void An_insertion_is_undone_and_redone()
    {
        var workbook = NewWorkbook("Sheet1");
        var step = workbook.Do("Sheet1", SheetEdit.InsertSheet("Notes"));
        Enter(workbook, "Sheet1", "A1", "=Notes!A1");

        // Undone in the reverse of the order done.
        var entry = workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A2"), "1"));
        entry.Undo();
        step.Undo();
        Assert.Equal(["Sheet1"], Names(workbook));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));

        step.Redo();
        Assert.Equal(["Sheet1", "Notes"], Names(workbook));
        Assert.Equal(0, workbook.Sheets[0].Number("A1"));
    }

    [Fact] // ADR-0331 §4: a name another Sheet has, without regard to case, is refused by name
    public void A_name_another_sheet_has_is_refused()
    {
        var workbook = NewWorkbook("Sheet1", "Data");

        var refusal = workbook.Check("Sheet1", SheetEdit.Rename("DATA"));
        Assert.Equal(SheetRefusalReason.SheetNameTaken, refusal!.Reason);
        Assert.Contains("'Data'", refusal.Message);
        Assert.Throws<SheetRefusedException>(() => workbook.Do("Sheet1", SheetEdit.Rename("data")));
        Assert.Throws<SheetRefusedException>(() => workbook.Do("Sheet1", SheetEdit.InsertSheet("DATA")));
        Assert.Throws<ArgumentException>(() => SheetEdit.InsertSheet("a/b"));
        Assert.Equal(["Sheet1", "Data"], Names(workbook));

        // Its own name in another case is a rename.
        workbook.Do("Data", SheetEdit.Rename("DATA"));
        Assert.Equal(["Sheet1", "DATA"], Names(workbook));
    }

    [Fact] // ADR-0331 §3: a rename rewrites every Formula that names the Sheet, on every Sheet, and undoes exactly
    public void A_rename_rewrites_every_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet2", "A1", "5");
        Enter(workbook, "Sheet1", "A1", "=sheet2!A1*2");
        Enter(workbook, "Sheet1", "A2", "=SUM(Sheet2!A1:A3)");
        Enter(workbook, "Sheet2", "B1", "=Sheet2!A1+1");

        var step = workbook.Do("Sheet2", SheetEdit.Rename("Q1 Sales"));

        Assert.Equal("='Q1 Sales'!A1*2", Formula(workbook.Sheets[0], "A1"));
        Assert.Equal("=SUM('Q1 Sales'!A1:A3)", Formula(workbook.Sheets[0], "A2"));
        Assert.Equal("='Q1 Sales'!A1+1", Formula(workbook.Sheets[1], "B1"));
        Assert.Equal(10, workbook.Sheets[0].Number("A1"));
        Assert.True(step.Change.ChangesSheets);
        Assert.Contains(0, step.Change.For(workbook.Sheets[0]).Rows);

        // Still followed under the new name.
        Enter(workbook, "Q1 Sales", "A1", "7");
        Assert.Equal(14, workbook.Sheets[0].Number("A1"));

        workbook.Do("Q1 Sales", SheetEdit.Enter(CellAddress.Parse("A1"), "5")); // put back before undoing the rename's own step
        workbook.Sheets[1].GetValue(CellAddress.Parse("A1"));
    }

    [Fact] // ADR-0331 §3: undoing a rename gives back the name and every Formula exactly as typed
    public void Undoing_a_rename_gives_back_the_formulas_exactly()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "=sheet2!A1*2");

        var step = workbook.Do("Sheet2", SheetEdit.Rename("Data"));
        Assert.Equal("=Data!A1*2", Formula(workbook.Sheets[0], "A1"));
        step.Undo();

        Assert.Equal(["Sheet1", "Sheet2"], Names(workbook));
        Assert.Equal("=sheet2!A1*2", Formula(workbook.Sheets[0], "A1"));
        Enter(workbook, "Sheet2", "A1", "4");
        Assert.Equal(8, workbook.Sheets[0].Number("A1"));
    }

    [Fact] // ADR-0331 §3: a Formula whose only Reference to another Sheet the deletion turns into #REF! is recalculated to #REF!, and back on the undo
    public void A_formula_left_naming_no_sheet_is_recalculated()
    {
        var workbook = NewWorkbook("One", "Two");
        Enter(workbook, "One", "A1", "21");
        Enter(workbook, "Two", "A1", "=One!A1*2");

        var step = workbook.Do("One", SheetEdit.DeleteSheet());

        Assert.Equal("=#REF!*2", Formula(workbook.Sheets[0], "A1"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));
        Assert.Contains(CellAddress.Parse("A1"), step.Change.For(workbook.Sheets[0]).ValueChanges);

        step.Undo();

        Assert.Equal(42, workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331 §3: a deletion turns References to the Sheet into #REF!; undoing it gives back the Sheet and those Formulas exactly
    public void A_deletion_is_ref_and_undoes_exactly()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2", "Sheet3");
        Enter(workbook, "Sheet2", "A1", "5");
        Enter(workbook, "Sheet2", "A2", "=A1*3");
        Enter(workbook, "Sheet1", "A1", "=Sheet2!A1*2+Sheet3!A1");
        Enter(workbook, "Sheet3", "B1", "=SUM(Sheet2!A1:A2)");
        workbook.Shown = workbook.Sheets[1];

        var step = workbook.Do("Sheet2", SheetEdit.DeleteSheet());

        Assert.Equal(["Sheet1", "Sheet3"], Names(workbook));
        Assert.Equal("=#REF!*2+Sheet3!A1", Formula(workbook.Sheets[0], "A1"));
        Assert.Equal("=SUM(#REF!)", Formula(workbook.Sheets[1], "B1"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));
        Assert.Same(workbook.Sheets[1], workbook.Shown);
        Assert.True(step.Change.ChangesSheets);

        step.Undo();

        Assert.Equal(["Sheet1", "Sheet2", "Sheet3"], Names(workbook));
        Assert.Same(workbook.Sheets[1], workbook.Shown);
        Assert.Equal("=Sheet2!A1*2+Sheet3!A1", Formula(workbook.Sheets[0], "A1"));
        Assert.Equal("=SUM(Sheet2!A1:A2)", Formula(workbook.Sheets[2], "B1"));
        Assert.Equal(10, workbook.Sheets[0].Number("A1"));
        Assert.Equal(20, workbook.Sheets[2].Number("B1"));

        // Followed again after the undo.
        Enter(workbook, "Sheet2", "A1", "1");
        Assert.Equal(2, workbook.Sheets[0].Number("A1"));
        Assert.Equal(4, workbook.Sheets[2].Number("B1"));
    }

    [Fact] // ADR-0331 §3: the Workbook's last Sheet is not deleted
    public void The_last_sheet_is_not_deleted()
    {
        var workbook = NewWorkbook("Only");

        Assert.Equal(SheetRefusalReason.LastSheet, workbook.Check("Only", SheetEdit.DeleteSheet())!.Reason);
        Assert.Throws<SheetRefusedException>(() => workbook.Do("Only", SheetEdit.DeleteSheet()));
        Assert.Single(workbook.Sheets);
    }

    [Fact] // ADR-0331 §3: moving changes the order, and undoes
    public void Moving_changes_the_order()
    {
        var workbook = NewWorkbook("A", "B", "C");

        var step = workbook.Do("A", SheetEdit.MoveSheet(2));
        Assert.Equal(["B", "C", "A"], Names(workbook));

        step.Undo();
        Assert.Equal(["A", "B", "C"], Names(workbook));
        Assert.Throws<ArgumentOutOfRangeException>(() => workbook.Do("A", SheetEdit.MoveSheet(3)));
    }

    [Fact] // ADR-0331 §3: copying copies Entries, formats and widths, named Name (2), its own References naming the copy
    public void Copying_copies_entries_formats_and_widths()
    {
        var workbook = NewWorkbook("Sheet1", "Data");
        Enter(workbook, "Data", "A1", "4");
        Enter(workbook, "Data", "A2", "=A1*2");
        Enter(workbook, "Data", "A3", "=Data!A1+Sheet1!B1");
        Enter(workbook, "Sheet1", "B1", "1");
        workbook.Do("Data", SheetEdit.SetNumberFormat([CellAddress.Parse("A2")], NumberFormat.Parse("0.00")));
        workbook.Do("Data", SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 20));

        var step = workbook.Do("Data", SheetEdit.CopySheet());
        var copy = step.Sheet;

        Assert.Equal(["Sheet1", "Data", "Data (2)"], Names(workbook));
        Assert.Equal("Data (2)", copy.Name);
        Assert.Equal("8.00", copy.GetDisplay(CellAddress.Parse("A2")).Text);
        Assert.Equal("='Data (2)'!A1+Sheet1!B1", Formula(copy, "A3"));
        Assert.Equal(5, copy.Number("A3"));
        Assert.Equal(20, copy.GetColumnWidth(0)!.Value.Width);

        // Each copy follows its own cells.
        Enter(workbook, "Data (2)", "A1", "10");
        Assert.Equal(4, workbook.Sheets[1].Number("A1"));
        Assert.Equal(20, copy.Number("A2"));

        workbook.Do("Data", SheetEdit.CopySheet(0));
        Assert.Equal("Data (3)", workbook.Sheets[0].Name);
    }

    [Fact] // ADR-0331 §3: a copy is undone and redone
    public void A_copy_is_undone_and_redone()
    {
        var workbook = NewWorkbook("Sheet1");
        Enter(workbook, "Sheet1", "A1", "3");

        var step = workbook.Do("Sheet1", SheetEdit.CopySheet());
        step.Undo();
        Assert.Equal(["Sheet1"], Names(workbook));

        step.Redo();
        Assert.Equal(["Sheet1", "Sheet1 (2)"], Names(workbook));
        Assert.Equal(3, workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331 §4: a copy's name is cut to fit 31 characters
    public void A_copy_name_fits()
    {
        var name = new string('x', 31);
        var workbook = NewWorkbook(name);

        workbook.Do(name, SheetEdit.CopySheet());

        Assert.Equal(new string('x', 27) + " (2)", workbook.Sheets[1].Name);
    }

    [Fact] // ADR-0331 §3: a name that comes to exist resolves a Reference that was #REF!
    public void A_name_that_comes_to_exist_resolves_a_reference()
    {
        var workbook = NewWorkbook("Sheet1");
        Enter(workbook, "Sheet1", "A1", "=Data!A1+1");
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));

        workbook.Do("Sheet1", SheetEdit.InsertSheet("Data"));

        Assert.Equal(1, workbook.Sheets[0].Number("A1"));
    }
}
