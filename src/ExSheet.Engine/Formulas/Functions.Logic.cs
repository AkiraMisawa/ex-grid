namespace ExSheet.Engine.Formulas;

/// <summary>AND, OR, NOT, IFNA, ISBLANK, ISNUMBER, ISTEXT, ISNA, NA, IFS and SWITCH, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    private static Operand And(FunctionCall call) => Logical(call, all: true);

    private static Operand Or(FunctionCall call) => Logical(call, all: false);

    /// <summary>
    /// AND and OR. Inside a range or a Reference, numbers and booleans count, a number being TRUE
    /// unless it is 0, and text and blanks are ignored. Typed as an argument, text counts only when
    /// it spells TRUE or FALSE, and other text is <c>#VALUE!</c>. With nothing that counts, the
    /// result is <c>#VALUE!</c>. An Error Value anywhere is the result, the first one found left to
    /// right; every argument is read, so an error after a FALSE is still the result.
    /// </summary>
    private static Operand Logical(FunctionCall call, bool all)
    {
        var evaluator = call.Evaluator;
        bool? result = null;
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column:
                    foreach (var value in evaluator.RangeValues(operand))
                    {
                        if (value.IsError) return Operand.Of(value);
                        if (value.Kind == ValueKind.Number) Fold(value.Number != 0);
                        else if (value.Kind == ValueKind.Boolean) Fold(value.Boolean);
                    }
                    break;
                case OperandKind.Missing:
                    Fold(false);
                    break;
                default:
                    if (operand.Scalar is not { } scalar) break;
                    if (!TryTruth(scalar, out var truth)) return Operand.Of(scalar.IsError ? scalar.Error : ErrorValue.Value);
                    Fold(truth);
                    break;
            }
        }
        return result is { } answer ? Operand.Of(Value.FromBoolean(answer)) : Operand.Of(ErrorValue.Value);

        void Fold(bool truth) => result = all ? (result ?? true) && truth : (result ?? false) || truth;
    }

    /// <summary>NOT: a blank is FALSE, so its NOT is TRUE; otherwise the test <c>IF</c> applies.</summary>
    private static Operand Not(FunctionCall call)
    {
        if (!TryScalar(call, 0, out var value, out var failure)) return failure;
        if (value is not { } v) return Operand.Of(Value.FromBoolean(true));
        return TryTruth(v, out var truth) ? Operand.Of(Value.FromBoolean(!truth)) : Operand.Of(ErrorValue.Value);
    }

    /// <summary>
    /// IFNA: as <c>IFERROR</c>, for <c>#N/A</c> alone. So it catches neither <c>#GETTING_DATA</c>
    /// (ADR-0049) nor <c>#CIRC!</c> (ADR-0047), which are not <c>#N/A</c>.
    /// </summary>
    private static Operand IfNa(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Operand.Of(ErrorValue.Value);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true, Error: ErrorValue.NA })
        {
            if (!call.Has(1)) return Operand.Blank;
            var fallback = call.Operand(1);
            if (IsArray(fallback)) return Operand.Of(ErrorValue.Value);
            return call.Evaluator.ScalarOf(fallback) is { } caught ? Operand.Of(caught) : Operand.Blank;
        }
        return value is { } v ? Operand.Of(v) : Operand.Blank;
    }

    /// <summary>ISBLANK: TRUE for an empty cell only; a Formula that returns <c>""</c> is not blank.</summary>
    private static Operand IsBlank(FunctionCall call) => Inspect(call, value => value is null);

    /// <summary>ISNUMBER: TRUE for a number only; text that reads as one is not a number.</summary>
    private static Operand IsNumber(FunctionCall call) => Inspect(call, value => value is { Kind: ValueKind.Number });

    /// <summary>
    /// An IS function: it answers TRUE or FALSE for any one Value, an Error Value included — but
    /// <c>#GETTING_DATA</c> and <c>#CIRC!</c> are its result, as <c>ISERROR</c>'s (ADR-0047,
    /// ADR-0049): FALSE would be an answer about data that has not arrived, or about a cycle.
    /// </summary>
    private static Operand Inspect(FunctionCall call, Func<Value?, bool> test)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Operand.Of(ErrorValue.Value);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true, Error: ErrorValue.GettingData or ErrorValue.Circ } waiting) return Operand.Of(waiting);
        return Operand.Of(Value.FromBoolean(test(value)));
    }

    /// <summary>ISTEXT: TRUE for text only, empty text included; a number is not text.</summary>
    private static Operand IsText(FunctionCall call) => Inspect(call, value => value is { Kind: ValueKind.Text });

    /// <summary>ISNA: TRUE for <c>#N/A</c> only.</summary>
    private static Operand IsNa(FunctionCall call) => Inspect(call, value => value is { IsError: true, Error: ErrorValue.NA });

    /// <summary>NA: the Error Value <c>#N/A</c>.</summary>
    private static Operand Na(FunctionCall call) => Operand.Of(ErrorValue.NA);

    /// <summary>
    /// IFS: the value beside the first test that is TRUE, each test read as <c>IF</c> reads its own;
    /// a test's Error Value is the result; with none TRUE, <c>#N/A</c>. Only the tests up to the
    /// first TRUE, and its value, are evaluated.
    /// </summary>
    private static Operand Ifs(FunctionCall call)
    {
        for (var i = 0; i + 1 < call.Count; i += 2)
        {
            var test = call.Operand(i);
            if (IsArray(test)) return Operand.Of(ErrorValue.Value);
            var truth = false;
            if (call.Evaluator.ScalarOf(test) is { } value && !TryTruth(value, out truth)) return Operand.Of(value.IsError ? value.Error : ErrorValue.Value);
            if (truth) return call.Has(i + 1) ? call.Operand(i + 1) : Operand.Of(Value.FromNumber(0));
        }
        return Operand.Of(ErrorValue.NA);
    }

    /// <summary>
    /// SWITCH: the result beside the first value equal to the expression, compared as <c>=</c>
    /// compares; with none, the default when one is given, else <c>#N/A</c>. An Error Value in the
    /// expression, or in a value reached before a match, is the result.
    /// </summary>
    private static Operand Switch(FunctionCall call)
    {
        if (!TryScalar(call, 0, out var expression, out var failure)) return failure;
        var pairs = (call.Count - 1) / 2;
        for (var p = 0; p < pairs; p++)
        {
            var at = 1 + (2 * p);
            if (!TryScalar(call, at, out var candidate, out failure)) return failure;
            if (Evaluator.Compare(expression, candidate, Arithmetic.ApproximatelyEqual) == 0)
            {
                return call.Has(at + 1) ? call.Operand(at + 1) : Operand.Of(Value.FromNumber(0));
            }
        }
        var hasDefault = (call.Count - 1) % 2 == 1;
        return hasDefault ? call.Operand(call.Count - 1) : Operand.Of(ErrorValue.NA);
    }
}
