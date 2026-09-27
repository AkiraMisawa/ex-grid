namespace ExSheet.Engine.Formulas;

/// <summary>
/// The places where Excel's arithmetic is not plain IEEE arithmetic, as it was observed
/// (ADR-0047, "Observed in Excel" and "Observed in Excel, second run";
/// verification/2026-09-27-windows-excel and verification/2026-09-27-windows-excel-2). Excel
/// computes in doubles, and then adjusts four things: a final addition or subtraction that nearly
/// cancels is 0, and so is <c>SUM</c>'s last addition; a comparison of two numbers is made at
/// Excel's precision; and a power is e^(b ln a), not the correctly rounded one.
/// </summary>
/// <remarks>
/// How near is "nearly" is not documented by Microsoft, and the two adjustments were observed to
/// use different thresholds. For each, the engine takes the tightest power of two consistent with
/// every observed case (ADR-0047, "What the second observation settled"):
/// <list type="bullet">
/// <item>A final addition is 0 when its operands are closer than 2^-51 (4.4E-16) of each.
/// Observed: 0 at 2.8E-16 of each (<c>=0.5-0.4-0.1</c>) and 1.9E-16 (<c>=0.1+0.2-0.3</c>); not 0
/// at 3.1E-15 (<c>=1+3E-15-1</c>) nor 3.6E-15 (<c>=10.1-10-0.1</c>). 2^-52 would miss the first.</item>
/// <item>Two numbers compare equal when they are closer than 2^-47 (7.1E-15) of each. Observed:
/// equal at 4.0E-15 (<c>=1+4E-15=1</c>), which 2^-48 would miss.</item>
/// </list>
/// Both boundaries are bracketed by <c>uncertain</c> cases in the corpus (ARITH-099..121) for the
/// next run on Windows.
/// </remarks>
internal static class Arithmetic
{
    /// <summary>2^-51: a final addition whose operands are closer than this, relative to each, is 0.</summary>
    private const double CancelTolerance = 1.0 / 2251799813685248.0;

    /// <summary>2^-47: two numbers closer than this, relative to each, are equal at Excel's precision.</summary>
    private const double EqualTolerance = 1.0 / 140737488355328.0;

    /// <summary>
    /// Whether two numbers are equal at Excel's precision: identical, or both nonzero and closer
    /// than 2^-47 of each. <c>=0.1+0.2=0.3</c> and <c>=1+4E-15=1</c> are TRUE in Excel.
    /// </summary>
    public static bool ApproximatelyEqual(double a, double b) => Near(a, b, EqualTolerance);

    private static bool Near(double a, double b, double tolerance)
    {
        if (a == b) return true;
        if (a == 0 || b == 0) return false;
        var difference = Math.Abs(a - b);
        return difference < Math.Abs(a) * tolerance && difference < Math.Abs(b) * tolerance;
    }

    /// <summary>
    /// A Formula's last operation, when it is an addition (a subtraction is the addition of the
    /// negated operand), and <c>SUM</c>'s last addition: 0 when the operands have opposite signs
    /// and are closer than 2^-51 of each, the plain sum otherwise. Excel does this to the last
    /// operation only: <c>=0.5-0.4-0.1</c> is 0, but <c>=1*(0.5-0.4-0.1)</c> is -2.78E-17 and
    /// <c>=(0.1+0.2-0.3)</c>, in parentheses, is 5.55E-17.
    /// </summary>
    public static double FinalAdd(double a, double b)
    {
        var opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
        return opposite && Near(a, -b, CancelTolerance) ? 0 : a + b;
    }

    /// <summary>
    /// <c>a^b</c>, or <see langword="null"/> where it has no real answer (<c>#NUM!</c>). An integer
    /// exponent is <see cref="Math.Pow"/>. Any other exponent of a positive base is e^(b ln a), as
    /// Excel's is: <c>=8^(1/3)</c> is 1.9999999999999998, not the correctly rounded 2 (observed).
    /// A negative base with an exponent that is not an integer has a real answer when the exponent
    /// is the reciprocal of an odd integer: Excel gives <c>=(-8)^(1/3)</c> as -1.9999999999999998,
    /// which is −e^(ln 8 / 3). Other fractional exponents of a negative base, such as 2/3, are
    /// <c>#NUM!</c>.
    /// </summary>
    public static double? Power(double a, double b)
    {
        if (a == 0 || b == Math.Floor(b)) return Math.Pow(a, b);
        if (a > 0) return Math.Exp(Math.Log(a) * b);
        var root = 1 / b;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1) return -Math.Exp(Math.Log(-a) * b);
        return null;
    }
}
