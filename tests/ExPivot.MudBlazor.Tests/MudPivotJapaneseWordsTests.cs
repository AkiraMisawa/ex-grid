using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using ExGrid.Selection;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// The words of Excel's Japanese edition on the surfaces MudPivotChrome draws (ADR-0060/0062,
/// PV-33): the toolbar and its Layout menu, Show Details' tabs and the dialog speak ExPivot's words
/// by id, so one <c>Label</c> words them all. Over Japanese data, a Latin letter left on them is an
/// English word left.
/// </summary>
public class MudPivotJapaneseWordsTests : MudPivotTestContext
{
    /// <summary>A sale, in Japanese.</summary>
    public sealed record Uriage(string? Chiiki, string Shohin, decimal Kingaku);

    private static readonly Uriage[] Records =
    [
        new("東", "りんご", 100m),
        new("東", "なし", 50m),
        new("西", "りんご", 70m),
    ];

    private static readonly PivotField<Uriage>[] JapaneseFields =
    [
        new("Region", PivotFieldType.Text, r => r.Chiiki, caption: "地域"),
        new("Product", PivotFieldType.Text, r => r.Shohin, caption: "製品"),
        new("Amount", PivotFieldType.Number, r => r.Kingaku, caption: "金額"),
    ];

    private static readonly PivotLayout Layout = new()
    {
        Filters = [P("Product")],
        Rows = [P("Region")],
        Values = [Sum("Amount")],
    };

    // Excel's Japanese edition keeps OK on its buttons; nothing else Latin is a word of its.
    private static readonly Regex Latin = new(@"[A-Za-z]+", RegexOptions.CultureInvariant);

    /// <summary>A source that can be refreshed, so the toolbar offers Refresh too.</summary>
    private static PivotSource Source()
    {
        var data = PivotSource.From(Records, JapaneseFields);
        return PivotSource.Fetch(JapaneseFields, new PivotSourceFeatures([PivotAggregation.Sum, PivotAggregation.Count], canRefresh: true),
            data.AggregateAsync, data.ItemsAsync, data.DetailsAsync);
    }

    /// <summary>Every word an element shows, and the accessible names in it, its own included.</summary>
    private static IEnumerable<string> Words(IElement root)
    {
        foreach (var node in root.Descendants().OfType<IText>())
            yield return node.Text;
        foreach (var element in root.Descendants().OfType<IElement>().Prepend(root))
        {
            foreach (var name in new[] { "aria-label", "title", "placeholder" })
            {
                if (element.GetAttribute(name) is { } value)
                    yield return value;
            }
        }
    }

    [Fact] // ADR-0060/0062 (PV-33): under MudBlazor, the toolbar, the Layout menu, Show Details' tabs and the dialog speak Excel's Japanese words, with no English word left
    public async Task The_new_surfaces_speak_the_japanese_words()
    {
        var seen = new List<string>();
        var cut = RenderPivot(Layout, label: PivotWords.Japanese, source: Source());
        void Look(IRenderedComponent<PivotComponent> pivot, string selector)
        {
            foreach (var element in pivot.FindAll(selector))
                seen.AddRange(Words(element));
        }

        Look(cut, ".mud-ex-pivot-toolbar");
        await cut.Find(".mud-ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        Look(cut, ".mud-ex-pivot-toolbar");
        await cut.Find(".ex-pivot-backdrop").ClickAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 1)));
        Look(cut, ".mud-ex-pivot-tabs");

        var dialog = RenderPivot(Layout, label: PivotWords.Japanese, source: Source(), detailsView: PivotDetailsView.Dialog);
        await dialog.InvokeAsync(() => Grid(dialog).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 1)));
        Look(dialog, ".mud-ex-pivot-dialog-title");
        Look(dialog, ".mud-ex-pivot-dialog-actions");

        var english = seen.SelectMany(text => Latin.Matches(text).Select(m => m.Value)).Where(word => word != "OK").Distinct().ToArray();
        Assert.Empty(english);
        Assert.Contains("レイアウト", seen);
        Assert.Contains("更新", seen);
        Assert.Contains("フィールド リスト", seen);
        Assert.Contains("シート", seen);
        Assert.Contains("ピボットテーブル", seen);
        Assert.Contains("詳細: 東", seen);
        Assert.Contains("詳細: 東 を閉じる", seen);
        Assert.Contains("閉じる", seen);
    }
}
