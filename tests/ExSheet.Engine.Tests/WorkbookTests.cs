using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class WorkbookTests
{
    private static Workbook NewWorkbook(params string[] names) => new(EnUs, names);

    private static WorkbookStep Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    [Fact] // ADR-0331: a Workbook holds its Sheets in order, each named, under one culture
    public void A_workbook_holds_named_sheets_in_order()
    {
        var workbook = NewWorkbook("Inputs", "Totals", "Notes");

        Assert.Equal(["Inputs", "Totals", "Notes"], workbook.Sheets.Select(s => s.Name));
        Assert.All(workbook.Sheets, s => Assert.Same(workbook.Culture, s.Culture));
        Assert.Same(workbook.Sheets[1], workbook.Find("TOTALS"));
        Assert.Null(workbook.Find("Missing"));
    }

    [Theory] // ADR-0331: names follow Excel's rules, unique without regard to case, and a Workbook holds at least one Sheet
    [InlineData("Sheet1|SHEET1")]
    [InlineData("a:b")]
    [InlineData("")]
    public void A_workbook_refuses_names_excel_refuses(string names) =>
        Assert.Throws<ArgumentException>(() => NewWorkbook(names.Length == 0 ? [] : names.Split('|')));

    [Fact] // ADR-0331: a Workbook's Sheets hold Entries, each Sheet its own
    public void Each_sheet_holds_its_own_entries()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");

        Enter(workbook, "Sheet1", "A1", "1");
        Enter(workbook, "sheet2", "A1", "2");

        Assert.Equal(1, workbook.Sheets[0].Number("A1"));
        Assert.Equal(2, workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331: =Sheet2!A1 reads another Sheet, and recalculates when that cell changes
    public void A_reference_to_another_sheet_computes_and_follows_it()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        var (first, second) = (workbook.Sheets[0], workbook.Sheets[1]);
        Enter(workbook, "Sheet2", "A1", "5");
        Enter(workbook, "Sheet1", "B1", "=Sheet2!A1*2");
        Enter(workbook, "Sheet1", "C1", "=SUM(sheet2!A1:A3)");
        Assert.Equal(10, first.Number("B1"));

        var change = Enter(workbook, "Sheet2", "A2", "7").Change;

        Assert.Equal(12, first.Number("C1"));
        Assert.Equal(["Sheet1", "Sheet2"], change.Sheets.Select(s => s.Name));
        Assert.Equal(["A2"], change.For(second).ValueChanges.Addresses());
        Assert.Equal(["C1"], change.For(first).ValueChanges.Addresses());
        Assert.Equal(["C1"], change.For(first).Recalculated.Addresses());
    }

    [Fact] // ADR-0331, ADR-0047: one dependency order across Sheets, each Formula computed once, from Values already final
    public void A_chain_across_sheets_is_computed_in_one_dependency_order()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        var (first, second) = (workbook.Sheets[0], workbook.Sheets[1]);
        Enter(workbook, "Sheet2", "A1", "=Sheet1!A1+1");
        Enter(workbook, "Sheet1", "B1", "=Sheet2!A1*2");
        Enter(workbook, "Sheet2", "B1", "=Sheet1!B1+Sheet2!A1");

        var change = Enter(workbook, "Sheet1", "A1", "5").Change;

        Assert.Equal(6, second.Number("A1"));
        Assert.Equal(12, first.Number("B1"));
        Assert.Equal(18, second.Number("B1"));
        Assert.Equal(["B1"], change.For(first).Recalculated.Addresses());
        Assert.Equal(["A1", "B1"], change.For(second).Recalculated.Addresses());
    }

    [Fact] // ADR-0331, ADR-0047: a cycle through two Sheets is #CIRC! on both
    public void A_cycle_through_two_sheets_is_circ()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "=Sheet2!A1+1");
        Enter(workbook, "Sheet2", "A1", "=Sheet1!A1+1");

        Assert.Equal(ErrorValue.Circ, workbook.Sheets[0].Error("A1"));
        Assert.Equal(ErrorValue.Circ, workbook.Sheets[1].Error("A1"));
    }

    [Fact] // ADR-0331: a name with a space is quoted, as in Excel, and read without regard to case
    public void A_quoted_sheet_name_is_read()
    {
        var workbook = NewWorkbook("Sheet1", "Q1 Sales");
        Enter(workbook, "Q1 Sales", "B2", "3");
        Enter(workbook, "Q1 Sales", "B3", "4");
        Enter(workbook, "Sheet1", "A1", "=SUM('Q1 Sales'!B2:B3)");
        Enter(workbook, "Sheet1", "A2", "=INDIRECT(\"'q1 sales'!B3\")");

        Assert.Equal(7, workbook.Sheets[0].Number("A1"));
        Assert.Equal(4, workbook.Sheets[0].Number("A2"));
    }

    [Fact] // ADR-0331: a Formula that stops naming another Sheet stops following it
    public void A_replaced_formula_stops_following_another_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "=Sheet2!A1");
        Enter(workbook, "Sheet1", "A2", "=SUM(Sheet2!A1:A3)");
        Enter(workbook, "Sheet1", "A1", "1");
        Enter(workbook, "Sheet1", "A2", "2");

        var change = Enter(workbook, "Sheet2", "A1", "5").Change;

        Assert.Equal(["Sheet2"], change.Sheets.Select(s => s.Name));
        Assert.Empty(change.For(workbook.Sheets[0]).Recalculated);
    }

    [Fact] // ADR-0331, ADR-0047: a Formula entered into a General cell takes the format of the cell it reads on another Sheet, as of one on its own
    public void A_formula_takes_the_format_of_a_cell_on_another_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet2", "A1", "10%");
        Enter(workbook, "Sheet1", "A1", "=Sheet2!A1");

        Assert.Equal("10%", workbook.Sheets[0].GetDisplay(CellAddress.Parse("A1")).Text);
    }

    [Fact] // ADR-0331 §9, ADR-0180: the Workbook holds the seed and the count, so a change on one Sheet draws again on every Sheet, as Excel's recalculation does
    public void Rand_on_another_sheet_draws_again_when_any_sheet_changes()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        workbook.SetSeed(7);
        Enter(workbook, "Sheet2", "A1", "=RAND()");
        var drawn = workbook.Sheets[1].Number("A1");
        var changes = workbook.Changes;

        var change = Enter(workbook, "Sheet1", "A1", "1");

        Assert.Equal(changes + 1, workbook.Changes);
        Assert.NotEqual(drawn, workbook.Sheets[1].Number("A1"));
        Assert.Contains(workbook.Sheets[1], change.Change.Sheets);
    }

    [Fact] // ADR-0180 (2026-10-20), ADR-0331 §9: a draw names its Sheet, so the same Formula at the same cell of two Sheets draws two numbers
    public void Rand_at_the_same_cell_of_two_sheets_draws_two_numbers()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        workbook.SetSeed(7);
        Enter(workbook, "Sheet1", "A1", "=RAND()");
        Enter(workbook, "Sheet2", "A1", "=RAND()");

        Assert.NotEqual(workbook.Sheets[0].Number("A1"), workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331 §9: a copied Sheet draws from the Workbook's seed, under its own name
    public void A_copied_sheet_draws_from_the_workbooks_seed()
    {
        var workbook = NewWorkbook("Sheet1");
        workbook.SetSeed(7);
        Enter(workbook, "Sheet1", "A1", "=RAND()");

        workbook.Do("Sheet1", SheetEdit.CopySheet());

        Assert.Equal(7UL, workbook.Seed);
        Assert.Equal(2, workbook.Sheets.Count);
        Assert.NotEqual(workbook.Sheets[0].Number("A1"), workbook.Sheets[1].Number("A1"));
    }

    [Fact] // ADR-0331: a qualifier naming no Sheet of the Workbook is #REF!
    public void A_qualifier_naming_no_sheet_is_ref()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "=Sheet3!A1");
        Enter(workbook, "Sheet1", "A2", "=SUM(Sheet3!A1:B2)");
        Enter(workbook, "Sheet1", "A3", "=INDIRECT(\"Sheet3!A1\")");

        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A2"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A3"));
    }

    [Fact] // ADR-0331, ADR-0124: INDIRECT and OFFSET reach another Sheet, and read it as this recalculation leaves it
    public void Computed_references_reach_another_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet2", "A1", "1");
        Enter(workbook, "Sheet2", "A2", "=Sheet1!C1*3");
        Enter(workbook, "Sheet1", "A1", "=INDIRECT(\"Sheet2!A2\")");
        Enter(workbook, "Sheet1", "B1", "=SUM(OFFSET(Sheet2!A1,0,0,2,1))");

        Enter(workbook, "Sheet1", "C1", "4");

        Assert.Equal(12, workbook.Sheets[0].Number("A1"));
        Assert.Equal(13, workbook.Sheets[0].Number("B1"));
    }

    [Fact] // ADR-0331, ADR-0047: criteria and lookup ranges on another Sheet read that Sheet's cells
    public void Ranges_on_another_sheet_are_read_there()
    {
        var workbook = NewWorkbook("Sheet1", "Data");
        Enter(workbook, "Data", "A1", "x");
        Enter(workbook, "Data", "A2", "y");
        Enter(workbook, "Data", "A3", "x");
        Enter(workbook, "Data", "B1", "1");
        Enter(workbook, "Data", "B2", "2");
        Enter(workbook, "Data", "B3", "4");
        Enter(workbook, "Sheet1", "A1", "y");
        Enter(workbook, "Sheet1", "A2", "100");
        Enter(workbook, "Sheet1", "B1", "=SUMIF(Data!A1:A3,\"x\",Data!B1:B3)");
        Enter(workbook, "Sheet1", "B2", "=XLOOKUP(\"y\",Data!A1:A3,Data!B1:B3)");
        Enter(workbook, "Sheet1", "B3", "=INDEX(Data!B1:B3,3)");

        Assert.Equal(5, workbook.Sheets[0].Number("B1"));
        Assert.Equal(2, workbook.Sheets[0].Number("B2"));
        Assert.Equal(4, workbook.Sheets[0].Number("B3"));
    }

    [Fact] // ADR-0331, ADR-0125: A1# qualified with another Sheet reads that Sheet's spill
    public void A_spill_on_another_sheet_is_read_there()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet2", "A1", "=SEQUENCE(3)");
        Enter(workbook, "Sheet1", "A1", "=SUM(Sheet2!A1#)");
        Assert.Equal(6, workbook.Sheets[0].Number("A1"));

        Enter(workbook, "Sheet2", "A3", "x");

        Assert.Equal(ErrorValue.Spill, workbook.Sheets[1].Error("A1"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("A1"));
    }

    [Fact] // ADR-0331, ADR-0049: Linked Tables are declared on the Workbook and read from any Sheet
    public void A_linked_table_is_read_from_any_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "=SUM(Trades[Amount])");
        Enter(workbook, "Sheet2", "A1", "=IFERROR(COUNT(Trades[Amount]),-1)");
        Assert.Equal(ErrorValue.Name, workbook.Sheets[0].Error("A1"));

        workbook.DeclareLinkedTable("Trades", ["Id", "Amount"], ["Id"]);

        Assert.Equal(["Trades"], workbook.LinkedTables.Select(t => t.Name));
        Assert.Equal(ErrorValue.GettingData, workbook.Sheets[0].Error("A1"));
        Assert.Equal(ErrorValue.GettingData, workbook.Sheets[1].Error("A1"));

        var change = workbook.PushLinkedTable("Trades", [[Value.FromNumber(1), Value.FromNumber(10)], [Value.FromNumber(2), Value.FromNumber(5)]]);

        Assert.Equal(15, workbook.Sheets[0].Number("A1"));
        Assert.Equal(2, workbook.Sheets[1].Number("A1"));
        Assert.Equal(["Sheet1", "Sheet2"], change.Sheets.Select(s => s.Name));
    }

    [Fact] // ADR-0049, ADR-0330 §6: a Linked Table's snapshot is read as it stands, column by column, none while it waits, and a later snapshot replaces it without changing it
    public void A_linked_tables_snapshot_is_read_column_by_column()
    {
        var workbook = NewWorkbook("Sheet1");
        workbook.DeclareLinkedTable("Trades", ["Id", "Amount"], ["Id"]);
        Assert.Null(workbook.LinkedTableColumns("trades"));

        workbook.PushLinkedTable("Trades", [[Value.FromNumber(1), Value.FromNumber(10)], [Value.FromNumber(2), null]]);
        var first = workbook.LinkedTableColumns("Trades")!;
        workbook.PushLinkedTable("Trades", [[Value.FromNumber(3), Value.FromNumber(7)]]);

        Assert.Equal([[Value.FromNumber(1), Value.FromNumber(2)], [Value.FromNumber(10), null]], first.Select(c => c.ToArray()));
        Assert.Equal([[Value.FromNumber(3)], [Value.FromNumber(7)]], workbook.LinkedTableColumns("Trades")!.Select(c => c.ToArray()));
        Assert.Throws<InvalidOperationException>(() => workbook.LinkedTableColumns("Positions"));
        Assert.Throws<RepeatedKeyException>(() => workbook.PushLinkedTable("Trades", [[Value.FromNumber(3), null], [Value.FromNumber(3), null]]));
        Assert.Null(workbook.LinkedTableColumns("Trades"));
    }

    [Fact] // ADR-0331, ADR-0049: a waiting cell on another Sheet makes its reader wait, which IFERROR does not catch
    public void A_wait_on_another_sheet_taints_its_reader()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        workbook.DeclareLinkedTable("Trades", ["Amount"], []);
        Enter(workbook, "Sheet2", "A1", "=SUM(Trades[Amount])");
        Enter(workbook, "Sheet1", "A1", "=IFERROR(Sheet2!A1,0)");

        Assert.Equal(ErrorValue.GettingData, workbook.Sheets[0].Error("A1"));
    }

    [Fact] // ADR-0331, ADR-0048: an edit names its Sheet, and its step undoes and redoes it with the Workbook recalculated
    public void A_step_on_one_sheet_undoes_across_the_workbook()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        var (first, second) = (workbook.Sheets[0], workbook.Sheets[1]);
        Enter(workbook, "Sheet2", "A1", "1");
        Enter(workbook, "Sheet1", "A1", "=Sheet2!A1+1");

        var step = Enter(workbook, "Sheet2", "A1", "41");
        Assert.Same(second, step.Sheet);
        Assert.Equal(42, first.Number("A1"));

        var undone = step.Undo();
        Assert.True(step.IsUndone);
        Assert.Equal(1, second.Number("A1"));
        Assert.Equal(2, first.Number("A1"));
        Assert.Equal(["A1"], undone.For(first).ValueChanges.Addresses());

        step.Redo();
        Assert.Equal(42, first.Number("A1"));
    }

    [Fact] // ADR-0331: an edit naming no Sheet of the Workbook is refused, and changes nothing
    public void An_edit_naming_no_sheet_is_refused()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");

        Assert.Throws<ArgumentException>(() => Enter(workbook, "Sheet3", "A1", "1"));
        Assert.All(workbook.Sheets, s => Assert.Empty(s.EntryAddresses));
    }

    [Fact] // ADR-0331: a Sheet created on its own is a Workbook of one Sheet, and reads as it did
    public void A_sheet_on_its_own_still_refuses_other_sheets()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=Sheet2!A1");

        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));
    }

    private static (string Sheet, SheetEdit Edit) EnterOn(string sheet, string address, string typed) =>
        (sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    [Fact] // ADR-0332 §5, ADR-0180: edits on several Sheets are one operation: one change, counted once, one step
    public void Edits_on_several_sheets_are_one_operation()
    {
        var workbook = NewWorkbook("One", "Two");
        Enter(workbook, "One", "A1", "1");
        Enter(workbook, "Two", "A1", "=One!A1+1");
        var before = workbook.Changes;

        var step = workbook.Do([EnterOn("One", "A1", "10"), EnterOn("Two", "B1", "x")]);

        Assert.Equal(before + 1, workbook.Changes);
        Assert.Equal(10, workbook.Sheets[0].Number("A1"));
        Assert.Equal(11, workbook.Sheets[1].Number("A1"));
        Assert.Equal("x", workbook.Sheets[1].GetEntryText(CellAddress.Parse("B1")));
        Assert.Equal(["One", "Two"], step.Change.Sheets.Select(s => s.Name));
    }

    [Fact] // ADR-0332 §5, ADR-0048: the one step undoes every edit, on every Sheet, and redoes them all
    public void The_step_of_several_sheets_is_undone_and_redone_whole()
    {
        var workbook = NewWorkbook("One", "Two");
        Enter(workbook, "One", "A1", "1");
        Enter(workbook, "Two", "A1", "=One!A1+1");
        var step = workbook.Do([EnterOn("One", "A1", "10"), EnterOn("Two", "B1", "x")]);

        step.Undo();

        Assert.Equal(1, workbook.Sheets[0].Number("A1"));
        Assert.Equal(2, workbook.Sheets[1].Number("A1"));
        Assert.Null(workbook.Sheets[1].GetEntry(CellAddress.Parse("B1")));

        step.Redo();

        Assert.Equal(10, workbook.Sheets[0].Number("A1"));
        Assert.Equal("x", workbook.Sheets[1].GetEntryText(CellAddress.Parse("B1")));
    }

    [Fact] // ADR-0332 §2, ADR-0180: a Formula that cannot be read leaves every Sheet, and the count of changes, as they were
    public void An_unreadable_formula_leaves_every_sheet_as_it_was()
    {
        var workbook = NewWorkbook("One", "Two");
        Enter(workbook, "One", "A1", "1");
        var before = workbook.Changes;

        Assert.Throws<FormulaSyntaxException>(() => workbook.Do([EnterOn("One", "A1", "2"), EnterOn("Two", "A1", "=1+")]));

        Assert.Equal(1, workbook.Sheets[0].Number("A1"));
        Assert.Null(workbook.Sheets[1].GetEntry(CellAddress.Parse("A1")));
        Assert.Equal(before, workbook.Changes);
    }

    [Fact] // ADR-0331 §3: a Sheet operation is done alone, and an operation is at least one edit
    public void Several_edits_refuse_a_sheet_operation_and_nothing()
    {
        var workbook = NewWorkbook("One", "Two");

        Assert.Throws<ArgumentException>(() => workbook.Do([]));
        Assert.Throws<ArgumentException>(() => workbook.Do([("One", SheetEdit.DeleteSheet())]));
        Assert.Throws<ArgumentException>(() => workbook.Do([EnterOn("Three", "A1", "1")]));
    }
}
