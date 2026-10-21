using ExGrid;
using ExGrid.Chrome;
using ExGrid.Finding;
using ExGrid.Selection;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExSheet.Components;

/// <summary>
/// Find and Replace on a Sheet (ADR-0332 §2 and §5, ADR-0331 §8). ExSheet is its grid's Consumer for them
/// as for everything else (ADR-0046): it answers each Find step over the Sheet shown or over the Workbook,
/// answers the grid's Replace Request over the Sheet shown, judges the painted cells for the Match Mark by
/// its own rule (<see cref="SheetFind.Matches"/>), and draws the find panel's contents itself, through the
/// grid's own Chrome seam, adding Within. Replace is the grid's: the current match written as an ordinary
/// edit (an Edit Intent, ExSheet's <c>OnEdit</c>), one step on the undo stack. Replace All over the Sheet
/// is the grid's Replace Request, which ExSheet performs as one step; over the Workbook, which the grid's
/// request cannot name, ExSheet performs it itself, in its place in the Input Order, as one step too.
/// What Replace matches is the Entry — a Formula's text — and Find the text shown (ADR-0332 §2).
/// </summary>
public partial class ExSheet
{
    // Within, as the panel last chose it: kept across a close and reopen, as the grid keeps the text.
    private SheetFindWithin _findWithin = SheetFindWithin.Sheet;

    // The Match Mark's rule (ADR-0332 §4): the one a step finds by, so a mark is never a match no step reaches.
    private static readonly FindMatchOf<SheetRow> FindMatchRule =
        static (row, column, pattern) => SheetFind.Matches(row.Sheet, new CellAddress(row.Index, SheetColumns.IndexOf(column)), pattern);

    private Func<GridReplaceRequest, Task<GridReplaceResult>>? _replace;
    private EventCallback<FindRefusalReason> _findRefused;

    // The Chrome the grid is handed: the Consumer's, with the find panel ExSheet's own.
    private SheetGridChrome? _gridChrome;

    // What the find panel holds, as last told by the grid's context or the panel since: read by a Replace All
    // over the Workbook, which the grid does not perform.
    private FindContext? _findContext;
    private string _findTextSeen = "";
    private bool _findMatchCaseSeen;
    private bool _findWholeCellSeen;
    private string _replaceTextSeen = "";

    // Every answer that changes what the panel says moves this on: a step asked, a refusal the grid raised, the
    // grid's Replace Request. ExSheet's own Replace All's outcome is said while nothing has moved it since, and
    // the panel has not been opened again.
    private int _findEvents;
    private OwnFindOutcome? _ownFindOutcome;

    private sealed record OwnFindOutcome(FindOutcome Outcome, int Count, IReadOnlyList<FindPanelRefusedCell> Cells, int Events, int FocusRequest);

    // Delegates handed into the panel's context, made once (ADR-0003).
    private Func<SheetFindWithin, Task>? _withinChanged;
    private Func<string>? _findOutcomeText;
    private Func<FindOutcome>? _findOutcome;
    private Func<string, string>? _findWords;
    private Action<string>? _findTextChanged;
    private Action<bool>? _findMatchCaseChanged;
    private Action<bool>? _findWholeCellChanged;
    private Action<string>? _replaceTextChanged;
    private Func<Task>? _replaceAll;

    /// <summary>The grid's Chrome: the Consumer's, its find panel ExSheet's (ADR-0332 §5). One instance per
    /// Consumer Chrome, so the grid sees no change of Chrome from render to render.</summary>
    private IGridChrome GridChrome => _gridChrome is { } held && ReferenceEquals(held.Inner, Chrome)
        ? held
        : _gridChrome = new SheetGridChrome(this, Chrome);

    /// <summary>Wires Find and Replace's answers, made once with the component.</summary>
    private void InitialiseFind()
    {
        _replace = AnswerReplaceAsync;
        _findRefused = new EventCallback<FindRefusalReason>(null, (Action<FindRefusalReason>)(_ => _findEvents++));
        // A choice of Within is a click taken in its place in the Input Order (ADR-0240): a step numbered
        // before it searches where the panel searched then, one numbered after it where it searches now.
        _withinChanged = within => InOrderAsync(GridInputKind.Click, () =>
        {
            _findWithin = within;
            _ownFindOutcome = null;
        });
        _findOutcomeText = FindOutcomeText;
        _findOutcome = () => FindSaid()?.Outcome ?? FindOutcome.None;
        _findWords = id => WordsTheGridSaid(id);
    }

