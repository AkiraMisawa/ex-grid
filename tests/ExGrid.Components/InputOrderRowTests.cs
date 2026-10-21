using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using ExGrid.Components.Tests.Support;
using ExGrid.Keys;
using ExGrid.Rows;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>ADR-0240: numbered input takes effect in browser order, including asynchronous Consumers.</summary>
public class InputOrderRowTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Func<GridDeclaredKeyPress, Task> handler)
        => Render<ExGrid<TestRow>>(parameters => parameters
            .Add(grid => grid.Window, TestRows.Many(10))
            .Add(grid => grid.TotalCount, 10)
            .Add(grid => grid.RowHeight, 20d)
            .Add(grid => grid.ViewportHeight, 120)
            .Add(grid => grid.ViewportWidth, 200)
            .Add(grid => grid.Columns, new GridColumn<TestRow>[]
            {
                new("Book", ColumnType.Text, row => row.Book, width: new(ColumnWidth.Fixed(100))),
            })
            .Add(grid => grid.DeclaredKeys, new[] { "Control+b", "Control+i" })
            .Add(grid => grid.OnDeclaredKey, handler));

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> grid, string key, int number)
        => grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, true, false, false, false, false, input: number));

    private static Task BeginClickAsync(IRenderedComponent<ExGrid<TestRow>> grid, bool mark, int paint, int input)
        => grid.InvokeAsync(() =>
        {
            grid.Instance.RememberRowClick(input, paint, 0, 0, 0, mark, false);
            grid.Instance.BeginRowClick(input, input);
        });

    [Fact]
    public async Task ADR0240_a_row_action_waits_until_the_previous_consumer_key_finishes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<string>();
        var grid = RenderGrid(async _ =>
        {
            seen.Add("first:start");
            await release.Task;
            seen.Add("first:end");
        });
        await grid.InvokeAsync(() => grid.Render(parameters => parameters
            .Add(component => component.Columns, new GridColumn<TestRow>[]
            {
                GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")],
                    width: new(ColumnWidth.Fixed(100))),
            })
            .Add(component => component.OnAction, (GridActionEventArgs<TestRow> _) => seen.Add("action"))));

        var first = KeyAsync(grid, "b", 1);
        await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 2));
        var click = grid.FindAll(".ex-row .ex-action")[0].ClickAsync(new MouseEventArgs { Type = "click", Button = 0 });
        try
        {
            await AssertWhenIdleAsync(() => Assert.Equal(["first:start"], seen));
        }
        finally
        {
            release.SetResult();
            await Task.WhenAll(first, click);
        }

        await AssertWhenIdleAsync(() => Assert.Equal(["first:start", "first:end", "action"], seen));
    }

    private sealed class RecordingMarks(Action mark) : IRowMarks<TestRow>
    {
        public RowMarkCounts? Counts => null;
        public bool IsMarked(TestRow row) => false;
        public event Action? Changed { add { } remove { } }
        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind) { }
        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent)
        {
            mark();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ADR0240_a_row_mark_waits_until_the_previous_consumer_key_finishes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<string>();
        var grid = RenderGrid(async _ =>
        {
            seen.Add("first:start");
            await release.Task;
            seen.Add("first:end");
        });
        await grid.InvokeAsync(() => grid.Render(parameters => parameters
            .Add(component => component.Columns, new GridColumn<TestRow>[]
            {
                GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100))),
            })
            .Add(component => component.Marks, new RecordingMarks(() => seen.Add("mark")))));

        var first = KeyAsync(grid, "b", 1);
        await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 2));
        var click = grid.FindAll(".ex-row .ex-mark")[0].ClickAsync(new MouseEventArgs { Type = "click", Button = 0 });
        try
        {
            await AssertWhenIdleAsync(() => Assert.Equal(["first:start"], seen));
        }
        finally
        {
            release.SetResult();
            await Task.WhenAll(first, click);
        }

        await AssertWhenIdleAsync(() => Assert.Equal(["first:start", "first:end", "mark"], seen));
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ADR0160_a_waiting_row_command_is_refused_after_its_row_leaves_even_if_it_returns(bool mark, bool keyed)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<string>();
        var rows = TestRows.Many(10);
        var grid = RenderGrid(async _ => await release.Task);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.Window, rows)
            .Add(g => g.RowKey, keyed ? r => r.Book : null)
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RecordingMarks(() => seen.Add("marked")))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => seen.Add("acted"))
            .Add(g => g.OnActionRefused, (GridActionRefusal<TestRow> refusal) => seen.Add(refusal.Reason.ToString()))
            .Add(g => g.OnMarkRefused, (MarkRefusalReason reason) => seen.Add(reason.ToString()))));
        var first = KeyAsync(grid, "b", 1);
        try
        {
            await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 2));
            await grid.Find(mark ? ".ex-row .ex-mark" : ".ex-row .ex-action").ClickAsync(new MouseEventArgs());
            await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, rows[1..])));
            await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, rows)));
            await AssertWhenIdleAsync(() => Assert.Empty(seen));
        }
        finally { release.SetResult(); await first; }
        await AssertWhenIdleAsync(() => Assert.Equal(["RowLeftTheWindow"], seen));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0160_waiting_commands_follow_a_key_through_reorder_and_value_replacement(bool mark)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TestRow? acted = null;
        var marked = new List<string>();
        var grid = RenderGrid(async _ => await release.Task);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.RowKey, r => r.Book)
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RowRecordingMarks(r => { acted = r; marked.Add("marked"); }))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) => acted = a.Row)));
        var first = KeyAsync(grid, "b", 1);
        var rows = TestRows.Many(10);
        var expected = rows[0];
        expected.Amount = 999;
        Array.Reverse(rows);
        try
        {
            await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 2));
            await grid.Find(mark ? ".ex-row .ex-mark" : ".ex-row .ex-action").ClickAsync(new MouseEventArgs());
            await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, rows).Add(g => g.RowSequenceVersion, 1)));
            await AssertWhenIdleAsync(() => Assert.Null(acted));
        }
        finally { release.SetResult(); await first; }
        await AssertWhenIdleAsync(() => Assert.Same(expected, acted));
    }

    private sealed class RowRecordingMarks(Action<TestRow> mark) : IRowMarks<TestRow>
    {
        public RowMarkCounts? Counts => null;
        public bool IsMarked(TestRow row) => false;
        public event Action? Changed { add { } remove { } }
        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind) { }
        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent)
        {
            mark(Assert.IsType<RowMarkIntent<TestRow>.OneRow>(intent).Row);
            return Task.CompletedTask;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> QueueTwoRowsAsync(IRenderedComponent<ExGrid<TestRow>> grid, bool mark)
    {
        var rows = TestRows.Many(10);
        await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, rows)));
        for (var i = 0; i < 2; i++)
        {
            await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", i + 2));
            await grid.FindAll(mark ? ".ex-row .ex-mark" : ".ex-row .ex-action")[i].ClickAsync(new MouseEventArgs());
        }
        return rows.Select(row => new WeakReference(row)).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0160_waiting_row_callbacks_do_not_keep_a_previous_Window_alive(bool mark)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Begin empty: bUnit's original parameter declaration must hold no historical rows.
        var grid = Render<ExGrid<TestRow>>(p => p
            .Add(g => g.Window, Array.Empty<TestRow>()).Add(g => g.TotalCount, 10)
            .Add(g => g.ViewportWidth, 200).Add(g => g.ViewportHeight, 120).Add(g => g.RowHeight, 20d)
            .Add(g => g.DeclaredKeys, new[] { "Control+b" })
            .Add(g => g.OnDeclaredKey, async _ => await release.Task)
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RecordingMarks(() => { })));
        var first = KeyAsync(grid, "b", 1);
        try
        {
            var references = await QueueTwoRowsAsync(grid, mark);
            for (var i = 0; i < 3; i++)
                await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, TestRows.Many(10))));
            await WhenIdleAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.DoesNotContain(references, reference => reference.IsAlive);
        }
        finally { release.SetResult(); await first; await WhenIdleAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0240_a_row_press_recovered_after_its_button_disappears_waits_for_prior_input(bool mark)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<string>();
        var grid = RenderGrid(async _ => await release.Task);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RecordingMarks(() => seen.Add("marked")))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => seen.Add("acted"))
            .Add(g => g.OnActionRefused, (GridActionRefusal<TestRow> refusal) => seen.Add(refusal.Reason.ToString()))
            .Add(g => g.OnMarkRefused, (MarkRefusalReason reason) => seen.Add(reason.ToString()))));
        var first = KeyAsync(grid, "b", 1);
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        try
        {
            await BeginClickAsync(grid, mark, paint, 2);
            await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, Array.Empty<TestRow>())));
            await grid.InvokeAsync(() => grid.Instance.EndRowClickDispatch(2));
            await AssertWhenIdleAsync(() => Assert.Empty(seen));
        }
        finally { release.SetResult(); await first; }
        await KeyAsync(grid, "i", 3);
        await AssertWhenIdleAsync(() =>
        {
            Assert.Equal(["RowLeftTheWindow"], seen);
            Assert.Equal("3", grid.Find("[data-ex-answered]").GetAttribute("data-ex-answered"));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0142_a_numbered_click_still_names_a_keyed_row_moved_before_the_telling(bool mark)
    {
        TestRow? acted = null;
        var rows = TestRows.Many(10);
        var expected = rows[0];
        var grid = RenderGrid(_ => Task.CompletedTask);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.Window, rows).Add(g => g.RowKey, r => r.Book)
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RowRecordingMarks(r => acted = r))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) => acted = a.Row)));
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        var moved = rows.ToArray();
        (moved[0], moved[1]) = (moved[1], moved[0]);
        await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, moved).Add(g => g.RowSequenceVersion, 1)));
        await BeginClickAsync(grid, mark, paint, 1);
        await grid.FindAll(mark ? ".ex-row .ex-mark" : ".ex-row .ex-action")[1].ClickAsync(new MouseEventArgs());
        await AssertWhenIdleAsync(() => Assert.Same(expected, acted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0240_a_row_click_voids_an_earlier_click_whose_handler_never_came(bool mark)
    {
        var calls = 0;
        var grid = RenderGrid(_ => Task.CompletedTask);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RecordingMarks(() => calls++))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => calls++)));
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 1));
        await BeginClickAsync(grid, mark, paint, 2);
        await grid.Find(mark ? ".ex-row .ex-mark" : ".ex-row .ex-action").ClickAsync(new MouseEventArgs());
        await AssertWhenIdleAsync(() => Assert.Equal(1, calls));
    }

    private async Task<IRenderedComponent<ExGrid<TestRow>>> RenderBothCommandsAsync(List<string> seen)
    {
        var grid = RenderGrid(_ => Task.CompletedTask);
        await grid.InvokeAsync(() => grid.Render(p => p
            .Add(g => g.Columns, new[]
            {
                GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(80))),
                GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(80))),
            })
            .Add(g => g.Marks, new RecordingMarks(() => seen.Add("mark")))
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => seen.Add("action"))));
        return grid;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ADR0240_a_later_telling_abandons_an_unheard_row_click_without_stalling(bool mark, bool nextIsRowTicket)
    {
        var seen = new List<string>();
        var grid = await RenderBothCommandsAsync(seen);
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        await BeginClickAsync(grid, mark, paint, 1);
        if (nextIsRowTicket) await BeginClickAsync(grid, !mark, paint, 2);
        else await grid.InvokeAsync(() => grid.Instance.InputTakenAt("click", 2));
        await grid.Find(mark ? ".ex-row .ex-action" : ".ex-row .ex-mark").ClickAsync(new MouseEventArgs());
        await grid.InvokeAsync(() => grid.Instance.EndRowClickDispatch(1));
        if (nextIsRowTicket) await grid.InvokeAsync(() => grid.Instance.EndRowClickDispatch(2));
        await KeyAsync(grid, "i", 3);
        await AssertWhenIdleAsync(() => Assert.Equal([mark ? "action" : "mark"], seen));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0240_dispatch_completion_recovers_a_button_removed_while_its_row_survives(bool mark)
    {
        var seen = new List<string>();
        var grid = await RenderBothCommandsAsync(seen);
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        await BeginClickAsync(grid, mark, paint, 1);
        await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Columns, mark
            ? new[]
            {
                GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(80))),
                GridColumn<TestRow>.MarkColumn("RemainingMark", width: new(ColumnWidth.Fixed(80))),
            }
            : new[] { GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(80))) })));
        await grid.InvokeAsync(() => grid.Instance.EndRowClickDispatch(1));
        await grid.InvokeAsync(() => grid.Instance.EndRowClickDispatch(1));
        await KeyAsync(grid, "i", 2);
        await AssertWhenIdleAsync(() => Assert.Equal([mark ? "mark" : "action"], seen));
    }

    private sealed class RowHoldingKey(TestRow row)
    {
        public TestRow Row { get; } = row;
        public override bool Equals(object? other) => other is RowHoldingKey key && Row.Book == key.Row.Book;
        public override int GetHashCode() => Row.Book.GetHashCode(StringComparison.Ordinal);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> RememberTwoRowsAsync(IRenderedComponent<ExGrid<TestRow>> grid, bool mark)
    {
        var rows = TestRows.Many(10);
        await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, rows)));
        var paint = int.Parse(grid.Find(".ex-viewport").GetAttribute("data-ex-paint")!);
        for (var i = 0; i < 2; i++)
            await grid.InvokeAsync(() => grid.Instance.RememberRowClick(i + 1, paint, i, 0, 0, mark, false));
        return rows.Select(row => new WeakReference(row)).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0160_browser_held_targets_refresh_keys_without_retaining_old_rows(bool mark)
    {
        var grid = Render<ExGrid<TestRow>>(p => p
            .Add(g => g.Window, Array.Empty<TestRow>()).Add(g => g.TotalCount, 10)
            .Add(g => g.ViewportWidth, 200).Add(g => g.ViewportHeight, 120).Add(g => g.RowHeight, 20d)
            .Add(g => g.RowKey, row => new RowHoldingKey(row))
            .Add(g => g.Columns, new[] { mark
                ? GridColumn<TestRow>.MarkColumn("Mark", width: new(ColumnWidth.Fixed(100)))
                : GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: new(ColumnWidth.Fixed(100))) })
            .Add(g => g.Marks, new RecordingMarks(() => { })));
        var references = await RememberTwoRowsAsync(grid, mark);
        for (var i = 0; i < 3; i++)
            await grid.InvokeAsync(() => grid.Render(p => p.Add(g => g.Window, TestRows.Many(10))));
        await WhenIdleAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.DoesNotContain(references, reference => reference.IsAlive);
        await grid.InvokeAsync(() => { grid.Instance.CancelRowClick(1); grid.Instance.CancelRowClick(2); });
    }

}
