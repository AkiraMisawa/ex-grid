using ExGrid.Cells;
using ExGrid.Chrome;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet.Components;

/// <summary>
/// A Sheet drawn by ExGrid as that grid's Consumer (ADR-0046). Its markup and most of its wiring are
/// in <c>ExSheet.razor</c>; this part joins it to a Pointing Scope (ADR-0058). The Sheet tells its
/// Scope whether it points — it holds the keyboard, has an edit open and is in Point — and the Scope
/// writes a press on one of its grids into this Sheet's edit, or has this Sheet tell its Consumer why
/// it wrote nothing. Whether the edit is open and in Point, the grid says in C# at the change
/// (<c>OnPointStateChanged</c>). Where DOM focus is, C# learns only from the browser's focus events,
/// as it is told of them: nothing is measured, and no script is added (ADR-0021).
/// </summary>
public partial class ExSheet : IPointingSheet, IDisposable
{
    /// <summary>
    /// The Pointing Scope this Sheet belongs to (ADR-0058), or null — the default — for none. While
    /// this Sheet holds the keyboard, has an edit open and is in Point — its caret stands where a
    /// Reference can go, or what Point wrote there still stands — the grids registered in the Scope
    /// are pointed at: a press on a cell writes <c>XLOOKUP(&lt;key&gt;, T[&lt;key column&gt;],
    /// T[&lt;column&gt;])</c> where Point writes, and a press on a column header <c>T[&lt;column&gt;]</c>,
    /// in the table's column names as this Sheet declares them. A further press, on a registered grid
    /// or on this Sheet, replaces what this Point wrote. A press that stands for anything else writes
    /// nothing, and is told through <see cref="OnPointingRefused"/>. When the keyboard leaves this
    /// Sheet, no grid is pointed at, and the edit stands.
    ///
    /// <para>After such a press the Name Box is empty, F4 changes nothing, and the arrow keys point
    /// inside the grid pressed: ↑ and ↓ one row further in its current order, ← and → to the next
    /// column its table has, rewriting what this Point wrote, and the grid scrolls the cell into view.
    /// After a press on a column header, ↓ points at the column's first row, and ← and → at the next
    /// column its table has, as a column, which the grid scrolls into view across; ↑ moves nothing. At
    /// an edge nothing moves; a row that has not arrived, Shift and an arrow, and Ctrl and an arrow
    /// write nothing and are told through <see cref="OnPointingRefused"/>. Give several Sheets the same
    /// Scope and each points in turn, while it holds the keyboard; a Sheet is never pointed at.</para>
    ///
    /// <para>The Scope also draws in its grids (ADR-0058, "What is drawn"): while a Formula is edited
    /// here, the Linked Table columns it reads are outlined in the grids registered for their table,
    /// in the colours <see cref="OnLinkedColumnColoursChanged"/> tells, until the edit ends; and the
    /// cell or column a press wrote for is dashed until Point over what it wrote ends.</para>
    /// </summary>
    [Parameter] public PointingScope? PointingScope { get; set; }

    /// <summary>
    /// Tells the Consumer that a press on a grid of <see cref="PointingScope"/> wrote nothing into the
    /// Formula being edited, and why (ADR-0058): more than one cell or column, a Header Group, a
    /// column the table does not have, a table declared without a key, a blank key, a row not yet
    /// arrived. A drag that reached another cell took back what its press wrote. After a press, an
    /// arrow key that wrote nothing is told too: Shift and an arrow, Ctrl and an arrow, a row not yet
    /// arrived, or a cell or column the grid no longer shows; after a press on a column's header, a
    /// first row that cannot be written. The Formula's text is as it was before the press or the
    /// key. ExSheet shows nothing of it itself: show the
    /// <see cref="PointingRefusal.Message"/> where the page tells its user things.
    /// </summary>
    [Parameter] public EventCallback<PointingRefusal> OnPointingRefused { get; set; }

    // The Scope this Sheet joined, which is left when another is given.
    private PointingScope? _joinedScope;

    // What the grid last said of the open edit's Point, whether DOM focus is inside this Sheet as
    // the browser last said, and what the Scope last heard.
    private PointState _pointState;
    private bool _holdsKeyboard;
    private bool _scopeHeardPoints;

    // The focus listeners on the Sheet's element, present only while a Scope is given, so a Sheet in
    // none sends the browser's focus events nowhere.
    private readonly Dictionary<string, object> _keyboardListeners = new(StringComparer.Ordinal);
    private KeyboardListener? _keyboardListener;
    private EventCallback<PointState> _pointStateChanged;
    private EventCallback<GridPointArrow> _pointArrow;

    /// <summary>Hears the grid's word of its edit's Point, and the arrow keys pressed while what a
    /// press on a registered grid wrote stands there: no receiver, since nothing this component
    /// paints changes with either.</summary>
    private void InitialisePointing()
    {
        _pointStateChanged = new EventCallback<PointState>(null, (Action<PointState>)(state =>
        {
            _pointState = state;
            TellScopeWhetherPointing();
            // Whether or not this Sheet holds the keyboard: the dashes go once Point over what a
            // press wrote has ended, and only then (ADR-0058, "What is drawn").
            _joinedScope?.PointStateChanged(this, state);
        }));
        // The arrows point inside the grid the press landed on (ADR-0058, "The keyboard").
        _pointArrow = new EventCallback<GridPointArrow>(null, (Func<GridPointArrow, Task>)(arrow =>
            _joinedScope is { } scope ? scope.PointArrowAsync(this, arrow) : Task.CompletedTask));
    }

