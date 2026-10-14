using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0060, "Captions and words": a Value Field is captioned as Excel captions it, and a caption a
/// second Value Field would repeat takes a number. Count Numbers is captioned as Count is, through
/// COM in the seventeenth Windows run (<c>verification/2026-10-11-windows-17</c>, item 4) and in the
/// readings of 2026-10-12 (R3 (a)), and in Value Field Settings in the English (UK) and Japanese
/// editions (R3 (d)) and the English (US) one (<c>verification/2026-10-12-windows-pivot-en-us</c>,
/// (a) and (d)). ExPivot's English is that English (US) edition's, and the English (UK) edition's
/// words are bundled where they differ (ADR-0060's notes of 2026-10-12 and 2026-10-14).
/// </summary>
public class PivotWordsTests
{
    private sealed record Obs(string Group, object? Value);

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Value", PivotFieldType.Number, o => o.Value),
    ];

    private static readonly PivotOptions Japanese = new() { Culture = CultureInfo.GetCultureInfo("ja-JP"), Label = PivotWords.Japanese };

    private static IReadOnlyList<string> Captions(PivotOptions options, params PivotValueField[] values)
        => PivotEngine.Compute([new Obs("A", 1)], ObsFields, new PivotLayout { Values = values }, options).ValueCaptions;

    private static PivotValueField Count => new("Value", PivotAggregation.Count);

    private static PivotValueField CountNumbers => new("Value", PivotAggregation.CountNumbers);

    [Fact] // ADR-0060, observed (run 17, item 4; R3 (a); the English (US) record, (a)): beside a Count of the same field, Count Numbers is "Count of Value2"
    public void ADR0060_Count_Numbers_is_captioned_as_Count_is()
    {
        Obs[] records = [new("A", 10), new("A", 1), new("A", null), new("B", 20), new("B", 30)];

        var report = PivotEngine.Compute(records, ObsFields, new PivotLayout
        {
            Rows = [P("Group")],
            Values = [Count, CountNumbers],
        }, EnUs);

        Assert.Equal(["Count of Value", "Count of Value2"], report.ValueCaptions);
    }

    [Fact] // ADR-0060, observed (R3 (d); the English (US) record, (d)): Count Numbers alone is "Count of Value", as Value Field Settings captions it
    public void ADR0060_Count_Numbers_alone_is_Count_of()
    {
        Assert.Equal(["Count of Value"], Captions(EnUs, CountNumbers));
        Assert.Equal("Count of {0}", PivotWords.EnglishFor(PivotWords.CaptionOf(PivotAggregation.CountNumbers)));
        Assert.Equal("Count Numbers", PivotWords.EnglishFor(PivotWords.AggregationName(PivotAggregation.CountNumbers)));
    }

    [Fact] // ADR-0060, by generalisation (Excel numbered the second of Count, then Count Numbers): the numbering follows the order of Values, whichever comes first
    public void ADR0060_The_first_of_the_two_keeps_the_bare_caption()
        => Assert.Equal(["Count of Value", "Count of Value2"], Captions(EnUs, CountNumbers, Count));

    [Fact] // ADR-0060, observed (R3 (d), the Japanese edition, chosen in Value Field Settings and added through COM): Count Numbers is captioned データの個数 / Value, as Count is
    public void ADR0060_The_Japanese_edition_captions_Count_Numbers_as_Count()
    {
        Assert.Equal(["データの個数 / Value"], Captions(Japanese, CountNumbers));
        Assert.Equal("データの個数 / {0}", PivotWords.JapaneseFor(PivotWords.CaptionOf(PivotAggregation.CountNumbers)));
        Assert.Equal("数値の個数", PivotWords.JapaneseFor(PivotWords.AggregationName(PivotAggregation.CountNumbers)));
    }

    [Fact] // ADR-0060, by generalisation (the number observed in English only): in Japanese, a Count and a Count Numbers of one field are データの個数 / Value and データの個数 / Value2
    public void ADR0060_A_Japanese_Count_and_Count_Numbers_are_numbered_as_in_English()
        => Assert.Equal(["データの個数 / Value", "データの個数 / Value2"], Captions(Japanese, Count, CountNumbers));

    [Fact] // ADR-0060: a repeated caption takes the next free number — the second observed (run 17, item 14: Sum of Amount2), the third by generalisation
    public void ADR0060_A_repeated_caption_takes_the_next_free_number()
    {
        Assert.Equal(["Count of Value", "Count of Value2", "Count of Value3"], Captions(EnUs, Count, CountNumbers, Count));
        Assert.Equal(["Sum of Value", "Sum of Value2", "Sum of Value3"],
            Captions(EnUs, new("Value", PivotAggregation.Sum), new("Value", PivotAggregation.Sum), new("Value", PivotAggregation.Sum)));
    }

    [Fact] // ADR-0060 (ExPivot's rule, not observed): the number also goes past a Pivot Field's caption, which a Value Field's caption may not repeat
    public void ADR0060_A_default_caption_goes_past_a_Pivot_Fields_caption()
    {
        PivotField<Obs>[] fields = [.. ObsFields, new("Tally", PivotFieldType.Number, o => o.Value, caption: "Count of Value")];

        var report = PivotEngine.Compute([new Obs("A", 1)], fields, new PivotLayout { Values = [Count] }, EnUs);

        Assert.Equal(["Count of Value2"], report.ValueCaptions);
    }

    [Fact] // ADR-0060 (ExPivot's rule, not observed): a caption the user set is kept as typed, and a generated one it would repeat takes the next number free of it
    public void ADR0060_A_caption_the_user_set_is_kept_and_the_generated_ones_go_round_it()
    {
        // The user's "Count of Value2" is kept; the Count Numbers that would be "Count of Value2" is "Count of Value3".
        Assert.Equal(["Count of Value", "Count of Value2", "Count of Value3"],
            Captions(EnUs, Count, Count with { Caption = "Count of Value2" }, CountNumbers));
        // The user's "Count of Value", standing after the Count it repeats, is kept; that Count is "Count of Value2".
        Assert.Equal(["Count of Value2", "Count of Value"], Captions(EnUs, CountNumbers, Count with { Caption = "Count of Value" }));
        Assert.Equal(["データの個数 / Value2", "データの個数 / Value"],
            Captions(Japanese, CountNumbers, Count with { Caption = "データの個数 / Value" }));
    }

    [Fact] // ADR-0060's note of 2026-10-12, observed (the English (US) record, (a) and (d)): ExPivot's English is Excel's English (US) edition's — Max of, Min of, Summarize
    public void ADR0060_The_English_words_are_Excels_English_US_editions()
    {
        Assert.Equal(["Max of Value", "Min of Value"],
            Captions(EnUs, new("Value", PivotAggregation.Max), new("Value", PivotAggregation.Min)));
        // Value Field Settings' list, in that edition's order (the English (US) record, (d)).
        Assert.Equal(
            ["Sum", "Count", "Average", "Max", "Min", "Product", "Count Numbers", "StdDev", "StdDevp", "Var", "Varp"],
            Enum.GetValues<PivotAggregation>().Select(a => PivotWords.EnglishFor(PivotWords.AggregationName(a))));
        Assert.Equal("Summarize value field by", PivotWords.EnglishFor("summarize-by"));
    }

    [Fact] // ADR-0060's note of 2026-10-14, observed (R3 (a) and (d)): the English (UK) edition's words are bundled where they differ — Max. of, Min. of, Max., Min., Summarise value field by — and every other id keeps the English
    public void ADR0060_The_English_UK_editions_words_are_bundled()
    {
        var uk = new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-GB"), Label = PivotWords.EnglishUnitedKingdom };

        Assert.Equal(["Max. of Value", "Min. of Value", "Sum of Value", "Count of Value", "StdDev of Value"],
            Captions(uk, new("Value", PivotAggregation.Max), new("Value", PivotAggregation.Min), new("Value", PivotAggregation.Sum),
                Count, new("Value", PivotAggregation.StdDev)));
        // Value Field Settings' list, in that edition's order (R3 (d)'s pictures), and the heading over it.
        Assert.Equal(
            ["Sum", "Count", "Average", "Max.", "Min.", "Product", "Count Numbers", "StdDev", "StdDevp", "Var", "Varp"],
            Enum.GetValues<PivotAggregation>().Select(a => PivotWords.Resolve(PivotWords.AggregationName(a), PivotWords.EnglishUnitedKingdom)));
        Assert.Equal("Summarise value field by", PivotWords.Resolve("summarize-by", PivotWords.EnglishUnitedKingdom));
        // Only the five words differ: every other id is left to the English, the instruction lines'
        // colons and Count Numbers' caption among them (the English (US) record, (a), (b) and (d)).
        Assert.Equal(["aggregation-max", "aggregation-min", "caption-max", "caption-min", "summarize-by"],
            PivotWords.Ids.Where(id => PivotWords.EnglishUnitedKingdomFor(id) is not null).Order(StringComparer.Ordinal));
        Assert.All(PivotWords.Ids.Where(id => PivotWords.EnglishUnitedKingdomFor(id) is not null), id =>
            Assert.Equal(Holes(PivotWords.EnglishFor(id)!), Holes(PivotWords.EnglishUnitedKingdomFor(id)!)));
        Assert.Null(PivotWords.EnglishUnitedKingdom("choose-fields"));
        Assert.Null(PivotWords.EnglishUnitedKingdom(PivotWords.CaptionOf(PivotAggregation.CountNumbers)));
        Assert.Null(PivotWords.EnglishUnitedKingdomFor("no-such-id"));

        static string[] Holes(string word)
            => System.Text.RegularExpressions.Regex.Matches(word, @"\{\d\}").Select(m => m.Value).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact] // ADR-0060's note of 2026-10-14, ADR-0152: the engine reads no culture for its words — under an en-GB UI culture and report culture, without the Label, it keeps the English (US) words; ExPivot chooses the edition and hands it over as the Label, so a server computes what the browser asked for
    public void ADR0060_The_engine_takes_the_English_UK_words_only_through_the_Label()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
        try
        {
            var culture = new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-GB") };

            Assert.Equal(["Max of Value", "Min of Value"],
                Captions(culture, new("Value", PivotAggregation.Max), new("Value", PivotAggregation.Min)));
            Assert.Equal("Max", PivotWords.Resolve(PivotWords.AggregationName(PivotAggregation.Max), null));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact] // ADR-0061, observed from pictures (R3 (b) in English (UK) and Japanese; the English (US) record, (b)): the Field List's two instruction lines end with a colon; ExPivot writes ':' U+003A, a code point no record read
    public void ADR0061_The_Field_Lists_two_instruction_lines_end_with_a_colon()
    {
        Assert.Equal("Choose fields to add to report:", PivotWords.EnglishFor("choose-fields"));
        Assert.Equal("Drag fields between areas below:", PivotWords.EnglishFor("drag-fields"));
        // R3 (b): UI Automation's names leave the colon out, so the character itself was not read. In
        // Japanese the colon on screen sits where U+003A sits in a drawing of the same text, and U+FF1A
        // begins about 10 px later; a full-width colon that Office draws narrower is not excluded.
        Assert.Equal("レポートに追加するフィールドを選択してください:", PivotWords.JapaneseFor("choose-fields"));
        Assert.Equal("次のボックス間でフィールドをドラッグしてください:", PivotWords.JapaneseFor("drag-fields"));
    }
}
