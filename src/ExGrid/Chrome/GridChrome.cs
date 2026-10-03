using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using ExGrid.Cells;
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

/// <summary>The editing states the arrow keys mean different things in (ADR-0010/0051).</summary>
public enum CellEditMode
{
    /// <summary>Entered by typing onto a selected cell: the original value is replaced,
    /// and the arrow keys commit and move to the neighbouring cell.</summary>
    Overwrite,

    /// <summary>Entered with F2 or a double click: the original value stays, and the
    /// arrow keys move the caret within the text.</summary>
    Caret,

    /// <summary>Only where the Consumer declares it (ADR-0051): the arrow keys and the mouse
    /// point at cells — an outline moves over the grid, and the Consumer's Reference text for
    /// it is written at the caret. The Selection and the Focus do not move. F2 switches to
    /// <see cref="Caret"/>, and typing ends it.</summary>
    Point,
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
/// <para><see cref="Apply"/> is OK. It refuses an answer
/// <see cref="FilterPanelChoices.CanApply"/> says cannot be applied — an <c>In</c> with no
/// values, nothing ticked — and the panel stands; the panel shows its Apply unavailable
/// while its answer is that one, as Excel's OK is (ADR-0009, ticket 76).</para>
///
/// <para><see cref="Clear"/> removes the column's filter and closes. The one popover offers
/// it as the "clear-filter" command above the panel, so the panels this package and
/// <c>ExGrid.MudBlazor</c> ship draw no Clear of their own (ADR-0044).</para>
///
/// <para><see cref="DateType"/> is a Date column's declared date type (ADR-0023, section of
/// 2026-10-02): what a typed operand reads as, through
/// <see cref="FilterPanelChoices.ReadOperand"/>, and what an operand in <see cref="Current"/> is.
/// A panel hands that type to the engine or nothing: a <see cref="DateTime"/> on a
/// <see cref="ExGrid.DateType.DateOnly"/> column is refused when it is applied.</para>
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
    int SearchRequest = 0,
    DateType DateType = DateType.DateTime);

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
///
/// <para>On a Server circuit <see cref="InitialText"/> carries each reported text back a round
/// trip late. A control that writes it into its field puts that text back over the characters
/// typed since (SRV-5, ED-22). The Wrapper's controls bind their field with <c>@bind:get</c>
/// and <c>@bind:set</c> on <c>oninput</c>, render what the browser last reported, and take
/// only a text the core wrote itself; the same holds for the Formula Bar's and the Name Box's
/// controls.</para>
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
    int FocusRequest = 0,
    Func<Task>? TakeFocus = null)
{
    /// <summary>The id of the popover carrying <see cref="Error"/> while a Reject stands
    /// (ADR-0034), for the control's <c>aria-describedby</c>; null when nothing is
    /// shown.</summary>
    public string? MessageId { get; init; } = MessageId;

    /// <summary>Names the core's latest request that the control take DOM focus — on
    /// opening, on F2, and after a Reject that a click-away had moved focus off it. Zero asks
    /// nothing: an edit the Formula Bar opened keeps the keyboard in the bar, and its cell
    /// editor is handed zero until the core asks otherwise (ADR-0051). A substitute focuses
    /// its control when it is handed a non-zero request it has not answered yet — on mounting
    /// as on any later render; a request is never re-used, so a control mounted by one edit
    /// cannot mistake it for another's. The core holds no reference to a control it did not
    /// render (ADR-0010/0030), which is the rule <see cref="TemplateCellContext{TRow}.FocusRequest"/>
    /// follows too (ADR-0037). The control answers a request by calling <see cref="TakeFocus"/>,
    /// not by focusing itself.</summary>
    public int FocusRequest { get; init; } = FocusRequest;

    /// <summary>The core's focus function, which a control calls, once it is painted, to answer
    /// a <see cref="FocusRequest"/> it has not answered yet, instead of taking DOM focus itself
    /// (ADR-0010, ADR-0021's note of 2026-09-30). The core finds the control inside its own box
    /// and focuses it only while the keyboard is still this grid's: DOM focus inside the root or
    /// on nothing. On a circuit the request lands a round trip after the render that painted
    /// the control, and a grid or a control of the page's the user has pressed in the meantime
    /// keeps the keyboard; the edit is left standing (ADR-0018, section 6). Null only in a
    /// context the core did not make.</summary>
    public Func<Task>? TakeFocus { get; init; } = TakeFocus;

    /// <summary>
    /// The coloured text (ADR-0057): the editor's text with each Reference in its colour, drawn by
    /// the core, for the control to be painted over. The Chrome renders it <b>immediately before
    /// its control</b>, both directly inside the core's box; the core stands it over the box's
    /// content, which the control fills, in the box's font. A control with no padding and no
    /// background of its own then shows it through. It shows, and the control's own text turns
    /// transparent, only in the surface the edit is in — the control holding DOM focus, as Excel
    /// colours the cell's text or the Formula Bar's and not both — and only while it holds the
    /// control's value; the core's listener decides that, and the control does nothing. Null where
    /// no References function is declared: there is nothing to place.
    /// </summary>
    public RenderFragment? ReferenceText { get; init; }
}

