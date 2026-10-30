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
/// <para>The menu is drawn in the browser by the Script Core, which runs its own commands there; a
/// Consumer's are asked for as it opens (<c>ExGrid.ContextCommands</c>, ADR-0421).</para>
/// </summary>
public sealed record ContextMenuContext<TRow>(
    TRow Row,
    string Column,
    ColumnType Type,
    IReadOnlyList<SelectionRange> Selection,
    int RowSequenceVersion,
    Action Close);

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
///
/// <para>A button of the contents' that closes the popover — OK, Cancel — carries the class
/// <c>ex-popover-closes</c>, and a choice among others is ARIA's <c>menuitemradio</c>: pressed, by
/// the pointer or the keys, each closes it a round trip later on a circuit, and the grid holds the
/// keys typed meanwhile and hands them on where the keyboard lands (ADR-0010; ADR-0044's note of
/// 2026-10-12).</para>
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
    Action<bool> InnerPopupChanged)
{
    /// <summary>
    /// The core's focus function for the contents' controls (ADR-0204's note of 2026-10-17): the
    /// control, and whether it may scroll into view. The contents answer a change of
    /// <see cref="FocusRequest"/> or <see cref="FocusLastRequest"/> by calling it instead of the
    /// control's own <c>FocusAsync</c>. It lands a round trip after the gesture that asked, and a
    /// press the user has made inside the popover meanwhile keeps the keyboard where it was pressed:
    /// the opening focus is then not taken. Null only in a context the core did not make.
    /// </summary>
    public Func<ElementReference, bool, Task>? TakeFocus { get; init; }

    /// <summary>
    /// The Input Order's seam (ADR-0240 section 6), as <c>ExGrid.InOrderAsync</c> offers it: the
    /// contents' handler of an input that changes the instance or moves the keyboard — an OK, a Cancel,
    /// a choice made, a key the contents answer — calls it as it starts, before its first <c>await</c>, with the kind of the event it
    /// hears and its work, and is handled in its place among the grid's keys, presses and clicks. The
    /// control declares the event in its <c>data-ex-claims</c> attribute (<c>click</c>, <c>keydown</c>,
    /// <c>submit</c>, …), so the grid numbers it as it happens; a handler that declares an event calls this
    /// every time it hears it. What the contents' draft holds — a field typed into, a box ticked — is not
    /// an input and needs no call: the input that commits it is one. Null only in a context the core did
    /// not make.
    /// </summary>
    public Func<GridInputKind, Func<Task>, Task>? InOrder { get; init; }
}

/// <summary>
/// The substitutable UI seams (ADR-0009/0010, ADR-0410): the parts of the grid the Script Core leaves to Razor —
/// the column menu, the Context Menu, the filter and find panels, the Cell Editor, the Formula Bar's two fields, the
/// completion list and the loading indicator — named by <see cref="ScriptSlots"/>, each drawn from a view of data
/// with its controls marked by <see cref="ScriptSlotAttributes"/>. One rule throughout: Chrome renders and calls
/// back; it does not decide meaning — the commands, the allowed operators and what a filter means are the core's,
/// which is why substituting Chrome cannot change behaviour.
/// </summary>
public interface IGridChrome
{
    /// <summary>The stylesheets this Chrome brings (ADR-0123): a Wrapper's own, each as its shipped
    /// stylesheet's notice rules name it, with the stamp it was built with. The grid's root renders a
    /// notice for each beside its own, which only another version's stylesheet shows, so a page
    /// holding another version of the Wrapper's stylesheet says so, with no script. Empty for the
    /// built-in Chrome, which brings none. Read on every render of the root: answer the same list
    /// each time.</summary>
    IReadOnlyList<ChromeStylesheet> Stylesheets => [];

    /// <summary>
    /// The Chrome's words for something the core says rather than shows: a refusal, by its
    /// <see cref="RefusalLabelIds"/> id (ADR-0203), which the root's live region announces once, as
    /// it happens. The core holds no sentence; this is the Chrome's label function, the seam
    /// <c>ExGrid.CommandLabel</c> is for the built-in Chrome. Null — the default — leaves the
    /// built-in Chrome's words: <c>ExGrid.CommandLabel</c>, then <see cref="BuiltInCommandLabels"/>.
    /// The empty string says nothing for that id, and whoever answers it owns saying it.
    /// </summary>
    /// <param name="id">The id of the words asked for.</param>
    string? Words(string id) => null;

