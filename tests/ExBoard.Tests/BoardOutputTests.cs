using Bunit;
using ExGrid.Data;
using ExGrid.Selection;
using BoardComponent = ExBoard.Components.ExBoard;

namespace ExBoard.Tests;

public sealed class BoardOutputTests : BunitContext
{
    private const string Views = """
        "views": [
          {"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "id"}, {"column": "desk"}, {"column": "pnl", "format": "#,##0"}]}},
          {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}],
        "layout": [{"views": ["blotter", "byDesk"]}]
        """;

    private static string Board(string outputs) => $$$"""
        {"version": 1,
         "inputs": {"trades": {"columns": [{"name": "id", "type": "text"}, {"name": "desk", "type": "text"},
                                           {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
         {{{Views}}}, "outputs": {{{{outputs}}}}}
        """;

    private const string Chosen = """
        "chosen": {"from": "blotter", "on": ["id", "day"], "confirm": "Book these trades"}
        """;

    private static Snapshot Trades(BoardReading board) => board.Input("trades")!.FromJson("""
        [{"id": "T-1", "desk": "Rates", "pnl": 100, "day": "2026-10-19"},
         {"id": "T-2", "desk": "FX", "pnl": -50.5, "day": "2026-10-20"},
         {"id": "T-3", "desk": "Rates", "pnl": 75, "day": null}]
        """);

    private static GridSelection Rows(params int[] rows)
    {
        var extent = new GridExtent(10, 3);
        var selection = GridSelection.Empty.Click(new CellPosition(rows[0], 1), extent);
        foreach (var row in rows.Skip(1))
            selection = selection.ToggleRange(new CellPosition(row, 1), extent);
        return selection;
    }

    [Fact] // ADR-0340 Q6: an output names a grid, the columns it hands on, and the confirm that raises it
    public void An_output_is_read_with_its_confirm()
    {
        var output = Assert.Single(BoardSpec.Read(Board(Chosen)).Outputs);

        Assert.Equal(new BoardOutputDeclaration("chosen", "blotter", output.On, "Book these trades"), output);
        Assert.Equal(["id", "day"], output.On);
    }

    public static TheoryData<string, string, string> Refusals => new()
    {
        { """ "chosen": {"from": "blotter", "on": ["id"]} """, "outputs.chosen.confirm", "Output 'chosen' is raised by a confirm action, whose label is its 'confirm'." },
        { """ "chosen": {"from": "byDesk", "on": ["desk"], "confirm": "Go"} """, "outputs.chosen.from", "Output 'chosen' is taken from view 'byDesk', a pivot; an output is taken from a grid's selected rows." },
        { """ "chosen": {"from": "book", "on": ["id"], "confirm": "Go"} """, "outputs.chosen.from", "Output 'chosen' is taken from view 'book', which the Board does not have." },
        { """ "chosen": {"from": "blotter", "on": ["book"], "confirm": "Go"} """, "outputs.chosen.on[0]", "Output 'chosen' hands on column 'book', which input 'trades' does not declare." },
        { """ "chosen": {"from": "blotter", "on": ["id"], "confirm": "Go", "when": "selected"} """, "outputs.chosen.when", "An output has no member 'when'." },
    };

    [Theory, MemberData(nameof(Refusals))] // ADR-0340 Q6: an output that cannot be raised is refused by path
    public void An_output_that_cannot_be_raised_is_refused(string outputs, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoardSpec.Read(Board(outputs)));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
    }

    [Fact] // ADR-0340 Q6: a selection raises nothing; a confirm hands on its values with the version
    public void A_confirm_hands_on_the_selected_values_and_their_version()
    {
        var reading = BoardSpec.Read(Board(Chosen));
        var board = BoundBoard.Bind(reading, new Dictionary<string, Snapshot> { ["trades"] = Trades(reading) });

        Assert.False(board.CanConfirm("chosen"));
        Assert.Null(board.Confirm("chosen"));
        board.Select("blotter", Rows(0, 2));
        Assert.True(board.CanConfirm("chosen"));

        var output = board.Confirm("chosen")!;
        Assert.Equal(("chosen", "trades", 0L), (output.Name, output.Input, output.SourceVersion));
        Assert.Equal("""{"output":"chosen","input":"trades","version":0,"on":["id","day"],"values":[["T-1","2026-10-19"],["T-3",null]]}""", output.ToJson());

        board.Select("blotter", GridSelection.Empty);
        Assert.False(board.CanConfirm("chosen"));
        Assert.Throws<ArgumentException>(() => board.Confirm("booked"));
    }

    [Fact] // ADR-0340 Q6 / spine rule 6: the rows handed on are taken against the version they were chosen in
    public void An_output_takes_its_rows_only_from_the_version_it_was_taken_against()
    {
        var reading = BoardSpec.Read(Board(Chosen));
        var trades = Trades(reading);
        var board = BoundBoard.Bind(reading, new Dictionary<string, Snapshot> { ["trades"] = trades });
        board.Select("blotter", Rows(1));
        var output = board.Confirm("chosen")!;

        var taken = output.Take(trades);
        Assert.Equal(1, taken.RowCount);
        Assert.Equal(-50.5m, taken.ValueAt(taken.Rows[0], taken["pnl"]));

        var builder = new SnapshotColumnsBuilder { Version = 1 };
        builder.Text("id").Append("T-2");
        builder.Date("day").Append(new DateOnly(2026, 10, 20));
        var refused = Assert.Throws<BoardRefusedException>(() => output.Take(builder.Build()));
        Assert.Equal("outputs.chosen", refused.Path);
        Assert.StartsWith("Output 'chosen' was taken against version 0 of input 'trades', and the data is version 1", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0340 Q6: the confirm is drawn under its grid, disabled until the grid selects, and raises the output
    public async Task The_confirm_raises_the_output()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("WebAssembly", isInteractive: true));
        var reading = BoardSpec.Read(Board(Chosen));
        BoardOutput? raised = null;
        var cut = Render<BoardComponent>(ps => ps
            .Add(b => b.Board, reading)
            .Add(b => b.Data, new Dictionary<string, Snapshot> { ["trades"] = Trades(reading) })
            .Add(b => b.OnOutput, output => raised = output));
        var confirm = () => cut.Find("[data-ex-view=blotter] button.ex-board-confirm[data-ex-output=chosen]");

        Assert.Equal("Book these trades", confirm().TextContent);
        Assert.True(confirm().HasAttribute("disabled"));

        var grid = cut.FindComponent<ExGrid.Components.ExGrid<ExGrid.ShapedRow>>();
        await cut.InvokeAsync(() => grid.Instance.SelectionChanged.InvokeAsync(Rows(1)));
        Assert.False(confirm().HasAttribute("disabled"));
        Assert.Null(raised);

        await cut.InvokeAsync(() => confirm().Click());
        Assert.Equal([["T-2", new DateTime(2026, 10, 20)]], raised!.Condition.Values);
    }
}
