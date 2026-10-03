using ExGrid.Selection;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// The Keyboard Field (ADR-0080). The fifteenth Windows run found that Japanese typed onto a
// selected cell could not compose: DOM focus was on the root, an element that is not editable,
// Chrome and Edge give it no input context, and the IME stayed off. So while the grid has an
// editable column, the keyboard with no edit open is held by a text field of the grid's own inside
// the root. The capture-phase listener on the root still hears every key first, and takes what it
// takes as before; a key it does not take reaches the field, and an IME composes there. The
// composition is drawn in the field, over the Focus cell. When it ends, its text is handed to the
// core, which opens the Cell Editor in Overwrite holding it, as a typed character opens it holding
// the character (ADR-0010); the listener holds the keys typed after it until the editor has the
// keyboard, as after any key that opens an edit. DOM focus never moves while a composition lasts,
// so the IME's state is never carried anywhere: it finishes where it started.
//
// The field is the grid's one tab stop, and carries aria-activedescendant, since it is the element
// holding the keyboard (ADR-0033, ADR-0080). A display-only grid has none, and keeps both on its root.
public partial class ExGrid<TRow>
{
    private static readonly object KeyFieldLayerKey = new();

    /// <summary>The Keyboard Field stands while the listener is attached and some column edits
    /// (what the key gate was told: a display-only grid keeps the keyboard on its root, and takes no
    /// printable key at all). A Prerendered grid has none: nothing could hear its keys yet
    /// (ADR-0033, ADR-0080).</summary>
    private bool KeyFieldShown => !Prerendered && _jsClaims is { CanEdit: true };

    /// <summary>The root's <c>tabindex</c> (ADR-0033, ADR-0080): none while Prerendered; <c>-1</c>
    /// while its Keyboard Field is the one tab stop, so that Shift+Tab from the field leaves the grid
    /// instead of landing on the root, which would pass focus straight back (ADR-0012's release of
    /// Tab); and <c>0</c> on a grid without a field, whose root is the tab stop.</summary>
    private string? RootTabIndex => Prerendered ? null : KeyFieldShown ? "-1" : "0";

    /// <summary>Where the Keyboard Field stands, and whether it rides the pinned layer's sticky
    /// anchor: over the Focus cell, in the Cell Editor's box from the same arithmetic; over the first
    /// painted cell with no Focus, where the first key would open the edit (ADR-0012); and, with the
    /// Focus not painted, at the painted rows' top-left, held at the Viewport's left edge, until a
    /// composition's start reveals the Focus (ADR-0080).</summary>
    private (string Layer, string Style) KeyFieldBox()
    {
        if (_visible is { } visible
            && (_selection.Selection.IsEmpty ? FirstVisibleCell() : _selection.Selection.Focus) is { } cell)
        {
            var range = SelectionStyles.CellRange(cell);
            if (SelectionStyles.Pinned(range, _columnStyles.Geometry, _metrics.RowHeightPx, visible) is { } pinned)
                return (KeyFieldHeldLayer, pinned);
            if (SelectionStyles.Scrollable(range, _columnStyles.Geometry, _metrics.RowHeightPx, visible) is { } scrollable)
                return (KeyFieldLayer, scrollable);
        }
        return (KeyFieldHeldLayer, KeyFieldWaiting);
    }

    private const string KeyFieldLayer = "ex-key-field-layer";
    private const string KeyFieldHeldLayer = "ex-key-field-layer ex-key-field-held";
    private const string KeyFieldWaiting = "left: 0px; top: 0px; width: 0px; height: 0px";

    /// <summary>Read-only over a cell that typing would not open — not Editable, or over a row not
    /// in hand — so an IME stays off there, as it did on the root (Chrome gives a read-only field
    /// no input context either; ADR-0080). With no Focus the first key places one, so the field
    /// takes typing.</summary>
    private bool KeyFieldReadOnly()
        => !_selection.Selection.IsEmpty && !IsEditableCell(_selection.Selection.Focus);

    /// <summary>
    /// An IME has started a composition in the Keyboard Field (ADR-0080): the Focus is revealed, so
    /// the field stands over it and the composition is drawn there, as Excel scrolls to the active
    /// cell when typing begins. Nothing else changes: no edit opens until the composition ends.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public only
    /// because JavaScript interop requires it.</para>
    /// </summary>
    [JSInvokable]
    public Task OnKeyFieldCompositionStartAsync()
    {
        if (_disposed || _editMode != EditMode.None || _selection.Selection.IsEmpty || FocusCellIsPainted())
            return Task.CompletedTask;
        RequestFocusReveal();
        _suppressRender = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// A composition in the Keyboard Field has ended with <paramref name="text"/> — what the IME
    /// committed, or nothing when it was cancelled (ADR-0080): the Cell Editor opens in Overwrite
    /// holding it, on the Focus cell, judged as a typed character is (ADR-0010/0035). An empty text
    /// opens an empty edit, as Excel's does when Escape ends a composition (the fifteenth Windows
    /// run, i2). Over a cell that does not edit nothing opens. Answers whether an edit is open now;
    /// the listener empties the field when none is.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public only
    /// because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="text">The composition's text.</param>
    [JSInvokable]
    public async Task<bool> OnKeyFieldTextAsync(string text)
    {
        if (_disposed)
            return false;
        if (_editMode == EditMode.None)
            await TryStartEditingAsync(EditMode.Overwrite, text);
        return _editMode != EditMode.None;
    }
}
