using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using ExGrid.Selection;

namespace ExGrid.Chrome;

/// <summary>Whether a column's filter can offer a list of values, only conditions, or
/// both — declared by the Consumer in the column definition, because only the Consumer
/// knows the cardinality (ADR-0009).</summary>
public enum FilterUiMode
{
    /// <summary>The safe default: no enumeration is ever asked for.</summary>
    Condition = 0,

    /// <summary>A list of the column's distinct values to tick, asked for when the panel
    /// opens; a TooMany answer degrades the panel to its condition form (ADR-0009).</summary>
    ValueList,

    /// <summary>The value list and the condition form both. The distinct values are asked
    /// for as with <see cref="ValueList"/>.</summary>
    Both,
}

/// <summary>The two editing states the arrow keys mean different things in (ADR-0010).</summary>
public enum CellEditMode
{
    /// <summary>Entered by typing onto a selected cell: the original value is replaced,
    /// and the arrow keys commit and move to the neighbouring cell.</summary>
    Overwrite,

    /// <summary>Entered with F2 or a double click: the original value stays, and the
    /// arrow keys move the caret within the text.</summary>
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

    /// <summary>The answer that the column has too many distinct values to list. The
    /// panel degrades to its condition form rather than breaking (ADR-0009).</summary>
    public static DistinctValues TooMany { get; } = new(true, []);

    /// <summary>The distinct values themselves — under every applied filter except the
    /// column's own (ADR-0009). A null entry stands for the Blanks.</summary>
    public static DistinctValues Of(IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(false, values);
    }

    /// <summary>Whether this is the <see cref="TooMany"/> answer.</summary>
    public bool IsTooMany { get; }

    /// <summary>Empty when <see cref="IsTooMany"/> — an unanswerable list is not an
    /// empty domain.</summary>
    public IReadOnlyList<object?> Values { get; }
}

/// <summary>
/// One item of a menu the core decides (ADR-0010/0036). It carries no label: the core
/// names a command and says whether it is available, and what it is called is
/// rendering, which is Chrome's. A substituted Chrome resolves the wording from the
/// <paramref name="Id"/>; the built-in menu resolves it through
/// <c>ExGrid.CommandLabel</c>, falling back to <see cref="BuiltInCommandLabels"/>.
///
/// <para><paramref name="Id"/> is stable — the hook for icons and for a Consumer
/// substituting a particular command.</para>
/// </summary>
public sealed record GridCommand(string Id, bool Enabled, Func<Task> Invoke);

/// <summary>
/// Everything a filter panel substitute needs (ADR-0009): the column, the condition in
/// force, the operators the core allows, the declared UI mode, the pull for distinct
/// values, and the three exits. A substitute compiles against this alone.
///
/// <para>The panel stands under the column's commands in the one popover Alt+↓ and the ▾
/// open (ADR-0044), which opens with the keyboard on the commands. <see cref="FocusRequest"/>
/// counts the requests for the panel to take it — Tab from the commands — from zero at each
/// opening, where nothing is asked: whenever it changes, the panel's contents put DOM focus on
/// their first control. <see cref="FocusLastRequest"/> is the same for their last control,
/// where Shift+Tab from the commands wraps, and <see cref="SearchRequest"/> for the field a
/// search is typed into — Excel's E: the value list's search box, or the condition's value
/// where no list stands. The core holds no reference to a control it did not render, and does
/// not try — the same rule as <see cref="CellEditorContext.FocusRequest"/> (ADR-0039). Tab
/// off either end of the panel is the core's: it renders the popover's wrap around the
/// contents, so the contents need none of their own.</para>
///
/// <para><see cref="Format"/> is the column's own, so a value list shows each value as the
/// cells show it — null where the column declares none. What the choices mean is
/// <see cref="FilterPanelChoices"/>', the rules the built-in panel applies by.</para>
///
/// <para><see cref="InnerPopupChanged"/> is how the contents report a popup of their own —
/// a select's list, a picker's calendar — opening (<c>true</c>) and closing (<c>false</c>).
/// While one is open, Escape is the popup's: the grid leaves it to the control, and the next
/// one closes the panel (ADR-0039). Contents that open no popup never call it.</para>
///
/// <para><see cref="ValueListKey"/> is where the contents hand a key pressed on their value
/// list — its checkboxes, "(Select All)" — for Excel's letters (ADR-0044): S, O and C run
/// the commands above and close the popover, and E moves to the search box through
/// <see cref="SearchRequest"/>. The core decides, by <see cref="MenuKeys.Letter"/>,
/// and every other key means nothing to it; the task completes when what the key ran has.
/// A key in a text field is never handed over: a letter there is text. The element that
/// hands them over carries the class <c>ex-value-list</c>, by which the grid's key listener
/// knows the keys typed behind a letter there are to be held while it acts (ADR-0010).</para>
///
/// <para><see cref="Clear"/> removes the column's filter and closes. The one popover offers
/// it as the "clear-filter" command above the panel, so the panels this package and
/// <c>ExGrid.MudBlazor</c> ship draw no Clear of their own (ADR-0044).</para>
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
    Action Close,
    int FocusRequest = 0,
    Func<object, string>? Format = null,
    Action<bool>? InnerPopupChanged = null,
    int FocusLastRequest = 0,
    Func<KeyboardEventArgs, Task>? ValueListKey = null,
    int SearchRequest = 0);

