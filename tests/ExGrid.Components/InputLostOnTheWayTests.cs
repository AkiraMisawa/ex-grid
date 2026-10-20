using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Input made while the Blazor Server circuit was down (ADR-0240 section 11). The script sends each number
/// in the call made as it gives it, in order, over one connection: a number missing when a later one
/// arrives was sent while the circuit was down, and never will arrive. It is made void, so the input behind
/// it goes on; it is refused aloud, never replayed; and the script is told, so that what it waits on for
/// it ends. What the circuit drops is layer 3's; here the numbers are skipped by hand. 20px rows in a
/// 120px Viewport; Book and Amount, 100px each.
/// </summary>
public class InputLostOnTheWayTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        return Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns,
            [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));
    }

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int input)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, input: input));

    private Task<string?> AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered"));

    private static CellPosition Focus(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection.Focus;

    private static readonly string LostWords = BuiltInCommandLabels.For(KeyRefusalLabelIds.LostOnTheWay)!;

    [Fact] // ADR-0240 §11: a key arriving behind numbers that never did is handled, and the lost ones are refused aloud and never run
    public async Task ADR0240_a_key_behind_numbers_lost_on_the_way_is_handled_and_the_lost_ones_refused()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await KeyAsync(cut, "ArrowDown", input: 1);

        // 2 and 3, two more ↓, were sent while the circuit was down.
        await KeyAsync(cut, "ArrowDown", input: 4);

        Assert.Equal("4", await AnsweredAsync(cut));
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
        Assert.Equal(LostWords, cut.Find(".ex-announce").TextContent);
        var told = Assert.Single(Js.InputsLost.Invocations);
        Assert.Equal([2, 3], told.Arguments);
    }

    [Fact] // ADR-0240 §11: an input held behind a lost one sends how far the numbers went, and the order goes on with nothing numbered arriving
    public async Task ADR0240_told_how_far_the_numbers_went_the_core_refuses_the_lost_ones()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

        // 1, a letter that would open an edit, was sent while the circuit was down; what the script then
        // holds behind it says how far its numbers had gone.
        await cut.InvokeAsync(() => cut.Instance.InputsSent(1));

        Assert.Equal("1", await AnsweredAsync(cut));
        Assert.Empty(cut.FindAll("input.ex-editor"));
        Assert.Equal(LostWords, cut.Find(".ex-announce").TextContent);
        Assert.Equal([1, 1], Assert.Single(Js.InputsLost.Invocations).Arguments);

        await KeyAsync(cut, "ArrowDown", input: 2);
        Assert.Equal("2", await AnsweredAsync(cut));
        Assert.Equal(new CellPosition(1, 0), Focus(cut));
        Assert.Single(Js.InputsLost.Invocations);
    }

    [Fact] // ADR-0240 §11: numbers that all arrive, in order, are never taken for lost
    public async Task ADR0240_numbers_that_arrive_in_order_are_never_refused()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await KeyAsync(cut, "ArrowDown", input: 1);
        await cut.InvokeAsync(() => cut.Instance.InputsSent(1));
        await KeyAsync(cut, "ArrowDown", input: 2);

        Assert.Equal("2", await AnsweredAsync(cut));
        Assert.Empty(Js.InputsLost.Invocations);
        Assert.NotEqual(LostWords, cut.Find(".ex-announce").TextContent);
    }
}
