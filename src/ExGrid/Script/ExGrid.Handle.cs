using System.Runtime.ExceptionServices;
using System.Text.Json;
using ExGrid.Components;
using ExGrid.Selection;
using Microsoft.JSInterop;

namespace ExGrid.Script;

// What the Consumer calls on the grid, beyond placing the Selection and handing the keyboard on (ADR-0441): the view,
// the open edit's text and the Selection after the inputs are read from the browser, which holds them; a view handed
// back crosses at once, ahead of the state that brings its rows; and work of the Consumer's takes its turn in the
// browser's Input Order, which asks .NET to run it.
public sealed partial class ExGrid<TRow>
{
    // The views the browser keeps for .NET, by the number each was read under, while .NET may still hand one back: the
    // core knows a view by the object it handed out (ADR-0441 section 1). Those .NET has collected are let go with the
    // next read.
    private readonly List<(int Number, WeakReference<RowSetStamp> Stamp)> _viewsHeld = [];

    // The work placed in the browser's order, by its number, while it may still run or be said too late.
    private readonly Dictionary<int, Work> _works = [];
    private int _worksPlaced;

    private sealed class Work(Func<CancellationToken, Task> handling, Func<bool, Task>? cancelledTooLate)
    {
        public Func<CancellationToken, Task> Handling { get; } = handling;
        public Func<bool, Task>? CancelledTooLate { get; } = cancelledTooLate;
        public CancellationTokenSource Cancel { get; } = new();
        public Exception? Failure { get; set; }

        /// <summary>Completed once the work has run, for a handler that handed it to a claim and waits on it.</summary>
        public TaskCompletionSource? Ended { get; init; }
    }

    // The turn of work placed in the browser's order that this flow runs in, while it runs: work placed from there runs
    // inside it (the core's GridTurn). A flow that outlives it carries an ended turn, and is free.
    private static readonly AsyncLocal<Turn?> WorkingIn = new();

    // The turn this flow runs in that holds the inputs after it and has no turn of the core's to place work in, while
    // it runs: a handler of the grid's events, or cancellable work. Work placed from there runs at once, as part of it
    // (ADR-0441 section 3).
    private static readonly AsyncLocal<Turn?> HoldingInputs = new();

    private sealed class Turn(object grid, int number)
    {
        public object Grid { get; } = grid;
        public int Number { get; } = number;
        public bool Ended { get; set; }
    }

    /// <summary>The number of this grid's work whose handling the caller runs in, while it runs, or null.</summary>
    private int? TurnCalledFrom() => WorkingIn.Value is { Ended: false } turn && ReferenceEquals(turn.Grid, this) ? turn.Number : null;

    /// <summary>Whether the caller runs in a handler of this grid's events, or in its cancellable work, while it runs.</summary>
    private bool InputsHeldByCaller() => HoldingInputs.Value is { Ended: false } turn && ReferenceEquals(turn.Grid, this);

    /// <summary>Runs <paramref name="hand"/>, the handling of one of the grid's events, as the event's own turn.</summary>
    private async Task<string?> HandledAsEvent(Func<Task<string?>> hand)
    {
        var turn = new Turn(this, 0);
        HoldingInputs.Value = turn;
        try
        {
            return await hand();
        }
        finally
        {
            turn.Ended = true;
        }
    }

    private IJSObjectReference Attached(string what)
        => _grid ?? throw new InvalidOperationException($"This ExGrid is not attached: a grid not drawn {what}.");

    // Runs `work` on the renderer's dispatcher, where the views and the work this grid keeps are touched: a Consumer
    // may call from a service's thread.
    private async Task<T> Dispatched<T>(Func<Task<T>> work)
    {
        Task<T>? started = null;
        await InvokeAsync(() => { started = work(); });
        return await (started ?? throw new InvalidOperationException("Internal: the dispatcher ran nothing."));
    }

