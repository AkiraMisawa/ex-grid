namespace ExSheet.Engine.Formulas;

/// <summary>AND, OR, NOT, IFNA, ISBLANK, ISNUMBER, ISTEXT, ISNA, NA, ERROR.TYPE, IFS and SWITCH, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    private static Operand And(FunctionCall call) => Logical(call, all: true);

    private static Operand Or(FunctionCall call) => Logical(call, all: false);

    /// <summary>
    /// AND and OR, over the logical values <see cref="TryReadLogicals"/> reads. With nothing that
    /// counts, the result is <c>#VALUE!</c>.
    /// </summary>
    private static Operand Logical(FunctionCall call, bool all)
    {
        bool? result = null;
        if (!TryReadLogicals(call, truth => result = all ? (result ?? true) && truth : (result ?? false) || truth, out var failure)) return failure;
        return result is { } answer ? Operand.Of(Value.FromBoolean(answer)) : Operand.Of(ErrorValue.Value);
    }

    /// <summary>
    /// The logical values AND, OR and XOR take, left to right, each handed to <paramref name="take"/>.
    /// Inside a range or a Reference, numbers and booleans count, a number being TRUE unless it is 0,
    /// and text and blanks are passed over. Typed as an argument, a number or a boolean counts, and
    /// so does text that spells TRUE or FALSE; other text is passed over, as text inside a range is
    /// (AND-011: Excel's <c>AND("abc",TRUE)</c> is TRUE, read by the seventeenth Windows run). An
    /// argument left empty is FALSE. An Error Value anywhere is the <paramref name="failure"/>, the
    /// first one found left to right; no value decides the result early, so an error after a FALSE
    /// is still the result.
    /// </summary>
    private static bool TryReadLogicals(FunctionCall call, Action<bool> take, out Operand failure)
    {
        var evaluator = call.Evaluator;
        failure = default;
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column or OperandKind.Array:
                    foreach (var value in evaluator.RangeValues(operand))
                    {
                        if (value.IsError)
                        {
                            failure = Operand.Of(value);
                            return false;
                        }
                        if (value.Kind == ValueKind.Number) take(value.Number != 0);
                        else if (value.Kind == ValueKind.Boolean) take(value.Boolean);
                    }
                    break;
                case OperandKind.Missing:
                    take(false);
                    break;
                default:
                    if (operand.Scalar is not { } scalar) break;
                    if (scalar.IsError)
                    {
                        failure = Operand.Of(scalar);
                        return false;
                    }
                    if (TryTruth(scalar, out var truth)) take(truth);
                    break;
            }
        }
        return true;
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
        if (IsArray(operand)) return Unlifted(call);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true, Error: ErrorValue.NA })
        {
            if (!call.Has(1)) return Operand.Blank;
            var fallback = call.Operand(1);
            if (IsArray(fallback)) return Unlifted(call);
            return call.Evaluator.ScalarOf(fallback) is { } caught ? Operand.Of(caught) : Operand.Blank;
        }
        return value is { } v ? Operand.Of(v) : Operand.Blank;
    }

    /// <summary>ISBLANK: TRUE for an empty cell only; a Formula that returns <c>""</c> is not blank.</summary>
    private static Operand IsBlank(FunctionCall call) => Inspect(call, value => value is null);

    /// <summary>ISNUMBER: TRUE for a number only; text that reads as one is not a number.</summary>
    private static Operand IsNumber(FunctionCall call) => Inspect(call, value => value is { Kind: ValueKind.Number });

    /// <summary>
    /// An IS function: it answers TRUE or FALSE for any one Value, an Error Value included, and a kept
    /// blank as a blank cell's, as Excel's <c>ISBLANK</c> read <c>SORT</c>'s (SORT-022), carried to the
    /// other IS functions and the other four's kept blank by generalisation, which no run has read — but
    /// <c>#GETTING_DATA</c> and <c>#CIRC!</c> are its result, as <c>ISERROR</c>'s (ADR-0047,
    /// ADR-0049): FALSE would be an answer about data that has not arrived, or about a cycle.
    /// </summary>
    private static Operand Inspect(FunctionCall call, Func<Value?, bool> test)
    {
        var operand = call.OneValue(0);
        if (IsArray(operand)) return Unlifted(call);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true, Error: ErrorValue.GettingData or ErrorValue.Circ } waiting) return Operand.Of(waiting);
        return Operand.Of(Value.FromBoolean(test(value)));
    }

    /// <summary>ISTEXT: TRUE for text only, empty text included; a number is not text.</summary>
    private static Operand IsText(FunctionCall call) => Inspect(call, value => value is { Kind: ValueKind.Text });

    /// <summary>ISNA: TRUE for <c>#N/A</c> only.</summary>
    private static Operand IsNa(FunctionCall call) => Inspect(call, value => value is { IsError: true, Error: ErrorValue.NA });

    /// <summary>
    /// LET is read into a <see cref="LetNode"/> of its own, with its names in scope (ADR-0181), and
    /// never called as a function: declared here so that completion, the argument hint and the
    /// catalogue know it.
    /// </summary>
    private static Operand Let(FunctionCall call) => throw new InvalidOperationException("LET is evaluated as a LetNode, never as a call.");

    /// <summary>NA: the Error Value <c>#N/A</c>.</summary>
    private static Operand Na(FunctionCall call) => Operand.Of(ErrorValue.NA);

    /// <summary>
    /// ERROR.TYPE (ADR-0047's note of 2026-10-10): Microsoft's number for an Error Value —
    /// <c>#NULL!</c> 1, <c>#DIV/0!</c> 2, <c>#VALUE!</c> 3, <c>#REF!</c> 4, <c>#NAME?</c> 5,
    /// <c>#NUM!</c> 6, <c>#N/A</c> 7 — and <c>#N/A</c> for a Value that is no error. Excel's newer
    /// Error Values, outside Microsoft's table, take the numbers Excel gave them when the seventeenth
    /// Windows run asked (ERRORTYPE-013 to 019): <c>#SPILL!</c> 9, <c>#CONNECT!</c> 10,
    /// <c>#BLOCKED!</c> 11, <c>#UNKNOWN!</c> 12, <c>#FIELD!</c> 13, <c>#CALC!</c> 14.
    /// <c>#GETTING_DATA</c> and <c>#CIRC!</c> are given back, never numbered: Microsoft's table numbers
    /// <c>#GETTING_DATA</c> 8, and a number would let a Formula show something while its data is on its
    /// way (ADR-0049). An Error Value added later is <c>#VALUE!</c> until Excel is asked; not
    /// <c>#N/A</c>, which is the answer for a Value that is no error.
    /// </summary>
    private static Operand ErrorType(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Unlifted(call);
        if (call.Evaluator.ScalarOf(operand) is not { IsError: true } value) return Operand.Of(ErrorValue.NA);
        return value.Error switch
        {
            ErrorValue.Null => Operand.Of(Value.FromNumber(1)),
            ErrorValue.Div0 => Operand.Of(Value.FromNumber(2)),
            ErrorValue.Value => Operand.Of(Value.FromNumber(3)),
            ErrorValue.Ref => Operand.Of(Value.FromNumber(4)),
            ErrorValue.Name => Operand.Of(Value.FromNumber(5)),
            ErrorValue.Num => Operand.Of(Value.FromNumber(6)),
            ErrorValue.NA => Operand.Of(Value.FromNumber(7)),
            ErrorValue.Spill => Operand.Of(Value.FromNumber(9)),
            ErrorValue.Connect => Operand.Of(Value.FromNumber(10)),
            ErrorValue.Blocked => Operand.Of(Value.FromNumber(11)),
            ErrorValue.Unknown => Operand.Of(Value.FromNumber(12)),
            ErrorValue.Field => Operand.Of(Value.FromNumber(13)),
            ErrorValue.Calc => Operand.Of(Value.FromNumber(14)),
            ErrorValue.GettingData or ErrorValue.Circ => Operand.Of(value),
            _ => Operand.Of(ErrorValue.Value),
        };
    }

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
            if (IsArray(test)) return Unlifted(call);
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

    /// <summary>XOR: TRUE when an odd number of the logical values AND and OR take are TRUE; with none, <c>#VALUE!</c>.</summary>
    private static Operand Xor(FunctionCall call)
    {
        var any = false;
        var odd = false;
        if (!TryReadLogicals(call, Count, out var failure)) return failure;
        return any ? Operand.Of(Value.FromBoolean(odd)) : Operand.Of(ErrorValue.Value);

        void Count(bool truth)
        {
            any = true;
            if (truth) odd = !odd;
        }
    }
}
