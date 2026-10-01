using System.Globalization;
using System.Text.RegularExpressions;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The words of Excel's Japanese edition, bundled (ADR-0059, PV-33): a word for every id ExPivot
/// paints, chosen by the Consumer in one line, and never by the culture.
/// </summary>
public class JapaneseWordsTests
{
    private static readonly PivotOptions Japanese = new() { Culture = CultureInfo.GetCultureInfo("en-US"), Label = PivotWords.Japanese };

    [Fact] // ADR-0059: every id ExPivot has a word for has a word of the Japanese edition
    public void Every_id_has_a_japanese_word()
    {
        var missing = PivotWords.Ids.Where(id => PivotWords.JapaneseFor(id) is null).ToArray();

        Assert.Empty(missing);
    }

    [Fact] // ADR-0059: a Japanese template takes the same arguments as the English one
    public void Templates_take_the_same_arguments()
    {
        static string[] Holes(string word) => Regex.Matches(word, @"\{\d\}").Select(m => m.Value).Order().ToArray();

        Assert.All(PivotWords.Ids, id => Assert.Equal(Holes(PivotWords.EnglishFor(id)!), Holes(PivotWords.JapaneseFor(id)!)));
    }

    [Fact] // ADR-0059: the Japanese words are Excel's — 行ラベル, 総計, 合計 / 金額, データの個数 / 地域, (空白), (すべて), (複数のアイテム)
    public void The_words_are_excels()
    {
        Assert.Equal("行ラベル", PivotWords.Japanese(PivotWords.RowLabels));
        Assert.Equal("総計", PivotWords.Japanese(PivotWords.GrandTotal));
        Assert.Equal("合計 / 金額", PivotWords.Fill(PivotWords.Japanese(PivotWords.CaptionOf(PivotAggregation.Sum))!, "金額"));
        Assert.Equal("データの個数 / 地域", PivotWords.Fill(PivotWords.Japanese(PivotWords.CaptionOf(PivotAggregation.Count))!, "地域"));
        Assert.Equal("(空白)", PivotWords.Japanese(PivotWords.Blank));
        Assert.Equal("(すべて)", PivotWords.Japanese(PivotWords.All));
        Assert.Equal("(複数のアイテム)", PivotWords.Japanese(PivotWords.MultipleItems));
        Assert.Equal("ピボットテーブルのフィールド", PivotWords.Japanese("field-list"));
        Assert.Equal("値", PivotWords.Japanese(PivotWords.Values));
    }

    [Fact] // ADR-0059: the ExGrid commands in the report's Context Menu have Japanese words too, which ExGrid ships only in English
    public void The_grids_context_menu_commands_have_japanese_words()
    {
        Assert.Equal("コピー", PivotWords.Japanese("copy"));
        Assert.Equal("見出し付きでコピー", PivotWords.Japanese("copy-with-headers"));
    }

    [Fact] // ADR-0059: a report laid out with the Japanese words, as a server would lay it out
    public void A_report_in_japanese_words()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Value("Region", PivotAggregation.Count)] };

        var report = Report(layout, options: Japanese);

        Assert.Equal(["行ラベル", "合計 / Amount", "データの個数 / Region"], Headers(report));
        Assert.Equal("t 総計 || 285 | 6", Lines(report)[^1]);
        Assert.Equal("i (空白) || 5 |", Lines(report)[^2]);
    }

    [Fact] // ADR-0059: the words never follow the culture on their own — a Japanese culture without the Label stays English
    public void The_culture_alone_changes_no_word()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };

        var report = Report(layout, options: new PivotOptions { Culture = CultureInfo.GetCultureInfo("ja-JP") });

        Assert.Equal(["Row Labels", "Sum of Amount"], Headers(report));
        Assert.Equal("t Grand Total || 285", Lines(report)[^1]);
    }

    [Fact] // ADR-0059: an id the Japanese words do not know keeps the English, as any Label's null does
    public void An_unknown_id_keeps_the_english()
    {
        Assert.Null(PivotWords.Japanese("no-such-id"));
        Assert.Equal("Row Labels", PivotWords.Resolve(PivotWords.RowLabels, _ => null));
        Assert.Equal("行ラベル", PivotWords.Resolve(PivotWords.RowLabels, PivotWords.Japanese));
    }
}
