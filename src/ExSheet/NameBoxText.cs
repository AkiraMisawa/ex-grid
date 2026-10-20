using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What a user types into the Name Box, read as a place on the Sheet (ADR-0051): a cell
/// (<c>D200</c>), a range (<c>A1:C3</c>), whole columns (<c>B:D</c>) or whole rows
/// (<c>5:9</c>), in either case, with or without <c>$</c> markers, as Excel's Name Box takes
/// them. Anything else is not an address, and nothing moves. A Sheet's name may come before the
/// address, as a Reference qualifies one (<c>Sheet2!B5</c>, <c>'Q1 Sales'!A1:C3</c>), and the
/// Name Box then goes to that Sheet (ADR-0331 §5).
/// </summary>
internal static class NameBoxText
{
    /// <summary>
    /// Reads <paramref name="typed"/> as a range, with the name of the Sheet it is on when one is
    /// typed before it: <paramref name="sheet"/> is <see langword="null"/> when none is, and the
    /// range is then on the Sheet shown. A name in quotes doubles a quote inside it, as a Reference
    /// writes one.
    /// </summary>
    internal static bool TryParse([NotNullWhen(true)] string? typed, out string? sheet, out CellRange range)
    {
        sheet = null;
        range = default;
        if (typed is null) return false;
        var text = typed.Trim();
        if (text.StartsWith('\''))
        {
            var name = new System.Text.StringBuilder();
            var i = 1;
            while (true)
            {
                if (i >= text.Length) return false;
                if (text[i] == '\'')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\'') { name.Append('\''); i += 2; continue; }
                    break;
                }
                name.Append(text[i++]);
            }
            if (name.Length == 0 || i + 1 >= text.Length || text[i + 1] != '!') return false;
            sheet = name.ToString();
            text = text[(i + 2)..];
        }
        else if (text.LastIndexOf('!') is var bang and >= 0)
        {
            if (bang == 0) return false;
            sheet = text[..bang];
            text = text[(bang + 1)..];
        }
        return TryParse(text, out range);
    }

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
