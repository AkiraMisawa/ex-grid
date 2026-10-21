using System.Text.Json;
using Xunit;

namespace ExGrid.Data.Tests;

public sealed class BoardInputTests
{
    private static BoardInput Trades() => new("trades",
    [
        new("desk", SnapshotKind.Text),
        new("notional", SnapshotKind.Decimal),
        new("rate", SnapshotKind.Double),
        new("count", SnapshotKind.Integer),
        new("tradeDate", SnapshotKind.Date),
        new("hedged", SnapshotKind.Boolean),
    ]);

    private static BoardInput ReadDeclaration(string json)
    {
        using var document = JsonDocument.Parse(json);
        return BoardInput.Read("trades", document.RootElement, "inputs.trades");
    }

    [Fact] // ADR-0341: a declaration names each column's type
    public void A_declaration_reads_each_column_and_its_kind_in_order()
    {
        var input = ReadDeclaration("""{"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}]}""");

        Assert.Equal("trades", input.Name);
        Assert.Equal([new DataShapeColumn("desk", SnapshotKind.Text), new DataShapeColumn("pnl", SnapshotKind.Decimal)], input.Columns);
    }

    [Theory] // ADR-0341: types are never inferred, and a declaration that does not say is refused by path
    [InlineData("""{"columns": [{"name": "pnl"}]}""", "inputs.trades.columns[0].type")]
    [InlineData("""{"columns": [{"name": "pnl", "type": "money"}]}""", "inputs.trades.columns[0].type")]
    [InlineData("""{"columns": [{"name": "a", "type": "text"}, {"name": "a", "type": "text"}]}""", "inputs.trades.columns[1].name")]
    [InlineData("""{"columns": []}""", "inputs.trades.columns")]
    [InlineData("""{"columns": [{"name": "a", "type": "text", "format": "0"}]}""", "inputs.trades.columns[0].format")]
    [InlineData("""{"rows": []}""", "inputs.trades.rows")]
    public void A_declaration_that_does_not_say_its_types_is_refused_by_path(string json, string path)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => ReadDeclaration(json));
        Assert.Equal(path, refused.Path);
    }

    [Fact] // ADR-0341: binding checks the source's shape and names the input and the column
    public void A_source_missing_a_declared_column_is_refused_naming_the_input_and_the_column()
    {
        var shape = new DataShape([new("desk", SnapshotKind.Text)]);
        var input = new BoardInput("trades", [new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal)]);

        var refused = Assert.Throws<BoardRefusedException>(() => input.Check(shape));
        Assert.Equal("Input 'trades' declares column 'pnl', which its source does not have.", refused.Message);
    }

    [Fact] // ADR-0341: a change upstream is caught on the run that meets it, not drawn
    public void A_source_holding_a_column_of_another_kind_is_refused()
    {
        var shape = new DataShape([new("pnl", SnapshotKind.Double)]);
        var input = new BoardInput("trades", [new("pnl", SnapshotKind.Decimal)]);

        var refused = Assert.Throws<BoardRefusedException>(() => input.Check(shape));
        Assert.Equal("Input 'trades', column 'pnl': declared decimal, and its source holds double.", refused.Message);
    }

    [Fact] // ADR-0341: a column the input does not declare is no mismatch
    public void A_source_with_more_columns_than_declared_binds()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Text("desk").Append("Rates");
        builder.Double("extra").Append(1.0);
        var snapshot = builder.Build();

        Assert.Same(snapshot, new BoardInput("trades", [new("desk", SnapshotKind.Text)]).Bind(snapshot));
    }

    [Fact] // ADR-0341: JSON becomes a Snapshot by the declared types
    public void Json_rows_become_a_snapshot_by_the_declared_kinds()
    {
        var snapshot = Trades().FromJson("""
            [
              {"desk": "Rates", "notional": 1000000.10, "rate": 1.08734, "count": 3, "tradeDate": "2026-10-20", "hedged": true, "ignored": [1]},
              {"desk": null, "notional": "12345678901234567890.123456789", "rate": "NaN", "count": "-7", "tradeDate": "2026-10-20T09:30:15.5"}
            ]
            """);

        Assert.Equal(2, snapshot.RowCount);
        Assert.Equal(["desk", "notional", "rate", "count", "tradeDate", "hedged"], snapshot.Columns.Select(c => c.Name));
        var first = snapshot.Rows[0];
        var second = snapshot.Rows[1];
        Assert.Equal(1000000.1m, snapshot.ValueAt(first, snapshot["notional"]));
        Assert.Equal(1.08734, snapshot.ValueAt(first, snapshot["rate"]));
        Assert.Equal(3L, snapshot.ValueAt(first, snapshot["count"]));
        Assert.Equal(new DateTime(2026, 10, 20), snapshot.ValueAt(first, snapshot["tradeDate"]));
        Assert.Equal(true, snapshot.ValueAt(first, snapshot["hedged"]));
        Assert.Null(snapshot.ValueAt(second, snapshot["desk"]));
        Assert.Equal(12345678901234567890.123456789m, snapshot.ValueAt(second, snapshot["notional"]));
        Assert.True(double.IsNaN((double)snapshot.ValueAt(second, snapshot["rate"])!));
        Assert.Equal(-7L, snapshot.ValueAt(second, snapshot["count"]));
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 15, 500), snapshot.ValueAt(second, snapshot["tradeDate"]));
        Assert.Null(snapshot.ValueAt(second, snapshot["hedged"]));
    }

    [Theory] // ADR-0341: a value that does not convert is refused by input, column and row, never dropped or read as zero
    [InlineData("""[{"notional": "abc"}]""", "trades[0].notional", "Input 'trades', row 1, column 'notional': \"abc\" is not a decimal.")]
    [InlineData("""[{"desk": "a"}, {"desk": 12}]""", "trades[1].desk", "Input 'trades', row 2, column 'desk': 12 is not text.")]
    [InlineData("""[{"count": 1.5}]""", "trades[0].count", "Input 'trades', row 1, column 'count': 1.5 is not an integer.")]
    [InlineData("""[{"tradeDate": "2026-10-20T09:30:00Z"}]""", "trades[0].tradeDate", "Input 'trades', row 1, column 'tradeDate': \"2026-10-20T09:30:00Z\" is not a date.")]
    [InlineData("""[{"tradeDate": "20/10/2026"}]""", "trades[0].tradeDate", "Input 'trades', row 1, column 'tradeDate': \"20/10/2026\" is not a date.")]
    [InlineData("""[{"hedged": "yes"}]""", "trades[0].hedged", "Input 'trades', row 1, column 'hedged': \"yes\" is not a boolean.")]
    [InlineData("""[{"rate": "1.5"}]""", "trades[0].rate", "Input 'trades', row 1, column 'rate': \"1.5\" is not a double.")]
    [InlineData("""[1]""", "trades[0]", "Input 'trades', row 1: is not an object.")]
    public void A_value_that_does_not_convert_is_refused_by_row_and_column(string json, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => Trades().FromJson(json));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
    }

    [Fact] // ADR-0341: types are never inferred from rows
    public void A_first_page_of_nulls_still_gives_the_declared_kinds()
    {
        var snapshot = Trades().FromJson("""[{"notional": null}, {}]""");

        Assert.Equal(SnapshotKind.Decimal, snapshot["notional"].Kind);
        Assert.Equal(2, snapshot.RowCount);
        Assert.Null(snapshot.ValueAt(snapshot.Rows[0], snapshot["notional"]));
    }

    [Fact] // ADR-0341: a member written twice in a row is refused, never settled by whichever came last
    public void A_row_naming_a_column_twice_is_refused()
    {
        var refused = Assert.Throws<BoardRefusedException>(() => Trades().FromJson("""[{"notional": 1, "notional": 1000000}]"""));
        Assert.StartsWith("The data of input 'trades' is not JSON", refused.Message, StringComparison.Ordinal);
    }

    private sealed class Answering(DataShape? shape) : IShapedSource
    {
        public int Asked { get; private set; }

        public ValueTask<DataShape?> ShapeAsync(CancellationToken cancellationToken = default)
        {
            Asked++;
            return ValueTask.FromResult(shape);
        }
    }

    [Fact] // ADR-0341: a source held where the data lives is checked by the shape it answers, before any row
    public async Task A_source_is_checked_by_the_shape_it_answers()
    {
        var input = new BoardInput("trades", [new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal)]);
        var shape = new DataShape([new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal), new("book", SnapshotKind.Text)]);
        var source = new Answering(shape);

        Assert.Same(shape, await input.CheckAsync(source, TestContext.Current.CancellationToken));
        Assert.Equal(1, source.Asked);

        var other = await Assert.ThrowsAsync<BoardRefusedException>(async () =>
            await input.CheckAsync(new Answering(new DataShape([new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Double)])), TestContext.Current.CancellationToken));
        Assert.Equal("Input 'trades', column 'pnl': declared decimal, and its source holds double.", other.Message);
    }

    [Fact] // ADR-0341: a source that says no shape is refused, its kinds never guessed from its rows
    public async Task A_source_that_says_no_shape_is_refused()
    {
        var input = new BoardInput("trades", [new("desk", SnapshotKind.Text)]);

        var refused = await Assert.ThrowsAsync<BoardRefusedException>(async () => await input.CheckAsync(new Answering(null), TestContext.Current.CancellationToken));

        Assert.Equal("trades", refused.Path);
        Assert.Equal("Input 'trades' is bound to a source that does not say its shape, and its kinds are not guessed from its rows.", refused.Message);
    }
}
