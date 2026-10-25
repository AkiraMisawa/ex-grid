using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>What an Item is of a .NET value (ADR-0060, "Items"): its types, <c>DateOnly</c> and
/// <c>DateTimeOffset</c>, an enum; and a number Item's copy. What an Item is and in what order Items stand
/// is held on both engines by the shared cases (<c>tests/cases/pivot/items.json</c>, <c>item-keys.json</c>).</summary>
public class ItemTests
{
    private sealed record Thing(object? Key, decimal Amount);

    private enum Rating { Low, High }

    private static readonly PivotField<Thing>[] ThingFields =
    [
        new("Key", PivotFieldType.Text, t => t.Key),
        new("Amount", PivotFieldType.Number, t => t.Amount),
    ];

    private static PivotReport Things(params object?[] keys)
        => Report(keys.Select(k => new Thing(k, 1m)).ToArray(), ThingFields,
            new PivotLayout { Rows = [P("Key")], Values = [Sum("Amount")] });

    private static string[] Labels(PivotReport report)
        => report.Rows.Where(r => r.Role != PivotRowRole.GrandTotal).Select(r => r.Labels[0].Text!).ToArray();

    [Fact] // ADR-0060: a number is its value, whatever its .NET type
    public void Numbers_of_any_type_with_one_value_are_one_item()
    {
        var report = Things(1, 1.0m, 1.0, 1L, (short)1, 2.5f);

        Assert.Equal(["1", "2.5"], Labels(report));
        Assert.Equal("5", Cell(report, 0, 0));
    }

    [Fact] // ADR-0060: a date is its clock value; DateOnly is midnight of that day
    public void Dates_are_their_clock_value()
    {
        var report = Things(new DateTime(2026, 1, 1), new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 13, 45, 0),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(9)));

        // The culture's own long time, whatever space ICU puts before its PM.
        var afternoon = new DateTime(2026, 1, 1, 13, 45, 0).ToString("G", EnUs.Culture);
        Assert.Equal(["1/1/2026", afternoon, "1/2/2026"], Labels(report));
        Assert.StartsWith("1/1/2026 1:45:00", afternoon);
        Assert.Equal("2", Cell(report, 0, 0));
    }

    [Fact] // ADR-0060: any other type is text, by its name
    public void An_enum_is_text_by_its_name()
        => Assert.Equal(["High", "Low"], Labels(Things(Rating.Low, Rating.High)));

    [Fact] // ADR-0060: a DateOnly is the key of its midnight, and two text keys of one Item hash alike
    public void An_item_key_of_a_dotnet_value()
    {
        Assert.Equal(new PivotItemKey(PivotItemKind.Date, "2026-09-30T00:00:00"), PivotItemKey.For(new DateOnly(2026, 9, 30)));
        Assert.Equal(PivotItemKey.Text("EAST").GetHashCode(), PivotItemKey.For("east").GetHashCode());
    }

    private sealed record Keyed(decimal Key, int Amount);

    private static readonly PivotField<Keyed>[] KeyedFields =
    [
        new("Key", PivotFieldType.Number, k => k.Key),
        new("Amount", PivotFieldType.Number, k => k.Amount),
    ];

    // The readings of 2026-10-12's e16b.csv (R3 (c)): Key read as Decimal and Amount as Integer, as
    // the Schema ExPivot suggested for it.
    private static readonly Keyed[] E16b = [new(1.23456789012345m, 1), new(123456789012.345m, 2)];

    private static readonly PivotLayout ByKey = new() { Rows = [P("Key")], Values = [Sum("Amount")] };

    [Fact] // ADR-0060, PV-7, ADR-0005: a number Item's label is shown in General, and its copy carries the number in full, as a value cell's does — Excel's label cell holds 123456789012.345 (R3 (c))
    public async Task ADR0060_PV7_A_number_Items_copy_carries_the_number_in_full()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var source = PivotReportSource.From(PivotSource.From(E16b, KeyedFields));
        var client = new PivotReportClient(source);
        Assert.True(await client.ReadAsync(ByKey, PivotReportSettings.Invariant, new(0, 3), cancellationToken: ct));
        var shown = client.Current!.Metadata;
        string[] columns = [shown.LabelColumns[0].Name, shown.ValueColumns[0].Name];

        var copy = await source.CopyAsync(new(shown.Version, columns, [new(0, 0, 2, 1)]), ct);

        var rows = copy.Blocks[0].Rows;
        Assert.Equal(["1.23456789", "1.23457E+11", "Grand Total"], rows.Select(row => row[0].Text));
        Assert.Equal(["1.23456789012345", "123456789012.345", "Grand Total"], rows.Select(row => row[0].Raw));
        Assert.Equal([true, true, false], rows.Select(row => row[0].IsNumber));
        Assert.Equal(["1", "2", "3"], rows.Select(row => row[1].Raw));
    }

    [Fact] // ADR-0005's note of 2026-10-13 (decided with the user), ADR-0060: a number column Item heads its column in General, and a copy's header carries its number in full for the HTML flavour; it travels a server's JSON
    public async Task ADR0005_A_number_column_Items_header_carries_its_number_in_full()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var source = PivotReportSource.From(PivotSource.From(E16b, KeyedFields));
        var client = new PivotReportClient(source);
        var byColumn = new PivotLayout { Columns = [P("Key")], Values = [Sum("Amount")] };
        Assert.True(await client.ReadAsync(byColumn, PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var shown = client.Current!.Metadata;
        var columns = shown.ValueColumns.Select(c => c.Name).ToArray();

        var copy = await source.CopyAsync(new(shown.Version, columns, [new(0, 0, 0, columns.Length - 1)]), ct);

        var block = copy.Blocks[0];
        Assert.Equal(["1.23456789", "1.23457E+11", "Grand Total"], block.Headers);
        Assert.Equal(["1.23456789012345", "123456789012.345", null], block.HeaderRaws!);
        var travelled = PivotReportJson.Read<PivotReportCopyResult>(PivotReportJson.Write(copy)).Blocks[0];
        Assert.Equal(block.HeaderRaws!, travelled.HeaderRaws!);
        // A rectangle whose headers are all text has none.
        var labels = await source.CopyAsync(new(shown.Version, [columns[^1]], [new(0, 0, 0, 0)]), ct);
        Assert.Null(labels.Blocks[0].HeaderRaws);
        Assert.Null(PivotReportJson.Read<PivotReportCopyResult>(PivotReportJson.Write(labels)).Blocks[0].HeaderRaws);
    }
}