/// <summary>The column menu's contract (ADR-0010): the core decides the items. They stand
/// at the top of the one popover Alt+↓ and the ▾ open, above the column's filter where it
/// can be filtered (ADR-0044). <see cref="FocusRequest"/> counts the requests for the menu to
/// take the keyboard — each opening, and each time Tab wraps back to the commands from the
/// filter below; the menu puts DOM focus on its first enabled item whenever it changes
/// (ADR-0039). Invoking a command also closes the popover and hands the keyboard back: the
/// Chrome only invokes, and calls <see cref="Close"/> for a dismissal of its own. What each
/// key means on an item is <see cref="MenuKeys"/>' column-menu table, asked through
/// <see cref="ResolveKey"/> — Excel's letters, and Tab into the filter, which the core
/// carries out itself (<see cref="MenuKeyKind.FilterFirst"/>, <see cref="MenuKeyKind.FilterLast"/>,
/// <see cref="MenuKeyKind.FilterSearch"/>). The core keeps the menu's
/// place, so a key is answered against where the keys have moved it, not against whichever
/// item holds DOM focus when it lands — on a Blazor Server circuit focus follows a round
/// trip behind (ADR-0039). A popup the menu's contents open of their own is reported
/// through <see cref="InnerPopupChanged"/>, as in the filter panel.</summary>
public sealed record ColumnMenuContext(
    string Column,
    ColumnType Type,
    IReadOnlyList<GridCommand> Commands,
    Action Close,
    int FocusRequest = 0,
    Action<bool>? InnerPopupChanged = null,
    MenuKeyResolver? ResolveKey = null);

/// <summary>
/// A key pressed on a menu's item, answered by the core against the place it keeps for
/// the open menu (ADR-0039): <paramref name="key"/> is <c>KeyboardEvent.key</c>,
/// <paramref name="shift"/> the Shift state, and <paramref name="controlAltOrMeta"/>
/// whether any other modifier was held. A <see cref="MenuKeyKind.Move"/> has already
/// moved that place; the contents focus the item it names, invoke the command a
/// <see cref="MenuKeyKind.Run"/> names, and close on a <see cref="MenuKeyKind.Close"/>.
/// </summary>
public delegate MenuKey MenuKeyResolver(string key, bool shift, bool controlAltOrMeta);

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
///
/// <para><see cref="FocusRequest"/> counts the openings; the menu puts DOM focus on its
/// first enabled item whenever it changes (ADR-0039). As in the column menu, invoking a
/// command closes the menu and hands the keyboard back, and <see cref="MenuKeys"/> says
/// what each key means on an item.</para>
/// </summary>
public sealed record ContextMenuContext<TRow>(
    TRow Row,
    string Column,
    ColumnType Type,
    IReadOnlyList<SelectionRange> Selection,
    int RowSequenceVersion,
    IReadOnlyList<GridCommand> Commands,
    Action Close,
    int FocusRequest = 0,
    Action<bool>? InnerPopupChanged = null,
    MenuKeyResolver? ResolveKey = null);

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
