namespace ExSheet.Engine.Formulas;

/// <summary>
/// The places where Excel's arithmetic is not plain IEEE arithmetic, as it was observed
/// (ADR-0047, "Observed in Excel"; verification/2026-09-27-windows-excel). Excel computes in
/// doubles, and then adjusts three things: a final addition or subtraction that nearly cancels is
/// 0, a comparison of two numbers is made at Excel's precision, and a negative number raised to
/// the reciprocal of an odd integer has a real root.
/// </summary>
/// <remarks>
/// How near is "nearly" is not documented by Microsoft. The threshold taken is a relative
/// difference below 2^-48 of both operands, about 3.6E-15 — LibreOffice's <c>approxEqual</c>,
/// written to reproduce Excel. It gives Excel's observed answer on every observed case
/// (<c>=0.1+0.2-0.3</c> and <c>=0.5-0.4-0.1</c> are 0, <c>=0.1+0.2=0.3</c> is TRUE); the cases at
/// the threshold itself are <c>uncertain</c> in the case corpus until Excel is asked.
/// </remarks>
internal static class Arithmetic
{
    /// <summary>2^-48: two numbers closer than this, relative to each, are equal at Excel's precision.</summary>
    private const double Tolerance = 1.0 / (16777216.0 * 16777216.0);

    /// <summary>
    /// Whether two numbers are equal at Excel's precision: identical, or both nonzero and closer
    /// than 2^-48 of each. <c>=0.1+0.2=0.3</c> is TRUE in Excel.
    /// </summary>
    public static bool ApproximatelyEqual(double a, double b)
    {
        if (a == b) return true;
        if (a == 0 || b == 0) return false;
        var difference = Math.Abs(a - b);
        return difference < Math.Abs(a) * Tolerance && difference < Math.Abs(b) * Tolerance;
    }

    /// <summary>
    /// A Formula's last operation, when it is an addition (a subtraction is the addition of the
    /// negated operand): 0 when the operands have opposite signs and are equal in magnitude at
    /// Excel's precision, the plain sum otherwise. Excel does this to the last operation only:
    /// <c>=0.5-0.4-0.1</c> is 0 but <c>=1*(0.5-0.4-0.1)</c> is -2.78E-17.
    /// </summary>
    public static double FinalAdd(double a, double b)
    {
        var opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
        return opposite && ApproximatelyEqual(a, -b) ? 0 : a + b;
    }

    /// <summary>
    /// <c>a^b</c>, or <see langword="null"/> where it has no real answer (<c>#NUM!</c>). A negative
    /// base with an exponent that is not an integer has a real answer when the exponent is the
    /// reciprocal of an odd integer: Excel gives <c>=(-8)^(1/3)</c> as -1.9999999999999998, which is
    /// −e^(ln 8 / 3), not the correctly rounded −2 that <see cref="Math.Pow"/> would give for 8.
    /// Other fractional exponents of a negative base, such as 2/3, are <c>#NUM!</c> (uncertain in
    /// the case corpus).
    /// </summary>
    public static double? Power(double a, double b)
    {
        if (a >= 0 || b == Math.Floor(b)) return Math.Pow(a, b);
        var root = 1 / b;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1) return -Math.Exp(Math.Log(-a) * b);
        return null;
    }
}
