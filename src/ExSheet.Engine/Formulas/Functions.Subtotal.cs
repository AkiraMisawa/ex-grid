namespace ExSheet.Engine.Formulas;

/// <summary>
/// SUBTOTAL and AGGREGATE (ADR-0047's note of 2026-10-10), admitted while a Sheet cannot hide a
/// row (ADR-0046): the function numbers and options that pass over hidden rows give what the others
/// give, as they do in Excel over such a sheet. A cell is nested when its Formula, or for a cell of a
/// Spill Range its Anchor's, calls SUBTOTAL or AGGREGATE anywhere in its text, as the seventeenth
/// Windows run read Excel's answers (SH-68): SUBTOTAL and AGGREGATE's options 0 to 3 pass over it,
/// and AGGREGATE's options 4 to 7 count its Value.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// Whether a cell holding <paramref name="formula"/> is a nested SUBTOTAL or AGGREGATE: its
    /// Formula calls either anywhere in its text — the whole Formula, inside a larger expression, in
    /// parentheses, under <c>IF</c> — as Excel counts one (SUBTOTAL-020, 021, 035 to 038,
    /// AGGREGATE-026, 052 to 054). <see langword="false"/> for a constant or a blank.
    /// </summary>
    private static bool IsNested(Node? formula) =>
        formula is not null && formula.Calls.Any(call => call.Function is not null && call.Name is "SUBTOTAL" or "AGGREGATE");

    /// <summary>
    /// SUBTOTAL: function numbers 1 to 11 — AVERAGE, COUNT, COUNTA, MAX, MIN, PRODUCT, STDEV.S,
    /// STDEV.P, SUM, VAR.S, VAR.P — over its references, each function as it reads a range; 101 to
    /// 111 give the same while no row can be hidden. A function number that is not whole is truncated
    /// toward zero, as Excel's is (SUBTOTAL-027: 9.5 is 9). A nested cell (<see cref="IsNested"/>)
    /// is passed over. <c>#VALUE!</c>, as Excel's was for 12 and 0 (SUBTOTAL-025, 026): another function
    /// number; and an argument that is no reference when it is computed (a Formula that gives one something that can never be a
    /// Reference is refused on entry, <see cref="FunctionDefinition.ReferenceOnly"/>).
    /// </summary>
    private static Operand Subtotal(FunctionCall call)
    {
        if (!TryTruncated(call, 0, out var number, out var failure)) return failure;
        // No row can be hidden yet (ADR-0046), so 101 to 111 skip nothing 1 to 11 do not.
        var function = number is >= 101 and <= 111 ? number - 100 : number;
        if (function is < 1 or > 11) return Operand.Of(ErrorValue.Value);
        var values = new List<Value>();
        for (var i = 1; i < call.Count; i++)
        {
            if (!TryCollect(call, call.Operand(i), arrays: false, skipNested: true, passOverErrors: false, values, out failure)) return failure;
        }
        return Reduce(function, values, k: null);
    }

    /// <summary>
    /// AGGREGATE: function numbers 1 to 12 over references (the reference form) and 14 and 15 —
    /// LARGE and SMALL — over an array and its k (the array form), with options 0 to 7. Options 0 to 3
    /// pass over a nested cell (<see cref="IsNested"/>), and options 4 to 7 count its Value, as Excel
    /// does (AGGREGATE-025, 042 to 051); options 2, 3, 6 and 7 pass over Error Values; options 1, 3,
    /// 5 and 7, which pass over hidden rows, give what 0, 2, 4 and 6 give, since no row can be hidden.
    /// A function number or option that is not whole is truncated toward zero, as SUBTOTAL's is. The
    /// array form takes a Value given directly as an array of that one Value: a number is its one
    /// number (AGGREGATE-039), and text or a boolean leaves no number, so <c>#NUM!</c> (AGGREGATE-061).
    /// Its k is read as LARGE's and SMALL's (<see cref="KthOf"/>).
    /// Refused (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's): the function numbers of
    /// functions the engine does not support (13, MODE.SNGL; 16 to 19, PERCENTILE and QUARTILE), where Excel
    /// answers (AGGREGATE-027 to 031). <c>#VALUE!</c>, as Excel's: a number or option outside Excel's, LARGE
    /// or SMALL without its k (as Microsoft documents) or with an argument after it, and an argument
    /// of the reference form that is no reference when it is computed.
    /// </summary>
    private static Operand Aggregate(FunctionCall call)
    {
        if (!TryTruncated(call, 0, out var function, out var failure)) return failure;
        var option = 0;
        if (call.Has(1) && !TryTruncated(call, 1, out option, out failure)) return failure;
        if (function is < 1 or > 19 || option is < 0 or > 7) return Operand.Of(ErrorValue.Value);
        // MODE.SNGL, PERCENTILE.INC, QUARTILE.INC, PERCENTILE.EXC and QUARTILE.EXC are not declared: refused.
        if (function is 13 or > 15) return call.Evaluator.Refuse();
        var skipNested = option <= 3;
        var passOverErrors = option is 2 or 3 or 6 or 7;
        var values = new List<Value>();
        if (function <= 12)
        {
            for (var i = 2; i < call.Count; i++)
            {
                if (!TryCollect(call, call.Operand(i), arrays: false, skipNested, passOverErrors, values, out failure)) return failure;
            }
            return Reduce(function, values, k: null);
        }

        // The array form: the array, and k.
        if (call.Count != 4) return Operand.Of(ErrorValue.Value);
        if (!TryCollect(call, call.Operand(2), arrays: true, skipNested, passOverErrors, values, out failure)) return failure;
        if (values.FirstOrDefault(v => v.IsError) is { IsError: true } error) return Operand.Of(error);
        if (!TryNumber(call, 3, out var k, out failure)) return failure;
        return Reduce(function, values, k);
    }

    /// <summary>
    /// Argument <paramref name="index"/> truncated toward zero to a whole number, as Excel reads
    /// SUBTOTAL's function number (SUBTOTAL-027) and, by the same rule, AGGREGATE's function number and
    /// option; one past what an <see cref="int"/> holds is refused with <c>#VALUE!</c>, outside every
    /// range either takes.
    /// </summary>
    private static bool TryTruncated(FunctionCall call, int index, out int whole, out Operand failure)
    {
        whole = 0;
        if (!TryNumber(call, index, out var number, out failure)) return false;
        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        whole = (int)truncated;
        return true;
    }

    /// <summary>
    /// The Values of one argument that SUBTOTAL or AGGREGATE reduces, added in order to
    /// <paramref name="values"/>: a reference's cells that are not blank, a Linked Table's column, or,
    /// where <paramref name="arrays"/> allows it, a computed array or a number, text or boolean given
    /// directly, which is an array of that one Value (<see cref="Reduce"/> counts only its numbers). A
    /// nested cell (<see cref="IsNested"/>) is passed over when <paramref name="skipNested"/>, and its
    /// Value counted otherwise; an Error Value is passed over when <paramref name="passOverErrors"/>. <see langword="false"/> with <paramref name="failure"/>
    /// when the argument is refused, or is an Error Value itself.
    /// </summary>
    private static bool TryCollect(FunctionCall call, Operand operand, bool arrays, bool skipNested, bool passOverErrors, List<Value> values, out Operand failure)
    {
        failure = default;
        var cells = call.Evaluator.Cells;
        switch (operand.Kind)
        {
            case OperandKind.Area:
                foreach (var address in cells.NonBlankIn(operand.Area))
                {
                    if (skipNested && IsNested(cells.FormulaAt(address))) continue;
                    Add(cells.Read(address)!.Value);
                }
                return true;
            case OperandKind.Column:
                foreach (var value in operand.Column!) if (value is { } v) Add(v);
                return true;
            case OperandKind.Array when arrays:
                foreach (var value in operand.Array!.NonBlank()) Add(value);
                return true;
            // A Value given directly is an array of that one Value: a number is taken (AGGREGATE-039), and
            // text or a boolean is no number, not coerced, so LARGE and SMALL pass over it (AGGREGATE-061:
            // AGGREGATE(14,6,"5",1) is #NUM!, not 5 and not #VALUE!).
            case OperandKind.Scalar when arrays && operand.Scalar is { Kind: ValueKind.Number or ValueKind.Text or ValueKind.Boolean } given:
                Add(given);
                return true;
            default:
                failure = operand.IsError ? operand : Operand.Of(ErrorValue.Value);
                return false;
        }

        void Add(Value value)
        {
            if (!(value.IsError && passOverErrors)) values.Add(value);
        }
    }

    /// <summary>
    /// The function a SUBTOTAL or AGGREGATE number names, over <paramref name="values"/>, each as it
    /// reads a range: only numbers count, COUNT passes over an Error Value and COUNTA counts it, and
    /// for the others the first Error Value is the result.
    /// </summary>
    private static Operand Reduce(int function, List<Value> values, double? k)
    {
        if (function == 2) return Operand.Of(Value.FromNumber(values.Count(v => v.Kind == ValueKind.Number)));
        if (function == 3) return Operand.Of(Value.FromNumber(values.Count));
        var numbers = new List<double>();
        foreach (var value in values)
        {
            if (value.IsError) return Operand.Of(value);
            if (value.Kind == ValueKind.Number) numbers.Add(value.Number);
        }
        return function switch
        {
            1 => AverageOf(numbers),
            4 => MaxOf(numbers),
            5 => MinOf(numbers),
            6 => ProductOf(numbers),
            7 => SpreadOf(numbers, sample: true, root: true),
            8 => SpreadOf(numbers, sample: false, root: true),
            9 => SumOf(numbers),
            10 => SpreadOf(numbers, sample: true, root: false),
            11 => SpreadOf(numbers, sample: false, root: false),
            12 => MedianOf(numbers),
            14 => KthOf(numbers, k!.Value, largest: true),
            15 => KthOf(numbers, k!.Value, largest: false),
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, "No such function number."),
        };
    }
}
