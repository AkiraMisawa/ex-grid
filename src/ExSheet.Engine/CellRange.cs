using System.Diagnostics.CodeAnalysis;

namespace ExSheet.Engine;

/// <summary>
/// A rectangle of cells on a Sheet, inclusive at both corners, held from its top-left corner
/// (<see cref="First"/>) to its bottom-right one (<see cref="Last"/>) whichever corners it was made
/// from. Written <c>A1</c> for one cell and <c>A1:B2</c> otherwise.
/// </summary>
public readonly record struct CellRange
{
    /// <summary>The rectangle spanned by two corners, in either order.</summary>
    public CellRange(CellAddress corner1, CellAddress corner2)
    {
        First = new CellAddress(Math.Min(corner1.Row, corner2.Row), Math.Min(corner1.Column, corner2.Column));
        Last = new CellAddress(Math.Max(corner1.Row, corner2.Row), Math.Max(corner1.Column, corner2.Column));
    }

    /// <summary>The one-cell rectangle at <paramref name="cell"/>.</summary>
    public CellRange(CellAddress cell)
        : this(cell, cell)
    {
    }

    /// <summary>The top-left cell.</summary>
    public CellAddress First { get; }

    /// <summary>The bottom-right cell.</summary>
    public CellAddress Last { get; }

    /// <summary>How many rows it spans.</summary>
    public int RowCount => Last.Row - First.Row + 1;

    /// <summary>How many columns it spans.</summary>
    public int ColumnCount => Last.Column - First.Column + 1;

    /// <summary>How many cells it holds.</summary>
    public long CellCount => (long)RowCount * ColumnCount;

    /// <summary>Whether <paramref name="address"/> lies inside it.</summary>
    public bool Contains(CellAddress address) =>
        address.Row >= First.Row && address.Row <= Last.Row && address.Column >= First.Column && address.Column <= Last.Column;

    /// <summary>Its cells in row-major order.</summary>
    public IEnumerable<CellAddress> Cells()
    {
        for (var row = First.Row; row <= Last.Row; row++)
        {
            for (var column = First.Column; column <= Last.Column; column++) yield return new CellAddress(row, column);
        }
    }

    /// <summary>Parses <c>B7</c> or <c>B7:C9</c> (corners in either order, letters in either case). No <c>$</c>.</summary>
    /// <exception cref="FormatException">The text is not a range inside the Sheet's extent.</exception>
    public static CellRange Parse(string text) =>
        TryParse(text, out var range) ? range : throw new FormatException($"'{text}' is not a cell range such as A1 or A1:B2.");

    /// <summary>Parses <c>B7</c> or <c>B7:C9</c> (corners in either order, letters in either case). No <c>$</c>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out CellRange range)
    {
        range = default;
        if (text is null) return false;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            if (!CellAddress.TryParse(text, out var single)) return false;
            range = new CellRange(single);
            return true;
        }
        if (!CellAddress.TryParse(text[..colon], out var a) || !CellAddress.TryParse(text[(colon + 1)..], out var b)) return false;
        range = new CellRange(a, b);
        return true;
    }

    /// <summary><c>A1</c> for one cell, <c>A1:B2</c> otherwise.</summary>
    public override string ToString() => First == Last ? First.ToString() : $"{First}:{Last}";
}
