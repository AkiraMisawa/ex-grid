namespace ExSheet.Engine.Formulas;

/// <summary>ROUNDUP, ROUNDDOWN, ABS, INT and MOD, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The quotient at and above which MOD is <c>#NUM!</c>. Older versions of Excel were known to
    /// fail once the quotient reached 2^27; whether a current Excel still does is asked of the next
    /// Windows run (MOD-008). Until it answers, a quotient this large is refused rather than computed.
    /// </summary>
    private const double ModQuotientLimit = 134_217_728;

    private static Operand RoundUp(FunctionCall call) => RoundBy(call, Rounding.AwayFromZero);

    private static Operand RoundDown(FunctionCall call) => RoundBy(call, Rounding.TowardZero);

    private static Operand RoundBy(FunctionCall call, Rounding rounding)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var digits, out failure)) return failure;
        return Operand.Of(Evaluator.Number(RoundAt(number, Math.Truncate(digits), rounding)));
    }

    private static Operand Abs(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Value.FromNumber(Math.Abs(number))) : failure;

    /// <summary>INT rounds down, toward negative infinity: INT(-8.9) is -9.</summary>
    private static Operand Int(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Value.FromNumber(Math.Floor(number))) : failure;

    /// <summary>
    /// MOD: <c>number - divisor*INT(number/divisor)</c>, so the result takes the divisor's sign.
    /// A divisor of 0 is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Mod(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var divisor, out failure)) return failure;
        if (divisor == 0) return Operand.Of(ErrorValue.Div0);
        var quotient = number / divisor;
        if (Math.Abs(quotient) >= ModQuotientLimit) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Evaluator.Number(number - divisor * Math.Floor(quotient)));
    }
}
