using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>The Pivot Layout's saved form (ADR-0059: View State is serialisable) and the engine's
/// package boundary (ADR-0059/0060).</summary>
public class LayoutJsonTests
{
    private static readonly PivotLayout Full = new()
    {
        Filters = [P("Online") with { HiddenItems = [PivotItemKey.Boolean(false)] }],
        Rows =
        [
            P("Region") with
            {
                Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 1),
                HiddenItems = [PivotItemKey.Text("West"), PivotItemKey.Blank],
                Subtotals = false,
                Collapsed = true,
                ToggledItems = [PivotItemKey.Text("East")],
            },
            P("Date") with { HiddenItems = [PivotItemKey.Date(new DateTime(2026, 1, 10, 13, 45, 0, 250))] },
        ],
        Columns = [P("Quantity") with { HiddenItems = [PivotItemKey.Number(10), PivotItemKey.Error] }],
        Values =
        [
            Sum("Amount") with { Caption = "Sales", NumberFormat = "#,##0.00" },
            Value("Amount", PivotAggregation.StdDevp) with { ShowValuesAs = PivotShowValuesAs.PercentOfRowTotal },
        ],
        ValuesAxis = PivotAxis.Rows,
        Form = PivotReportForm.Tabular,
        SubtotalsAtTop = false,
        GrandTotalRow = false,
        GrandTotalColumn = false,
        RepeatItemLabels = true,
    };

    [Fact] // ADR-0059: a layout written and read back is the same layout, and lays out the same report
    public void A_layout_round_trips()
    {
        var json = PivotLayoutJson.Write(Full);
        var read = PivotLayoutJson.Read(json);

        Assert.Equal(json, PivotLayoutJson.Write(read));
        Assert.Equal(Full.Rows[0].HiddenItems, read.Rows[0].HiddenItems);
        Assert.Equal(Full.Rows[1].HiddenItems, read.Rows[1].HiddenItems);
        Assert.Equal(Full.Columns[0].HiddenItems, read.Columns[0].HiddenItems);
        Assert.Equal(Full.Values, read.Values);
        Assert.Equal(Lines(Report(Full)), Lines(Report(read)));
    }

    [Fact] // ADR-0059: the defaults are written sparingly and read back as defaults
    public void The_default_layout_round_trips()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var json = PivotLayoutJson.Write(layout);

        Assert.Equal(
            """{"version":1,"form":"compact","valuesAxis":"columns","subtotalsAtTop":true,"grandTotalRow":true,"grandTotalColumn":true,"repeatItemLabels":false,"filters":[],"rows":[{"field":"Region"}],"columns":[],"values":[{"field":"Amount","aggregation":"sum"}]}""",
            json);
        var read = PivotLayoutJson.Read("""{"version":1,"rows":[{"field":"Region"}],"values":[{"field":"Amount","aggregation":"sum"}]}""");
        Assert.Equal(PivotLayoutJson.Write(read), json);
    }

    [Fact] // ADR-0059: a document of another version is refused, never half-read
    public void Another_version_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => PivotLayoutJson.Read("""{"version":2}"""));
        Assert.Throws<FormatException>(() => PivotLayoutJson.Read("""{"form":"compact"}"""));
    }

    [Theory] // ADR-0059: a name the reader does not know is refused by name
    [InlineData("""{"version":1,"form":"pivot"}""")]
    [InlineData("""{"version":1,"valuesAxis":"filters"}""")]
    [InlineData("""{"version":1,"values":[{"field":"Amount","aggregation":"median"}]}""")]
    [InlineData("""{"version":1,"rows":[{"field":"Region","sort":{"direction":"sideways"}}]}""")]
    [InlineData("""{"version":1,"rows":[{"field":"Region","hiddenItems":[{"kind":"colour","value":"red"}]}]}""")]
    [InlineData("""{"version":1,"rows":[{"field":"Region","hiddenItems":[{"kind":"number","value":"12,5"}]}]}""")]
    [InlineData("""{"version":1,"rows":[{"nofield":"Region"}]}""")]
    [InlineData("""{"version":1,"rows":{}}""")]
    [InlineData("""{"version":1,"grandTotalRow":"yes"}""")]
    [InlineData("""[1]""")]
    public void An_unknown_name_or_shape_is_refused(string json)
        => Assert.ThrowsAny<Exception>(() => PivotLayoutJson.Read(json));

    [Fact] // ADR-0059/0064 (PV-1): the engine references the base class library and ExGrid.Data, and nothing else
    public void The_engine_references_only_the_base_class_library_and_the_data_package()
    {
        var referenced = typeof(PivotEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.All(referenced, name => Assert.True(
            name == "System" || name.StartsWith("System.", StringComparison.Ordinal) || name == "netstandard" || name == "ExGrid.Data",
            $"ExPivot.Engine references {name}."));
        Assert.Contains("ExGrid.Data", referenced);
    }
}
