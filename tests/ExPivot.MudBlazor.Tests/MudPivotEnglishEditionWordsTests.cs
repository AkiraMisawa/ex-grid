using System.Globalization;
using Bunit;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using MudBlazor;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// ExPivot's two English editions under MudBlazor (ADR-0060's note of 2026-10-14, ADR-0062): the Mud
/// Chrome draws ExPivot's words by id, so under an <c>en-GB</c> UI culture its pane, its Value Field
/// Settings… and the report speak the English (United Kingdom) edition's <c>Max. of</c>,
/// <c>Max.</c> and <c>Summarise value field by</c>, and under any other the English (United States)
/// edition's. Each test sets the UI culture it is about and puts back the one it found.
/// </summary>
public class MudPivotEnglishEditionWordsTests : MudPivotTestContext
{
    private static readonly PivotLayout MaxAndMin = new()
    {
        Rows = [P("Region")],
        Values = [new PivotValueField("Amount", PivotAggregation.Max), new PivotValueField("Amount", PivotAggregation.Min)],
    };

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public UiCultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    /// <summary>The Aggregation select's items, as its open list paints them in the popover provider.</summary>
    private string[] PaintedItems()
        => Popovers.FindComponents<MudSelectItem<PivotAggregation>>()
            .Select(i => string.Concat(i.Nodes.Select(n => n.TextContent)).Trim()).ToArray();

    [Theory] // ADR-0060's note of 2026-10-14 (observed: R3 (a) and (d); the English (US) record, (a) and (d)), ADR-0062: under MudBlazor, the captions, the Aggregation select's label and its items are the English edition's the UI culture chooses
    [InlineData("en-GB", "Max. of Amount", "Min. of Amount", "Summarise value field by", "Max.", "Min.")]
    [InlineData("en-US", "Max of Amount", "Min of Amount", "Summarize value field by", "Max", "Min")]
    [InlineData("en-AU", "Max of Amount", "Min of Amount", "Summarize value field by", "Max", "Min")]
    public async Task The_mud_chrome_speaks_the_english_edition_the_UI_culture_chooses(
        string culture, string max, string min, string heading, string maxItem, string minItem)
    {
        using var _ = new UiCultureScope(culture);
        var cut = RenderPivot(MaxAndMin);

        Assert.Equal(["Row Labels", max, min], HeaderTexts(cut));
        Assert.Equal([max, min], Entries(cut, "Values"));

        await OpenMenuAsync(cut, "Values", max);
        await RunMenuAsync(cut, "Value Field Settings…");
        var select = cut.FindComponent<MudSelect<PivotAggregation>>();
        Assert.Equal(heading, select.Instance.Label);
        Assert.Equal(heading, cut.Find(".mud-ex-pivot-aggregation [role=combobox]").GetAttribute("aria-label"));
        await cut.InvokeAsync(() => select.Instance.OpenMenu());
        Popovers.WaitForAssertion(() => Assert.Equal(
            ["Sum", "Count", "Average", maxItem, minItem, "Product", "Count Numbers", "StdDev", "StdDevp", "Var", "Varp"], PaintedItems()));
    }

    [Fact] // ADR-0060's note of 2026-10-14: under MudBlazor too, the Consumer's Label wins over the edition — the English (US) words kept under en-GB
    public void The_consumers_label_still_wins_under_mudblazor()
    {
        using var _ = new UiCultureScope("en-GB");

        var cut = RenderPivot(MaxAndMin, label: PivotWords.EnglishFor);

        Assert.Equal(["Row Labels", "Max of Amount", "Min of Amount"], HeaderTexts(cut));
    }
}
