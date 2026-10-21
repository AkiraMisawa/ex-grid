using System.Globalization;

namespace ExGrid.Data.Xlsx;

/// <summary>A cell's place written as <c>B7</c>, and read from it, within Excel's grid of 1,048,576 rows and 16,384 columns.</summary>
public static class XlsxCellReference
{
    /// <summary>The rows a worksheet has: 1,048,576.</summary>
    public const int MaxRows = 1_048_576;

    /// <summary>The columns a worksheet has: 16,384 (A to XFD).</summary>
    public const int MaxColumns = 16_384;

    /// <summary>The cell at <paramref name="row"/> and <paramref name="column"/>, both from zero, as <c>B7</c>.</summary>
    /// <param name="row">The row, from zero.</param>
    /// <param name="column">The column, from zero.</param>
    public static string Format(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        return ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The name of the column <paramref name="column"/>, from zero: 0 is <c>A</c>, 26 is <c>AA</c>.</summary>
    /// <param name="column">The column, from zero.</param>
    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        Span<char> letters = stackalloc char[8];
        var at = letters.Length;
        var n = column + 1;
        while (n > 0)
        {
            var rem = (n - 1) % 26;
            letters[--at] = (char)('A' + rem);
            n = (n - 1) / 26;
        }
        return new string(letters[at..]);
    }

    /// <summary>Reads <c>B7</c> into its row and column, from zero; false for text that is not a cell inside the grid.</summary>
    /// <param name="text">The reference, as <c>B7</c>; <c>$</c> marks are allowed.</param>
    /// <param name="row">The row, from zero.</param>
    /// <param name="column">The column, from zero.</param>
    public static bool TryParse(ReadOnlySpan<char> text, out int row, out int column)
    {
        row = column = -1;
        var i = 0;
        if (i < text.Length && text[i] == '$')
            i++;
        var col = 0;
        var letters = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            col = col * 26 + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            i++;
            if (++letters > 3)
                return false;
        }
        if (letters == 0 || col > MaxColumns)
            return false;
        if (i < text.Length && text[i] == '$')
            i++;
        var r = 0;
        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            r = r * 10 + (text[i] - '0');
            i++;
            if (++digits > 7)
                return false;
        }
        if (digits == 0 || i != text.Length || r < 1 || r > MaxRows)
            return false;
        row = r - 1;
        column = col - 1;
        return true;
    }
}
