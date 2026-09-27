namespace ExSheet.Engine.Formulas;

/// <summary>
/// Rewrites a Formula's References through a map: the new Reference, or <see langword="null"/>
/// where the cells it named no longer exist, which Excel writes as <c>#REF!</c> in the stored
/// Formula (ADR-0047). Only the Reference tokens change; the whitespace and every other token are
/// kept as they were, but for the whitespace before a Reference that becomes <c>#REF!</c>
/// (<see cref="FormulaText.RewriteReferences"/>).
/// </summary>
internal static class ReferenceRewriter
{
    /// <summary>The Entry with its References mapped; the same instance when nothing changed.</summary>
    public static Entry Rewrite(Entry entry, Func<Reference, Reference?> map, Func<string, string>? requalify = null)
    {
        if (entry.Formula is not { } formula) return entry;
        var rewritten = FormulaText.RewriteReferences(formula, map, requalify);
        return ReferenceEquals(rewritten, formula) ? entry : Entry.FromFormula(rewritten);
    }
}

/// <summary>Which of a Sheet's two axes an insertion or deletion runs along.</summary>
internal enum SheetAxis
{
    Rows,
    Columns,
}

/// <summary>
/// One insertion or deletion of whole rows or columns (ADR-0046): rows and columns are places, so
/// what lies beyond the edit moves, and every Reference is rewritten to keep naming the same cells.
/// </summary>
internal readonly record struct StructuralEdit(SheetAxis Axis, int Start, int Count, bool IsInsert)
{
    public int Size => Axis == SheetAxis.Rows ? Sheet.RowCount : Sheet.ColumnCount;

    private int Max => Size - 1;

    private int End => Start + Count - 1;

    public StructuralEdit Inverse => this with { IsInsert = !IsInsert };

    public string Describe() =>
        $"{(IsInsert ? "inserting" : "deleting")} {Count} {(Axis == SheetAxis.Rows ? "row" : "column")}{(Count == 1 ? "" : "s")} at {(Axis == SheetAxis.Rows ? (Start + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : CellAddress.ColumnName(Start))}";

    public void Validate()
    {
        if (Start < 0 || Start >= Size) throw new ArgumentOutOfRangeException(nameof(Start), Start, $"A {(Axis == SheetAxis.Rows ? "row" : "column")} is 0 to {Max}.");
        if (Count < 1 || Count > Size - Start) throw new ArgumentOutOfRangeException(nameof(Count), Count, $"The count is 1 to {Size - Start} here.");
    }

    /// <summary>Where a cell goes: its new address, or <see langword="null"/> when it is deleted or pushed off the edge.</summary>
    public CellAddress? Move(CellAddress address)
    {
        var p = Axis == SheetAxis.Rows ? address.Row : address.Column;
        int moved;
        if (IsInsert)
        {
            if (p < Start) return address;
            moved = p + Count;
            if (moved > Max) return null;
        }
        else
        {
            if (p < Start) return address;
            if (p <= End) return null;
            moved = p - Count;
        }
        return Axis == SheetAxis.Rows ? new CellAddress(moved, address.Column) : new CellAddress(address.Row, moved);
    }

    /// <summary>
    /// A Reference rewritten as Excel rewrites it: an insertion at or before a range's first cell
    /// moves it, one inside it grows it; a deletion shrinks it, moves it, or — when it takes every
    /// cell it named — makes it <c>#REF!</c> (<see langword="null"/>). A Reference that spans the
    /// whole axis (<c>A:A</c> for rows) is untouched, as is one qualified with another Sheet's name
    /// (ADR-0046); one qualified with <paramref name="sheet"/>'s own name is rewritten like any other.
    /// <paramref name="leavesSheet"/> is set when an insertion would push the cells it names off
    /// the edge; the caller refuses the insertion.
    /// </summary>
    public Reference? Map(Reference reference, Sheet sheet, out bool leavesSheet)
    {
        leavesSheet = false;
        if (!sheet.IsLocal(reference)) return reference;
        var rows = Axis == SheetAxis.Rows;
        if (rows && reference.Shape == ReferenceShape.Columns) return reference;
        if (!rows && reference.Shape == ReferenceShape.Rows) return reference;
        var p1 = rows ? reference.Row1 : reference.Column1;
        var p2 = rows ? reference.Row2 : reference.Column2;
        // A:A (A1:A1048576 is written so) spans the whole axis; an edit along it leaves it alone.
        if (reference.Shape != ReferenceShape.Cell && p1 == 0 && p2 == Max) return reference;

        int n1, n2;
        if (IsInsert)
        {
            if (p1 >= Start)
            {
                n1 = p1 + Count;
                n2 = p2 + Count;
            }
            else if (p2 >= Start)
            {
                n1 = p1;
                n2 = p2 + Count;
            }
            else
            {
                return reference;
            }
            if (n2 > Max)
            {
                leavesSheet = true;
                return reference;
            }
        }
        else
        {
            if (p2 < Start) return reference;
            if (p1 >= Start && p2 <= End) return null;
            n1 = p1 < Start ? p1 : p1 > End ? p1 - Count : Start;
            n2 = p2 > End ? p2 - Count : Start - 1;
        }
        return rows ? reference with { Row1 = n1, Row2 = n2 } : reference with { Column1 = n1, Column2 = n2 };
    }

    /// <summary>Whether a Reference covers any place at or beyond the edit along its axis.</summary>
    public bool Reaches(Reference reference, Sheet sheet) =>
        sheet.IsLocal(reference) && (Axis == SheetAxis.Rows ? reference.Area.Row2 : reference.Area.Column2) >= Start;
}