/// <summary>
/// The find panel's contract (ADR-0055). The core owns the search — the request, the answer,
/// the Focus it moves — and the Chrome draws a field, the two options and the steps, and
/// reports what the user did. It is a popover by every popover rule (ADR-0039/0040): it takes
/// the keyboard through <see cref="FocusRequest"/>, which counts the requests for the field to
/// take DOM focus — each opening — and it is closed by Escape, a pointer-down elsewhere in the
/// instance, or <see cref="Close"/>, the keyboard returning to the root however it closes.
///
/// <para><see cref="Text"/>, <see cref="MatchCase"/> and <see cref="WholeCell"/> are what the
/// panel shows; the Chrome reports changes through <see cref="TextChanged"/>,
/// <see cref="MatchCaseChanged"/> and <see cref="WholeCellChanged"/>, and the core keeps them
/// across a close and reopen within the instance, as Excel's dialog keeps its last search.
/// <see cref="Next"/> and <see cref="Previous"/> run a step — Enter and Shift+Enter in the
/// field, and whatever buttons the Chrome draws. <see cref="Outcome"/> is what the last step
/// came to; the grid holds no sentence for it, so the Chrome words it, into a live region
/// (ADR-0033).</para>
/// </summary>
public sealed record FindContext(
    string Text,
    bool MatchCase,
    bool WholeCell,
    Finding.FindOutcome Outcome,
    Action<string> TextChanged,
    Action<bool> MatchCaseChanged,
    Action<bool> WholeCellChanged,
    Func<Task> Next,
    Func<Task> Previous,
    Action Close,
    int FocusRequest = 0,
    Action<bool>? InnerPopupChanged = null);

/// <summary>
/// What the grid hands a Consumer's own contents in its popover frame (ADR-0050, item 16;
/// <c>ExGrid.OpenPopoverAsync</c>). The frame is the core's, as every popover's is: it stands
/// inside the grid's box and is bounded by it, scrolling when the contents are taller
/// (ADR-0040), and Escape, a pointer-down elsewhere in the instance and the box shrinking below
/// one row each close it as a Cancel (ADR-0010/0040). However it closes, the keyboard returns
/// to the root (ADR-0039).
///
/// <para><see cref="FocusRequest"/> counts the requests for the contents to put DOM focus on
/// their first control: the opening, and Tab off their last control, which the frame wraps
/// back. <see cref="FocusLastRequest"/> counts the requests for their last control: Shift+Tab
/// off their first. The contents answer each change with Blazor's own <c>FocusAsync</c>, as a
/// popover's contents do (ADR-0039); the core holds no reference to an element it did not
/// render. <see cref="Close"/> is the contents' own way out, a Cancel as far as the grid is
/// concerned: what the contents did before calling it is theirs. A popup the contents open of
/// their own is reported through <see cref="InnerPopupChanged"/>, as in every popover.</para>
/// </summary>
/// <param name="Close">Closes the popover; the keyboard returns to the root.</param>
/// <param name="FocusRequest">Changes whenever the contents' first control is to take DOM focus.</param>
/// <param name="FocusLastRequest">Changes whenever the contents' last control is to take DOM focus;
/// zero until Shift+Tab first wraps.</param>
/// <param name="InnerPopupChanged">The contents report a popup of their own opening (<c>true</c>)
/// and closing (<c>false</c>); while one is open, Escape is the popup's (ADR-0039).</param>
public sealed record GridPopoverContext(
    Action Close,
    int FocusRequest,
    int FocusLastRequest,
    Action<bool> InnerPopupChanged);

