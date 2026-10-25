using System.Text.RegularExpressions;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The words of Excel's Japanese edition, bundled (ADR-0060, PV-33): a word for every id ExPivot
/// paints, chosen by the Consumer in one line, and never by the culture. The words themselves, and
/// reports laid out in them, are held on both engines by the shared cases (<c>tests/cases/pivot/</c>
/// <c>words.tables.json</c>, <c>words.json</c>); these are the rules over the C# table.
/// </summary>
public class JapaneseWordsTests
{
    [Fact] // ADR-0060: every id ExPivot has a word for has a word of the Japanese edition
    public void Every_id_has_a_japanese_word()
    {
        var missing = PivotWords.Ids.Where(id => PivotWords.JapaneseFor(id) is null).ToArray();

        Assert.Empty(missing);
    }

    [Fact] // ADR-0060: a Japanese template takes the same arguments as the English one
    public void Templates_take_the_same_arguments()
    {
        static string[] Holes(string word) => Regex.Matches(word, @"\{\d\}").Select(m => m.Value).Order().ToArray();

        Assert.All(PivotWords.Ids, id => Assert.Equal(Holes(PivotWords.EnglishFor(id)!), Holes(PivotWords.JapaneseFor(id)!)));
    }

    [Fact] // ADR-0060: an id the Japanese words do not know keeps the English, as any Label's null does
    public void An_unknown_id_keeps_the_english()
    {
        Assert.Null(PivotWords.Japanese("no-such-id"));
        Assert.Equal("Row Labels", PivotWords.Resolve(PivotWords.RowLabels, _ => null));
        Assert.Equal("行ラベル", PivotWords.Resolve(PivotWords.RowLabels, PivotWords.Japanese));
    }
}
