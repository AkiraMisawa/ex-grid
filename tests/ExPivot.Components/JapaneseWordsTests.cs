using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The words of Excel's Japanese edition (ADR-0060, PV-33): chosen in one line, they replace every
/// word ExPivot paints — the pane, the toolbar, the menus, the panels, the report, Show Details —
/// and the ExGrid commands in the report's Context Menu. Over data whose captions and Items are
/// Japanese too, a Latin letter left anywhere is an English word left.
/// </summary>
public class JapaneseWordsTests : PivotTestContext
{
    /// <summary>A sale, in Japanese.</summary>
    public sealed record Uriage(string? Chiiki, string Shohin, decimal Kingaku);

    private static readonly Uriage[] Records =
    [
        new("東", "りんご", 100m),
        new("東", "なし", 50m),
        new("西", "りんご", 70m),
        new(null, "すもも", 5m),
    ];

    private static readonly PivotField<Uriage>[] JapaneseFields =
    [
        new("Region", PivotFieldType.Text, r => r.Chiiki, caption: "地域"),
        new("Product", PivotFieldType.Text, r => r.Shohin, caption: "製品"),
        new("Amount", PivotFieldType.Number, r => r.Kingaku, caption: "金額"),
    ];

    private static readonly PivotLayout Layout = new()
    {
        Filters = [P("Product") with { HiddenItems = [PivotItemKey.Text("すもも")] }],
        Rows = [P("Region")],
        Values = [Sum("Amount"), new PivotValueField("Region", PivotAggregation.Count)],
    };

    // Excel's Japanese edition keeps OK on its buttons; nothing else Latin is a word of its.
    private static readonly Regex Latin = new(@"[A-Za-z]+", RegexOptions.CultureInvariant);

    private IRenderedComponent<PivotComponent> RenderJapanese(Func<string, string?> label, PivotDetailsView view = PivotDetailsView.Tab)
        => RenderPivot(Layout, ps => ps
            .Add(p => p.Label, label)
            .Add(p => p.DetailsView, view)
            .Add(p => p.Caps, new PivotCaps { MaxColumns = 3 }),
            source: new OnDemandSource(PivotSource.From(Records, JapaneseFields),
                new PivotSourceFeatures([PivotAggregation.Sum, PivotAggregation.Count], canRefresh: true)) { AnswersAtOnce = true });

