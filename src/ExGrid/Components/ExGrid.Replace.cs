using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

// Replace (ADR-0332 §2 and §3): Ctrl+H opens the find panel with a Replace field and two writes. The
// grid writes nothing itself. Replace is one ordinary write of the current match — an Edit Intent, with
// its Edit Verdict — and then a Find step to the next match. Replace All is one Replace Request over
// every match, which only the Consumer can reach (ADR-0001): it performs the whole write, all or
// nothing and uncapped, and answers with the count or the cells it could not write, under the Row
// Sequence Version it read its rows in; an answer under another version is refused as the order
// having moved (ADR-0011). Both buttons, and the Chrome's calls, are taken in the Input Order
// (ADR-0240), as Find's steps are.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// The Replace Request (ADR-0332 §3): Replace All, asked of the Consumer, who performs it and
    /// answers. Ctrl+H opens the find panel with Replace only while this has a delegate, a visible
    /// column is Editable and a Find is wired (a Source that finds, or <see cref="OnFind"/>); otherwise
    /// the key is still taken, opens nothing and raises <see cref="OnFindRefused"/> with
    /// <see cref="FindRefusalReason.ReplaceUnavailable"/>, as Ctrl+F is refused where no search is
    /// wired.
    ///
    /// <para>The request carries Find's text and two options, the replacement, the selection as the
    /// scope when more than one cell is selected, the visible columns in their current order, which of
    /// them are Editable, and the Row Sequence Version they were read in. <b>What is matched is each
    /// cell's entry</b> — the text the Cell Editor would open with — not the displayed text Find
    /// matches. The Consumer writes every match or none: if any cannot be written (a column not
    /// Editable, a value it cannot hold), it writes nothing and answers
    /// <see cref="GridReplaceResult.Refused"/> naming every such cell; otherwise it writes them all as
    /// one step of its undo stack and answers <see cref="GridReplaceResult.Replaced"/> with the count.
    /// There is no cap. It answers under the version it read its rows in, and an answer under another
    /// version than the request's is refused as <see cref="FindRefusalReason.OrderChanged"/>, the
    /// Consumer having written nothing. The panel says the count, or names the cells and why.</para>
    ///
    /// <para><see cref="InMemoryGridSource{TRow}.Replace"/> answers it for <c>GridSource.From</c>, and
    /// <see cref="GridReplace.Plan{TRow}"/> is the reference for what is replaced. It is a write, so it
    /// is raised beside a bound <see cref="Source"/>, as <see cref="OnEdit"/> is. Replace — the current
    /// match alone — is not raised here: it is an ordinary <see cref="OnEdit"/>, and is refused and said
    /// where no <see cref="OnEdit"/> is wired. Like
    /// <see cref="OnFind"/> it is a function that answers, not an event callback, so the Consumer's own
    /// component is not rendered after it: a page that shows something of the answer itself — a
    /// message, an Undo button — calls <c>StateHasChanged</c>.</para>
    /// </summary>
    [Parameter] public Func<GridReplaceRequest, Task<GridReplaceResult>>? OnReplace { get; set; }

    // Whether the panel was opened for Replace (Ctrl+H), and what its Replace field holds — kept across
    // a close and reopen within the instance, as the find text is.
    private bool _findReplace;
    private string _replaceText = "";

    // What the last Replace All replaced, or the cells the last Replace could not write.
    private int _replacedCount;
    private IReadOnlyList<FindPanelRefusedCell> _replaceRefused = [];

    /// <summary>Whether a Replace can land here (ADR-0332 §2): someone handles the Replace Request, a
    /// visible column is Editable, and a Find is wired, which Replace's next match is.</summary>
    private bool CanReplace() => OnReplace is not null && AnyColumnEdits() && CanFind();

    /// <summary>Whether the open panel offers Replace now: opened by Ctrl+H, and still able to land
    /// one.</summary>
    private bool ReplaceShown => _findOpen && _findReplace && CanReplace();

    /// <summary>
    /// Ctrl+H (ADR-0332 §2): the find panel with Replace, as Ctrl+F opens it without — or, where no
    /// Replace can land, a refusal and nothing opened, as Ctrl+F's where nothing can search.
    /// </summary>
    private async Task OpenReplaceAsync()
    {
        if (!CanReplace())
        {
            if (OnFindRefused.HasDelegate)
                await OnFindRefused.InvokeAsync(FindRefusalReason.ReplaceUnavailable);
            return;
        }
        await OpenFindAsync();
        _findReplace = _findOpen;
    }

    /// <summary>
    /// Replace (ADR-0332 §2): the current match written with the replacement, as one Edit Intent with
    /// its Edit Verdict, and then the next match found. The current match is the cell the last step
    /// found while the Focus stays there; with none, this finds the next one, so the user sees what
    /// the next press writes. A current match whose entry no longer matches — the cell changed, or
    /// its entry is not what it shows — is passed over to the next. One that cannot be written — its
    /// column not Editable, a Reject, the Consumer's refusal — writes nothing and is said, and stays
    /// the current match.
    /// </summary>
    private async Task ReplaceOneAsync()
    {
        if (!ReplaceShown || _findText.Length == 0)
            return;
        // The newest version the bound source holds, as every write is made on (ADR-0142 D5).
        if (!await TakeInGatheredAsync())
            return;
        ForgetCurrentMatchOffFocus();
        if (_findCurrent is not { } cell || RowInHand(cell.Row) is not { } row)
        {
            await FindStepAsync(backward: false);
            return;
        }
        var pattern = new GridFindPattern(_findText, _findMatchCase, _findWholeCell);
        if (GridReplace.Replaced(OpeningTextOf(cell), pattern, _replaceText) is not { } text)
        {
            await FindStepAsync(backward: false);
            return;
        }
        var column = Columns[cell.Column];
        // With no OnEdit nothing would hear the write: refused and said, never a step that looks
        // like it wrote.
        if (!ColumnIsEditable(cell.Column) || !OnEdit.HasDelegate)
        {
            await RefuseReplaceAsync([new FindPanelRefusedCell(cell.Row + 1, HeaderOf(column), null)]);
            return;
        }
        // The Consumer's judgement, as at a commit (ADR-0034): a Reject writes nothing.
        var verdict = column.Validate?.Invoke(row, text) ?? EditVerdict.Accept;
        if (verdict.Kind == EditVerdictKind.Reject)
        {
            await RefuseReplaceAsync([new FindPanelRefusedCell(cell.Row + 1, HeaderOf(column), verdict.Message)]);
            return;
        }
        // What the cell paints now is what the user sees as they press, and what the write
        // replaces (ADR-0142).
        var replaced = ReplacedTextAt(cell);
        var intent = new GridEditIntent<TRow>(row, column.Name, text)
        {
            SeenText = replaced,
            ReplacedText = replaced,
        };
        // The user's own write (D1), taken back if refused; the bound source puts out what it
        // gathered as the handler completes (D5).
        var written = NoteOwnWrite([SelectionRange.FromCorners(cell, cell)]);
        await HandleWriteAsync(() => OnEdit.InvokeAsync(intent));
        await OwnWriteAnsweredAsync(written, intent.IsRefused);
        if (_disposed)
            return;
        if (intent.RefusalMessage is { } refused)
        {
            await RefuseReplaceAsync([new FindPanelRefusedCell(cell.Row + 1, HeaderOf(column), refused)]);
            return;
        }
        AskSummaryAgain();
        await FindStepAsync(backward: false);
    }

    /// <summary>
    /// Replace All (ADR-0332 §3): one Replace Request, raised under the Row Sequence Version the
    /// positions in it were read in, which the Consumer performs and answers. The grid writes nothing:
    /// it says the count, or names the cells the Consumer could not write, and refuses an answer under
    /// another version than the request's as the order having moved (ADR-0011).
    /// </summary>
    private async Task ReplaceAllAsync()
    {
        if (!ReplaceShown || _findText.Length == 0 || OnReplace is not { } replace)
            return;
        if (!await TakeInGatheredAsync())
            return;
        // The write moves the Focus nowhere, and no match is current after it (ADR-0332 §4).
        _findCurrent = null;
        DropCurrentHeader();
        var selection = _selection.Selection;
        var names = new string[Columns.Count];
        var editable = new List<string>(Columns.Count);
        for (var i = 0; i < Columns.Count; i++)
        {
            names[i] = Columns[i].Name;
            if (Columns[i].Editable)
                editable.Add(Columns[i].Name);
        }
        var request = new GridReplaceRequest
        {
            Text = _findText,
            MatchCase = _findMatchCase,
            WholeCell = _findWholeCell,
            Replacement = _replaceText,
            Scope = !selection.IsEmpty && selection.CellCount > 1 ? selection.Ranges : null,
            Columns = names,
            EditableColumns = editable,
            RowSequenceVersion = _sequenceVersion,
        };

        // The answer belongs to the Source it was asked of: one replaced meanwhile by another instance
        // may count from 0 again under the same version, and its rows are not these (ADR-0142).
        var binding = _binding;
        GridReplaceResult? answer = null;
        await HandleWriteAsync(async () => answer = await replace(request));
        if (_disposed)
            return;
        // The bound source puts out what the write gathered before the next gesture (D5).
        await TakeInGatheredAsync();
        if (answer is null)
            throw new InvalidOperationException("ExGrid: OnReplace answered null; it answers with GridReplaceResult.Replaced or Refused (ADR-0332).");
        if (answer.RowSequenceVersion != request.RowSequenceVersion || binding != _binding)
        {
            await RefuseFindAsync(FindRefusalReason.OrderChanged);
            return;
        }
        if (!answer.IsReplaced)
        {
            var cells = new List<FindPanelRefusedCell>(answer.RefusedCells.Count);
            foreach (var cell in answer.RefusedCells)
            {
                var index = Array.IndexOf(names, cell.Column);
                if (index < 0 || cell.Row < 0)
                {
                    throw new InvalidOperationException(FormattableString.Invariant(
                        $"ExGrid: the Replace answer names row {cell.Row}, column '{cell.Column}', outside the request: it asked about the columns [{string.Join(", ", names)}] (ADR-0332)."));
                }
                cells.Add(new FindPanelRefusedCell(cell.Row + 1,
                    index < Columns.Count && Columns[index].Name == cell.Column ? HeaderOf(Columns[index]) : cell.Column,
                    cell.Message));
            }
            await RefuseReplaceAsync(cells);
            return;
        }
        if (answer.Count == 0)
        {
            await RefuseFindAsync(FindRefusalReason.NotFound);
            return;
        }
        _findOutcome = FindOutcome.Replaced;
        _replacedCount = answer.Count;
        AskSummaryAgain();
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>A Replace that wrote nothing (ADR-0332 §2): the panel names the cells and why, and the
    /// Consumer is told.</summary>
    private async Task RefuseReplaceAsync(IReadOnlyList<FindPanelRefusedCell> cells)
    {
        _replaceRefused = cells;
        _findOutcome = FindOutcome.ReplaceRefused;
        _suppressRender = false;
        StateHasChanged();
        if (OnFindRefused.HasDelegate)
            await OnFindRefused.InvokeAsync(FindRefusalReason.ReplaceRefused);
    }

    /// <summary>A column as the panel names it: its header, or its name where the header is blank.</summary>
    private static string HeaderOf(GridColumn<TRow> column)
        => string.IsNullOrWhiteSpace(column.Header) ? column.Name : column.Header;

    /// <summary>The panel's Replace part in its context, for a substituted Chrome: absent while it is
    /// not shown, so the Chrome draws Find alone.</summary>
    private FindContext WithReplace(FindContext context) => !ReplaceShown
        ? context with { ReplacedCount = _replacedCount, ReplaceRefusedCells = _replaceRefused, FoundHeader = FoundHeaderNow }
        : context with
        {
            FoundHeader = FoundHeaderNow,
            ReplaceShown = true,
            ReplaceText = _replaceText,
            ReplaceTextChanged = _replaceTextChanged ??= text => _replaceText = text,
            Replace = _replaceFromChrome ??= () => InvokeAsync(() => ClaimedAsync(ClaimKinds.Click, ClaimKinds.Submit, ReplaceOneAsync)),
            ReplaceAll = _replaceAllFromChrome ??= () => InvokeAsync(() => ClaimedAsync(ClaimKinds.Click, ClaimKinds.Submit, ReplaceAllAsync)),
            ReplacedCount = _replacedCount,
            ReplaceRefusedCells = _replaceRefused,
        };

    private Action<string>? _replaceTextChanged;
    private Func<Task>? _replaceFromChrome;
    private Func<Task>? _replaceAllFromChrome;

    // The built-in Replace field, written as the find field is (FindField): the value its own typing
    // outranks a render with, and its handler kept the same instance every render (PF-3).
    private RenderFragment ReplaceField => _replaceFieldFragment ??= ReplaceFieldBuilder;

    private void ReplaceFieldBuilder(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "text");
        builder.AddAttribute(2, "class", "ex-find-replace-field");
        builder.AddAttribute(3, "aria-label", Label(FindPanelLabelIds.ReplaceField));
        builder.AddAttribute(4, "value", _replaceText);
        builder.AddAttribute(5, "oninput", _replaceTextInput ??= EventCallback.Factory.Create<ChangeEventArgs>(
            this, e => _replaceText = e.Value as string ?? ""));
        builder.SetUpdatesAttributeName("value");
        builder.AddAttribute(6, "onkeydown", _findFieldKeyDown ??= EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs>(this, OnFindFieldKeyDown));
        builder.CloseElement();
    }

    private RenderFragment? _replaceFieldFragment;
    private EventCallback<ChangeEventArgs>? _replaceTextInput;
    private Func<Task>? _onReplaceOne;
    private Func<Task>? _onReplaceAll;

    private Func<Task> ReplaceOneHandler => _onReplaceOne ??= () => ClaimedAsync(ClaimKinds.Click, ReplaceOneAsync);

    private Func<Task> ReplaceAllHandler => _onReplaceAll ??= () => ClaimedAsync(ClaimKinds.Click, ReplaceAllAsync);
}