    /// <summary>
    /// The view as the grid holds it now (ADR-0331, ADR-0441): the Selection with the Row Sequence Version it is written
    /// in, and the scroll offsets, read from the browser once what the Consumer's source said before the call has
    /// crossed. A Consumer that shows several sets of rows in one grid, one at a time, keeps each set's view and hands
    /// it back with <see cref="ShowViewWith(GridView)"/> when that set is shown again. Read inside work placed with
    /// <see cref="InOrderAsync(Func{Task})"/>, or from a handler of a key or a press of the grid's, it is the view the inputs before
    /// it left.
    /// </summary>
    /// <returns>The view, which brings the same set of rows when handed back to this grid.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public Task<GridView> ReadViewAsync()
    {
        var grid = Attached("holds no view");
        return Dispatched(async () =>
        {
            await SendStateAsync();
            var released = _viewsHeld.Where(held => !held.Stamp.TryGetTarget(out _)).Select(held => held.Number).ToHashSet();
            var read = await AnswerOf(grid, await grid.InvokeAsync<JsonElement>("readView", released));
            // The browser let them go with the read: only now are they forgotten here.
            _viewsHeld.RemoveAll(held => released.Contains(held.Number));
            var number = read.GetProperty("number").GetInt32();
            var view = new GridView(
                read.GetProperty("rowSequenceVersion").GetInt32(), ScriptJson.Selection(read.GetProperty("selection")),
                read.GetProperty("scrollTopPx").GetDouble(), read.GetProperty("scrollLeftPx").GetDouble());
            var stamp = new RowSetStamp(this, number) { Read = view };
            _viewsHeld.Add((number, new WeakReference<RowSetStamp>(stamp)));
            return view with { RowSet = stamp };
        });
    }

    /// <summary>
    /// Shows <paramref name="view"/> once the grid takes the rows its <see cref="GridView.RowSequenceVersion"/> names
    /// (ADR-0331, ADR-0441): its Selection in place of the empty one a new version leaves (ADR-0011), and its scroll
    /// offsets. Call it from the handler that moves the source to that version, before the source says the rows changed
    /// or in the same run of the handler, with nothing awaited between: the view crosses at once, ahead of the state that
    /// brings the rows, and the grid takes both together, so an input made after the switch meets the set's own
    /// Selection. A view handed later replaces one not yet taken; one whose version the grid never takes is never shown,
    /// and a Selection that does not fit the rows then shown is dropped.
    ///
    /// <para>A view this grid read with <see cref="ReadViewAsync"/>, carried through <c>with</c> to the version, brings back
    /// the set of rows it was read of, its Selection and offsets as read, and an edit left open over that set comes back
    /// over its cell (ADR-0331 section 5). Any other view is of rows never shown before: it holds no Selection or one
    /// range, at offsets of zero.</para>
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid.</param>
    /// <exception cref="ArgumentException">The view holds no Selection (<c>default(GridView)</c>); is one this grid read,
    /// with another Selection or other offsets than it was read with; or is not one this grid read and holds more than
    /// one range or offsets other than zero.</exception>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public void ShowViewWith(GridView view)
    {
        if (view.Selection is null)
            throw new ArgumentException("A view holds a Selection; start from GridSelection.Empty.", nameof(view));
        // A view this grid read brings its own Selection back; one of rows never shown brings its one range, if any.
        var read = ReadHere(view) is not null;
        if (!read && view.Selection.Ranges.Count > 1)
        {
            throw new ArgumentException("A view this grid did not read is of rows never shown before: it holds no Selection or one range. Read "
                + "the view to hand back with ReadViewAsync, or hand a place with ShowViewWith(view, range, focus) (ADR-0441).", nameof(view));
        }
        (SelectionRange, CellPosition)? place = !read && view.Selection.Ranges is [var range] ? (range, view.Selection.Focus) : null;
        ShowView(view, place, nameof(view));
    }

    /// <summary>
    /// Shows <paramref name="view"/> as <see cref="ShowViewWith(GridView)"/> does, with <paramref name="range"/> placed
    /// as its Selection and the Focus on <paramref name="focus"/>, revealed from the view's offsets, as
    /// <see cref="PlaceSelectionAsync"/> places them (ADR-0050 item 4, ADR-0441). A Consumer that shows other rows to go
    /// to a place among them hands the place with the rows, so it is placed under the version that brings them. A range
    /// reaching outside the rows and columns shown once the version is taken is not placed.
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid; its Selection is replaced.</param>
    /// <param name="range">The one range to place.</param>
    /// <param name="focus">The Focus, inside <paramref name="range"/>.</param>
    /// <exception cref="ArgumentException">The view holds no Selection; is one this grid read, with other offsets than it
    /// was read with; or is not one this grid read and has offsets other than zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The Focus lies outside the range, or the range starts before the first row or column.</exception>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public void ShowViewWith(GridView view, SelectionRange range, CellPosition focus)
    {
        if (view.Selection is null)
            throw new ArgumentException("A view holds a Selection; start from GridSelection.Empty.", nameof(view));
        if (range.TopRow < 0 || range.LeftColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(range), range, "A range starts at row and column 0 or after (ADR-0050).");
        if (!range.Contains(focus))
            throw new ArgumentOutOfRangeException(nameof(focus), focus, "The Focus is placed inside the range it belongs to (ADR-0050/0052).");
        ShowView(view, (range, focus), nameof(view));
    }

