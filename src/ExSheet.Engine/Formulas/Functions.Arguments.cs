namespace ExSheet.Engine.Formulas;

/// <summary>
/// How the functions admitted after ADR-0047's first list read an argument that takes one Value.
/// A range of more than one cell is <c>#VALUE!</c>, as everywhere ExSheet has no spilled arrays
/// (ADR-0047); an Error Value is the function's result.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>Argument <paramref name="index"/> as one Value, blank as <see langword="null"/>; <paramref name="failure"/> when it is a range of several cells or an Error Value.</summary>
    private static bool TryScalar(FunctionCall call, int index, out Value? value, out Operand failure)
    {
        value = null;
        failure = default;
        var operand = call.Operand(index);
        if (IsArray(operand))
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true } error)
        {
            failure = Operand.Of(error);
            return false;
        }
        return true;
    }

    /// <summary>Argument <paramref name="index"/> coerced to a number as the operators coerce it: a blank is 0, a boolean 1 or 0, text that reads as a number is that number.</summary>
    private static bool TryNumber(FunctionCall call, int index, out double number, out Operand failure)
    {
        number = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        number = call.Evaluator.ToNumber(value, out var error);
        if (error is { } e)
        {
            failure = Operand.Of(e);
            return false;
        }
        return true;
    }

    /// <summary>Argument <paramref name="index"/> coerced to text as <c>&amp;</c> coerces it.</summary>
    private static bool TryText(FunctionCall call, int index, out string text, out Operand failure)
    {
        text = "";
        if (!TryScalar(call, index, out var value, out failure)) return false;
        text = call.Evaluator.ToText(value);
        return true;
    }
}