    /// <summary>Joins the Scope given, leaving one no longer given, and listens to the keyboard only
    /// while in one.</summary>
    private void BindPointingScope()
    {
        if (ReferenceEquals(_joinedScope, PointingScope))
            return;
        _joinedScope?.Leave(this);
        _scopeHeardPoints = false;
        _joinedScope = PointingScope;
        if (_joinedScope is null)
        {
            // Unheard from here on: what was last heard of the keyboard would go stale.
            _keyboardListeners.Remove("onfocusin");
            _keyboardListeners.Remove("onfocusout");
            _keyboardListeners.Remove(ClaimsAttribute);
            _holdsKeyboard = false;
            return;
        }
        _joinedScope.Join(this);
        // The columns an edit already open reads are outlined in the new Scope's grids.
        _joinedScope.OutlineLinkedColumns(this, _linkedColumnsTold);
        // A delegate whose target is not the component: Blazor re-renders a handler's target after
        // every event, and a focus change here paints nothing.
        _keyboardListener ??= new KeyboardListener(this);
        _keyboardListeners["onfocusin"] = (Func<Task>)_keyboardListener.In;
        _keyboardListeners["onfocusout"] = (Func<Task>)_keyboardListener.Out;
        // The grid's listener tells the keyboard crossing the Sheet's edge, numbered in its order, only
        // while these handlers are here to take it: a number told that nothing takes holds every input
        // behind it (ADR-0240 section 6).
        _keyboardListeners[ClaimsAttribute] = KeyboardCrossingClaims;
        TellScopeWhetherPointing();
    }

    /// <summary>Tells the Scope whether this Sheet points, when that changed.</summary>
    private void TellScopeWhetherPointing()
    {
        if (_joinedScope is not { } scope)
            return;
        var points = _holdsKeyboard && _pointState != PointState.None;
        if (points == _scopeHeardPoints)
            return;
        _scopeHeardPoints = points;
        scope.Points(this, points);
    }

    // The Input Root's claims (ADR-0240 section 6): the keyboard coming into the Sheet and leaving it.
    private const string ClaimsAttribute = "data-ex-claims";
    private const string KeyboardCrossingClaims = "focusin focusout";

    /// <summary>
    /// The keyboard came into the Sheet: in its place in the Input Order (ADR-0240 sections 1 and 6), as
    /// the grid's listener told it crossing the Sheet's edge. A move of DOM focus inside the Sheet — from
    /// the grid's root to the Cell Editor as = opens it, from the toolbar to the grid — crosses nothing, is
    /// not told, and changes nothing here; except the first such move of a keyboard that came in untold,
    /// before the Sheet listened or as the page loaded, which is told as its arrival.
    /// </summary>
    private Task OnKeyboardIn() => InKeyboardOrderAsync(GridInputKind.KeyboardIn, () =>
    {
        _holdsKeyboard = true;
        TellScopeWhetherPointing();
    });

    /// <summary>
    /// The keyboard left the Sheet: in its place in the Input Order (ADR-0240 sections 1 and 6). Taken
    /// after the inputs before it, and before the inputs after it: a press on another grid of the Scope
    /// that took the keyboard is that grid's own once this is handled, and one the keys before it still
    /// point with is pointed. Nothing waits for the browser's next turn: the move inside the Sheet that
    /// a focusout followed by a focusin made is never told as a departure, so the wait for the browser's
    /// turn that told the two apart before (input-order ticket 04) is gone.
    /// </summary>
    private Task OnKeyboardOut() => InKeyboardOrderAsync(GridInputKind.KeyboardOut, () =>
    {
        _holdsKeyboard = false;
        TellScopeWhetherPointing();
    });

    // The keyboard's crossing, handled in the grid's order; with no grid yet, there is nothing to order
    // it among, and it is heard at once.
    private Task InKeyboardOrderAsync(GridInputKind kind, Action heard)
    {
        if (_grid is { } grid)
        {
            return grid.InOrderAsync(kind, () =>
            {
                heard();
                return Task.CompletedTask;
            });
        }
        heard();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    string? IPointingSheet.RootId => _grid?.RootId;

    /// <inheritdoc />
    Task<bool> IPointingSheet.WritePointedTextAsync(string text)
        => _grid is { } grid ? grid.WritePointedTextAsync(text) : Task.FromResult(false);

    /// <inheritdoc />
    Task<bool> IPointingSheet.TakeBackPointedTextAsync()
        => _grid is { } grid ? grid.TakeBackPointedTextAsync() : Task.FromResult(false);

    /// <inheritdoc />
    Task IPointingSheet.TellPointingRefusedAsync(PointingRefusal refusal)
        => OnPointingRefused.InvokeAsync(refusal);

    /// <summary>Leaves the Pointing Scope, and stops keeping the Sheet Day: a disposed Sheet points at nothing and reads no clock.</summary>
    public void Dispose()
    {
        _joinedScope?.Leave(this);
        _joinedScope = null;
        StopTheDay();
        GC.SuppressFinalize(this);
    }

    /// <summary>The focus handlers' target: not the component, so a focus event does not re-render
    /// it (Blazor re-renders a handler's target when the target handles events).</summary>
    private sealed class KeyboardListener(ExSheet sheet)
    {
        public Task In() => sheet.OnKeyboardIn();

        public Task Out() => sheet.OnKeyboardOut();
    }
}