    /// <summary>The number the browser keeps <paramref name="view"/> under, where this grid read it.</summary>
    private int? ReadHere(GridView view) => view.RowSet is { } stamp && ReferenceEquals(stamp.Grid, this) ? stamp.Set : null;

    private void ShowView(GridView view, (SelectionRange Range, CellPosition Focus)? place, string name)
    {
        var grid = Attached("shows no view");
        var number = ReadHere(view);
        if (view.RowSet is { Read: { } read } && number is not null
            && (!view.ScrollTopPx.Equals(read.ScrollTopPx) || !view.ScrollLeftPx.Equals(read.ScrollLeftPx) || (place is null && !Equals(view.Selection, read.Selection))))
        {
            // The browser keeps the view as it read it, and would show that, not this.
            throw new ArgumentException("A view this grid read is handed back with the Selection and offsets it was read with, its version "
                + "aside. Hand another place with ShowViewWith(view, range, focus) (ADR-0441).", name);
        }
        if (number is null && (!view.ScrollTopPx.Equals(0) || !view.ScrollLeftPx.Equals(0)))
        {
            throw new ArgumentException("A view this grid did not read is of rows never shown before, at offsets of zero. Read the view to "
                + "hand back with ReadViewAsync (ADR-0441).", name);
        }
        var json = Json(json =>
        {
            json.WriteStartObject();
            if (number is { } read)
                json.WriteNumber("number", read);
            else
                json.WriteNull("number");
            json.WriteNumber("rowSequenceVersion", view.RowSequenceVersion);
            if (place is { } placed)
            {
                json.WriteStartObject("place");
                json.WritePropertyName("range");
                JsonSerializer.Serialize(json, ScriptJson.Range(placed.Range));
                json.WritePropertyName("focus");
                JsonSerializer.Serialize(json, ScriptJson.Cell(placed.Focus));
                json.WriteEndObject();
            }
            else
            {
                json.WriteNull("place");
            }
            json.WriteEndObject();
        });
        // Sent now, as the call is made, so it crosses ahead of any state the source sends after it.
        _ = Sent(grid.InvokeVoidAsync("showView", json));
    }

    // A call sent without waiting on it: a failure that is not the circuit going fails the component, in its place.
    private async Task Sent(ValueTask sending)
    {
        try
        {
            await sending;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The grid or the circuit is gone, and the view with it.
        }
        catch (Exception ex) when (!_disposed)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    /// <summary>
    /// The open edit's text as the grid holds it, or null while no edit is open (ADR-0331 section 5, ADR-0441): read
    /// from the browser, what the keys before the call left in it. A Consumer deciding what a declared key pressed with
    /// an edit open means reads it from that key's handler, where it is the text the key was pressed over.
    /// </summary>
    /// <returns>The open edit's text, or null.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public async Task<string?> ReadEditTextAsync()
    {
        var grid = Attached("holds no edit");
        await SendStateAsync();
        var text = await AnswerOf(grid, await grid.InvokeAsync<JsonElement>("readEditText"));
        return text.ValueKind == JsonValueKind.Null ? null : text.GetString();
    }

    /// <summary>
    /// The Selection once the grid has answered every input it received before the call (ADR-0240 section 6,
    /// ADR-0441): for a button of the Consumer's, pressed straight after a click, that acts on what the user selected.
    /// From a handler of the grid's events, or inside work placed with <see cref="InOrderAsync(Func{Task})"/> or
    /// <see cref="CancellableInOrderAsync"/>, it is read at once, as the inputs after it are held.
    /// </summary>
    /// <returns>The Selection.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    /// <exception cref="ObjectDisposedException">The grid was disposed before it was read.</exception>
    public async Task<GridSelection> SelectionAfterInputsAsync()
    {
        var grid = Attached("holds no Selection");
        if (!InputsHeldByCaller())
            return await SelectionInTurnAsync(grid, TurnCalledFrom());
        await SendStateAsync();
        return ScriptJson.Selection(await AnswerOf(grid, await grid.InvokeAsync<JsonElement>("readSelection")));
    }

    private async Task<GridSelection> SelectionInTurnAsync(IJSObjectReference grid, int? within)
    {
        JsonElement read;
        try
        {
            // Inputs ahead of it may take longer than a call's own time allows.
            read = await AnswerOf(grid, await grid.InvokeAsync<JsonElement>("selectionAfterInputs", CancellationToken.None, [within]));
        }
        catch (JSDisconnectedException) when (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, "The grid was disposed before the Selection was read.");
        }
        if (read.ValueKind == JsonValueKind.Null)
            throw new ObjectDisposedException(GetType().Name, "The grid was disposed before the Selection was read.");
        return ScriptJson.Selection(read);
    }

