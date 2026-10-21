using System.ComponentModel;

namespace ExGrid.Components;

/// <summary>The field text carried by a numbered browser input (ADR-0240 section 5).</summary>
/// <remarks>Only for the grid's script interop; not a Consumer contract (ADR-0201).</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GridInputText
{
    /// <summary>Creates an interop payload.</summary>
    public GridInputText() { }

    /// <summary>The surface: <c>cell</c>, <c>bar</c> or <c>name</c>.</summary>
    public string Surface { get; set; } = "";

    /// <summary>The text the browser field holds.</summary>
    public string Text { get; set; } = "";

    /// <summary>The caret offset, or -1 when the field has no text selection.</summary>
    public int Caret { get; set; } = -1;

    /// <summary>The browser input event's revision, shared by inputs carrying that text;
    /// -1 for a rendered value with no matching input event in this edit,
    /// or 0 for a caller that does not supply revisions.</summary>
    public long Revision { get; set; }
}
