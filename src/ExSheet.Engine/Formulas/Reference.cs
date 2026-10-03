using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>What a Reference names: one cell, a rectangle, whole columns or whole rows.</summary>
internal enum ReferenceShape
{
    Cell,
    Area,
    Columns,
    Rows,
}

/// <summary>
/// A Reference as a Formula records it. The target positions are held absolutely, with a flag per
/// axis saying whether the text wrote <c>$</c>; a relative Reference shifts when its Formula is
/// copied (ADR-0048), and every form survives a structural edit by rewriting the positions. The
/// Sheet qualifier is recorded as written (ADR-0046: one Sheet today, several later).
/// </summary>
internal sealed record Reference(
    string? SheetName,
    ReferenceShape Shape,
    int Row1,
    int Column1,
    int Row2,
    int Column2,
    bool Row1Absolute,
    bool Column1Absolute,
    bool Row2Absolute,
    bool Column2Absolute)
{
    /// <summary>
    /// <c>A1#</c>: the Spill Range of the Formula in this one cell (ADR-0125). Its dependency is the
    /// Anchor's cell, and it moves as the cell's Reference moves.
    /// </summary>
    public bool Spilled { get; init; }

    /// <summary>The rectangle the Reference covers.</summary>
    public Area Area => Shape switch
    {
        ReferenceShape.Columns => new Area(0, Column1, Sheet.RowCount - 1, Column2),
        ReferenceShape.Rows => new Area(Row1, 0, Row2, Sheet.ColumnCount - 1),
        _ => new Area(Row1, Column1, Row2, Column2),
    };

    /// <summary>
    /// A Reference to a rectangle, in the shape Excel writes it: one spanning every column is
    /// whole rows (<c>A1:XFD3</c> is <c>1:3</c>, <c>A:XFD</c> is <c>$1:$1048576</c>), one
    /// spanning every row is whole columns (<c>A1:A1048576</c> is <c>A:A</c>), and any other is
    /// an area — observed in Excel for <c>A:XFD</c> and <c>A1:A1048576</c>
    /// (verification/2026-09-27-windows-excel). The axis a whole form drops is held absolute, as
    /// Excel holds it.
    /// </summary>
    public static Reference Rectangle(string? sheet, int row1, int column1, int row2, int column2, bool row1Absolute, bool column1Absolute, bool row2Absolute, bool column2Absolute)
    {
        if (column1 == 0 && column2 == Sheet.ColumnCount - 1)
        {
            return new Reference(sheet, ReferenceShape.Rows, row1, 0, row2, column2, row1Absolute, true, row2Absolute, true);
        }
        if (row1 == 0 && row2 == Sheet.RowCount - 1)
        {
            return new Reference(sheet, ReferenceShape.Columns, 0, column1, row2, column2, true, column1Absolute, true, column2Absolute);
        }
        return new Reference(sheet, ReferenceShape.Area, row1, column1, row2, column2, row1Absolute, column1Absolute, row2Absolute, column2Absolute);
    }

    /// <summary>Writes the Reference as Excel writes it: a rectangle in the shape <see cref="Rectangle"/> gives it.</summary>
    public void WriteTo(StringBuilder text)
    {
        if (Shape != ReferenceShape.Cell)
        {
            var shaped = Rectangle(SheetName, Row1, Column1, Row2, Column2, Row1Absolute, Column1Absolute, Row2Absolute, Column2Absolute);
            if (shaped.Shape != Shape)
            {
                shaped.WriteTo(text);
                return;
            }
        }
        if (SheetName is not null)
        {
            text.Append(QuoteSheetName(SheetName)).Append('!');
        }
        switch (Shape)
        {
            case ReferenceShape.Cell:
                WriteColumn(text, Column1, Column1Absolute);
                WriteRow(text, Row1, Row1Absolute);
                if (Spilled) text.Append('#');
                break;
            case ReferenceShape.Area:
                WriteColumn(text, Column1, Column1Absolute);
                WriteRow(text, Row1, Row1Absolute);
                text.Append(':');
                WriteColumn(text, Column2, Column2Absolute);
                WriteRow(text, Row2, Row2Absolute);
                break;
            case ReferenceShape.Columns:
                WriteColumn(text, Column1, Column1Absolute);
                text.Append(':');
                WriteColumn(text, Column2, Column2Absolute);
                break;
            case ReferenceShape.Rows:
                WriteRow(text, Row1, Row1Absolute);
                text.Append(':');
                WriteRow(text, Row2, Row2Absolute);
                break;
        }
    }

    private static void WriteColumn(StringBuilder text, int column, bool absolute)
    {
        if (absolute) text.Append('$');
        text.Append(CellAddress.ColumnName(column));
    }

    private static void WriteRow(StringBuilder text, int row, bool absolute)
    {
        if (absolute) text.Append('$');
        text.Append(row + 1);
    }

    /// <summary>A Sheet name is written bare when it is a plain identifier, and quoted otherwise, doubling any <c>'</c>.</summary>
    internal static string QuoteSheetName(string name)
    {
        var bare = name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_');
        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_' || c == '.')) bare = false;
        }
        // A bare name that reads as a cell address (A1!B2), an R1C1 address or a boolean would be misread.
        if (bare && (CellAddress.TryParse(name, out _) || RowColumn(name) || name.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || name.Equals("FALSE", StringComparison.OrdinalIgnoreCase)))
        {
            bare = false;
        }
        return bare ? name : "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'";

        // R, C, R1, C1, R1C1, RC, ...: R1C1 notation.
        static bool RowColumn(string name)
        {
            var i = 0;
            if (i < name.Length && name[i] is 'R' or 'r')
            {
                i++;
                while (i < name.Length && char.IsAsciiDigit(name[i])) i++;
            }
            if (i < name.Length && name[i] is 'C' or 'c')
            {
                i++;
                while (i < name.Length && char.IsAsciiDigit(name[i])) i++;
            }
            return i > 0 && i == name.Length;
        }
    }
}

/// <summary>An inclusive rectangle of cells, zero-based.</summary>
internal readonly record struct Area(int Row1, int Column1, int Row2, int Column2)
{
    public bool IsSingleCell => Row1 == Row2 && Column1 == Column2;

    public long CellCount => (long)(Row2 - Row1 + 1) * (Column2 - Column1 + 1);

    public int Rows => Row2 - Row1 + 1;

    public int Columns => Column2 - Column1 + 1;

    public bool Contains(CellAddress address) =>
        address.Row >= Row1 && address.Row <= Row2 && address.Column >= Column1 && address.Column <= Column2;
}
