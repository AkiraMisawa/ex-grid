namespace ExGrid.Cells;

/// <summary>
/// What a Template Column's fragment receives for one cell (ADR-0020/0037): the row it
/// paints, and whether the core is asking the cell's control to take DOM focus.
///
/// <para><see cref="FocusRequest"/> is how Space enters a Template cell. The core renders
/// none of the fragment's markup and holds no reference to any of it — the rule ADR-0030
/// applies to a Chrome's Cell Editor — so it asks, and a control that wants to be
/// reachable by keyboard takes the keyboard when it sees a non-zero request it has not acted on
/// yet: through the grid's <c>HandKeyboardToAsync</c>, handed its own element, and not with its own
/// <c>FocusAsync</c> (ADR-0204). On Blazor Server the request lands a round trip after the Space,
/// and the grid grants it only while the keyboard is still its own — DOM focus inside its root or on
/// nothing — so another grid the user has pressed in the meantime keeps it. A template with nothing
/// to enter — a bar, a badge — ignores it, and Space then leaves the keyboard where it was.</para>
///
/// <para><b>A control's write is the Consumer's to judge.</b> The grid lands an Action press on the
/// row it was pressed on, as that row is now, and refuses one whose row is gone (ADR-0142), but it
/// cannot see what a template's control does. A handler receives <see cref="Row"/>, the row the
/// template was painted with: a control that writes resolves it to the Consumer's current version —
/// with a Row Key, the one that names it across versions (ADR-0140) — and refuses where it must.</para>
/// </summary>
/// <param name="Row">The row instance this cell belongs to.</param>
/// <param name="FocusRequest">Handed over non-zero on exactly one render of exactly one
/// cell, and zero to every other cell and on every later render of that one. Act once per
/// number: a component that re-renders on its own keeps its last parameters until the row
/// renders again. A row re-created by virtualisation never sees a request meant for an
/// earlier instance of it, so a control acting on this can never steal focus later
/// (ADR-0037).</param>
public readonly record struct TemplateCellContext<TRow>(TRow Row, int FocusRequest);
