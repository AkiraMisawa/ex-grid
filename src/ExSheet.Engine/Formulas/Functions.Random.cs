namespace ExSheet.Engine.Formulas;

/// <summary>
/// RAND and RANDBETWEEN (ADR-0180): each draw a function of the Sheet Seed, the cell, the call's
/// place in its Formula and the count of changes, so the same Entries, seed and count give the same
/// draws whatever order cells are computed in. Excel's own numbers cannot be matched — its generator
/// is not documented — so what is Excel's is the contract it documents and was seen to keep.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>The largest bound RANDBETWEEN takes: 2^53, past which a double no longer holds every whole number (ADR-0180).</summary>
    private const double LargestBound = 9_007_199_254_740_992;

    /// <summary>RAND: a number from 0 up to 1, evenly spread; <c>#GETTING_DATA</c> while the Sheet has no seed.</summary>
    private static Operand Rand(FunctionCall call) =>
        DrawOf(call) is { } draw ? Operand.Of(Value.FromNumber(Draws.Unit(draw))) : Operand.Of(ErrorValue.GettingData);

    /// <summary>
    /// RANDBETWEEN: a whole number from <c>bottom</c> to <c>top</c>, both included, every one equally
    /// likely. As Excel was observed to answer: a bottom above the top is <c>#NUM!</c>, a boolean and
    /// text that is not a number are <c>#VALUE!</c>, a numeric string is read as its number, a blank
    /// Reference is 0, and an Error Value passes through (RANDBETWEEN-007 to 014). Refused by decision
    /// (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's): a fractional bound, whose draw
    /// Excel was seen to take past the top (RANDBETWEEN-004 to 006), and a bound above 2^53 in magnitude.
    /// </summary>
    private static Operand RandBetween(FunctionCall call)
    {
        if (!TryBound(call, 0, out var bottom, out var failure)) return failure;
        if (!TryBound(call, 1, out var top, out failure)) return failure;
        if (!IsWhole(bottom) || !IsWhole(top)) return call.Evaluator.Refuse();
        if (bottom > top) return Operand.Of(ErrorValue.Num);
        if (DrawOf(call) is not { } draw) return Operand.Of(ErrorValue.GettingData);
        return Operand.Of(Value.FromNumber(Draws.Between(draw, (long)bottom, (long)top)));

        static bool IsWhole(double bound) => bound == Math.Floor(bound) && Math.Abs(bound) <= LargestBound;
    }

    /// <summary>A bound of RANDBETWEEN: one Value, coerced as a number except that a boolean is <c>#VALUE!</c>, as Excel answered <c>RANDBETWEEN(TRUE,3)</c>.</summary>
    private static bool TryBound(FunctionCall call, int index, out double bound, out Operand failure)
    {
        bound = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        if (value is { Kind: ValueKind.Boolean })
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        bound = call.Evaluator.ToNumber(value, out var error);
        if (error is { } e)
        {
            failure = Operand.Of(e);
            return false;
        }
        return true;
    }

    /// <summary>This call's draw, from the cell it is computed in; <see langword="null"/> while the Sheet has no seed.</summary>
    private static ulong? DrawOf(FunctionCall call) =>
        call.Evaluator.Self is { } self ? call.Evaluator.Cells.Draw(self, call.Place) : null;
}
