namespace ExSheet.Engine.Formulas;

/// <summary>INDEX (its array form), XMATCH, CHOOSE, ROW and COLUMN, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// INDEX: the cell at <c>row_num</c> and <c>column_num</c> of the range, both counted from 1
    /// and truncated to whole numbers. Over a range of one row given <c>row_num</c> alone, that
    /// number runs along the row. Below 0 is <c>#VALUE!</c>, and past the range's edge is
    /// <c>#REF!</c>. The result is the cell itself, so a blank one reads as blank.
    /// </summary>
    /// <remarks>
    /// A row or a column of 0 names the whole column or row, which spills where it is more than one
    /// cell (ADR-0125), and is a range a function such as <c>SUM</c> reads.
    /// </remarks>
    private static Operand Index(FunctionCall call)
    {
        var source = call.Operand(0);
        if (source.IsError) return source;
        var (rows, columns) = source.Kind switch
        {
            OperandKind.Area => (source.Area.Rows, source.Area.Columns),
            OperandKind.Column => (source.Column!.Count, 1),
            OperandKind.Array => (source.Array!.Rows, source.Array.Columns),
            _ => (1, 1),
        };

        var row = 0.0;
        if (call.Has(1) && !TryNumber(call, 1, out row, out var failure)) return failure;
        var column = 0.0;
        if (call.Count < 3)
        {
            if (rows == 1 && columns > 1) (row, column) = (1, row);
        }
        else if (call.Has(2) && !TryNumber(call, 2, out column, out failure))
        {
            return failure;
        }
        row = Math.Truncate(row);
        column = Math.Truncate(column);
        if (row < 0 || column < 0) return Operand.Of(ErrorValue.Value);
        if (row > rows || column > columns) return Operand.Of(ErrorValue.Ref);
        if ((row == 0 && rows > 1) || (column == 0 && columns > 1))
        {
            // The whole row, column or range: rows r1..r2 and columns c1..c2, from 0.
            var (r1, r2) = row == 0 ? (0, rows - 1) : ((int)row - 1, (int)row - 1);
            var (c1, c2) = column == 0 ? (0, columns - 1) : ((int)column - 1, (int)column - 1);
            switch (source.Kind)
            {
                case OperandKind.Area:
                    var area = source.Area;
                    return Operand.Of(new Area(area.Row1 + r1, area.Column1 + c1, area.Row1 + r2, area.Column1 + c2));
                case OperandKind.Column:
                    return Operand.Of(Slice(call.Evaluator.ToArray(source), r1, r2, c1, c2));
                case OperandKind.Array:
                    return Operand.Of(Slice(source.Array!, r1, r2, c1, c2));
            }
        }

        var r = Math.Max((int)row, 1) - 1;
        var c = Math.Max((int)column, 1) - 1;
        return source.Kind switch
        {
            OperandKind.Area => Operand.Of(new Area(source.Area.Row1 + r, source.Area.Column1 + c, source.Area.Row1 + r, source.Area.Column1 + c)),
            OperandKind.Column => source.Column![r] is { } value ? Operand.Of(value) : Operand.Blank,
            OperandKind.Array => source.Array![r, c] is { } item ? Operand.Of(item) : Operand.Blank,
            _ => source,
        };
    }

    /// <summary>Rows <paramref name="r1"/>..<paramref name="r2"/> and columns <paramref name="c1"/>..<paramref name="c2"/> of an array, from 0.</summary>
    private static ValueArray Slice(ValueArray array, int r1, int r2, int c1, int c2)
    {
        var slice = new ValueArray(r2 - r1 + 1, c2 - c1 + 1);
        for (var r = r1; r <= r2; r++)
        {
            for (var c = c1; c <= c2; c++) slice[r - r1, c - c1] = array[r, c];
        }
        return slice;
    }

    /// <summary>
    /// XMATCH: the position, from 1, at which XLOOKUP's search finds the lookup value, its modes
    /// and its refusals XLOOKUP's (<see cref="Search"/>); <c>#N/A</c> when nothing matches.
    /// </summary>
    private static Operand XMatch(FunctionCall call)
    {
        var lookupOperand = call.Operand(0);
        if (IsArray(lookupOperand)) return Operand.Of(ErrorValue.Value);
        if (call.Evaluator.ScalarOf(lookupOperand) is { IsError: true } lookupError) return Operand.Of(lookupError);
        var lookupArray = call.Operand(1);
        if (!lookupArray.IsRange) return lookupArray.IsError ? lookupArray : Operand.Of(ErrorValue.Value);
        if (Vector.Of(lookupArray) is not { } vector) return Operand.Of(ErrorValue.Value);
        if (Search(call, vector, modesAt: 2) is not { } outcome) return Operand.Of(ErrorValue.Value);
        if (outcome.Failure is { } failure) return failure;
        return outcome.Found is { } index ? Operand.Of(Value.FromNumber(index + 1)) : Operand.Of(ErrorValue.NA);
    }

    /// <summary>
    /// CHOOSE: the value at <c>index_num</c>, from 1, truncated; below 1 or past the values,
    /// <c>#VALUE!</c>. Only the chosen value is evaluated, and a Reference stays a Reference.
    /// </summary>
    private static Operand Choose(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var index, out var failure)) return failure;
        index = Math.Truncate(index);
        if (index < 1 || index > call.Count - 1) return Operand.Of(ErrorValue.Value);
        var at = (int)index;
        return call.Has(at) ? call.Operand(at) : Operand.Blank;
    }

    private static Operand Row(FunctionCall call) => Place(call, row: true);

    private static Operand Column(FunctionCall call) => Place(call, row: false);

    /// <summary>
    /// ROW and COLUMN: the number, from 1, of the Reference's row or column, or of the Formula's own
    /// cell when it is left out (a move recalculates it: <c>Sheet.ReadsOwnPlace</c>). A Reference of
    /// several rows to ROW gives each row's number, down a column, and of several columns to COLUMN
    /// each column's, along a row; it spills (ADR-0125). A Linked Table's column has no place on the
    /// Sheet: <c>#VALUE!</c>.
    /// </summary>
    private static Operand Place(FunctionCall call, bool row)
    {
        if (!call.Has(0))
        {
            if (call.Evaluator.Self is not { } self) return Operand.Of(ErrorValue.Value);
            return Operand.Of(Value.FromNumber((row ? self.Row : self.Column) + 1));
        }
        var reference = call.Operand(0);
        if (reference.Kind != OperandKind.Area) return reference.IsError ? reference : Operand.Of(ErrorValue.Value);
        var area = reference.Area;
        var count = row ? area.Rows : area.Columns;
        var places = row ? new ValueArray(count, 1) : new ValueArray(1, count);
        for (var k = 0; k < count; k++)
        {
            var place = Value.FromNumber((row ? area.Row1 : area.Column1) + k + 1);
            if (row) places[k, 0] = place;
            else places[0, k] = place;
        }
        return Operand.Of(places);
    }

    private static Operand Rows(FunctionCall call) => Extent(call, rows: true);

    private static Operand Columns(FunctionCall call) => Extent(call, rows: false);

    /// <summary>ROWS and COLUMNS: how many rows or columns the range spans; a Linked Table's column is one column; a single Value, 1.</summary>
    private static Operand Extent(FunctionCall call, bool rows)
    {
        var operand = call.Operand(0);
        if (operand.IsError) return operand;
        var count = operand.Kind switch
        {
            OperandKind.Area => rows ? operand.Area.Rows : operand.Area.Columns,
            OperandKind.Column => rows ? operand.Column!.Count : 1,
            OperandKind.Array => rows ? operand.Array!.Rows : operand.Array!.Columns,
            _ => 1,
        };
        return Operand.Of(Value.FromNumber(count));
    }

    /// <summary>
    /// OFFSET (ADR-0124, a volatile function): the Reference <c>rows</c> below and <c>cols</c> right
    /// of <c>reference</c>, <c>height</c> rows by <c>width</c> columns, each truncated and the size the
    /// reference's own when left out. A size below 1, or a Reference past the Sheet's edge, is
    /// <c>#REF!</c>; a Linked Table's column has no place to move from: <c>#VALUE!</c>. A Reference of
    /// several cells is a range for a function such as <c>SUM</c>, and spills where it is the
    /// result (ADR-0125).
    /// </summary>
    private static Operand Offset(FunctionCall call)
    {
        var reference = call.Operand(0);
        if (reference.Kind != OperandKind.Area) return reference.IsError ? reference : Operand.Of(ErrorValue.Value);
        var area = reference.Area;
        if (!TryNumber(call, 1, out var rows, out var failure)) return failure;
        if (!TryNumber(call, 2, out var columns, out failure)) return failure;
        double height = area.Rows;
        double width = area.Columns;
        if (call.Has(3) && !TryNumber(call, 3, out height, out failure)) return failure;
        if (call.Has(4) && !TryNumber(call, 4, out width, out failure)) return failure;
        rows = Math.Truncate(rows);
        columns = Math.Truncate(columns);
        height = Math.Truncate(height);
        width = Math.Truncate(width);
        if (height < 1 || width < 1) return Operand.Of(ErrorValue.Ref);
        var top = area.Row1 + rows;
        var left = area.Column1 + columns;
        var bottom = top + height - 1;
        var right = left + width - 1;
        if (top < 0 || left < 0 || bottom >= Sheet.RowCount || right >= Sheet.ColumnCount) return Operand.Of(ErrorValue.Ref);
        return Operand.Of(new Area((int)top, (int)left, (int)bottom, (int)right));
    }
}
