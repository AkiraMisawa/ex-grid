using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// An Excel-like grid for Blazor, drawn by the Script Core (ADR-0350 section 7, ADR-0370 section 1): Blazor's
/// Framework Adapter over <c>@ex-grid/core</c>. The component renders one element, the grid's instance root,
/// mounts the Script Core on it, hands it its parameters as options and its source's Window as data, and raises
/// the grid's events as <see cref="EventCallback"/>s. It holds no state of the grid's, draws nothing inside the
/// root and decides nothing: keys, selection, copy and paste, the editor, the overflow decision and Auto width are
/// the Script Core's, in the browser, on both hosting models. The rows, their order and what an intent does to them
/// stay the Consumer's, in .NET, behind an <see cref="IGridSource{TRow}"/>.
/// </summary>
/// <remarks>
/// It stands beside <see cref="Components.ExGrid{TRow}"/>, the C# core, until every product that draws through
/// that one has moved (ADR-0350 section 7). What this adapter does not carry yet is refused by name as its
/// parameters are set, never left out quietly. Find is the source's: Ctrl+F asks
/// <see cref="IGridSource{TRow}.FindAsync"/> where the source <see cref="IGridSource{TRow}.CanFind"/>, and is refused as
/// a Find with nothing to answer it where it does not; Replace is not carried, so Ctrl+H is refused as Replace unavailable. The Selection
/// Summary, which no column declares, is not offered: no summary is drawn.
/// </remarks>
/// <typeparam name="TRow">The Consumer's row type.</typeparam>
public sealed class ExGrid<TRow> : ComponentBase, IAsyncDisposable
    where TRow : class
{
    /// <summary>The script's address under the application's base: the package's minified Script Core and bridge.</summary>
    private const string ScriptPath = "_content/ExGrid/ex-grid-core.min.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Where the rows come from (ADR-0001): the grid neither holds nor executes, and tells the source the
    /// columns, the Sort and the Filter. Exactly one of this and <see cref="Rows"/>.</summary>
    [Parameter] public IGridSource<TRow>? Source { get; set; }

    /// <summary>Rows held in memory, sorted and filtered by <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/>,
    /// the reference semantics (ADR-0001). A new list is a new source. Exactly one of this and <see cref="Source"/>.</summary>
    [Parameter] public IReadOnlyList<TRow>? Rows { get; set; }

    /// <summary>The columns, in display order. A new list holding the same columns is the same columns (ADR-0003).</summary>
    [Parameter, EditorRequired] public IReadOnlyList<GridColumn<TRow>> Columns { get; set; } = [];

    /// <summary>The density preset the Grid Metrics resolve from (ADR-0028); the core's Compact unless set.</summary>
    [Parameter] public GridDensity? Density { get; set; }

    /// <summary>A row's height in CSS pixels, through which every geometry is computed (ADR-0013); the density's unless set.</summary>
    [Parameter] public double? RowHeight { get; set; }

    /// <summary>The header band's height in CSS pixels; the density's unless set.</summary>
    [Parameter] public double? HeaderHeight { get; set; }

    /// <summary>The Viewport's height: pixels, or <see cref="ViewportSize.Stretch"/> to fill the parent (ADR-0028); the core's unless set.</summary>
    [Parameter] public ViewportSize? ViewportHeight { get; set; }

    /// <summary>The Viewport's width: pixels, or <see cref="ViewportSize.Stretch"/>; the core's unless set.</summary>
    [Parameter] public ViewportSize? ViewportWidth { get; set; }

    /// <summary>How many leading columns stay in place while the rest scroll (ADR-0007).</summary>
    [Parameter] public int PinnedColumnCount { get; set; }

    /// <summary>Whether only the columns in view are drawn (ADR-0004); on unless set off.</summary>
    [Parameter] public bool VirtualiseColumns { get; set; } = true;

    /// <summary>Whether every other row is striped (UX-15).</summary>
    [Parameter] public bool StripeRows { get; set; }

    /// <summary>Whether the Focus's row is tinted (ADR-0029).</summary>
    [Parameter] public bool HighlightFocusRow { get; set; }

    /// <summary>Whether the row under the pointer is tinted (ADR-0029).</summary>
    [Parameter] public bool HighlightHoverRow { get; set; }

    /// <summary>The widths the grid charges each glyph (ADR-0016), beating a Wrapper's and the Density's: a Consumer
    /// that sets the font the cells are drawn in hands that font's widths, so <c>####</c> and an Auto width are judged
    /// by the face on screen. The Density's unless set.</summary>
    [Parameter] public CellTextMetrics? CellMetrics { get; set; }

    /// <summary>What a Wrapper hands down (ADR-0030): the widths of the font its Theme sets, and the Density, hover band
    /// and Row Stripes its words map onto. Read only where the corresponding parameter was not written: an explicit
    /// parameter still wins, per value (ADR-0028), as on the C# core.</summary>
    [CascadingParameter] public GridPresentationDefaults? Presentation { get; set; }

    /// <summary>Whether the header's ▾ column menu is hidden (ADR-0044).</summary>
    [Parameter] public bool HideColumnMenu { get; set; }

    /// <summary>Whether the menus leave out Copy with headers (ADR-0036).</summary>
    [Parameter] public bool HideCopyWithHeaders { get; set; }

    /// <summary>Whether a click on a header selects its column (ADR-0050).</summary>
    [Parameter] public bool HeaderClickSelects { get; set; }

    /// <summary>Whether a paste larger than the Selection may write past it (ADR-0014). Off: it is refused.</summary>
    [Parameter] public bool PasteMaySpill { get; set; }

    /// <summary>Whether Find matches the headers too (ADR-0333); on unless set off.</summary>
    [Parameter] public bool FindsHeaders { get; set; } = true;

    /// <summary>The most cells a copy writes, past which it is refused (ADR-0005); the core's unless set.</summary>
    [Parameter] public long? CopyCellCap { get; set; }

    /// <summary>The most a paste reads, in UTF-8 bytes, past which it is refused (ADR-0005); the core's unless set.</summary>
    [Parameter] public long? PasteByteCap { get; set; }

    /// <summary>The header's groups, over the columns they name (ADR-0032).</summary>
    [Parameter] public IReadOnlyList<HeaderGroup>? HeaderGroups { get; set; }

    /// <summary>Hears the Selection each time it changes.</summary>
    [Parameter] public EventCallback<GridSelection> SelectionChanged { get; set; }

    /// <summary>
    /// Hears a Cell Editor's commit (ADR-0034): the Consumer writes the value, and the row it then shows is what the
    /// cell paints. Without it the grid edits nothing. The commit is pending until the handler completes (ADR-0380
    /// section 5): the editor stands with the text, and every input made meanwhile waits for it. A handler that calls
    /// <see cref="GridEditIntent{TRow}.Refuse"/> before it completes holds the editor open with its message, as a
    /// Reject does; the intent's <see cref="GridEditIntent{TRow}.CancellationToken"/> is cancelled when the user
    /// cancels the commit with Escape.
    /// </summary>
    [Parameter] public EventCallback<GridEditIntent<TRow>> OnEdit { get; set; }

    /// <summary>Hears a paste or a fill the grid planned (ADR-0014): the Consumer writes it. Without it the grid
    /// pastes nothing. As with <see cref="OnEdit"/>, the paste is pending until the handler completes, and one that
    /// calls <see cref="GridPasteIntent.Refuse"/> before then leaves the Selection where it was (ADR-0050 item 3).</summary>
    [Parameter] public EventCallback<GridPasteIntent> OnPaste { get; set; }

    /// <summary>
    /// Whether the fill handle is painted on a single-range Selection while no edit is open (ADR-0050, item 5): at its
    /// bottom-right, at the top-right of whole columns and at the bottom-left of whole rows. Dragged along one axis and released, it raises one <see cref="OnFill"/> intent; a target over
    /// a column that is not Editable is refused first, through <see cref="OnRefused"/>. Off unless set.
    /// </summary>
    [Parameter] public bool ShowFillHandle { get; set; }

    /// <summary>
    /// Whether the Formula Bar stands above the header (ADR-0051): the Name Box and the Focus cell's text, which edits
    /// the cell as its editor does. A Chrome draws its fields where it names <see cref="ScriptSlots.FormulaBarText"/>
    /// and <see cref="ScriptSlots.NameBox"/> (ADR-0410). Off unless set. The Name Box names no cell here yet: the
    /// Script Core asks a label of the Consumer's in the browser, which a .NET delegate cannot answer without a round
    /// trip per move, so it stays empty until the Consumer types in it.
    /// </summary>
    [Parameter] public bool ShowFormulaBar { get; set; }

    /// <summary>
    /// Hears Enter in the Name Box (ADR-0051): the text the user typed there. The Consumer resolves it to a position,
    /// the grid reads no address, and places the Selection and the Focus with <see cref="PlaceSelectionAsync"/>
    /// (ADR-0050, item 4). The keyboard is the grid's again as Enter is typed.
    /// </summary>
    [Parameter] public EventCallback<string> OnNameBoxEntered { get; set; }

    /// <summary>
    /// Hears a fill-handle drag, released (ADR-0050, item 5): the source, the target beside it and the direction. The
    /// Consumer writes the fill, and the rows it then shows are what the cells paint. As with <see cref="OnPaste"/>, the
    /// fill is pending until the handler completes (ADR-0380): one that calls <see cref="GridFillIntent.Refuse"/> before
    /// then leaves the Selection on the source, and otherwise it becomes the source and the target together.
    /// </summary>
    [Parameter] public EventCallback<GridFillIntent> OnFill { get; set; }

    /// <summary>Hears Delete over editable cells (ADR-0035): the Consumer clears them.</summary>
    [Parameter] public EventCallback<GridClearIntent> OnClear { get; set; }

    /// <summary>Hears Ctrl+Z (ADR-0012): undo is the Consumer's.</summary>
    [Parameter] public EventCallback OnUndo { get; set; }

    /// <summary>Hears Ctrl+Y (ADR-0012): redo is the Consumer's.</summary>
    [Parameter] public EventCallback OnRedo { get; set; }

    /// <summary>Hears every gesture the grid refused, by kind and reason (ADR-0005, ADR-0350 section 16).</summary>
    [Parameter] public EventCallback<GridRefusal<TRow>> OnRefused { get; set; }

    /// <summary>Hears a column's width set by a drag or by Size to fit (ADR-0016): the Consumer declares it.</summary>
    [Parameter] public EventCallback<ColumnWidthChange> OnColumnWidthChanged { get; set; }

    /// <summary>Hears the columns' order after a header drag (ADR-0032): the Consumer reorders them.</summary>
    [Parameter] public EventCallback<IReadOnlyList<string>> OnColumnOrderChanged { get; set; }

    /// <summary>Hears Hide from the column menu: the Consumer leaves the column out.</summary>
    [Parameter] public EventCallback<string> OnColumnHidden { get; set; }

    /// <summary>Hears a Header Group's toggle (ADR-0400): a press on it, or Alt+Shift+Down or Up from a cell under
    /// the group. The Consumer answers by setting <see cref="Columns"/> and the group's new
    /// <see cref="HeaderGroup.Expanded"/>; the grid adds and removes no column. Required for any group declared
    /// expanded or collapsed.</summary>
    [Parameter] public EventCallback<HeaderGroupToggle> OnHeaderGroupToggled { get; set; }

    /// <summary>Hears Pin or Unpin from the column menu: the Consumer sets <see cref="PinnedColumnCount"/>.</summary>
    [Parameter] public EventCallback<int> OnPinnedCountChanged { get; set; }

    /// <summary>Hears the Cell Editor open and close.</summary>
    [Parameter] public EventCallback<bool> OnEditingChanged { get; set; }

    /// <summary>Hears an open edit discarded, with the reason (ADR-0011, ADR-0035).</summary>
    [Parameter] public EventCallback<EditDiscardReason> OnEditDiscarded { get; set; }

    /// <summary>Hears a double click on a cell that does not edit.</summary>
    [Parameter] public EventCallback<CellPosition> OnCellDoubleClick { get; set; }

    /// <summary>Hears Escape with nothing left to dismiss (DC-62): the Consumer takes the keyboard elsewhere.</summary>
    [Parameter] public EventCallback OnLeave { get; set; }

    /// <summary>Hears a commit that wrote over a value the user had not seen (ADR-0142).</summary>
    [Parameter] public EventCallback<GridOverwriteNotice> OnOverwriteNotice { get; set; }

    /// <summary>Hears an Action Column's action pressed (ADR-0020): what it does is the Consumer's.</summary>
    [Parameter] public EventCallback<GridActionEventArgs<TRow>> OnAction { get; set; }

    /// <summary>
    /// Cell Metadata, asked for by (row, column) and answered as one of the five states the grid understands
    /// (ADR-0006), as the C# core's <c>CellState</c> is: a Flag's <see cref="CellState.Error"/> paints through it
    /// (ADR-0034). It is asked for each cell of each row as the row crosses to the browser, so it stays a cheap
    /// lookup. Hold it in a field and hand over a new one when the metadata changes: the delegate's identity is the
    /// change signal, and the rows the browser shows are sent again with their states.
    /// </summary>
    [Parameter] public CellStateOf<TRow>? CellState { get; set; }

    /// <summary>A cell's message, asked for by (row, column), as the C# core's <c>CellMessageOf</c> is (ADR-0034):
    /// asked of .NET at the moment a popover would open, once the Focus or the pointer has settled on the cell,
    /// never while painting (ED-17), and the popover opens once it has answered. Null means nothing opens.</summary>
    [Parameter] public Func<TRow, GridColumn<TRow>, string?>? CellMessageOf { get; set; }

    /// <summary>
    /// The step to the next and the previous error cell, F8 and Shift+F8 (ADR-0381 section 3), answered as a Find
    /// step is (ADR-0055): the grid knows only the cells it paints, so where the next error stands is the
    /// Consumer's, read in the order the grid shows. <see cref="Validation.GridRuleset{TRow}.FindError"/> answers it
    /// from the rows the Source holds in that order. It is asked in its turn among the grid's events, once what the
    /// browser asked under is still the source's: a step asked under another order or another source is not handed
    /// to it, and the grid says the rows moved. The token is cancelled when the user steps again before it answers.
    /// Unset, the grid offers no step and leaves F8 to the page. It takes a <see cref="Source"/>, whose order the
    /// Consumer can read, and refuses <see cref="Rows"/>, which the grid orders out of the Consumer's sight.
    /// </summary>
    [Parameter] public Func<Validation.GridErrorStepRequest, CancellationToken, Task<Finding.GridFindResult>>? OnFindError { get; set; }

    /// <summary>
    /// Completion and the argument hint (ADR-0051): asked with the editor's text and caret as the user types or moves
    /// the caret, in the Cell Editor and the Formula Bar alike, in its turn among the grid's events. The grid shows the
    /// answer beneath the surface typed in, unless the text has changed since it was asked: an answer for older text is
    /// dropped, never shown. ↑ and ↓ choose, Tab accepts and Escape closes the list, all in the browser; what a
    /// candidate writes is the Consumer's, the grid knows no Formula. Null, or a null answer, shows nothing.
    /// </summary>
    [Parameter] public Func<string, int, ValueTask<EditorCompletion?>>? CompleteEditorText { get; set; }

    /// <summary>
    /// The Chrome that fills the Script Core's slots with Razor (ADR-0410): each slot its
    /// <see cref="IGridChrome.ScriptSlots"/> names is drawn by its view overload, rendered beside the grid's root
    /// and moved into the slot by the script, which wires its controls in the browser from the attributes of
    /// <see cref="ScriptSlotAttributes"/>. Every other part is the Script Core's own, and so is every part while
    /// this is null.
    /// </summary>
    [Parameter] public IGridChrome? Chrome { get; set; }

    /// <summary>
    /// The Consumer's own Context Menu commands, after the Script Core's (ADR-0036, ADR-0421): handed the row the menu
    /// opened on, the column, and the Selection as rectangles in the version they are written in, as the Razor
    /// component's are. The menu is drawn in the browser, so it asks .NET as it opens and stands once .NET has
    /// answered; what the user types meanwhile is held and reaches it then. A command's <c>Invoke</c> runs in its
    /// turn among the grid's events, and every input made after the press is held until it completes, so a menu
    /// opened next is asked of what it left. The context's <see cref="ContextMenuContext{TRow}.Commands"/> is
    /// empty here: the core's own run in the browser.
    /// </summary>
    [Parameter] public Func<ContextMenuContext<TRow>, IEnumerable<GridCommand>>? ContextCommands { get; set; }

    /// <summary>What the Context Menu calls each command <see cref="ContextCommands"/> answers, by its id; null for
    /// the id itself. It words those commands only: the Script Core's own keep its words.</summary>
    [Parameter] public Func<string, string?>? ContextCommandLabel { get; set; }

    /// <summary>The source the grid reads its rows from: <see cref="Source"/>, or the one it made of
    /// <see cref="Rows"/>, which orders and filters them as the user asks. Null until the parameters are first set.
    /// A Consumer's command reads the rows a Selection covers from it, in the Row Sequence Version the Selection was
    /// taken in.</summary>
    public IGridSource<TRow>? RowSource => _source;

    /// <summary>
    /// The declaration a Pointing Scope made for this grid as it registered it (ADR-0058, ADR-0430), or null — the
    /// default — for a grid in no Scope. The grid joins the Scope in the page, where the Scope's Sheets drawn by the
    /// Script Core point at it: while one points, a press on its rows or its column headers is written into that
    /// Sheet's Formula, and the grid draws the Scope's dashes and column outlines. Each row is sent with what the
    /// Scope's function reads of it as the table's row.
    ///
    /// <para>Set as the grid is made, as a Scope's grids are registered once, as the page is made: another declaration
    /// later is refused. A declaration a Consumer made itself, whose <see cref="GridPointedAt{TRow}.OnPress"/> is a
    /// .NET function, is not carried, and is refused by name; so is a Scope's declaration whose Scope already holds the
    /// Razor components.</para>
    /// </summary>
    [Parameter] public GridPointedAt<TRow>? PointedAt { get; set; }

    private ElementReference _root;
    // The commands the last Context Menu was answered with, by the number the browser asked it under (ADR-0421).
    private (long Number, IReadOnlyList<GridCommand> Commands)? _menu;
    // The slots the script mounted for the Chrome, by the number it gave each (ADR-0410 section 1).
    private readonly RazorSlotHolders _slots = new(RazorSlot.ViewOf);
    private IGridChrome? _renderedChrome;
    private IJSObjectReference? _module;
    private IJSObjectReference? _grid;
    private DotNetObjectReference<ScriptBridge<TRow>>? _bridge;
    private readonly ScriptRows<TRow> _rows = new();

    /// <summary>The browser's calls into .NET, made in the order the browser made them, one too large for a message of
    /// its own among them (ADR-0005, as amended 2026-10-20).</summary>
    internal HeldCalls Calls { get; } = new();
    private readonly Dictionary<long, CancellationTokenSource> _asked = [];
    private readonly Queue<Heard> _heard = new();
    private readonly Dictionary<long, CancellationTokenSource> _answers = [];
    private CellStateOf<TRow>? _sentCellState;
    private Func<TRow, GridColumn<TRow>, string?>? _sentCellMessage;
    // Moves when the Consumer hands another judgement, so the browser asks a message it shows again (ADR-0034).
    private int _judgement;
    // The declarations sent lately, by their number: a verdict is the column's as the browser held it when the
    // user committed, whatever the Consumer has declared since (ADR-0380 section 3).
    private readonly Dictionary<int, IReadOnlyList<GridColumn<TRow>>> _declared = [];
    private const int DeclarationsKept = 8;

    private IGridSource<TRow>? _source;
    private InMemoryGridSource<TRow>? _ownSource;
    private IReadOnlyList<TRow>? _ownRows;
    private int _generation;
    private IReadOnlyList<GridColumn<TRow>>? _toldColumns;

    /// <summary>The rows sent before the grid has asked for any: about five screens at the Compact density.</summary>
    private const int FirstRows = 256;

    /// <summary>The part of the source's Window the browser is sent: around the rows the grid last asked for, so a
    /// source that holds every row, as <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/> does, crosses the
    /// rows in view and a few screens either side, never the whole result on every change (ADR-0004).</summary>
    private RowRange _shown = new(0, FirstRows);

    private int _columnsVersion;
    private string? _toldCulture;
    private readonly FormatNames _formats = new();

    private string? _sentOptions;
    private bool _sourceReplaced;
    private bool _columnsChanged;
    private bool _attached;
    private bool _attaching;
    private bool _refused;
    private bool _disposed;
    private bool _stateOwed;
    private bool _stateScheduled;
    private bool _hearing;
    private bool _renderOwed = true;
    private bool _hoverSupplied;
    private bool _stripesSupplied;
    // The declaration of a Pointing Scope this grid took as it was made, which it keeps (ADR-0430).
    private GridPointedInBrowser<TRow>? _pointedAt;
    private bool _parametersSet;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // A Wrapper's hover band and Row Stripes stand where these were never written (ADR-0030).
        _hoverSupplied |= parameters.TryGetValue<bool>(nameof(HighlightHoverRow), out _);
        _stripesSupplied |= parameters.TryGetValue<bool>(nameof(StripeRows), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (CellMetrics is { DigitWidthPx: <= 0 } empty)
            throw new ArgumentOutOfRangeException(nameof(CellMetrics), empty, "default(CellTextMetrics) is not valid; construct it with real metrics.");
        if ((Source is null) == (Rows is null))
            throw new InvalidOperationException("ExGrid takes its rows from exactly one of Source and Rows (ADR-0350 section 16).");
        if (OnFindError is not null && Rows is not null)
        {
            throw new InvalidOperationException("OnFindError answers in the order the grid shows, which a Consumer reads from its Source, and "
                + "the grid orders Rows out of its sight: give the grid a Source, GridSource.From(rows) for rows held in memory (ADR-0381).");
        }
        ScriptColumns.Check(Columns);
        TakePointedAt();
        if (!ReferenceEquals(Chrome, _renderedChrome))
        {
            // Another Chrome draws the slots standing, and the script wires what it drew.
            _renderedChrome = Chrome;
            _slots.Redrawn();
            _renderOwed |= _slots.Any;
        }
        if (!ReferenceEquals(CellState, _sentCellState))
        {
            // Another judgement: the rows the browser shows go again, with their states (ADR-0006).
            _sentCellState = CellState;
            _stateOwed = true;
            _judgement += 1;
        }
        if (!ReferenceEquals(CellMessageOf, _sentCellMessage))
        {
            _sentCellMessage = CellMessageOf;
            _judgement += 1;
        }

        IGridSource<TRow> source;
        if (Rows is { } rows)
        {
            if (_ownSource is null || !ReferenceEquals(rows, _ownRows))
            {
                _ownSource = GridSource.From(rows);
                _ownRows = rows;
            }
            source = _ownSource;
        }
        else
        {
            source = Source!;
            _ownSource = null;
            _ownRows = null;
        }

        if (!ReferenceEquals(source, _source))
        {
            // A bundled source belongs to one circuit (ADR-0018 section 5): attached before the swap, so a refusal
            // leaves the grid bound to what it had.
            if (source is IBindsToOneCircuit bindsTo)
                bindsTo.Binding.Attach(this, SynchronizationContext.Current);
            if (_source is not null)
            {
                _source.StateChanged -= OnSourceChanged;
                (_source as IBindsToOneCircuit)?.Binding.Detach(this);
                _sourceReplaced = true;
            }
            _source = source;
            _generation += 1;
            _toldColumns = null;
            source.StateChanged += OnSourceChanged;
        }
        var culture = CultureInfo.CurrentCulture.Name;
        var sameColumns = SameColumns(_toldColumns, Columns);
        if (!sameColumns || culture != _toldCulture)
        {
            // Another declaration, or another culture for the formats to write in, is another number, and the rows
            // the browser holds were read by the one before (ADR-0006).
            _columnsVersion += 1;
            _declared[_columnsVersion] = Columns;
            _declared.Remove(_columnsVersion - DeclarationsKept);
            _columnsChanged = _toldCulture is not null;
            _toldCulture = culture;
            if (!sameColumns)
            {
                // The source sorts and filters by the columns' values, read in .NET, as the C# core tells it (ADR-0001).
                source.OnColumnsChanged([.. Columns.Select(column => column.Info)]);
                _toldColumns = Columns;
            }
        }
    }

    // A Scope's declaration, taken once, as the grid is made: the browser registers it in its Scope then, and every row
    // it is sent carries what the Scope reads of it (ADR-0430).
    private void TakePointedAt()
    {
        var given = PointedAt?.InBrowser;
        if (PointedAt is not null && given is null)
        {
            throw new NotSupportedException("PointedAt is carried by Blazor's ExGrid over the Script Core only as a Pointing Scope's RegisterGrid made it; "
                + "a GridPointedAt whose OnPress is a .NET function of the Consumer's own is not (ADR-0430).");
        }
        if (_parametersSet)
        {
            if (!ReferenceEquals(given, _pointedAt))
            {
                throw new NotSupportedException("PointedAt is set as the grid is made, as a Pointing Scope registers its grids once, as the page is made; "
                    + "another declaration later is not taken (ADR-0430).");
            }
            return;
        }
        _parametersSet = true;
        given?.Take(inBrowser: true);
        _pointedAt = given;
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_attached)
            await SendUpdateAsync();
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _renderOwed;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _renderOwed = false;
        builder.OpenElement(0, "div");
        builder.AddElementReferenceCapture(1, element => _root = element);
        if (_refused)
        {
            // The core never attached: the root is Blazor's to say why, in the built-in words (ADR-0123).
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "role", "alert");
            builder.AddAttribute(4, "data-ex-notice", "script");
            builder.AddContent(5, BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
            builder.CloseElement();
        }
        builder.CloseElement();
        _slots.Render(builder, 6, (name, view) => RazorSlot.Fragment(Chrome, name, view));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            await ReportSlotsAsync();
            return;
        }
        // The script this build was made with, by an address that changes with its content (ADR-0123), and refused
        // when the browser holds another version's: nothing is attached, and the root says so.
        var address = "./" + PackageStylesheet.AddressOf(Assets, ScriptPath, ScriptCoreAsset.Version);
        IJSObjectReference? module = null;
        string? stamp;
        try
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", address);
            _module = module;
            stamp = await module.InvokeAsync<string?>("stamp");
        }
        catch (JSException)
        {
            // A module that does not load, or one older than the stamp, which has no `stamp` to call.
            stamp = null;
        }
        catch (JsonException)
        {
            // An answer that is not a string is no stamp of this family's.
            stamp = null;
        }
        if (_disposed)
        {
            // Disposed while the module loaded: what DisposeAsync could not yet see is let go here.
            await ReleaseAsync(null, module);
            return;
        }
        if (module is null || stamp != ScriptCoreAsset.Stamp)
        {
            _refused = true;
            _renderOwed = true;
            StateHasChanged();
            return;
        }
        // What the attach carries is what the browser holds once it returns: anything that changes while it is on
        // its way is owed again, and sent after it, in the order it changed.
        var options = OptionsJson();
        var attach = AttachJson(options);
        _sentOptions = options;
        _sourceReplaced = false;
        _columnsChanged = false;
        _stateOwed = false;
        var bridge = DotNetObjectReference.Create(new ScriptBridge<TRow>(this));
        _bridge = bridge;
        _attaching = true;
        IJSObjectReference grid;
        try
        {
            grid = await module.InvokeAsync<IJSObjectReference>("attach", _root, bridge, attach);
        }
        catch
        {
            // Never attached: a call held as it attached is never taken, and every call after it is let through.
            Calls.Stopped();
            throw;
        }
        finally
        {
            _attaching = false;
        }
        if (_disposed)
        {
            // Disposed while it attached: the browser's grid is stopped before the object it calls is let go.
            await ReleaseAsync(grid, module);
            bridge.Dispose();
            return;
        }
        _grid = grid;
        _attached = true;
        Calls.Attached(grid);
        // A slot the grid mounted as it attached may have been drawn already, before there was a grid to hand it to.
        await ReportSlotsAsync();
        await SendUpdateAsync();
    }

    /// <summary>Places the Selection as a click leaves it (ADR-0050 item 4), if the source still says
    /// <paramref name="rowSequenceVersion"/>: true once placed, and otherwise refused through <see cref="OnRefused"/>.</summary>
    public async Task<bool> PlaceSelectionAsync(SelectionRange range, CellPosition focus, int rowSequenceVersion)
    {
        var grid = _grid ?? throw new InvalidOperationException("This ExGrid is not attached: a grid not drawn places nothing.");
        // The version is judged against the source's state the browser holds: a change the Consumer made just
        // before crosses first, whatever is still waiting to send it (the spine's sixth rule).
        await SendStateAsync();
        return await grid.InvokeAsync<bool>("placeSelection",
            new Dictionary<string, int> { ["topRow"] = range.TopRow, ["leftColumn"] = range.LeftColumn, ["rowCount"] = range.RowCount, ["columnCount"] = range.ColumnCount },
            new Dictionary<string, int> { ["row"] = focus.Row, ["column"] = focus.Column },
            rowSequenceVersion);
    }

    /// <summary>Hands the keyboard to the grid (ADR-0080).</summary>
    public async Task FocusAsync()
    {
        var grid = _grid ?? throw new InvalidOperationException("This ExGrid is not attached: a grid not drawn takes no keyboard.");
        await grid.InvokeVoidAsync("focus");
    }

    /// <summary>
    /// Gives the keyboard back to the grid from something of the Consumer's that held it over the grid and goes
    /// away, a dialog, a panel, a tab (DC-61, ADR-0070): to an open edit's field, else where the grid takes keys
    /// (ADR-0080). It is granted only while the keyboard is still this grid's, DOM focus inside it or on nothing,
    /// so a control or another grid the user has moved to keeps it.
    ///
    /// <para>It moves neither the Focus nor the Selection, and scrolls nothing. On Blazor Server the request lands
    /// a round trip after the call, and a press made in that time wins. Before the grid is attached, and after it
    /// is gone, it does nothing, and with the circuit gone it is dropped.</para>
    /// </summary>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task ReturnKeyboardAsync() => ReturnKeyboardAsync(default);

    /// <summary>
    /// <see cref="ReturnKeyboardAsync()"/>, from a frame of the Consumer's own that holds the keyboard as it goes:
    /// it is granted while DOM focus is on <paramref name="from"/> too, so the frame gives the keyboard back
    /// before it is removed (DC-61, ADR-0070). A frame not yet drawn has no element to name, and the grid's own
    /// condition stands. Keys typed on the frame before the request lands are the frame's: this grid joins no
    /// surface to its order, so it has no keys typed on the way to hand on, unlike
    /// <c>ExGrid.Components.ExGrid</c>'s overload that collects them.
    /// </summary>
    /// <param name="from">The frame's element that holds the keyboard, or that holds the element that does.</param>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task ReturnKeyboardAsync(ElementReference from)
        => InvokeAsync(async () =>
        {
            var grid = _grid;
            if (grid is null || _disposed)
                return;
            try
            {
                await grid.InvokeVoidAsync("returnKeyboard", string.IsNullOrEmpty(from.Id) ? null : from);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
                // The grid or the circuit is gone: there is no keyboard left to give back.
            }
        });

    /// <summary>
    /// Hands the keyboard the grid holds on to a control of the Consumer's own, a frame's first control or a
    /// dialog's Close, in place of the control's own <c>FocusAsync</c> (DC-61, ADR-0070). It is granted under
    /// <see cref="ReturnKeyboardAsync()"/>'s condition, so a press the user made before the request lands keeps
    /// the keyboard where it put it, whatever the round trip took.
    ///
    /// <para>It moves neither the Focus nor the Selection, and renders nothing. The control scrolls into view, as
    /// its own focus would have it. Before the grid is attached, and after it is gone, the control takes the
    /// keyboard by its own <c>FocusAsync</c>, as it would without the grid. A control a render removed before the
    /// request lands takes nothing, and with the circuit gone the request is dropped.</para>
    /// </summary>
    /// <param name="control">The element to hand the keyboard to. Once the grid is attached, a container names its
    /// first enabled text field, its text selected, or else its first enabled control; one holding neither is
    /// refused with a <see cref="JSException"/>, as is an element that cannot take the keyboard.</param>
    /// <exception cref="ArgumentException"><paramref name="control"/> names no element: it is not drawn yet.</exception>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task HandKeyboardToAsync(ElementReference control) => HandKeyboardToAsync(control, preventScroll: false);

    /// <summary>
    /// <see cref="HandKeyboardToAsync(ElementReference)"/>, with the control taking the keyboard where it stands
    /// when <paramref name="preventScroll"/> is true: nothing scrolls to bring it into view, as
    /// <c>FocusAsync(preventScroll: true)</c> has it.
    /// </summary>
    /// <param name="control">The element to hand the keyboard to, as for <see cref="HandKeyboardToAsync(ElementReference)"/>.</param>
    /// <param name="preventScroll">Whether the control takes the keyboard without scrolling into view.</param>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task HandKeyboardToAsync(ElementReference control, bool preventScroll)
        => InvokeAsync(async () =>
        {
            if (string.IsNullOrEmpty(control.Id))
                throw new ArgumentException("A control not yet drawn has no element to hand the keyboard to.", nameof(control));
            var grid = _grid;
            try
            {
                if (grid is null || _disposed)
                    await control.FocusAsync(preventScroll);
                else
                    await grid.InvokeVoidAsync("handKeyboardTo", control, preventScroll);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
                // The grid or the circuit is gone: there is nothing left to take the keyboard.
            }
        });

    // ---- What crosses to the browser ------------------------------------------------------------------------

    /// <summary>The script mounted slot <paramref name="id"/> for the Chrome, with its context as data (ADR-0410).</summary>
    internal void MountSlot(long id, int version, string name, string context)
    {
        if (_disposed)
            return;
        _slots.Mount(id, version, name, context);
        RenderSlots();
    }

    /// <summary>The slot <paramref name="id"/> was mounted again in place, with another context.</summary>
    internal void UpdateSlot(long id, int version, string context)
    {
        if (_disposed || !_slots.Update(id, version, context))
            return;
        RenderSlots();
    }

    /// <summary>The core took the slot <paramref name="id"/> away; the script has moved its contents back.</summary>
    internal void UnmountSlot(long id)
    {
        if (_disposed || !_slots.Unmount(id))
            return;
        RenderSlots();
    }

    private void RenderSlots()
    {
        _renderOwed = true;
        StateHasChanged();
    }

    // Each slot drawn again is handed to the script, which moves it in, or wires it for the context it now holds.
    private Task ReportSlotsAsync() => _slots.ReportAsync(_grid, () => _disposed);

    private async Task SendUpdateAsync()
    {
        var grid = _grid;
        if (grid is null || _disposed)
            return;
        var options = OptionsJson();
        var replaced = _sourceReplaced;
        var columnsChanged = _columnsChanged;
        if (!replaced && !columnsChanged && options == _sentOptions)
        {
            if (_stateOwed)
                await SendStateAsync();
            return;
        }
        _sentOptions = options;
        _sourceReplaced = false;
        _columnsChanged = false;
        // The rows go with columns that changed, read by the new columns: a row the browser holds was read by the old ones.
        var state = replaced || columnsChanged ? StateJson() : null;
        if (state is not null)
            _stateOwed = false;
        await grid.InvokeVoidAsync("update", Json(json =>
        {
            json.WriteStartObject();
            json.WritePropertyName("options");
            json.WriteRawValue(options);
            if (replaced)
            {
                json.WritePropertyName("source");
                WriteTraits(json);
            }
            if (state is not null)
            {
                json.WritePropertyName("state");
                json.WriteRawValue(state);
            }
            json.WriteEndObject();
        }));
        if (_stateOwed)
            await SendStateAsync();
    }

    private void OnSourceChanged()
    {
        _stateOwed = true;
        if (_stateScheduled)
            return;
        _stateScheduled = true;
        // A source may say it changed from any thread, and many times in one handler's writes: the state is sent
        // from the component's context once what is running there now is done, so a paste of 500 rows crosses
        // as one Window, as the C# core takes a handler's writes in once.
        _ = InvokeAsync(async () =>
        {
            await Task.Yield();
            _stateScheduled = false;
            try
            {
                if (_attached)
                    await SendStateAsync();
            }
            catch (Exception ex) when (ex is not JSDisconnectedException)
            {
                await DispatchExceptionAsync(ex);
            }
        });
    }

    private async Task SendStateAsync()
    {
        var grid = _grid;
        if (grid is null || _disposed || !_stateOwed)
            return;
        _stateOwed = false;
        await grid.InvokeVoidAsync("state", StateJson());
    }

    private string AttachJson(string options) => Json(json =>
    {
        json.WriteStartObject();
        json.WritePropertyName("options");
        json.WriteRawValue(options);
        json.WritePropertyName("source");
        WriteTraits(json);
        json.WritePropertyName("state");
        json.WriteRawValue(StateJson());
        // Across a circuit, Blazor Server's, a call larger than its hub takes would close it: the browser holds one.
        json.WriteBoolean("circuit", !OperatingSystem.IsBrowser());
        json.WriteEndObject();
    });

    private void WriteTraits(Utf8JsonWriter json)
    {
        var source = _source ?? throw new InvalidOperationException("Internal: the grid has no source.");
        json.WriteStartObject();
        json.WriteNumber("generation", _generation);
        json.WriteBoolean("rowKey", source.RowKey is not null);
        json.WriteBoolean("vouchesDistinctRows", source.VouchesDistinctRows);
        json.WriteBoolean("finds", source.CanFind);
        json.WriteEndObject();
    }

    private string StateJson()
    {
        var source = _source ?? throw new InvalidOperationException("Internal: the grid has no source.");
        var window = source.Window;
        var from = Math.Max(source.WindowStart, _shown.Start);
        var to = Math.Min(source.WindowStart + window.Count, _shown.Start + _shown.Count);
        return _rows.Sending(sequence => Json(json =>
        {
            json.WriteStartObject();
            json.WriteNumber("seq", sequence);
            json.WriteNumber("cv", _columnsVersion);
            json.WriteStartArray("window");
            for (var at = from; at < to; at++)
                _rows.Write(json, window[at - source.WindowStart], Columns, source.RowKey, CellState, _pointedAt?.TableRow);
            json.WriteEndArray();
            json.WriteNumber("windowStart", to > from ? from : source.WindowStart);
            if (source.TotalCount is { } total)
                json.WriteNumber("totalCount", total);
            else
                json.WriteNull("totalCount");
            json.WriteBoolean("loading", source.IsLoading);
            json.WriteNumber("rowSequenceVersion", source.RowSequenceVersion);
            json.WriteString("query", GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), source.Sorts, source.Filter)));
            json.WriteEndObject();
        }));
    }

    private string OptionsJson() => Json(json =>
    {
        json.WriteStartObject();
        json.WritePropertyName("columns");
        ScriptColumns.Write(json, Columns, _formats, _toldCulture ?? throw new InvalidOperationException("Internal: options written before the parameters were set."));
        json.WriteNumber("columnsVersion", _columnsVersion);
        // The culture the component reads, as the C# core reads it (ADR-0370 section 10).
        var culture = CultureInfo.CurrentCulture.Name;
        json.WriteString("locale", culture.Length == 0 ? "invariant" : culture);
        if (Density is { } density)
            json.WriteString("density", ScriptJson.Camel(density));
        if (RowHeight is { } rowHeight)
            json.WriteNumber("rowHeight", rowHeight);
        if (HeaderHeight is { } headerHeight)
            json.WriteNumber("headerHeight", headerHeight);
        if (ViewportHeight is { } height)
            WriteSize(json, "viewportHeight", height);
        if (ViewportWidth is { } width)
            WriteSize(json, "viewportWidth", width);
        json.WriteNumber("pinnedColumnCount", PinnedColumnCount);
        json.WriteBoolean("virtualiseColumns", VirtualiseColumns);
        // Written only where the Consumer wrote them, so a Wrapper's stand where it did not (ADR-0030).
        if (_stripesSupplied)
            json.WriteBoolean("stripeRows", StripeRows);
        json.WriteBoolean("highlightFocusRow", HighlightFocusRow);
        if (_hoverSupplied)
            json.WriteBoolean("highlightHoverRow", HighlightHoverRow);
        if (CellMetrics is { } metrics)
            ScriptMetricsJson.WriteCellMetrics(json, "cellMetrics", metrics);
        if (Presentation is { } presentation)
            ScriptMetricsJson.WritePresentationDefaults(json, "presentationDefaults", presentation);
        json.WriteBoolean("hideColumnMenu", HideColumnMenu);
        json.WriteBoolean("hideCopyWithHeaders", HideCopyWithHeaders);
        json.WriteBoolean("headerClickSelects", HeaderClickSelects);
        json.WriteBoolean("pasteMaySpill", PasteMaySpill);
        json.WriteBoolean("showFillHandle", ShowFillHandle);
        json.WriteBoolean("showFormulaBar", ShowFormulaBar);
        json.WriteBoolean("findsHeaders", FindsHeaders);
        // Nothing answers a Selection Summary over a source in .NET yet (ADR-0130): none is drawn, rather than a
        // status line that never fills.
        json.WriteBoolean("showSelectionSummary", false);
        if (CopyCellCap is { } copyCap)
            json.WriteNumber("copyCellCap", copyCap);
        if (PasteByteCap is { } pasteCap)
            json.WriteNumber("pasteByteCap", pasteCap);
        if (HeaderGroups is { } groups)
        {
            json.WriteStartArray("headerGroups");
            foreach (var group in groups)
            {
                json.WriteStartObject();
                json.WriteString("label", group.Label);
                json.WriteStartArray("columns");
                foreach (var column in group.Columns)
                    json.WriteStringValue(column);
                json.WriteEndArray();
                json.WriteNumber("tier", group.Tier);
                json.WriteNumber("tierSpan", group.TierSpan);
                if (group.Id is { } id)
                    json.WriteString("id", id);
                if (group.Expanded is { } expanded)
                    json.WriteBoolean("expanded", expanded);
                if (group.HiddenMark is { } mark)
                {
                    json.WriteStartObject("hiddenMark");
                    json.WriteString("state", ScriptJson.Camel(mark.State));
                    json.WriteString("reason", mark.Reason);
                    json.WriteEndObject();
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        if (CellState is not null)
            json.WriteBoolean("cellStates", true);
        if (CellMessageOf is not null)
            json.WriteNumber("cellMessages", _judgement);
        if (OnFindError is not null)
            json.WriteBoolean("findsErrors", true);
        if (CompleteEditorText is not null)
            json.WriteBoolean("completesText", true);
        if (ContextCommands is not null)
            json.WriteBoolean("contextCommands", true);
        if (_pointedAt is { } pointedAt)
        {
            json.WriteStartObject("pointedAt");
            json.WriteString("scope", pointedAt.Scope);
            json.WriteString("declaration", pointedAt.Declaration);
            json.WriteString("table", pointedAt.Table);
            json.WriteStartObject("tableColumns");
            foreach (var (gridColumn, tableColumn) in pointedAt.TableColumns)
                json.WriteString(gridColumn, tableColumn);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        if (Chrome?.ScriptSlots is { } slots and not ScriptSlots.None)
        {
            json.WriteStartArray("razorSlots");
            foreach (var name in RazorSlot.NamesOf(slots))
                json.WriteStringValue(name);
            json.WriteEndArray();
        }
        json.WriteStartArray("hears");
        foreach (var (kind, heard) in Hears())
        {
            if (heard)
                json.WriteStringValue(kind);
        }
        json.WriteEndArray();
        json.WriteEndObject();
    });

    private IEnumerable<(string Kind, bool Heard)> Hears()
    {
        yield return ("selectionChanged", SelectionChanged.HasDelegate);
        yield return ("edit", OnEdit.HasDelegate);
        yield return ("paste", OnPaste.HasDelegate);
        yield return ("fill", OnFill.HasDelegate);
        yield return ("clear", OnClear.HasDelegate);
        yield return ("undo", OnUndo.HasDelegate);
        yield return ("redo", OnRedo.HasDelegate);
        yield return ("refused", OnRefused.HasDelegate);
        yield return ("columnWidthChanged", OnColumnWidthChanged.HasDelegate);
        yield return ("columnOrderChanged", OnColumnOrderChanged.HasDelegate);
        yield return ("columnHidden", OnColumnHidden.HasDelegate);
        yield return ("pinnedCountChanged", OnPinnedCountChanged.HasDelegate);
        yield return ("headerGroupToggled", OnHeaderGroupToggled.HasDelegate);
        yield return ("editingChanged", OnEditingChanged.HasDelegate);
        yield return ("editDiscarded", OnEditDiscarded.HasDelegate);
        yield return ("cellDoubleClick", OnCellDoubleClick.HasDelegate);
        yield return ("leave", OnLeave.HasDelegate);
        yield return ("overwriteNotice", OnOverwriteNotice.HasDelegate);
        yield return ("action", OnAction.HasDelegate);
        yield return ("nameBoxEntered", OnNameBoxEntered.HasDelegate);
    }

    private static void WriteSize(Utf8JsonWriter json, string name, ViewportSize size)
    {
        if (size.IsStretch)
            json.WriteString(name, "stretch");
        else
            json.WriteNumber(name, size.Px);
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
            write(json);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static bool SameColumns(IReadOnlyList<GridColumn<TRow>>? a, IReadOnlyList<GridColumn<TRow>> b)
    {
        if (a is null || a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        }
        return true;
    }

    // ---- What the browser asks of .NET (ScriptBridge) -------------------------------------------------------

    /// <summary>The source a question from the browser is for, or null where the Consumer has since replaced it:
    /// what the browser asked of a source it no longer reads is not acted on.</summary>
    private IGridSource<TRow>? SourceOf(int generation) => generation == _generation && !_disposed ? _source : null;

    // What the grid tells its source is the Consumer's code running: what it throws fails the component as a
    // handler of Blazor's own does, so the error UI or an error boundary says so, rather than a promise the
    // browser was left with.
    internal Task SetSortsAsync(int generation, string query)
        => Telling(() => SourceOf(generation)?.OnSortChanged(GridViewStateJson.ReadQuery(query).Sorts));

    internal Task SetFilterAsync(int generation, string query)
        => Telling(() => SourceOf(generation)?.OnFilterChanged(GridViewStateJson.ReadQuery(query).Filter));

    internal async Task NeedRangeAsync(int generation, int start, int count)
    {
        if (SourceOf(generation) is not { } source)
            return;
        try
        {
            // The rows asked for, and two times as many either side, so a page down or a fling's landing is
            // mostly in hand already; the source is told too, for a source that fetches (ADR-0001).
            var pad = 2 * Math.Max(count, 64);
            var shownStart = Math.Max(0, start - pad);
            _shown = new RowRange(shownStart, start + count + pad - shownStart);
            _stateOwed = true;
            await source.OnRangeNeededAsync(new RowRange(start, count));
            if (_attached)
                await SendStateAsync();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    private async Task Telling(Action tell)
    {
        try
        {
            tell();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    internal async Task<string> RowsAsync(int generation, long id, int start, int count)
    {
        var source = SourceOf(generation) ?? throw new OperationCanceledException("The source asked was replaced.");
        using var asked = Ask(id);
        try
        {
            var rows = await source.GetRowsAsync(new RowRange(start, count), asked.Token);
            return _rows.Sending(sequence => Json(json =>
            {
                json.WriteStartObject();
                json.WriteNumber("seq", sequence);
                json.WriteNumber("cv", _columnsVersion);
                json.WriteStartArray("rows");
                foreach (var row in rows)
                    // Read for a copy or a find, never painted: their states are not asked.
                    _rows.Write(json, row, Columns, source.RowKey, cellState: null, _pointedAt?.TableRow);
                json.WriteEndArray();
                json.WriteEndObject();
            }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The source's own failure, or a value refused as it crossed, fails the component as the C# core's copy
            // fails on it; the browser's question is answered by the same failure, so nothing waits on it.
            await DispatchExceptionAsync(ex);
            throw;
        }
        finally
        {
            _asked.Remove(id);
        }
    }

    internal async Task<string> DistinctValuesAsync(int generation, long id, string column)
    {
        var source = SourceOf(generation) ?? throw new OperationCanceledException("The source asked was replaced.");
        using var asked = Ask(id);
        try
        {
            // A column the Consumer took away while the browser asked, before .NET heard it or while its source
            // answered, is answered as gone: the browser reads that once it has the columns that say so.
            if (Declared(column) is null)
                return Gone();
            var answer = await source.GetDistinctValuesAsync(column, asked.Token);
            if (Declared(column) is not { } declared)
                return Gone();
            return Json(json =>
            {
                json.WriteStartObject();
                json.WriteNumber("cv", _columnsVersion);
                if (answer.IsTooMany)
                {
                    json.WriteString("kind", "tooMany");
                }
                else
                {
                    json.WriteString("kind", "values");
                    json.WriteStartArray("values");
                    foreach (var value in answer.Values)
                        ScriptValues.Write(json, value, column);
                    json.WriteEndArray();
                    if (declared.Format is { } format)
                    {
                        json.WriteStartArray("texts");
                        foreach (var value in answer.Values)
                        {
                            if (value is null)
                                json.WriteNullValue();
                            else
                                json.WriteStringValue(format(value));
                        }
                        json.WriteEndArray();
                    }
                }
                json.WriteEndObject();
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // As RowsAsync: the source's failure, or a format's, fails the component, and the panel's question too.
            await DispatchExceptionAsync(ex);
            throw;
        }
        finally
        {
            _asked.Remove(id);
        }
    }

    private GridColumn<TRow>? Declared(string column) => Columns.FirstOrDefault(each => each.Name == column);

    private string Gone() => Json(json =>
    {
        json.WriteStartObject();
        json.WriteNumber("cv", _columnsVersion);
        json.WriteString("kind", "gone");
        json.WriteEndObject();
    });

    private CancellationTokenSource Ask(long id)
    {
        var asked = new CancellationTokenSource();
        _asked[id] = asked;
        return asked;
    }

    internal void Cancel(long id)
    {
        if (_asked.TryGetValue(id, out var asked))
            asked.Cancel();
    }

    internal void Release(long[] ids, long seen) => _rows.Release(ids, seen);

    /// <summary>One thing the browser handed .NET, in its turn: an event, or a question it waits on the answer to.</summary>
    private sealed record Heard(Func<Task<string?>> Hand, bool Answers, bool SendsState)
    {
        public TaskCompletionSource<string?> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Hears an event the grid raised. It is read as it arrives, so a row it names is the row the browser
    /// meant even if the browser lets that row go before the event's turn comes. Each is handed to the Consumer once
    /// the one before it was, in the order the grid raised them, whatever a handler awaits (the spine's sixth rule).
    /// What the handler wrote to the source crosses before the browser hears the event was handled, so the browser,
    /// which holds what .NET sends while an edit, a paste, a fill or a clear is being handled, takes in a Window that
    /// holds the write, never one sent before it (ADR-0142, D1). A handler that throws fails the component as a handler
    /// of Blazor's own events does, so an error boundary or the host's error UI says so. What .NET answers is an edit's,
    /// a paste's or a fill's refusal (ADR-0380 section 5): null where the Consumer took it, and where it refused it, the
    /// Consumer's sentence, empty for a paste or a fill, which carry none. An edit, a paste or a fill whose handler
    /// failed is answered by the failure, so the browser does not take a write the Consumer never made as made.</summary>
    internal Task<string?> HeardAsync(string kind, JsonElement payload)
    {
        Func<Task<string?>> hand;
        var answers = kind is "edit" or "paste" or "fill";
        try
        {
            hand = kind switch
            {
                "edit" => PrepareEdit(payload),
                "paste" => PreparePaste(payload),
                "fill" => PrepareFill(payload),
                _ => Unanswered(Prepare(kind, payload)),
            };
        }
        catch (Exception ex)
        {
            // An event .NET cannot read fails the component in its turn, as a handler's exception does.
            hand = () => Task.FromException<string?>(ex);
        }
        return InTurnAsync(new Heard(hand, answers, SendsState: true));
    }

    /// <summary>The verdict on a commit the browser numbered <paramref name="number"/> (ADR-0380 section 5), asked of
    /// the column's <see cref="GridColumn{TRow}.Validate"/> or <see cref="GridColumn{TRow}.ValidateAsync"/> as the
    /// browser held it, in the declarations numbered <paramref name="columnsVersion"/>, in its turn among the events,
    /// against the row the browser holds, and written as the core reads an Edit Verdict. A column declared without a
    /// verdict accepts; a commit the user cancelled is not judged, and its answer is not read.</summary>
    internal Task<string?> ValidateAsync(long number, int columnsVersion, long row, string column, string text)
    {
        var judged = _rows.RowOf(row);
        if (!_declared.TryGetValue(columnsVersion, out var declared))
            throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser judged a commit by columns numbered {columnsVersion}, which .NET no longer holds."));
        var judging = declared.FirstOrDefault(each => each.Name == column)
            ?? throw new InvalidOperationException($"Internal: the browser judged a commit into column '{column}', which its columns did not declare.");
        return InTurnAsync(new Heard(Answered(number, async cancelled =>
        {
            var verdict = judging switch
            {
                { Validate: { } now } => now(judged, text),
                { ValidateAsync: { } later } => await later(judged, text, cancelled),
                _ => EditVerdict.Accept,
            };
            return Json(json =>
            {
                json.WriteStartObject();
                json.WriteString("kind", ScriptJson.Camel(verdict.Kind));
                if (verdict.Message is { } message)
                    json.WriteString("message", message);
                json.WriteEndObject();
            });
        }), Answers: true, SendsState: false));
    }

    /// <summary>The Consumer's completion for the editor's <paramref name="text"/> and <paramref name="caret"/>
    /// (ADR-0051), asked in its turn among the events and written as the browser reads it; null where it answers
    /// nothing, or was taken away since the browser asked. The browser asks one at a time, and only its latest
    /// question once one is on its way, so typing does not queue a question per key ahead of the edit.</summary>
    internal Task<string?> CompleteAsync(string text, int caret)
        => InTurnAsync(new Heard(async () =>
        {
            if (CompleteEditorText is not { } complete || await complete(text, caret) is not { } answer)
                return null;
            return ScriptJson.Completion(answer);
        }, Answers: true, SendsState: false));

    /// <summary>A cell's message, asked as the browser's popover would open (ADR-0034), in its turn among the
    /// events: null where the Consumer has nothing to say, or no longer asks.</summary>
    internal Task<string?> MessageAsync(long row, string column)
    {
        var asked = _rows.RowOf(row);
        return InTurnAsync(new Heard(() => Task.FromResult(
            CellMessageOf is { } message && Declared(column) is { } declared ? message(asked, declared) : null),
            Answers: true, SendsState: false));
    }

    /// <summary>The Consumer's commands for the Context Menu the browser numbered <paramref name="number"/>, opened on
    /// the row it holds as <paramref name="row"/> (ADR-0421), asked in its turn among the events, and written as the
    /// browser reads them: each command's id, whether it is enabled, and its words where
    /// <see cref="ContextCommandLabel"/> gives some. They are kept until a command of theirs runs or the next menu is
    /// asked, which is the only one the browser runs a command of. A menu opened under columns, a source or an order
    /// .NET no longer holds is not handed to the Consumer, whose positions would be read against another grid: it is
    /// answered as moved, after what .NET owes the browser, and no menu stands.</summary>
    internal Task<string?> ContextCommandsAsync(long number, int generation, int columnsVersion, long row, string column, JsonElement selection, int rowSequenceVersion)
    {
        var asked = _rows.RowOf(row);
        var ranges = ScriptJson.Ranges(selection);
        return InTurnAsync(new Heard(async () =>
        {
            if (columnsVersion != _columnsVersion || SourceOf(generation) is not { } source || source.RowSequenceVersion != rowSequenceVersion)
            {
                _menu = null;
                await SendStateAsync();
                return "{\"moved\":true}";
            }
            var declared = Declared(column)
                ?? throw new InvalidOperationException($"Internal: the browser opened the Context Menu on column '{column}', which the columns do not declare.");
            var context = new ContextMenuContext<TRow>(asked, column, declared.Type, ranges, rowSequenceVersion, [], CloseMenu);
            var commands = ContextCommands is { } answer ? answer(context).ToList() : [];
            _menu = (number, commands);
            return Json(json =>
            {
                json.WriteStartArray();
                foreach (var command in commands)
                {
                    json.WriteStartObject();
                    json.WriteString("id", command.Id);
                    json.WriteBoolean("enabled", command.Enabled);
                    if (ContextCommandLabel?.Invoke(command.Id) is { } words)
                        json.WriteString("label", words);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            });
        }, Answers: true, SendsState: false));
    }

    // The menu is the Script Core's, closed by its own keys and presses, and by a command run.
    private static void CloseMenu() =>
        throw new NotSupportedException("The Script Core's Context Menu closes itself as a command runs; a command does not close it (ADR-0421).");

    /// <summary>Runs the command at <paramref name="index"/> of the Context Menu numbered <paramref name="number"/>,
    /// pressed in the browser (ADR-0421), in its turn among the events. What it writes to the source crosses before
    /// the browser hears it ran, so the input the browser held behind it is taken against what it left.</summary>
    internal Task<string?> RunContextCommandAsync(long number, int index) => InTurnAsync(new Heard(async () =>
    {
        if (_menu is not { } menu || menu.Number != number)
            throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser ran a command of the Context Menu numbered {number}, which is not the last one it asked."));
        if (index < 0 || index >= menu.Commands.Count)
            throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser ran command {index} of a menu of {menu.Commands.Count}."));
        var command = menu.Commands[index];
        if (!command.Enabled)
            throw new InvalidOperationException($"Internal: the browser ran the command '{command.Id}', which the Consumer answered disabled.");
        // The menu closed as the command ran: what its commands hold is let go.
        _menu = null;
        await command.Invoke();
        return null;
    }, Answers: true, SendsState: true));

    /// <summary>The error step the browser numbered <paramref name="number"/> (ADR-0381 section 3), asked of
    /// <see cref="OnFindError"/> in its turn among the events, and written as the core reads a Find result. A step
    /// asked of a source since replaced, or under another order, is not handed to the Consumer, which would read
    /// positions in an order they were not taken in: it is answered as moved, after what the source owes the browser,
    /// which the browser takes in before it says the rows moved. A step the user gave up is not asked, and neither is
    /// one asked as the Consumer took <see cref="OnFindError"/> away, which is answered as withdrawn.</summary>
    internal Task<string?> FindErrorAsync(long number, int generation, JsonElement request)
    {
        var asked = new Validation.GridErrorStepRequest
        {
            Backward = request.GetProperty("backward").GetBoolean(),
            From = ScriptJson.Present(request, "from", out var from) ? ScriptJson.Cell(from) : null,
            Columns = ScriptJson.Texts(request.GetProperty("columns")),
            RowSequenceVersion = request.GetProperty("rowSequenceVersion").GetInt32(),
        };
        return InTurnAsync(new Heard(Answered(number, async cancelled =>
        {
            // Read in its turn: the Consumer may have taken it away, or handed another, since the browser asked.
            if (OnFindError is not { } find)
                return """{"withdrawn":true}""";
            Finding.GridFindResult? found = null;
            if (SourceOf(generation) is { } source && source.RowSequenceVersion == asked.RowSequenceVersion)
            {
                found = await find(asked, cancelled);
                cancelled.ThrowIfCancellationRequested();
            }
            return await FoundJsonAsync(found);
        }), Answers: true, SendsState: false));
    }

    /// <summary>The Find step the browser numbered <paramref name="number"/> (ADR-0055), asked of the source's
    /// <see cref="IGridSource{TRow}.FindAsync"/> in its turn among the events, as an error step is asked of
    /// <see cref="OnFindError"/>: one asked of a source since replaced, or under another order, is answered as moved,
    /// after what the source owes the browser. <see cref="IGridSource{TRow}.CanFind"/> is read once for each source, as
    /// it is handed to the browser.</summary>
    internal Task<string?> FindAsync(long number, int generation, JsonElement request)
    {
        var asked = new Finding.GridFindRequest
        {
            Text = ScriptJson.Text(request, "text"),
            MatchCase = request.GetProperty("matchCase").GetBoolean(),
            WholeCell = request.GetProperty("wholeCell").GetBoolean(),
            Backward = request.GetProperty("backward").GetBoolean(),
            From = ScriptJson.Present(request, "from", out var from) ? ScriptJson.Cell(from) : null,
            Scope = ScriptJson.Present(request, "scope", out var scope) ? ScriptJson.Ranges(scope) : null,
            Columns = ScriptJson.Texts(request.GetProperty("columns")),
            RowSequenceVersion = request.GetProperty("rowSequenceVersion").GetInt32(),
        };
        return InTurnAsync(new Heard(Answered(number, async cancelled =>
        {
            Finding.GridFindResult? found = null;
            if (SourceOf(generation) is { } source && source.RowSequenceVersion == asked.RowSequenceVersion)
            {
                // The browser was told CanFind once, as the source was handed over: a source that stops searching
                // while it stands is said, never answered as "none found" or as rows that moved.
                if (!source.CanFind)
                    throw new InvalidOperationException($"{source.GetType().Name} stopped answering Find (CanFind is false) after the grid was told it searches: "
                        + "ExGrid over the Script Core reads CanFind once for each source it is handed (ADR-0055).");
                found = await source.FindAsync(asked, cancelled);
                cancelled.ThrowIfCancellationRequested();
            }
            return await FoundJsonAsync(found);
        }), Answers: true, SendsState: false));
    }

    /// <summary>A step's answer as the core reads a Find result, or moved where it was not asked. The browser judges
    /// the answer against the source it holds when the answer lands: what changed before it, or while the source or
    /// the Consumer read, crosses ahead of it.</summary>
    private async Task<string?> FoundJsonAsync(Finding.GridFindResult? found)
    {
        if (_attached)
            await SendUpdateAsync();
        return Json(json =>
        {
            json.WriteStartObject();
            if (found is null)
            {
                json.WriteBoolean("moved", true);
            }
            else
            {
                json.WriteBoolean("found", found.IsFound);
                if (found.IsFound)
                {
                    json.WriteNumber("row", found.Row);
                    json.WriteString("column", found.Column);
                }
            }
            json.WriteEndObject();
        });
    }

    /// <summary>The user cancelled the commit numbered <paramref name="number"/> with Escape (ADR-0380 section 3).
    /// One already answered is not held any more, and nothing is left to call off.</summary>
    internal void CancelAnswer(long number)
    {
        if (_answers.TryGetValue(number, out var answering))
            answering.Cancel();
    }

    private static Func<Task<string?>> Unanswered(Func<Task> hand) => async () =>
    {
        await hand();
        return null;
    };

    /// <summary>An answer the browser numbered <paramref name="number"/> and waits on, registered now, as its call
    /// arrives, so the cancel the browser sends after it finds it whenever its turn comes. One the user cancelled
    /// before its turn is never asked of the Consumer, and one whose Consumer gives up on the token, as the token
    /// asks, is answered as cancelled: the browser reads no answer to either.</summary>
    private Func<Task<string?>> Answered(long number, Func<CancellationToken, Task<string?>> answer)
    {
        var answering = new CancellationTokenSource();
        if (!_answers.TryAdd(number, answering))
        {
            answering.Dispose();
            throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser numbered two answers {number}."));
        }
        return async () =>
        {
            try
            {
                if (answering.IsCancellationRequested)
                    return null;
                return await answer(answering.Token);
            }
            catch (OperationCanceledException) when (answering.IsCancellationRequested)
            {
                return null;
            }
            finally
            {
                _answers.Remove(number);
                answering.Dispose();
            }
        };
    }

    private async Task<string?> InTurnAsync(Heard heard)
    {
        await InvokeAsync(async () =>
        {
            _heard.Enqueue(heard);
            if (_hearing)
                return;
            _hearing = true;
            try
            {
                while (_heard.TryDequeue(out var next))
                {
                    try
                    {
                        if (_disposed)
                            continue;
                        var answer = await next.Hand();
                        if (next.SendsState && _attached)
                            await SendStateAsync();
                        next.Done.TrySetResult(answer);
                    }
                    catch (JSDisconnectedException)
                    {
                        // The circuit is gone, and the browser that raised the event with it: nobody is left to tell.
                    }
                    catch (Exception ex)
                    {
                        await DispatchExceptionAsync(ex);
                        if (next.Answers)
                            next.Done.TrySetException(ex);
                    }
                    finally
                    {
                        next.Done.TrySetResult(null);
                    }
                }
            }
            finally
            {
                _hearing = false;
            }
        });
        return await heard.Done.Task;
    }

    // Each intent carries the token the user's cancel cancels (ADR-0380 section 3): a handler that has not written
    // when it is cancelled writes nothing.
    private Func<Task<string?>> PrepareEdit(JsonElement payload)
    {
        var row = _rows.RowOf(payload.GetProperty("row").GetInt64());
        var column = Text(payload, "column");
        var value = Text(payload, "value");
        var seen = Text(payload, "seenText");
        var replaced = Text(payload, "replacedText");
        return Answered(payload.GetProperty("answer").GetInt64(), async cancelled =>
        {
            var intent = new GridEditIntent<TRow>(row, column, value) { SeenText = seen, ReplacedText = replaced, CancellationToken = cancelled };
            await OnEdit.InvokeAsync(intent);
            return intent.RefusalMessage;
        });
    }

    private Func<Task<string?>> PreparePaste(JsonElement payload)
    {
        var source = payload.GetProperty("source");
        var plan = new PastePlan(ScriptJson.Ranges(payload.GetProperty("targets")),
            new PasteShape(source.GetProperty("rows").GetInt32(), source.GetProperty("columns").GetInt32()),
            payload.GetProperty("collapsesSelection").GetBoolean());
        IReadOnlyList<IReadOnlyList<string>> values = [.. payload.GetProperty("values").EnumerateArray().Select(ScriptJson.Texts)];
        var version = payload.GetProperty("rowSequenceVersion").GetInt32();
        IReadOnlyList<IReadOnlyList<PasteFieldOrigin>> origins = [.. payload.GetProperty("origins").EnumerateArray()
            .Select(row => (IReadOnlyList<PasteFieldOrigin>)[.. row.EnumerateArray().Select(ScriptJson.Member<PasteFieldOrigin>)])];
        var fillSource = ScriptJson.Present(payload, "fillSource", out var fill) ? ScriptJson.Range(fill) : (SelectionRange?)null;
        var enteredAt = ScriptJson.Present(payload, "enteredAt", out var entered) ? ScriptJson.Cell(entered) : (CellPosition?)null;
        return Answered(payload.GetProperty("answer").GetInt64(), async cancelled =>
        {
            var intent = new GridPasteIntent(plan, values, version, origins, fillSource, enteredAt) { CancellationToken = cancelled };
            await OnPaste.InvokeAsync(intent);
            return intent.IsRefused ? "" : null;
        });
    }

    private Func<Task<string?>> PrepareFill(JsonElement payload)
    {
        var source = ScriptJson.Range(payload.GetProperty("source"));
        var extension = new FillExtension(ScriptJson.Range(payload.GetProperty("target")), ScriptJson.Member<GridDirection>(payload.GetProperty("direction")));
        var version = payload.GetProperty("rowSequenceVersion").GetInt32();
        return Answered(payload.GetProperty("answer").GetInt64(), async cancelled =>
        {
            var intent = new GridFillIntent(source, extension, version) { CancellationToken = cancelled };
            await OnFill.InvokeAsync(intent);
            return intent.IsRefused ? "" : null;
        });
    }

    private Func<Task> Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "selectionChanged":
            {
                var selection = ScriptJson.Selection(payload);
                return () => SelectionChanged.InvokeAsync(selection);
            }
            case "clear":
            {
                var intent = new GridClearIntent(ScriptJson.Ranges(payload.GetProperty("targets")), payload.GetProperty("rowSequenceVersion").GetInt32());
                return () => OnClear.InvokeAsync(intent);
            }
            case "undo":
                return () => OnUndo.InvokeAsync();
            case "redo":
                return () => OnRedo.InvokeAsync();
            case "refused":
            {
                var refusal = RefusalOf(payload);
                return () => OnRefused.InvokeAsync(refusal);
            }
            case "columnWidthChanged":
            {
                var change = new ColumnWidthChange(Text(payload, "column"), payload.GetProperty("widthPx").GetDouble());
                return () => OnColumnWidthChanged.InvokeAsync(change);
            }
            case "columnOrderChanged":
            {
                var order = ScriptJson.Texts(payload);
                return () => OnColumnOrderChanged.InvokeAsync(order);
            }
            case "columnHidden":
            {
                var column = payload.GetString() ?? throw new InvalidOperationException("Internal: a column hidden without its name.");
                return () => OnColumnHidden.InvokeAsync(column);
            }
            case "pinnedCountChanged":
            {
                var count = payload.GetInt32();
                return () => OnPinnedCountChanged.InvokeAsync(count);
            }
            case "headerGroupToggled":
            {
                var focusColumn = payload.TryGetProperty("focusColumn", out var named) ? named.GetString() : null;
                var toggle = new HeaderGroupToggle(Text(payload, "id"), payload.GetProperty("expanded").GetBoolean(), focusColumn);
                return () => OnHeaderGroupToggled.InvokeAsync(toggle);
            }
            case "editingChanged":
            {
                var open = payload.GetBoolean();
                return () => OnEditingChanged.InvokeAsync(open);
            }
            case "editDiscarded":
            {
                var reason = ScriptJson.Member<EditDiscardReason>(payload);
                return () => OnEditDiscarded.InvokeAsync(reason);
            }
            case "cellDoubleClick":
            {
                var cell = ScriptJson.Cell(payload);
                return () => OnCellDoubleClick.InvokeAsync(cell);
            }
            case "leave":
                return () => OnLeave.InvokeAsync();
            case "overwriteNotice":
            {
                var notice = new GridOverwriteNotice(
                    ScriptJson.Cell(payload.GetProperty("cell")), Text(payload, "column"), Text(payload, "seenText"), Text(payload, "replacedText"));
                return () => OnOverwriteNotice.InvokeAsync(notice);
            }
            case "action":
            {
                var action = new GridActionEventArgs<TRow>(
                    _rows.RowOf(payload.GetProperty("row").GetInt64()), Text(payload, "column"), Text(payload, "action"));
                return () => OnAction.InvokeAsync(action);
            }
            case "nameBoxEntered":
            {
                var typed = payload.GetString() ?? throw new InvalidOperationException("Internal: Enter in the Name Box without its text.");
                return () => OnNameBoxEntered.InvokeAsync(typed);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised '{kind}', which the adapter does not hear.");
        }
    }

    private GridRefusal<TRow> RefusalOf(JsonElement payload)
    {
        var reason = payload.GetProperty("reason");
        return Text(payload, "kind") switch
        {
            "copy" => new GridRefusal<TRow>.Copy(ScriptJson.Member<CopyRefusalReason>(reason)),
            "paste" => new GridRefusal<TRow>.Paste(ScriptJson.Member<PasteRefusalReason>(reason)),
            "commit" => new GridRefusal<TRow>.Commit(ScriptJson.Member<CommitRefusalReason>(reason), ScriptJson.Cell(payload.GetProperty("cell")), Text(payload, "column")),
            "find" => new GridRefusal<TRow>.Find(ScriptJson.Member<Finding.FindRefusalReason>(reason)),
            "mark" => new GridRefusal<TRow>.Mark(ScriptJson.Member<Rows.MarkRefusalReason>(reason)),
            "placement" => new GridRefusal<TRow>.Placement(ScriptJson.Member<PlacementRefusalReason>(reason)),
            "action" => new GridRefusal<TRow>.Action(ScriptJson.Member<ActionRefusalReason>(reason),
                ScriptJson.Present(payload, "row", out var row) ? _rows.RowOf(row.GetInt64()) : null,
                Text(payload, "column"), Text(payload, "action")),
            var other => throw new InvalidOperationException($"Internal: the Script Core refused a gesture of the kind '{other}'."),
        };
    }

    private static string Text(JsonElement json, string name) => ScriptJson.Text(json, name);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        Calls.Stopped();
        if (_source is not null)
        {
            _source.StateChanged -= OnSourceChanged;
            (_source as IBindsToOneCircuit)?.Binding.Detach(this);
        }
        // A question's continuation may run inside Cancel and take itself off the list.
        foreach (var asked in _asked.Values.ToArray())
            asked.Cancel();
        foreach (var answering in _answers.Values.ToArray())
            answering.Cancel();
        // While it attaches, the grid it makes is stopped once it is made, and the bridge let go after it.
        if (_attaching)
            return;
        await ReleaseAsync(_grid, _module);
        _bridge?.Dispose();
    }

    /// <summary>Stops the browser's grid, and lets go what .NET holds of the browser.</summary>
    private static async Task ReleaseAsync(IJSObjectReference? grid, IJSObjectReference? module)
    {
        try
        {
            if (grid is not null)
            {
                await grid.InvokeVoidAsync("dispose");
                await grid.DisposeAsync();
            }
            if (module is not null)
                await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone, or is closing and has called off what it had sent the browser, and the browser's
            // grid goes with it: nothing is left there to stop.
        }
    }
}
