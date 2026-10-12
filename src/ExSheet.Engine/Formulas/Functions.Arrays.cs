namespace ExSheet.Engine.Formulas;

/// <summary>
/// The functions built for arrays (ADR-0125): FILTER, UNIQUE, SORT, SORTBY, SEQUENCE, TRANSPOSE,
/// which spill, and SUMPRODUCT, which reduces its arrays to one Value. Each to Microsoft's
/// documentation of it; where the documentation leaves a case open, the case is refused with an
/// Error Value until Excel is asked.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// An argument read whole as an array, or its Error Value when it is one; <see langword="false"/>
    /// with <paramref name="failure"/> then. A kept blank (<see cref="Operand.KeptBlank"/>) refuses the
    /// Formula (<see cref="FunctionCall.Operand"/>), unless <paramref name="keepingBlanks"/> says this
    /// function gives the same answer for every reading of it.
    /// </summary>
    private static bool TryArray(FunctionCall call, int index, out ValueArray array, out Operand failure, bool keepingBlanks = false)
    {
        var operand = keepingBlanks ? call.KeepingBlanks(index) : call.Operand(index);
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
    /// A blank kept from the array is marked as <c>SORT</c>'s is (<see cref="Made"/>).
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
        return Made(result);
    }

    /// <summary>
    /// UNIQUE: the distinct rows of <c>array</c> (its columns when <c>by_col</c> is TRUE), in the order
    /// they first appear; with <c>exactly_once</c> TRUE, only those that appear once. Text is compared
    /// without regard to case, as Excel's comparison operators compare it; a number and text that
    /// reads as one are different. A blank is one of a kind only with another blank, never with 0 or
    /// empty text, and spills as 0, as Excel's does (UNIQUE-010 to 015); read inside the Formula, it is
    /// a kept blank (<see cref="Made"/>). Nothing left is <c>#CALC!</c>.
    /// </summary>
    private static Operand Unique(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        if (!TryFlag(call, 1, out var byColumn, out failure)) return failure;
        if (!TryFlag(call, 2, out var exactlyOnce, out failure)) return failure;

        var count = byColumn ? array.Columns : array.Rows;
        var width = byColumn ? array.Rows : array.Columns;
        Value? Item(int line, int at) => byColumn ? array[at, line] : array[line, at];
        bool Same(int a, int b)
        {
            for (var k = 0; k < width; k++)
            {
                if (!SameItem(Item(a, k), Item(b, k))) return false;
            }
            return true;
        }
        static bool SameItem(Value? x, Value? y) => (x, y) switch
        {
            (null, null) => true,
            ({ } a, { } b) => a.Kind == b.Kind && (a.IsError ? a.Error == b.Error : Evaluator.Compare(a, b) == 0),
            _ => false,
        };

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
        return Made(result);
    }

    /// <summary>
    /// SORT: <c>array</c>'s rows ordered by the column <c>sort_index</c> (from 1, the first when left
    /// out), ascending for <c>sort_order</c> 1 and descending for −1; its columns, by a row, when
    /// <c>by_col</c> is TRUE. Equal keys keep their order. Keys order as the comparison operators order
    /// them: numbers, then text without regard to case, then booleans. A blank key sorts after numbers
    /// and text in either order and after a boolean ascending, as Excel's did (SORT-011 to 014, 016 to
    /// 020, 024, 025), and after a boolean descending, by generalisation (SORT-040 asks); blank keys
    /// keep their order, as Excel's did in a descending sort (SORT-023), and ascending as equal keys
    /// do, by generalisation (SORT-041 asks); beside an Error Value key the Formula is refused
    /// (SORT-031 asks). A blank spills as 0, as Excel's did (SORT-012, 017, 019, 020, 025); a blank in
    /// the result is a kept blank (<see cref="Made"/>). An Error Value key is answered only where it
    /// already follows every number, as Excel's <c>#N/A</c> followed the 7 of SORT-015, and the Formula
    /// is refused with <c>#VALUE!</c> elsewhere (<see cref="ErrorKeysSettled"/>). An index outside the
    /// array, or an order other than 1 and −1, is <c>#VALUE!</c>, as Excel's is (SORT-007, 008).
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
        if (!TryOrderLines([(keys, descending)], lines, errorKeysOrdered: true, out var order)) return call.Evaluator.Refuse();
        return Made(Reorder(array, order, byColumn));
    }

    /// <summary>
    /// SORTBY: <c>array</c> ordered by one or more <c>by_array</c>s, each with its <c>sort_order</c>
    /// (1 when left out), the first deciding first. Each <c>by_array</c> is one column as tall as the
    /// array, which sorts its rows, or one row as wide, which sorts its columns, and all of them run
    /// the same way; any other shape is <c>#VALUE!</c>. Keys order as in <see cref="Sort"/>, a blank
    /// last: Excel's was after a number in an ascending key (SORTBY-007 to 009), and the rest, a
    /// descending key among it, is <c>SORT</c>'s order by generalisation (SORTBY-014 asks a descending
    /// one); a blank in the result is a kept blank, as <c>SORT</c>'s is
    /// (<see cref="Made"/>). An Error Value among the keys refuses the Formula with
    /// <c>#VALUE!</c>: SORT-015 read <c>SORT</c>'s, and a <c>by_array</c>'s could as well be the result,
    /// as <c>FILTER</c>'s <c>include</c>'s is (SORTBY-010 asks).
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
        if (!TryOrderLines(keys, lines, errorKeysOrdered: false, out var order)) return call.Evaluator.Refuse();
        return Made(Reorder(array, order, byColumn == true));
    }

    /// <summary>
    /// The array <c>UNIQUE</c>, <c>SORT</c>, <c>SORTBY</c>, <c>FILTER</c> or <c>TRANSPOSE</c> made
    /// (ADR-0125), a blank it kept from its array marked as a kept blank (<see cref="Operand.KeptBlank"/>).
    /// What the area passes read of Excel's, for <c>SORT</c> and <c>UNIQUE</c>: it spills as 0
    /// (SORT-012, UNIQUE-010, and <c>TRANSPOSE</c>'s too, TRANSPOSE-004), <c>COUNTA</c> counts it in the
    /// array (SORT-021, UNIQUE-017), and taken out by <c>INDEX</c>, <c>SORT</c>'s is blank to
    /// <c>ISBLANK</c> (SORT-022), the empty value a blank cell holds, and <c>UNIQUE</c>'s equal to empty
    /// text (UNIQUE-018), not the 0 it spills as. The engine takes <c>UNIQUE</c>'s for a blank cell's
    /// value too, which UNIQUE-020 asks. <c>SORTBY</c>, <c>FILTER</c> and <c>TRANSPOSE</c> keep a blank
    /// as <c>SORT</c> does, so theirs is taken to be the same kept blank: a generalisation SORTBY-011 to
    /// 013, FILTER-012 and 013 and TRANSPOSE-005 and 006 ask, not a reading. Before the mark, the engine's
    /// <c>=COUNTA(FILTER(...))</c> passed over a blank that its <c>=COUNTA(SORT(FILTER(...)))</c>
    /// counted, though a reorder cannot change a count. A function reads a kept blank only where the
    /// passes read it, where every reading of it gives the same answer, or where what the passes read is
    /// carried to it by generalisation, which no run has read: <c>COUNTA</c> over <c>SORTBY</c>'s,
    /// <c>FILTER</c>'s and <c>TRANSPOSE</c>'s array, as over <c>SORT</c>'s; <c>INDEX</c> taking it out
    /// of those three and of <c>UNIQUE</c>'s still blank; and <c>LEN</c>, <c>ISTEXT</c>, <c>MATCH</c>'s
    /// lookup value and every other one Value read through <see cref="FunctionCall.OneValue"/>, reading
    /// the blank <c>INDEX</c> takes out as a blank cell's, as <c>ISBLANK</c> read <c>SORT</c>'s
    /// (<see cref="FunctionCall.KeepingBlanks"/>); elsewhere, <c>XLOOKUP</c>'s lookup value and
    /// <c>ROUND</c> among them, the Formula is refused (<see cref="FunctionCall.Operand"/>).
    /// </summary>
    private static Operand Made(ValueArray result) => Operand.Of(result).WithKeptBlank();

    /// <summary>
    /// The order of <paramref name="lines"/> lines by their keys, stable. A blank key comes after
    /// numbers, text and booleans whichever the order: Excel's <c>SORT</c> put it after numbers and text
    /// in either order and after a boolean ascending (SORT-011 to 014, 016 to 019, 024, 025), neither
    /// as 0 nor as empty text, which would come first (SORT-012, 014), and after a boolean descending is
    /// the engine's by generalisation (SORT-040 asks). An Error Value key is ordered only where
    /// <paramref name="errorKeysOrdered"/> allows it and every reading SORT-015 leaves gives the one order
    /// (<see cref="ErrorKeysSettled"/>), which a blank beside it never does (SORT-031 asks);
    /// <see langword="false"/> anywhere else a key is an Error Value.
    /// </summary>
    private static bool TryOrderLines(List<(List<Value?> Keys, bool Descending)> keys, int lines, bool errorKeysOrdered, out int[] order)
    {
        order = [.. Enumerable.Range(0, lines)];
        foreach (var (values, descending) in keys)
        {
            if (values.Any(v => v is { IsError: true }) && !(errorKeysOrdered && ErrorKeysSettled(values, descending))) return false;
        }
        order = [.. order.Order(Comparer<int>.Create((a, b) =>
        {
            foreach (var (values, descending) in keys)
            {
                var compared = (values[a], values[b]) switch
                {
                    (null, null) => 0,
                    (null, _) => 1,
                    (_, null) => -1,
                    ({ IsError: true }, { IsError: true }) => 0,
                    ({ IsError: true }, _) => 1,
                    (_, { IsError: true }) => -1,
                    var (x, y) => descending ? -Evaluator.Compare(x, y) : Evaluator.Compare(x, y),
                };
                if (compared != 0) return compared;
            }
            return a.CompareTo(b);
        }))];
        return true;
    }

    /// <summary>
    /// Whether a key of Error Values is ordered as every reading SORT-015 leaves orders it. Over 7 and
    /// then <c>#N/A</c>, Excel's ascending sort showed 7 first: the Error Value is not the result, and
    /// does not sort before the numbers. It had followed the 7 already, so that answer fits an Error
    /// Value that sorts after the numbers and one that keeps its place alike (SORT-026 asks the two
    /// apart). Whether it sorts before or after text, booleans and blanks, whether a descending sort
    /// puts it first or still last, and how two different Error Values order, no run has read either
    /// (SORT-028 to 032 ask). So a key is answered where every one of those readings gives the same
    /// order: its Error Values are all one, and beside them it holds nothing, or, sorted ascending,
    /// numbers alone, none of them after an Error Value.
    /// </summary>
    private static bool ErrorKeysSettled(List<Value?> values, bool descending)
    {
        if (values.Where(v => v is { IsError: true }).Select(v => v!.Value.Error).Distinct().Count() > 1) return false;
        if (values.All(v => v is { IsError: true })) return true;
        if (descending || !values.All(v => v is { IsError: true } or { Kind: ValueKind.Number })) return false;
        return values.FindLastIndex(v => v is { Kind: ValueKind.Number }) < values.FindIndex(v => v is { IsError: true });
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

    /// <summary>
    /// TRANSPOSE: the array's rows as columns. A blank stays blank, and spills as 0, as Excel's does
    /// (TRANSPOSE-004); read inside the Formula it is marked as <c>SORT</c>'s is (<see cref="Made"/>).
    /// </summary>
    private static Operand Transpose(FunctionCall call)
    {
        if (!TryArray(call, 0, out var array, out var failure)) return failure;
        var result = new ValueArray(array.Columns, array.Rows);
        for (var r = 0; r < array.Rows; r++)
        {
            for (var c = 0; c < array.Columns; c++) result[c, r] = array[r, c];
        }
        return Made(result);
    }

    /// <summary>
    /// SUMPRODUCT: the sum of the products of its arrays' corresponding Values, the arrays all of
    /// one size, or <c>#VALUE!</c>. Inside an array anything but a number counts as 0, as Microsoft
    /// documents, so a kept blank is 0 whichever way it is read (<see cref="Made"/>), an answer that
    /// agrees with every reading rather than one a run read; an Error Value is the result. An operator
    /// on ranges is evaluated as an array first (ADR-0125), so <c>SUMPRODUCT((A1:A3="x")*B1:B3)</c>
    /// counts what it is meant to.
    /// </summary>
    private static Operand SumProduct(FunctionCall call)
    {
        var arrays = new List<ValueArray>();
        for (var i = 0; i < call.Count; i++)
        {
            if (!call.Has(i)) return Operand.Of(ErrorValue.Value);
            if (!TryArray(call, i, out var array, out var failure, keepingBlanks: true)) return failure;
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
