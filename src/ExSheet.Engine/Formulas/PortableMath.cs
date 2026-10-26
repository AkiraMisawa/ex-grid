using System.Numerics;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// The functions a platform's math library answers, each its own last bit — glibc's, the Windows runtime's and
/// V8's differ — answered here as the exact value rounded once to the nearest double, ties to even, so that every
/// platform and the Script Core (<c>portable-math.ts</c>) give one double. The differential run found the
/// libraries apart (src/npm/sheet-engine/differential/README.md). Each is computed in <see cref="BigReal"/> to 128
/// bits and rounded when every number within its error rounds alike; otherwise to twice the bits, and so on (Ziv's
/// strategy). e^x and ln x, asked far more often, keep <see cref="CorrectlyRounded"/>'s double-double path.
/// </summary>
internal static class PortableMath
{
    /// <summary>The bits at the end of a result its computation may have got wrong: far more than any function here loses.</summary>
    private const int SlackBits = 24;

    /// <summary>The double nearest the value <paramref name="compute"/> approximates to the bits it is asked for.</summary>
    private static double Rounded(Func<int, BigReal> compute)
    {
        for (var precision = 128; precision <= 4096; precision *= 2)
        {
            var value = compute(precision);
            if (value.IsZero) return 0;
            var slack = (BigInteger.Abs(value.M) >> (precision - SlackBits)) + 1;
            var low = CorrectlyRounded.RoundToDouble(value.M - slack, value.E);
            var high = CorrectlyRounded.RoundToDouble(value.M + slack, value.E);
            if (low.Equals(high)) return low;
        }
        throw new InvalidOperationException("Internal: a function lies too close to a rounding boundary to round at 4096 bits.");
    }

    public static double Exp(double x) => CorrectlyRounded.Exp(x);

    public static double Log(double x) => CorrectlyRounded.Log(x);

