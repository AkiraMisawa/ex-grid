using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The core's half of the Settled Mark (ADR-0220): the two attributes it writes on the root's own first child,
/// which the script reads beside what the key gate knows. <c>data-ex-answered</c> names the last input
/// answered, every one before it included, and is written by the render after the one an input's
/// answer made, once that render's after-work has run; <c>data-ex-owing</c> stands while a render
/// waits on one of the grid's own timers. Neither reaches a row. The mark itself, and what the gate
/// holds, are the script's: layer 3's (settled-mark.spec.mjs). 20px rows; Book editable, Amount not,
/// each 100px.
/// </summary>
public class SettledMarkTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Func<TestRow, GridColumn<TestRow>, string?>? cellMessageOf = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (cellMessageOf is not null)
                ps.Add(g => g.CellMessageOf, cellMessageOf);
        });

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int input, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false, input: input));

    private static Task TellPressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string kind, int input, int doubleClickInput = 0)
        => cut.InvokeAsync(() => cut.Instance.PressTakenAt(kind, 50, 10, 0, 0, 0, 0, 0, input: input, doubleClickInput: doubleClickInput));

    private static MouseEventArgs Press(double x, double y, long buttons = 1, long detail = 1)
        => new() { Button = 0, Buttons = buttons, OffsetX = x, OffsetY = y, Detail = detail };

    private Task<string?> AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > .ex-settled-stamp").GetAttribute("data-ex-answered"));

    private Task<bool> OwingAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > .ex-settled-stamp").HasAttribute("data-ex-owing"));

    [Fact] // ADR-0220: the root names no input before any is answered, and each key once it has been
    public async Task ADR0220_the_root_names_the_last_key_answered()
    {
        var cut = RenderGrid();
        Assert.Equal("0", await AnsweredAsync(cut));

        await KeyAsync(cut, "ArrowDown", input: 1);
        Assert.Equal("1", await AnsweredAsync(cut));
        await KeyAsync(cut, "ArrowDown", input: 2);
        await KeyAsync(cut, "ArrowRight", input: 3);
        Assert.Equal("3", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0220: an input not yet answered holds back every one after it
    public async Task ADR0220_a_press_told_but_not_yet_heard_holds_back_the_keys_after_it()
    {
        var cut = RenderGrid();
        await KeyAsync(cut, "ArrowDown", input: 1);

        // The script told of a press, and Blazor has not delivered it yet: the key after it is
        // answered, but the root goes on naming the key before the press.
        await TellPressAsync(cut, "mousedown", input: 2);
        await KeyAsync(cut, "ArrowDown", input: 3);
        Assert.Equal("1", await AnsweredAsync(cut));

        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 10));
        Assert.Equal("3", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0220: a press told and never delivered is answered when the next one is told, never left owed
    public async Task ADR0220_a_press_told_and_never_delivered_is_answered_by_the_next_told()
    {
        var cut = RenderGrid();
        await TellPressAsync(cut, "mousedown", input: 1);
        await TellPressAsync(cut, "mousedown", input: 2);
        Assert.Equal("1", await AnsweredAsync(cut));

        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 10));
        Assert.Equal("2", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0220: a release that ends a double click is answered with the double click after it
    public async Task ADR0220_a_double_click_is_answered_after_the_release_before_it()
    {
        var cut = RenderGrid();
        var viewport = () => cut.Find(".ex-viewport");
        await TellPressAsync(cut, "mousedown", input: 1);
        await viewport().MouseDownAsync(Press(50, 10, detail: 2));
        await TellPressAsync(cut, "mouseup", input: 2, doubleClickInput: 3);
        await viewport().MouseUpAsync(Press(50, 10, buttons: 0, detail: 2));
        Assert.Equal("2", await AnsweredAsync(cut));

        await viewport().DoubleClickAsync(Press(50, 10, buttons: 0, detail: 2));
        Assert.Equal("3", await AnsweredAsync(cut));
        Assert.NotNull(cut.Find(".ex-editor"));
    }

    [Fact] // ADR-0220: an unnumbered input is no one's, and leaves the count where it was
    public async Task ADR0220_an_unnumbered_input_names_nothing()
    {
        var cut = RenderGrid();
        await KeyAsync(cut, "ArrowDown", input: 1);
        await KeyAsync(cut, "ArrowDown", input: 0);
        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 30));
        Assert.Equal("1", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0220 / ADR-0034: the root says a render is owed while the error message waits on the Focus standing still
    public async Task ADR0220_the_error_messages_wait_is_owed()
    {
        var cut = RenderGrid(cellMessageOf: (_, column) => column.Name == "Book" ? "check" : null);
        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 10));
        await cut.Find(".ex-viewport").MouseUpAsync(Press(50, 10, buttons: 0));
        Assert.True(await OwingAsync(cut));

        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(300)));
        Assert.False(await OwingAsync(cut));
        Assert.NotEmpty(cut.FindAll(".ex-popover-message, .ex-message"));
    }

    [Fact] // ADR-0220 / ADR-0033: the description of a selection, said once it has settled, is owed until then
    public async Task ADR0220_a_selections_description_is_owed_until_it_is_said()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 10));
        await cut.Find(".ex-viewport").MouseUpAsync(Press(50, 10, buttons: 0));
        await KeyAsync(cut, "ArrowDown", input: 1, shift: true);
        Assert.True(await OwingAsync(cut));

        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(150)));
        Assert.False(await OwingAsync(cut));
    }

    [Fact] // ADR-0220 / ADR-0004: the Placeholders filling after a scroll settles are owed until they fill
    public async Task ADR0220_the_placeholders_are_owed_until_the_scroll_settles()
    {
        var cut = RenderGrid();
        Assert.False(await OwingAsync(cut));
        await ScrollToAsync(cut.Find(".ex-scroller"), 2000, 0);
        Assert.True(await OwingAsync(cut));

        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.False(await OwingAsync(cut));
    }

    [Fact] // ADR-0220 / ADR-0003: the root's count moves with each key, and no row renders for it
    public async Task ADR0220_the_count_moves_and_the_rows_skip_their_render()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(Press(50, 10));
        await WhenIdleAsync();
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        for (var input = 1; input <= 3; input++)
            await KeyAsync(cut, "ArrowRight", input);
        await KeyAsync(cut, "ArrowLeft", input: 4);

        Assert.Equal("4", await AnsweredAsync(cut));
        Assert.Equal(counts, await ReadWhenIdleAsync(() => cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList()));
    }
}
