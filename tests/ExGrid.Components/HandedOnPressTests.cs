using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press handed on keeps its place among the keys of the grid that points (ADR-0058, "On a
/// circuit"; ADR-0021's note of 2026-09-30; DC-54). While the grid is pointed at, its render names
/// the root of the grid that points, and its script tells that root of each primary press, telling
/// this core first: the press, and whether it is in turn already — nothing held before it there — or,
/// later, that it now is (<c>PressHandedOnAsync</c>, <c>PressInTurn</c>). The press is handed over
/// only once it is in turn, and the task the script was given completes once it has been answered,
/// which is how long the grid that points holds the keys typed after it. The script is stood in for
/// by calling those two directly. 50 rows of 20px in a 350 × 200 Viewport under a 20px header; Book,
/// Note and Amount are 100px each.
/// </summary>
public class HandedOnPressTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private readonly List<GridPointedPress<TestRow>> _presses = [];

    // What the Consumer's answer to each press waits on, until the test lets it finish.
    private TaskCompletionSource _consumerAnswers = CompletedSource();

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A declaration pointed at from the root <paramref name="root"/>, recording every press
    /// handed over and answering it when <see cref="_consumerAnswers"/> completes.</summary>
    private GridPointedAt<TestRow> Declaration(string? root = "ex-sheet-root")
        => new(press =>
        {
            _presses.Add(press);
            return _consumerAnswers.Task;
        })
        {
            IsPointedAt = true,
            PointingRootId = root,
        };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(GridPointedAt<TestRow> pointedAt, TestRow[]? rows = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows ?? TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PointedAt, pointedAt));

    /// <summary>A press on the rows, as Blazor dispatches it: not awaited, since a press waiting for
    /// its turn has not been answered.</summary>
    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, long button = 0)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = button, Buttons = button == 0 ? 1 : 2, OffsetX = x, OffsetY = y, ClientX = x, ClientY = 20 + y,
        });

    /// <summary>What the script tells the core ahead of a press it handed on: the task it is given.</summary>
    private static async Task<Task> HandedOnAsync(IRenderedComponent<ExGrid<TestRow>> cut, int press, bool inTurn)
    {
        Task answered = null!;
        await cut.InvokeAsync(() => { answered = cut.Instance.PressHandedOnAsync(press, inTurn); });
        return answered;
    }

    /// <summary>What the script tells the core when the press's turn comes; the hand-over it lets go
    /// runs on the renderer's context after it.</summary>
    private static async Task InTurnAsync(IRenderedComponent<ExGrid<TestRow>> cut, int press)
    {
        await cut.InvokeAsync(() => cut.Instance.PressInTurn(press));
        await cut.InvokeAsync(() => { });
    }

    private static string? PointedFrom(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-grid").GetAttribute("data-ex-pointed-from");

    [Fact] // ADR-0058 ("On a circuit") / DC-54: the root carries its id always, and names the root that points only while pointed at
    public async Task The_render_names_the_root_that_points_only_while_pointed_at()
    {
        var declaration = Declaration();
        var cut = RenderGrid(declaration);

        Assert.Equal(cut.Instance.RootId, cut.Find(".ex-grid").GetAttribute("id"));
        Assert.Matches("^ex[0-9]+-root$", cut.Instance.RootId);
        Assert.Equal("ex-sheet-root", PointedFrom(cut));

        await cut.InvokeAsync(() => declaration.PointingRootId = "ex-other-root");
        Assert.Equal("ex-other-root", PointedFrom(cut));

        await cut.InvokeAsync(() => declaration.IsPointedAt = false);
        Assert.Null(PointedFrom(cut));

        // Pointed at, with no root named: nothing is named, and a press is handed over at once.
        await cut.InvokeAsync(() =>
        {
            declaration.PointingRootId = null;
            declaration.IsPointedAt = true;
        });
        Assert.Contains("ex-pointed-at", cut.Find(".ex-grid").GetAttribute("class"));
        Assert.Null(PointedFrom(cut));
    }

    [Fact] // ADR-0018: two grids on a page have roots of their own
    public void Each_grid_has_a_root_id_of_its_own()
    {
        var first = RenderGrid(Declaration());
        var second = RenderGrid(Declaration());

        Assert.NotEqual(first.Instance.RootId, second.Instance.RootId);
    }

    [Fact] // ADR-0058 ("On a circuit") / ADR-0021 (note of 2026-09-30) / DC-54: a press not yet in turn is handed over once it is, and answered once the Consumer has answered it
    public async Task A_press_out_of_turn_is_handed_over_once_in_turn()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);
        _consumerAnswers = new TaskCompletionSource();

        var answered = await HandedOnAsync(cut, 1, inTurn: false);
        var pressing = PressAsync(cut, 250, 70);

        // Keys typed before it are still held where the Sheet is: nothing is handed over yet.
        Assert.Empty(_presses);
        Assert.False(answered.IsCompleted);

        await InTurnAsync(cut, 1);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[3], "Amount")], _presses);
        // Handed over, and not answered until the Consumer has: the keys typed after it still wait.
        Assert.False(answered.IsCompleted);
        await cut.InvokeAsync(() => _consumerAnswers.SetResult());
        await answered.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        await pressing.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
    }

    [Fact] // ADR-0058 ("On a circuit") / DC-54: a press in turn at once is handed over at once
    public async Task A_press_in_turn_at_once_is_handed_over_at_once()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);

        var answered = await HandedOnAsync(cut, 1, inTurn: true);
        await PressAsync(cut, 50, 30);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[1], "Book")], _presses);
        Assert.True(answered.IsCompletedSuccessfully);
    }

    [Fact] // ADR-0058 / DC-54 / ADR-0010: a press that hands nothing over is answered at once, so no keys wait for the two-second fallback
    public async Task A_press_that_hands_nothing_over_is_answered_at_once()
    {
        var cut = RenderGrid(Declaration());

        var answered = await HandedOnAsync(cut, 1, inTurn: false);
        // Past the last column: the rows are dead space there.
        await PressAsync(cut, 320, 30);

        Assert.Empty(_presses);
        Assert.True(answered.IsCompletedSuccessfully);
        // Its turn, when it comes, finds nothing waiting.
        await InTurnAsync(cut, 1);
        Assert.Empty(_presses);
    }

    [Fact] // ADR-0058 / DC-54: the script tells the core only of a primary press, and a secondary one takes nothing told of
    public async Task A_secondary_press_takes_nothing_told_of()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);

        var answered = await HandedOnAsync(cut, 1, inTurn: false);
        await PressAsync(cut, 50, 30, button: 2);
        Assert.False(answered.IsCompleted);

        var pressing = PressAsync(cut, 50, 30);
        Assert.Empty(_presses);
        await InTurnAsync(cut, 1);
        await pressing.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[1], "Book")], _presses);
        Assert.True(answered.IsCompletedSuccessfully);
    }

    [Fact] // ADR-0058 / DC-54: a press never heard is answered when the next is told of, rather than hold keys
    public async Task A_press_told_of_and_never_heard_is_answered_at_the_next()
    {
        var cut = RenderGrid(Declaration());

        var first = await HandedOnAsync(cut, 1, inTurn: false);
        var second = await HandedOnAsync(cut, 2, inTurn: true);

        Assert.True(first.IsCompletedSuccessfully);
        Assert.False(second.IsCompleted);
        await PressAsync(cut, 50, 30);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.Single(_presses);
    }

    [Fact] // ADR-0058 ("What is written", a drag) / DC-54: a drag from a press waiting for its turn is handed over after that press
    public async Task A_drag_is_handed_over_after_the_press_it_began_with()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);

        var answered = await HandedOnAsync(cut, 1, inTurn: false);
        var pressing = PressAsync(cut, 50, 50);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = 90 });
        Assert.Empty(_presses);

        await InTurnAsync(cut, 1);
        await pressing.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[2], "Book"),
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralCells, Dragged: true),
        ], _presses);
        Assert.True(answered.IsCompletedSuccessfully);
    }

    [Fact] // ADR-0058 / DC-54 / ADR-0018: a grid disposed lets go of a press waiting for its turn, and hands it over no more
    public async Task Disposing_lets_go_of_a_press_waiting_for_its_turn()
    {
        var cut = RenderGrid(Declaration());

        var answered = await HandedOnAsync(cut, 1, inTurn: false);
        var pressing = PressAsync(cut, 50, 30);
        var told = await HandedOnAsync(cut, 2, inTurn: false);

        await DisposeComponentsAsync();

        await answered.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        await told.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        await pressing.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.Empty(_presses);
    }
}
