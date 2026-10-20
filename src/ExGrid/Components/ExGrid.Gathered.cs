using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// Before a write is handled, the bound source puts out what it has gathered, so the write is made on,
// and carries, the newest version (ADR-0141/0142, D5 of 2026-10-06; LV-16). It is asked again as the
// handler of a write intent the grid raised completes, so a write the Consumer handed it, and it
// gathered, is in the Window before the next gesture is handled, and the user's own write settles
// there (D1; ExGrid.Commit.cs).
public partial class ExGrid<TRow>
{
    // Set while the grid asks its bound source to put out what it has gathered (ADR-0141/0142,
    // D5). The source raises StateChanged inside the call, on this thread, when it published
    // something; that event is noted instead of answered, and the Window is taken in once, after
    // the call, by the write that asked — never twice, and never half-way through the write.
    private bool _askingGathered;
    private bool _gatheredHeard;

    /// <summary>
    /// Asks the bound source to put out what it has gathered, on the grid's own synchronization
    /// context, and takes its Window in again through the state application, as a change it
    /// announced is taken in (ADR-0141/0142, D5; LV-16). Called just before a write is handled — a
    /// commit, an Action, a paste, a fill, a clear — so the write is made on, and carries, the
    /// newest version; and as the handler of a write intent the grid raised completes, so the write
    /// the Consumer handed the source is in the Window before the next gesture is handled (D1). A
    /// change gathered while the user typed is brought forward by the gesture, as in ExPivot
    /// (ADR-0067), never waited for. Without a source, or with nothing gathered, nothing
    /// happens. A gathered change that moved the order drops the Selection (ADR-0011), and an open
    /// edit outlives it as it outlives any order move (ADR-0011's note of 2026-10-07). The caller
    /// reads the state again before it goes on.
    /// Answers false when the source or the new state was refused; the failure is reported as a
    /// source's is, and the write is not made.
    /// </summary>
    private async Task<bool> TakeInGatheredAsync()
    {
        if (_disposed)
            return false;
        if (Source is not { } source)
            return true;
        _gatheredHeard = false;
        try
        {
            _askingGathered = true;
            try
            {
                source.PublishGathered();
            }
            finally
            {
                _askingGathered = false;
            }
            var heard = _gatheredHeard;
            _gatheredHeard = false;
            // A source that moved without saying so inside the call is taken in all the same.
            if (!heard && !SourceMovedOn(source))
                return true;
            ApplyState();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
            return false;
        }
        // The newest version is painted as well as handled: a press heard through a row's own
        // handler renders that row, not this root.
        _suppressRender = false;
        StateHasChanged();
        return !_disposed;
    }

    // Set while the part of a Consumer's write handler that runs before its first await runs (a paste,
    // a fill, a clear): a source it changes row by row raises StateChanged at each row, on this
    // thread, and taking each in re-laid the grid out once a row, which for a paste of 400 long cells
    // on WebAssembly was seconds (widened-run ticket 05). The changes are taken in once instead, by
    // the write's own OwnWriteAnsweredAsync, before the next gesture is handled. A change raised from
    // another thread, or after the handler's first await, is taken in as ever. The thread the handler
    // runs on while it does; 0 for none.
    private int _handlingWriteOn;

    /// <summary>Runs a Consumer's handler of the grid's own write intent, noting rather than answering
    /// the changes it makes to the bound source before its first await.</summary>
    private Task HandleWriteAsync(Func<Task> handler)
    {
        var outer = _handlingWriteOn;
        _handlingWriteOn = Environment.CurrentManagedThreadId;
        try
        {
            return handler();
        }
        finally
        {
            _handlingWriteOn = outer;
        }
    }

    /// <summary>Whether the source's state is not what the grid last took in.</summary>
    private bool SourceMovedOn(IGridSource<TRow> source)
        => !ReferenceEquals(source.Window, _window) || source.WindowStart != _windowStart
           || source.TotalCount != _total || source.IsLoading != _loading
           || source.RowSequenceVersion != _sequenceVersion;
}
