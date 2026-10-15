using System.Globalization;
using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// ExPivot's English follows Excel's two English editions (ADR-0060's note of 2026-10-14): under an
/// <c>en-GB</c> UI culture, the English (United Kingdom) edition's <c>Max. of</c>, <c>Min. of</c>,
/// <c>Max.</c>, <c>Min.</c> and <c>Summarise value field by</c> (the readings of 2026-10-12, R3 (a)
/// and (d)); under any other, the English (United States) edition's (the English (US) record of that
/// day, (a) and (d)). The Consumer's <c>Label</c> still wins. Each test sets the UI culture it is
/// about and puts back the one it found, so no test depends on the machine's display language.
/// </summary>
public class EnglishEditionWordsTests : PivotTestContext
{
    private static readonly PivotLayout MaxAndMin = new()
    {
        Rows = [P("Region")],
        Values = [new PivotValueField("Amount", PivotAggregation.Max), new PivotValueField("Amount", PivotAggregation.Min)],
    };

    private static readonly string[] UnitedKingdomList =
        ["Sum", "Count", "Average", "Max.", "Min.", "Product", "Count Numbers", "StdDev", "StdDevp", "Var", "Varp"];

    private static readonly string[] UnitedStatesList =
        ["Sum", "Count", "Average", "Max", "Min", "Product", "Count Numbers", "StdDev", "StdDevp", "Var", "Varp"];

    /// <summary>Sets the UI culture until disposed. Set from the test's own method, it holds for
    /// every render and handler the test drives from there.</summary>
    private static UiCultureScope UiCulture(string name) => new(CultureInfo.GetCultureInfo(name));

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public UiCultureScope(CultureInfo culture) => CultureInfo.CurrentUICulture = culture;

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    /// <summary>Opens Value Field Settings… on the Values entry captioned
    /// <paramref name="caption"/>, and reads the heading over the Aggregation list and the list.</summary>
    private static async Task<(string Heading, string[] List)> ValueFieldSettingsAsync(IRenderedComponent<PivotComponent> cut, string caption)
    {
        await OpenMenuAsync(cut, "Values", caption);
        await RunMenuAsync(cut, "Value Field Settings…");
        var row = cut.FindAll(".ex-pivot-value-settings label.ex-pivot-row").Single(r => r.QuerySelector("select") is { } s
            && s.QuerySelectorAll("option").Any(o => o.GetAttribute("value") == nameof(PivotAggregation.CountNumbers)));
        return (row.QuerySelector("span")!.TextContent.Trim(),
            row.QuerySelectorAll("option").Select(o => o.TextContent.Trim()).ToArray());
    }

    [Fact] // ADR-0060's note of 2026-10-14 (observed: R3 (a) and (d)): under an en-GB UI culture the captions, Value Field Settings' list and its heading are the English (UK) edition's
    public async Task Under_en_GB_the_words_are_the_English_UK_editions()
    {
        using var _ = UiCulture("en-GB");
        var cut = RenderPivot(MaxAndMin);

        Assert.Equal(["Row Labels", "Max. of Amount", "Min. of Amount"], HeaderTexts(cut));
        Assert.Equal(["Max. of Amount", "Min. of Amount"], AreaEntries(cut, "Values"));
        var (heading, list) = await ValueFieldSettingsAsync(cut, "Max. of Amount");
        Assert.Equal("Summarise value field by", heading);
        Assert.Equal(UnitedKingdomList, list);
        // Every other word is the one both editions share: the pane, its instruction lines, Grand Total.
        Assert.Equal(["Choose fields to add to report:", "Drag fields between areas below:"],
            cut.FindAll(".ex-pivot-pane-caption").Select(c => c.TextContent.Trim()));
        Assert.Equal("Grand Total", RowTexts(cut)[^1].Split(" | ")[0]);
    }

    [Fact] // ADR-0060's notes of 2026-10-12 and 2026-10-14 (observed: the English (US) record, (a) and (d)): under an en-US UI culture they are the English (US) edition's
    public async Task Under_en_US_the_words_are_the_English_US_editions()
    {
        using var _ = UiCulture("en-US");
        var cut = RenderPivot(MaxAndMin);

        Assert.Equal(["Row Labels", "Max of Amount", "Min of Amount"], HeaderTexts(cut));
        Assert.Equal(["Max of Amount", "Min of Amount"], AreaEntries(cut, "Values"));
        var (heading, list) = await ValueFieldSettingsAsync(cut, "Max of Amount");
        Assert.Equal("Summarize value field by", heading);
        Assert.Equal(UnitedStatesList, list);
    }

