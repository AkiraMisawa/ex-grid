using ExGrid.Data;
using ExGrid.Data.Arrow;

namespace PackageSmoke;

/// <summary>
/// A Snapshot written to an Arrow stream and read back through the packed ExGrid.Data.Arrow and
/// ExGrid.Data, as a Consumer's server and browser do (ADR-0064, DA-16): every kind, a Blank in each,
/// a caption, the Record Key and a version moved on by a Change Batch. check.sh runs it through
/// <c>RoundTrip/</c> and fails when what is read back differs; the Blazor application beside it
/// compiles it as a browser takes the packages.
/// </summary>
internal static class ArrowRoundTrip
{
    private sealed record Deal(long Id, string? Desk, decimal? Notional, double? Price, long? Quantity, DateOnly? Day, DateTime? When, bool? Live);

    /// <summary>What differs between the Snapshot written and the one read back; empty when nothing does.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(CancellationToken token)
    {
        var builder = new SnapshotBuilder<Deal>()
            .Integer("Id", d => d.Id)
            .Text("Desk", d => d.Desk, caption: "Trading desk")
            .Decimal("Notional", d => d.Notional)
            .Double("Price", d => d.Price)
            .Integer("Quantity", d => d.Quantity)
            .Date("Day", d => d.Day)
            .Date("When", d => d.When)
            .Boolean("Live", d => d.Live)
            .Key("Id");
        Deal[] deals =
        [
            new(1, "EMEA", 1_000_000.25m, 101.5, 10, new DateOnly(2026, 9, 30), new DateTime(2026, 9, 30, 12, 0, 0, 500), true),
            new(2, "amer", -0.01m, double.NaN, long.MinValue, new DateOnly(1969, 12, 31), new DateTime(1970, 1, 1), false),
            new(3, null, null, null, null, null, null, null),
            new(4, "AMER", 79_228_162_514_264_337_593_543_950_335m, -0.0, long.MaxValue, new DateOnly(9999, 12, 31), new DateTime(2026, 1, 1).AddTicks(1), true),
            new(5, "", 0m, double.PositiveInfinity, 0, DateOnly.MinValue, DateTime.UnixEpoch, null),
        ];
        var written = builder.Build(deals).Apply(builder.Batch(changed: [deals[0] with { Desk = "APAC" }], removedKeys: [5L])).After;

        using var stream = new MemoryStream();
        await SnapshotArrow.WriteAsync(written, stream, token);
        stream.Position = 0;
        var read = await SnapshotArrow.ReadAsync(stream, cancellationToken: token);
        return Differences(written, read);
    }

    private static List<string> Differences(Snapshot written, Snapshot read)
    {
        var differences = new List<string>();
        if (written.Version != read.Version)
            differences.Add($"the version {written.Version} was read back as {read.Version}");
        if (written.RecordKey?.Name != read.RecordKey?.Name)
            differences.Add($"the Record Key '{written.RecordKey?.Name}' was read back as '{read.RecordKey?.Name}'");
        if (written.RowCount != read.RowCount || written.Columns.Count != read.Columns.Count)
        {
            differences.Add($"{written.RowCount} rows of {written.Columns.Count} columns were read back as {read.RowCount} rows of {read.Columns.Count}");
            return differences;
        }
        for (var c = 0; c < written.Columns.Count; c++)
        {
            var (before, after) = (written.Columns[c], read.Columns[c]);
            if ((before.Name, before.Caption, before.Kind) != (after.Name, after.Caption, after.Kind))
            {
                differences.Add($"the column {before.Name} '{before.Caption}' {before.Kind} was read back as {after.Name} '{after.Caption}' {after.Kind}");
                continue;
            }
            for (var r = 0; r < written.RowCount; r++)
            {
                var (was, @is) = (Show(written.ValueAt(written.Rows[r], before)), Show(read.ValueAt(read.Rows[r], after)));
                if (was != @is)
                    differences.Add($"row {r + 1}, column '{before.Name}': {was} was read back as {@is}");
            }
        }
        return differences;
    }

    /// <summary>A value exactly: a double by its bits, a date by its ticks, a Blank apart from any value.</summary>
    private static string Show(object? value) => value switch
    {
        null => "a Blank",
        double number => $"the double with bits {BitConverter.DoubleToInt64Bits(number):X16}",
        DateTime date => $"the date of {date.Ticks} ticks",
        string text => $"'{text}'",
        _ => $"{value.GetType().Name} {value}",
    };
}
