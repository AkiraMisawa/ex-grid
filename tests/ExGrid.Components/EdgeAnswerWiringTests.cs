using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The edge answer, wired (ADR-0050, item 2; DC-7): asked on Ctrl+arrow and
/// Ctrl+Shift+arrow, never on Home and End, and without one the grid's edge as before.
/// 50 rows of 20px, four 100px columns.
/// </summary>
public class EdgeAnswerWiringTests : GridTestContext
{
    private readonly List<(CellPosition From, GridDirection Direction)> _asked = [];

    private CellPosition Stub(CellPosition origin, GridDirection direction)
    {
        _asked.Add((origin, direction));
        return direction switch
        {
            GridDirection.Down => origin with { Row = Math.Max(origin.Row, 7) },
            GridDirection.Right => origin with { Column = Math.Max(origin.Column, 2) },
            _ => origin,
        };
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Action<GridSelection> onSelection, bool withAnswer)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(4))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection);
            if (withAnswer)
                ps.Add(g => g.DataEdge, Stub);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0050 / DC-7: Ctrl+↓ moves the Focus to the Consumer's answer
    public async Task Ctrl_down_moves_to_the_answer()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, withAnswer: true);
        await ClickAsync(cut, 50, 45);

        await PressAsync(cut, "ArrowDown", ctrl: true);

        Assert.Equal(new CellPosition(7, 0), selection!.Focus);
        Assert.Equal([(new CellPosition(2, 0), GridDirection.Down)], _asked);
    }

    [Fact] // ADR-0050 / DC-7: Ctrl+Shift+→ extends the range to the Consumer's answer
    public async Task Ctrl_shift_right_extends_to_the_answer()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, withAnswer: true);
        await ClickAsync(cut, 50, 45);

        await PressAsync(cut, "ArrowRight", ctrl: true, shift: true);

        Assert.Equal([new SelectionRange(2, 0, 1, 3)], selection!.Ranges);
    }

    [Fact] // ADR-0050/0012: Home and End never ask — they keep the row's edges
    public async Task Home_and_end_do_not_ask_the_answer()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, withAnswer: true);
        await ClickAsync(cut, 150, 45);

        await PressAsync(cut, "End");

        Assert.Equal(new CellPosition(2, 3), selection!.Focus);
        Assert.Empty(_asked);
    }

    [Fact] // ADR-0050/0012 / DC-7: without an answer, Ctrl+↓ and Ctrl+Shift+→ go to the grid's edge
    public async Task Without_an_answer_the_grids_edge()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, withAnswer: false);
        await ClickAsync(cut, 50, 45);

        await PressAsync(cut, "ArrowRight", ctrl: true, shift: true);
        Assert.Equal([new SelectionRange(2, 0, 1, 4)], selection!.Ranges);

        await PressAsync(cut, "ArrowDown", ctrl: true);
        Assert.Equal(new CellPosition(49, 3), selection.Focus);
    }
}