    /// <summary>The slots this Chrome fills with Razor when the Script Core draws the grid (ADR-0410):
    /// each is asked for through its view overload. <see cref="ScriptSlots.None"/>, the default, leaves
    /// every part the Script Core's own. Read as the grid's parameters are sent: answer the same each
    /// time.</summary>
    ScriptSlots ScriptSlots => ScriptSlots.None;

    /// <summary>The column menu's commands when the Script Core draws the grid (ADR-0410), for a Chrome
    /// that names <see cref="ScriptSlots.ColumnMenu"/>: one control per item, each marked with
    /// <see cref="ScriptSlotAttributes.MenuItem"/>, and no handler of its own.</summary>
    RenderFragment? ColumnMenu(GridMenuView view) => null;

    /// <summary>The Context Menu when the Script Core draws the grid (ADR-0410), for a Chrome that names
    /// <see cref="ScriptSlots.ContextMenu"/>, drawn as <see cref="ColumnMenu(GridMenuView)"/> is.</summary>
    RenderFragment? ContextMenu(GridMenuView view) => null;

    /// <summary>The loading indicator when the Script Core draws the grid (ADR-0410), for a Chrome that
    /// names <see cref="ScriptSlots.LoadingIndicator"/>. Null draws nothing in the slot; the Placeholder
    /// rows stay the core's (ADR-0004).</summary>
    RenderFragment? LoadingIndicator(GridLoadingView view) => null;

    /// <summary>A column's filter panel when the Script Core draws the grid (ADR-0410), for a Chrome that
    /// names <see cref="ScriptSlots.FilterPanel"/>: its controls, each marked with
    /// <see cref="ScriptSlotAttributes"/>' filter attributes, a submit button for OK, and no handler of
    /// its own.</summary>
    RenderFragment? FilterPanel(GridFilterView view) => null;

    /// <summary>The find panel when the Script Core draws the grid (ADR-0410), for a Chrome that names
    /// <see cref="ScriptSlots.FindPanel"/>: its controls, each marked with <see cref="ScriptSlotAttributes"/>'
    /// find attributes, and no form or handler of its own; the slot stands inside the core's form.</summary>
    RenderFragment? FindPanel(GridFindView view) => null;

    /// <summary>The Cell Editor's field when the Script Core draws the grid (ADR-0410), for a Chrome that names
    /// <see cref="ScriptSlots.CellEditor"/>: one text field marked with <see cref="ScriptSlotAttributes.EditorField"/>,
    /// holding <see cref="GridCellEditorView.InitialText"/> as drawn first, and no handler of its own.</summary>
    RenderFragment? CellEditor(GridCellEditorView view) => null;

    /// <summary>The Formula Bar's text field when the Script Core draws the grid (ADR-0410), for a Chrome that names
    /// <see cref="ScriptSlots.FormulaBarText"/>: one text field marked with
    /// <see cref="ScriptSlotAttributes.FormulaBarField"/>, with no value, no <c>readonly</c> and no handler of its
    /// own.</summary>
    RenderFragment? FormulaBarText(GridFormulaBarTextView view) => null;

    /// <summary>The Name Box when the Script Core draws the grid (ADR-0410), for a Chrome that names
    /// <see cref="ScriptSlots.NameBox"/>: one <c>input</c> marked with <see cref="ScriptSlotAttributes.NameBoxField"/>,
    /// with no value and no handler of its own.</summary>
    RenderFragment? NameBox(GridNameBoxView view) => null;

    /// <summary>The completion list and the argument hint when the Script Core draws the grid (ADR-0410), for a Chrome
    /// that names <see cref="ScriptSlots.EditorCompletion"/>: the candidates in an element marked
    /// <see cref="ScriptSlotAttributes.CompletionList"/>, each marked <see cref="ScriptSlotAttributes.CompletionCandidate"/>,
    /// then the hint, with no handler and no choice of its own.</summary>
    RenderFragment? EditorCompletion(GridEditorCompletionView view) => null;
}
