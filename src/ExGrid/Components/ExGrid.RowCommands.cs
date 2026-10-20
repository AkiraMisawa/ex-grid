using ExGrid.Chrome;
using ExGrid.Rows;
using Microsoft.JSInterop;

namespace ExGrid.Components;

public partial class ExGrid<TRow>
{
    // Only current-Window references live here. Queued delegates capture this mutable ticket,
    // never a row, a key, an event argument or a row component (ADR-0160, ADR-0240).
    private sealed class WaitingRow(TRow? row, object? key, int binding)
    {
        internal TRow? Row = row;
        internal object? Key = key;
        internal int Binding = binding;
        internal bool Identified = row is not null;
    }

    private readonly HashSet<WaitingRow> _waitingRows = [];
    private const string ClaimRowAction = "row-action";
    private const string ClaimRowMark = "row-mark";
    private sealed record RememberedRowCommand(WaitingRow Target, int Paint, bool Mark,
        string? Column, string? Action, bool Marked)
    {
        internal int Input { get; set; }
    }
    private readonly Dictionary<int, RememberedRowCommand> _rowCommands = [];

    /// <summary>Registers a row click's target before any browser hold, without executing it or
    /// assigning its Input Order number. Called only by the grid's script (ADR-0240, ADR-0160).</summary>
    /// <param name="ticket">The script's per-instance click ticket.</param>
    /// <param name="paint">The paint at the press.</param>
    /// <param name="row">The row position at the press.</param>
    /// <param name="column">The Action column position, or -1 for a Mark.</param>
    /// <param name="action">The Action index, or -1 for a Mark.</param>
    /// <param name="mark">Whether this is a row Mark click.</param>
    /// <param name="wasMarked">Whether the pressed checkbox was painted as marked.</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void RememberRowClick(int ticket, int paint, int row, int column, int action, bool mark, bool wasMarked)
    {
        if (_disposed) return;
        CancelRowClick(ticket);
        var target = KeepWaitingRow(RowNamedByPress(paint, row, SerialPaintedAt(paint, row)));
        string? columnName = null;
        string? actionName = null;
        var painted = _paintRows.FindLast(p => p.Id == paint);
        if (!mark && painted is not null && column >= 0 && column < painted.Columns.Count
            && action >= 0 && action < painted.Columns[column].Actions.Count)
        {
            columnName = painted.Columns[column].Name;
            actionName = painted.Columns[column].Actions[action].Name;
        }
        _rowCommands[ticket] = new(target, paint, mark, columnName, actionName, !wasMarked);
    }

    /// <summary>Gives a registered click its Input Order number immediately before dispatching
    /// its Blazor event. Called only by the grid's script (ADR-0240).</summary>
    /// <param name="ticket">The registered click ticket.</param>
    /// <param name="input">The click's Input Order number.</param>
    /// <param name="fieldText">The browser field's text carried by this input (ADR-0240 section 5).</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void BeginRowClick(int ticket, int input, GridInputText? fieldText = null)
    {
        InputReached(input);
        RememberInputText(input, fieldText);
        if (_disposed || !_rowCommands.TryGetValue(ticket, out var command))
        {
            InputVoid(input);
            return;
        }
        TellInput(command.Mark ? ClaimRowMark : ClaimRowAction, input, ticket);
        command.Input = input;
    }

    /// <summary>Ends dispatch of a registered row click. If Blazor discarded the event, the core
    /// recovers that same command and number. Called only by the grid's script (ADR-0240).</summary>
    /// <param name="ticket">The click whose event dispatch finished.</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Task EndRowClickDispatch(int ticket)
    {
        if (_claimTold is { RowCommand: var active } && active == ticket
            && _rowCommands.TryGetValue(ticket, out var command)
            && TakeDispatchedRowCommand(command.Mark) is { } taken)
            return QueueRememberedRowCommand(taken);
        CancelRowClick(ticket);
        return Task.CompletedTask;
    }

