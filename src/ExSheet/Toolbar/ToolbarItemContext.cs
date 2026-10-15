using ExSheet.Engine;

namespace ExSheet;

/// <summary>What kind of control a Toolbar Item is, which tells the Chrome how to draw it (ADR-0100).</summary>
public enum ToolbarItemKind
{
    /// <summary>A command that runs when pressed: Format Cells…, a Consumer's action.</summary>
    Button,

    /// <summary>A command that is on or off for the Focus cell, drawn pressed while it is on: Bold, an alignment.</summary>
    Toggle,

    /// <summary>A list of choices that opens when pressed: the Number Format.</summary>
    Menu,

    /// <summary>
    /// A split control, as Excel's colour and border buttons are: pressing the face runs
    /// <see cref="ToolbarItemContext.Invoke"/>, the last choice again, and pressing its arrow opens
    /// the choices.
    /// </summary>
    Split,

    /// <summary>A gap between groups of items. It does nothing and is never a tab stop.</summary>
    Separator,
}

/// <summary>The picture a built-in Toolbar Item shows. A Chrome draws it in its own icon set (ADR-0100).</summary>
public enum ToolbarIcon
{
    /// <summary>No picture: the item shows its <see cref="ToolbarItemContext.Text"/>.</summary>
    None,

    /// <summary>Bold.</summary>
    Bold,

    /// <summary>Italic.</summary>
    Italic,

    /// <summary>A single underline.</summary>
    Underline,

    /// <summary>Strikethrough.</summary>
    Strikethrough,

    /// <summary>The Font colour.</summary>
    FontColour,

    /// <summary>The Fill.</summary>
    Fill,

    /// <summary>Borders.</summary>
    Borders,

    /// <summary>Left alignment.</summary>
    AlignLeft,

    /// <summary>Centre alignment.</summary>
    AlignCenter,

    /// <summary>Right alignment.</summary>
    AlignRight,

    /// <summary>The percent style.</summary>
    Percent,

    /// <summary>The comma style.</summary>
    Comma,

    /// <summary>Format Cells.</summary>
    FormatCells,
}

/// <summary>
/// One choice in a Toolbar Item's list: a Number Format category, a colour, a border preset
/// (ADR-0100). The meaning is ExSheet's; the Chrome draws it and calls <see cref="Choose"/>.
/// </summary>
/// <param name="Label">What the choice is called, as Excel calls it.</param>
/// <param name="Choose">Runs the choice. The Chrome closes its list and calls this once.</param>
/// <param name="DisabledReason">Why the choice cannot be made, shown beside it; null when it can.</param>
/// <param name="Colour">The colour a swatch shows, for a colour choice; null for any other.</param>
/// <param name="Selected">Whether this is what the Focus cell has.</param>
public sealed record ToolbarChoice(
    string Label,
    Func<Task> Choose,
    string? DisabledReason = null,
    CellColour? Colour = null,
    bool Selected = false)
{
    /// <summary>
    /// Whether the choice opens a popover of the Sheet's in the list's place: More Number Formats… opens
    /// Format Cells. In the grid's popover frame the choice is then made before the list closes, so the
    /// core opens it in the list's place and hands the keyboard on before it asks for the root, as for a
    /// menu's command (ADR-0039's note of 2026-10-13): closed first, the list asked for the root a message
    /// ahead of the hand-off, and a key landing between the two was the grid's.
    /// </summary>
    internal bool OpensPopover { get; init; }
}