/// <summary>
/// The Formula Bar's Name Box seam (ADR-0051, ADR-0010/0030). The core owns the box, its
/// width, the form whose implicit submission is Enter, and what the text means; the Chrome
/// renders the control. <see cref="Text"/> is what the box shows: what the user has typed
/// there and not yet entered, or otherwise the Consumer's label for the Focus. The control
/// reports what is typed through <see cref="TextChanged"/>, and Enter submits the core's form,
/// which hands the text to the Consumer. <see cref="Focused"/> is a press into the control:
/// an open edit commits first, as a press anywhere past the editor does, and a Reject takes
/// the keyboard back to the editor (ADR-0034). <see cref="Blurred"/> abandons what was typed.
/// The grid holds no sentence, so the control's accessible name is the Chrome's.
/// </summary>
/// <param name="Text">What the control shows.</param>
/// <param name="TextChanged">The control's text, as it changes.</param>
/// <param name="Focused">The control took DOM focus.</param>
/// <param name="Blurred">The control lost DOM focus.</param>
public sealed record NameBoxContext(
    string Text,
    Action<string> TextChanged,
    Func<Task> Focused,
    Action Blurred);

/// <summary>
/// The Formula Bar's text field seam (ADR-0051, ADR-0010/0030): the Cell Editor's second
/// surface, holding the one uncommitted text. The core owns the box, which wears the
/// editor's <c>ex-editor</c> class so the root's capture listener gates the keys typed in it
/// as the editor's (ADR-0018): Enter and Tab commit once, Escape cancels once, F2 switches
/// the mode. The Chrome renders the control inside it.
///
/// <para><see cref="Text"/> is the Focus cell's text: the uncommitted text while an edit is
/// open, otherwise the Consumer's opening text or the full value (ADR-0016). While
/// <see cref="ReadOnly"/>, the Focus cell does not edit and the control takes no typing.
/// <see cref="Focused"/> is a press into the control, which opens Caret on the Focus cell
/// when none is open. What is typed is reported through <see cref="TextChanged"/> and is
/// shown in the cell's editor on the next render. <see cref="FocusRequest"/> counts the core's
/// requests that the control take DOM focus while the user works in the bar, as
/// <see cref="CellEditorContext.FocusRequest"/> does for the cell.</para>
/// </summary>
/// <param name="Text">What the control shows.</param>
/// <param name="ReadOnly">Whether the control takes typing.</param>
/// <param name="Focused">The control took DOM focus.</param>
/// <param name="TextChanged">The control's text, as it changes.</param>
/// <param name="FocusRequest">Changes whenever the control is to take DOM focus. The control
/// answers a change by calling <paramref name="TakeFocus"/>, not by focusing itself.</param>
/// <param name="TakeFocus">The core's focus function, as <see cref="CellEditorContext.TakeFocus"/>
/// is for the cell's control: called once the control is painted, it focuses the control only
/// while the keyboard is still this grid's (ADR-0010, ADR-0021's note of 2026-09-30). Null only
/// in a context the core did not make.</param>
public sealed record FormulaBarTextContext(
    string Text,
    bool ReadOnly,
    Func<Task> Focused,
    Action<string> TextChanged,
    int FocusRequest = 0,
    Func<Task>? TakeFocus = null)
{
    /// <summary>
    /// The coloured text (ADR-0057), as <see cref="CellEditorContext.ReferenceText"/> is for the
    /// cell: rendered immediately before the control, directly inside the core's box. It stands
    /// whenever a References function is declared, and holds the edit's text while an edit is
    /// open on the Focus cell. Null where none is declared.
    /// </summary>
    public RenderFragment? ReferenceText { get; init; }

    /// <summary>
    /// Whether <see cref="Text"/> is not the Focus cell's own (ADR-0125): a cell an array spills
    /// into shows its Anchor's Formula. The control shows it dimmed, and it is read-only.
    /// </summary>
    public bool Borrowed { get; init; }
}

