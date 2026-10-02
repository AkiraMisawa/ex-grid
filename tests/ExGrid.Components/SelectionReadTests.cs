using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The read of the grid's Held Selection a Consumer opts into for its own commands (ADR-0050, item
/// 14's note of 2026-10-01; ADR-0011; ticket 56). SelectionChanged is raised after the render that
/// shows a move, a round trip later on a circuit, so a toolbar button pressed straight after
/// Shift+arrow reaches the Consumer first. That ExSheet's commands act on what this answers is
/// ExSheet's suite's.
/// </summary>
public class SelectionReadTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridSelection> heard, int rowSequenceVersion = 0, Func<RowRange, Task>? rangeNeeded = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(20))
              .Add(g => g.TotalCount, 1000)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.RowSequenceVersion, rowSequenceVersion)
              .Add(g => g.SelectionChanged, heard.Add);
            if (rangeNeeded is not null)
                ps.Add(g => g.OnRangeNeeded, rangeNeeded);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0050 item 14's note / ADR-0011: the read answers the Selection the grid holds, in the version it is written in
    public async Task The_read_answers_the_held_selection_and_its_version()
    {
        var heard = new List<GridSelection>();
        var cut = RenderGrid(heard, rowSequenceVersion: 3);
        await ClickCellAsync(cut, 50, 30);
        await PressAsync(cut, "ArrowDown", shift: true);

        var held = cut.Instance.ReadSelection();

        Assert.Equal(3, held.RowSequenceVersion);
        Assert.Equal(heard[^1], held.Selection);
        Assert.Equal(new SelectionRange(1, 0, 2, 1), Assert.Single(held.Selection.Ranges));
        Assert.Equal(new CellPosition(1, 0), held.Selection.Focus);
    }

    [Fact] // ADR-0050 item 14's note: the read answers a move the Consumer has not heard yet — as on a circuit, where SelectionChanged is raised a round trip later
    public async Task The_read_answers_a_move_the_consumer_has_not_heard_yet()
    {
        var heard = new List<GridSelection>();
        var answer = new TaskCompletionSource();
        // A Window of twenty rows in a result of a thousand: Ctrl+Shift+End reveals the last row,
        // the grid asks for it, and until the Consumer has answered, the after-render path that
        // raises SelectionChanged waits behind the Range Request — the order a circuit gives when
        // the Consumer's next command reaches the core before the render's acknowledgement.
        var cut = RenderGrid(heard, rangeNeeded: _ => answer.Task);
        await ClickCellAsync(cut, 50, 30);
        var told = heard.Count;

        await PressAsync(cut, "ArrowDown", ctrl: true, shift: true);
        var held = await cut.InvokeAsync(cut.Instance.ReadSelection);

        Assert.Equal(told, heard.Count);
        Assert.Equal(new SelectionRange(1, 0, 999, 1), Assert.Single(held.Selection.Ranges));
        Assert.Equal(new CellPosition(1, 0), held.Selection.Focus);
        Assert.Equal(0, held.RowSequenceVersion);

        // Answered, the notification lands, naming the Selection the read already answered.
        await cut.InvokeAsync(answer.SetResult);
        Assert.Equal(held.Selection, heard[^1]);
    }

    [Fact] // ADR-0011: a new order drops the Selection, and the read answers nothing selected under the new version
    public async Task After_a_reorder_the_read_answers_nothing_under_the_new_version()
    {
        var heard = new List<GridSelection>();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 30);
        var before = cut.Instance.ReadSelection();

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        var held = cut.Instance.ReadSelection();
        Assert.Equal(1, held.RowSequenceVersion);
        Assert.True(held.Selection.IsEmpty);
        // A Consumer reconciling what it read before against the new version drops it too.
        Assert.True(before.Under(1).Selection.IsEmpty);
    }
}
