using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Find (ADR-0047, FD-1..FD-8): the grid asks and moves the Focus; the Consumer searches.
/// 20px rows in a 120px Viewport, three 100px columns: Book (editable), Amount, AsOf.
/// </summary>
public class FindTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private sealed class Heard
    {
        public List<GridFindRequest> Requests { get; } = [];
        public List<FindRefusalReason> Refusals { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public GridSelection? Selection;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Heard heard,
        Func<GridFindRequest, CancellationToken, Task<GridFindResult>>? onFind,
        int rows = 500)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(rows))
              .Add(g => g.TotalCount, rows)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnFindRefused, r => heard.Refusals.Add(r))
              .Add(g => g.SelectionChanged, s => heard.Selection = s);
            if (onFind is not null)
            {
                ps.Add(g => g.OnFind, (request, token) =>
                {
                    heard.Requests.Add(request);
                    heard.Tokens.Add(token);
                    return onFind(request, token);
                });
            }
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static async Task SearchAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text, bool backward = false)
    {
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });
        await cut.Find(backward ? "button.ex-find-previous" : "button.ex-find-next").ClickAsync(new MouseEventArgs());
    }

    private static Func<GridFindRequest, CancellationToken, Task<GridFindResult>> Answer(int row, string column)
        => (request, _) => Task.FromResult(GridFindResult.Found(row, column));

    [Fact] // ADR-0047 / FD-2: nothing can search — Ctrl+F opens nothing and says so
    public async Task Ctrl_f_with_nothing_to_search_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, onFind: null);

        await PressAsync(cut, "f", ctrl: true);

        Assert.Equal([FindRefusalReason.Unavailable], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0047 / FD-1: Ctrl+F is claimed on every grid, wired or not
    public void The_gate_is_told_to_take_ctrl_f_on_every_grid()
    {
        RenderGrid(new Heard(), onFind: null);

        Assert.Contains("Control+f", Js.TakenAtAttach);
        Assert.Contains("Control+F", Js.TakenAtAttach);
    }

    [Fact] // ADR-0047 / FD-3: with a search wired, Ctrl+F opens the panel, which takes the keyboard
    public async Task Ctrl_f_opens_the_find_panel()
    {
        var cut = RenderGrid(new Heard(), Answer(0, "Book"));

        await PressAsync(cut, "f", ctrl: true);

        var panel = cut.Find(".ex-popover-find");
        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Single(cut.FindAll("input.ex-find-field"));
    }

    [Fact] // ADR-0047 / FD-4: a step asks with every field
    public async Task A_step_asks_with_the_focus_the_columns_and_the_version()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, Answer(0, "Book"));
        await ClickCellAsync(cut, 150, 30); // (1, 1)
        await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-match-case").ChangeAsync(new ChangeEventArgs { Value = true });

        await SearchAsync(cut, "Row", backward: true);

        var request = Assert.Single(heard.Requests);
        Assert.Equal("Row", request.Text);
        Assert.True(request.MatchCase);
        Assert.False(request.WholeCell);
        Assert.True(request.Backward);
        Assert.Equal(new CellPosition(1, 1), request.From);
        Assert.Null(request.Scope);
        Assert.Equal(["Book", "Amount", "AsOf"], request.Columns);
    }

    [Fact] // ADR-0047 / FD-5: a found cell becomes the Focus, and the selection collapses onto it
    public async Task A_found_cell_becomes_the_focus_and_is_revealed()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, Answer(300, "Amount"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "f", ctrl: true);

        await SearchAsync(cut, "300");

        Assert.Equal(new CellPosition(300, 1), heard.Selection!.Focus);
        Assert.Equal(1, heard.Selection.CellCount);
        // Revealed: the grid scrolled towards row 300.
        Assert.Contains(Js.ScrolledTo, offset => offset.Top > 5000);
        // The panel stands, for the next step.
        Assert.Single(cut.FindAll(".ex-popover-find"));
    }

    [Fact] // ADR-0047 / FD-4/FD-5: with more than one cell selected the step searches the selection, which stands
    public async Task A_step_over_a_range_searches_it_and_keeps_it()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, Answer(2, "Amount"));
        await ClickCellAsync(cut, 50, 10);
        await ClickCellAsync(cut, 150, 70, shift: true); // (0,0)–(3,1)
        await PressAsync(cut, "f", ctrl: true);

        await SearchAsync(cut, "2");

        Assert.Equal([new SelectionRange(0, 0, 4, 2)], heard.Requests[0].Scope);
        Assert.Equal(new CellPosition(2, 1), heard.Selection!.Focus);
        Assert.Equal(8, heard.Selection.CellCount);
    }

    [Fact] // ADR-0047 / FD-6: not found is refused, and the panel says so
    public async Task Not_found_is_refused_and_shown()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(GridFindResult.NotFound));
        await PressAsync(cut, "f", ctrl: true);

        await SearchAsync(cut, "zzz");

        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Equal("No match", cut.Find(".ex-find-outcome").TextContent);
        Assert.Equal("status", cut.Find(".ex-find-outcome").GetAttribute("role"));
    }

    [Fact] // ADR-0047 / ADR-0011 / FD-6: an answer under a stale version moves nothing
    public async Task An_answer_under_a_stale_version_moves_nothing()
    {
        var heard = new Heard();
        var answer = new TaskCompletionSource<GridFindResult>();
        var cut = RenderGrid(heard, (_, _) => answer.Task);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "Row" });
        // Not awaited: the step waits on an answer this test gives below.
        var step = cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());

        // The order moves while the question is out.
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        await cut.InvokeAsync(() => answer.SetResult(GridFindResult.Found(5, "Book")));
        await step;

        Assert.Equal([FindRefusalReason.OrderChanged], heard.Refusals);
        Assert.NotEqual(new CellPosition(5, 0), heard.Selection?.IsEmpty == false ? heard.Selection.Focus : default);
    }

    [Fact] // ADR-0047 / FD-6: a step asked while one is out cancels it, and its answer is discarded
    public async Task A_new_step_cancels_the_one_that_is_out()
    {
        var heard = new Heard();
        var first = new TaskCompletionSource<GridFindResult>();
        var calls = 0;
        var cut = RenderGrid(heard, (_, _) => ++calls == 1 ? first.Task : Task.FromResult(GridFindResult.Found(4, "Book")));
        await PressAsync(cut, "f", ctrl: true);

        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "Row" });
        // Not awaited: the first step waits on an answer this test gives last.
        var firstStep = cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());
        await cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());

        Assert.True(heard.Tokens[0].IsCancellationRequested);
        Assert.Equal(new CellPosition(4, 0), heard.Selection!.Focus);
        await cut.InvokeAsync(() => first.SetResult(GridFindResult.Found(9, "Book")));
        await firstStep;
        Assert.Equal(new CellPosition(4, 0), heard.Selection!.Focus);
    }

    [Fact] // ADR-0047 / FD-8: Escape closes the panel, and the text survives a reopen
    public async Task Escape_closes_and_the_text_survives()
    {
        var cut = RenderGrid(new Heard(), Answer(0, "Book"));
        await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "kept" });

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));
        Assert.Empty(cut.FindAll(".ex-popover-find"));

        await PressAsync(cut, "f", ctrl: true);
        Assert.Equal("kept", cut.Find("input.ex-find-field").GetAttribute("value"));
    }

    [Fact] // ADR-0047 / FD-3: a pointer-down elsewhere in the instance closes the panel
    public async Task A_press_on_the_rows_closes_the_panel()
    {
        var cut = RenderGrid(new Heard(), Answer(0, "Book"));
        await PressAsync(cut, "f", ctrl: true);

        await ClickCellAsync(cut, 50, 30);

        Assert.Empty(cut.FindAll(".ex-popover-find"));
    }

    [Fact] // ADR-0047 / FD-1: in the Cell Editor Ctrl+F is taken and does nothing
    public async Task Ctrl_f_in_the_editor_does_nothing()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, Answer(0, "Book"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "f", ctrl: true);

        Assert.Empty(cut.FindAll(".ex-popover-find"));
        Assert.Equal("5", cut.Find(".ex-editor").GetAttribute("value"));
        Assert.Empty(heard.Refusals);
    }

    [Fact] // ADR-0047 / FD-5: a bound Source that finds answers without OnFind
    public async Task A_bound_in_memory_source_answers()
    {
        var heard = new Heard();
        var source = GridSource.From(TestRows.Many(200));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, s => heard.Selection = s));
        await PressAsync(cut, "f", ctrl: true);

        await SearchAsync(cut, "Row 000150");

        Assert.Equal(new CellPosition(150, 0), heard.Selection!.Focus);
    }
}
