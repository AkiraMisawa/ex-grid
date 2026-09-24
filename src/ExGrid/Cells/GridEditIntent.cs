namespace ExGrid.Cells;

/// <summary>
/// The Edit Intent (ADR-0007, `CONTEXT.md`): the notification the grid raises when a
/// user commits an edit — row identity, column, new value. The grid changes nothing
/// itself; the screen changes when the Consumer returns new row instances, with the
/// override recorded in its Overlay.
/// </summary>
/// <param name="Row">The row instance the edit lands on — Row Identity, not a position:
/// the Consumer resolves it into its own key.</param>
/// <param name="Column">The column's name.</param>
/// <param name="Value">The editor's text as committed. The Consumer parses it per its
/// own column type; refusing an unparseable value is validation, which is the
/// Consumer's (ADR-0007).</param>
public sealed record GridEditIntent<TRow>(TRow Row, string Column, string Value);
