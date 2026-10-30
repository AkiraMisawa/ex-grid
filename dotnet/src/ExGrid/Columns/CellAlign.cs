namespace ExGrid.Columns;

/// <summary>
/// Alignment is a closed enum, not a stylesheet hook (ADR-0016/0029). <see cref="Auto"/>
/// derives from the type — Number/Date right, Text/Boolean left, exactly the
/// <c>ex-cell-numeric</c> behaviour — and an explicit value beats the derivation, which
/// is safe: <c>####</c>, the ellipsis rule and copy are all alignment-blind. Painted as
/// interned <c>ex-align-*</c> classes. There is no vertical alignment anywhere in the
/// grid: a single-line fixed row centres by construction.
/// </summary>
public enum CellAlign
{
    /// <summary>Derived: a cell aligns by its column type — Number and Date right, Text
    /// and Boolean left — and a header aligns left.</summary>
    Auto = 0,

    /// <summary>Against the cell's left edge.</summary>
    Left,

    /// <summary>Centred in the cell.</summary>
    Center,

    /// <summary>Against the cell's right edge.</summary>
    Right,
}
