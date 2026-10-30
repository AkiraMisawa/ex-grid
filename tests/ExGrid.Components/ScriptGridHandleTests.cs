using System.Text.Json;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using ExGrid.Keys;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0441: what the Consumer calls on Blazor's ExGrid over the Script Core crosses to the browser, which holds the
/// view, the open edit and the Input Order. What the browser does with each is the core's and the bridge's, tested
/// there; here, what .NET sends, in what order, and what it makes of the answers.
/// </summary>
public class ScriptGridHandleTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _grid;
    private readonly TestRow[] _rows = TestRows.Window();

    public ScriptGridHandleTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
        _grid.SetupVoid("showView", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(
        Action<ComponentParameterCollectionBuilder<global::ExGrid.Script.ExGrid<TestRow>>>? extra = null)
        => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Columns, TestRows.Columns());
            if (extra is null)
                ps.Add(g => g.Rows, _rows);
            else
                extra(ps);
        });

    private global::ExGrid.Script.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement Parsed(object? json) => Json(Assert.IsType<string>(json));

    private const string FocusOnRowTwo = """{"ranges":[{"topRow":2,"leftColumn":0,"rowCount":1,"columnCount":1}],"focus":{"row":2,"column":0},"focusRangeIndex":0}""";

    private void ReadsView(int number) => _grid.Setup<JsonElement>("readView", _ => true).SetResult(Json(
        $$"""{"number":{{number}},"rowSequenceVersion":4,"selection":{{FocusOnRowTwo}},"scrollTopPx":480,"scrollLeftPx":12}"""));

    [Fact] // A11Y-20's note of 2026-10-30: the element the script mounts on is Busy and says it is mounting until the script attaches, and a refused script's says neither
    public void A11Y20_the_mount_is_busy_until_the_script_attaches_and_a_refused_one_is_not()
    {
        var stamp = _module.Setup<string?>("stamp");
        var cut = Grid();

        var mount = cut.Find("[data-ex-mounting='grid']");
        Assert.Equal("true", mount.GetAttribute("aria-busy"));

        stamp.SetResult(new string('0', 64));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-ex-notice='script']")));
        Assert.Empty(cut.FindAll("[data-ex-mounting]"));
        Assert.Empty(cut.FindAll("[aria-busy]"));
    }

    [Fact] // ADR-0331 / ADR-0441 section 1: a view is read from the browser, and handed back by the number it keeps it under, ahead of the state that brings its rows
    public async Task ADR0441_a_view_read_crosses_back_by_its_number_ahead_of_the_rows()
    {
        var source = new TestSource();
        source.Push(_rows, totalCount: _rows.Length, rowSequenceVersion: 4);
        var cut = Grid(ps => ps.Add(g => g.Source, source));
        ReadsView(7);

        var view = await cut.InvokeAsync(() => cut.Instance.ReadViewAsync());
        Assert.Equal((4, 2, 480d, 12d), (view.RowSequenceVersion, view.Selection.Focus.Row, view.ScrollTopPx, view.ScrollLeftPx));

        var statesBefore = 0;
        await cut.InvokeAsync(() =>
        {
            statesBefore = StatesSent();
            source.Push(_rows, totalCount: _rows.Length, rowSequenceVersion: 5);
            cut.Instance.ShowViewWith(view with { RowSequenceVersion = 5 });
            // The view crossed as the call was made, and the state the source raised just before it has not.
            Assert.Equal(statesBefore, StatesSent());
        });

        var shown = Parsed(Assert.Single(_grid.Invocations["showView"]).Arguments[0]);
        Assert.Equal((7, 5, JsonValueKind.Null), (shown.GetProperty("number").GetInt32(), shown.GetProperty("rowSequenceVersion").GetInt32(), shown.GetProperty("place").ValueKind));
        // The state follows the view, once its send comes round.
        cut.WaitForAssertion(() => Assert.Equal(statesBefore + 1, StatesSent()));
    }

    private int StatesSent() => _grid.Invocations.Count(invocation => invocation.Identifier == "state");

    [Fact] // ADR-0441 section 1: a view not read here is rows never shown, with one range or none at offsets of zero; a place replaces any view's Selection; a view read here comes back as read
    public async Task ADR0441_a_view_not_read_here_is_rows_never_shown_and_a_place_replaces_the_selection()
    {
        var cut = Grid();
        ReadsView(3);
        var read = await cut.InvokeAsync(() => cut.Instance.ReadViewAsync());
        var several = read with { RowSet = null, ScrollTopPx = 0, ScrollLeftPx = 0, Selection = GridSelection.Reported(
            [new SelectionRange(0, 0, 1, 1), new SelectionRange(2, 0, 1, 1)], new CellPosition(2, 0), 1) };

        await cut.InvokeAsync(() =>
        {
            cut.Instance.ShowViewWith(new GridView(9, GridSelection.Empty, 0, 0));
            cut.Instance.ShowViewWith(read with { RowSet = null, ScrollTopPx = 0, ScrollLeftPx = 0 });
            cut.Instance.ShowViewWith(read with { Selection = GridSelection.Empty }, new SelectionRange(5, 1, 2, 1), new CellPosition(6, 1));
            cut.Instance.ShowViewWith(several, new SelectionRange(0, 0, 1, 1), new CellPosition(0, 0));
        });
        Assert.Throws<ArgumentException>(() => cut.Instance.ShowViewWith(several));
        Assert.Throws<ArgumentException>(() => cut.Instance.ShowViewWith(read with { RowSet = null }));
        // The browser keeps the view as it read it: one handed back changed is refused, not shown as read.
        Assert.Throws<ArgumentException>(() => cut.Instance.ShowViewWith(read with { Selection = GridSelection.Empty }));
        Assert.Throws<ArgumentException>(() => cut.Instance.ShowViewWith(read with { ScrollTopPx = 0 }, new SelectionRange(0, 0, 1, 1), new CellPosition(0, 0)));

        var shown = _grid.Invocations["showView"].Select(invocation => Parsed(invocation.Arguments[0])).ToList();
        Assert.Equal(4, shown.Count);
        Assert.Equal(JsonValueKind.Null, shown[3].GetProperty("number").ValueKind);
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (shown[0].GetProperty("number").ValueKind, shown[0].GetProperty("place").ValueKind));
        Assert.Equal(JsonValueKind.Null, shown[1].GetProperty("number").ValueKind);
        Assert.Equal(2, shown[1].GetProperty("place").GetProperty("focus").GetProperty("row").GetInt32());
        Assert.Equal(3, shown[2].GetProperty("number").GetInt32());
        Assert.Equal((5, 2, 6), (shown[2].GetProperty("place").GetProperty("range").GetProperty("topRow").GetInt32(),
            shown[2].GetProperty("place").GetProperty("range").GetProperty("rowCount").GetInt32(),
            shown[2].GetProperty("place").GetProperty("focus").GetProperty("row").GetInt32()));
    }

    [Fact] // ADR-0441 section 3: work placed from outside the grid's events runs when the browser reaches its turn, and work placed from inside it runs there
    public async Task ADR0441_work_runs_in_the_turn_the_browser_reaches_and_nested_work_runs_inside_it()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("inOrder", _ => true);
        var ran = new List<string>();
        Task? nested = null;

        Task? outer = null;
        await cut.InvokeAsync(() =>
        {
            outer = cut.Instance.InOrderAsync(() =>
            {
                ran.Add("outer");
                nested = cut.Instance.InOrderAsync(() => { ran.Add("inner"); return Task.CompletedTask; });
                return Task.CompletedTask;
            });
        });
        Assert.Empty(ran);
        Assert.Equal([1, null], _grid.Invocations["inOrder"][0].Arguments);

        Assert.Equal("landed", await cut.InvokeAsync(() => Bridge().RunWork(1)));
        // Placed from inside work 1's turn, work 2 names it.
        Assert.Equal([2, 1], _grid.Invocations["inOrder"][1].Arguments);
        Assert.Equal("landed", await cut.InvokeAsync(() => Bridge().RunWork(2)));
        placed.SetResult(true);
        await (outer ?? throw new InvalidOperationException("Nothing was placed."));
        await (nested ?? throw new InvalidOperationException("The work placed nothing."));
        Assert.Equal(["outer", "inner"], ran);
    }

    [Fact] // ADR-0441 section 3: work that failed in .NET is thrown from the call as it was thrown
    public async Task ADR0441_work_that_failed_is_thrown_as_it_was()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("inOrder", _ => true);
        Task? outer = null;
        await cut.InvokeAsync(() => { outer = cut.Instance.InOrderAsync(() => throw new FormatException("The file is not a workbook.")); });

        Assert.Equal("failed", await cut.InvokeAsync(() => Bridge().RunWork(1)));
        // The browser ended the turn failed, and answers the call with its own error.
        placed.SetException(new JSException(".NET's work 1 failed."));
        Assert.Equal("The file is not a workbook.", (await Assert.ThrowsAsync<FormatException>(() => outer ?? Task.CompletedTask)).Message);
    }

    [Fact] // ADR-0441 section 3: from a handler of the grid's events, work runs at once as part of the event, and cancellable work is refused by name
    public async Task ADR0441_from_an_events_handler_work_runs_at_once_and_cancellable_work_is_refused()
    {
        var ran = false;
        Exception? refused = null;
        IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>>? cut = null;
        cut = Grid(ps => ps.Add(g => g.Rows, _rows).Add(g => g.DeclaredKeys, ["F9"]).Add(g => g.OnDeclaredKey, async (GridDeclaredKeyPress _) =>
        {
            var grid = cut?.Instance ?? throw new InvalidOperationException("The key was pressed before the grid was rendered.");
            await grid.InOrderAsync(() => { ran = true; return Task.CompletedTask; });
            refused = await Record.ExceptionAsync(() => grid.CancellableInOrderAsync(_ => Task.CompletedTask, _ => Task.CompletedTask));
        }));

        await cut.InvokeAsync(() => Bridge().Heard("declaredKey", Json(
            $$"""{"key":"F9","editOpen":false,"selection":{{FocusOnRowTwo}},"rowSequenceVersion":0}""")));

        Assert.True(ran);
        Assert.DoesNotContain(_grid.Invocations, invocation => invocation.Identifier is "inOrder" or "cancellableInOrder");
        Assert.Contains("outside a handler of the grid's events", Assert.IsType<InvalidOperationException>(refused).Message);
    }

    [Fact] // ADR-0240 section 12 / ADR-0441 section 3: an Escape cancels the work's token from the browser, and one too late is said by the Consumer in its turn
    public async Task ADR0441_an_escape_cancels_the_works_token_and_one_too_late_is_said()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("cancellableInOrder", _ => true);
        var cancelled = false;
        var late = new List<bool>();
        Task<bool>? work = null;
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(async token =>
            {
                var seen = new TaskCompletionSource();
                await using (token.Register(() => { cancelled = true; seen.SetResult(); }))
                    await seen.Task;
                token.ThrowIfCancellationRequested();
            }, landed => { late.Add(landed); return Task.CompletedTask; });
        });

        var running = cut.InvokeAsync(() => Bridge().RunWork(1));
        await cut.InvokeAsync(() => Bridge().CancelWork(1));
        Assert.Equal("cancelled", await running);
        Assert.True(cancelled);
        placed.SetResult(false);
        Assert.False(await (work ?? throw new InvalidOperationException("Nothing was placed.")));

        // An Escape the browser answers after the work ended still finds it, until the browser lets it go.
        await cut.InvokeAsync(() => Bridge().WorkCancelledTooLate(1, false));
        await cut.InvokeAsync(() => Bridge().ForgetWork(1));
        await cut.InvokeAsync(() => Bridge().WorkCancelledTooLate(1, true));
        Assert.Equal([false], late);
    }

    [Fact] // ADR-0441 section 3: cancellable work holds no turn of the core's: what it places runs at once inside it, its Selection is read at once, and cancellable work inside it is refused
    public async Task ADR0441_inside_cancellable_work_work_runs_at_once_and_cancellable_work_is_refused()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("cancellableInOrder", _ => true);
        _grid.Setup<JsonElement>("readSelection", _ => true).SetResult(Json(FocusOnRowTwo));
        var ran = false;
        GridSelection? read = null;
        Exception? refused = null;
        Task<bool>? work = null;
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(async _ =>
            {
                await cut.Instance.InOrderAsync(() => { ran = true; return Task.CompletedTask; });
                read = await cut.Instance.SelectionAfterInputsAsync();
                refused = await Record.ExceptionAsync(() => cut.Instance.CancellableInOrderAsync(_ => Task.CompletedTask, _ => Task.CompletedTask));
            }, _ => Task.CompletedTask);
        });

        Assert.Equal("landed", await cut.InvokeAsync(() => Bridge().RunWork(1)));
        Assert.True(ran);
        Assert.Equal(2, read?.Focus.Row);
        Assert.IsType<InvalidOperationException>(refused);
        Assert.DoesNotContain(_grid.Invocations, invocation => invocation.Identifier is "inOrder" or "selectionAfterInputs");
        Assert.Single(_grid.Invocations["cancellableInOrder"]);
        placed.SetResult(true);
        Assert.True(await (work ?? throw new InvalidOperationException("Nothing was placed.")));
    }

    [Fact] // ADR-0441 section 3: disposing of the grid cancels the work under way
    public async Task ADR0441_disposing_of_the_grid_cancels_the_work_under_way()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("cancellableInOrder", _ => true);
        _grid.SetupVoid("dispose", _ => true).SetVoidResult();
        var token = CancellationToken.None;
        var started = new TaskCompletionSource();
        Task<bool>? work = null;
        await cut.InvokeAsync(() =>
        {
            work = cut.Instance.CancellableInOrderAsync(async seen =>
            {
                token = seen;
                started.SetResult();
                await Task.Delay(Timeout.Infinite, seen);
            }, _ => Task.CompletedTask);
        });
        var running = cut.InvokeAsync(() => Bridge().RunWork(1));
        await started.Task;

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        Assert.True(token.IsCancellationRequested);
        Assert.Equal("cancelled", await running);
        // The browser's grid went with it: the call answers that the work did not run to its end.
        placed.SetException(new JSDisconnectedException("The grid is gone."));
        Assert.False(await (work ?? throw new InvalidOperationException("Nothing was placed.")));
    }

    [Fact] // ADR-0441 section 2: the open edit's text is the browser's, and none crosses as null
    public async Task ADR0441_the_open_edits_text_is_read_from_the_browser()
    {
        var cut = Grid();
        var answer = _grid.Setup<JsonElement>("readEditText", _ => true);
        answer.SetResult(Json("\"=SUM(\""));
        Assert.Equal("=SUM(", await cut.InvokeAsync(() => cut.Instance.ReadEditTextAsync()));
    }

    [Fact] // ADR-0130 / ADR-0441 section 5: whether anything can answer a Selection Summary is answered in .NET: rows the grid holds summarise
    public void ADR0441_can_summarize_is_the_components_own_answerer_or_its_sources()
    {
        Assert.False(Grid(ps => ps.Add(g => g.Source, new TestSource())).Instance.CanSummarize);
        Assert.True(Grid().Instance.CanSummarize);
        Assert.True(Grid(ps => ps.Add(g => g.Rows, _rows).Add(g => g.OnSummarize,
            (Summarizing.GridSummaryRequest _, CancellationToken _) => Task.FromResult(Summarizing.GridSummaryResult.Declined("Ask the desk.")))).Instance.CanSummarize);
    }

    private static RenderFragment<GridPopoverContext> Contents(Action<GridPopoverContext> seen) => context => builder =>
    {
        seen(context);
        builder.AddMarkupContent(0, "<button data-ex-claims=\"click\">Apply the book</button>");
    };

    [Fact] // ADR-0441 section 4: the Consumer's popover opens in the browser's frame, its contents drawn in the slot the browser mounts for the opening, and its context crosses as it is used
    public async Task ADR0441_the_consumers_popover_is_drawn_in_the_slot_the_browser_mounts()
    {
        var cut = Grid();
        var opened = _grid.Setup<bool>("openPopover", _ => true);
        _grid.SetupVoid("slotRendered", _ => true).SetVoidResult();
        _grid.SetupVoid("closePopoverOf", _ => true).SetVoidResult();
        _grid.SetupVoid("innerPopupChanged", _ => true).SetVoidResult();
        GridPopoverContext? context = null;
        var opening = cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(Contents(seen => context = seen), "Book"));
        Assert.Equal([1, "Book"], Assert.Single(_grid.Invocations["openPopover"]).Arguments);

        // The browser mounts the slot as it opens the frame, inside the call.
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "consumerPopover", """{"opening":1}"""));
        opened.SetResult(true);
        Assert.True(await opening);
        Assert.Contains("Apply the book", cut.Markup);
        var standing = context ?? throw new InvalidOperationException("The contents were drawn with no context.");
        Assert.NotNull(standing.InOrder);
        Assert.NotNull(standing.TakeFocus);
        await cut.InvokeAsync(() =>
        {
            standing.InnerPopupChanged(true);
            standing.Close();
        });
        Assert.Equal([1, true], Assert.Single(_grid.Invocations["innerPopupChanged"]).Arguments);
        Assert.Equal([1], Assert.Single(_grid.Invocations["closePopoverOf"]).Arguments);

        // Closed in the browser, the opening is gone: its context speaks for nothing, and its slot draws nothing.
        await cut.InvokeAsync(() => Bridge().PopoverClosed(1));
        await cut.InvokeAsync(() => standing.Close());
        Assert.Single(_grid.Invocations["closePopoverOf"]);
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2, """{"opening":1}"""));
        Assert.DoesNotContain("Apply the book", cut.Markup);
    }

    [Fact] // ADR-0441 section 4: a popover the browser refuses to open leaves none standing, and an empty label is refused by name
    public async Task ADR0441_a_popover_refused_leaves_none_and_an_empty_label_is_refused()
    {
        var cut = Grid();
        _grid.Setup<bool>("openPopover", _ => true).SetResult(false);
        Assert.False(await cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(Contents(_ => { }), "Book")));
        await Assert.ThrowsAsync<ArgumentException>(() => cut.Instance.OpenPopoverAsync(Contents(_ => { }), " "));
        // Nothing stands for ClosePopoverAsync to close.
        await cut.InvokeAsync(() => cut.Instance.ClosePopoverAsync());
        Assert.DoesNotContain(_grid.Invocations, invocation => invocation.Identifier == "closePopover");
    }

    private static RenderFragment<GridPopoverContext> Showing(Func<string> text) => _ => builder => builder.AddContent(0, text());

    [Fact] // ADR-0441 section 4: a later opening refused leaves the popover the browser shows drawn, never one it closed meanwhile; the contents are drawn again with the page
    public async Task ADR0441_a_refused_opening_leaves_the_popover_shown_and_the_contents_follow_the_page()
    {
        var cut = Grid();
        _grid.SetupVoid("slotRendered", _ => true).SetVoidResult();
        _grid.Setup<bool>("openPopover", invocation => invocation.Arguments[0] is 1).SetResult(true);
        var book = "Credit";
        Assert.True(await cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(Showing(() => $"First: {book}"), "Book")));
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "consumerPopover", """{"opening":1}"""));
        Assert.Contains("First: Credit", cut.Markup);

        // The page renders with a new book: the contents read it, drawn again on their own.
        book = "Rates";
        cut.Render(_ => { });
        Assert.Contains("First: Rates", cut.Markup);

        // A second opening refused: the first, still shown, is drawn again.
        _grid.Setup<bool>("openPopover", invocation => invocation.Arguments[0] is 2).SetResult(false);
        Assert.False(await cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(Showing(() => "Second"), "Book")));
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2, """{"opening":1}"""));
        Assert.Contains("First: Rates", cut.Markup);

        // A third refused while the first closed meanwhile: nothing comes back.
        var third = _grid.Setup<bool>("openPopover", invocation => invocation.Arguments[0] is 3);
        var refused = cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(Showing(() => "Third"), "Book"));
        await cut.InvokeAsync(() => Bridge().PopoverClosed(1));
        third.SetResult(false);
        Assert.False(await refused);
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 3, """{"opening":1}"""));
        Assert.DoesNotContain("First", cut.Markup);
        await cut.InvokeAsync(() => cut.Instance.ClosePopoverAsync());
        Assert.DoesNotContain(_grid.Invocations, invocation => invocation.Identifier == "closePopover");
    }

    [Fact] // ADR-0240 section 6 / ADR-0441 section 4: a claimed event's handler hands its work, which runs in the turn the browser reached for the claim's number
    public async Task ADR0441_a_claimed_events_work_runs_in_the_turn_of_its_number()
    {
        var cut = Grid();
        var ran = false;
        await cut.InvokeAsync(() => Bridge().ClaimTold("click", -1));
        Task? handled = null;
        await cut.InvokeAsync(() => { handled = cut.Instance.InOrderAsync(GridInputKind.Click, () => { ran = true; return Task.CompletedTask; }); });
        Assert.False(ran);
        // Its dispatch over, a telling a handler took stands.
        await cut.InvokeAsync(() => Bridge().ClaimPassed(-1));

        Assert.Equal("landed", await cut.InvokeAsync(() => Bridge().RunClaim(-1)));
        Assert.True(ran);
        await (handled ?? throw new InvalidOperationException("Nothing was handed."));
        Assert.DoesNotContain(_grid.Invocations, invocation => invocation.Identifier == "inOrder");
    }

    [Fact] // ADR-0441 section 4: a telling no handler takes ends its turn void: its dispatch passed, voided by the next, or of another kind than the handler hears
    public async Task ADR0441_a_telling_no_handler_takes_ends_its_turn_void()
    {
        var cut = Grid();
        var placed = _grid.Setup<bool>("inOrder", _ => true);
        await cut.InvokeAsync(() => Bridge().ClaimTold("click", -1));
        await cut.InvokeAsync(() => Bridge().ClaimPassed(-1));
        Assert.Equal("void", await cut.InvokeAsync(() => Bridge().RunClaim(-1)));

        await cut.InvokeAsync(() => Bridge().ClaimTold("click", -2));
        await cut.InvokeAsync(() => Bridge().ClaimTold("keydown", -3));
        Assert.Equal("void", await cut.InvokeAsync(() => Bridge().RunClaim(-2)));

        // The handler hears a click where a key was told: the key's number is no one's, and the work takes a place of its own.
        Task? handled = null;
        await cut.InvokeAsync(() => { handled = cut.Instance.InOrderAsync(GridInputKind.Click, () => Task.CompletedTask); });
        Assert.Equal("void", await cut.InvokeAsync(() => Bridge().RunClaim(-3)));
        Assert.Single(_grid.Invocations["inOrder"]);
        Assert.Equal("landed", await cut.InvokeAsync(() => Bridge().RunWork(1)));
        placed.SetResult(true);
        await (handled ?? throw new InvalidOperationException("Nothing was handed."));

        // A crossing of the keyboard is told by no Input Root around this grid: its handling does not run.
        var crossed = false;
        await cut.InvokeAsync(() => cut.Instance.InOrderAsync(GridInputKind.KeyboardIn, () => { crossed = true; return Task.CompletedTask; }));
        Assert.False(crossed);
    }

    [Fact] // ADR-0441 section 4: a claimed handler's failure is thrown from its call as it was thrown; disposing of the grid ends a handed work not yet run with nothing done
    public async Task ADR0441_a_claimed_failure_is_thrown_and_disposal_ends_handed_work()
    {
        var cut = Grid();
        _grid.SetupVoid("dispose", _ => true).SetVoidResult();
        await cut.InvokeAsync(() => Bridge().ClaimTold("keydown", -1));
        Task? failing = null;
        await cut.InvokeAsync(() => { failing = cut.Instance.InOrderAsync(GridInputKind.KeyDown, () => throw new FormatException("Not a book.")); });
        Assert.Equal("failed", await cut.InvokeAsync(() => Bridge().RunClaim(-1)));
        Assert.Equal("Not a book.", (await Assert.ThrowsAsync<FormatException>(() => failing ?? Task.CompletedTask)).Message);

        var ran = false;
        await cut.InvokeAsync(() => Bridge().ClaimTold("click", -2));
        Task? handed = null;
        await cut.InvokeAsync(() => { handed = cut.Instance.InOrderAsync(GridInputKind.Click, () => { ran = true; return Task.CompletedTask; }); });
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await (handed ?? throw new InvalidOperationException("Nothing was handed."));
        Assert.False(ran);
    }
}