/// <summary>
/// What a Toolbar Item declares to the Chrome that draws it (ADR-0100, ADR-0010): what it is, what
/// it shows, whether it is pressed or available, and what pressing it does. The Chrome decides how
/// it looks and nothing else, so swapping the Chrome keeps every item's meaning.
/// </summary>
/// <param name="Id">The element id the Chrome gives the control, which the Sheet Toolbar names as the
/// active item while the keyboard moves among its items.</param>
/// <param name="Kind">What kind of control it is.</param>
/// <param name="Name">The item's name, as Excel names it: the control's accessible name and tooltip.</param>
/// <param name="Shortcut">The key that does the same, such as <c>Ctrl+B</c>, for the tooltip; null for none.</param>
/// <param name="Icon">The picture it shows.</param>
/// <param name="Text">The text it shows beside or instead of its picture; null for none.</param>
/// <param name="Pressed">For a <see cref="ToolbarItemKind.Toggle"/>, whether the Focus cell has it.</param>
/// <param name="Enabled">Whether it can be pressed. While an edit is open no item can (ADR-0100).</param>
/// <param name="DisabledReason">Why it cannot be pressed, for the tooltip; null when it can.</param>
/// <param name="Invoke">What pressing it does; for a <see cref="ToolbarItemKind.Menu"/>, opening its
/// choices in the built-in frame.</param>
/// <param name="Choices">The choices of a menu or a split control, in Excel's order; empty for any other.</param>
/// <param name="Colour">For a colour control, the colour its face applies, drawn under its picture.</param>
/// <param name="Active">Whether the keyboard is on this item inside the Sheet Toolbar.</param>
/// <param name="OpenRequest">Changes whenever the item's choices are to open from the keyboard. A Chrome
/// whose list is its own opens it when this changes, as a popover answers its focus requests.</param>
/// <param name="CloseRequest">Changes whenever the Sheet Toolbar takes the keyboard back while the item's
/// choices may be open from the keyboard (ADR-0100's note of 2026-10-12): a key typed on the toolbar
/// before the list took the keyboard — Escape, a KeyTip — gave it back to the Sheet. A Chrome whose
/// list is its own closes it when this changes, drops a focus into it not yet given, and does not call
/// <see cref="ChoicesClosed"/> for that close: the toolbar takes the keyboard back itself. A list whose
/// focus was already given when the close was asked for holds the keyboard by then, where the toolbar's
/// hand-back does not take it from: it closes as it does on its own, and calls
/// <see cref="ChoicesClosed"/>. It takes the value <see cref="OpenRequest"/> had when the close was asked
/// for, so a Chrome that answers both after one render knows which came last: an opening it has not yet
/// answered is still asked for only while the two differ.</param>
/// <param name="ChoicesClosed">A Chrome whose list is its own calls this once the list has closed, without a
/// choice or after making one, so the Sheet Toolbar takes the keyboard on from wherever the close left it: back
/// to the Sheet, or, after Escape in a list a KeyTip opened, to the toolbar's KeyTips, as Excel went back one
/// level when read on Windows (observed: the readings of 2026-10-12, R2 (b); ADR-0100's note of that day).</param>
/// <param name="ChoicesClosing">A Chrome whose list is its own calls this as the list begins to close — a
/// choice made, Escape, or the design system's own means while the keyboard is in it — with the key that
/// closes it (null when no key does), and passes every key typed in the list from then until it has gone to
/// the action this returns, in order (decided with the user, 2026-10-12). On a circuit the list stands a round
/// trip after that key, and the keys typed meanwhile are not the list's: the Sheet Toolbar holds them, with
/// the keys typed on its band, and hands them all on, in order, to where the keyboard goes when
/// <see cref="ChoicesClosed"/> is called. A Chrome that does not call it leaves those keys to its list, which
/// is about to go.</param>
/// <param name="ChoicesOpened">A Chrome whose list is its own calls this once the list it opened for
/// <see cref="OpenRequest"/> holds the keyboard, with true, or once it knows the list will not take it — it
/// did not open — with false (decided with the user, 2026-10-13). On a circuit the list takes the keyboard a
/// round trip after the toolbar asked for it, and the keys typed on the toolbar meanwhile are the list's, as
/// they would be had they been typed a moment later: the Sheet Toolbar holds them from the moment it asks,
/// and answers them, in order, to a call with true, for the Chrome to answer as typed in the list, ahead of
/// any typed in it since; with false it answers none and gives them to the toolbar itself. The keys of a
/// list the toolbar closes before it is opened (<see cref="CloseRequest"/>) go where the toolbar takes the
/// keyboard, and a call after that answers none. A Chrome that never calls it leaves the toolbar holding
/// them until the keyboard, having left the toolbar's band, comes back to it: they are then the band's.</param>
/// <param name="Content">A Consumer item's own content, drawn in place of a picture and text; null for
/// ExSheet's items.</param>
/// <param name="KeyTip">The item's KeyTip letters; null for none.</param>
/// <param name="ShowKeyTip">Whether the KeyTip is shown now.</param>
public sealed record ToolbarItemContext(
    string Id,
    ToolbarItemKind Kind,
    string Name,
    string? Shortcut,
    ToolbarIcon Icon,
    string? Text,
    bool Pressed,
    bool Enabled,
    string? DisabledReason,
    Func<Task> Invoke,
    IReadOnlyList<ToolbarChoice> Choices,
    CellColour? Colour,
    bool Active,
    int OpenRequest,
    int CloseRequest,
    Func<Task> ChoicesClosed,
    Func<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs?, Action<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs>> ChoicesClosing,
    Func<bool, IReadOnlyList<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs>> ChoicesOpened,
    Microsoft.AspNetCore.Components.RenderFragment? Content,
    string? KeyTip,
    bool ShowKeyTip)
{
    /// <summary>The tooltip: the name, the key in brackets when there is one, and the reason it cannot be pressed.</summary>
    public string Tooltip =>
        (Shortcut is { } key ? $"{Name} ({key})" : Name) + (DisabledReason is { } reason ? " — " + reason : "");

    /// <inheritdoc />
    public bool Equals(ToolbarItemContext? other) =>
        other is not null
        && Id == other.Id && Kind == other.Kind && Name == other.Name && Shortcut == other.Shortcut
        && Icon == other.Icon && Text == other.Text && Pressed == other.Pressed && Enabled == other.Enabled
        && DisabledReason == other.DisabledReason && ReferenceEquals(Invoke, other.Invoke)
        && Choices.SequenceEqual(other.Choices) && Colour == other.Colour && Active == other.Active
        && OpenRequest == other.OpenRequest && CloseRequest == other.CloseRequest
        && ReferenceEquals(ChoicesClosed, other.ChoicesClosed) && ReferenceEquals(ChoicesClosing, other.ChoicesClosing)
        && ReferenceEquals(ChoicesOpened, other.ChoicesOpened)
        && ReferenceEquals(Content, other.Content) && KeyTip == other.KeyTip && ShowKeyTip == other.ShowKeyTip;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Kind, Pressed, Enabled, Active, OpenRequest, CloseRequest, ShowKeyTip);
}
