using ExGrid.Keys;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// Declared keys (ADR-0050, item 14): keys a Consumer names, which the core claims from the browser
// and raises, with whether an edit is open, in either state. Without the declaration a key stays
// the browser's, and a declaration never takes a key the core answers itself (ADR-0010).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0050, item 14): keys the core claims from the browser and
    /// raises through <see cref="OnDeclaredKey"/>, whether or not an edit is open — ExSheet's
    /// formatting keys, Ctrl+B and Ctrl+Shift+$ among them. Each is in
    /// <see cref="GridKeys.Canonical"/>'s form and matched exactly: <c>Control+b</c> and, for
    /// CapsLock, <c>Control+B</c>; a character the layout may type with or without Shift —
    /// <c>#</c> needs none on a UK layout — in both forms. A key the core answers itself, in any
    /// state, is refused by name, as is a key not in the canonical form, which nothing would ever
    /// match. Claimed only while the grid holds the keyboard on its root or in an editor surface:
    /// a control inside the grid keeps its own keys. Requires <see cref="OnDeclaredKey"/>. Null —
    /// the default — claims none, and every key stays the browser's as before. Nothing per cell
    /// reaches JavaScript: the listener is handed this list with the core's own (ADR-0021).
    /// </summary>
    [Parameter] public IReadOnlyCollection<string>? DeclaredKeys { get; set; }

    /// <summary>
    /// A declared key, pressed (ADR-0050, item 14): the key as it was declared, whether an edit
    /// was open, and the Selection as the grid holds it at the key, with the Row Sequence Version
    /// it is written in. A key that acts on the Selection acts on that one: the
    /// <see cref="SelectionChanged"/> for a move made just before the key can arrive after it on a
    /// circuit. Raised and not interpreted — the grid moves nothing, opens nothing and leaves an
    /// open edit as it was, its text and caret included; while one is open, the Consumer decides
    /// what the key does. Required with <see cref="DeclaredKeys"/>: a key claimed for nobody would
    /// be a key taken from the page for nothing.
    /// </summary>
    [Parameter] public EventCallback<GridDeclaredKeyPress> OnDeclaredKey { get; set; }

    // The declared keys, checked, and the collection they were read from: an unchanged
    // declaration is not read again, and one that names the same keys keeps the same set, so
    // the gate is re-told only when what it claims has changed.
    private IReadOnlySet<string> _declared = GridKeys.Declare(null);
    private IReadOnlyCollection<string>? _declaredFrom;
    // The declared keys the gate was last told.
    private IReadOnlySet<string>? _jsDeclared;

    /// <summary>Checks the declared keys and keeps them, refusing a declaration by name
    /// (ADR-0050, item 14).</summary>
    private void ResolveDeclaredKeys()
    {
        if (!ReferenceEquals(DeclaredKeys, _declaredFrom))
        {
            var declared = GridKeys.Declare(DeclaredKeys);
            _declaredFrom = DeclaredKeys;
            if (!declared.SetEquals(_declared))
                _declared = declared;
        }
        if (_declared.Count > 0 && !OnDeclaredKey.HasDelegate)
        {
            throw new ArgumentException(
                "DeclaredKeys names keys, but OnDeclaredKey has no delegate: the keys would be taken from the page " +
                "and nobody told (ADR-0050, item 14).", nameof(OnDeclaredKey));
        }
    }

    /// <summary>Every key the gate takes while no edit is open: the core's own for this grid's
    /// claims, and the declared ones (ADR-0010, ADR-0050 item 14).</summary>
    private IReadOnlyList<string> TakenKeys(GridKeyClaims claims)
        => _declared.Count == 0 ? GridKeys.TakenFor(claims) : [.. GridKeys.TakenFor(claims), .. _declared];

    /// <summary>The declared keys as the gate is told them, for its editing branch, where it
    /// claims them beside the editor's own.</summary>
    private string[] DeclaredForGate() => [.. _declared];

    /// <summary>
    /// Raises <paramref name="canonical"/> when it is declared (ADR-0050, item 14), and answers
    /// whether it was: the key then means nothing more to the core, in either state.
    /// </summary>
    private async Task<bool> RaiseDeclaredKeyAsync(string canonical, bool editOpen)
    {
        if (!_declared.Contains(canonical))
            return false;
        await OnDeclaredKey.InvokeAsync(new GridDeclaredKeyPress(canonical, editOpen, _selection.Selection, _sequenceVersion));
        return true;
    }
}
