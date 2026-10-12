using Bunit;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// A value cell as the report paints it (ADR-0060, the seventeenth Windows run's items 4 and 16):
/// General at most eleven characters, as Excel's PivotTable shows it, a copy carrying the whole
/// number (PV-7), and Count Numbers captioned as Excel captions it.
/// </summary>
public class PivotValueTextTests : PivotTestContext
{
    private sealed record Observation(string Group, double? Amount);

    private static readonly PivotField<Observation>[] ObservationFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Amount", PivotFieldType.Number, o => o.Amount),
    ];

    // verification/2026-10-11-windows-17, part-e/e16.csv: the same records Excel was given.
    private static readonly Observation[] E16 =
    [
        new("A", 0.1), new("A", 0.2), new("B", 123456789012.345678), new("C", 1.23456789012345678),
        new("D", 0.000012345678901234567),
    ];

    private IRenderedComponent<PivotComponent> RenderE16() => RenderPivot(
        new PivotLayout { Rows = [P("Group")], Values = [Sum("Amount")] },
        source: PivotSource.From(E16, ObservationFields));

    [Fact] // ADR-0060, item 16: General paints at most eleven characters, as Excel's PivotTable showed e16
    public void ADR0060_General_paints_at_most_eleven_characters()
    {
        var cut = RenderE16();

        Assert.Equal(
            ["A | 0.3", "B | 1.23457E+11", "C | 1.23456789", "D | 1.23457E-05", "Grand Total | 1.23457E+11"],
            RowTexts(cut));
    }

    [Fact] // ADR-0060, PV-7, ADR-0005: a copy carries the full-precision number in the flavour Excel reads, and the painted text in the TSV, as Excel's own text flavour does
    public async Task ADR0060_PV7_A_copy_carries_the_full_precision_number()
    {
        var cut = RenderE16();
        var grid = Grid(cut).Instance;
        await cut.InvokeAsync(() => grid.PlaceSelectionAsync(new SelectionRange(1, 1, 3, 1), new CellPosition(1, 1), grid.RowSequenceVersion));

        var payload = await cut.InvokeAsync(() => grid.BuildCopyPayloadAsync());

        Assert.NotNull(payload);
        Assert.Equal(["1.23457E+11", "1.23456789", "1.23457E-05"],
            payload!.Text!.TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r')));
        Assert.Contains(">123456789012.34567<", payload.Html, StringComparison.Ordinal);
        Assert.Contains(">1.2345678901234567<", payload.Html, StringComparison.Ordinal);
        Assert.Contains(">1.2345678901234568E-05<", payload.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("1.23457E", payload.Html, StringComparison.Ordinal);
    }

    [Fact] // ADR-0060, item 4: Count Numbers is captioned "Count of", so beside a Count of the same field it is "Count of Value2"
    public void ADR0060_Count_Numbers_is_captioned_as_Excel_captions_it()
    {
        var cut = RenderPivot(new PivotLayout
        {
            Rows = [P("Group")],
            Values = [new PivotValueField("Amount", PivotAggregation.Count), new PivotValueField("Amount", PivotAggregation.CountNumbers)],
        }, source: PivotSource.From(E16, ObservationFields));

        Assert.Equal(["Row Labels", "Count of Amount", "Count of Amount2"], HeaderTexts(cut));
        Assert.Equal(["Count of Amount", "Count of Amount2"], AreaEntries(cut, "Values"));
    }
}
