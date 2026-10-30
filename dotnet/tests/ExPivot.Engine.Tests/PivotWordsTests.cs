using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0060, "Captions and words": ExPivot's English is Excel's English (US) edition's, and the English
/// (UK) edition's words are bundled where they differ (ADR-0060's notes of 2026-10-12 and 2026-10-14).
/// The captions, numbered as Excel numbers them, and every word of every edition are held on both engines
/// by the shared cases (<c>tests/cases/pivot/words.json</c>, <c>words.tables.json</c>); these are the
/// rules over the C# tables, and that the C# engine reads no culture of its own for a word.
/// </summary>
public class PivotWordsTests
{
    private sealed record Obs(string Group, object? Value);

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Value", PivotFieldType.Number, o => o.Value),
    ];

    private static IReadOnlyList<string> Captions(PivotOptions options, params PivotValueField[] values)
        => PivotEngine.Compute([new Obs("A", 1)], ObsFields, new PivotLayout { Values = values }, options).ValueCaptions;

    [Fact] // ADR-0060's note of 2026-10-14, observed (R3 (a) and (d)): only five of the English (UK) edition's words differ, and each takes the arguments the English one takes
    public void ADR0060_The_English_UK_edition_differs_in_five_words_of_the_same_arguments()
    {
        Assert.Equal(["aggregation-max", "aggregation-min", "caption-max", "caption-min", "summarize-by"],
            PivotWords.Ids.Where(id => PivotWords.EnglishUnitedKingdomFor(id) is not null).Order(StringComparer.Ordinal));
        Assert.All(PivotWords.Ids.Where(id => PivotWords.EnglishUnitedKingdomFor(id) is not null), id =>
            Assert.Equal(Holes(PivotWords.EnglishFor(id)!), Holes(PivotWords.EnglishUnitedKingdomFor(id)!)));
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
}
