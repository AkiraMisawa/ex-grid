namespace ExGrid.Components;

/// <summary>
/// The kinds of event a control claims (<c>data-ex-claims</c>) and the script tells (<c>InputTakenAt</c>),
/// in the browser's names (ADR-0240 section 2): every place the core names one — a handler claiming its
/// number, the markup declaring it — names it from here, so a misspelt kind is a compile error rather
/// than a number told that no handler ever takes.
/// </summary>
internal static class ClaimKinds
{
    /// <summary>A click.</summary>
    public const string Click = "click";

    /// <summary>A double click.</summary>
    public const string DoubleClick = "dblclick";

    /// <summary>A context menu.</summary>
    public const string ContextMenu = "contextmenu";

    /// <summary>A press of the pointer's button.</summary>
    public const string Press = "mousedown";

    /// <summary>A key going down.</summary>
    public const string KeyDown = "keydown";

    /// <summary>A form's submission.</summary>
    public const string Submit = "submit";

    /// <summary>An element taking DOM focus.</summary>
    public const string Focus = "focus";

    /// <summary>The keyboard coming into the Input Root.</summary>
    public const string KeyboardIn = "focusin";

    /// <summary>The keyboard leaving the Input Root.</summary>
    public const string KeyboardOut = "focusout";
}
