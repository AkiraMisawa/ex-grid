namespace ExGrid.Chrome;

/// <summary>
/// The Chrome slots an <see cref="IGridChrome"/> fills with Razor when the Script Core draws the grid
/// (<c>ExGrid.Script.ExGrid</c>, ADR-0410): each named here is asked for through its view overload, and
/// the Script Core draws its own part for every slot not named.
/// </summary>
/// <remarks>
/// It stands beside the C# core's contexts until the C# core is removed (ADR-0350 section 7, ADR-0410
/// section 4's note of 2026-10-28): a Chrome answers both while both cores draw.
/// </remarks>
[Flags]
public enum ScriptSlots
{
    /// <summary>No slot: the Script Core draws every part.</summary>
    None = 0,

    /// <summary>The column menu's commands, through <see cref="IGridChrome.ColumnMenu(GridMenuView)"/>.</summary>
    ColumnMenu = 1,

    /// <summary>The Context Menu, through <see cref="IGridChrome.ContextMenu(GridMenuView)"/>.</summary>
    ContextMenu = 2,

    /// <summary>The loading indicator, through <see cref="IGridChrome.LoadingIndicator(GridLoadingView)"/>.</summary>
    LoadingIndicator = 4,

    /// <summary>A column's filter panel, under the column menu's commands, through
    /// <see cref="IGridChrome.FilterPanel(GridFilterView)"/>.</summary>
    FilterPanel = 8,

    /// <summary>The find panel and its Replace, inside the core's popover and form, through
    /// <see cref="IGridChrome.FindPanel(GridFindView)"/>.</summary>
    FindPanel = 16,

    /// <summary>The Cell Editor's field, inside the box the core stands over the cell, through
    /// <see cref="IGridChrome.CellEditor(GridCellEditorView)"/>. The core's own editor colours a formula's
    /// References (ADR-0057); a field a Chrome draws shows its text plain.</summary>
    CellEditor = 32,

    /// <summary>The Formula Bar's text field, inside the box the core stands in the bar, through
    /// <see cref="IGridChrome.FormulaBarText(GridFormulaBarTextView)"/>.</summary>
    FormulaBarText = 64,

    /// <summary>The Formula Bar's Name Box field, inside the core's box and form, through
    /// <see cref="IGridChrome.NameBox(GridNameBoxView)"/>.</summary>
    NameBox = 128,
}

/// <summary>
/// The attributes a slot's Razor writes on its controls when the Script Core draws the grid (ADR-0410
/// section 2): the adapter's script wires each control it finds by them, in the browser, so nothing a key
/// means waits on the circuit. The contents carry no handler of their own for what an attribute says.
/// </summary>
public static class ScriptSlotAttributes
{
    /// <summary>
    /// On a menu's control for one command, the command's index in <see cref="GridMenuView.Items"/>,
    /// once for every item. The script gives the control the menu's role, takes it out of the tab order,
    /// marks it disabled where its command is, answers every key on it against the place the core keeps
    /// for the menu (ADR-0039), runs its command on a press, and focuses the first enabled one as the menu
    /// asks.
    /// </summary>
    public const string MenuItem = "data-ex-menu-item";

    /// <summary>On the filter panel's search box: its text is the search, read in the browser as it is typed.
    /// Draw the field with no value bound to the view, which arrives a round trip behind the typing.</summary>
    public const string FilterSearch = "data-ex-filter-search";

    /// <summary>On the checkbox "Add current selection to filter".</summary>
    public const string FilterAddToCurrent = "data-ex-filter-add";

    /// <summary>On the element holding the value list: a letter typed on its checkboxes is Excel's, S, O
    /// and C running their command and E moving to the search box (ADR-0044).</summary>
    public const string FilterValues = "data-ex-filter-values";

    /// <summary>On the checkbox "(Select All)".</summary>
    public const string FilterSelectAll = "data-ex-filter-all";

    /// <summary>On a value's checkbox, the entry's index in <see cref="GridFilterValueListView.Entries"/>:
    /// the script ticks the value of the entry the checkbox was drawn for.</summary>
    public const string FilterValue = "data-ex-filter-value";

    /// <summary>On a condition's operator, <c>first</c> or <c>second</c>: a native <c>select</c> whose
    /// options' values are the <see cref="GridFilterOperatorView.Id"/>s, with an empty option for the
    /// second's "(none)". A component that answers its choice in .NET would reach the core a round trip
    /// after an Enter that applies the panel, so the choice is read in the browser.</summary>
    public const string FilterOperator = "data-ex-filter-operator";