    [Theory] // ADR-0060's note of 2026-10-14, by generalisation: only en-GB was observed, so every other English culture keeps the English (US) words; and a culture of another language chooses no language — its words stay English
    [InlineData("en-AU")]
    [InlineData("en-IE")]
    [InlineData("en-IN")]
    [InlineData("en-NZ")]
    [InlineData("en-CA")]
    [InlineData("en")]
    [InlineData("ja-JP")]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void Any_other_UI_culture_keeps_the_English_US_words(string culture)
    {
        using var _ = UiCulture(culture);
        var cut = RenderPivot(MaxAndMin);

        Assert.Equal(["Row Labels", "Max of Amount", "Min of Amount"], HeaderTexts(cut));
    }

    [Fact] // ADR-0060's note of 2026-10-14: the Consumer's Label wins over either edition — a word of its own, the Japanese words, or the English (US) words kept under en-GB
    public void The_consumers_label_still_wins()
    {
        using var _ = UiCulture("en-GB");

        var own = RenderPivot(MaxAndMin, ps => ps.Add(p => p.Label, id => id == PivotWords.CaptionOf(PivotAggregation.Max) ? "Highest {0}" : null));
        Assert.Equal(["Row Labels", "Highest Amount", "Min. of Amount"], HeaderTexts(own));

        var japanese = RenderPivot(MaxAndMin, ps => ps.Add(p => p.Label, PivotWords.Japanese));
        Assert.Equal(["行ラベル", "最大 / Amount", "最小 / Amount"], HeaderTexts(japanese));

        var unitedStates = RenderPivot(MaxAndMin, ps => ps.Add(p => p.Label, PivotWords.EnglishFor));
        Assert.Equal(["Row Labels", "Max of Amount", "Min of Amount"], HeaderTexts(unitedStates));

        // And the English (UK) words chosen in one line under any UI culture.
        using var __ = UiCulture("en-US");
        var unitedKingdom = RenderPivot(MaxAndMin, ps => ps.Add(p => p.Label, PivotWords.EnglishUnitedKingdom));
        Assert.Equal(["Row Labels", "Max. of Amount", "Min. of Amount"], HeaderTexts(unitedKingdom));
    }

    [Fact] // ADR-0060's note of 2026-10-14, ADR-0152: the edition's words travel in the report's explicit settings, so a report computed elsewhere is in the words the browser chose and never in the server's culture
    public void The_editions_words_are_explicit_report_settings()
    {
        using (UiCulture("en-GB"))
        {
            var cut = RenderPivot(MaxAndMin);

            var words = cut.Instance.Report!.Metadata.Settings.Words;
            Assert.Equal("Max. of {0}", words[PivotWords.CaptionOf(PivotAggregation.Max)]);
            Assert.Equal("Summarise value field by", words["summarize-by"]);
            Assert.False(words.ContainsKey(PivotWords.GrandTotal));
        }
        using (UiCulture("en-US"))
        {
            var cut = RenderPivot(MaxAndMin);

            Assert.Empty(cut.Instance.Report!.Metadata.Settings.Words);
        }
    }

    [Fact] // ADR-0060's notes of 2026-10-12 and 2026-10-14: a caption Value Field Settings gives is numbered in the edition's words — Min changed to Max beside a Max is "Max. of Amount2" under en-GB
    public async Task A_caption_is_numbered_in_the_editions_words()
    {
        using var _ = UiCulture("en-GB");
        var cut = RenderPivot(MaxAndMin);

        await OpenMenuAsync(cut, "Values", "Min. of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");
        await cut.FindAll(".ex-pivot-value-settings select")[0].ChangeAsync(new ChangeEventArgs { Value = nameof(PivotAggregation.Max) });
        Assert.Equal("Max. of Amount", cut.FindAll(".ex-pivot-value-settings input[type=text]")[0].GetAttribute("value"));
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Equal(["Row Labels", "Max. of Amount", "Max. of Amount2"], HeaderTexts(cut)));
    }

    [Fact] // ADR-0060's note of 2026-10-14: the UI culture is read as the parameters are set — set again under another culture, the report is repainted in that edition's words
    public void A_new_UI_culture_repaints_in_its_editions_words()
    {
        using var _ = UiCulture("en-US");
        var cut = RenderPivot(MaxAndMin);
        Assert.Equal(["Row Labels", "Max of Amount", "Min of Amount"], HeaderTexts(cut));

        using (UiCulture("en-GB"))
            cut.Render();

        cut.WaitForAssertion(() => Assert.Equal(["Row Labels", "Max. of Amount", "Min. of Amount"], HeaderTexts(cut)));
    }
}
