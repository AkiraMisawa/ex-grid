namespace ExSheet;

/// <summary>
/// The Sheet's own Chrome slots an <see cref="ISheetChrome"/> fills with Razor when the Script Core draws the Sheet
/// (<c>ExSheet.Components.ExSheet</c>, ADR-0410): each named here is asked for through its view overload, and the Script
/// Core draws its own part for every slot not named. The grid's slots are the Chrome's
/// <see cref="ExGrid.Chrome.IGridChrome.ScriptSlots"/>, all but the find panel, which is the Sheet's own (ADR-0332 §5),
/// and the Cell Editor and the Formula Bar's text, whose own fields colour a formula's References (ADR-0057).
/// </summary>
/// <remarks>
/// The Sheet Tabs' menu and the Sheet's find panel are not carried: the Script Core draws its own under every Chrome.
/// </remarks>
[Flags]
public enum SheetScriptSlots
{
    /// <summary>No slot: the Script Core draws every part of the Sheet's own.</summary>
    None = 0,

    /// <summary>Each Toolbar Item, through <see cref="ISheetChrome.ToolbarItem(SheetToolbarItemView)"/>.</summary>
    ToolbarItem = 1,

    /// <summary>Each Sheet Tab's face, through <see cref="ISheetChrome.SheetTab(SheetTabContext)"/>, which takes data
    /// alone.</summary>
    SheetTab = 2,

    /// <summary>The face of the button that adds a Sheet, through <see cref="ISheetChrome.SheetTabAdd"/>.</summary>
    SheetTabAdd = 4,

    /// <summary>Format Cells' contents, through <see cref="ISheetChrome.FormatCells(SheetFormatCellsView)"/>. They stand
    /// in the grid's popover frame, as the Script Core's own do: where it stands, Escape, a press elsewhere, Tab wrapping
    /// inside it and the keyboard back on the grid as it closes are the grid's (ADR-0050 item 16).</summary>
    FormatCells = 8,
}

/// <summary>
/// The attributes a Sheet slot's Razor writes on its controls when the Script Core draws the Sheet (ADR-0410 section
/// 2): the adapter's script wires each control it finds by them, in the browser, so nothing a key means waits on the
/// circuit. The contents carry no handler of their own for what an attribute says.
/// </summary>
public static class SheetScriptSlotAttributes
{
    /// <summary>On a Toolbar Item's button: what a press on it does. <c>invoke</c> presses the item, as a toggle, a
    /// button or a split control's face does; <c>list</c> opens or closes its list, as a menu's face or a split
    /// control's arrow does. The script takes the button out of the Tab order, keeps the keyboard on the Sheet as it is
    /// pressed, and runs the press in the Sheet's Input Order (ADR-0240).</summary>
    public const string Press = "data-ex-sheet-press";

    /// <summary>On the one element holding a Toolbar Item's choices, drawn whatever the list's state, inside the item's
    /// contents, never elsewhere: a press outside them closes the list. The script shows it while it is open, fixed at
    /// the item's foot, so the band, which clips what its rows hold, does not cut it; names it a menu; and answers its
    /// keys, the built-in list's (the arrows, Home and End among the choices that can be made, swatches ten across;
    /// Enter or Space; Escape).</summary>
    public const string List = "data-ex-sheet-list";

    /// <summary>On a choice's control, the choice's index in <see cref="SheetToolbarItemView.Choices"/>, once for every
    /// choice. The script gives it the role <c>menuitemradio</c>, marks it checked while the Focus cell has it and
    /// disabled while it cannot be made, takes it out of the Tab order, gives the list's keyboard to it, and makes the
    /// choice on a press.</summary>
    public const string Choice = "data-ex-sheet-choice";

    /// <summary>On the empty element a Toolbar Item's contents draw where a <see cref="ToolbarButton"/>'s
    /// <c>ChildContent</c> goes, for an item whose <see cref="SheetToolbarItemView.HasContent"/> says it has one: the
    /// script moves the content in.</summary>
    public const string Content = "data-ex-sheet-content";

    /// <summary>On the one form holding every tab of Format Cells: its submission is OK. The script checks its fields
    /// first, on every tab, and shows the tab of one the browser holds invalid; then OK sets what was touched, or the
    /// view says why it set nothing. OK is a submit button in it, and Enter in a field submits it.</summary>
    public const string FormatForm = "data-ex-format-form";

    /// <summary>On each of Format Cells' tab controls, the tab's <see cref="SheetFormatCellsChoice.Value"/>. The script
    /// makes them a tab list's tabs, shows the tab pressed, and moves among them on the arrows, Home and End; the opening's
    /// keyboard goes to the tab shown.</summary>
    public const string FormatTab = "data-ex-format-tab";

