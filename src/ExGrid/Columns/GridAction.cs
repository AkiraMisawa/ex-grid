namespace ExGrid.Columns;

/// <summary>
/// One declared action in an Action Column (ADR-0020). The Consumer supplies what it
/// looks like — a label, or a class naming an icon — and the core holds the meaning:
/// pressing this fires something, and the grid reports it without doing it.
///
/// <para><b>How many actions a cell carries is declared, never inferred</b> from the
/// markup Chrome happened to paint: the keyboard's behaviour differs at one action and at
/// several, and counting focusable elements would make that behaviour depend on the theme
/// (ADR-0010 / ADR-0020).</para>
/// </summary>
public sealed record GridAction
{
    /// <summary>Declares one action. <paramref name="name"/> and <paramref name="label"/>
    /// are both required: the label is the button's accessible name even when
    /// <paramref name="cssClass"/> paints an icon.</summary>
    public GridAction(string name, string label, string? cssClass = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        // Always required, even when an icon carries the appearance: it is the button's
        // accessible name, and an icon-only control without one is unreachable by
        // anything but a mouse.
        ArgumentException.ThrowIfNullOrEmpty(label);
        Name = name;
        Label = label;
        CssClass = cssClass;
    }

    /// <summary>How the Consumer recognises this action when it is reported back.</summary>
    public string Name { get; }

    /// <summary>The text on the button, and its accessible name when
    /// <see cref="CssClass"/> paints an icon instead.</summary>
    public string Label { get; }

    /// <summary>An extra class on the button, for a theme's icon. Appearance only.</summary>
    public string? CssClass { get; }
}
