using Bunit;
using ExGrid;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using HorizontalAlignment = ExSheet.Engine.HorizontalAlignment;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet's commands act on the grid's Selection as it is now (ticket 56; ADR-0050 item 14's note
/// of 2026-10-01). The grid raises a move after the render that shows it, a round trip later on a
/// circuit, so a toolbar button pressed straight after Shift+arrow reaches ExSheet before the move
/// does. Each test stages that order and says so: a page's own button that forwards the key and
/// gives the command in one handler, whose renders, and the notification after them, come after it
/// (<see cref="SheetWithCommand"/>); or the menu's own focus held, which on a circuit is the round
/// trip the notification waits behind. That a real circuit gives this order is layer 3's, on the
/// Server host.
/// </summary>
public class CommandSelectionTests : SheetTestContext
{
    private static CellFormat FormatAt(IRenderedComponent<ExSheet> sheet, string address) =>
        sheet.Instance.CellFormatAt(CellAddress.Parse(address));

    private static bool IsOpen(IRenderedComponent<ExSheet> sheet) => sheet.FindAll(".ex-format-cells").Count > 0;

    /// <summary>A key as the capture listener forwards it, from inside the page's handler.</summary>
    private static Task ForwardAsync(IRenderedComponent<ExSheet> sheet, string key, bool ctrl = false, bool shift = false) =>
        Grid(sheet).Instance.OnKeyAsync(key, ctrl, shift, false, false, false);

    /// <summary>
    /// Clicks the page's button, whose handler runs <paramref name="command"/>, and answers how many
    /// Selections the page had heard when the handler began, and when it reached the command.
    /// </summary>
    private static async Task<(int Before, int AtCommand)> ClickAsync(
        IRenderedComponent<SheetWithCommand> page, Func<Task> move, Func<Task> command)
    {
        int before = -1, atCommand = -1;
        page.Instance.Command = async () =>
        {
            before = page.Instance.Heard.Count;
            await move();
            atCommand = page.Instance.Heard.Count;
            await command();
        };
        await page.Find(".page-command").ClickAsync(new MouseEventArgs());
        return (before, atCommand);
    }

    // The three commands that set a Cell Format on the Selection, and how each shows on a cell.
    private static Task RunAsync(ExSheet sheet, string command) => command switch
    {
        nameof(ExSheet.SetCellFormatAsync) => sheet.SetCellFormatAsync(new CellFormatChange { Bold = true }),
        nameof(ExSheet.SetNumberFormatAsync) => sheet.SetNumberFormatAsync(NumberFormat.Parse("#,##0.00")),
        nameof(ExSheet.SetAlignmentAsync) => sheet.SetAlignmentAsync(HorizontalAlignment.Center),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a formatting command."),
    };

    private static bool Applied(CellFormat format, string command) => command switch
    {
        nameof(ExSheet.SetCellFormatAsync) => format.Font.Bold,
        nameof(ExSheet.SetNumberFormatAsync) => format.NumberFormat.Code == "#,##0.00",
        nameof(ExSheet.SetAlignmentAsync) => format.Alignment == HorizontalAlignment.Center,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a formatting command."),
    };

    [Theory] // ADR-0050 item 14's note / ADR-0063, SH-44 / ticket 56: a command given straight after Shift+ArrowDown, before ExSheet has heard the move, formats the extended range as one step
    [InlineData(nameof(ExSheet.SetCellFormatAsync))]
    [InlineData(nameof(ExSheet.SetNumberFormatAsync))]
    [InlineData(nameof(ExSheet.SetAlignmentAsync))]
    public async Task A_command_straight_after_a_move_formats_the_selection_the_grid_holds(string command)
    {
        var page = RenderPage<SheetWithCommand>();
        var sheet = page.FindComponent<ExSheet>();
        await GoToAsync(sheet, "A1");

        var (before, atCommand) = await ClickAsync(page,
            () => ForwardAsync(sheet, "ArrowDown", shift: true),
            () => RunAsync(sheet.Instance, command));

        // The order held: the command ran before the move's notification reached the page.
        Assert.Equal(before, atCommand);
        Assert.True(Applied(FormatAt(sheet, "A1"), command));
        Assert.True(Applied(FormatAt(sheet, "A2"), command));
        Assert.False(Applied(FormatAt(sheet, "A3"), command));
        // The notification landed after it, naming the range formatted.
        Assert.Equal([new SelectionRange(0, 0, 2, 1)], page.Instance.Heard[^1].Ranges);
        Assert.True(await sheet.Instance.UndoAsync());
        Assert.False(Applied(FormatAt(sheet, "A1"), command));
        Assert.False(Applied(FormatAt(sheet, "A2"), command));
        Assert.False(sheet.Instance.CanUndo);
    }

    [Fact] // ADR-0050 item 14's note / ADR-0063, SH-45 / ticket 56: OpenFormatCellsAsync straight after Shift+ArrowDown opens over the extended range, and the late notification naming it leaves Format Cells open
    public async Task Open_format_cells_straight_after_a_move_opens_over_the_selection_the_grid_holds()
    {
        var page = RenderPage<SheetWithCommand>();
        var sheet = page.FindComponent<ExSheet>();
        await GoToAsync(sheet, "A1");

        var (before, atCommand) = await ClickAsync(page,
            () => ForwardAsync(sheet, "ArrowDown", shift: true),
            () => sheet.Instance.OpenFormatCellsAsync());

        Assert.Equal(before, atCommand);
        Assert.Equal([new SelectionRange(0, 0, 2, 1)], page.Instance.Heard[^1].Ranges);
        sheet.WaitForAssertion(() => Assert.True(IsOpen(sheet)));
        await sheet.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == "Font").ClickAsync(new MouseEventArgs());
        await sheet.FindAll(".ex-format-cells-font-styles label").Single(l => l.TextContent.Trim() == "Bold")
            .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = "on" });
        await sheet.Find("form.ex-format-cells").SubmitAsync();
        Assert.True(FormatAt(sheet, "A1").Font.Bold);
        Assert.True(FormatAt(sheet, "A2").Font.Bold);
    }

    [Fact] // ADR-0050 item 14's note / ADR-0036 / ADR-0063 / ticket 56: the Context Menu's "Format Cells…" opens over the Selection the menu was opened on, whose notification waits behind the menu's focus
    public async Task The_context_menu_opens_format_cells_over_the_selection_it_was_opened_on()
    {
        var heard = new List<GridSelection>();
        var sheet = RenderSheet(ps => ps.Add(s => s.SelectionChanged, heard.Add));
        await GoToAsync(sheet, "B2");
        // The menu's first item takes DOM focus after the render that opens the menu: on a circuit a
        // round trip, held here, and the selection notification after it waits for it.
        var focus = JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true);

        // Outside the Selection, the secondary click collapses it onto C3 and opens the menu there.
        await SecondaryClickAsync(sheet, "C3");
        Assert.NotEmpty(sheet.FindAll("[role=menu]"));
        Assert.NotEqual(new CellPosition(2, 2), heard[^1].Focus);

        await sheet.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "Format Cells…").ClickAsync(new MouseEventArgs());
        sheet.WaitForAssertion(() => Assert.True(IsOpen(sheet)));
        await sheet.InvokeAsync(focus.SetVoidResult);

        // The notification has landed, naming C3, and Format Cells stands over it.
        Assert.Equal([new SelectionRange(2, 2, 1, 1)], heard[^1].Ranges);
        Assert.True(IsOpen(sheet));
        await sheet.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == "Font").ClickAsync(new MouseEventArgs());
        await sheet.FindAll(".ex-format-cells-font-styles label").Single(l => l.TextContent.Trim() == "Bold")
            .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = "on" });
        await sheet.Find("form.ex-format-cells").SubmitAsync();
        Assert.True(FormatAt(sheet, "C3").Font.Bold);
        Assert.False(FormatAt(sheet, "B2").Font.Bold);
    }

    [Fact] // ADR-0016 FN-12c / ADR-0048 / ticket 56: a whole-column resize reported before ExSheet has heard the whole columns selected is still one undo step
    public async Task A_whole_column_resize_before_the_selection_is_heard_is_one_step()
    {
        var page = RenderPage<SheetWithCommand>();
        var sheet = page.FindComponent<ExSheet>();
        await GoToAsync(sheet, "B1:D1");
        var grid = Grid(sheet);

        // Ctrl+Space selects columns B:D whole; the grid then reports their resize, one column at a
        // time, before the Selection's notification lands.
        var (before, atCommand) = await ClickAsync(page,
            () => ForwardAsync(sheet, " ", ctrl: true),
            async () =>
            {
                foreach (var column in new[] { "B", "C", "D" })
                    await grid.Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(column, 100));
            });

        Assert.Equal(before, atCommand);
        Assert.Equal([new SelectionRange(0, 1, Sheet.RowCount, 3)], page.Instance.Heard[^1].Ranges);
        Assert.All(new[] { 1, 2, 3 }, c => Assert.Equal(100, grid.Instance.Columns[c].Width.Width.FixedPx, 6));
        Assert.True(await sheet.Instance.UndoAsync());
        Assert.All(new[] { 1, 2, 3 }, c => Assert.Equal(SheetColumns.DefaultWidthPx, grid.Instance.Columns[c].Width.Width.FixedPx));
        Assert.False(sheet.Instance.CanUndo);
    }

    private static Task SecondaryClickAsync(IRenderedComponent<ExSheet> sheet, string address)
    {
        var at = CellAddress.Parse(address);
        var heading = double.Parse(
            sheet.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
        return sheet.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs
        {
            Button = 2,
            OffsetX = heading + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
    }
}
