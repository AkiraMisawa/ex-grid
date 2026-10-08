namespace ExGrid.Cells;

/// <summary>
/// The Edit Intent (ADR-0007, <c>CONTEXT.md</c>): the notification the grid raises when a
/// user commits an edit — row identity, column, new value. The grid changes nothing
/// itself; the screen changes when the Consumer returns new row instances, with the
/// override recorded in its Overlay.
///
/// <para>A commit lands as the user entered it (ADR-0142, rewritten 2026-10-07). The intent
/// carries the text the user saw of the cell, <see cref="SeenText"/>, and what it painted as the
/// commit landed, <see cref="ReplacedText"/>: when the two differ, the value changed under the
/// editor, and the grid raises an Overwrite Notice with the same two texts once the intent is
/// accepted; when they are equal it raises none. The user's own earlier writes count as seen (D1),
/// on every host alike, so a Consumer comparing the two never finds its user's own value given as a
/// change. A Consumer that must not write over a change — or whose store, a reducer or a server,
/// is where writes are ordered — declines the intent with <see cref="Refuse"/> before its handler
/// completes, and the editor stays open with the typing (ADR-0034's note of 2026-10-07).</para>
/// </summary>
/// <param name="Row">The row instance the edit lands on — Row Identity, not a position:
/// the Consumer resolves it into its own key.</param>
/// <param name="Column">The column's name.</param>
/// <param name="Value">The editor's text as committed. The Consumer parses it per its
/// own column type; refusing an unparseable value is validation, which is the
/// Consumer's (ADR-0007).</param>
public sealed record GridEditIntent<TRow>(TRow Row, string Column, string Value)
{
    /// <summary>The text the user saw of the edited cell (ADR-0142, D1): what it painted when the
    /// editor opened over it — or, where one of the user's own earlier writes to the cell had not
    /// settled then, what it painted once that write settled, the user's own value. The text the
    /// edited cell painted when the commit landed, where it painted none at the open. Null where the
    /// grid raised the intent without one.</summary>
    public string? SeenText { get; init; }

    /// <summary>The text the edited cell painted as the commit landed: what the commit replaces
    /// (ADR-0142). Equal to <see cref="SeenText"/> unless the value changed while the editor
    /// covered the cell, and then the grid raises an Overwrite Notice with the same two texts once
    /// the intent is accepted.</summary>
    public string? ReplacedText { get; init; }

    /// <summary>The message the Consumer refused the intent with (<see cref="Refuse"/>), or null
    /// while it has not.</summary>
    public string? RefusalMessage { get; private set; }

    /// <summary>Whether the Consumer refused the intent through <see cref="Refuse"/>.</summary>
    public bool IsRefused => RefusalMessage is not null;

    /// <summary>
    /// The Consumer's answer that it does not write this edit (ADR-0142, ADR-0034's note of
    /// 2026-10-07), the edit's counterpart of <c>GridPasteIntent.Refuse()</c> and
    /// <c>GridFillIntent.Refuse()</c> (ADR-0050, item 3). Called before the <c>OnEdit</c> handler
    /// completes, it refuses the commit: the editor stays open with the text typed, the gesture that
    /// committed keeps no meaning of its own, and <paramref name="message"/> is shown at the editor
    /// and in the root's live region, as an Edit Verdict's Reject is. A later commit raises the
    /// intent again. A handler that completes without calling it has accepted; one called after
    /// is not heard, since by then the editor is closed.
    /// </summary>
    /// <param name="message">The Consumer's sentence: the grid holds none of its own.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty: a refusal that
    /// says nothing is the silence ADR-0034 rules out.</exception>
    public void Refuse(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        RefusalMessage = message;
    }
}
