using System.Numerics;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// e^x and ln x, each the exact value rounded once to the nearest double, ties to even: the same double on
/// every platform. <see cref="Math.Exp"/> and <see cref="Math.Log(double)"/> are the platform's C library's,
/// which round a few arguments the other way, and not the same ones everywhere. PV, FV and XNPV raise 1+rate
/// to a number that is not whole by these (<c>Functions.Financial.cs</c>): Excel's XNPV-013 (the second
/// area pass of 2026-10-12) is a correctly rounded exponential's double at an argument where glibc's
/// <c>exp</c> is one unit in the last place below it, and every other power Excel was observed to raise in
/// PV, FV and XNPV is these functions' double too (<c>spikes/exsheet-financial-evaluation/</c>). That is the
/// one argument observed where an exponential tells Excel's from glibc's; no observed double tells
/// logarithms apart (FV-023 and PV-016 ask), so a correctly rounded logarithm is the engine's choice, not a
/// reading of Excel's.
/// </summary>
/// <remarks>
/// Each is computed in double-double arithmetic (<see cref="Wide"/>), about 100 bits, and that is rounded when no rounding
/// boundary lies within its error bound (<see cref="Bound"/>, 2^-90, against an analysis of about 2^-96).
/// Otherwise, for about one argument in 2^37, it is computed again in 256-bit fixed point
/// (<see cref="ExpExact"/>, <see cref="LogExact"/>), whose error is far below the distance from any
/// double's exponential or logarithm to a rounding boundary, so that every double is rounded correctly.
/// </remarks>
internal static class CorrectlyRounded
{
    /// <summary>The relative error the double-double path is trusted to; a result nearer than this to a rounding boundary is computed again exactly.</summary>
    private static readonly double Bound = Math.ScaleB(1, -90);