    /// <summary>On a condition's value field, <c>first</c> or <c>second</c>: its text is the operand, read
    /// in the browser as it is typed. Draw it with the operand it opens with, and no value bound after.</summary>
    public const string FilterOperand = "data-ex-filter-operand";

    /// <summary>On the radio button that joins the conditions, <c>and</c> or <c>or</c>.</summary>
    public const string FilterJoin = "data-ex-filter-join";

    /// <summary>On Cancel. OK is a submit button, which submits the form the panel stands in, as Enter in a
    /// text field does.</summary>
    public const string FilterCancel = "data-ex-filter-cancel";

    /// <summary>On the controls a focus request may name, separated by spaces: <c>first</c>, the panel's
    /// first control; <c>last</c>, its last; and <c>search</c>, the search box, or the first condition's
    /// value where no value list stands. A control's element, or one holding it.</summary>
    public const string FilterFocus = "data-ex-filter-focus";

    /// <summary>On the find panel's text fields, <c>find</c> or <c>replace</c>: its text is what to look for, or
    /// what Replace writes, read in the browser as it is typed. The <c>find</c> field takes the keyboard at each
    /// request of the core's. Draw each with the text it opens with, and no value bound after.</summary>
    public const string FindField = "data-ex-find-field";

    /// <summary>On the find panel's options, <c>matchCase</c> or <c>wholeCell</c>: a checkbox.</summary>
    public const string FindOption = "data-ex-find-option";

    /// <summary>On the find panel's buttons, <c>next</c>, <c>previous</c>, <c>replace</c> or <c>replaceAll</c>: a
    /// press steps or writes. Enter in a field submits the core's form, which steps, backward with Shift, so no
    /// button need be a submit button.</summary>
    public const string FindStep = "data-ex-find-step";

    /// <summary>On the Cell Editor's one text field, an <c>input</c> or a <c>textarea</c>: the script hands it to the
    /// core, which reads and writes its text, places its caret and gives it the keyboard as the editor opens, in the
    /// browser. Draw it with <see cref="GridCellEditorView.InitialText"/> as its value and nothing bound after, and
    /// handle none of its keys: Enter, Tab, Escape, F2 and Overwrite's arrows are the core's.</summary>
    public const string EditorField = "data-ex-editor-field";

    /// <summary>On the Formula Bar's one text field, an <c>input</c> or a <c>textarea</c>: the script hands it to the
    /// core, which writes the Focus cell's text into it, names it, sets its <c>readonly</c>, and takes what is typed
    /// there as the Cell Editor's, in the browser. Draw it with no value, no <c>readonly</c> and no key handler.</summary>
    public const string FormulaBarField = "data-ex-formula-bar-field";

    /// <summary>On the Name Box's one <c>input</c>: the script hands it to the core, which writes the Focus's name into
    /// it, names it, and hands what Enter entered to the Consumer, in the browser. Draw it with no value and no key
    /// handler.</summary>
    public const string NameBoxField = "data-ex-name-box-field";
}

/// <summary>One command of a menu, as the Script Core hands it (ADR-0010, ADR-0036).</summary>
/// <param name="Id">The command's id, the one <see cref="GridCommand.Id"/> carries under the C# core.</param>
/// <param name="Label">The command's words, the Consumer's <c>commandLabel</c> else the built-in English.
/// A Wrapper with words of its own may show those instead.</param>
/// <param name="Enabled">Whether the command runs; a disabled one is shown and runs nothing.</param>
public sealed record GridMenuItemView(string Id, string Label, bool Enabled);

/// <summary>
/// A column menu's or the Context Menu's contents when the Script Core draws the grid (ADR-0410 section 3):
/// data only. The Razor draws one control per item, in order, each marked with
/// <see cref="ScriptSlotAttributes.MenuItem"/>, and handles none of its keys, presses or focus: the script
/// does, in the browser.
/// </summary>
/// <param name="Column">The <c>Name</c> of the column the menu is for, or was opened on.</param>
/// <param name="Type">That column's type.</param>
/// <param name="Items">The commands, in the core's order.</param>
/// <remarks>Where the keyboard goes in the menu is the script's: a view carries no focus request, and the
/// first enabled item, or the one a key typed as the menu opened moved to, takes the keyboard as the
/// contents arrive (ADR-0410 section 5).</remarks>
public sealed record GridMenuView(string Column, ColumnType Type, IReadOnlyList<GridMenuItemView> Items);

/// <summary>The loading indicator's view when the Script Core draws the grid (ADR-0010): data only.</summary>
/// <param name="IsLoading">Whether rows are on their way.</param>
/// <param name="PlaceholderRowCount">How many of the painted rows are Placeholders, which stay the core's.</param>
public sealed record GridLoadingView(bool IsLoading, int PlaceholderRowCount);