    /// <summary>Releases a click discarded by the browser hold. Called only by the grid's
    /// script; a numbered click is answered void (ADR-0240, ADR-0160).</summary>
    /// <param name="ticket">The click to release.</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void CancelRowClick(int ticket)
    {
        if (!_rowCommands.Remove(ticket, out var command)) return;
        TakeWaitingRow(command.Target);
        if (_claimTold is { RowCommand: var active } && active == ticket) _claimTold = null;
        InputVoid(command.Input);
    }

    private RememberedRowCommand? TakeDispatchedRowCommand(bool mark)
    {
        var kind = mark ? ClaimRowMark : ClaimRowAction;
        if (_claimTold is not { } claim || claim.Kind != kind || claim.RowCommand == 0
            || !_rowCommands.Remove(claim.RowCommand, out var command)) return null;
        command.Input = ClaimInput(kind);
        return command;
    }

    private Task QueueRememberedRowCommand(RememberedRowCommand command)
    {
        if (command.Mark)
            return QueueMarkAsync(command.Input, command.Target, command.Paint, command.Marked);
        if (command.Column is { } column && command.Action is { } action)
            return QueueActionAsync(command.Input, command.Target, column, action, command.Paint);
        // No surviving descriptor or callback can name the action: nothing plausible is executed.
        TakeWaitingRow(command.Target);
        return QueueRowCommand(command.Input, () =>
        {
            if (!_disposed) AnnounceRowCommandRefusal();
            return Task.CompletedTask;
        });
    }


    // A keyed component keeps its serial when its row moves. Resolve it in the current Window;
    // no historical row/key is needed to recover the identity the browser's paint named.
    private TRow? RowNamedByPress(int paint, int position, int serial)
    {
        if (serial > 0)
            foreach (var row in _window)
                if (RowSerialOf(row) == serial) return row;
        return AimedUnderAnotherOrder(paint) ? null : RowInHand(position);
    }


    private WaitingRow KeepWaitingRow(TRow? row)
    {
        var waiting = new WaitingRow(row, row is null ? null : _rowKey?.Invoke(row), _binding);
        _waitingRows.Add(waiting);
        ReconcileWaitingRows();
        return waiting;
    }

    private void ReconcileWaitingRows()
    {
        foreach (var waiting in _waitingRows)
        {
            if (waiting.Row is null) continue; // Absence is permanent, including after reappearance.
            TRow? current = null;
            if (waiting.Binding == _binding)
            {
                foreach (var row in _window)
                {
                    if (waiting.Key is { } key && _rowKey is { } rowKey
                        ? Equals(rowKey(row), key) : ReferenceEquals(row, waiting.Row))
                    {
                        current = row;
                        break;
                    }
                }
            }
            waiting.Row = current;
            // A key can itself retain a whole historical generation. Refresh it with the row.
            waiting.Key = current is null ? null : _rowKey?.Invoke(current);
        }
    }

    private TRow? TakeWaitingRow(WaitingRow waiting)
    {
        _waitingRows.Remove(waiting);
        var row = waiting.Row;
        waiting.Row = null;
        waiting.Key = null;
        return row;
    }

    private void ReleaseWaitingRows()
    {
        foreach (var waiting in _waitingRows)
        {
            waiting.Row = null;
            waiting.Key = null;
        }
        _waitingRows.Clear();
        _rowCommands.Clear();
    }

    // Return to the row's Blazor event synchronously: its completion otherwise keeps the
    // row component and its old Row parameter alive throughout a potentially long queue.
    private Task QueueRowCommand(int input, Func<Task> command)
    {
        var completion = AnsweredAsync(command, input);
        _ = ObserveRowCommandAsync(completion);
        return Task.CompletedTask;
    }

    private async Task ObserveRowCommandAsync(Task completion)
    {
        try { await completion; }
        catch (Exception error) { await DispatchExceptionAsync(error); }
    }

    private void AnnounceRowCommandRefusal()
    {
        var words = RefusalWords(KeyRefusalLabelIds.RowCommandTargetLeft);
        if (string.IsNullOrEmpty(words)) return;
        Announce(words);
        _suppressRender = false;
        StateHasChanged();
    }
}
