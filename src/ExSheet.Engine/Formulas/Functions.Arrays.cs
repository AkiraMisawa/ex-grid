namespace ExSheet.Engine.Formulas;

/// <summary>
/// The functions built for arrays (ADR-0125): FILTER, UNIQUE, SORT, SORTBY, SEQUENCE, TRANSPOSE,
/// which spill, and SUMPRODUCT, which reduces its arrays to one Value. Each to Microsoft's
/// documentation of it; where the documentation leaves a case open, the case is refused with an
/// Error Value until Excel is asked.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>An argument read whole as an array, or its Error Value when it is one; <see langword="false"/> with <paramref name="failure"/> then.</summary>
    private static bool TryArray(FunctionCall call, int index, out ValueArray array, out Operand failure)
    {
        var operand = call.Operand(index);
        failure = default;
        array = null!;
        if (operand.IsError)
        {
            failure = operand;
            return false;
        }
        array = call.Evaluator.ToArray(operand);
        if (array.IsSingle && array[0, 0] is { IsError: true } error && !operand.IsRange)
        {
            failure = Operand.Of(error);
            return false;
        }
        return true;
    }

    /// <summary>
    /// FILTER: the rows (or columns) of <c>array</c> whose <c>include</c> is TRUE. <c>include</c> is
    /// one column as tall as the array, or one row as wide; any other shape is <c>#VALUE!</c>. A
    /// number counts as TRUE when it is not 0, a blank as FALSE, text is <c>#VALUE!</c>, and an
    /// Error Value is the result. Nothing included is <c>if_empty</c>, or <c>#CALC!</c> without it.
    /// </summary>
    private static Operand Filter(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        if (!TryArray(call, 1, out var include, out failure)) return failure;
        bool byRows;
        if (include.Columns == 1 && include.Rows == array.Rows) byRows = true;
        else if (include.Rows == 1 && include.Columns == array.Columns) byRows = false;
        else return Operand.Of(ErrorValue.Value);

        var count = byRows ? array.Rows : array.Columns;
        var kept = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var flag = byRows ? include[i, 0] : include[0, i];
            switch (flag)
            {
                case null:
                    continue;
                case { IsError: true }:
                    return Operand.Of(flag.Value);
                case { Kind: ValueKind.Boolean } b:
                    if (b.Boolean) kept.Add(i);
                    continue;
                case { Kind: ValueKind.Number } n:
                    if (n.Number != 0) kept.Add(i);
                    continue;
                default:
                    return Operand.Of(ErrorValue.Value);
            }
        }
        if (kept.Count == 0) return call.Has(2) ? call.Operand(2) : Operand.Of(ErrorValue.Calc);

        var result = byRows ? new ValueArray(kept.Count, array.Columns) : new ValueArray(array.Rows, kept.Count);
        for (var k = 0; k < kept.Count; k++)
        {
            if (byRows)
            {
                for (var c = 0; c < array.Columns; c++) result[k, c] = array[kept[k], c];
            }
            else
            {
                for (var r = 0; r < array.Rows; r++) result[r, k] = array[r, kept[k]];
            }
        }
        return Operand.Of(result);
    }

    /// <summary>
    /// UNIQUE: the distinct rows of <c>array</c> (its columns when <c>by_col</c> is TRUE), in the order
    /// they first appear; with <c>exactly_once</c> TRUE, only those that appear once. Text is compared
    /// without regard to case, as Excel's comparison operators compare it; a number and text that
    /// reads as one are different. Nothing left is <c>#CALC!</c>. A blank is refused with
    /// <c>#VALUE!</c> until Excel is asked whether it is one of a kind with 0.
    /// </summary>
    private static Operand Unique(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        if (!TryFlag(call, 1, out var byColumn, out failure)) return failure;
        if (!TryFlag(call, 2, out var exactlyOnce, out failure)) return failure;
        if (array.All().Any(v => v is null)) return Operand.Of(ErrorValue.Value);

        var count = byColumn ? array.Columns : array.Rows;
        var width = byColumn ? array.Rows : array.Columns;
        Value Item(int line, int at) => (byColumn ? array[at, line] : array[line, at])!.Value;
        bool Same(int a, int b)
        {
            for (var k = 0; k < width; k++)
            {
                var x = Item(a, k);
                var y = Item(b, k);
                if (x.Kind != y.Kind) return false;
                if (x.IsError ? x.Error != y.Error : Evaluator.Compare(x, y) != 0) return false;
            }
            return true;
        }

        // Each line's first equal, then how many there are of it.
        var first = new int[count];
        var occurrences = new Dictionary<int, int>();
        for (var i = 0; i < count; i++)
        {
            first[i] = i;
            for (var j = 0; j < i; j++)
            {
                if (first[j] == j && Same(i, j))
                {
                    first[i] = j;
                    break;
                }
            }
            occurrences[first[i]] = occurrences.GetValueOrDefault(first[i]) + 1;
        }
        var kept = Enumerable.Range(0, count).Where(i => first[i] == i && (!exactlyOnce || occurrences[i] == 1)).ToList();
        if (kept.Count == 0) return Operand.Of(ErrorValue.Calc);

        var result = byColumn ? new ValueArray(width, kept.Count) : new ValueArray(kept.Count, width);
        for (var k = 0; k < kept.Count; k++)
        {
            for (var at = 0; at < width; at++)
            {
                if (byColumn) result[at, k] = Item(kept[k], at);
                else result[k, at] = Item(kept[k], at);
            }
        }
        return Operand.Of(result);
    }

    /// <summary>
    /// SORT: <c>array</c>'s rows ordered by the column <c>sort_index</c> (from 1, the first when left
    /// out), ascending for <c>sort_order</c> 1 and descending for −1; its columns, by a row, when
    /// <c>by_col</c> is TRUE. Equal keys keep their order. Keys order as the comparison operators
    /// order them: numbers, then text without regard to case, then booleans. An index outside the
    /// array, or an order other than 1 and −1, is <c>#VALUE!</c>; a blank or an Error Value among
    /// the keys is refused with <c>#VALUE!</c> until Excel is asked where it sorts.
    /// </summary>
    private static Operand Sort(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        var index = 1.0;
        if (call.Has(1) && !TryNumber(call, 1, out index, out failure)) return failure;
        if (!TryOrder(call, 2, out var descending, out failure)) return failure;
        if (!TryFlag(call, 3, out var byColumn, out failure)) return failure;
        index = Math.Truncate(index);
        var lines = byColumn ? array.Columns : array.Rows;
        var across = byColumn ? array.Rows : array.Columns;
        if (index < 1 || index > across) return Operand.Of(ErrorValue.Value);
        var at = (int)index - 1;
        var keys = Enumerable.Range(0, lines).Select(i => byColumn ? array[at, i] : array[i, at]).ToList();
        if (!TryOrderLines([(keys, descending)], lines, out var order)) return Operand.Of(ErrorValue.Value);
        return Operand.Of(Reorder(array, order, byColumn));
    }

    /// <summary>
    /// SORTBY: <c>array</c> ordered by one or more <c>by_array</c>s, each with its <c>sort_order</c>
    /// (1 when left out), the first deciding first. Each <c>by_array</c> is one column as tall as the
    /// array, which sorts its rows, or one row as wide, which sorts its columns, and all of them run
    /// the same way; any other shape is <c>#VALUE!</c>. Keys order as in <see cref="Sort"/>, and are
    /// refused as there.
    /// </summary>
    private static Operand SortBy(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        bool? byColumn = null;
        var keys = new List<(List<Value?> Keys, bool Descending)>();
        for (var i = 1; i < call.Count; i += 2)
        {
            if (!TryArray(call, i, out var by, out failure)) return failure;
            bool columns;
            if (by.Columns == 1 && by.Rows == array.Rows && (by.Rows > 1 || array.Columns == 1)) columns = false;
            else if (by.Rows == 1 && by.Columns == array.Columns && by.Columns > 1) columns = true;
            else return Operand.Of(ErrorValue.Value);
            if (byColumn is { } way && way != columns) return Operand.Of(ErrorValue.Value);
            byColumn = columns;
            if (!TryOrder(call, i + 1, out var descending, out failure)) return failure;
            var length = columns ? by.Columns : by.Rows;
            keys.Add(([.. Enumerable.Range(0, length).Select(k => columns ? by[0, k] : by[k, 0])], descending));
        }
        var lines = byColumn == true ? array.Columns : array.Rows;
        if (!TryOrderLines(keys, lines, out var order)) return Operand.Of(ErrorValue.Value);
        return Operand.Of(Reorder(array, order, byColumn == true));
    }

    /// <summary>The order of <paramref name="lines"/> lines by their keys, stable; <see langword="false"/> when a key is blank or an Error Value.</summary>
    private static bool TryOrderLines(List<(List<Value?> Keys, bool Descending)> keys, int lines, out int[] order)
    {
        order = [.. Enumerable.Range(0, lines)];
        foreach (var (values, _) in keys)
        {
            if (values.Any(v => v is null or { IsError: true })) return false;
        }
        order = [.. order.Order(Comparer<int>.Create((a, b) =>
        {
            foreach (var (values, descending) in keys)
            {
                var compared = Evaluator.Compare(values[a], values[b]);
                if (compared != 0) return descending ? -compared : compared;
            }
            return a.CompareTo(b);
        }))];
        return true;
    }

    private static ValueArray Reorder(ValueArray array, int[] order, bool byColumn)
    {
        var result = new ValueArray(array.Rows, array.Columns);
        for (var k = 0; k < order.Length; k++)
        {
            if (byColumn)
            {
                for (var r = 0; r < array.Rows; r++) result[r, k] = array[r, order[k]];
            }
            else
            {
                for (var c = 0; c < array.Columns; c++) result[k, c] = array[order[k], c];
            }
        }
        return result;
    }

    /// <summary>A sort order argument: 1 or left out is ascending, −1 descending, anything else <c>#VALUE!</c>.</summary>
    private static bool TryOrder(FunctionCall call, int index, out bool descending, out Operand failure)
    {
        descending = false;
        failure = default;
        if (!call.Has(index)) return true;
        if (!TryNumber(call, index, out var order, out failure)) return false;
        if (order is not (1 or -1))
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        descending = order == -1;
        return true;
    }

    /// <summary>A TRUE or FALSE argument, FALSE when left out; a number is TRUE when it is not 0, other text <c>#VALUE!</c>.</summary>
    private static bool TryFlag(FunctionCall call, int index, out bool flag, out Operand failure)
    {
        flag = false;
        failure = default;
        if (!call.Has(index)) return true;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        switch (value)
        {
            case null:
                return true;
            case { Kind: ValueKind.Boolean } b:
                flag = b.Boolean;
                return true;
            case { Kind: ValueKind.Number } n:
                flag = n.Number != 0;
                return true;
            default:
                failure = Operand.Of(ErrorValue.Value);
                return false;
        }
    }

    /// <summary>
    /// SEQUENCE: <c>rows</c> by <c>columns</c> numbers, row by row, from <c>start</c> by <c>step</c>,
    /// each 1 when left out and the sizes truncated. A size of 0 is <c>#CALC!</c>, below 0
    /// <c>#VALUE!</c>; an array larger than the engine can hold is <c>#NUM!</c> (<see cref="ValueArray.MostCells"/>).
    /// </summary>
    private static Operand Sequence(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rows, out var failure)) return failure;
        var columns = 1.0;
        if (call.Has(1) && !TryNumber(call, 1, out columns, out failure)) return failure;
        var start = 1.0;
        if (call.Has(2) && !TryNumber(call, 2, out start, out failure)) return failure;
        var step = 1.0;
        if (call.Has(3) && !TryNumber(call, 3, out step, out failure)) return failure;
        rows = Math.Truncate(rows);
        columns = Math.Truncate(columns);
        if (rows < 0 || columns < 0) return Operand.Of(ErrorValue.Value);
        if (rows == 0 || columns == 0) return Operand.Of(ErrorValue.Calc);
        if (rows * columns > ValueArray.MostCells) return Operand.Of(ErrorValue.Num);
        var result = new ValueArray((int)rows, (int)columns);
        var k = 0;
        for (var r = 0; r < result.Rows; r++)
        {
            for (var c = 0; c < result.Columns; c++) result[r, c] = Value.FromNumber(start + (step * k++));
        }
        return Operand.Of(result);
    }

    /// <summary>TRANSPOSE: the array's rows as columns. A blank stays blank, and spills as the 0 every spilled blank shows.</summary>
    private static Operand Transpose(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        var result = new ValueArray(array.Columns, array.Rows);
        for (var r = 0; r < array.Rows; r++)
        {
            for (var c = 0; c < array.Columns; c++) result[c, r] = array[r, c];
        }
        return Operand.Of(result);
    }

    /// <summary>
    /// SUMPRODUCT: the sum of the products of its arrays' corresponding Values, the arrays all of
    /// one size, or <c>#VALUE!</c>. Inside an array anything but a number counts as 0, as Microsoft
    /// documents; an Error Value is the result. An operator on ranges is evaluated as an array first
    /// (ADR-0125), so <c>SUMPRODUCT((A1:A3="x")*B1:B3)</c> counts what it is meant to.
    /// </summary>
    private static Operand SumProduct(FunctionCall call)
    {
        var arrays = new List<ValueArray>();
        for (var i = 0; i < call.Count; i++)
        {
            if (!call.Has(i)) return Operand.Of(ErrorValue.Value);
            if (!TryArray(call, i, out var array, out var failure)) return failure;
            if (arrays.Count > 0 && (array.Rows != arrays[0].Rows || array.Columns != arrays[0].Columns)) return Operand.Of(ErrorValue.Value);
            arrays.Add(array);
        }
        var sum = 0.0;
        for (var r = 0; r < arrays[0].Rows; r++)
        {
            for (var c = 0; c < arrays[0].Columns; c++)
            {
                var product = 1.0;
                foreach (var array in arrays)
                {
                    var value = array[r, c];
                    if (value is { IsError: true } error) return Operand.Of(error);
                    product *= value is { Kind: ValueKind.Number } number ? number.Number : 0;
                }
                sum += product;
            }
        }
        return Operand.Of(Value.FromNumber(sum));
    }
}
