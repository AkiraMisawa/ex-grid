using System.Globalization;
using Bunit;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Linked Tables through the component (ticket 16, ADR-0049; SH-16's layer 2 half): the Consumer
/// declares and pushes through ExSheet, readers wait with <c>#GETTING_DATA</c> until the first
/// snapshot, a snapshot repaints only the rows whose Values changed, and completion offers the
/// tables' names.
/// </summary>
public class LinkedTableWiringTests : SheetTestContext
{
    private static readonly string[] Columns = ["Id", "Book", "PV"];

    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static IReadOnlyList<Value?>[] Positions(params (string Id, string Book, double PV)[] rows) =>
        [.. rows.Select(r => (IReadOnlyList<Value?>)[Value.FromText(r.Id), Value.FromText(r.Book), Value.FromNumber(r.PV)])];

    private static SheetDocument Readers() => DocumentOf(
        ("A1", "=SUM(Positions[PV])"),
        ("A2", "=XLOOKUP(\"R-2\", Positions[Id], Positions[PV])"),
        ("A3", "=IFERROR(A2, 0)"),
        ("A5", "unrelated"),
        ("A6", "=1+1"));

    [Fact] // ADR-0049: a table never declared is #NAME?
    public void An_undeclared_table_is_name()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));

        Assert.Equal("#NAME?", CellText(cut, "A1"));
    }

    [Fact] // ADR-0049, SH-16: declared and waiting, readers show #GETTING_DATA, and IFERROR does not catch it
    public async Task Declared_readers_wait_and_iferror_does_not_catch_it()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));

        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);

        Assert.Equal("#GETTING_DATA", CellText(cut, "A1"));
        Assert.Equal("#GETTING_DATA", CellText(cut, "A2"));
        Assert.Equal("#GETTING_DATA", CellText(cut, "A3"));
        Assert.True(Assert.Single(cut.Instance.LinkedTables).IsWaiting);
    }

    [Fact] // ADR-0049, SH-16: SUM over a column and XLOOKUP by key read the pushed snapshot
    public async Task Sum_and_xlookup_read_the_snapshot()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);

        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 100), ("R-2", "FX", 250)));

        Assert.Equal("350", CellText(cut, "A1"));
        Assert.Equal("250", CellText(cut, "A2"));
        Assert.Equal("250", CellText(cut, "A3"));
    }

    [Fact] // ADR-0049: a newer snapshot replaces the last whole
    public async Task A_newer_snapshot_replaces_the_last()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 100), ("R-2", "FX", 250)));

        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-2", "FX", 10)));

        Assert.Equal("10", CellText(cut, "A1"));
        Assert.Equal("10", CellText(cut, "A2"));
    }

    [Fact] // ADR-0049/0003, SH-4: a snapshot repaints only the rows of its readers; every other row skips its render
    public async Task A_snapshot_repaints_only_the_readers_rows()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        var before = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => r.Instance.Row);

        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-2", "FX", 5)));

        foreach (var row in cut.FindComponents<ExGridRow<SheetRow>>())
        {
            if (row.Instance.RowIndex is 0 or 1 or 2) Assert.NotSame(before[row.Instance.RowIndex], row.Instance.Row);
            else Assert.Same(before[row.Instance.RowIndex], row.Instance.Row);
        }
    }

    [Fact] // ADR-0049/0048: a declaration is recorded in the Sheet Document and raised; a snapshot is not; neither is an undo step
    public async Task A_declaration_is_raised_and_a_snapshot_is_not()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));

        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        Assert.Equal("Positions", Assert.Single(Assert.Single(raised).LinkedTables).Name);
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 1)));

        Assert.Single(raised);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0049/0051: completion offers the tables' names beside the functions
    public async Task Completion_offers_the_table_names()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "=");

        await TypeAsync(cut, "=SUM(Po");

        Assert.Equal(["Positions"], cut.FindAll(".ex-completion .ex-completion-item").Select(i => i.TextContent));
        await PressAsync(cut, "Tab");
        Assert.Equal("=SUM(Positions", EditorText(cut));
    }

    [Fact] // ADR-0049: a push to a table never declared is refused by name, and nothing changes
    public async Task A_push_to_an_undeclared_table_is_refused()
    {
        var cut = RenderSheet();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 1))));

        Assert.Contains("Positions", refused.Message);
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: the key is declared with the table, readable from the Sheet, recorded in the Sheet Document, and another key is another declaration
    public async Task A_key_is_declared_recorded_and_raised()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));

        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns, key: "Id");
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns, key: "id");

        Assert.Equal("Id", Assert.Single(cut.Instance.LinkedTables).Key);
        Assert.Equal("Id", Assert.Single(Assert.Single(raised).LinkedTables).Key);
        Assert.Contains("\"key\":\"Id\"", cut.Instance.ToDocument().ToJson(), StringComparison.Ordinal);

        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);

        Assert.Equal(2, raised.Count);
        Assert.Null(Assert.Single(raised[1].LinkedTables).Key);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a repeated key refuses the push by name; every reader shows #GETTING_DATA, never the earlier snapshot's Value or IFERROR's 0
    public async Task A_repeated_key_refuses_the_push_and_readers_wait()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Readers()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns, key: "Id");
        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 100), ("R-2", "FX", 250)));
        Assert.Equal("350", CellText(cut, "A1"));

        var refused = await Assert.ThrowsAsync<RepeatedKeyException>(() =>
            cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 100), ("r-1", "FX", 250))));

        Assert.Contains("'Positions'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'Id'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("\"R-1\"", refused.Message, StringComparison.Ordinal);
        Assert.Equal("#GETTING_DATA", CellText(cut, "A1"));
        Assert.Equal("#GETTING_DATA", CellText(cut, "A2"));
        Assert.Equal("#GETTING_DATA", CellText(cut, "A3"));
        Assert.True(Assert.Single(cut.Instance.LinkedTables).IsWaiting);

        await cut.Instance.PushLinkedTableAsync("Positions", Positions(("R-1", "Rates", 1), ("R-2", "FX", 2)));

        Assert.Equal("3", CellText(cut, "A1"));
        Assert.Equal("2", CellText(cut, "A3"));
    }
}
