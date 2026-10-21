using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Work of the Consumer's own in the Input Order (ADR-0240 sections 6 and 12; ADR-0330 section 10):
/// <see cref="ExGrid{TRow}.InOrderAsync(Func{Task})"/> takes a place after every input received so far
/// and holds the inputs behind it, never claiming a number told for a control's event;
/// <see cref="ExGrid{TRow}.CancellableInOrderAsync"/> does the same, and while it is under way the root is
/// busy, names it (<c>data-ex-work</c>), owes the Settled Mark, and an Escape cancels it: the call the script
/// makes (<c>OnWorkCancelAsync</c>) is heard at once, and the Escape's own turn says, through the Consumer,
/// when it came too late. What the script reads and numbers is layer 3's; here the calls are made by hand.
/// </summary>
public class WorkUnderWayTests : GridTestContext
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

    private static AngleSharp.Dom.IElement Root(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid");

    private static bool Owing(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid > [data-ex-answered]").HasAttribute("data-ex-owing");

    private static int Work(IRenderedComponent<ExGrid<TestRow>> cut) => int.Parse(Root(cut).GetAttribute("data-ex-work")!, System.Globalization.CultureInfo.InvariantCulture);

    // Starts the work on the renderer's context, as a Consumer's handler calls it; it runs until `lands` completes.
    private static async Task<Task<bool>> StartAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, Task lands, List<string> said, bool looks = true)
    {
        Task<bool> work = Task.FromResult(false);
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(
                async token =>
                {
                    await (looks ? lands.WaitAsync(token) : lands);
                    said.Add("landed");
                },
                landed =>
                {
                    said.Add(landed ? "too late" : "nothing to cancel");
                    return Task.CompletedTask;
                });
        });
        return work;
    }

    private static async Task<Task> CancelAsync(IRenderedComponent<ExGrid<TestRow>> cut, int work, int input)
    {
        Task sent = Task.CompletedTask;
        await cut.InvokeAsync(() => { sent = cut.Instance.OnWorkCancelAsync(work, input); });
        return sent;
    }

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int input)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, input: input));

    [Fact] // ADR-0330 §10, ADR-0240 §6: the Consumer's work holds the inputs made after it until it ends
    public async Task ADR0330_the_consumers_work_holds_the_inputs_behind_it()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said);

        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = KeyAsync(cut, "ArrowDown", 1); });
        Assert.False(key.IsCompleted);

        lands.SetResult();
        Assert.True(await work);
        await key;
        Assert.Equal(["landed"], said);
    }

    [Fact] // ADR-0240 §12, ADR-0330 §10: from its start until it ends the root is busy, names the work and owes the Settled Mark
    public async Task ADR0330_cancellable_work_is_busy_from_its_start_until_it_ends()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        Assert.Null(Root(cut).GetAttribute("aria-busy"));

        var work = await StartAsync(cut, lands.Task, []);

        Assert.Equal("true", Root(cut).GetAttribute("aria-busy"));
        Assert.True(Work(cut) > 0);
        Assert.True(Owing(cut));

        lands.SetResult();
        await work;

        Assert.Null(Root(cut).GetAttribute("aria-busy"));
        Assert.Null(Root(cut).GetAttribute("data-ex-work"));
        Assert.False(Owing(cut));
    }

    [Fact] // ADR-0240 §12: an Escape while the work looks at its token cancels it; nothing lands, and the Escape's turn says nothing more
    public async Task ADR0330_an_escape_cancels_work_still_under_way()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said);

        var escape = await CancelAsync(cut, Work(cut), 1);

        Assert.False(await work);
        await escape;
        Assert.Empty(said);
        Assert.Null(Root(cut).GetAttribute("data-ex-work"));
    }

    [Fact] // ADR-0240 §12: work past the point a cancel could stop lands, and the Escape's own turn says it came too late
    public async Task ADR0330_an_escape_after_the_point_of_landing_is_said_too_late()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said, looks: false);

        var escape = await CancelAsync(cut, Work(cut), 1);
        lands.SetResult();

        Assert.True(await work);
        await escape;
        Assert.Equal(["landed", "too late"], said);
    }

    [Fact] // ADR-0240 §12: an Escape naming work that has already ended finds it landed, and says so in its turn
    public async Task ADR0330_an_escape_naming_work_that_ended_is_said_too_late()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said);
        var named = Work(cut);
        lands.SetResult();
        await work;

        await await CancelAsync(cut, named, 1);

        Assert.Equal(["landed", "too late"], said);
    }

    [Fact] // ADR-0240 §6: the Consumer's work never claims a number told for a control's event, which waits for its own handler
    public async Task ADR0330_the_consumers_work_claims_no_number_told()
    {
        var cut = RenderGrid();
        var ran = new List<string>();
        await cut.InvokeAsync(() => cut.Instance.InputTakenAt("click", 1));

        Task work = Task.CompletedTask;
        await cut.InvokeAsync(() => { work = cut.Instance.InOrderAsync(() => { ran.Add("work"); return Task.CompletedTask; }); });
        await work;
        Task click = Task.CompletedTask;
        await cut.InvokeAsync(() => { click = cut.Instance.InOrderAsync(GridInputKind.Click, () => { ran.Add("click"); return Task.CompletedTask; }); });
        await click;

        Assert.Equal(["work", "click"], ran);
        Assert.Equal("1", await ReadWhenIdleAsync(() => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered")));
    }

    // Holds the order with work of the Consumer's that cannot be cancelled, until `holds` completes.
    private static async Task<Task> HoldAsync(IRenderedComponent<ExGrid<TestRow>> cut, Task holds)
    {
        Task held = Task.CompletedTask;
        await cut.InvokeAsync(() => { held = cut.Instance.InOrderAsync(() => holds); });
        return held;
    }

    private static bool EscapeLeaves(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid > [data-ex-answered]").HasAttribute("data-ex-escape-leaves");

    // The work the stamp names as placed (data-ex-work-placed), which the script reads at an Escape's keydown; 0 for none.
    private static int Placed(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.TryParse(cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-work-placed"), System.Globalization.CultureInfo.InvariantCulture, out var placed) ? placed : 0;

    [Fact] // ADR-0240 §12, ADR-0330 §10: from the moment it takes its place the stamp names the work, though it still waits for its turn and the root names nothing, and an Escape typed against it cancels it
    public async Task ADR0330_an_escape_typed_against_the_stamp_that_names_the_work_cancels_it_while_it_waits()
    {
        var cut = RenderGrid();
        Assert.Equal(0, Placed(cut));
        var holds = new TaskCompletionSource();
        var held = await HoldAsync(cut, holds.Task);
        List<string> said = [];
        var work = await StartAsync(cut, new TaskCompletionSource().Task, said);
        Assert.Null(Root(cut).GetAttribute("data-ex-work"));
        var placed = Placed(cut);
        Assert.True(placed > 0);

        var escape = await CancelAsync(cut, placed, 1);
        holds.SetResult();
        await held;

        Assert.False(await work);
        await escape;
        Assert.Empty(said);
        Assert.Null(Root(cut).GetAttribute("aria-busy"));
        Assert.Equal(0, Placed(cut));
    }

    [Fact] // ADR-0240 §12, ADR-0330 §10: an Escape held at a keydown whose stamp named no work, and numbered when it is drained after the work took its place, is an ordinary key and cancels nothing
    public async Task ADR0330_a_held_escape_typed_before_the_stamp_showed_the_work_does_not_cancel_it()
    {
        var cut = RenderGrid();
        // The stamp the Escape was typed against named no work: it goes as a key, not as a cancel.
        Assert.Equal(0, Placed(cut));
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said);
        var releases = Js.TabReleases;

        // Drained and numbered once the work has taken its place.
        Task escape = Task.CompletedTask;
        await cut.InvokeAsync(() => { escape = KeyAsync(cut, "Escape", 1); });
        lands.SetResult();

        Assert.True(await work);
        await escape;
        Assert.Equal(["landed"], said);
        // It did what it was typed to do: an Escape on a grid with nothing to peel leaves it, releasing Tab.
        Assert.Equal(releases + 1, Js.TabReleases);
    }

    [Theory] // ADR-0240 §12, ADR-0330 §10: a second Escape after a cancel gets the same answer whether it arrives before the work has ended or after
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ADR0330_a_second_escape_after_a_cancel_gets_the_same_answer_before_or_after_the_work_ends(bool beforeTheEnd, bool landsRegardless)
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, lands.Task, said, looks: !landsRegardless);
        var placed = Placed(cut);

        var first = await CancelAsync(cut, placed, 1);
        Task second = Task.CompletedTask;
        if (beforeTheEnd)
            second = await CancelAsync(cut, placed, 2);
        lands.SetResult();
        Assert.Equal(landsRegardless, await work);
        await first;
        if (!beforeTheEnd)
            second = await CancelAsync(cut, placed, 2);
        await second;

        Assert.Equal(landsRegardless ? ["landed", "too late", "too late"] : ["nothing to cancel"], said);
    }

    [Fact] // ADR-0240 §12: an Escape on the key path is an ordinary key, whatever work was placed by its arrival; one with a modifier or a repeat is one too
    public async Task ADR0330_an_escape_on_the_key_path_cancels_nothing()
    {
        var cut = RenderGrid();
        var lands = new TaskCompletionSource();
        Task second = Task.CompletedTask;
        await cut.InvokeAsync(() => { second = KeyAsync(cut, "ArrowDown", 2); });
        var work = await StartAsync(cut, lands.Task, []);

        Task first = Task.CompletedTask, shifted = Task.CompletedTask, repeated = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            first = cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, input: 1);
            shifted = cut.Instance.OnKeyAsync("Escape", false, true, false, false, false, input: 3);
            repeated = cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, repeat: true, input: 4);
        });
        lands.SetResult();

        Assert.True(await work);
        await Task.WhenAll(first, second, shifted, repeated);
    }

    [Fact] // ADR-0230 §1, ADR-0240 §12: while cancellable work has taken its place the stamp no longer says that an Escape leaves the grid
    public async Task ADR0330_an_escape_does_not_leave_while_work_can_be_cancelled()
    {
        var cut = RenderGrid();
        Assert.True(EscapeLeaves(cut));
        var holds = new TaskCompletionSource();
        var held = await HoldAsync(cut, holds.Task);
        var lands = new TaskCompletionSource();
        var work = await StartAsync(cut, lands.Task, []);

        Assert.False(EscapeLeaves(cut));
        Assert.True(Owing(cut));

        holds.SetResult();
        lands.SetResult();
        await held;
        await work;
        Assert.True(EscapeLeaves(cut));
    }

    [Fact] // ADR-0240 §12: an Escape naming work that failed finds nothing to cancel, and its turn says so rather than nothing
    public async Task ADR0330_an_escape_naming_work_that_failed_says_nothing_to_cancel()
    {
        var cut = RenderGrid();
        var fails = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, fails.Task, said, looks: false);
        var named = Work(cut);
        fails.SetException(new InvalidOperationException("refused"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => work);

        await await CancelAsync(cut, named, 1);

        Assert.Equal(["nothing to cancel"], said);
    }

    [Fact] // ADR-0240 §12: an Escape that cancelled work which then failed otherwise found nothing to cancel, said in its turn
    public async Task ADR0330_a_cancel_of_work_that_failed_otherwise_says_nothing_to_cancel()
    {
        var cut = RenderGrid();
        var fails = new TaskCompletionSource();
        List<string> said = [];
        var work = await StartAsync(cut, fails.Task, said, looks: false);

        var escape = await CancelAsync(cut, Work(cut), 1);
        fails.SetException(new InvalidOperationException("refused"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => work);
        await escape;
        Assert.Equal(["nothing to cancel"], said);
    }

    [Fact] // ADR-0330 §10: disposing of the grid cancels the work under way, which ends having done nothing
    public async Task ADR0330_disposing_of_the_grid_cancels_the_work_under_way()
    {
        var cut = RenderGrid();
        List<CancellationToken> tokens = [];
        Task<bool> work = Task.FromResult(true);
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(async token =>
            {
                tokens.Add(token);
                await Task.Delay(Timeout.Infinite, token);
            }, _ => Task.CompletedTask);
        });

        await cut.Instance.DisposeAsync();

        Assert.False(await work);
        Assert.True(tokens.Single().IsCancellationRequested);
    }

    [Theory] // ADR-0330 §10: disposing of the grid on the renderer's context cancels the work under way, whose token ends it at once — inline, within the cancel — and the work waiting behind it, which never runs
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0330_disposing_of_the_grid_on_the_renderer_ends_every_work_placed(bool endsInline)
    {
        var cut = RenderGrid();
        var ran = new List<string>();
        Task<bool> running = Task.FromResult(true), waiting = Task.FromResult(true);
        await cut.InvokeAsync(() =>
        {
            running = cut.Instance.CancellableInOrderAsync(async token =>
            {
                ran.Add("running");
                if (!endsInline)
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return;
                }
                // Ended by the token's callback itself: the work's end runs within the cancel, on the renderer.
                var cancelled = new TaskCompletionSource();
                using var registration = token.Register(() => cancelled.TrySetCanceled(token));
                await cancelled.Task;
            }, _ => Task.CompletedTask);
            waiting = cut.Instance.CancellableInOrderAsync(_ =>
            {
                ran.Add("waiting");
                return Task.CompletedTask;
            }, _ => Task.CompletedTask);
        });

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.False(await running);
        Assert.False(await waiting);
        Assert.Equal(["running"], ran);
    }

    [Fact] // ADR-0240 §12: one cancellable work cannot run inside another, which one Escape could not tell apart
    public async Task ADR0330_cancellable_work_inside_cancellable_work_is_refused()
    {
        var cut = RenderGrid();
        Exception? refused = null;
        Task<bool> work = Task.FromResult(false);
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(async _ =>
            {
                refused = await Record.ExceptionAsync(() => cut.Instance.CancellableInOrderAsync(_ => Task.CompletedTask, _ => Task.CompletedTask));
            }, _ => Task.CompletedTask);
        });

        Assert.True(await work);
        Assert.IsType<InvalidOperationException>(refused);
    }

    [Fact] // ADR-0240 §6: work asked for from another thread is taken on the renderer's synchronisation context
    public async Task ADR0330_work_asked_for_from_another_thread_runs_on_the_renderer()
    {
        var cut = RenderGrid();
        var onRenderer = new List<bool>();

        await Task.Run(() => cut.Instance.InOrderAsync(() =>
        {
            onRenderer.Add(Renderer.Dispatcher.CheckAccess());
            return Task.CompletedTask;
        }), Xunit.TestContext.Current.CancellationToken);
        var ran = await Task.Run(() => cut.Instance.CancellableInOrderAsync(_ =>
        {
            onRenderer.Add(Renderer.Dispatcher.CheckAccess());
            return Task.CompletedTask;
        }, _ => Task.CompletedTask), Xunit.TestContext.Current.CancellationToken);

        Assert.True(ran);
        Assert.Equal([true, true], onRenderer);
    }
}
