namespace ExGrid.Components;

/// <summary>
/// What one row paints of its cells' appearance (ADR-0050, item 15): each painted cell's classes,
/// and whether it is bold, resolved by the grid once, outside the row's render, from the
/// Consumer's answers for the row and the rows either side of it. Immutable, and replaced only
/// when what the row paints changed, so its identity is the row's change signal, as a row
/// instance's is (ADR-0003).
///
/// <para>Public only because <see cref="ExGridRow{TRow}"/> is: the row is the documented seam the
/// render-count tests watch. A Consumer neither makes nor reads one.</para>
/// </summary>
public sealed class RowAppearance
{
    private readonly int _pinned;
    private readonly int _scrollStart;
    private readonly string?[] _classes;
    private readonly bool[] _bold;
    private readonly AppearanceStyles _styles;

    internal RowAppearance(int pinned, int scrollStart, string?[] classes, bool[] bold, AppearanceStyles styles)
    {
        _pinned = pinned;
        _scrollStart = scrollStart;
        _classes = classes;
        _bold = bold;
        _styles = styles;
    }

    /// <summary>The cell's whole class attribute: <paramref name="baseClass"/> with the
    /// appearance's classes after it, interned (P5), or the base alone.</summary>
    internal string ClassOf(int column, string baseClass)
        => Slot(column) is var slot && slot >= 0 && _classes[slot] is { } appearance
            ? _styles.Join(baseClass, appearance)
            : baseClass;

    /// <summary>Whether the cell is bold, so judged by the bold widths (ADR-0016).</summary>
    internal bool IsBold(int column) => Slot(column) is var slot && slot >= 0 && _bold[slot];

    /// <summary>Whether <paramref name="other"/> paints exactly what this does, for the same painted
    /// columns: then the row keeps the instance it has, and skips.</summary>
    internal bool SameAs(RowAppearance other)
    {
        if (_pinned != other._pinned || _scrollStart != other._scrollStart || _classes.Length != other._classes.Length)
            return false;
        // The classes are interned, so a reference comparison is an equality one.
        for (var i = 0; i < _classes.Length; i++)
        {
            if (!ReferenceEquals(_classes[i], other._classes[i]) || _bold[i] != other._bold[i])
                return false;
        }
        return true;
    }

    // The painted cells in order: the Pinned Columns, then the scrollable ones on screen.
    private int Slot(int column)
    {
        var slot = column < _pinned ? column : _pinned + column - _scrollStart;
        return slot >= 0 && slot < _classes.Length && (column < _pinned || column >= _scrollStart) ? slot : -1;
    }
}
