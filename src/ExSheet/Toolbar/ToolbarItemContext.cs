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
    bool Selected = false);

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
/// <param name="ChoicesClosed">A Chrome whose list is its own calls this when the list closes without a
/// choice, so the Sheet takes the keyboard back.</param>
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
    Func<Task> ChoicesClosed,
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
        && OpenRequest == other.OpenRequest && ReferenceEquals(ChoicesClosed, other.ChoicesClosed)
        && ReferenceEquals(Content, other.Content) && KeyTip == other.KeyTip && ShowKeyTip == other.ShowKeyTip;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Kind, Pressed, Enabled, Active, OpenRequest, ShowKeyTip);
}
