using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What a user types into the Name Box, read as a place on the Sheet (ADR-0051): a cell
/// (<c>D200</c>), a range (<c>A1:C3</c>), whole columns (<c>B:D</c>) or whole rows
/// (<c>5:9</c>), in either case, with or without <c>$</c> markers, as Excel's Name Box takes
/// them. Anything else is not an address, and nothing moves.
/// </summary>
internal static class NameBoxText
{
    /// <summary>Reads <paramref name="typed"/> as a range of the Sheet.</summary>
    internal static bool TryParse([NotNullWhen(true)] string? typed, out CellRange range)
    {
        range = default;
        if (typed is null) return false;
        var text = typed.Trim().Replace("$", "", StringComparison.Ordinal);
        if (text.Length == 0) return false;
        if (CellRange.TryParse(text, out range)) return true;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || colon == text.Length - 1) return false;
        var (a, b) = (text[..colon], text[(colon + 1)..]);
        if (CellAddress.TryParseColumn(a, out var firstColumn) && CellAddress.TryParseColumn(b, out var lastColumn))
        {
            range = new CellRange(new CellAddress(0, firstColumn), new CellAddress(Sheet.RowCount - 1, lastColumn));
            return true;
        }
        if (TryParseRow(a, out var firstRow) && TryParseRow(b, out var lastRow))
        {
            range = new CellRange(new CellAddress(firstRow, 0), new CellAddress(lastRow, Sheet.ColumnCount - 1));
            return true;
        }
        return false;
    }

    private static bool TryParseRow(string text, out int row)
    {
        row = -1;
        if (text.Length == 0 || !text.All(char.IsAsciiDigit) || text[0] == '0') return false;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > Sheet.RowCount) return false;
        row = number - 1;
        return true;
    }
}
