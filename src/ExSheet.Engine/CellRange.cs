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

    /// <summary>Whether it spans every row: whole columns, such as <c>B:D</c> (ADR-0047).</summary>
    public bool IsWholeColumns => First.Row == 0 && Last.Row == Sheet.RowCount - 1;

    /// <summary>Whether it spans every column: whole rows, such as <c>2:4</c> (ADR-0047).</summary>
    public bool IsWholeRows => First.Column == 0 && Last.Column == Sheet.ColumnCount - 1;

    /// <summary>Whole columns <paramref name="first"/> to <paramref name="last"/> (0 is <c>A</c>), as <c>A:A</c> names one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A column is outside <c>A</c> to <c>XFD</c>.</exception>
    public static CellRange WholeColumns(int first, int last) =>
        new(new CellAddress(0, first), new CellAddress(Sheet.RowCount - 1, last));

    /// <summary>Whole rows <paramref name="first"/> to <paramref name="last"/> (0 is row <c>1</c>), as <c>1:1</c> names one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A row is outside 1 to 1,048,576.</exception>
    public static CellRange WholeRows(int first, int last) =>
        new(new CellAddress(first, 0), new CellAddress(last, Sheet.ColumnCount - 1));

    /// <summary>Its cells in row-major order.</summary>
    public IEnumerable<CellAddress> Cells()
    {
        for (var row = First.Row; row <= Last.Row; row++)
        {
            for (var column = First.Column; column <= Last.Column; column++) yield return new CellAddress(row, column);
        }
    }

    /// <summary>Parses <c>B7</c>, <c>B7:C9</c> (corners in either order, letters in either case), whole columns <c>B:D</c> or whole rows <c>2:4</c>. No <c>$</c>.</summary>
    /// <exception cref="FormatException">The text is not a range inside the Sheet's extent.</exception>
    public static CellRange Parse(string text) =>
        TryParse(text, out var range) ? range : throw new FormatException($"'{text}' is not a cell range such as A1 or A1:B2.");

    /// <summary>Parses <c>B7</c>, <c>B7:C9</c> (corners in either order, letters in either case), whole columns <c>B:D</c> or whole rows <c>2:4</c>. No <c>$</c>.</summary>
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
        var left = text[..colon];
        var right = text[(colon + 1)..];
        if (CellAddress.TryParseColumn(left, out var c1) && CellAddress.TryParseColumn(right, out var c2))
        {
            range = WholeColumns(Math.Min(c1, c2), Math.Max(c1, c2));
            return true;
        }
        if (TryParseRow(left, out var r1) && TryParseRow(right, out var r2))
        {
            range = WholeRows(Math.Min(r1, r2), Math.Max(r1, r2));
            return true;
        }
        if (!CellAddress.TryParse(left, out var a) || !CellAddress.TryParse(right, out var b)) return false;
        range = new CellRange(a, b);
        return true;
    }

    private static bool TryParseRow(string text, out int row)
    {
        row = -1;
        if (text.Length is 0 or > 7 || !text.All(char.IsAsciiDigit) || text[0] == '0') return false;
        var n = int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        if (n > Sheet.RowCount) return false;
        row = n - 1;
        return true;
    }

    /// <summary><c>A1</c> for one cell, <c>A1:B2</c> otherwise.</summary>
    public override string ToString() => First == Last ? First.ToString() : $"{First}:{Last}";
}