    /// <summary>log10 x for x above 0; 10^k is k exactly.</summary>
    public static double Log10(double x)
    {
        if (!(x > 0) || !double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x), x, "Internal: log10 is asked of a number not above 0.");
        return CorrectlyRounded.TryLog10(x, out var rounded) ? rounded : Rounded(p => BigReal.Log10(BigReal.Of(x), p));
    }

    public static double Atan(double x) => x == 0 ? x : Rounded(p => BigReal.Atan(BigReal.Of(x), p));

    /// <summary>atan2(y, x) as <see cref="Math.Atan2"/> answers it at a 0: (0, x &gt; 0) is 0, (0, x &lt; 0) is pi, (y, 0) is pi/2 with y's sign.</summary>
    public static double Atan2(double y, double x)
    {
        if (y == 0) return x >= 0 && !double.IsNegative(x) ? y : double.IsNegative(y) ? -Rounded(BigReal.Pi) : Rounded(BigReal.Pi);
        if (x == 0) return Rounded(p => BigReal.Pi(p).Scaled(-1)) * Math.Sign(y);
        return Rounded(p => BigReal.Atan2(BigReal.Of(y), BigReal.Of(x), p));
    }

    /// <summary>asin x for |x| at most 1.</summary>
    public static double Asin(double x)
    {
        if (Math.Abs(x) == 1) return Rounded(p => BigReal.Pi(p).Scaled(-1)) * x;
        return x == 0 ? x : Rounded(p => BigReal.Asin(BigReal.Of(x), p));
    }

    /// <summary>acos x for |x| at most 1.</summary>
    public static double Acos(double x)
    {
        if (x == 1) return 0;
        if (x == -1) return Rounded(BigReal.Pi);
        return Rounded(p => BigReal.Acos(BigReal.Of(x), p));
    }

    /// <summary>The magnitude past which sinh and cosh overflow every double.</summary>
    private const double HyperbolicOverflow = 711;

    public static double Sinh(double x)
    {
        if (x == 0) return x;
        if (Math.Abs(x) > HyperbolicOverflow) return x > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        return Rounded(p => BigReal.Sinh(BigReal.Of(x), p));
    }

    public static double Cosh(double x) =>
        Math.Abs(x) > HyperbolicOverflow ? double.PositiveInfinity : Rounded(p => BigReal.Cosh(BigReal.Of(x), p));

    /// <summary>tanh x; past 22 in magnitude it lies within 2^-63 of 1 and rounds to it.</summary>
    public static double Tanh(double x)
    {
        if (x == 0) return x;
        if (Math.Abs(x) >= 22) return Math.Sign(x);
        return Rounded(p => BigReal.Tanh(BigReal.Of(x), p));
    }

    public static double Asinh(double x) => x == 0 ? x : Rounded(p => BigReal.Asinh(BigReal.Of(x), p));

    /// <summary>acosh x for x at least 1.</summary>
    public static double Acosh(double x) => Rounded(p => BigReal.Acosh(BigReal.Of(x), p));

    /// <summary>atanh x for |x| below 1.</summary>
    public static double Atanh(double x) => x == 0 ? x : Rounded(p => BigReal.Atanh(BigReal.Of(x), p));

    /// <summary>10^0 to 10^22, every one a double exactly.</summary>
    private static readonly double[] PowersOfTen =
        [1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22];

    /// <summary>
    /// <paramref name="a"/>^<paramref name="b"/> for a whole number <paramref name="b"/>, as <see cref="Math.Pow"/>
    /// answers it at 0, 1 and past the doubles' range, and otherwise the exact power rounded once: counted exactly
    /// where |b| is at most 64, and as e^(b ln |a|) beyond.
    /// </summary>
    public static double PowWhole(double a, double b)
    {
        if (b != Math.Floor(b) || !double.IsFinite(b)) throw new ArgumentOutOfRangeException(nameof(b), b, "Internal: no whole power.");
        if (b == 0) return 1;
        // One operation each, which IEEE arithmetic rounds correctly: a square, a reciprocal, and a power of ten whose 10^|b| is exact.
        if (b == 1) return a;
        if (b == 2) return a * a;
        if (b == -1) return 1 / a;
        if (Math.Abs(a) == 10 && Math.Abs(b) <= 22)
        {
            var exact = PowersOfTen[(int)Math.Abs(b)];
            var tenth = b > 0 ? exact : 1 / exact;
            return a < 0 && Math.Abs(b % 2) == 1 ? -tenth : tenth;
        }
        var odd = Math.Abs(b % 2) == 1;
        var negative = a < 0 && odd;
        if (a == 0)
        {
            var negativeZero = double.IsNegative(a) && odd;
            return b > 0 ? (negativeZero ? -0.0 : 0.0) : (negativeZero ? double.NegativeInfinity : double.PositiveInfinity);
        }
        var magnitude = Math.Abs(a);
        if (magnitude == 1) return negative ? -1 : 1;
        // Past the doubles' range at once: 2^1100 overflows and 2^-1180 underflows every double. The platform's log2 only
        // places the number on one side of a line far from the range's ends, which its last bit cannot move.
        var log2 = Math.Log2(magnitude) * b;
        if (log2 > 1100) return negative ? double.NegativeInfinity : double.PositiveInfinity;
        if (log2 < -1180) return negative ? -0.0 : 0.0;
        var (significand, exponent) = CorrectlyRounded.Decompose(magnitude);
        var twos = (int)BigInteger.TrailingZeroCount(significand);
        var oddPart = significand >> twos;
        var power = exponent + twos;
        double result;
        if (oddPart.IsOne)
        {
            result = CorrectlyRounded.RoundToDouble(BigInteger.One, (int)(power * b));
        }
        else if (Math.Abs(b) <= 64)
        {
            var whole = (int)b;
            var n = BigInteger.Pow(oddPart, Math.Abs(whole));
            if (whole > 0)
            {
                result = CorrectlyRounded.RoundToDouble(n, power * whole);
            }
            else
            {
                // 2^(power b) / n with 64 bits more than a double holds, and a sticky bit for any remainder.
                var shift = (int)n.GetBitLength() + 64;
                var quotient = BigInteger.DivRem(BigInteger.One << shift, n, out var remainder);
                result = CorrectlyRounded.RoundToDouble((quotient << 1) | (remainder.IsZero ? BigInteger.Zero : BigInteger.One), (power * whole) - shift - 1);
            }
        }
        else
        {
            var exponentWhole = new BigInteger(b);
            result = Rounded(p => BigReal.Exp(BigReal.Ln(BigReal.Of(magnitude), p + 16).Times(BigReal.Whole(exponentWhole), p + 16), p));
        }
        return negative ? -result : result;
    }
}