    /// <summary>Every word the pivot shows on every surface, and its accessible names — the
    /// pane, each entry's menu, each panel, the toolbar and its menu, a refusal, a Show Details
    /// tab and the report — collected as the user opens them.</summary>
    private static async Task<List<string>> WalkAsync(IRenderedComponent<PivotComponent> cut)
    {
        var seen = new List<string>();
        void Look() => seen.AddRange(Words(cut.Find(".ex-pivot")));

        Look();
        foreach (var (area, caption) in new[] { ("フィルター", "製品"), ("列", "Σ 値"), ("行", "地域"), ("値", "合計 / 金額"), ("値", "データの個数 / 地域") })
        {
            await OpenMenuAsync(cut, area, caption);
            Look();
            await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        await OpenMenuAsync(cut, "行", "地域");
        await RunMenuAsync(cut, "フィルター...");
        Look();
        await cut.Find(".ex-pivot-item-filter .ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "無" });
        Look();
        await cut.Find(".ex-pivot-cancel").ClickAsync(new MouseEventArgs());
        await OpenMenuAsync(cut, "行", "地域");
        await RunMenuAsync(cut, "フィールドの設定...");
        Look();
        await cut.Find(".ex-pivot-cancel").ClickAsync(new MouseEventArgs());
        await OpenMenuAsync(cut, "値", "合計 / 金額");
        await RunMenuAsync(cut, "値フィールドの設定...");
        Look();
        await cut.FindAll(".ex-pivot-value-settings input[type=text]")[1].InputAsync(new ChangeEventArgs { Value = "N999999999" });
        Look();
        await cut.Find(".ex-pivot-cancel").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        Look();
        await cut.Find(".ex-pivot-backdrop").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        Look();
        await cut.Find(".ex-pivot-backdrop").ClickAsync(new MouseEventArgs());
        // A refusal by name: Product in Columns would need more than the three columns allowed.
        await FieldItem(cut, "製品").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "列").DropAsync(new DragEventArgs());
        Look();
        await cut.Find(".ex-pivot-defer input").ChangeAsync(new ChangeEventArgs { Value = true });
        await TickFieldAsync(cut, "製品", false);
        Look();
        await cut.Find(".ex-pivot-defer input").ChangeAsync(new ChangeEventArgs { Value = false });
        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 1)));
        Look();
        return seen;
    }

    private static IEnumerable<string> Words(IElement root)
    {
        // The grid's own live region is ExGrid's sentence about the Selection, not a word of
        // ExPivot's or of the Context Menu's; none is spoken here, as nothing is selected.
        foreach (var node in root.Descendants().OfType<IText>())
        {
            if (node.ParentElement?.Closest(".ex-announce") is null)
                yield return node.Text;
        }
        foreach (var element in root.Descendants().OfType<IElement>())
        {
            foreach (var name in new[] { "aria-label", "title", "placeholder" })
            {
                if (element.GetAttribute(name) is { } value)
                    yield return value;
            }
        }
    }

    [Fact] // ADR-0060 (PV-33): with the Japanese words chosen, no English word is left on any surface of the pivot
    public async Task No_english_word_is_left()
    {
        var cut = RenderJapanese(PivotWords.Japanese);

        var seen = await WalkAsync(cut);

        var english = seen.SelectMany(text => Latin.Matches(text).Select(m => m.Value)).Where(word => word != "OK").Distinct().ToArray();
        Assert.Empty(english);
        Assert.Contains("ピボットテーブルのフィールド", seen);
        Assert.Contains("行ラベル", seen);
        Assert.Contains("合計 / 金額", seen);
        Assert.Contains("データの個数 / 地域", seen);
        Assert.Contains("総計", seen);
        Assert.Contains("(空白)", seen);
        Assert.Contains("レイアウト", seen);
        Assert.Contains(seen, text => text.StartsWith("詳細: ", StringComparison.Ordinal));
    }

    [Fact] // ADR-0060 (PV-33): every id ExPivot asks a word for on every surface, and every id of the report's Context Menu, has a word of the Japanese edition
    public async Task Every_id_the_pivot_uses_has_a_japanese_word()
    {
        var asked = new HashSet<string>(StringComparer.Ordinal);
        string? Recording(string id)
        {
            asked.Add(id);
            return PivotWords.JapaneseFor(id);
        }
        var cut = RenderJapanese(Recording);
        await WalkAsync(cut);

        var dialog = RenderJapanese(Recording, PivotDetailsView.Dialog);
        await dialog.InvokeAsync(() => Grid(dialog).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 1)));

        // The report's Context Menu: ExPivot's commands at a label cell and at a value cell, and
        // ExGrid's own, which the grid adds to every Context Menu.
        var grid = Grid(cut).Instance;
        var ids = new[] { grid.Columns[0].Name, grid.Columns[1].Name }
            .SelectMany(column => ContextCommands(cut, 0, column).Select(c => c.Id))
            .Concat([GridCommandIds.Copy, GridCommandIds.CopyWithHeaders])
            .Distinct()
            .ToArray();
        foreach (var id in ids)
        {
            var word = grid.CommandLabel!(id);
            Assert.NotNull(word);
            Assert.DoesNotMatch(Latin, word!);
        }
        Assert.Equal("\"地域\" の削除", grid.CommandLabel!(PivotCommandIds.RemoveNamedField + ":地域"));
        Assert.Equal("コピー", grid.CommandLabel!(GridCommandIds.Copy));

        Assert.NotEmpty(asked);
        Assert.All(asked.Where(id => !id.StartsWith(PivotCommandIds.RemoveNamedField + ":", StringComparison.Ordinal)),
            id => Assert.True(PivotWords.JapaneseFor(id) is not null, $"'{id}' has no Japanese word"));
        // The walk reached the new surfaces' words.
        Assert.Superset(new HashSet<string>(["layout-menu", "defer-layout-update", "update", "refresh", "field-list-toggle",
            "details-title", "report-tab", "sheets", "close-tab", "close", "refused-too-many-columns", "subtotals-at-top"]), asked);
    }

    [Fact] // ADR-0060 (PV-33): the words never follow the culture on their own — a Japanese culture without the Label keeps the English
    public void The_culture_alone_changes_no_word()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        cut.Render(ps => ps.Add(p => p.Culture, CultureInfo.GetCultureInfo("ja-JP")));

        Assert.Equal("PivotTable Fields", cut.Find(".ex-pivot-pane-title").TextContent);
        Assert.Equal(["Row Labels", "Sum of Amount"], HeaderTexts(cut));
        Assert.Equal("Layout", cut.Find(".ex-pivot-layout-button span").TextContent);
    }
}