    /// <summary>On the element holding each tab's controls, the tab's value: the script shows the one of the tab shown,
    /// and hides the rest, which stay drawn, their fields as typed.</summary>
    public const string FormatPanel = "data-ex-format-panel";

    /// <summary>On each of Format Cells' controls, the choice it makes: <c>category</c>, <c>negative</c>, <c>type</c>,
    /// <c>fontStyle</c>, <c>lineStyle</c> and <c>colour</c> on a radio button, each with its
    /// <see cref="FormatValue"/>; <c>separator</c>, <c>strikethrough</c> and <c>normalFont</c> on a checkbox;
    /// <c>alignment</c> and <c>underline</c> on a native select, whose options' values are the choices' values and
    /// <c>none</c> or <c>single</c>; <c>places</c> (a number field, 0 to 30), <c>code</c> and <c>colourText</c> (with the
    /// palette's <see cref="SheetFormatCellsPalette.Target"/> as its value) on a text field drawn with no value;
    /// <c>preset</c> and <c>edge</c> on a button, with the value. The script reads each as the browser fires it, never
    /// through a component's handler, and sets what each holds from the draft.</summary>
    public const string FormatSet = "data-ex-format-set";

    /// <summary>Beside <see cref="FormatSet"/>, which one of several choices the control makes.</summary>
    public const string FormatValue = "data-ex-format-value";

    /// <summary>On Format Cells' Cancel: it sets nothing, and closes Format Cells.</summary>
    public const string FormatCancel = "data-ex-format-cancel";
}

/// <summary>
/// A Toolbar Item as the Script Core describes it to a Chrome (ADR-0100, ADR-0410): what it is and what it shows now.
/// What pressing it and each choice does is the Sheet's, wired by the script from
/// <see cref="SheetScriptSlotAttributes"/>. The KeyTip, the item's place and the outline the toolbar's keyboard draws
/// on it are the band's, around the contents.
/// </summary>
/// <param name="Id">Stable for the item's place in the toolbar.</param>
/// <param name="Kind">What the item is: a toggle, a button, a menu or a split control. Never a separator, which the
/// band draws.</param>
/// <param name="Name">The item's name, its accessible name: <c>Fill Colour</c>.</param>
/// <param name="Tooltip">The name with its key, for the tooltip: <c>Bold (Ctrl+B)</c>.</param>
/// <param name="Icon">The picture the built-in Chrome draws for it, or <see cref="ToolbarIcon.None"/> for an item that
/// shows its text.</param>
/// <param name="Text">For a menu, the text its face shows, the Number Format's category; null otherwise.</param>
/// <param name="Swatch">For a colour control, the colour its face sets, as CSS paints it; null otherwise.</param>
/// <param name="MoreName">For a split control, its arrow's name: <c>Borders, more</c>; null otherwise.</param>
/// <param name="Pressed">For a toggle, whether the Focus cell has what it sets.</param>
/// <param name="DisabledReason">Why it cannot be pressed now, or null where it can: while an edit is open, every item
/// says so.</param>
/// <param name="Choices">For a menu or a split control, its list's choices, in the Sheet's order; none otherwise.</param>
/// <param name="Rows">The list's rows, as the list's keys move among them: each the indices in <paramref name="Choices"/>
/// of one choice that is no colour, or of a run of colours drawn together, ten across, as Excel's palette lays them out.
/// None where there are no choices.</param>
public sealed record SheetToolbarItemView(
    string Id,
    ToolbarItemKind Kind,
    string Name,
    string Tooltip,
    ToolbarIcon Icon,
    string? Text,
    string? Swatch,
    string? MoreName,
    bool Pressed,
    string? DisabledReason,
    IReadOnlyList<SheetToolbarChoiceView> Choices,
    IReadOnlyList<IReadOnlyList<int>> Rows)
{
    /// <summary>Whether it can be pressed now.</summary>
    public bool Enabled => DisabledReason is null;

    /// <summary>For a Consumer's <see cref="ToolbarButton"/> with <c>ChildContent</c>, which shows in place of its text:
    /// the contents draw an empty element marked <see cref="SheetScriptSlotAttributes.Content"/> where it goes, and the
    /// script moves it in (ADR-0100). <see cref="Text"/> is then null, and <see cref="Name"/> still names it.</summary>
    public bool HasContent { get; init; }
}

/// <summary>One choice of a Toolbar Item's list as the Script Core describes it (ADR-0100, ADR-0410).</summary>
/// <param name="Label">Its words: a colour's name, a Number Format's category, a border preset's name.</param>
/// <param name="Swatch">For a colour, the colour as CSS paints it, the label naming it; null for any other choice.</param>
/// <param name="Selected">Whether it is what the Focus cell has.</param>
/// <param name="DisabledReason">Why it cannot be chosen, or null where it can.</param>
public sealed record SheetToolbarChoiceView(string Label, string? Swatch, bool Selected, string? DisabledReason);
