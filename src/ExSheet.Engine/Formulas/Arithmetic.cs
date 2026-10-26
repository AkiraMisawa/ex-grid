using System.Globalization;
using System.Numerics;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// The places where Excel's arithmetic is not plain IEEE arithmetic, as it was observed
/// (ADR-0047, "Observed in Excel" and its second and third runs; verification/2026-09-27-windows-excel,
/// verification/2026-09-27-windows-excel-2 and verification/2026-09-28-windows-excel-3). Excel
/// computes in doubles, and then adjusts four things: a final addition or subtraction that nearly
/// cancels is 0, and so is <c>SUM</c>'s last addition; a comparison of two numbers is made at
/// Excel's precision; and a power is e^(b ln a), not the correctly rounded one, except a square root.
/// </summary>
/// <remarks>
/// Neither adjustment is documented by Microsoft; both were observed (ADR-0047, "What the third
/// observation settled" and "Settled by the equality run"):
/// <list type="bullet">
/// <item>A final addition is 0 when its operands are closer than 2^-49 (1.78E-15) of each.
/// Observed: 0 at 1.11E-15 of each (<c>=1+1E-15-1</c>) and below; not 0 at 1.998E-15
/// (<c>=1+2E-15-1</c>) and above. 2^-49 is the only power of two between the two.</item>
/// <item>Two numbers compare as they read at 15 significant digits: each is rounded to 15
/// significant digits, half away from zero on its exact decimal value, and the results are
/// compared. Observed (verification/2026-09-29-windows-excel-equality): <c>=1+4.8E-15=1</c> is TRUE
/// (1.0000000000000049 reads 1.00000000000000), <c>=1+5E-15=1</c> is FALSE (1.00000000000001), and
/// away from 1, where a relative threshold would say TRUE, <c>=9+3E-14=9</c>, <c>=9+5E-15=9</c> and
/// <c>=1+4E-15=1+6E-15</c> are FALSE. Every comparison operator reads through it, so two numbers
/// that are equal are neither less nor greater.</item>
/// </list>
/// </remarks>
internal static class Arithmetic
{
    /// <summary>2^-49: a final addition whose operands are closer than this, relative to each, is 0.</summary>
    internal const double CancelTolerance = 1.0 / 562949953421312.0;

    /// <summary>
    /// Whether two numbers are equal at Excel's precision: identical, or the same when each is
    /// rounded to 15 significant digits. <c>=0.1+0.2=0.3</c> and <c>=1+4.8E-15=1</c> are TRUE in
    /// Excel; <c>=1+5E-15=1</c> and <c>=9+3E-14=9</c> are FALSE.
    /// </summary>
    public static bool ApproximatelyEqual(double a, double b) => a == b || AtFifteenDigits(a) == AtFifteenDigits(b);

    /// <summary>
    /// A number rounded to 15 significant digits, half away from zero on its exact decimal value (ADR-0047),
    /// as the nearest double: every place the engine reads a number at Excel's precision reads this one.
    /// </summary>
    internal static double AtFifteenDigits(double x)
    {
        if (!double.IsFinite(x) || x == 0) return x;
        var (digits, exponent) = FifteenDigits(Math.Abs(x));
        var rounded = double.Parse($"{digits}E{(exponent - 14).ToString(CultureInfo.InvariantCulture)}", CultureInfo.InvariantCulture);
        return x < 0 ? -rounded : rounded;
    }

    /// <summary>
    /// A positive finite number's 15 significant digits, rounded half away from zero on its exact decimal value
    /// (ADR-0047), and the power of ten of the first. .NET's own <c>"E14"</c> rounds the exact decimal value too,
    /// but an exact midpoint to even: 1.000030517578125 (1+2^-15) is 1.00003051757812 there and 1.00003051757813
    /// here, as the Script Core reads it. The differential run found it (src/npm/sheet-engine/differential/README.md).
    /// </summary>
    internal static (string Digits, int Exponent) FifteenDigits(double magnitude)
    {
        // A midpoint's exact value has 16 significant digits, the last a 5, so its 17 digits end in 50; few
        // numbers beside one spell that, and those the exact comparison turns away.
        var seventeen = magnitude.ToString("E16", CultureInfo.InvariantCulture);
        if (seventeen[16] == '5' && seventeen[17] == '0')
        {
            var sixteen = long.Parse(seventeen[0] + seventeen[2..17], CultureInfo.InvariantCulture);
            var exponent = int.Parse(seventeen.AsSpan(19), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (IsExactly(magnitude, sixteen, exponent - 15))
            {
                var away = (sixteen / 10) + 1;
                // 999999999999999.5 rounds to the next power of ten.
                return away == 1_000_000_000_000_000 ? ("100000000000000", exponent + 1) : (away.ToString(CultureInfo.InvariantCulture), exponent);
            }
        }
        var e14 = magnitude.ToString("E14", CultureInfo.InvariantCulture);
        return (e14[0] + e14[2..16], int.Parse(e14.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
    }

    /// <summary>Whether <paramref name="x"/> is exactly <paramref name="digits"/> times 10^<paramref name="power"/>.</summary>
    private static bool IsExactly(double x, long digits, int power)
    {
        var (significand, twos) = CorrectlyRounded.Decompose(x);
        var left = significand * BigInteger.Pow(2, Math.Max(twos, 0)) * BigInteger.Pow(10, Math.Max(-power, 0));
        var right = digits * BigInteger.Pow(10, Math.Max(power, 0)) * BigInteger.Pow(2, Math.Max(-twos, 0));
        return left == right;
    }

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
    /// and are closer than 2^-49 of each, the plain sum otherwise. Excel does this to the last
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
    /// exponent is the exact power rounded once (<see cref="PortableMath.PowWhole"/>), and an exponent of 0.5 is <see cref="Math.Sqrt"/>, the
    /// square root correctly rounded: Excel gives <c>=2^0.5</c> as 1.4142135623730951 (ARITH-133,
    /// third run), where e^(0.5 ln 2) is one unit in the last place below it. Any other exponent
    /// of a positive base is e^(b ln a), as Excel's is, e^x and ln x each correctly rounded: <c>=8^(1/3)</c> is 1.9999999999999998 and
    /// <c>=27^(1/3)</c> 2.9999999999999996, not the correctly rounded 2 and 3 (observed), so neither
    /// the correctly rounded power nor e^(b ln a) alone gives every observed answer.
    /// A negative base with an exponent that is not an integer has a real answer when the exponent
    /// is the reciprocal of an odd integer: Excel gives <c>=(-8)^(1/3)</c> as -1.9999999999999998,
    /// which is −e^(ln 8 / 3). Other fractional exponents of a negative base, such as 2/3, are
    /// <c>#NUM!</c>.
    /// </summary>
    public static double? Power(double a, double b)
    {
        if (b == Math.Floor(b)) return PortableMath.PowWhole(a, b);
        if (a == 0) return b > 0 ? 0 : double.PositiveInfinity;
        if (a > 0) return b == 0.5 ? Math.Sqrt(a) : PortableMath.Exp(PortableMath.Log(a) * b);
        var root = 1 / b;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1) return -PortableMath.Exp(PortableMath.Log(-a) * b);
        return null;
    }
}
