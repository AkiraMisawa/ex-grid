namespace ExSheet.Engine.Formulas;

/// <summary>AND, OR, NOT, IFNA, ISBLANK and ISNUMBER, each to Microsoft's documentation of it.</summary>
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
}
