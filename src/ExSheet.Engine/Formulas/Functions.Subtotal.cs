namespace ExSheet.Engine.Formulas;

/// <summary>
/// SUBTOTAL and AGGREGATE (ADR-0047's note of 2026-10-10), admitted while a Sheet cannot hide a
/// row (ADR-0046): the function numbers and options that pass over hidden rows give what the others
/// give, as they do in Excel over such a sheet. Microsoft documents that both pass over nested
/// subtotals, but not which cells count as nested; only a cell whose whole Formula is one call is
/// passed over, and any other call of either function inside the range is <c>#VALUE!</c> until a
/// Windows run asks Excel.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>What a cell inside SUBTOTAL's or AGGREGATE's range is, as a nested call.</summary>
    private enum Nesting
    {
        /// <summary>A constant, a blank, or a Formula that calls neither function.</summary>
        None,

        /// <summary>A cell whose whole Formula is a SUBTOTAL.</summary>
        Subtotal,

        /// <summary>A cell whose whole Formula is an AGGREGATE.</summary>
        Aggregate,

        /// <summary>Any other call of either: inside a larger Formula, or behind a spill.</summary>
        Other,
    }

    private static Nesting NestingOf(Node? formula)
    {
        if (formula is null) return Nesting.None;
        if (formula is FunctionNode { Function: not null, Name: "SUBTOTAL" }) return Nesting.Subtotal;
        if (formula is FunctionNode { Function: not null, Name: "AGGREGATE" }) return Nesting.Aggregate;
        return formula.Calls.Any(call => call.Function is not null && call.Name is "SUBTOTAL" or "AGGREGATE") ? Nesting.Other : Nesting.None;
    }

    /// <summary>
    /// SUBTOTAL: function numbers 1 to 11 — AVERAGE, COUNT, COUNTA, MAX, MIN, PRODUCT, STDEV.S,
    /// STDEV.P, SUM, VAR.S, VAR.P — over its references, each function as it reads a range; 101 to
    /// 111 give the same while no row can be hidden. A cell whose whole Formula is a SUBTOTAL is
    /// passed over. Refused with <c>#VALUE!</c>: another function number, one that is not a whole
    /// number, an argument that is no reference (a computed array, a number), and any other call of
    /// SUBTOTAL or AGGREGATE in the range, an AGGREGATE whole included.
    /// </summary>
    private static Operand Subtotal(FunctionCall call)
    {
        if (!TryWhole(call, 0, out var number, out var failure)) return failure;
        // No row can be hidden yet (ADR-0046), so 101 to 111 skip nothing 1 to 11 do not.
        var function = number is >= 101 and <= 111 ? number - 100 : number;
        if (function is < 1 or > 11) return Operand.Of(ErrorValue.Value);
        var values = new List<Value>();
        for (var i = 1; i < call.Count; i++)
        {
            if (!TryCollect(call, call.Operand(i), arrays: false, skipSubtotal: true, skipAggregate: false, passOverErrors: false, values, out failure)) return failure;
        }
        return Reduce(function, values, k: null);
    }

    /// <summary>
    /// AGGREGATE: function numbers 1 to 12 over references (the reference form) and 14 and 15 —
    /// LARGE and SMALL — over an array and its k (the array form), with options 0 to 7. Options 0 to 3
    /// pass over a cell whose whole Formula is a SUBTOTAL or an AGGREGATE; options 2, 3, 6 and 7 pass
    /// over Error Values; options 1, 3, 5 and 7, which pass over hidden rows, give what 0, 2, 4 and 6
    /// give, since no row can be hidden. Refused with <c>#VALUE!</c>: the function numbers of
    /// functions the engine does not support (13, MODE.SNGL; 16 to 19, PERCENTILE and QUARTILE), a
    /// number or option outside Excel's, one that is not a whole number, LARGE or SMALL without its k
    /// (as Microsoft documents) or with an argument after it, an argument of the reference form that
    /// is no reference, and any call of SUBTOTAL or AGGREGATE in the range that is not passed over.
    /// </summary>
    private static Operand Aggregate(FunctionCall call)
    {
        if (!TryWhole(call, 0, out var function, out var failure)) return failure;
        var option = 0;
        if (call.Has(1) && !TryWhole(call, 1, out option, out failure)) return failure;
        if (function is < 1 or > 19 || option is < 0 or > 7) return Operand.Of(ErrorValue.Value);
        // MODE.SNGL, PERCENTILE.INC, QUARTILE.INC, PERCENTILE.EXC and QUARTILE.EXC are not declared.
        if (function is 13 or > 15) return Operand.Of(ErrorValue.Value);
        var nested = option <= 3;
        var passOverErrors = option is 2 or 3 or 6 or 7;
        var values = new List<Value>();
        if (function <= 12)
        {
            for (var i = 2; i < call.Count; i++)
            {
                if (!TryCollect(call, call.Operand(i), arrays: false, skipSubtotal: nested, skipAggregate: nested, passOverErrors, values, out failure)) return failure;
            }
            return Reduce(function, values, k: null);
        }

        // The array form: the array, and k.
        if (call.Count != 4) return Operand.Of(ErrorValue.Value);
        if (!TryCollect(call, call.Operand(2), arrays: true, skipSubtotal: nested, skipAggregate: nested, passOverErrors, values, out failure)) return failure;
        if (values.FirstOrDefault(v => v.IsError) is { IsError: true } error) return Operand.Of(error);
        if (!TryNumber(call, 3, out var k, out failure)) return failure;
        return Reduce(function, values, k);
    }

    /// <summary>Argument <paramref name="index"/> as a whole number; a fraction is refused with <c>#VALUE!</c> until Excel is asked how it reads one.</summary>
    private static bool TryWhole(FunctionCall call, int index, out int whole, out Operand failure)
    {
        whole = 0;
        if (!TryNumber(call, index, out var number, out failure)) return false;
        if (number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue)
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        whole = (int)number;
        return true;
    }

    /// <summary>
    /// The Values of one argument that SUBTOTAL or AGGREGATE reduces, added in order to
    /// <paramref name="values"/>: a reference's cells that are not blank, a Linked Table's column, or,
    /// where <paramref name="arrays"/> allows it, a computed array. A nested call is passed over or
    /// refused (<see cref="NestingOf"/>), and an Error Value is passed over when
    /// <paramref name="passOverErrors"/>. <see langword="false"/> with <paramref name="failure"/> when
    /// the argument is refused, or is an Error Value itself.
    /// </summary>
    private static bool TryCollect(FunctionCall call, Operand operand, bool arrays, bool skipSubtotal, bool skipAggregate, bool passOverErrors, List<Value> values, out Operand failure)
    {
        failure = default;
        var cells = call.Evaluator.Cells;
        switch (operand.Kind)
        {
            case OperandKind.Area:
                foreach (var address in cells.NonBlankIn(operand.Area))
                {
                    switch (NestingOf(cells.FormulaAt(address)))
                    {
                        case Nesting.Subtotal when skipSubtotal:
                        case Nesting.Aggregate when skipAggregate:
                            continue;
                        case Nesting.None:
                            Add(cells.Read(address)!.Value);
                            continue;
                        default:
                            failure = Operand.Of(ErrorValue.Value);
                            return false;
                    }
                }
                return true;
            case OperandKind.Column:
                foreach (var value in operand.Column!) if (value is { } v) Add(v);
                return true;
            case OperandKind.Array when arrays:
                foreach (var value in operand.Array!.NonBlank()) Add(value);
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
