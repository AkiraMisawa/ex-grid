using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0060, "Captions and words": a Value Field is captioned as Excel captions it, and a caption a
/// second Value Field would repeat takes a number. The seventeenth Windows run read Count Numbers'
/// caption (<c>verification/2026-10-11-windows-17</c>, item 4): Excel captions it as it captions Count.
/// </summary>
public class PivotWordsTests
{
    private sealed record Obs(string Group, object? Value);

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Value", PivotFieldType.Number, o => o.Value),
    ];

    [Fact] // ADR-0060, item 4: Excel captions Count Numbers "Count of", so beside a Count of the same field it is "Count of Value2"
    public void ADR0060_Count_Numbers_is_captioned_as_Count_is()
    {
        Obs[] records = [new("A", 10), new("A", 1), new("A", null), new("B", 20), new("B", 30)];

        var report = PivotEngine.Compute(records, ObsFields, new PivotLayout
        {
            Rows = [P("Group")],
            Values = [new PivotValueField("Value", PivotAggregation.Count), new PivotValueField("Value", PivotAggregation.CountNumbers)],
        }, EnUs);

        Assert.Equal(["Count of Value", "Count of Value2"], report.ValueCaptions);
    }

    [Fact] // ADR-0060, item 4: Count Numbers alone is "Count of Value", as Excel captions it
    public void ADR0060_Count_Numbers_alone_is_Count_of()
    {
        var report = PivotEngine.Compute([new Obs("A", 1)], ObsFields, new PivotLayout
        {
            Values = [new PivotValueField("Value", PivotAggregation.CountNumbers)],
        }, EnUs);

        Assert.Equal(["Count of Value"], report.ValueCaptions);
        Assert.Equal("Count of {0}", PivotWords.EnglishFor(PivotWords.CaptionOf(PivotAggregation.CountNumbers)));
        Assert.Equal("Count Numbers", PivotWords.EnglishFor(PivotWords.AggregationName(PivotAggregation.CountNumbers)));
    }

    [Fact] // ADR-0060, item 4: the numbering follows the order of Values, whichever of the two comes first
    public void ADR0060_The_first_of_the_two_keeps_the_bare_caption()
    {
        var report = PivotEngine.Compute([new Obs("A", 1)], ObsFields, new PivotLayout
        {
            Values = [new PivotValueField("Value", PivotAggregation.CountNumbers), new PivotValueField("Value", PivotAggregation.Count)],
        }, EnUs);

        Assert.Equal(["Count of Value", "Count of Value2"], report.ValueCaptions);
    }

    [Fact] // ADR-0060 (a reading, not observed): the Japanese edition's Count Numbers caption stays 数値の個数 / {0}
    public void ADR0060_reading_The_Japanese_Count_Numbers_caption_is_unchanged()
    {
        var japanese = new PivotOptions { Culture = CultureInfo.GetCultureInfo("ja-JP"), Label = PivotWords.Japanese };

        var report = PivotEngine.Compute([new Obs("A", 1)], ObsFields, new PivotLayout
        {
            Values = [new PivotValueField("Value", PivotAggregation.Count), new PivotValueField("Value", PivotAggregation.CountNumbers)],
        }, japanese);

        Assert.Equal(["データの個数 / Value", "数値の個数 / Value"], report.ValueCaptions);
    }
}
