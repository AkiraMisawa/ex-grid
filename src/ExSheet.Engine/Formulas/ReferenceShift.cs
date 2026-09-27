namespace ExSheet.Engine.Formulas;

/// <summary>
/// What copying a Formula does to its References (ADR-0048): each relative part moves by the
/// distance copied, each absolute part (<c>$</c>) stays. A Reference moved off the Sheet names no
/// cells, and Excel writes it <c>#REF!</c>.
/// </summary>
internal static class ReferenceShift
{
    public static Reference? Shift(Reference reference, int rows, int columns)
    {
        var r1 = reference.Row1Absolute ? reference.Row1 : reference.Row1 + rows;
        var r2 = reference.Row2Absolute ? reference.Row2 : reference.Row2 + rows;
        var c1 = reference.Column1Absolute ? reference.Column1 : reference.Column1 + columns;
        var c2 = reference.Column2Absolute ? reference.Column2 : reference.Column2 + columns;
        // A whole column spans every row whatever is copied, and a whole row every column.
        if (reference.Shape == ReferenceShape.Columns) (r1, r2) = (reference.Row1, reference.Row2);
        if (reference.Shape == ReferenceShape.Rows) (c1, c2) = (reference.Column1, reference.Column2);
        if (!Inside(r1, Sheet.RowCount) || !Inside(r2, Sheet.RowCount) || !Inside(c1, Sheet.ColumnCount) || !Inside(c2, Sheet.ColumnCount)) return null;

        var r1Absolute = reference.Row1Absolute;
        var r2Absolute = reference.Row2Absolute;
        var c1Absolute = reference.Column1Absolute;
        var c2Absolute = reference.Column2Absolute;
        // A$1:A3 copied down past row 1 is written from its top: A$1 stays first only while it is on top.
        if (r2 < r1) (r1, r1Absolute, r2, r2Absolute) = (r2, r2Absolute, r1, r1Absolute);
        if (c2 < c1) (c1, c1Absolute, c2, c2Absolute) = (c2, c2Absolute, c1, c1Absolute);
        return reference with
        {
            Row1 = r1,
            Row2 = r2,
            Column1 = c1,
            Column2 = c2,
            Row1Absolute = r1Absolute,
            Row2Absolute = r2Absolute,
            Column1Absolute = c1Absolute,
            Column2Absolute = c2Absolute,
        };

        static bool Inside(int position, int size) => position >= 0 && position < size;
    }

    /// <summary>The Entry as it is written <paramref name="rows"/> down and <paramref name="columns"/> across from where it was.</summary>
    public static Entry Shift(Entry entry, int rows, int columns) =>
        rows == 0 && columns == 0 ? entry : ReferenceRewriter.Rewrite(entry, r => Shift(r, rows, columns));
}