    /// <summary>
    /// Runs <paramref name="handling"/>, work of the Consumer's that no input of the grid's made — a command from a
    /// button outside the grid, a service's call — in its place in the Input Order (ADR-0240 section 6, ADR-0441):
    /// after every input the grid has received, and before any made after it, which wait until it has finished. Work
    /// placed from inside <paramref name="handling"/> runs at once, inside it. From a handler of the grid's events, or
    /// from inside work placed with <see cref="CancellableInOrderAsync"/>, it runs at once, as part of it: an edit, a paste, a fill, a declared key, a Replace All, a marking gesture or
    /// a Context Menu's command holds the inputs after it until its handler ends, and cancellable work until it ends. Once the grid is disposed it runs
    /// nothing.
    /// </summary>
    /// <param name="handling">The work.</param>
    /// <returns>A task that completes once the work has, with its failure where it failed.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public async Task InOrderAsync(Func<Task> handling)
    {
        ArgumentNullException.ThrowIfNull(handling);
        if (InputsHeldByCaller())
        {
            await handling();
            return;
        }
        await PlaceAsync(new Work(_ => handling(), null), "inOrder");
    }

    /// <summary>
    /// Runs <paramref name="handling"/>, work that may take a while and that the user may cancel — a file being read —
    /// in its place in the Input Order as <see cref="InOrderAsync(Func{Task})"/> does (ADR-0240 section 12, ADR-0441). From the call
    /// until the work ends the Settled Stamp names it (<c>data-ex-work-placed</c>), and from its turn the root is busy and
    /// painted as a paste under way is. A plain Escape typed in the grid against a page whose stamp named the work
    /// cancels <paramref name="handling"/>'s token and does nothing else. The work looks at the token until the point
    /// where it lands, and ends in <see cref="OperationCanceledException"/> once it is cancelled; past that point it lands
    /// whatever the token says. An Escape that came after the work landed runs <paramref name="cancelledTooLate"/> with
    /// true, in its own turn, and one that found nothing to cancel with false: saying what the work did is the
    /// Consumer's. Disposing of the grid cancels the work under way.
    /// </summary>
    /// <param name="handling">The work, handed the token an Escape cancels.</param>
    /// <param name="cancelledTooLate">Says an Escape came too late: true where the work had landed.</param>
    /// <returns>Whether the work ran to its end: false where an Escape cancelled it, or it never ran.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet, or the call is made from a handler of the
    /// grid's events or from inside cancellable work, whose turn no Escape made after it can reach.</exception>
    public async Task<bool> CancellableInOrderAsync(Func<CancellationToken, Task> handling, Func<bool, Task> cancelledTooLate)
    {
        ArgumentNullException.ThrowIfNull(handling);
        ArgumentNullException.ThrowIfNull(cancelledTooLate);
        if (InputsHeldByCaller())
        {
            throw new InvalidOperationException("Cancellable work is placed from outside a handler of the grid's events and outside cancellable "
                + "work: there the work's turn is that one, and no Escape typed after it could reach it to cancel it (ADR-0441).");
        }
        return await PlaceAsync(new Work(handling, cancelledTooLate), "cancellableInOrder");
    }

    private Task<bool> PlaceAsync(Work work, string method)
    {
        var grid = Attached("orders no work");
        // Asked in the caller's flow, which the dispatcher need not carry.
        var within = TurnCalledFrom();
        return Dispatched(() => PlacedAsync(grid, work, method, within));
    }

    private async Task<bool> PlacedAsync(IJSObjectReference grid, Work work, string method, int? within)
    {
        var number = ++_worksPlaced;
        _works[number] = work;
        try
        {
            // The inputs ahead of it, and the work itself, may take longer than a call's own time allows.
            var landed = await grid.InvokeAsync<bool>(method, CancellationToken.None, [number, within]);
            if (work.Failure is { } failure)
                ExceptionDispatchInfo.Throw(failure);
            return landed;
        }
        catch (JSException) when (work.Failure is { } failure)
        {
            // The work failed in .NET, and the browser was told so its turn ended failed: thrown as it was thrown.
            ExceptionDispatchInfo.Throw(failure);
            throw;
        }
        catch (Exception ex) when (_disposed && ex is JSDisconnectedException or JSException or ObjectDisposedException or OperationCanceledException)
        {
            return false;
        }
        finally
        {
            // An Escape said too late still finds the work; the browser lets it go once no Escape can (ForgetWork).
            if (work.CancelledTooLate is null)
                _works.Remove(number);
            work.Cancel.Dispose();
        }
    }

