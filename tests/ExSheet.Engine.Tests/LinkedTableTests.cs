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

    [Fact] // ADR-0048/0049: a Sheet Document holds the Formulas that name a table and its declaration, never its rows
    public void A_document_holds_the_formulas_not_the_rows()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");
        var json = sheet.ToDocument().ToJson();

        Assert.DoesNotContain("R-4471", json, StringComparison.Ordinal);
        Assert.DoesNotContain("250.5", json, StringComparison.Ordinal);
        Assert.Contains("""
            "linkedTables":[{"name":"Positions","columns":["Id","PV","Desk"]}]
            """, json, StringComparison.Ordinal);
    }

    [Fact] // ADR-0049: opening a document shows #GETTING_DATA for every reader until the first push, never #NAME?
    public void An_opened_document_waits_for_the_first_push()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=IFERROR(XLOOKUP(\"R-4471\",Positions[Id],Positions[PV]),0)");
        sheet.Enter("A3", "=A1+1");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal(ErrorValue.GettingData, reopened.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, reopened.Error("A2"));
        Assert.Equal(ErrorValue.GettingData, reopened.Error("A3"));
        Assert.Equal([new LinkedTable("Positions", ["Id", "PV", "Desk"], true, 0)], reopened.LinkedTables,
            (a, b) => a.Name == b.Name && a.Columns.SequenceEqual(b.Columns) && a.IsWaiting == b.IsWaiting && a.RowCount == b.RowCount);

        var change = reopened.PushLinkedTable("Positions", [[T("R-4471"), N(250.5), T("FX")]]);

        Assert.Equal(250.5, reopened.Number("A1"));
        Assert.Equal(250.5, reopened.Number("A2"));
        Assert.Equal(251.5, reopened.Number("A3"));
        Assert.Equal(["A1", "A2", "A3"], change.ValueChanges.Addresses());
    }

    [Fact] // ADR-0049: declaring the same table again — as a Consumer does at start-up over a document that carries it — changes nothing
    public void An_identical_declaration_changes_nothing()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");
        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));
        reopened.PushLinkedTable("Positions", [[T("R-1"), N(7), T("FX")]]);

        var again = reopened.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"]);
        var sameOnTheLiveSheet = sheet.DeclareLinkedTable("positions", ["Id", "PV", "Desk"]);

        Assert.Empty(again.ValueChanges);
        Assert.Empty(sameOnTheLiveSheet.ValueChanges);
        Assert.Equal(7, reopened.Number("A1"));
        Assert.Equal(300.5, sheet.Number("A1"));
        Assert.Single(reopened.LinkedTables);
    }

    [Fact] // ADR-0049: other columns replace the declaration; the rows held are dropped, readers wait, and a column that is gone is #REF!
    public void A_declaration_with_other_columns_replaces_the_held_one()
    {
        var sheet = WithPositions();
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=SUM(Positions[Desk])");

        var change = sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Book"]);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));
        Assert.Equal(["A1", "A2"], change.ValueChanges.Addresses());

        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("B1")]]);

        Assert.Equal(10, sheet.Number("A1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("A2"));
        Assert.Equal(["Id", "PV", "Book"], sheet.LinkedTables.Single().Columns);
    }

    [Fact] // ADR-0049: a table is never undeclared — there is no API for it
    public void A_table_is_never_undeclared()
    {
        Assert.Equal(
            ["DeclareLinkedTable", "PushLinkedTable", "get_LinkedTables"],
            typeof(Sheet).GetMethods().Select(m => m.Name).Where(n => n.Contains("LinkedTable", StringComparison.Ordinal) || n.Contains("Undeclare", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0049: several declarations, a table no Formula reads, and odd column names round-trip in order
    public void Declarations_round_trip_in_order()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.DeclareLinkedTable("Trades", ["Unit Price", "Qty [lots]", "It's"]);

        var document = SheetDocument.FromJson(sheet.ToDocument().ToJson());

        Assert.Equal(["Rates", "Trades"], document.LinkedTables.Select(t => t.Name));
        Assert.Equal(["Unit Price", "Qty [lots]", "It's"], document.LinkedTables[1].Columns);
        Assert.Equal(["Rates", "Trades"], Sheet.Open(document).LinkedTables.Select(t => t.Name));
    }

    [Fact] // ADR-0049: after opening, an unknown column is still #REF!, and a one-row column still reads as its Value
    public void Opened_declarations_read_as_declared()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("One", ["V"]);
        sheet.Enter("A1", "=One[Missing]");
        sheet.Enter("A2", "=One[V]*2");
        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        reopened.PushLinkedTable("One", [[N(21)]]);
        Assert.Equal(ErrorValue.Ref, reopened.Error("A1"));
        Assert.Equal(42, reopened.Number("A2"));

        reopened.PushLinkedTable("One", [[N(21)], [N(1)]]);
        Assert.Equal(ErrorValue.Value, reopened.Error("A2"));

        reopened.PushLinkedTable("One", []);
        Assert.Equal(ErrorValue.Value, reopened.Error("A2"));
    }

    [Theory] // ADR-0048/0049: a declaration the document cannot hold is refused, as is one in a version 1 document
    [InlineData("""{"version":1,"culture":"en-US","linkedTables":[{"name":"T","columns":["V"]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":{},"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T"}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"columns":["V"]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":[]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V","v"]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":[1]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"A1","columns":["V"]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"rows":[[1]]}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"]},{"name":"t","columns":["W"]}],"cells":[]}""")]
    public void A_bad_declaration_in_a_document_is_refused(string json)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));
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

        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("Dup", ["A", "a"]));
        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("None", []));
        Assert.Throws<InvalidOperationException>(() => sheet.PushLinkedTable("Missing", []));
        Assert.Throws<ArgumentException>(() => sheet.PushLinkedTable("Positions", [[T("a"), N(1)]]));
        Assert.Throws<ArgumentException>(() => sheet.PushLinkedTable("Positions", [[T("a"), Value.FromError(ErrorValue.GettingData), T("b")]]));

        Assert.Equal(300.5, sheet.Number("A1"));
    }
}
