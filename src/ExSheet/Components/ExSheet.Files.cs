using System.Runtime.ExceptionServices;
using ExSheet.Engine;

namespace ExSheet.Components;

/// <summary>
/// Opening and saving a file in the Input Order (ADR-0330 §10). The application chooses the file and
/// delivers the written one (ADR-0330 §2): ExSheet references no file format, and takes a Workbook
/// Document still being read, or lends the Workbook to a writer, each at its own place in the order. An
/// opening holds the inputs behind it until the document arrives, so a key typed meanwhile lands on the
/// Workbook it opened, whatever the file's size; it is said while under way, and an Escape typed against a
/// page that shows it cancels it, under the exception ADR-0240 §12 accepts for a paste.
/// </summary>
public partial class ExSheet
{
    private Func<bool, Task>? _openingCameTooLate;

    // Set once the Sheet is disposed: an opening still under way neither lands nor raises anything.
    private bool _sheetDisposed;

    /// <summary>
    /// Replaces the Workbook with the Workbook Document <paramref name="read"/> answers, taken at this
    /// call's place in the Input Order (ADR-0330 §10): the inputs made before it are taken against the
    /// Workbook they were made on, and the inputs made after it wait until the document arrives and are
    /// then taken against the new one, or refused aloud when what they were taken against no longer holds
    /// (a paste aimed at the old cells, ADR-0142). Call it from the handler that hears the user's choice of
    /// a file — an <c>InputFile</c>'s change — with the read still to do, such as
    /// <c>token =&gt; XlsxWorkbookReader.ReadAsync(stream, culture, null, token)</c> from <c>ExSheet.Xlsx</c>.
    /// It may be called from any thread.
    ///
    /// <para>In its turn, an edit still open refuses it by name, as a save is refused, and nothing is read:
    /// what is being typed would be thrown away. From this call until the opening ends — waiting behind
    /// earlier inputs or being read — the Sheet's grid names it in its Settled Stamp
    /// (<c>data-ex-work-placed</c>), and a plain Escape typed in the Sheet against a page that shows it
    /// cancels it: <paramref name="read"/>'s token is cancelled, the Workbook stays as it was, and the notice
    /// says so; an opening cancelled before its turn ends cancelled, even if an edit was opened meanwhile.
    /// An Escape typed against a page that did not yet show the opening is an ordinary key: what it was typed
    /// against decides it, not when it arrives. Once the opening's turn has started and while the read is
    /// under way, the Sheet says so in its notice and its grid is busy and painted as a paste under way is. A read that fails — a file the reader refuses by name — leaves the Workbook as it was
    /// too, says so with the failure's message, and the failure is this method's. When the document arrives
    /// it replaces the whole Workbook, as a different <see cref="Workbook"/> does (ADR-0048): the undo stack
    /// is cleared, the Linked Tables the application declared are gone with the Workbook that held them,
    /// and <see cref="WorkbookChanged"/> (and <see cref="DocumentChanged"/> while it holds one Sheet) is
    /// raised with the Workbook opened. An Escape that comes once it has landed changes nothing, and the
    /// Sheet says that it came too late; one that finds the opening over otherwise says there was nothing to
    /// cancel.</para>
    ///
    /// <para>Only the Sheet's inputs wait behind it. A method the application calls meanwhile —
    /// <see cref="UndoAsync"/>, a Linked Table pushed, a parameter set — is not an input and is not held:
    /// what it changes is replaced when the file lands. Once the Sheet is disposed, the read's token is
    /// cancelled and nothing lands.</para>
    /// </summary>
    /// <param name="read">Reads the document, looking at the token as it goes; a read that sees it
    /// cancelled ends in <see cref="OperationCanceledException"/>.</param>
    /// <returns>A task answering <see langword="true"/> once the document opened, and
    /// <see langword="false"/> when Escape cancelled the read, or the Sheet was disposed first.</returns>
    /// <exception cref="SheetRefusedException">An edit is open (<see cref="IsEditing"/>); nothing was read.</exception>
    /// <exception cref="SheetDocumentException">The document is one the engine does not read; nothing opened.</exception>
    /// <exception cref="Exception">Whatever <paramref name="read"/> failed with, such as
    /// <c>XlsxOpenException</c>, or an <c>IOException</c> for a file over the size its stream allows; nothing
    /// opened. Or whatever a <see cref="WorkbookChanged"/> or <see cref="DocumentChanged"/> handler failed
    /// with, once the file has opened.</exception>
    public Task<bool> OpenAsync(Func<CancellationToken, Task<WorkbookDocument>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        _openingCameTooLate ??= landed =>
        {
            if (_sheetDisposed) return Task.CompletedTask;
            Tell(landed ? SheetWords.OpeningAlreadyLanded : SheetWords.OpeningNothingToCancel);
            StateHasChanged();
            return Task.CompletedTask;
        };
        return OnDispatcherAsync(async () =>
        {
            if (_sheetDisposed) return false;
            // Landing is tracked here, not read from how the work ended: what a handler of the application's
            // throws once the file has landed is that handler's, and never reads as a cancel.
            var landed = false;
            Exception? afterLanding = null;
            async Task Opening(CancellationToken token)
            {
                if (await OpeningAsync(read, token) is null) return;
                landed = true;
                try
                {
                    await RaiseDocumentsAsync();
                }
                catch (Exception failed)
                {
                    afterLanding = failed;
                }
            }
            if (_grid is { } grid)
                await grid.CancellableInOrderAsync(Opening, _openingCameTooLate);
            else
                // With no grid yet there is nothing to order it among, and nothing to cancel it from.
                await Opening(CancellationToken.None);
            if (afterLanding is not null)
                ExceptionDispatchInfo.Throw(afterLanding);
            return landed;
        });
    }