/// <summary>
/// The Formula Bar's text field when the Script Core draws the grid (ADR-0051, ADR-0410 section 3): data only, drawn
/// again as it changes. The text, the field's name and its <c>readonly</c> are the core's, set on the field in the
/// browser; the core also marks its box <c>ex-formula-bar-borrowed</c> while <paramref name="IsBorrowed"/> holds, so a
/// stylesheet can dim it without waiting for this view.
/// </summary>
/// <param name="IsReadOnly">Whether the field takes no typing: the Focus cell does not edit.</param>
/// <param name="IsBorrowed">Whether the text is not the Focus cell's own, an array's Formula over a cell it spills into
/// (ADR-0125).</param>
public sealed record GridFormulaBarTextView(bool IsReadOnly, bool IsBorrowed);

/// <summary>
/// The Name Box when the Script Core draws the grid (ADR-0051, ADR-0410 section 3). It carries nothing: what the box
/// shows and what Enter in it means are the core's, in the browser.
/// </summary>
public sealed record GridNameBoxView;

/// <summary>
/// The Cell Editor when the Script Core draws the grid (ADR-0010, ADR-0410 section 3): data only, drawn again as the
/// mode, the error or the pending commit changes. The Razor draws one text field marked with
/// <see cref="ScriptSlotAttributes.EditorField"/>; the core owns the box, the keys and the text.
/// </summary>
/// <param name="Column">The <c>Name</c> of the column the cell is in.</param>
/// <param name="Type">That column's type.</param>
/// <param name="InitialText">The text the field opens holding: what was typed to open it, or the cell's text. The
/// field's value as it is first drawn, and never after: what is typed is the core's, a round trip ahead of any view.</param>
/// <param name="Mode">Overwrite, Caret or Point (ADR-0051).</param>
/// <param name="Error">The sentence a Reject or a refusal holds the editor with, while one does; null otherwise. The
/// core has marked the field <c>aria-invalid</c> and described it by the message already.</param>
/// <param name="IsPending">Whether the commit is pending (ADR-0380 section 3): the core has made the field read only.</param>
public sealed record GridCellEditorView(string Column, ColumnType Type, string InitialText, CellEditMode Mode, string? Error, bool IsPending);

/// <summary>
/// A column's filter panel when the Script Core draws the grid (ADR-0009, ADR-0044, ADR-0410 section 3):
/// data only, drawn again at each change the core makes to it. Exactly one of
/// <see cref="ValueList"/> and <see cref="Conditions"/> stands. The Razor draws its controls marked with
/// <see cref="ScriptSlotAttributes"/>' filter attributes and handles none of their changes, keys or focus:
/// the script hands each to the core in the browser, and the core decides what a choice means.
/// </summary>
/// <param name="Column">The <c>Name</c> of the column the panel filters.</param>
/// <param name="Type">That column's type.</param>
/// <param name="ValueList">The value list, where it stands.</param>
/// <param name="Conditions">The condition form, Excel's Custom AutoFilter, where it stands.</param>
/// <param name="CanApply">Whether OK is available: not while a value list keeps nothing. The core
/// refuses OK while it is not, so a button drawn a round trip late applies nothing.</param>
/// <param name="Fetching">Whether the value list is still on its way; the condition form stands
/// meanwhile.</param>
/// <param name="Words">The panel's words.</param>
public sealed record GridFilterView(
    string Column,
    ColumnType Type,
    GridFilterValueListView? ValueList,
    GridFilterConditionsView? Conditions,
    bool CanApply,
    bool Fetching,
    GridFilterWords Words);

/// <summary>The value list as it stands (ADR-0009).</summary>
/// <param name="Search">The search the core holds, a round trip behind the typing: draw the search box
/// with no value bound to it.</param>
/// <param name="AddOffered">Whether "Add current selection to filter" is offered: while a search stands
/// over a column filtered by its values.</param>
/// <param name="AddToCurrent">Whether it is ticked.</param>
/// <param name="SelectAll">"(Select All)" over the values the search shows.</param>
/// <param name="Entries">The values the search shows, in the domain's order.</param>
public sealed record GridFilterValueListView(
    string Search,
    bool AddOffered,
    bool AddToCurrent,
    SelectAllState SelectAll,
    IReadOnlyList<GridFilterEntryView> Entries);

/// <summary>A value the list shows.</summary>
/// <param name="Text">The text its cells show, or the Blank's words.</param>
/// <param name="Blank">Whether it is the Blank.</param>
/// <param name="Checked">Whether it is ticked.</param>
public sealed record GridFilterEntryView(string Text, bool Blank, bool Checked);

