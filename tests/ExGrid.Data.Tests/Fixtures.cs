using System.Globalization;
using ExGrid.Data;
using ExGrid.Data.Storage;

namespace ExGrid.Data.Tests;

/// <summary>A trade, the record most tests build a Snapshot from. Every field but the id may be missing.</summary>
internal sealed record Trade(long Id, string? Desk, decimal? Notional, double? Price, DateTime? When, bool? Live);

/// <summary>One value, for the tests that look at a single column.</summary>
internal sealed record Cell<TValue>(TValue Value);

/// <summary>The builders the tests share, and readers that turn a Snapshot into something to compare.</summary>
internal static class Fixtures
{
    public static readonly DateTime Epoch = new(2026, 1, 1);

    public static readonly string?[] Desks = ["EMEA", "AMER", "APAC", null, "", "emea", "Amer"];

    /// <summary>Trades by id, keyed, with every kind of column.</summary>
    public static SnapshotBuilder<Trade> Trades(SnapshotTuning? tuning = null)
        => new SnapshotBuilder<Trade> { Tuning = tuning ?? SnapshotTuning.Default }
            .Integer("Id", t => t.Id)
            .Text("Desk", t => t.Desk)
            .Decimal("Notional", t => t.Notional)
            .Double("Price", t => t.Price)
            .Date("When", t => t.When)
            .Boolean("Live", t => t.Live)
            .Key("Id");

    /// <summary>Trades with no Record Key.</summary>
    public static SnapshotBuilder<Trade> UnkeyedTrades(SnapshotTuning? tuning = null)
        => new SnapshotBuilder<Trade> { Tuning = tuning ?? SnapshotTuning.Default }
            .Integer("Id", t => t.Id)
            .Text("Desk", t => t.Desk)
            .Decimal("Notional", t => t.Notional);

    public static Trade Trade(long id) => new(
        id,
        Desks[(int)(id % Desks.Length)],
        id % 11 == 0 ? null : (id * 25) / 100m,
        id % 13 == 0 ? null : id * 1.5,
        id % 17 == 0 ? null : Epoch.AddMinutes(id * 7),
        id % 19 == 0 ? null : id % 2 == 0);

    public static Trade[] Trades(int count, long first = 0)
        => [.. Enumerable.Range(0, count).Select(i => Trade(first + i))];

    /// <summary>A one-column Snapshot of <paramref name="values"/>, declared by <paramref name="declare"/>.</summary>
    public static Snapshot Column<TValue>(Func<SnapshotBuilder<Cell<TValue>>, SnapshotBuilder<Cell<TValue>>> declare, params TValue[] values)
        => declare(new SnapshotBuilder<Cell<TValue>>()).Build([.. values.Select(v => new Cell<TValue>(v))]);

    /// <summary>The column's values in row order, boxed as <see cref="Snapshot.ValueAt"/> gives them.</summary>
    public static object?[] Values(Snapshot snapshot, string column)
    {
        var target = snapshot[column];
        return [.. snapshot.Rows.Select(r => snapshot.ValueAt(r, target))];
    }

    /// <summary>A Text column's codes in row order.</summary>
    public static int[] Codes(Snapshot snapshot, string column)
    {
        var target = (TextColumn)snapshot[column];
        return [.. snapshot.Rows.Select(r => snapshot.Slice(r.Slice).Codes(target)[r.Offset])];
    }

    /// <summary>Every row in order, every value shown exactly, and the record behind it.</summary>
    public static List<string> Dump(Snapshot snapshot)
    {
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"v{snapshot.Version} rows {snapshot.RowCount} slices {snapshot.SliceCount} records {snapshot.KeepsRecords} key {snapshot.RecordKey?.Name}"),
        };
        foreach (var column in snapshot.Columns)
        {
            var line = $"{column.Ordinal} {column.Name} '{column.Caption}' {column.Kind}";
            if (column is TextColumn text)
                line += " [" + string.Join("|", text.Dictionary) + "]";
            if (column is DateColumn date)
                line += $" time {date.HasTime}";
            lines.Add(line);
        }
        foreach (var row in snapshot.Rows)
        {
            var values = snapshot.Columns.Select(c => Show(snapshot.ValueAt(row, c)));
            var record = snapshot.RecordAt(row) is Trade trade ? $" <{trade.Id}@{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(trade)}>" : "";
            lines.Add($"{row.Slice}:{row.Offset} " + string.Join(" | ", values) + record);
        }
        for (var s = 0; s < snapshot.SliceCount; s++)
        {
            var slice = snapshot.Slice(s);
            lines.Add($"slice {s}: {slice.Length} held {slice.HeldCount} removed {string.Join(",", slice.Removed.ToArray())}");
        }
        return lines;
    }

    /// <summary>A value shown exactly: a double by its bits, a date by its ticks and kind, a decimal by
    /// its value (its representation is tested on its own).</summary>
    public static string Show(object? value) => value switch
    {
        null => "∅",
        string text => $"'{text}'",
        double number => BitConverter.DoubleToInt64Bits(number).ToString(CultureInfo.InvariantCulture) + "d",
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture) + "m",
        DateTime date => date.Ticks.ToString(CultureInfo.InvariantCulture) + "t" + date.Kind,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) + value.GetType().Name[0],
        _ => value.ToString() ?? "",
    };

    /// <summary>What a trade reads back as, column by column, in the order <see cref="Trades(SnapshotTuning?)"/> declares.</summary>
    public static string[] Expected(Trade trade) =>
    [
        Show(trade.Id),
        Show(trade.Desk),
        Show(trade.Notional),
        Show(trade.Price),
        Show(trade.When is { } w ? new DateTime(w.Ticks, DateTimeKind.Unspecified) : null),
        Show(trade.Live),
    ];

    /// <summary>Asserts that <paramref name="snapshot"/> holds exactly <paramref name="model"/>, in order,
    /// each record the very object, each value exact.</summary>
    public static void AssertHolds(IReadOnlyList<Trade> model, Snapshot snapshot)
    {
        Xunit.Assert.Equal(model.Count, snapshot.RowCount);
        Xunit.Assert.Equal(model.Count, snapshot.Rows.Count);
        var held = 0;
        for (var s = 0; s < snapshot.SliceCount; s++)
            held += snapshot.Slice(s).HeldCount;
        Xunit.Assert.Equal(model.Count, held);
        for (var i = 0; i < model.Count; i++)
        {
            var row = snapshot.Rows[i];
            Xunit.Assert.True(snapshot.Holds(row));
            if (snapshot.KeepsRecords)
                Xunit.Assert.Same(model[i], snapshot.RecordAt(row));
            var expected = Expected(model[i]);
            for (var c = 0; c < snapshot.Columns.Count; c++)
                Xunit.Assert.Equal(expected[c], Show(snapshot.ValueAt(row, snapshot.Columns[c])));
        }
    }
}
