namespace ExSheet;

/// <summary>What kind of control a Toolbar Item is, which tells the Chrome how to draw it (ADR-0100). A separator is the band's, never an item a Chrome draws.</summary>
public enum ToolbarItemKind
{
    /// <summary>A command that runs when pressed: Format Cells…, a Consumer's action.</summary>
    Button,

    /// <summary>A command that is on or off for the Focus cell, drawn pressed while it is on: Bold, an alignment.</summary>
    Toggle,

    /// <summary>A list of choices that opens when pressed: the Number Format.</summary>
    Menu,

    /// <summary>
    /// A split control, as Excel's colour and border buttons are: pressing the face sets the last choice again, and
    /// pressing its arrow opens the choices.
    /// </summary>
    Split,
}

/// <summary>The picture a built-in Toolbar Item shows. A Chrome draws it in its own icon set (ADR-0100).</summary>
public enum ToolbarIcon
{
    /// <summary>No picture: the item shows its <see cref="SheetToolbarItemView.Text"/>.</summary>
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