    /// <summary>
    /// One Find step (ADR-0055, ADR-0332 §5): over the Sheet shown, or over the Workbook. A match on the Sheet
    /// shown is the answer. A match on another Sheet shows that Sheet with the cell selected and taken as the
    /// current match, handed to the grid with the Sheet's rows, and the step is given up: its answer could
    /// name only a place among the rows it was asked about. While an edit is open no other Sheet is shown,
    /// and the notice says why.
    /// </summary>
    private async Task<GridFindResult> AnswerFindAsync(GridFindRequest request)
    {
        _findEvents++;
        // A Sheet's rows are places, so a step on another Row Sequence Version names nothing here (ADR-0046).
        if (request.RowSequenceVersion != _sequence || _sheet is not { } shown)
            return GridFindResult.NotFound;
        if (_findWithin == SheetFindWithin.Sheet || _workbook is not { } workbook)
            return SheetFind.Step(shown, request);
        if (SheetFind.StepWorkbook(workbook, shown, request) is not { } found)
            return GridFindResult.NotFound;
        if (found.Sheet == shown)
            return GridFindResult.Found(found.Row, request.Columns[found.Position]);
        if (RefuseSwitchWhileEditing())
            throw new OperationCanceledException("The match is on another Sheet, which is not shown while an edit is open.");
        ClearNotice();
        if (ShowSheet(found.Sheet, (default, new CellPosition(found.Row, found.Position)), asMatch: true))
            await RaiseDocumentsAsync();
        throw new OperationCanceledException("The match is on another Sheet, now shown with it selected.");
    }

    /// <summary>
    /// The grid's Replace Request (ADR-0332 §3): Replace All over the Sheet shown, inside the Selection when
    /// more than one cell is selected, matched on each cell's Entry. All or nothing: a replaced Formula the
    /// engine cannot read refuses it all, naming every such cell. Otherwise one step on the undo stack, as
    /// typing the replaced Entries would be, and the count.
    /// </summary>
    private async Task<GridReplaceResult> AnswerReplaceAsync(GridReplaceRequest request)
    {
        _findEvents++;
        if (request.RowSequenceVersion != _sequence || _sheet is not { } shown)
            return GridReplaceResult.Replaced(0, _sequence);
        // Answered under the version it was checked in, which the write does not move: a handler of the
        // Consumer's that showed other rows meanwhile cannot turn a write that happened into "order changed".
        var version = request.RowSequenceVersion;
        var positions = SheetFind.PositionsOf(request.Columns);
        var plan = SheetFind.PlanReplace([shown], request.Pattern, request.Replacement, positions, request.Scope);
        if (plan.Refused.Count > 0)
        {
            return GridReplaceResult.Refused(
                [.. plan.Refused.Select(cell => new GridReplaceRefusedCell(cell.Address.Row, request.Columns[cell.Position], cell.Reason))],
                version);
        }
        if (plan.Writes.Count > 0 && await WriteReplacementsAsync(plan.Writes) is { } refused)
        {
            return GridReplaceResult.Refused(
                [.. plan.Writes.Select(write => new GridReplaceRefusedCell(write.Address.Row, request.Columns[positions[write.Address.Column]], refused))],
                version);
        }
        return GridReplaceResult.Replaced(plan.Writes.Count, version);
    }

