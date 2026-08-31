namespace ExGrid.Selection;

/// <summary>
/// A Selection paired with the Row Sequence Version it was made under. Selection
/// coordinates are positions in the current order, so when the version changes the
/// selection is dropped, never remapped (ADR-0011) — and this pairing is where that
/// rule lives in code, rather than as a convention every holder re-implements. The
/// component adopts it when it grows selection; a Consumer holding selection itself
/// goes through the same gate.
///
/// (<c>default(HeldSelection)</c> has a null Selection — the same tolerated
/// struct-default hole as <c>default(SelectionRange)</c>; start from
/// <see cref="Empty"/>.)
/// </summary>
public readonly record struct HeldSelection(int RowSequenceVersion, GridSelection Selection)
{
    public static HeldSelection Empty(int rowSequenceVersion) => new(rowSequenceVersion, GridSelection.Empty);

    /// <summary>
    /// Reconciles with the version now current: the same version keeps the selection,
    /// any other drops it. There is no "newer" — versions only identify orders, so any
    /// difference means the positions no longer name the rows the user selected
    /// (ADR-0011).
    /// </summary>
    public HeldSelection Under(int currentRowSequenceVersion)
        => currentRowSequenceVersion == RowSequenceVersion ? this : Empty(currentRowSequenceVersion);

    /// <summary>Replaces the selection under the same version — the ordinary result of
    /// a selection gesture.</summary>
    public HeldSelection With(GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return this with { Selection = selection };
    }
}
