using System.Globalization;
using Bunit;
using ExGrid.Chrome;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Copy with headers is off on a Sheet (ADR-0048; ADR-0050 item 13): a Sheet's column letters are
/// addresses, not headers, so ExSheet declares that the grid does not offer the command, and a
/// Consumer that wants it switches it on with <c>AllowCopyWithHeaders</c>.
/// </summary>
public class CopyWithHeadersTests : SheetTestContext
{
    private static double HeadingWidth(IRenderedComponent<Components.ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    /// <summary>A secondary click on the cell at <paramref name="address"/>, which opens the Context Menu.</summary>
    private static Task SecondaryClickAsync(IRenderedComponent<Components.ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        return cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs
        {
            Button = 2,
            OffsetX = HeadingWidth(cut) + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * Components.ExSheet.DefaultRowHeightPx + 5,
        });
    }

    private static List<string> Items(IRenderedComponent<Components.ExSheet> cut) =>
        [.. cut.FindAll("[role=menu] button[role=menuitem]").Select(b => b.TextContent)];

    [Fact] // ADR-0048, ADR-0050 item 13: by default ExSheet declares it, the Context Menu offers Copy without copy with headers, and no other route copies with headers
    public async Task Copy_with_headers_is_off_by_default()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "B2", "5");
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");
        var grid = Grid(cut);
        var withHeaders = await grid.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync(withHeaders: true));

        Assert.True(grid.Instance.HideCopyWithHeaders);
        Assert.Contains("Copy", Items(cut));
        Assert.DoesNotContain("Copy with headers", Items(cut));
        Assert.Null(withHeaders);
    }

    [Fact] // ADR-0048, ADR-0050 item 13: AllowCopyWithHeaders stops the declaration; the command copies the Values with the column letters as the first row
    public async Task AllowCopyWithHeaders_offers_it()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.AllowCopyWithHeaders, true));
        await EnterAsync(cut, "B2", "5");
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");
        var grid = Grid(cut);
        var withHeaders = await grid.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync(withHeaders: true));

        Assert.False(grid.Instance.HideCopyWithHeaders);
        Assert.Contains("Copy with headers", Items(cut));
        Assert.NotNull(withHeaders);
        Assert.Equal("data", withHeaders!.Kind);
        Assert.StartsWith("B\r\n5", withHeaders.Text, StringComparison.Ordinal);
    }

    [Fact] // ADR-0050 item 13: the command's id is the grid's own, so a Chrome's wording for it is unchanged when switched on
    public async Task The_command_is_the_grids_own()
    {
        var cut = RenderSheet(ps => ps
            .Add(s => s.AllowCopyWithHeaders, true)
            .Add(s => s.CommandLabel, id => id == GridCommandIds.CopyWithHeaders ? "With letters" : null));
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");

        Assert.Contains("With letters", Items(cut));
    }
}
