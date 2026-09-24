using Microsoft.AspNetCore.Components;

using ExGrid.Selection;

namespace ExGrid.Chrome;

/// <summary>Whether a column's filter can offer a list of values, only conditions, or
/// both — declared by the Consumer in the column definition, because only the Consumer
/// knows the cardinality (ADR-0009).</summary>
public enum FilterUiMode
{
    /// <summary>The safe default: no enumeration is ever asked for.</summary>
    Condition = 0,

    ValueList,

    Both,
}

/// <summary>The two editing states the arrow keys mean different things in (ADR-0010).</summary>
public enum CellEditMode
{
    Overwrite,
    Caret,
}

/// <summary>
/// A distinct-value answer: the values, or TooMany — the runtime safety net for a
/// declaration that drifted from reality, on which the panel degrades to condition
/// mode without breaking (ADR-0009).
/// </summary>
public sealed class DistinctValues
{
    private DistinctValues(bool isTooMany, IReadOnlyList<object?> values)
    {
        IsTooMany = isTooMany;
        Values = values;
    }

    public static DistinctValues TooMany { get; } = new(true, []);

    public static DistinctValues Of(IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(false, values);
    }

    public bool IsTooMany { get; }

    /// <summary>Empty when <see cref="IsTooMany"/> — an unanswerable list is not an
    /// empty domain.</summary>
    public IReadOnlyList<object?> Values { get; }
}

/// <summary>One command in the column menu (ADR-0010). The core decides the list;
/// Chrome only lays them out. <see cref="Id"/> is stable — the hook for icons and for
/// a Consumer substituting a particular command.</summary>
/// <summary>
/// One item of a menu the core decides (ADR-0010/0036). It carries no label: the core
/// names a command and says whether it is available, and what it is called is
/// rendering, which is Chrome's. A substituted Chrome resolves the wording from the
/// <paramref name="Id"/>; the built-in menu resolves it through
/// <c>ExGrid.CommandLabel</c>, falling back to <see cref="BuiltInCommandLabels"/>.
/// </summary>
public sealed record GridCommand(string Id, bool Enabled, Func<Task> Invoke);

/// <summary>
/// Everything a filter panel substitute needs (ADR-0009): the column, the condition in
/// force, the operators the core allows, the declared UI mode, the pull for distinct
/// values, and the three exits. A substitute compiles against this alone.
/// </summary>
public sealed record FilterPanelContext(
    string Column,
    ColumnType Type,
    FilterSpec? Current,
    IReadOnlyList<FilterOperator> Allowed,
    FilterUiMode Mode,
    Func<Task<DistinctValues>> RequestDistinctValues,
    Action<FilterSpec?> Apply,
    Action Clear,
    Action Close);

/// <summary>The column menu's contract (ADR-0010): the core decides the items.</summary>
public sealed record ColumnMenuContext(
    string Column,
    ColumnType Type,
    IReadOnlyList<GridCommand> Commands,
    Action Close);

/// <summary>
/// The context menu's contract (ADR-0036). The core decides the items and the Consumer
/// extends them, exactly as for the column menu; what is different is the target.
///
/// The clicked cell arrives as a <b>row instance</b>, because a secondary click is
/// inside the Window by construction. The selection arrives as <b>rectangles in index
/// space</b> and the version they are written in (ADR-0011) — not as rows, and it
/// cannot be: a selection legitimately covers rows outside the Window and rows never
/// fetched. The Consumer holds the data, so resolving an index into a row is its
/// question, asked of the source it already has.
/// </summary>
public sealed record ContextMenuContext<TRow>(
    TRow Row,
    string Column,
    ColumnType Type,
    IReadOnlyList<SelectionRange> Selection,
    int RowSequenceVersion,
    IReadOnlyList<GridCommand> Commands,
    Action Close);

/// <summary>
/// A cell's message, while it is showing (ADR-0034): the Consumer's sentence for a
/// flagged cell, or the standing Reject's while an editor holds. The core decides when
/// it opens and closes and supplies the text; Chrome draws it. It is a popover and never
/// an element in the row — the row's height does not move (ADR-0013).
/// </summary>
public sealed record CellMessageContext(string Column, string Message, bool IsEditorError, Action Close);

/// <summary>Receive and render (ADR-0010). Placeholders are one mechanism, so Chrome
/// does not distinguish waiting from skipping either.</summary>
public sealed record LoadingContext(bool IsLoading, int PlaceholderRowCount);

/// <summary>
/// The Cell Editor seam (ADR-0010). The core owns the box, the keys and the
/// uncommitted text's lifecycle; the Chrome renders the control. The control reports
/// its text through <see cref="TextChanged"/> as it changes — Enter, Tab, Esc and the
/// Overwrite arrows never reach it (the capture phase takes them), so the core must
/// already hold the text when one of them commits.
///
/// <para><see cref="Error"/> is the standing Reject's message while the editor holds
/// (ADR-0034), for the substitute to paint <c>aria-invalid</c> and its own border.
/// Chrome still cannot veto: <see cref="Commit"/> stays argument-less and the decision
/// to hold is the core's.</para>
/// </summary>
public sealed record CellEditorContext(
    string Column,
    ColumnType Type,
    string InitialText,
    CellEditMode Mode,
    string? Error,
    Action<string> TextChanged,
    Action Commit,
    Action Cancel,
    string? MessageId = null,
    int FocusRequest = 0)
{
    /// <summary>The id of the popover carrying <see cref="Error"/> while a Reject stands
    /// (ADR-0034), for the control's <c>aria-describedby</c>; null when nothing is
    /// shown.</summary>
    public string? MessageId { get; init; } = MessageId;

    /// <summary>Counts the core's requests that the control take DOM focus — on opening,
    /// on F2, and after a Reject that a click-away had moved focus off it. A substitute
    /// focuses its control whenever this changes; the core holds no reference to a
    /// control it did not render (ADR-0010/0030).</summary>
    public int FocusRequest { get; init; } = FocusRequest;
}

/// <summary>
/// The substitutable UI seams (ADR-0009/0010): the filter panel, the column menu, the
/// cell editor and the loading indicator. One rule throughout: Chrome renders and
/// calls back; it does not decide meaning — the commands, the allowed operators and
/// what a filter means are the core's, which is why substituting Chrome cannot change
/// behaviour.
/// </summary>
public interface IGridChrome
{
    /// <summary>Null falls back to the core's built-in panel.</summary>
    RenderFragment? FilterPanel(FilterPanelContext context);

    /// <summary>Null falls back to the core's built-in menu.</summary>
    RenderFragment? ColumnMenu(ColumnMenuContext context);

    /// <summary>Null falls back to the core's built-in menu (ADR-0036).</summary>
    RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context) => null;

    /// <summary>Null falls back to the core's built-in popover (ADR-0034).</summary>
    RenderFragment? CellMessage(CellMessageContext context) => null;

    /// <summary>Null falls back to the core's own floating input. A fragment is
    /// rendered inside the core's <c>ex-editor</c> box, which carries the padding,
    /// outline and background — the control fills it — and <b>the fragment focuses its
    /// own control</b>, when it appears and when <see cref="CellEditorContext.Mode"/>
    /// changes: the core holds no reference to a control it did not render, and does
    /// not try (ADR-0010/0030).</summary>
    RenderFragment? CellEditor(CellEditorContext context);

    /// <summary>Null falls back to the core's own loading presentation (the
    /// <c>ex-loading</c> class and the Placeholder rows).</summary>
    RenderFragment? LoadingIndicator(LoadingContext context);
}