    /// <summary>
    /// Replace All over the Workbook (ADR-0332 §5), from the panel's button in its place in the Input Order:
    /// every cell of every Sheet whose Entry matches, all or nothing, as one step on the undo stack — refused
    /// whole when any replaced Formula cannot be read, naming each such cell by its Sheet — and the count
    /// said. With nothing matched, the panel says no match.
    /// </summary>
    private async Task ReplaceAllInWorkbookAsync()
    {
        _findEvents++;
        if (_findContext is not { ReplaceShown: true } context || _findTextSeen.Length == 0 || _workbook is not { } workbook)
            return;
        if (IsEditing || _grid?.ReadEditText() is not null)
        {
            Tell(SheetWords.EditIsOpen);
            StateHasChanged();
            return;
        }
        // The write moves the Focus nowhere, and no match is current after it, as after the grid's own
        // Replace All (ADR-0332 §4): the next Replace finds one before it writes.
        _grid?.ForgetCurrentMatch();
        var pattern = new GridFindPattern(_findTextSeen, _findMatchCaseSeen, _findWholeCellSeen);
        var plan = SheetFind.PlanReplace(workbook.Sheets, pattern, _replaceTextSeen);
        if (plan.Refused.Count > 0)
        {
            SayOwnFindOutcome(FindOutcome.ReplaceRefused, 0,
                [.. plan.Refused.Select(cell => new FindPanelRefusedCell(cell.Address.Row + 1,
                    SheetQualifier(cell.Sheet) + CellAddress.ColumnName(cell.Address.Column), cell.Reason))],
                context.FocusRequest);
            return;
        }
        if (plan.Writes.Count == 0)
        {
            SayOwnFindOutcome(FindOutcome.NotFound, 0, [], context.FocusRequest);
            return;
        }
        ClearNotice();
        if (await WriteReplacementsAsync(plan.Writes) is { } refused)
        {
            SayOwnFindOutcome(FindOutcome.ReplaceRefused, 0,
                [.. plan.Writes.Select(write => new FindPanelRefusedCell(write.Address.Row + 1,
                    SheetQualifier(write.Sheet) + CellAddress.ColumnName(write.Address.Column), refused))],
                context.FocusRequest);
            return;
        }
        SayOwnFindOutcome(FindOutcome.Replaced, plan.Writes.Count, [], context.FocusRequest);
    }

    /// <summary>How a Reference names <paramref name="sheet"/> before its cell, quoted where its name needs it: <c>Sheet2!</c>, <c>'Q1 Sales'!</c>.</summary>
    private static string SheetQualifier(Sheet sheet)
    {
        var a1 = FormulaEntry.ReferenceText(new CellRange(new CellAddress(0, 0)), sheet.Name);
        return a1[..^"A1".Length];
    }

    private void SayOwnFindOutcome(FindOutcome outcome, int count, IReadOnlyList<FindPanelRefusedCell> cells, int focusRequest)
    {
        _ownFindOutcome = new OwnFindOutcome(outcome, count, cells, _findEvents, focusRequest);
        StateHasChanged();
    }

    /// <summary>
    /// The replaced Entries, typed into their cells on whichever Sheets they lie, as one operation: one engine
    /// step across the Sheets (one change, ADR-0180), with the widths a typed number widens its column to, as
    /// typing it would (ADR-0047), one step on the undo stack (ADR-0048). One the engine refuses — a Formula it
    /// cannot read after all, a Sheet that refuses the edit — writes nothing: it is said in the Sheet's notice,
    /// as a typed one is, and its words are returned for the panel; null once written.
    /// </summary>
    private async Task<string?> WriteReplacementsAsync(IReadOnlyList<SheetReplaceWrite> writes)
    {
        var workbook = CurrentWorkbook;
        var bySheet = writes.GroupBy(write => write.Sheet).ToList();
        var edits = bySheet
            .Select(group => (group.Key.Name, SheetEdit.Enter(group.Select(write => new KeyValuePair<CellAddress, string>(write.Address, write.Text)))))
            .ToList();
        WorkbookStep step;
        try
        {
            step = workbook.Do(edits);
        }
        catch (FormulaSyntaxException error)
        {
            var words = SheetWords.FormulaUnreadable(error);
            Tell(words);
            return words;
        }
        catch (SheetRefusedException refused)
        {
            var words = SheetWords.Refused(refused.Refusal);
            Tell(words);
            return words;
        }
        var steps = new List<WorkbookStep> { step };
        var metrics = CellMetrics;
        foreach (var group in bySheet)
        {
            var sheet = group.Key;
            var columns = sheet == _sheet ? _columns : SheetColumnList.Of(sheet, metrics);
            var cells = group.Select(write => new CellRange(write.Address)).ToList();
            foreach (var widened in SheetWidening.Of(sheet, cells, columns, metrics))
                steps.Add(workbook.Do(sheet.Name, WidenedByEntry(widened.Column, widened.Characters)));
        }
        _history.Record(steps);
        await ChangedAsync([.. steps.Select(s => s.Change)]);
        return null;
    }

    /// <summary>What the panel says now: ExSheet's own Replace All's outcome while it stands, else the grid's
    /// last step's, in the panel's words.</summary>
    private string FindOutcomeText()
        => FindSaid() is { } said ? FindPanelLabelIds.OutcomeText(said, _findWords!) : "";