/// <summary>
/// The completion seam (ADR-0051/0039/0040): the candidates the Consumer offered for the
/// editor's text, and its argument hint, painted as the editor's Inner Popup inside the core's
/// box beneath the editor surface the user is typing in — the box, its place, its bound inside
/// the grid and the keys are the core's. ↑/↓ move <see cref="Selected"/>, Tab accepts it, and
/// Escape closes the list and leaves the edit open; the Chrome decides none of that and never
/// sees those keys (the capture phase takes them). A press on a candidate calls
/// <see cref="Accept"/> with its index; the box keeps DOM focus in the editor.
///
/// <para>Each candidate's element carries the id <see cref="OptionIdPrefix"/> followed by its
/// index, which the core's own editor names with <c>aria-activedescendant</c>. Either list may
/// be empty: an answer with only a hint paints only the hint.</para>
/// </summary>
/// <param name="Candidates">The candidates, in the Consumer's order.</param>
/// <param name="Selected">The index of the chosen candidate, or -1 when there are none.</param>
/// <param name="Hint">The argument hint, painted beneath the candidates, or null.</param>
/// <param name="Accept">Accepts the candidate at an index.</param>
/// <param name="OptionIdPrefix">The prefix of each candidate element's id.</param>
public sealed record EditorCompletionContext(
    IReadOnlyList<CompletionCandidate> Candidates,
    int Selected,
    EditorHint? Hint,
    Action<int> Accept,
    string OptionIdPrefix);

/// <summary>
/// The substitutable UI seams (ADR-0009/0010): the filter panel, the column menu, the
/// cell editor, the loading indicator, and the Formula Bar's two fields (ADR-0051). One
/// rule throughout: Chrome renders and calls back; it does not decide meaning — the
/// commands, the allowed operators and what a filter means are the core's, which is why
/// substituting Chrome cannot change behaviour.
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
    /// not try (ADR-0010/0030). Where the context carries
    /// <see cref="CellEditorContext.ReferenceText"/>, the fragment renders it immediately
    /// before its control (ADR-0057).</summary>
    RenderFragment? CellEditor(CellEditorContext context);

    /// <summary>Null falls back to the core's built-in find panel (ADR-0055).</summary>
    RenderFragment? FindPanel(FindContext context) => null;

    /// <summary>Null falls back to the core's own loading presentation (the
    /// <c>ex-loading</c> class and the Placeholder rows).</summary>
    RenderFragment? LoadingIndicator(LoadingContext context);

    /// <summary>The Formula Bar's Name Box (ADR-0051). Null falls back to the core's own
    /// input. A fragment is rendered inside the core's <c>ex-name-box</c> box, inside the
    /// form whose submission is Enter; the control fills the box.</summary>
    RenderFragment? NameBox(NameBoxContext context) => null;

    /// <summary>The Formula Bar's text field (ADR-0051). Null falls back to the core's own
    /// input. A fragment is rendered inside the core's <c>ex-editor</c> box in the bar, and
    /// <b>focuses its own control</b> whenever
    /// <see cref="FormulaBarTextContext.FocusRequest"/> changes, as the Cell Editor's
    /// does, and renders <see cref="FormulaBarTextContext.ReferenceText"/> immediately before
    /// its control where the context carries it (ADR-0057).</summary>
    RenderFragment? FormulaBarText(FormulaBarTextContext context) => null;

    /// <summary>The completion list and argument hint (ADR-0051). Null falls back to the core's
    /// own list. A fragment is rendered inside the core's <c>ex-completion</c> box, which stands
    /// beneath the editor surface inside the grid's box and scrolls when its room is short
    /// (ADR-0040); the fragment paints the candidates, then the hint.</summary>
    RenderFragment? EditorCompletion(EditorCompletionContext context) => null;
}
