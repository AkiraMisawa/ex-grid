using Bunit;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Copy with headers left out (ADR-0050, item 13): a Consumer declares that the grid does not
/// offer it, and neither the Context Menu nor any other route reaches it. Without the
/// declaration the menu is exactly as before (DC-1).
/// </summary>
public class CopyWithHeadersDeclarationTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        bool hideCopyWithHeaders,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(20))
              .Add(g => g.TotalCount, 20)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.HideCopyWithHeaders, hideCopyWithHeaders);
            extra?.Invoke(ps);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task SecondaryClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = x, OffsetY = y });

    private static string[] Items(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll("[role=menu] button[role=menuitem]").Select(b => b.TextContent)];

    [Fact] // ADR-0050 item 13: declared, the Context Menu holds Copy and not copy with headers
    public async Task Declared_the_menu_holds_copy_alone()
    {
        var cut = RenderGrid(hideCopyWithHeaders: true);
        await ClickAsync(cut, 50, 10);

        await SecondaryClickAsync(cut, 50, 10);

        Assert.Equal(["Copy"], Items(cut));
    }

    [Fact] // ADR-0050 item 13: the keyboard's way into the menu leaves it out too
    public async Task Declared_the_context_menu_key_opens_copy_alone()
    {
        var cut = RenderGrid(hideCopyWithHeaders: true);
        await ClickAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ContextMenu", false, false, false, false, false));

        Assert.Equal(["Copy"], Items(cut));
    }

    [Fact] // ADR-0050 item 13 / ADR-0036: the Consumer's commands follow Copy, and the context handed to it does not hold the command either
    public async Task Declared_the_consumer_is_handed_no_copy_with_headers()
    {
        ContextMenuContext<TestRow>? seen = null;
        var cut = RenderGrid(hideCopyWithHeaders: true, ps => ps.Add(g => g.ContextCommands, context =>
        {
            seen = context;
            return [new GridCommand("open-pricing", true, () => Task.CompletedTask)];
        }));
        await ClickAsync(cut, 50, 10);

        await SecondaryClickAsync(cut, 50, 10);

        Assert.Equal(["Copy", "open-pricing"], Items(cut));
        Assert.NotNull(seen);
        Assert.DoesNotContain(seen.Commands, c => c.Id == GridCommandIds.CopyWithHeaders);
    }

    [Fact] // ADR-0050 item 13 / ADR-0005: a copy asked with headers by another route is refused, never written without them; a plain copy still copies
    public async Task Declared_a_copy_with_headers_is_refused_by_every_route()
    {
        var requests = new List<GridCopyRequest>();
        var cut = RenderGrid(hideCopyWithHeaders: true, ps => ps.Add(g => g.CopyAnswer, (GridCopyRequest r) =>
        {
            requests.Add(r);
            return GridCopyAnswer.Write("t", "h");
        }));
        await ClickAsync(cut, 50, 10);

        var withHeaders = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync(withHeaders: true));
        var plain = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());

        Assert.Null(withHeaders);
        Assert.NotNull(plain);
        Assert.Equal("data", plain!.Kind);
        var request = Assert.Single(requests);
        Assert.False(request.WithHeaders);
    }

    [Fact] // ADR-0050 item 13 / DC-1: without the declaration the menu is exactly as before, and a copy with headers is built
    public async Task Undeclared_copy_with_headers_is_offered_as_before()
    {
        var requests = new List<GridCopyRequest>();
        var cut = RenderGrid(hideCopyWithHeaders: false, ps => ps.Add(g => g.CopyAnswer, (GridCopyRequest r) =>
        {
            requests.Add(r);
            return GridCopyAnswer.Write("t", "h");
        }));
        await ClickAsync(cut, 50, 10);

        await SecondaryClickAsync(cut, 50, 10);
        var withHeaders = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync(withHeaders: true));

        Assert.Equal(["Copy", "Copy with headers"], Items(cut));
        Assert.NotNull(withHeaders);
        Assert.True(Assert.Single(requests).WithHeaders);
    }
}
