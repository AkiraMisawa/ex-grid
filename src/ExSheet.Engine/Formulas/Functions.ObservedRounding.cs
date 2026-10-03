namespace ExSheet.Engine.Formulas;

/// <summary>Rounding to a multiple, pinned by the 2026-10-03 Windows observations (ADR-0047).</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// MROUND for whole-number multiples. Decimal multiples are refused: the Windows run's
    /// 6.05, 7.05, 0.15 and 0.35 midpoints cannot all be reproduced by one ordinary binary
    /// rounding operation, and Microsoft's MROUND documentation leaves their direction undefined.
    /// ADR-0047 permits this refusal, never an invented epsilon or a different numeric answer.
    /// </summary>
    private static Operand MRound(FunctionCall call)
    {
        if (!TryMroundNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryMroundNumber(call, 1, out var multiple, out failure)) return failure;
        if (number == 0 || multiple == 0) return Operand.Of(Value.FromNumber(0));
        if (Math.Sign(number) != Math.Sign(multiple)) return Operand.Of(ErrorValue.Num);
        if (multiple != Math.Truncate(multiple)) return Operand.Of(ErrorValue.Value);
        return Operand.Of(Evaluator.Number(Math.Round(number / multiple, MidpointRounding.AwayFromZero) * multiple));
    }

    private static bool TryMroundNumber(FunctionCall call, int index, out double number, out Operand failure)
    {
        number = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        if (value is { Kind: ValueKind.Boolean })
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        return TryNumber(call, index, out number, out failure);
    }

    private static Operand CeilingMath(FunctionCall call) => DirectionalMultiple(call, ceiling: true);

    private static Operand FloorMath(FunctionCall call) => DirectionalMultiple(call, ceiling: false);

    private static Operand DirectionalMultiple(FunctionCall call, bool ceiling)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        var significance = 1.0;
        if (call.Has(1) && !TryNumber(call, 1, out significance, out failure)) return failure;
        var mode = 0.0;
        if (call.Has(2) && !TryNumber(call, 2, out mode, out failure)) return failure;
        if (significance == 0 || number == 0) return Operand.Of(Value.FromNumber(0));
        significance = Math.Abs(significance);
        var quotient = number / significance;
        if (quotient == 0) return Operand.Of(ErrorValue.Num);
        // The run pins half-multiples, not which side Excel takes when division is almost
        // integral. Refuse that boundary at the existing 15-digit precision; never use
        // that precision as an epsilon to choose a rounded numeric answer.
        var nearest = Math.Round(quotient);
        if (quotient != nearest && Arithmetic.ApproximatelyEqual(quotient, nearest))
            return Operand.Of(ErrorValue.Value);
        if (number < 0 && mode != 0) ceiling = !ceiling;
        return Operand.Of(Evaluator.Number((ceiling ? Math.Ceiling(quotient) : Math.Floor(quotient)) * significance));
    }
}
