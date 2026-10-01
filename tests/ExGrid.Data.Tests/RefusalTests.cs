using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A load that meets a value it cannot read fails whole, naming the row and the column
/// (ADR-0063, DA-6).</summary>
public class RefusalTests
{
    [Fact] // ADR-0063: a value of the wrong kind fails the whole build, naming the row and the column
    public void A_value_of_the_wrong_kind_fails_the_build_naming_row_and_column()
    {
        var builder = new SnapshotBuilder<Cell<object?>>().Column("Notional", SnapshotKind.Decimal, c => c.Value);
        Snapshot? built = null;

        var refusal = Assert.Throws<SnapshotException>(() => built = builder.Build([new(1m), new(2m), new("abc"), new(4m)]));

        Assert.Null(built);
        Assert.Equal(3, refusal.Row);
        Assert.Equal("Notional", refusal.Column);
        Assert.Equal("Row 3, column 'Notional': the value 'abc' (String) is not of the column's kind, Decimal.", refusal.Message);
    }

    [Fact] // ADR-0063: an accessor that throws fails the whole build, naming the row and the column
    public void An_accessor_that_throws_fails_the_build_naming_row_and_column()
    {
        var failure = new InvalidOperationException("no price today");
        var builder = new SnapshotBuilder<Trade>()
            .Integer("Id", t => t.Id)
            .Double("Price", t => t.Id == 5 ? throw failure : t.Price);

        var refusal = Assert.Throws<SnapshotException>(() => builder.Build(Trades(10)));

        Assert.Equal(6, refusal.Row);
        Assert.Equal("Price", refusal.Column);
        Assert.Same(failure, refusal.InnerException);
        Assert.Equal("Row 6, column 'Price': reading the value threw InvalidOperationException: no price today", refusal.Message);
    }

    [Fact] // ADR-0063: the row is numbered from one, with its thousands separated, as a person reads it
    public void The_row_is_named_as_a_person_reads_it()
    {
        var builder = new SnapshotBuilder<Cell<object?>>().Column("Notional", SnapshotKind.Decimal, c => c.Value);
        var records = Enumerable.Range(0, 12_345).Select(i => new Cell<object?>(i == 12_344 ? "abc" : 1m)).ToArray();

        var refusal = Assert.Throws<SnapshotException>(() => builder.Build(records));

        Assert.Equal(12_345, refusal.Row);
        Assert.StartsWith("Row 12,345, column 'Notional': ", refusal.Message);
    }

    [Fact] // ADR-0063: an asynchronous build refuses the same way, and yields no Snapshot
    public async Task An_asynchronous_build_refuses_the_same_way()
    {
        var builder = new SnapshotBuilder<Cell<object?>>().Column("When", SnapshotKind.Date, c => c.Value);
        var records = Enumerable.Range(0, 5_000).Select(i => new Cell<object?>(i == 4_000 ? 20260930 : new DateTime(2026, 9, 30))).ToArray();
        Snapshot? built = null;

        var refusal = await Assert.ThrowsAsync<SnapshotException>(async () => built = await builder.BuildAsync(
            records,
            new SnapshotLoadOptions { SliceBudget = TimeSpan.Zero, Yield = () => ValueTask.CompletedTask },
            TestContext.Current.CancellationToken));

        Assert.Null(built);
        Assert.Equal(4_001, refusal.Row);
        Assert.Equal("When", refusal.Column);
    }

    [Fact] // ADR-0063: a Double column refuses a decimal rather than round it, and an Integer column a double
    public void An_inexact_conversion_is_refused()
    {
        var doubles = new SnapshotBuilder<Cell<object?>>().Column("V", SnapshotKind.Double, c => c.Value);
        var integers = new SnapshotBuilder<Cell<object?>>().Column("V", SnapshotKind.Integer, c => c.Value);
        var decimals = new SnapshotBuilder<Cell<object?>>().Column("V", SnapshotKind.Decimal, c => c.Value);

        Assert.Equal(1, Assert.Throws<SnapshotException>(() => doubles.Build([new(0.1m)])).Row);
        Assert.Equal(1, Assert.Throws<SnapshotException>(() => integers.Build([new(1.0)])).Row);
        Assert.Equal(1, Assert.Throws<SnapshotException>(() => decimals.Build([new(0.1)])).Row);
        Assert.Equal(1, Assert.Throws<SnapshotException>(() => decimals.Build([new("1")])).Row);
    }

    [Fact] // ADR-0063: an integer beyond a long's range is refused by name
    public void An_integer_beyond_a_long_is_refused()
    {
        var integers = new SnapshotBuilder<Cell<object?>>().Column("V", SnapshotKind.Integer, c => c.Value);

        var refusal = Assert.Throws<SnapshotException>(() => integers.Build([new(1UL), new(ulong.MaxValue)]));

        Assert.Equal("Row 2, column 'V': the value 18446744073709551615 is outside the range of a 64-bit Integer.", refusal.Message);
    }

    [Fact] // ADR-0063: every reader words a refusal the same way
    public void A_refusal_is_worded_the_same_way_for_every_reader()
    {
        Assert.Equal("Row 12,345, column 'Notional': 'abc' is not a number.", new SnapshotException(12_345, "Notional", "'abc' is not a number.").Message);
        Assert.Equal("Column 'Notional': it is missing from the header.", new SnapshotException(null, "Notional", "it is missing from the header.").Message);
        Assert.Equal("Row 7: the record has 3 fields.", new SnapshotException(7, null, "the record has 3 fields.").Message);
        Assert.Equal("The file is empty.", new SnapshotException(null, null, "The file is empty.").Message);
    }
}
