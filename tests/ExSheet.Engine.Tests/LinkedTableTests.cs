using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Linked Tables (ticket 16): SH-16's engine half.</summary>
public class LinkedTableTests
{
    private static Value? N(double n) => Value.FromNumber(n);

    private static Value? T(string text) => Value.FromText(text);

    private static Sheet WithPositions(bool push = true)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"]);
        if (push)
        {
            sheet.PushLinkedTable("Positions",
            [
                [T("R-1001"), N(100), T("Rates")],
                [T("R-4471"), N(250.5), T("FX")],
                [T("R-2002"), N(-50), T("Rates")],
            ]);
        }
        return sheet;
    }

    [Fact] // ADR-0049 (SH-16): SUM over a column and XLOOKUP by key read the snapshot
    public void Formulas_read_the_snapshot_by_column_and_by_key()
    {
        var sheet = WithPositions();

        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])");
        sheet.Enter("A3", "=XLOOKUP(\"R-9999\", Positions[Id], Positions[PV], \"none\")");
        sheet.Enter("A4", "=XLOOKUP(\"R-9999\", Positions[Id], Positions[Desk])");

        Assert.Equal(300.5, sheet.Number("A1"));
        Assert.Equal(250.5, sheet.Number("A2"));
        Assert.Equal("none", sheet.Value("A3")!.Value.Text);
        Assert.Equal(ErrorValue.NA, sheet.Error("A4"));
    }

    [Fact] // ADR-0049 (SH-16): before the first snapshot every reader waits, and IFERROR and ISERROR do not catch it
    public void Before_the_first_snapshot_readers_wait()
    {
        var sheet = WithPositions(push: false);

        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=IFERROR(XLOOKUP(\"R-4471\", Positions[Id], Positions[PV]), 0)");
        sheet.Enter("A3", "=ISERROR(Positions[PV])");
        sheet.Enter("A4", "=IF(TRUE, 1, COUNTA(Positions[Id]))");
        sheet.Enter("B1", "=A1*2");
        sheet.Enter("B2", "=IFERROR(A2, 0)");

        foreach (var address in new[] { "A1", "A2", "A3", "A4", "B1", "B2" })
        {
            Assert.Equal(ErrorValue.GettingData, sheet.Error(address));
        }
    }

    [Fact] // ADR-0049 (SH-16): the first snapshot replaces the wait everywhere it reached
    public void The_first_snapshot_ends_the_wait()
    {
        var sheet = WithPositions(push: false);
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("B1", "=A1*2");

        var change = sheet.PushLinkedTable("Positions", [[T("R-1"), N(21), T("FX")]]);

        Assert.Equal(42, sheet.Number("B1"));
        Assert.Equal(["A1", "B1"], change.ValueChanges.Addresses());
    }

    [Fact] // ADR-0049 (SH-16): an undeclared table name is #NAME?, and declaring it makes its readers wait
    public void An_undeclared_name_is_a_name_error_until_declared()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=SUM(Rates[Mid])");
        Assert.Equal(ErrorValue.Name, sheet.Error("A1"));

        var change = sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(["A1"], change.ValueChanges.Addresses());
    }

    [Fact] // ADR-0049: a column the table does not have is #REF!
    public void An_unknown_column_is_a_ref_error()
    {
        var sheet = WithPositions();

        sheet.Enter("A1", "=SUM(Positions[Delta])");

        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));
    }

    [Fact] // ADR-0049: table and column names match without regard to case, as Excel's do
    public void Names_match_without_regard_to_case()
    {
        var sheet = WithPositions();

        sheet.Enter("A1", "=SUM(positions[pv])");

        Assert.Equal(300.5, sheet.Number("A1"));
    }

    [Fact] // ADR-0049 (SH-16): a new snapshot recalculates only the table's readers and what depends on them
    public void A_new_snapshot_recalculates_only_readers()
    {
        var sheet = WithPositions();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=A1+1");
        sheet.Enter("A3", "=COUNTA(Rates[Pair])");
        sheet.Enter("A4", "7");
        sheet.Enter("A5", "=A4*2");

        var change = sheet.PushLinkedTable("Positions", [[T("R-1"), N(1), T("FX")]]);

        Assert.Equal(["A1", "A2"], change.Recalculated.Addresses());
        Assert.Equal(2, sheet.Number("A2"));
    }

    [Fact] // ADR-0049 (SH-16): a snapshot replaces the last in one step; no recalculation sees a mix
    public void A_snapshot_replaces_the_last_in_one_step()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Book", ["Long", "Short"]);
        sheet.Enter("A1", "=SUM(Book[Long])-SUM(Book[Short])");
        sheet.Enter("A2", "=COUNT(Book[Long])-COUNT(Book[Short])");

        for (var n = 1; n <= 5; n++)
        {
            var rows = Enumerable.Range(1, n).Select(i => (IReadOnlyList<Value?>)[N(i * n), N(i * n)]).ToList();
            var change = sheet.PushLinkedTable("Book", rows);
            // Both columns come from the same snapshot, so the difference is always 0.
            Assert.Equal(0, sheet.Number("A1"));
            Assert.Equal(0, sheet.Number("A2"));
        }
        Assert.Equal(5, sheet.LinkedTables.Single().RowCount);
    }

    [Fact] // ADR-0049 (SH-16): a copy reaching a waiting cell is refused, and one that does not is not
    public void A_copy_reaching_a_waiting_cell_is_refused()
    {
        var sheet = WithPositions(push: false);
        sheet.Enter("A1", "1");
        sheet.Enter("B2", "=SUM(Positions[PV])");

        var refused = sheet.Copy(CellRange.Parse("A1:C3"));
        var allowed = sheet.Copy(CellRange.Parse("A1:A3"));

        Assert.True(refused.IsRefused);
        Assert.Equal(SheetRefusalReason.WaitingForData, refused.Refusal!.Reason);
        Assert.Contains("B2", refused.Refusal.Message, StringComparison.Ordinal);
        Assert.Null(refused.Block);
        Assert.Null(refused.Text);
        Assert.False(allowed.IsRefused);
    }

    [Fact] // ADR-0047/0049: a table's column is a range to the functions: text and blanks in it are skipped by SUM and counted by COUNTA
    public void A_column_is_a_range_to_the_functions()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Mixed", ["V"]);
        sheet.PushLinkedTable("Mixed", [[N(1)], [T("2")], [null], [Value.FromBoolean(true)], [N(4)]]);

        Assert.Equal(5, sheet.Evaluate("=SUM(Mixed[V])").Number);
        Assert.Equal(2.5, sheet.Evaluate("=AVERAGE(Mixed[V])").Number);
        Assert.Equal(2, sheet.Evaluate("=COUNT(Mixed[V])").Number);
        Assert.Equal(4, sheet.Evaluate("=COUNTA(Mixed[V])").Number);
        Assert.Equal(4, sheet.Evaluate("=MAX(Mixed[V])").Number);
    }

    [Fact] // ADR-0047/0049: an Error Value in a table's column propagates through the aggregates
    public void An_error_in_a_column_propagates()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("E", ["V"]);
        sheet.PushLinkedTable("E", [[N(1)], [Value.FromError(ErrorValue.NA)]]);

        Assert.Equal(ErrorValue.NA, sheet.Evaluate("=SUM(E[V])").Error);
        Assert.Equal(1, sheet.Evaluate("=IFERROR(SUM(E[V]), 1)").Number);
    }

    [Fact] // ADR-0047: a column of more than one row used as one Value is #VALUE! (no spilled arrays); one row reads as its Value
    public void A_column_used_as_one_value()
    {
        var sheet = WithPositions();
        sheet.DeclareLinkedTable("One", ["V"]);
        sheet.PushLinkedTable("One", [[N(21)]]);

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=Positions[PV]*2").Error);
        Assert.Equal(42, sheet.Evaluate("=One[V]*2").Number);
    }

    [Fact] // ADR-0049: a column whose name needs Excel's double brackets reads the same
    public void A_column_named_with_special_characters()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Trades", ["Unit Price", "Qty"]);
        sheet.PushLinkedTable("Trades", [[N(2.5), N(4)]]);

        sheet.Enter("A1", "=SUM(Trades[[Unit Price]])*SUM(Trades[Qty])");

        Assert.Equal(10, sheet.Number("A1"));
        Assert.Equal("=SUM(Trades[[Unit Price]])*SUM(Trades[Qty])", sheet.GetEntry(CellAddress.Parse("A1"))!.Formula);
    }

    [Fact] // ADR-0046/0049: a reader moved by an insertion still reads its table
    public void A_moved_reader_still_reads_its_table()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.InsertRows(0);

        var change = sheet.PushLinkedTable("Positions", [[T("x"), N(9), T("y")]]);

        Assert.Equal(["A2"], change.Recalculated.Addresses());
        Assert.Equal(9, sheet.Number("A2"));
    }

    [Fact] // ADR-0048/0049: a Sheet Document holds the Formulas that name a table and never its rows
    public void A_document_holds_the_formulas_not_the_rows()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));
        Assert.DoesNotContain("R-4471", sheet.ToDocument().ToJson(), StringComparison.Ordinal);
        reopened.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"]);

        Assert.Equal(ErrorValue.GettingData, reopened.Error("A1"));
    }

    [Fact] // ADR-0049: the declared tables are listed, with whether each is waiting
    public void The_declared_tables_are_listed()
    {
        var sheet = WithPositions();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);

        Assert.Equal(
            [new LinkedTable("Positions", ["Id", "PV", "Desk"], false, 3), new LinkedTable("Rates", ["Pair", "Mid"], true, 0)],
            sheet.LinkedTables,
            (a, b) => a.Name == b.Name && a.Columns.SequenceEqual(b.Columns) && a.IsWaiting == b.IsWaiting && a.RowCount == b.RowCount);
    }

    [Theory] // ADR-0049: a name a structured reference cannot carry, or one Excel refuses for a Table, is refused
    [InlineData("A1")]
    [InlineData("xfd1048576")]
    [InlineData("R1C1")]
    [InlineData("R")]
    [InlineData("c")]
    [InlineData("TRUE")]
    [InlineData("1st")]
    [InlineData("Has Space")]
    [InlineData("")]
    public void A_bad_table_name_is_refused(string name)
    {
        var sheet = NewSheet();

        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable(name, ["V"]));
    }

    [Fact] // ADR-0049: declarations and snapshots that cannot be read are refused, and change nothing
    public void Bad_declarations_and_snapshots_are_refused()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");

        Assert.Throws<InvalidOperationException>(() => sheet.DeclareLinkedTable("POSITIONS", ["X"]));
        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("Dup", ["A", "a"]));
        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("None", []));
        Assert.Throws<InvalidOperationException>(() => sheet.PushLinkedTable("Missing", []));
        Assert.Throws<ArgumentException>(() => sheet.PushLinkedTable("Positions", [[T("a"), N(1)]]));
        Assert.Throws<ArgumentException>(() => sheet.PushLinkedTable("Positions", [[T("a"), Value.FromError(ErrorValue.GettingData), T("b")]]));

        Assert.Equal(300.5, sheet.Number("A1"));
    }
}
