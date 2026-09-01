using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The context menu (ADR-0036): what a secondary click does to the selection, whose
/// items the menu holds, and the keyboard's way in.
/// </summary>
public class ContextMenuTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private readonly TestRow[] _rows = TestRows.Many(50);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Func<ContextMenuContext<TestRow>, IEnumerable<GridCommand>>? contextCommands = null,
        TestRow[]? rows = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows ?? _rows)
              .Add(g => g.TotalCount, rows?.Length ?? 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (contextCommands is not null)
                ps.Add(g => g.ContextCommands, contextCommands);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task SecondaryClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = x, OffsetY = y });

    private static string[] Items(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll("[role=menu] button[role=menuitem]").Select(b => b.TextContent)];

    private static string[] Rects(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-selection div").Select(d => d.GetAttribute("style") ?? "").Distinct()];

    [Fact] // ADR-0036: the core's items are the clipboard's, because only the grid can build that payload
    public async Task The_menu_holds_the_cores_clipboard_commands()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await SecondaryClickAsync(cut, 50, 10);

        Assert.Equal(["Copy", "Copy with headers"], Items(cut));
    }

    [Fact] // ADR-0036: outside the selection the press collapses onto the cell it lands on
    public async Task A_secondary_click_outside_the_selection_moves_it_onto_that_cell()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);                       // row 0, Book
        var before = Rects(cut);

        await SecondaryClickAsync(cut, 250, 50);             // row 2, AsOf

        Assert.NotEqual(before, Rects(cut));
        Assert.Single(Rects(cut));
        Assert.NotEmpty(Items(cut));
    }

    [Fact] // ADR-0036: inside the selection it stands — the commands act on what is already chosen
    public async Task A_secondary_click_inside_the_selection_leaves_it_alone()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        var before = Rects(cut);

        await SecondaryClickAsync(cut, 50, 30);              // the second row of the range

        Assert.Equal(before, Rects(cut));
        Assert.NotEmpty(Items(cut));
    }

    [Fact] // ADR-0010 / ADR-0036: the Consumer's commands are appended to the core's
    public async Task A_consumers_commands_are_appended_and_receive_the_target()
    {
        ContextMenuContext<TestRow>? seen = null;
        var cut = RenderGrid(contextCommands: context =>
        {
            seen = context;
            return [new GridCommand("open-pricing", true, () => Task.CompletedTask)];
        });
        await ClickAsync(cut, 50, 10);

        await SecondaryClickAsync(cut, 150, 30);             // row 1, Amount

        Assert.Equal(["Copy", "Copy with headers", "open-pricing"], Items(cut));
        Assert.NotNull(seen);
        // The clicked cell is a row instance, and the selection is rectangles plus the
        // version they are written in — never rows (ADR-0036).
        Assert.Equal("Amount", seen.Column);
        Assert.Same(_rows[1], seen.Row);
        Assert.Single(seen.Selection);
    }

    [Fact] // ADR-0036: the menu is reachable without a mouse, on the Focus cell
    public async Task The_context_menu_key_opens_it_on_the_focus()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ContextMenu", false, false, false, false, false));

        Assert.Equal(["Copy", "Copy with headers"], Items(cut));
    }

    [Fact] // ADR-0010's dismissal rules carry over unchanged
    public async Task Escape_closes_it()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await SecondaryClickAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false));

        Assert.Empty(Items(cut));
    }
}