/// <summary>The condition form as it stands (ADR-0009).</summary>
/// <param name="Allowed">The operators the core offers the column, and no other (FN-18).</param>
/// <param name="First">The first condition.</param>
/// <param name="Combinator">How the two conditions are joined.</param>
/// <param name="Second">The second condition; null until one is chosen ("(none)").</param>
public sealed record GridFilterConditionsView(
    IReadOnlyList<GridFilterOperatorView> Allowed,
    GridFilterConditionView First,
    FilterCombinator Combinator,
    GridFilterConditionView? Second);

/// <summary>An operator the condition form offers.</summary>
/// <param name="Id">The script's name for it: the value of its option.</param>
/// <param name="Label">Its words.</param>
public sealed record GridFilterOperatorView(string Id, string Label);

/// <summary>One condition of the condition form.</summary>
/// <param name="Operator">The <see cref="GridFilterOperatorView.Id"/> of its operator.</param>
/// <param name="Operand">The text typed for its value, a round trip behind the typing.</param>
/// <param name="TakesOperand">Whether the operator takes a value; where it takes none, draw no field.</param>
/// <param name="Refusal">Why the typed text does not read as the column's type, in the core's words;
/// null where it reads or nothing is typed (ADR-0023).</param>
public sealed record GridFilterConditionView(string Operator, string Operand, bool TakesOperand, string? Refusal);

/// <summary>The filter panel's words, the Consumer's <c>commandLabel</c> else the built-in English.</summary>
/// <param name="Ok">OK.</param>
/// <param name="Cancel">Cancel.</param>
/// <param name="Search">The search box's name.</param>
/// <param name="AddToCurrent">"Add current selection to filter".</param>
/// <param name="SelectAll">"(Select All)".</param>
/// <param name="SelectAllSearchResults">"(Select All Search Results)", while a search stands.</param>
/// <param name="And">The join where both conditions must hold.</param>
/// <param name="Or">The join where either may.</param>
/// <param name="None">The second condition's operator, unset.</param>
public sealed record GridFilterWords(
    string Ok,
    string Cancel,
    string Search,
    string AddToCurrent,
    string SelectAll,
    string SelectAllSearchResults,
    string And,
    string Or,
    string None);

/// <summary>
/// The find panel when the Script Core draws the grid (ADR-0055, ADR-0332, ADR-0410 section 3): data only, drawn
/// again at each change the core makes to it. The Razor draws its controls marked with
/// <see cref="ScriptSlotAttributes"/>' find attributes and handles none of their changes, presses or focus: the
/// script hands each to the core in the browser. The search, the Focus it moves and the Replace are the core's.
/// </summary>
/// <param name="Text">What the find field opens with: the text the core held as the panel opened. What is typed after
/// is the field's, read by the script, and not sent again; draw the field with this as it opens, and no value bound
/// after.</param>
/// <param name="MatchCase">Match case.</param>
/// <param name="WholeCell">Match entire cell contents.</param>
/// <param name="ReplaceShown">Whether the panel offers Replace (ADR-0332 section 2): while it does, draw the Replace
/// field and the two writes.</param>
/// <param name="ReplaceText">What the Replace field opens with: the replacement the core held as Replace was last
/// shown, drawn as <paramref name="Text"/> is.</param>
/// <param name="ReplaceWaiting">Whether a Replace All is out with the Consumer: show Replace and Replace All
/// unavailable meanwhile (ADR-0332 section 3). The core does nothing for either while it is.</param>
/// <param name="Outcome">What the last step or Replace came to, for a live region; empty when it says nothing
/// (ADR-0033). It is a sentence the core made, in the grid's <c>commandLabel</c> words else the built-in English: a
/// Chrome's own words for the panel's labels do not reach it.</param>
/// <param name="Words">The panel's words.</param>
public sealed record GridFindView(
    string Text,
    bool MatchCase,
    bool WholeCell,
    bool ReplaceShown,
    string ReplaceText,
    bool ReplaceWaiting,
    string Outcome,
    GridFindWords Words);

/// <summary>The find panel's words, the Consumer's <c>commandLabel</c> else the built-in English.</summary>
/// <param name="Field">The find field's name.</param>
/// <param name="MatchCase">Match case.</param>
/// <param name="WholeCell">Match entire cell contents.</param>
/// <param name="Next">Find next.</param>
/// <param name="Previous">Find previous.</param>
/// <param name="ReplaceField">The Replace field's name.</param>
/// <param name="Replace">Replace.</param>
/// <param name="ReplaceAll">Replace All.</param>
public sealed record GridFindWords(
    string Field,
    string MatchCase,
    string WholeCell,
    string Next,
    string Previous,
    string ReplaceField,
    string Replace,
    string ReplaceAll);
