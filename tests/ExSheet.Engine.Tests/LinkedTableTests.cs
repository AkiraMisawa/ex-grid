using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Linked Tables (ticket 16): SH-16's engine half; a table's key (ticket 36): SH-33.</summary>
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
            ["DeclareLinkedTable", "DeclareLinkedTable", "PushLinkedTable", "get_LinkedTables"],
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

    [Fact] // ADR-0049 / ADR-0125: after opening, an unknown column is still #REF!, a one-row column still reads as its Value, and a longer one spills
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
        Assert.Equal(42, reopened.Number("A2"));
        Assert.Equal(2, reopened.Number("A3"));
        Assert.Equal(CellAddress.Parse("A2"), reopened.SpilledFrom(CellAddress.Parse("A3")));

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

    private static Sheet WithKeyedPositions()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"], key: "Id");
        sheet.Enter("A1", "=SUM(Positions[PV])");
        sheet.Enter("A2", "=XLOOKUP(\"R-1\", Positions[Id], Positions[PV])");
        sheet.Enter("A3", "=IFERROR(XLOOKUP(\"R-1\", Positions[Id], Positions[PV]), 0)");
        sheet.Enter("A4", "=A1+1");
        return sheet;
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a keyed table's snapshot in which every key differs is taken
    public void A_keyed_snapshot_without_a_repeat_is_taken()
    {
        var sheet = WithKeyedPositions();

        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-2"), N(20), T("FX")], [T("R-10"), N(30), T("FX")]]);

        Assert.Equal(60, sheet.Number("A1"));
        Assert.Equal(10, sheet.Number("A2"));
        Assert.Equal(new LinkedTable("Positions", ["Id", "PV", "Desk"], false, 3, "Id"), sheet.LinkedTables.Single(),
            (a, b) => a.Name == b.Name && a.Columns.SequenceEqual(b.Columns) && a.IsWaiting == b.IsWaiting && a.RowCount == b.RowCount && a.Key == b.Key);
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a repeated key refuses the snapshot by name; the table waits again and no earlier Value is shown
    public void A_repeated_key_refuses_the_snapshot_and_the_table_waits()
    {
        var sheet = WithKeyedPositions();
        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-2"), N(20), T("FX")]]);
        Assert.Equal(30, sheet.Number("A1"));

        var refused = Assert.Throws<RepeatedKeyException>(() =>
            sheet.PushLinkedTable("Positions", [[T("R-1"), N(11), T("FX")], [T("R-2"), N(21), T("FX")], [T("R-1"), N(12), T("Rates")]]));

        Assert.Contains("'Positions'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'Id'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("\"R-1\"", refused.Message, StringComparison.Ordinal);
        Assert.Equal(("Positions", "Id", Value.FromText("R-1"), 0, 2), (refused.Table, refused.KeyColumn, refused.Key, refused.FirstRow, refused.SecondRow));
        // Neither the earlier snapshot's 30 nor the refused one's Values: every reader waits.
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A4"));
        Assert.Equal(["A1", "A2", "A3", "A4"], refused.Change.ValueChanges.Addresses());
        var table = sheet.LinkedTables.Single();
        Assert.True(table.IsWaiting);
        Assert.Equal(0, table.RowCount);
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: IFERROR(XLOOKUP(…), 0) over a refused table shows #GETTING_DATA, never 0
    public void Iferror_does_not_catch_a_refused_table()
    {
        var sheet = WithKeyedPositions();
        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")]]);
        Assert.Equal(10, sheet.Number("A3"));

        Assert.Throws<RepeatedKeyException>(() => sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-1"), N(10), T("FX")]]));

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A3"));
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: case does not tell keys apart, as it does not for XLOOKUP's exact match
    public void Keys_that_differ_only_by_case_repeat()
    {
        var sheet = WithKeyedPositions();

        var refused = Assert.Throws<RepeatedKeyException>(() =>
            sheet.PushLinkedTable("Positions", [[T("r-1"), N(1), T("FX")], [T("R-1"), N(2), T("FX")]]));

        Assert.Contains("\"r-1\" in row 0 and \"R-1\" in row 1", refused.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a number and text of the same digits are two keys, as XLOOKUP tells them apart
    public void A_number_and_text_of_the_same_digits_do_not_repeat()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Codes", ["Code", "V"], key: "Code");
        sheet.Enter("A1", "=XLOOKUP(1, Codes[Code], Codes[V])");
        sheet.Enter("A2", "=XLOOKUP(\"1\", Codes[Code], Codes[V])");

        sheet.PushLinkedTable("Codes", [[N(1), N(10)], [T("1"), N(20)], [Value.FromBoolean(true), N(30)], [T("TRUE"), N(40)]]);

        Assert.Equal(10, sheet.Number("A1"));
        Assert.Equal(20, sheet.Number("A2"));
        Assert.Equal(4, sheet.LinkedTables.Single().RowCount);
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a blank key is not a key; any number of rows may have none
    public void Blank_keys_are_not_compared()
    {
        var sheet = WithKeyedPositions();

        sheet.PushLinkedTable("Positions", [[null, N(1), T("FX")], [T("R-1"), N(10), T("FX")], [null, N(2), T("FX")], [null, N(3), T("FX")]]);

        Assert.Equal(16, sheet.Number("A1"));
        Assert.Equal(10, sheet.Number("A2"));
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a key repeats exactly when XLOOKUP's exact match cannot tell the two apart
    public void A_repeat_is_what_xlookup_cannot_tell_apart()
    {
        (Value First, Value Second)[] pairs =
        [
            (Value.FromText("R-1"), Value.FromText("r-1")),
            (Value.FromText("Straße"), Value.FromText("STRASSE")),
            (Value.FromText("é"), Value.FromText("e")),
            (Value.FromText("co-op"), Value.FromText("coop")),
            (Value.FromText("R-1"), Value.FromText("R-1 ")),
            (Value.FromNumber(1), Value.FromText("1")),
            (Value.FromNumber(0.1 + 0.2), Value.FromNumber(0.3)),
            (Value.FromBoolean(true), Value.FromText("TRUE")),
            (Value.FromBoolean(false), Value.FromBoolean(false)),
            (Value.FromNumber(2), Value.FromNumber(2)),
        ];
        foreach (var (first, second) in pairs)
        {
            var sheet = NewSheet();
            sheet.DeclareLinkedTable("Keys", ["Key", "Row"]);
            sheet.DeclareLinkedTable("Keyed", ["Key", "Row"], key: "Key");
            sheet.PushLinkedTable("Keys", [[second, N(2)]]);
            sheet.Enter("A1", "=XLOOKUP(B1, Keys[Key], Keys[Row], \"none\")");
            sheet.SetEntry(CellAddress.Parse("B1"), Entry.FromValue(first));
            var xlookupMatches = sheet.Value("A1") is { Kind: ValueKind.Number };

            var refused = Record.Exception(() => sheet.PushLinkedTable("Keyed", [[first, N(1)], [second, N(2)]]));

            Assert.True(xlookupMatches == refused is RepeatedKeyException,
                $"{first} and {second}: XLOOKUP {(xlookupMatches ? "matches" : "tells them apart")}, and the snapshot was {(refused is null ? "taken" : "refused")}.");
        }
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: the key names one of the columns, without regard to case; any other name is refused by name
    public void The_key_is_one_of_the_columns()
    {
        var sheet = NewSheet();

        var refused = Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Book"));
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"], key: "pair");

        Assert.Contains("'Book'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'Positions'", refused.Message, StringComparison.Ordinal);
        Assert.Equal("key", refused.ParamName);
        Assert.Equal(["Rates"], sheet.LinkedTables.Select(t => t.Name));
        Assert.Equal("Pair", sheet.LinkedTables.Single().Key);
        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("Empty", ["V"], key: ""));
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a declaration with another key replaces the held one; the rows are dropped and readers wait
    public void A_declaration_with_another_key_replaces_the_held_one()
    {
        var sheet = WithKeyedPositions();
        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-2"), N(20), T("FX")]]);

        var same = sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"], key: "id");
        Assert.Empty(same.ValueChanges);
        Assert.Equal(30, sheet.Number("A1"));

        var change = sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"], key: "Desk");

        Assert.Equal(["A1", "A2", "A3", "A4"], change.ValueChanges.Addresses());
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal("Desk", sheet.LinkedTables.Single().Key);
        // The new key is the one checked: Desk repeats, Id no longer counts.
        Assert.Throws<RepeatedKeyException>(() => sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-2"), N(20), T("FX")]]));
        sheet.PushLinkedTable("Positions", [[T("R-1"), N(10), T("FX")], [T("R-1"), N(20), T("Rates")]]);
        Assert.Equal(30, sheet.Number("A1"));

        sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"]);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Null(sheet.LinkedTables.Single().Key);
    }

    [Fact] // ADR-0049 (2026-09-30), ADR-0048, SH-33: the Sheet Document records the key with the declaration, and it opens with it
    public void The_document_records_the_key()
    {
        var sheet = WithKeyedPositions();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);

        var json = sheet.ToDocument().ToJson();
        var reopened = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Contains("""
            "linkedTables":[{"name":"Positions","columns":["Id","PV","Desk"],"key":"Id"},{"name":"Rates","columns":["Pair","Mid"]}]
            """, json, StringComparison.Ordinal);
        Assert.Equal(["Id", null], reopened.LinkedTables.Select(t => t.Key));
        Assert.Throws<RepeatedKeyException>(() => reopened.PushLinkedTable("Positions", [[T("R-1"), N(1), T("FX")], [T("R-1"), N(2), T("FX")]]));
        Assert.Empty(reopened.DeclareLinkedTable("Positions", ["Id", "PV", "Desk"], key: "Id").ValueChanges);
    }

    [Fact] // ADR-0048/0049 (2026-09-30): a document written before keys were recorded still opens, its tables without a key
    public void A_document_without_a_key_still_opens()
    {
        var document = SheetDocument.FromJson("""{"version":6,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"Positions","columns":["Id","PV"]}],"cells":[{"at":"A1","formula":"=SUM(Positions[PV])"}]}""");
        var sheet = Sheet.Open(document);

        Assert.Null(Assert.Single(document.LinkedTables).Key);
        Assert.Null(sheet.LinkedTables.Single().Key);
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        sheet.PushLinkedTable("Positions", [[T("R-1"), N(1)], [T("R-1"), N(2)]]);
        Assert.Equal(3, sheet.Number("A1"));
    }

    [Theory] // ADR-0048/0049 (2026-09-30): a key the document cannot hold is refused, as is a key in a document before version 7
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"key":"V"}],"cells":[]}""")]
    [InlineData("""{"version":7,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"key":"W"}],"cells":[]}""")]
    [InlineData("""{"version":7,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"key":1}],"cells":[]}""")]
    [InlineData("""{"version":7,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"key":null}],"cells":[]}""")]
    [InlineData("""{"version":7,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["V"],"key":""}],"cells":[]}""")]
    public void A_bad_key_in_a_document_is_refused(string json)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));
    }

    [Fact] // ADR-0058 (amended 2026-10-03): a key of several columns is declared as a list, spelled as the columns are
    public void A_key_of_several_columns_is_declared()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["entity", "TENOR"]);

        var table = sheet.LinkedTables.Single();
        Assert.Equal(["Entity", "Tenor"], table.KeyColumns);
        Assert.Null(table.Key);
        Assert.Empty(sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]).ValueChanges);
    }

    [Theory] // ADR-0058 (amended 2026-10-03): a key column that is not a column, or is named twice, is refused
    [InlineData("Entity", "Desk")]
    [InlineData("Entity", "entity")]
    public void A_bad_key_of_several_columns_is_refused(string first, string second)
    {
        var sheet = NewSheet();

        Assert.Throws<ArgumentException>(() => sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], [first, second]));
    }

    [Fact] // ADR-0058 (amended 2026-10-03): a snapshot in which two rows hold the same Values in every key column is refused, and the table waits
    public void A_repeated_key_of_several_columns_is_refused()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]);
        sheet.Enter("A1", "=SUM(Cds[Spread])");

        sheet.PushLinkedTable("Cds", [[T("ACME"), T("5Y"), N(1)], [T("ACME"), T("10Y"), N(2)], [T("BETA"), T("5Y"), N(3)]]);
        Assert.Equal(6, sheet.Number("A1"));

        var refused = Assert.Throws<RepeatedKeyException>(() =>
            sheet.PushLinkedTable("Cds", [[T("ACME"), T("5Y"), N(1)], [T("ACME"), T("10Y"), N(2)], [T("acme"), T("5y"), N(3)]]));

        Assert.Equal(["Entity", "Tenor"], refused.KeyColumns);
        Assert.Equal([Value.FromText("ACME"), Value.FromText("5Y")], refused.Keys);
        Assert.Equal((0, 2), (refused.FirstRow, refused.SecondRow));
        Assert.Contains("'Entity', 'Tenor'", refused.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
    }

    [Fact] // ADR-0058 (amended 2026-10-03): a row with a blank part of its key has no key, as a blank key is none
    public void A_blank_part_is_no_key()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]);

        sheet.PushLinkedTable("Cds", [[T("ACME"), null, N(1)], [T("ACME"), null, N(2)]]);

        Assert.False(sheet.LinkedTables.Single().IsWaiting);
    }

    [Fact] // ADR-0058 (amended 2026-10-03), ADR-0048: a key of several columns is recorded as a list at version 9, and one column still as its name
    public void A_key_of_several_columns_round_trips()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]);
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Id");

        var json = sheet.ToDocument().ToJson();
        var reopened = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Contains("\"key\":[\"Entity\",\"Tenor\"]", json, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"Id\"", json, StringComparison.Ordinal);
        Assert.Equal(["Entity", "Tenor"], reopened.LinkedTables[0].KeyColumns);
        Assert.Equal("Id", reopened.LinkedTables[1].Key);
    }

    [Theory] // ADR-0048: a key of several columns is not part of version 8, and a list of one column is not one
    [InlineData("""{"version":8,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["A","B"],"key":["A","B"]}],"cells":[]}""")]
    [InlineData("""{"version":9,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["A","B"],"key":["A"]}],"cells":[]}""")]
    [InlineData("""{"version":9,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["A","B"],"key":["A","C"]}],"cells":[]}""")]
    [InlineData("""{"version":9,"culture":"en-US","name":"Sheet1","linkedTables":[{"name":"T","columns":["A","B"],"key":["A",1]}],"cells":[]}""")]
    public void A_bad_key_of_several_columns_in_a_document_is_refused(string json)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));
    }
}