    /// <summary>
    /// Saves the Workbook at this call's place in the Input Order (ADR-0330 §10): <paramref name="write"/>
    /// is handed the Workbook as the inputs made before the call left it — every Sheet with its Values on
    /// screen, its Cell Formats and widths, and the rows each Linked Table holds — such as
    /// <c>workbook =&gt; XlsxWorkbookWriter.WriteAsync(workbook, stream).AsTask()</c> from
    /// <c>ExSheet.Xlsx</c>. The Workbook is lent for that call only: read it before the call's first
    /// <c>await</c>, as <c>XlsxWorkbookWriter</c> does, and never change it. The inputs made after the save
    /// are taken once the call has returned its task, not once the task ends, so a slow write holds nobody
    /// up, and what is read after that first <c>await</c> may already have changed. It may be called from
    /// any thread.
    /// </summary>
    /// <param name="write">Reads the Workbook, and writes it where the application wants it.</param>
    /// <returns>A task that completes once <paramref name="write"/>'s task has.</returns>
    /// <exception cref="SheetRefusedException">An edit is open (<see cref="IsEditing"/>): the file would
    /// quietly lack what is being typed. The Sheet says so, and nothing was written.</exception>
    /// <exception cref="Exception">Whatever <paramref name="write"/> failed with, such as
    /// <c>XlsxSaveException</c>.</exception>
    public Task SaveAsync(Func<global::ExSheet.Engine.Workbook, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return InvokeAsync(async () =>
        {
            Task writing = Task.CompletedTask;
            await ConsumerInOrderAsync(() =>
            {
                RefuseFileWhileEditing(SheetWords.SaveWhileEditing);
                writing = write(CurrentWorkbook);
                return Task.CompletedTask;
            });
            await writing;
        });
    }

    /// <summary>Work of the application's, through the grid's seam for work no control numbered: after
    /// every input received so far. With no grid yet there is nothing to order it among.</summary>
    private Task ConsumerInOrderAsync(Func<Task> handling) => _grid is { } grid ? grid.InOrderAsync(handling) : handling();

    /// <summary>A file opened or saved while an edit is open is refused by name, and said (ADR-0330 §10, ADR-0048).</summary>
    private void RefuseFileWhileEditing(string sentence)
    {
        if (!IsEditing) return;
        Tell(sentence);
        StateHasChanged();
        throw new SheetRefusedException(new SheetRefusal(SheetRefusalReason.EditIsOpen, sentence));
    }

    /// <summary>The opening, in its turn: refused while an edit is open; else said, read, and landed —
    /// answering the Workbook that landed — or left as it was.</summary>
    private async Task<global::ExSheet.Engine.Workbook?> OpeningAsync(Func<CancellationToken, Task<WorkbookDocument>> read, CancellationToken token)
    {
        if (_sheetDisposed) return null;
        // An Escape that came while the opening waited for its turn has cancelled it already: it ends
        // cancelled, whatever its turn finds — an edit opened meanwhile refuses nothing that was cancelled.
        if (token.IsCancellationRequested)
        {
            Tell(SheetWords.OpeningCancelled);
            StateHasChanged();
            token.ThrowIfCancellationRequested();
        }
        RefuseFileWhileEditing(SheetWords.OpenWhileEditing);
        Tell(SheetWords.Opening);
        StateHasChanged();
        WorkbookDocument? document;
        try
        {
            document = await read(token);
            if (document is null) throw new InvalidOperationException("The read answered no Workbook Document.");
            // The point past which an Escape finds the file opened.
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_sheetDisposed)
            {
                Tell(SheetWords.OpeningCancelled);
                StateHasChanged();
            }
            throw;
        }
        catch (Exception refused)
        {
            if (!_sheetDisposed)
            {
                Tell(SheetWords.OpeningRefused(refused.Message));
                StateHasChanged();
            }
            throw;
        }
        if (_sheetDisposed) return null;
        global::ExSheet.Engine.Workbook opened;
        try
        {
            // Read before anything is replaced: a document the engine refuses leaves the Workbook as it was.
            opened = global::ExSheet.Engine.Workbook.Open(document);
        }
        catch (Exception refused)
        {
            Tell(SheetWords.OpeningRefused(refused.Message));
            StateHasChanged();
            throw;
        }
        await LandAsync(opened);
        return opened;
    }

    /// <summary>
    /// The opened Workbook replaces the one shown (ADR-0048, ADR-0330 §10): an edit still open — one opened
    /// outside the order since the opening's turn began — is discarded first and said, so that it is never
    /// committed into the new Workbook's cell at the same place; then the Workbook, its undo stack and its
    /// views are replaced under a new Row Sequence Version, which drops the Selection and refuses what was
    /// aimed at the old cells (ADR-0011, ADR-0142). The application hears it afterwards (OpenAsync).
    /// </summary>
    private async Task LandAsync(global::ExSheet.Engine.Workbook opened)
    {
        var discarded = false;
        if (IsEditing && _grid is { } grid)
        {
            _ownDiscard = SheetWords.EditDiscardedByOpening;
            discarded = await grid.DiscardEditAsync(_ownDiscard);
            if (!discarded) _ownDiscard = null;
        }
        Open(opened);
        _sequence++;
        Tell(discarded ? SheetWords.EditDiscardedByOpening : SheetWords.Opened);
        StateHasChanged();
    }
}
