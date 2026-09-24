namespace ExGrid.Cells;

/// <summary>
/// What a Template Column's fragment receives for one cell (ADR-0020/0037): the row it
/// paints, and whether the core is asking the cell's control to take DOM focus.
///
/// <para><see cref="FocusRequest"/> is how Space enters a Template cell. The core renders
/// none of the fragment's markup and holds no reference to any of it — the rule ADR-0030
/// applies to a Chrome's Cell Editor — so it asks, and a control that wants to be
/// reachable by keyboard focuses <b>itself</b>, with Blazor's own <c>FocusAsync</c>, when it
/// sees a non-zero request it has not acted on yet. A template with nothing to enter — a
/// bar, a badge — ignores it, and Space then leaves the keyboard where it was.</para>
/// </summary>
/// <param name="Row">The row instance this cell belongs to.</param>
/// <param name="FocusRequest">Handed over non-zero on exactly one render of exactly one
/// cell, and zero to every other cell and on every later render of that one. Act once per
/// number: a component that re-renders on its own keeps its last parameters until the row
/// renders again. A row re-created by virtualisation never sees a request meant for an
/// earlier instance of it, so a control acting on this can never steal focus later
/// (ADR-0037).</param>
public readonly record struct TemplateCellContext<TRow>(TRow Row, int FocusRequest);