    /// <summary>The browser reached the turn of work <paramref name="number"/>: it runs, and the browser hears how it
    /// ended, <c>landed</c>, <c>cancelled</c> or <c>failed</c>, the failure kept to be thrown from the call.</summary>
    internal async Task<string> RunWorkAsync(int number)
    {
        if (!_works.TryGetValue(number, out var work))
            throw new InvalidOperationException($"Internal: the browser ran the work {number}, which is not under way.");
        var turn = new Turn(this, number);
        // Cancellable work holds no turn of the core's: what it places runs at once inside it, as an event's handler's does.
        if (work.CancelledTooLate is null)
            WorkingIn.Value = turn;
        else
            HoldingInputs.Value = turn;
        var token = work.Cancel.Token;
        try
        {
            await work.Handling(token);
            return "landed";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return "cancelled";
        }
        catch (Exception failed)
        {
            work.Failure = failed;
            return "failed";
        }
        finally
        {
            turn.Ended = true;
        }
    }

    /// <summary>An Escape typed against work <paramref name="number"/> the page named: its token is cancelled.</summary>
    internal void CancelWork(int number)
    {
        if (_works.TryGetValue(number, out var work))
        {
            try
            {
                work.Cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The work ended as the Escape crossed: the browser says it too late.
            }
        }
    }

    /// <summary>An Escape typed against work <paramref name="number"/> came after it landed, or found nothing to cancel,
    /// answered in its own turn: the Consumer says so.</summary>
    internal async Task WorkCancelledTooLateAsync(int number, bool landed)
    {
        if (_disposed || !_works.TryGetValue(number, out var work) || work.CancelledTooLate is not { } tooLate)
            return;
        try
        {
            await tooLate(landed);
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    /// <summary>No Escape can name work <paramref name="number"/> any more: it is let go.</summary>
    internal void ForgetWork(int number) => _works.Remove(number);

    /// <summary>The grid is disposed: the work under way is cancelled (ADR-0441 section 3).</summary>
    private void CancelWorks()
    {
        foreach (var number in _works.Keys.ToArray())
            CancelWork(number);
    }

    /// <summary>
    /// Asks the Selection Summary again (ADR-0130, ADR-0441): for a Consumer whose values under the Selection moved
    /// where the grid cannot see them, outside the Window or by a recalculation. The figures clear at once and the
    /// question is asked again; nothing happens while no figure stands or is being asked for.
    /// </summary>
    /// <returns>A task that completes once the request has been made.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public async Task RefreshSummaryAsync()
    {
        var grid = Attached("shows no summary");
        await SendStateAsync();
        await grid.InvokeVoidAsync("refreshSummary");
    }

    /// <summary>Whether anything can answer a Selection Summary (ADR-0130): <see cref="OnSummarize"/>, or a source that
    /// can summarise. False: no figure ever appears.</summary>
    public bool CanSummarize => OnSummarize is not null || (_source?.CanSummarize ?? false);

    /// <summary>
    /// Writes <paramref name="text"/> into the open edit as Point writes a Reference, for a press the Consumer took
    /// outside the grid (ADR-0058, ADR-0441): in place of what this Point wrote, or at the caret where a Reference can
    /// go. The edit is then in Point, written from outside; a further press replaces the text, and typing ends Point.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <returns>Whether it was written: false where no edit is open, pointing is not declared, or the edit is not in
    /// Point.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public async Task<bool> WritePointedTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var grid = Attached("holds no edit");
        await SendStateAsync();
        return await grid.InvokeAsync<bool>("writePointedText", text);
    }

    /// <summary>Takes back the text <see cref="WritePointedTextAsync"/> wrote last, while it still stands (ADR-0058,
    /// ADR-0441): the edit returns to what it was before that write.</summary>
    /// <returns>Whether it was taken back: false where no edit is open or that text no longer stands.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public async Task<bool> TakeBackPointedTextAsync()
    {
        var grid = Attached("holds no edit");
        await SendStateAsync();
        return await grid.InvokeAsync<bool>("takeBackPointedText");
    }

    // An answer of the browser's: as it came, or taken from the browser where it was too large for a message.
    private static async Task<JsonElement> AnswerOf(IJSObjectReference grid, JsonElement crossed)
        => HeldCalls.IsHeld(crossed) ? await HeldCalls.TakeAsync(grid, crossed) : crossed;
}
