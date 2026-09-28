using ExGrid.Selection;

namespace ExGrid.Keys;

/// <summary>What the core does with a key it has taken (ADR-0012 / ADR-0011 / ADR-0020).</summary>
public enum GridKeyKind
{
    /// <summary>Not the core's — it passes through to the browser and to whatever is
    /// inside the cell.</summary>
    None = 0,

    /// <summary>Arrow: collapse and move.</summary>
    Move,

    /// <summary>Shift+arrow: the Focus stays and the Extent moves (ADR-0052).</summary>
    Extend,

    /// <summary>Ctrl+arrow, Home, End: jump to the edge.</summary>
    MoveToEdge,

    /// <summary>Ctrl+Shift+arrow, Shift+Home, Shift+End: extend to the edge.</summary>
    ExtendToEdge,

    /// <summary>Enter / Tab and their Shift forms: move the Focus inside the selection.</summary>
    Cycle,

    /// <summary>Ctrl+A.</summary>
    SelectAll,

    /// <summary>Ctrl+Space.</summary>
    SelectWholeColumns,

    /// <summary>Shift+Space.</summary>
    SelectWholeRows,

    /// <summary>Ctrl+Home / Ctrl+End: the first or last cell of the whole result.</summary>
    MoveToCorner,

    /// <summary>PageUp / PageDown: collapse and move the Focus by the rows fully
    /// visible, with the Viewport moving by the same number (ADR-0012).</summary>
    MoveByViewport,

    /// <summary>Shift+PageUp / Shift+PageDown: extend by the same rows.</summary>
    ExtendByViewport,

    /// <summary>Space: engage with the cell's content (ADR-0020).</summary>
    Engage,

    /// <summary>Escape: leave the grid, which is the way out of Tab's cycle.</summary>
    Leave,

    /// <summary>The Context Menu key and Shift+F10: open the context menu on the Focus
    /// cell (ADR-0036). A menu only a mouse can reach would fail the same test ADR-0034's
    /// error popover has to pass.</summary>
    OpenContextMenu,

    /// <summary>Alt+↓: open the column menu of the Focus's column — the key Excel opens a
    /// header's filter drop-down with (ADR-0039). Before it, only a pointer on ▾ could.</summary>
    OpenColumnMenu,

    /// <summary>Ctrl+Z: forwarded to the Consumer, who owns the history (ADR-0007). Claimed
    /// only while <see cref="GridKeyClaims.CanUndo"/>.</summary>
    Undo,

    /// <summary>Ctrl+Y or Ctrl+Shift+Z: forwarded like <see cref="Undo"/>. Claimed only
    /// while <see cref="GridKeyClaims.CanRedo"/>.</summary>
    Redo,

    /// <summary>Delete: one Clear Intent over the selection (ADR-0054). Claimed only on a
    /// grid with an editable column.</summary>
    Clear,

    /// <summary>Backspace: Overwrite on the Focus with nothing in it — Excel's "clear this
    /// cell and type" (ADR-0035). Claimed only on a grid with an editable column.</summary>
    ClearAndEdit,

    /// <summary>Ctrl+D: fill the range's top row down it (ADR-0035). Claimed only on a grid
    /// with an editable column.</summary>
    FillDown,

    /// <summary>Ctrl+R: fill the range's left column across it (ADR-0035). Claimed only on
    /// a grid with an editable column.</summary>
    FillRight,

    /// <summary>Ctrl+F: the find panel, or a refusal where nothing can search (ADR-0055).
    /// Claimed on every grid.</summary>
    Find,

    /// <summary>Ctrl+. (period): move the Focus to the next corner of the range holding it,
    /// clockwise; the Selection does not change (ADR-0052).</summary>
    MoveFocusToNextCorner,

    /// <summary>Ctrl+Backspace: scroll the Focus into view and change nothing else
    /// (ADR-0052).</summary>
    RevealFocus,

    /// <summary>Shift+Backspace: collapse the Selection to the Focus (ADR-0052).</summary>
    CollapseToFocus,
}

/// <summary>
/// One taken key, resolved to what it means. A value, so the table can be a constant and
/// the component's switch has nothing to re-derive.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Direction">Which way, for the four movement kinds.</param>
/// <param name="Order">Enter runs down columns, Tab runs across rows (ADR-0012).</param>
/// <param name="Backward">Shift+Enter / Shift+Tab, and the corner Ctrl+Home takes.</param>
public readonly record struct GridKeyAction(
    GridKeyKind Kind,
    GridDirection Direction = GridDirection.Down,
    CycleOrder Order = CycleOrder.ColumnMajor,
    bool Backward = false)
{
    /// <summary>The key means nothing to the core — what every key outside the table
    /// resolves to.</summary>
    public static GridKeyAction None { get; } = new(GridKeyKind.None);
}
