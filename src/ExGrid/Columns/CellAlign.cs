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
    Auto = 0,
    Left,
    Center,
    Right,
}
