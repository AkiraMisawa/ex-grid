namespace ExGrid.Keys;

/// <summary>
/// What one grid can do, as far as its key claims are concerned (ADR-0007/0010/0046): the
/// facts that decide whether a conditionally claimed key is taken from the page. Read by
/// <see cref="GridKeys.TakenFor"/>.
/// </summary>
/// <param name="CanEdit">Whether any column is Editable. The writing keys — Delete,
/// Backspace, Ctrl+D, Ctrl+R — are claimed only then.</param>
/// <param name="CanUndo">Whether someone listens for undo. Ctrl+Z is claimed only then.</param>
/// <param name="CanRedo">Whether someone listens for redo. Ctrl+Y and Ctrl+Shift+Z are
/// claimed only then.</param>
public readonly record struct GridKeyClaims(bool CanEdit, bool CanUndo, bool CanRedo);
