using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Ctrl+arrow asks where the data ends (ADR-0050, item 2): with the Consumer's edge
/// answer the Focus moves to it, and Ctrl+Shift+arrow extends to it; without one, both go
/// to the grid's edge as ADR-0012 has it. The answers here are stubs — what Excel answers
/// over blocks and gaps is ExSheet's table.
/// </summary>
public class EdgeAnswerTests
{
    private static readonly GridExtent Grid = new(100, 26);

    // A block ends at row 9 below anything above it, and at column 4 to the right.
    private static CellPosition Stub(CellPosition origin, GridDirection direction) => direction switch
    {
        GridDirection.Down => origin with { Row = Math.Max(origin.Row, 9) },
        GridDirection.Up => origin with { Row = 0 },
        GridDirection.Right => origin with { Column = Math.Max(origin.Column, 4) },
        _ => origin with { Column = 0 },
    };

    [Fact] // ADR-0050 / DC-7: with an answer, Ctrl+arrow moves the Focus to it and collapses
    public void Ctrl_arrow_moves_to_the_answer()
    {
        var selection = GridSelection.Empty.Click(new(3, 2), Grid).MoveToEdge(GridDirection.Down, Grid, Stub);

        Assert.Equal(new CellPosition(9, 2), selection.Focus);
        Assert.Equal([new SelectionRange(9, 2, 1, 1)], selection.Ranges);
    }

    [Fact] // ADR-0050/0052 / DC-7: with an answer, Ctrl+Shift+arrow runs the Extent to it and the Focus stays
    public void Ctrl_shift_arrow_extends_to_the_answer()
    {
        var selection = GridSelection.Empty.Click(new(3, 2), Grid).ExtendToEdge(GridDirection.Right, Grid, Stub);

        Assert.Equal([new SelectionRange(3, 2, 1, 3)], selection.Ranges);
        Assert.Equal(new CellPosition(3, 2), selection.Focus);
        Assert.Equal(new CellPosition(3, 4), selection.Extent);
    }

    [Fact] // ADR-0052 (DC-7, Excel item 2): the answer is asked for the Extent, so a second press goes on from where the first stopped
    public void A_second_ctrl_shift_arrow_asks_from_the_extent()
    {
        var asked = new List<CellPosition>();
        CellPosition Recording(CellPosition origin, GridDirection direction)
        {
            asked.Add(origin);
            return origin.Column < 4 ? origin with { Column = 4 } : origin with { Column = 25 };
        }

        var selection = GridSelection.Empty.Click(new(3, 2), Grid)
            .ExtendToEdge(GridDirection.Right, Grid, Recording)
            .ExtendToEdge(GridDirection.Right, Grid, Recording);

        Assert.Equal([new CellPosition(3, 2), new CellPosition(3, 4)], asked);
        Assert.Equal([new SelectionRange(3, 2, 1, 24)], selection.Ranges);
        Assert.Equal(new CellPosition(3, 2), selection.Focus);
    }

    [Fact] // ADR-0050/0012 / DC-7: without an answer, both go to the grid's edge as before
    public void Without_an_answer_the_edge_is_the_grids()
    {
        var start = GridSelection.Empty.Click(new(3, 2), Grid);

        Assert.Equal(start.MoveToEdge(GridDirection.Down, Grid), start.MoveToEdge(GridDirection.Down, Grid, null));
        Assert.Equal(start.ExtendToEdge(GridDirection.Left, Grid), start.ExtendToEdge(GridDirection.Left, Grid, null));
    }

    [Fact] // ADR-0050/0012: whole columns stay whole when extended to the answer
    public void A_whole_column_extended_to_the_answer_stays_whole()
    {
        var selection = GridSelection.Empty.Click(new(3, 2), Grid).SelectWholeColumns(Grid)
            .ExtendToEdge(GridDirection.Right, Grid, Stub);

        Assert.Equal([new SelectionRange(0, 2, 100, 3)], selection.Ranges);
    }

    [Theory] // ADR-0050: an answer that is not on the Focus's line ahead of it is refused by name
    [InlineData(5, 3)]   // off the column
    [InlineData(1, 2)]   // behind the Focus
    [InlineData(100, 2)] // outside the grid
    public void An_answer_off_the_line_is_refused(int row, int column)
    {
        var start = GridSelection.Empty.Click(new(3, 2), Grid);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => start.MoveToEdge(GridDirection.Down, Grid, (_, _) => new CellPosition(row, column)));
        Assert.Contains("ADR-0050", refusal.Message);
    }

    [Fact] // ADR-0050: answering the Focus itself is a legal "nowhere to go"
    public void Answering_the_focus_itself_moves_nothing()
    {
        var start = GridSelection.Empty.Click(new(3, 2), Grid);

        Assert.Equal(start, start.MoveToEdge(GridDirection.Down, Grid, (origin, _) => origin));
    }
}
