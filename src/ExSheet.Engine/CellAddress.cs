using System.Diagnostics.CodeAnalysis;

namespace ExSheet.Engine;

/// <summary>
/// Where a cell is on a Sheet: a zero-based row and column inside Excel's extent. Written in A1
/// form (<c>A1</c> is row 0, column 0; <c>XFD1048576</c> is the last cell). An address is where a
/// cell is; a Reference is how a Formula names it (CONTEXT.md).
/// </summary>
/// <param name="Row">The zero-based row, 0 to <see cref="Sheet.RowCount"/> − 1.</param>
/// <param name="Column">The zero-based column, 0 to <see cref="Sheet.ColumnCount"/> − 1.</param>
public readonly record struct CellAddress(int Row, int Column) : IComparable<CellAddress>
{
    /// <summary>
    /// Validates the position; an address outside the Sheet's extent is refused, never clamped.
    /// </summary>
    public int Row { get; } = (uint)Row < Sheet.RowCount
        ? Row
        : throw new ArgumentOutOfRangeException(nameof(Row), Row, $"A row is 0 to {Sheet.RowCount - 1}.");

    /// <summary>
    /// Validates the position; an address outside the Sheet's extent is refused, never clamped.
    /// </summary>
    public int Column { get; } = (uint)Column < Sheet.ColumnCount
        ? Column
        : throw new ArgumentOutOfRangeException(nameof(Column), Column, $"A column is 0 to {Sheet.ColumnCount - 1}.");

    /// <summary>Parses an address in A1 form, such as <c>B7</c> or <c>xfd1048576</c>.</summary>
    /// <exception cref="FormatException">The text is not an address inside the Sheet's extent.</exception>
    public static CellAddress Parse(string text) =>
        TryParse(text, out var address)
            ? address
            : throw new FormatException($"'{text}' is not a cell address from A1 to XFD1048576.");

    /// <summary>
    /// Parses an address in A1 form. Letters may be in either case; <c>$</c> markers are not part
    /// of an address (they belong to a Reference) and are refused here.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out CellAddress address)
    {
        address = default;
        if (string.IsNullOrEmpty(text)) return false;
        var i = 0;
        var column = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            if (i == 3) return false;
            column = column * 26 + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            i++;
        }
        if (i == 0 || i == text.Length) return false;
        if (text[i] == '0') return false;
        long row = 0;
        for (; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i]) || row > Sheet.RowCount) return false;
            row = row * 10 + (text[i] - '0');
        }
        if (column > Sheet.ColumnCount || row > Sheet.RowCount) return false;
        address = new CellAddress((int)row - 1, column - 1);
        return true;
    }

    /// <summary>The column's letters: 0 is <c>A</c>, 25 is <c>Z</c>, 26 is <c>AA</c>, 16,383 is <c>XFD</c>.</summary>
    public static string ColumnName(int column)
    {
        if ((uint)column >= Sheet.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), column, $"A column is 0 to {Sheet.ColumnCount - 1}.");
        Span<char> buffer = stackalloc char[3];
        var at = buffer.Length;
        var n = column + 1;
        while (n > 0)
        {
            n--;
            buffer[--at] = (char)('A' + n % 26);
            n /= 26;
        }
        return new string(buffer[at..]);
    }

    /// <summary>Parses column letters (<c>A</c> to <c>XFD</c>, either case) to a zero-based column.</summary>
    public static bool TryParseColumn([NotNullWhen(true)] string? letters, out int column)
    {
        column = -1;
        if (string.IsNullOrEmpty(letters) || letters.Length > 3) return false;
        var n = 0;
        foreach (var c in letters)
        {
            if (!char.IsAsciiLetter(c)) return false;
            n = n * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }
        if (n > Sheet.ColumnCount) return false;
        column = n - 1;
        return true;
    }

    /// <summary>Row-major order: by row, then by column.</summary>
    public int CompareTo(CellAddress other) =>
        Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);

    /// <summary>The address in A1 form, such as <c>C12</c>.</summary>
    public override string ToString() => ColumnName(Column) + (Row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