    /// <summary>The grid's find context with ExSheet's own Replace All's outcome in it while that stands.</summary>
    private FindContext? FindSaid()
    {
        if (_findContext is not { } context)
            return null;
        return _ownFindOutcome is { } own && own.Events == _findEvents && own.FocusRequest == context.FocusRequest
            ? context with { Outcome = own.Outcome, ReplacedCount = own.Count, ReplaceRefusedCells = own.Cells }
            : context;
    }

    /// <summary>The find panel's contents (ADR-0332 §5): the Chrome's, when it draws a Sheet's, else the built-in
    /// one; asked by the grid in place of its own.</summary>
    private RenderFragment FindPanelFor(FindContext context)
    {
        _findContext = context;
        _findTextSeen = context.Text;
        _findMatchCaseSeen = context.MatchCase;
        _findWholeCellSeen = context.WholeCell;
        _replaceTextSeen = context.ReplaceText;
        var find = context with
        {
            TextChanged = _findTextChanged ??= text =>
            {
                _findTextSeen = text;
                _findContext?.TextChanged(text);
            },
            MatchCaseChanged = _findMatchCaseChanged ??= value =>
            {
                _findMatchCaseSeen = value;
                _findContext?.MatchCaseChanged(value);
            },
            WholeCellChanged = _findWholeCellChanged ??= value =>
            {
                _findWholeCellSeen = value;
                _findContext?.WholeCellChanged(value);
            },
        };
        if (context.ReplaceShown)
        {
            find = find with
            {
                ReplaceTextChanged = _replaceTextChanged ??= text =>
                {
                    _replaceTextSeen = text;
                    _findContext?.ReplaceTextChanged?.Invoke(text);
                },
                // Over the Sheet, the grid's Replace Request; over the Workbook, ExSheet's own, in the Input Order.
                ReplaceAll = _replaceAll ??= () => _findWithin == SheetFindWithin.Workbook
                    ? InOrderAsync(GridInputKind.Click, ReplaceAllInWorkbookAsync)
                    : _findContext?.ReplaceAll?.Invoke() ?? Task.CompletedTask,
            };
        }
        var sheetContext = new SheetFindContext(find, _findWithin, _withinChanged!, _findOutcomeText!, _findOutcome!, _findWords!);
        return (Chrome as ISheetChrome)?.SheetFindPanel(sheetContext) ?? BuiltInFindPanel(sheetContext);
    }

    private static RenderFragment BuiltInFindPanel(SheetFindContext context) => builder =>
    {
        builder.OpenComponent<SheetFindPanel>(0);
        builder.AddComponentParameter(1, nameof(SheetFindPanel.Context), context);
        builder.CloseComponent();
    };

    /// <summary>
    /// The Chrome a Sheet hands its grid: every seam the Consumer's Chrome's, or the built-in one's where it
    /// has none, except the find panel, which is ExSheet's (ADR-0332 §5). Asked through the interface, so a
    /// seam the Consumer's Chrome leaves to the interface's default answers that default.
    /// </summary>
    private sealed class SheetGridChrome(ExSheet sheet, IGridChrome? inner) : IGridChrome
    {
        internal IGridChrome? Inner { get; } = inner;

        public RenderFragment? FilterPanel(FilterPanelContext context) => Inner?.FilterPanel(context);

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => Inner?.ColumnMenu(context);

        public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context) => Inner?.ContextMenu(context);

        public RenderFragment? CellMessage(CellMessageContext context) => Inner?.CellMessage(context);

        public RenderFragment? CellEditor(CellEditorContext context) => Inner?.CellEditor(context);

        public RenderFragment? FindPanel(FindContext context) => sheet.FindPanelFor(context);

        public RenderFragment? LoadingIndicator(LoadingContext context) => Inner?.LoadingIndicator(context);

        public RenderFragment? NameBox(NameBoxContext context) => Inner?.NameBox(context);

        public RenderFragment? FormulaBarText(FormulaBarTextContext context) => Inner?.FormulaBarText(context);

        public RenderFragment? EditorCompletion(EditorCompletionContext context) => Inner?.EditorCompletion(context);

        public RenderFragment? SelectionSummary(SelectionSummaryContext context) => Inner?.SelectionSummary(context);

        public IReadOnlyList<ChromeStylesheet> Stylesheets => Inner?.Stylesheets ?? [];

        public string? Words(string id) => Inner?.Words(id);
    }
}
