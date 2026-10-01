using System.Data;
using System.Data.Common;
using System.Globalization;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A Snapshot read from a <see cref="DbDataReader"/> (ADR-0063, DA-10): each column by its own type,
/// <see cref="DBNull"/> a Blank, a type outside ADR-0063's list refused by name unless the Consumer
/// declares how to read it; columns chosen, renamed and captioned. The tests read through
/// <see cref="DataTableReader"/>, the framework's own <see cref="DbDataReader"/>, so they need no
/// database.
/// </summary>
public class DataReaderTests
{
    private static readonly DateTime Ten = new(2026, 9, 30, 10, 0, 0);

    /// <summary>A table with a column of every type a Snapshot reads by itself, a row of values and a
    /// row of nulls.</summary>
    private static DataTable EveryType()
    {
        var table = new DataTable();
        (string Name, Type Type, object Value)[] columns =
        [
            ("String", typeof(string), "FX"),
            ("Char", typeof(char), 'c'),
            ("Decimal", typeof(decimal), 1.50m),
            ("Double", typeof(double), 0.1),
            ("Single", typeof(float), 0.5f),
            ("Int64", typeof(long), long.MinValue),
            ("Int32", typeof(int), -5),
            ("Int16", typeof(short), (short)-6),
            ("Byte", typeof(byte), (byte)7),
            ("SByte", typeof(sbyte), (sbyte)-8),
            ("UInt16", typeof(ushort), (ushort)9),
            ("UInt32", typeof(uint), uint.MaxValue),
            ("UInt64", typeof(ulong), (ulong)long.MaxValue),
            ("DateTime", typeof(DateTime), new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc)),
            ("DateOnly", typeof(DateOnly), new DateOnly(2026, 9, 30)),
            ("DateTimeOffset", typeof(DateTimeOffset), new DateTimeOffset(Ten, TimeSpan.FromHours(9))),
            ("TimeOnly", typeof(TimeOnly), new TimeOnly(14, 5)),
            ("Boolean", typeof(bool), true),
        ];
        foreach (var (name, type, _) in columns)
            table.Columns.Add(name, type);
        table.Rows.Add([.. columns.Select(c => c.Value)]);
        table.Rows.Add([.. columns.Select(_ => (object)DBNull.Value)]);
        return table;
    }

    private static async Task<Snapshot> Build(SnapshotDataReaderBuilder builder, DataTable table, SnapshotLoadOptions? options = null, string[]? names = null, Func<int, int, Exception?>? fault = null)
    {
        using var reader = new TestReader(table.CreateDataReader(), names) { Fault = fault };
        return await builder.BuildAsync(reader, options, TestContext.Current.CancellationToken);
    }

    private static async Task<SnapshotException> Refusal(SnapshotDataReaderBuilder builder, DataTable table, string[]? names = null, Func<int, int, Exception?>? fault = null)
    {
        Snapshot? built = null;
        var refusal = await Assert.ThrowsAsync<SnapshotException>(async () => built = await Build(builder, table, null, names, fault));
        Assert.Null(built);
        return refusal;
    }

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
            table.Columns.Add(name, type);
        return table;
    }

    [Fact] // ADR-0063: each column is read by its own type, into the kind ADR-0063 gives it
    public async Task Each_column_is_read_by_its_own_type()
    {
        var snapshot = await Build(new SnapshotDataReaderBuilder(), EveryType());

        Assert.Equal(
            [
                SnapshotKind.Text, SnapshotKind.Text, SnapshotKind.Decimal, SnapshotKind.Double, SnapshotKind.Double,
                SnapshotKind.Integer, SnapshotKind.Integer, SnapshotKind.Integer, SnapshotKind.Integer, SnapshotKind.Integer,
                SnapshotKind.Integer, SnapshotKind.Integer, SnapshotKind.Integer,
                SnapshotKind.Date, SnapshotKind.Date, SnapshotKind.Date, SnapshotKind.Date, SnapshotKind.Boolean,
            ],
            snapshot.Columns.Select(c => c.Kind));
        Assert.Equal(EveryType().Columns.Cast<DataColumn>().Select(c => c.ColumnName), snapshot.Columns.Select(c => c.Name));
        Assert.Equal(snapshot.Columns.Select(c => c.Name), snapshot.Columns.Select(c => c.Caption));
        object?[] first = [.. snapshot.Columns.Select(c => snapshot.ValueAt(snapshot.Rows[0], c))];
        Assert.Equal(
            [
                "FX", "c", 1.5m, 0.1, 0.5, long.MinValue, -5L, -6L, 7L, -8L, 9L, (long)uint.MaxValue, long.MaxValue,
                Ten, new DateTime(2026, 9, 30), Ten, new DateTime(1, 1, 1, 14, 5, 0), true,
            ],
            first);
    }

    [Fact] // ADR-0063: a date is the clock it shows: a DateTime's Kind ignored, a DateTimeOffset's offset dropped, a DateOnly's midnight
    public async Task A_date_is_the_clock_it_shows()
    {
        var snapshot = await Build(new SnapshotDataReaderBuilder(), EveryType());

        Assert.Equal(Ten.Ticks, ((DateTime)Values(snapshot, "DateTime")[0]!).Ticks);
        Assert.Equal(DateTimeKind.Unspecified, ((DateTime)Values(snapshot, "DateTime")[0]!).Kind);
        Assert.Equal(Ten.Ticks, ((DateTime)Values(snapshot, "DateTimeOffset")[0]!).Ticks);
        Assert.Equal(new DateTime(2026, 9, 30).Ticks, ((DateTime)Values(snapshot, "DateOnly")[0]!).Ticks);
    }

    [Fact] // ADR-0063: DBNull is a Blank in every kind
    public async Task Dbnull_is_a_blank_in_every_kind()
    {
        var snapshot = await Build(new SnapshotDataReaderBuilder(), EveryType());

        foreach (var column in snapshot.Columns)
        {
            Assert.True(snapshot.IsBlank(snapshot.Rows[1], column), column.Name);
            Assert.False(snapshot.IsBlank(snapshot.Rows[0], column), column.Name);
        }
    }

    [Fact] // ADR-0063: a column of a type outside ADR-0063's list is refused by name, before a row is read, with every such column named
    public async Task A_column_of_another_type_is_refused_by_name()
    {
        var table = Table(("Id", typeof(long)), ("Book", typeof(Guid)), ("Blob", typeof(byte[])), ("Held", typeof(TimeSpan)));
        table.Rows.Add(1L, Guid.NewGuid(), new byte[] { 1 }, TimeSpan.FromHours(1));

        var refusal = await Refusal(new SnapshotDataReaderBuilder(), table);

        Assert.Null(refusal.Row);
        Assert.Equal("Book", refusal.Column);
        Assert.Equal("Column 'Book': its type, Guid, is not one a Snapshot reads by itself; declare its kind and how to read it. So are 'Blob' (byte[]) and 'Held' (TimeSpan).", refusal.Message);
        // Chosen by name, it is refused all the same.
        Assert.Equal("Column 'Held': its type, TimeSpan, is not one a Snapshot reads by itself; declare its kind and how to read it.",
            (await Refusal(new SnapshotDataReaderBuilder().Column("Id").Column("Held"), table)).Message);
    }

    [Fact] // ADR-0063: a column of another type is read once the Consumer declares its kind and how to read it
    public async Task A_column_of_another_type_is_read_as_declared()
    {
        var book = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var table = Table(("Id", typeof(long)), ("Book", typeof(Guid)), ("Blob", typeof(byte[])), ("Held", typeof(TimeSpan)));
        table.Rows.Add(1L, book, new byte[] { 0xCA, 0xFE }, TimeSpan.FromMinutes(90));
        table.Rows.Add(2L, DBNull.Value, DBNull.Value, DBNull.Value);

        var snapshot = await Build(
            new SnapshotDataReaderBuilder()
                .Column("Id")
                .Text<Guid>("Book", g => g.ToString())
                .Text<byte[]>("Blob", b => Convert.ToHexString(b))
                .Double<TimeSpan>("Held", t => t.TotalHours, name: "HeldHours"),
            table);

        Assert.Equal([book.ToString(), null], Values(snapshot, "Book"));
        Assert.Equal(["CAFE", null], Values(snapshot, "Blob"));
        Assert.Equal([1.5, null], Values(snapshot, "HeldHours"));
    }

    [Fact] // ADR-0063: a declared conversion reads a column otherwise than by its own type, into every kind, and a null it returns is a Blank
    public async Task A_declared_conversion_reads_into_every_kind()
    {
        var table = Table(("Code", typeof(string)));
        table.Rows.Add("7");
        table.Rows.Add("x");

        var snapshot = await Build(
            new SnapshotDataReaderBuilder()
                .Text<string>("Code", s => s.ToUpperInvariant(), name: "Upper")
                .Decimal<string>("Code", s => s == "x" ? null : decimal.Parse(s, CultureInfo.InvariantCulture) / 4, name: "Quarter")
                .Integer<string>("Code", s => s.Length, name: "Length")
                .Date<string>("Code", s => s == "x" ? null : new DateTime(2026, 1, int.Parse(s, CultureInfo.InvariantCulture)), name: "Day")
                .Boolean<string>("Code", s => s == "x", name: "IsX"),
            table);

        Assert.Equal(["7", "X"], Values(snapshot, "Upper"));
        Assert.Equal([1.75m, null], Values(snapshot, "Quarter"));
        Assert.Equal([1L, 1L], Values(snapshot, "Length"));
        Assert.Equal([new DateTime(2026, 1, 7), null], Values(snapshot, "Day"));
        Assert.Equal([false, true], Values(snapshot, "IsX"));
    }

    [Fact] // ADR-0063: columns can be chosen, renamed and captioned, and are held in the order declared
    public async Task Columns_are_chosen_renamed_and_captioned()
    {
        var snapshot = await Build(
            new SnapshotDataReaderBuilder()
                .Column("Decimal", name: "Notional", caption: "Notional, USD")
                .Column("String", caption: "Desk")
                .Column("Int32", name: "Qty"),
            EveryType());

        Assert.Equal(["Notional", "String", "Qty"], snapshot.Columns.Select(c => c.Name));
        Assert.Equal(["Notional, USD", "Desk", "Qty"], snapshot.Columns.Select(c => c.Caption));
        Assert.Equal([1.5m, null], Values(snapshot, "Notional"));
    }

    [Fact] // ADR-0063: a declared column missing from the reader is refused by name, before a row is read
    public async Task A_declared_column_missing_from_the_reader_is_refused_by_name()
    {
        var table = Table(("trade_id", typeof(long)), ("desk", typeof(string)));
        table.Rows.Add(1L, "FX");

        var refusal = await Refusal(new SnapshotDataReaderBuilder().Column("Desk").Column("Book").Column("trade_id", name: "Id"), table);

        Assert.Equal("Desk", refusal.Column);
        Assert.Equal("Column 'Desk': it is missing from the reader, and so is 'Book'; the reader has 'desk'.", refusal.Message);
        Assert.Equal("Column 'Id': its field, 'TradeId', is missing from the reader.",
            (await Refusal(new SnapshotDataReaderBuilder().Column("TradeId", name: "Id"), table)).Message);
    }

    [Fact] // ADR-0063: a reader whose columns cannot all be told apart by name is refused by name, unless the columns read are declared
    public async Task Columns_that_cannot_be_told_apart_are_refused()
    {
        var table = Table(("A", typeof(long)), ("B", typeof(long)), ("C", typeof(string)));
        table.Rows.Add(1L, 2L, "x");
        string[] twice = ["Id", "Id", "Desk"];

        Assert.Equal("Column 'Id': the reader has two columns of this name, and a Snapshot's names are unique; name them apart in the query, or declare the columns to read.",
            (await Refusal(new SnapshotDataReaderBuilder(), table, twice)).Message);
        Assert.Equal("The reader's column 1 has no name; name it in the query, or declare the columns to read.",
            (await Refusal(new SnapshotDataReaderBuilder(), table, ["Id", "", "Desk"])).Message);
        // Declared, the columns read are told apart, and one that stands twice is refused by name.
        Assert.Equal(["x"], Values(await Build(new SnapshotDataReaderBuilder().Column("Desk"), table, names: twice), "Desk"));
        Assert.Equal("Column 'Id': the reader has two columns named 'Id', so which one it is cannot be told; name them apart in the query.",
            (await Refusal(new SnapshotDataReaderBuilder().Column("Id"), table, twice)).Message);
    }

    [Fact] // ADR-0063: a value the driver cannot give fails the whole load, naming the row and the column
    public async Task A_value_the_driver_cannot_give_fails_the_load_by_row_and_column()
    {
        var table = Table(("Id", typeof(long)), ("Notional", typeof(decimal)));
        for (var i = 1; i <= 300; i++)
            table.Rows.Add((long)i, i * 1.5m);

        // As a driver throws for a NUMBER(38) beyond decimal's range.
        var refusal = await Refusal(new SnapshotDataReaderBuilder(), table, fault: (row, ordinal) => row == 257 && ordinal == 1 ? new OverflowException("Value was either too large or too small for a Decimal.") : null);

        Assert.Equal("Row 257, column 'Notional': reading the value threw OverflowException: Value was either too large or too small for a Decimal.", refusal.Message);
        Assert.Equal(257, refusal.Row);
        Assert.IsType<OverflowException>(refusal.InnerException);
    }

    [Fact] // ADR-0063: a Record Key is declared over a Text or an Integer column, and a key carried twice is refused, naming the key and the rows
    public async Task A_record_key_keys_the_snapshot_and_refuses_a_key_carried_twice()
    {
        var table = Table(("Id", typeof(int)), ("Desk", typeof(string)));
        table.Rows.Add(7, "FX");
        table.Rows.Add(9, "Rates");

        var snapshot = await Build(new SnapshotDataReaderBuilder().Key("Id"), table);
        Assert.Equal("Id", snapshot.RecordKey?.Name);
        Assert.Equal(["Rates"], Values(snapshot.Apply(ChangeBatch.Of(removedKeys: [7L])).After, "Desk"));

        table.Rows.Add(7, "Credit");
        var refusal = await Refusal(new SnapshotDataReaderBuilder().Key("Id"), table);
        Assert.Equal("Row 3, column 'Id': the Record Key 7 is already carried by row 1.", refusal.Message);
        Assert.Equal(7L, refusal.Key);
    }

    [Fact] // ADR-0063: a Blank Record Key is refused, naming the row
    public async Task A_blank_record_key_is_refused()
    {
        var table = Table(("Id", typeof(string)));
        table.Rows.Add("a");
        table.Rows.Add(DBNull.Value);

        var refusal = await Refusal(new SnapshotDataReaderBuilder().Key("Id"), table);

        Assert.Equal(2, refusal.Row);
        Assert.Equal("Id", refusal.Column);
    }

    [Fact] // ADR-0063: a Record Key the reader gives as another kind than Text or Integer is refused by name
    public async Task A_record_key_of_another_kind_is_refused()
    {
        var table = Table(("Id", typeof(decimal)));
        table.Rows.Add(1m);

        Assert.Equal("Column 'Id': the reader gives the Record Key as Decimal; only a Text or an Integer column can be the Record Key.",
            (await Refusal(new SnapshotDataReaderBuilder().Key("Id"), table)).Message);
        Assert.Equal("Column 'Ref': the Record Key is not a column of the reader.",
            (await Refusal(new SnapshotDataReaderBuilder().Key("Ref"), table)).Message);
        // Named before any column is declared, and then not declared.
        Assert.Equal("Column 'Id': the Record Key is not among the columns declared.",
            (await Refusal(new SnapshotDataReaderBuilder().Key("Id").Decimal<decimal>("Id", d => d, name: "Amount"), table)).Message);
        Assert.Throws<ArgumentException>(() => new SnapshotDataReaderBuilder().Decimal<decimal>("Id", d => d).Key("Id"));
        Assert.Throws<ArgumentException>(() => new SnapshotDataReaderBuilder().Column("Id").Key("Other"));
    }

    [Fact] // ADR-0063: an unsigned integer beyond a 64-bit Integer is refused by row and column rather than wrapped
    public async Task An_unsigned_integer_beyond_64_bits_is_refused()
    {
        var table = Table(("Big", typeof(ulong)));
        table.Rows.Add((ulong)long.MaxValue);
        table.Rows.Add(ulong.MaxValue);

        var refusal = await Refusal(new SnapshotDataReaderBuilder(), table);

        Assert.Equal("Row 2, column 'Big': the value 18446744073709551615 is outside the range of a 64-bit Integer.", refusal.Message);
    }

    [Fact] // ADR-0063: a value that cannot be read fails the whole load, naming the row and the column
    public async Task A_value_that_cannot_be_read_fails_the_load_by_row_and_column()
    {
        var table = Table(("Code", typeof(string)));
        table.Rows.Add("1");
        table.Rows.Add("one");

        var refusal = await Refusal(new SnapshotDataReaderBuilder().Integer<string>("Code", s => long.Parse(s, CultureInfo.InvariantCulture)), table);

        Assert.Equal(2, refusal.Row);
        Assert.Equal("Code", refusal.Column);
        Assert.StartsWith("Row 2, column 'Code': reading the value threw FormatException: ", refusal.Message, StringComparison.Ordinal);
        Assert.IsType<FormatException>(refusal.InnerException);
    }

    [Fact] // ADR-0063: a build works in slices, yields between them and reports the rows read
    public async Task A_build_yields_between_slices_and_reports_rows()
    {
        var table = Table(("Id", typeof(long)), ("Desk", typeof(string)));
        for (var i = 0; i < 10_000; i++)
            table.Rows.Add((long)i, "D" + (i % 7).ToString(CultureInfo.InvariantCulture));
        var yields = 0;
        var reports = new List<SnapshotProgress>();
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
            Progress = new Told(reports),
        };

        var snapshot = await Build(new SnapshotDataReaderBuilder().Key("Id"), table, options);

        Assert.Equal(10_000, snapshot.RowCount);
        Assert.True(yields >= 100, $"The build yielded {yields} times.");
        Assert.Equal(reports.Select(p => p.Rows), reports.Select(p => p.Rows).Order());
        Assert.Contains(new SnapshotProgress(10_000, 10_000), reports);
        Assert.Equal(new SnapshotProgress(10_000, 10_000), reports[^1]);
    }

    [Fact] // ADR-0063: a cancelled build throws, and yields nothing
    public async Task A_cancelled_build_throws_and_yields_nothing()
    {
        var table = Table(("Id", typeof(long)));
        for (var i = 0; i < 10_000; i++)
            table.Rows.Add((long)i);
        using var cancel = new CancellationTokenSource();
        var yields = 0;
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = () =>
            {
                if (++yields == 3)
                    cancel.Cancel();
                return ValueTask.CompletedTask;
            },
        };
        Snapshot? built = null;
        using var reader = table.CreateDataReader();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => built = await new SnapshotDataReaderBuilder().BuildAsync(reader, options, cancel.Token));

        Assert.Null(built);
        Assert.Equal(3, yields);
    }

    [Fact] // ADR-0063: a build asked for with a cancelled token reads nothing
    public async Task A_build_cancelled_before_it_starts_reads_nothing()
    {
        var table = Table(("Id", typeof(long)));
        table.Rows.Add(1L);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        using var reader = table.CreateDataReader();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new SnapshotDataReaderBuilder().BuildAsync(reader, null, cancel.Token));

        Assert.True(reader.Read());
        Assert.Equal(1L, reader.GetInt64(0));
    }

    [Fact] // ADR-0063: the reader's current result set is read, from where it stands; it is neither moved on nor closed
    public async Task The_current_result_set_is_read_and_the_reader_left_open()
    {
        var first = Table(("Id", typeof(long)));
        first.Rows.Add(1L);
        first.Rows.Add(2L);
        var second = Table(("Desk", typeof(string)));
        second.Rows.Add("FX");
        using var reader = new DataTableReader([first, second]);
        Assert.True(reader.Read());

        var snapshot = await new SnapshotDataReaderBuilder().BuildAsync(reader, null, TestContext.Current.CancellationToken);

        Assert.Equal([2L], Values(snapshot, "Id"));
        Assert.False(reader.IsClosed);
        Assert.True(reader.NextResult());
        Assert.Equal(["FX"], Values(await new SnapshotDataReaderBuilder().BuildAsync(reader, null, TestContext.Current.CancellationToken), "Desk"));
    }

    [Fact] // ADR-0063: the same records read from a DbDataReader and built from objects hold the same values
    public async Task A_reader_holds_what_the_same_records_hold_as_objects()
    {
        var records = Trades(3_000);
        var fromObjects = Fixtures.Trades().Build(records);
        var table = Table(("Id", typeof(long)), ("Desk", typeof(string)), ("Notional", typeof(decimal)), ("Price", typeof(double)), ("When", typeof(DateTime)), ("Live", typeof(bool)));
        foreach (var t in records)
            table.Rows.Add(t.Id, (object?)t.Desk ?? DBNull.Value, (object?)t.Notional ?? DBNull.Value, (object?)t.Price ?? DBNull.Value, (object?)t.When ?? DBNull.Value, (object?)t.Live ?? DBNull.Value);

        var fromReader = await Build(new SnapshotDataReaderBuilder().Key("Id"), table);

        foreach (var column in fromObjects.Columns)
            Assert.Equal(Values(fromObjects, column.Name), Values(fromReader, column.Name));
        Assert.Equal(((TextColumn)fromObjects["Desk"]).Dictionary, ((TextColumn)fromReader["Desk"]).Dictionary);
    }

    [Fact] // ADR-0063: a declaration is checked as it is made: a name is unique, and a field is named
    public void A_declaration_is_checked_as_it_is_made()
    {
        Assert.Throws<ArgumentException>(() => new SnapshotDataReaderBuilder().Column("A").Column("B", name: "A"));
        Assert.Throws<ArgumentException>(() => new SnapshotDataReaderBuilder().Column(""));
        Assert.Throws<ArgumentNullException>(() => new SnapshotDataReaderBuilder().Text<Guid>("A", null!));
    }
}
