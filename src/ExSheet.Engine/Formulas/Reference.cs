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
    /// <summary>The rectangle the Reference covers.</summary>
    public Area Area => Shape switch
    {
        ReferenceShape.Columns => new Area(0, Column1, Sheet.RowCount - 1, Column2),
        ReferenceShape.Rows => new Area(Row1, 0, Row2, Sheet.ColumnCount - 1),
        _ => new Area(Row1, Column1, Row2, Column2),
    };

    public void WriteTo(StringBuilder text)
    {
        if (SheetName is not null)
        {
            text.Append(QuoteSheetName(SheetName)).Append('!');
        }
        switch (Shape)
        {
            case ReferenceShape.Cell:
                WriteColumn(text, Column1, Column1Absolute);
                WriteRow(text, Row1, Row1Absolute);
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