    /// <summary>e^<paramref name="x"/>, correctly rounded.</summary>
    public static double Exp(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x == 0) return 1;
        if (Math.Abs(x) > 708) return ExpExact(x); // overflow, and results near or below the smallest normal double
        var k = Math.Round(x * InverseLn2);
        // x - k*ln2 as a double-double: k*Ln2High is exact (|k| < 2^11, Ln2High has 42 bits), and so is the
        // subtraction, x and k*ln2 being within a factor of 2 of each other whenever k is not 0.
        var reduced = new Wide(x - (k * Ln2High), 0) - Wide.Product(k, Ln2Low) - new Wide(k * Ln2Lowest, 0);
        // e^r = (e^(r/16))^16, e^(r/16) by its Taylor series to 2^-113.
        var s = reduced * (1.0 / 16);
        Wide power = 1;
        for (var j = 14; j >= 1; j--) power = 1 + (s * power / j);
        for (var i = 0; i < 4; i++) power *= power;
        return TryRound(power, out var rounded) ? Math.ScaleB(rounded, (int)k) : ExpExact(x);
    }

    /// <summary>ln <paramref name="x"/>, correctly rounded.</summary>
    public static double Log(double x)
    {
        if (double.IsNaN(x) || x < 0) return double.NaN;
        if (x == 0) return double.NegativeInfinity;
        if (double.IsPositiveInfinity(x)) return x;
        if (x == 1) return 0;
        if (x < MinNormal) return LogExact(x);
        return TryRound(WideLog(x), out var rounded) ? rounded : LogExact(x);
    }

    /// <summary>
    /// log10 <paramref name="x"/>, correctly rounded, where the double-double path rounds it surely; false for a
    /// subnormal x and the rare argument near a rounding boundary, which <see cref="PortableMath.Log10"/> computes again.
    /// </summary>
    internal static bool TryLog10(double x, out double rounded)
    {
        if (x == 1)
        {
            rounded = 0;
            return true;
        }
        if (!(x >= MinNormal) || !double.IsFinite(x))
        {
            rounded = double.NaN;
            return false;
        }
        return TryRound(WideLog(x) * InverseLn10, out rounded);
    }

    /// <summary>ln <paramref name="x"/> in double-double, for a normal, finite x above 0.</summary>
    private static Wide WideLog(double x)
    {
        // x = m * 2^e with m in [sqrt(1/2), sqrt(2)]; ln m = 2 atanh(s), s = (m-1)/(m+1), |s| < 0.172.
        var bits = BitConverter.DoubleToInt64Bits(x);
        var e = (int)((bits >> 52) & 0x7FF) - 1023;
        var m = BitConverter.Int64BitsToDouble((bits & 0xFFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        if (m > Sqrt2)
        {
            m /= 2;
            e++;
        }
        var s = new Wide(m - 1, 0) / Wide.Sum(m, 1); // m - 1 is exact (Sterbenz)
        var t = s * s;
        var series = AtanhCoefficients[^1];
        for (var j = AtanhCoefficients.Length - 2; j >= 0; j--) series = AtanhCoefficients[j] + (t * series);
        return (s * series) + new Wide(e * Ln2High, 0) + Wide.Product(e, Ln2Low) + new Wide(e * Ln2Lowest, 0);
    }

    /// <summary>
    /// The double nearest <paramref name="value"/>, when every number within <see cref="Bound"/> of it rounds
    /// to that same double; false where a rounding boundary lies so close that the error could cross it.
    /// </summary>
    private static bool TryRound(Wide value, out double rounded)
    {
        // value.High is the double nearest High + Low (the sum is normalised), so the boundary to watch is the
        // midpoint between High and its neighbour on Low's side.
        rounded = value.High;
        var neighbour = value.Low >= 0 ? Math.BitIncrement(value.High) : Math.BitDecrement(value.High);
        var halfGap = Math.Abs(neighbour - value.High) / 2;
        var error = Bound * Math.Abs(value.High);
        return Math.Abs(value.Low) + error < halfGap && error < halfGap / 2;
    }

    private const double InverseLn2 = 1.4426950408889634;
    private const double MinNormal = 2.2250738585072014E-308;
    private static readonly double Sqrt2 = Math.Sqrt(2);

    // 256-bit fixed point: a BigInteger n stands for n * 2^-256.
    private const int Scale = 256;
    private static readonly BigInteger FixedOne = BigInteger.One << Scale;
    private static readonly BigInteger FixedLn2 = Atanh2(BigInteger.One, 3);

    /// <summary>ln 2 as three doubles: the first 42 bits, so that a whole number below 2^11 times it is exact, then the next 53, then the next.</summary>
    private static readonly double Ln2High;
    private static readonly double Ln2Low;
    private static readonly double Ln2Lowest;

    /// <summary>1/ln 10 as a double-double.</summary>
    private static readonly Wide InverseLn10;

    /// <summary>2/(2j+1), j = 0 to 24: 2 atanh s = s * sum of these times (s^2)^j, which at |s| below 0.172 leaves less than 2^-112.</summary>
    private static readonly Wide[] AtanhCoefficients;

    static CorrectlyRounded()
    {
        var high = FixedLn2 >> (Scale - 42);
        Ln2High = Math.ScaleB((double)high, -42);
        var rest = FixedLn2 - (high << (Scale - 42));
        Ln2Low = RoundToDouble(rest, -Scale);
        rest -= Exactly(Ln2Low, Scale);
        Ln2Lowest = RoundToDouble(rest, -Scale);
        // ln 10 = 3 ln 2 + ln(5/4), and ln(5/4) = 2 atanh(1/9).
        var inverseLn10 = (FixedOne << Scale) / ((3 * FixedLn2) + Atanh2(BigInteger.One, 9));
        var inverseHigh = RoundToDouble(inverseLn10, -Scale);
        InverseLn10 = new Wide(inverseHigh, RoundToDouble(inverseLn10 - Exactly(inverseHigh, Scale), -Scale));
        AtanhCoefficients = new Wide[25];
        for (var j = 0; j < AtanhCoefficients.Length; j++) AtanhCoefficients[j] = (Wide)2 / (2 * j + 1);
    }

    /// <summary>e^<paramref name="x"/> computed in 256-bit fixed point and rounded once: the path for arguments near a rounding boundary, and near overflow or underflow.</summary>
    internal static double ExpExact(double x)
    {
        if (double.IsNaN(x)) return x;
        if (double.IsPositiveInfinity(x) || x > 710) return double.PositiveInfinity;
        if (double.IsNegativeInfinity(x) || x < -746) return 0;
        if (Math.Abs(x) < Math.ScaleB(1, -60)) return 1; // e^x lies within 2^-60 of 1, which is the nearest double
        var f = Exactly(x, Scale);
        var k = (int)Math.Round(x / 0.6931471805599453);
        var r = f - (k * FixedLn2);
        var sum = BigInteger.Zero;
        var term = FixedOne;
        for (var j = 1; !term.IsZero; j++)
        {
            sum += term;
            term = BigInteger.Divide(term * r, FixedOne * j);
        }
        return RoundToDouble(sum, k - Scale);
    }

    /// <summary>ln <paramref name="x"/> computed in 256-bit fixed point and rounded once.</summary>
    internal static double LogExact(double x)
    {
        if (double.IsNaN(x) || x < 0) return double.NaN;
        if (x == 0) return double.NegativeInfinity;
        if (double.IsPositiveInfinity(x)) return x;
        var (significand, exponent) = Decompose(x);
        var length = (int)significand.GetBitLength();
        var k = exponent + length - 1; // x = y * 2^k, y in [1, 2)
        var top = BigInteger.One << (length - 1);
        return RoundToDouble((k * FixedLn2) + Atanh2(significand - top, significand + top), -Scale);
    }

    /// <summary>2 atanh(n/d) = ln((d+n)/(d-n)) in fixed point, for 0 &lt;= n/d &lt;= 1/3.</summary>
    private static BigInteger Atanh2(BigInteger n, BigInteger d)
    {
        var z = BigInteger.Divide(n << Scale, d);
        var z2 = BigInteger.Divide(z * z, FixedOne);
        var term = z;
        var sum = BigInteger.Zero;
        for (var k = 1; !term.IsZero; k += 2)
        {
            sum += term / k;
            term = BigInteger.Divide(term * z2, FixedOne);
        }
        return 2 * sum;
    }

    /// <summary>A finite double as significand * 2^exponent, the significand a whole number.</summary>
    internal static (BigInteger Significand, int Exponent) Decompose(double x)
    {
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
        var biased = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        return biased == 0 ? (new BigInteger(fraction), -1074) : (new BigInteger(fraction | (1L << 52)), biased - 1075);
    }

    /// <summary><paramref name="x"/> times 2^<paramref name="shift"/>, truncated toward zero to a whole number.</summary>
    private static BigInteger Exactly(double x, int shift)
    {
        var (significand, exponent) = Decompose(x);
        var scaled = exponent + shift >= 0 ? significand << (exponent + shift) : significand >> -(exponent + shift);
        return x < 0 ? -scaled : scaled;
    }

    /// <summary>The double nearest <paramref name="m"/> * 2^<paramref name="e"/>, ties to even, subnormal and infinite results included.</summary>
    internal static double RoundToDouble(BigInteger m, int e)
    {
        if (m.IsZero) return 0;
        var negative = m.Sign < 0;
        var a = BigInteger.Abs(m);
        var length = (int)a.GetBitLength();
        var top = e + length - 1; // a * 2^e lies in [2^top, 2^(top+1))
        if (top > 1023) return negative ? double.NegativeInfinity : double.PositiveInfinity;
        var precision = top >= -1022 ? 53 : 53 - (-1022 - top);
        var shift = length - precision;
        BigInteger kept;
        if (shift <= 0) kept = a;
        else
        {
            kept = a >> shift;
            var rest = a - (kept << shift);
            var half = BigInteger.One << (shift - 1);
            if (rest > half || (rest == half && !kept.IsEven)) kept += 1;
        }
        var result = Math.ScaleB((double)kept, e + Math.Max(shift, 0));
        return negative ? -result : result;
    }
}
