using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// Before a write is handled, the bound source puts out what it has gathered, so the write is made on,
// and carries, the newest version (ADR-0141/0142, D5 of 2026-10-06; LV-16).
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
    /// newest version. A change gathered while the user typed is brought forward by the gesture, as
    /// in ExPivot (ADR-0067), never waited for. Without a source, or with nothing gathered, nothing
    /// happens. A gathered change that moved the order drops the Selection (ADR-0011); an open edit
    /// is discarded as any such change discards it, unless a commit is answering the move itself
    /// (<see cref="EditOutlivesOrderMove"/>). The caller reads the state again before it goes on.
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

    /// <summary>Whether the source's state is not what the grid last took in.</summary>
    private bool SourceMovedOn(IGridSource<TRow> source)
        => !ReferenceEquals(source.Window, _window) || source.WindowStart != _windowStart
           || source.TotalCount != _total || source.IsLoading != _loading
           || source.RowSequenceVersion != _sequenceVersion;
}
