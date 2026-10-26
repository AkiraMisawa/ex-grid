using System.Numerics;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// A binary number of any size, m × 2^e, carried to a chosen precision, and the elementary functions on it
/// (the Script Core's <c>big-real.ts</c>). <see cref="PortableMath"/> computes the functions the platform's math
/// library would otherwise answer — each library its own last bit — in it, and rounds the result once.
/// Addition and subtraction are exact; every other operation keeps the bits it is asked for, truncated toward zero.
/// </summary>
internal readonly record struct BigReal(BigInteger M, int E)
{
    public static readonly BigReal Zero = new(BigInteger.Zero, 0);
    public static readonly BigReal One = new(BigInteger.One, 0);

    /// <summary>A finite double, exactly.</summary>
    public static BigReal Of(double x)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x), x, "A BigReal is a finite number.");
        if (x == 0) return Zero;
        var (significand, exponent) = CorrectlyRounded.Decompose(x);
        return new(x < 0 ? -significand : significand, exponent);
    }

    /// <summary>A whole number, exactly.</summary>
    public static BigReal Whole(BigInteger n) => new(n, 0);

    public int Sign => M.Sign;

    public bool IsZero => M.IsZero;

    /// <summary>The power of two of the leading bit: the number lies in [2^Top, 2^(Top+1)) in magnitude.</summary>
    public int Top => M.IsZero ? int.MinValue : E + (int)BigInteger.Abs(M).GetBitLength() - 1;

    /// <summary>The double nearest, ties to even.</summary>
    public double ToDouble() => CorrectlyRounded.RoundToDouble(M, E);

    public BigReal Negated() => new(-M, E);

    public BigReal Abs() => M.Sign < 0 ? Negated() : this;

    /// <summary>This times 2^<paramref name="n"/>, exactly.</summary>
    public BigReal Scaled(int n) => new(M, E + n);

    public BigReal Plus(BigReal other)
    {
        if (M.IsZero) return other;
        if (other.M.IsZero) return this;
        var e = Math.Min(E, other.E);
        return new((M << (E - e)) + (other.M << (other.E - e)), e);
    }

    public BigReal Minus(BigReal other) => Plus(other.Negated());

    /// <summary>Kept to <paramref name="precision"/> bits, truncated toward zero.</summary>
    public BigReal Kept(int precision)
    {
        var magnitude = BigInteger.Abs(M);
        var excess = (int)magnitude.GetBitLength() - precision;
        if (excess <= 0) return this;
        var shifted = magnitude >> excess;
        return new(M.Sign < 0 ? -shifted : shifted, E + excess);
    }

    public BigReal Times(BigReal other, int precision) => new BigReal(M * other.M, E + other.E).Kept(precision);

    public BigReal DividedBy(BigReal other, int precision)
    {
        if (other.M.IsZero) throw new DivideByZeroException("Internal: a BigReal is divided by 0.");
        if (M.IsZero) return Zero;
        var n = BigInteger.Abs(M);
        var d = BigInteger.Abs(other.M);
        var shift = Math.Max(0, precision + (int)d.GetBitLength() - (int)n.GetBitLength() + 1);
        var q = (n << shift) / d;
        var negative = (M.Sign < 0) != (other.M.Sign < 0);
        return new BigReal(negative ? -q : q, E - other.E - shift).Kept(precision);
    }

    /// <summary>The square root of a number not below 0.</summary>
    public BigReal Sqrt(int precision)
    {
        if (M.Sign < 0) throw new ArgumentOutOfRangeException(nameof(precision), "Internal: the square root of a negative BigReal is asked.");
        if (M.IsZero) return Zero;
        var shift = Math.Max(0, (2 * precision) + 2 - (int)M.GetBitLength());
        if ((E - shift) % 2 != 0) shift++;
        return new BigReal(WholeSqrt(M << shift), (E - shift) / 2).Kept(precision);
    }

    public int CompareTo(BigReal other) => Minus(other).Sign;

    /// <summary>The whole square root, rounded down, of a whole number above 0 (Newton's method from above).</summary>
    private static BigInteger WholeSqrt(BigInteger n)
    {
        var x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            var y = (x + (n / x)) >> 1;
            if (y >= x) return x;
            x = y;
        }
    }

    // ---- The functions, each to p bits with an error of a few of its last bits -------------------------------

    private static readonly Dictionary<int, BigReal> Ln2Cache = [];
    private static readonly Dictionary<int, BigReal> PiCache = [];
    private static readonly Dictionary<int, BigReal> Ln10Cache = [];
    private static readonly Lock CacheLock = new();

    private static BigReal Cached(Dictionary<int, BigReal> cache, int p, Func<BigReal> compute)
    {
        lock (CacheLock)
        {
            if (cache.TryGetValue(p, out var known)) return known;
        }
        var value = compute();
        lock (CacheLock) cache[p] = value;
        return value;
    }

    /// <summary>s + s^3/3 + s^5/5 + …, atanh s, for |s| at most 1/3.</summary>
    private static BigReal AtanhSeries(BigReal s, int p)
    {
        var s2 = s.Times(s, p);
        var term = s;
        var sum = Zero;
        for (var k = 1; !term.IsZero && term.Top > s.Top - p - 4; k += 2)
        {
            sum = sum.Plus(term.DividedBy(Whole(k), p)).Kept(p + 8);
            term = term.Times(s2, p);
        }
        return sum;
    }

    /// <summary>s - s^3/3 + s^5/5 - …, atan s, for |s| at most 1/8.</summary>
    private static BigReal AtanSeries(BigReal s, int p)
    {
        var s2 = s.Times(s, p).Negated();
        var term = s;
        var sum = Zero;
        for (var k = 1; !term.IsZero && term.Top > s.Top - p - 4; k += 2)
        {
            sum = sum.Plus(term.DividedBy(Whole(k), p)).Kept(p + 8);
            term = term.Times(s2, p);
        }
        return sum;
    }

    /// <summary>ln 2 to <paramref name="p"/> bits: 2 atanh(1/3).</summary>
    public static BigReal Ln2(int p) =>
        Cached(Ln2Cache, p, () => AtanhSeries(One.DividedBy(Whole(3), p + 8), p + 8).Scaled(1).Kept(p));

    /// <summary>pi to <paramref name="p"/> bits: Machin's 16 atan(1/5) - 4 atan(1/239).</summary>
    public static BigReal Pi(int p) => Cached(PiCache, p, () =>
    {
        var q = p + 8;
        var fifth = AtanSeries(Halved(One.DividedBy(Whole(5), q), q), q).Scaled(1);
        var rest = AtanSeries(One.DividedBy(Whole(239), q), q);
        return fifth.Scaled(4).Minus(rest.Scaled(2)).Kept(p);
    });

    /// <summary>tan(a/2) from tan a = s: s / (1 + sqrt(1 + s^2)).</summary>
    private static BigReal Halved(BigReal s, int p) => s.DividedBy(One.Plus(One.Plus(s.Times(s, p)).Sqrt(p)), p);

    /// <summary>ln x for x above 0.</summary>
    public static BigReal Ln(BigReal x, int p)
    {
        if (x.M.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Internal: the logarithm of a BigReal not above 0 is asked.");
        // x = y * 2^k with y in [1/sqrt 2, sqrt 2): the top 54 bits tell which side of sqrt 2 y lies on well enough.
        var k = x.Top;
        var y = x.Scaled(-k);
        if (y.Kept(54).ToDouble() > Math.Sqrt(2))
        {
            k++;
            y = y.Scaled(-1);
        }
        var s = y.Minus(One).DividedBy(y.Plus(One), p + 4);
        var logarithm = AtanhSeries(s, p + 4).Scaled(1);
        return k == 0 ? logarithm.Kept(p) : logarithm.Plus(Ln2(p + 16).Times(Whole(k), p + 16)).Kept(p);
    }

    /// <summary>ln(1 + u) for u above -1, as exact relative to u as to 1.</summary>
    public static BigReal Ln1p(BigReal u, int p)
    {
        if (u.IsZero) return Zero;
        if (u.Top < -1) return AtanhSeries(u.DividedBy(u.Plus(Whole(2)), p + 4), p + 4).Scaled(1).Kept(p);
        return Ln(One.Plus(u), p);
    }

    /// <summary>log10 x for x above 0.</summary>
    public static BigReal Log10(BigReal x, int p) =>
        Ln(x, p + 4).DividedBy(Cached(Ln10Cache, p + 4, () => Ln(Whole(10), p + 4)), p);

    /// <summary>e^x for |x| below 2^20.</summary>
    public static BigReal Exp(BigReal x, int p)
    {
        if (x.IsZero) return One;
        if (x.Top > 20) throw new ArgumentOutOfRangeException(nameof(x), "Internal: e^x is asked of a BigReal too large.");
        var q = p + 32;
        var k = (int)Math.Round(x.Kept(54).ToDouble() / 0.6931471805599453, MidpointRounding.AwayFromZero);
        var r = k == 0 ? x : x.Minus(Ln2(q + 24).Times(Whole(k), q + 24)).Kept(q);
        // e^r = (e^(r/256))^256, e^(r/256) by its Taylor series.
        var s = r.Scaled(-8);
        var term = One;
        var sum = Zero;
        for (var j = 1; !term.IsZero && term.Top > -q - 4; j++)
        {
            sum = sum.Plus(term).Kept(q + 8);
            term = term.Times(s, q).DividedBy(Whole(j), q);
        }
        var power = sum;
        for (var i = 0; i < 8; i++) power = power.Times(power, q);
        return power.Scaled(k).Kept(p);
    }

    /// <summary>e^x - 1 for |x| below 1, as exact relative to the result as e^x is.</summary>
    public static BigReal Expm1(BigReal x, int p)
    {
        if (x.IsZero) return Zero;
        if (x.Top >= 0) return Exp(x, p + 4).Minus(One).Kept(p);
        var term = x;
        var sum = Zero;
        for (var j = 2; !term.IsZero && term.Top > x.Top - p - 6; j++)
        {
            sum = sum.Plus(term).Kept(p + 8);
            term = term.Times(x, p + 4).DividedBy(Whole(j), p + 4);
        }
        return sum.Kept(p);
    }

    /// <summary>atan x.</summary>
    public static BigReal Atan(BigReal x, int p)
    {
        if (x.IsZero) return Zero;
        var q = p + 12;
        var a = x.Abs();
        // Above 1, pi/2 - atan(1/a), at least pi/4.
        var result = a.CompareTo(One) > 0 ? Pi(q).Scaled(-1).Minus(AtanReduced(One.DividedBy(a, q), q)) : AtanReduced(a, q);
        return (x.M.Sign < 0 ? result.Negated() : result).Kept(p);
    }

    /// <summary>atan a for 0 &lt;= a &lt;= 1: three halvings bring it below tan(pi/32), where the series converges fast.</summary>
    private static BigReal AtanReduced(BigReal a, int q)
    {
        var s = a;
        for (var i = 0; i < 3; i++) s = Halved(s, q);
        return AtanSeries(s, q).Scaled(3);
    }

    /// <summary>sinh x for |x| below 2^20.</summary>
    public static BigReal Sinh(BigReal x, int p)
    {
        if (x.Top < 0)
        {
            // x + x^3/3! + x^5/5! + …, with no cancellation.
            var x2 = x.Times(x, p + 4);
            var term = x;
            var sum = Zero;
            for (var j = 2; !term.IsZero && term.Top > x.Top - p - 6; j += 2)
            {
                sum = sum.Plus(term).Kept(p + 8);
                term = term.Times(x2, p + 4).DividedBy(Whole(j * (j + 1)), p + 4);
            }
            return sum.Kept(p);
        }
        var ex = Exp(x, p + 4);
        return ex.Minus(One.DividedBy(ex, p + 4)).Scaled(-1).Kept(p);
    }

    /// <summary>cosh x for |x| below 2^20.</summary>
    public static BigReal Cosh(BigReal x, int p)
    {
        var ex = Exp(x, p + 4);
        return ex.Plus(One.DividedBy(ex, p + 4)).Scaled(-1).Kept(p);
    }

    /// <summary>tanh x for |x| below 2^20.</summary>
    public static BigReal Tanh(BigReal x, int p)
    {
        var a = x.Abs();
        // (e^2a - 1) / (e^2a + 1), its numerator from e^2a - 1 as exact relative to itself as e^2a.
        var m1 = a.Top < -1 ? Expm1(a.Scaled(1), p + 4) : Exp(a.Scaled(1), p + 8).Minus(One);
        var result = m1.DividedBy(m1.Plus(Whole(2)), p);
        return x.M.Sign < 0 ? result.Negated() : result;
    }

    /// <summary>asinh x.</summary>
    public static BigReal Asinh(BigReal x, int p)
    {
        var a = x.Abs();
        var q = p + 8;
        var a2 = a.Times(a, q);
        var root = One.Plus(a2).Sqrt(q);
        var result = a.Top < 0 ? Ln1p(a.Plus(a2.DividedBy(One.Plus(root), q)), p) : Ln(a.Plus(root), p);
        return x.M.Sign < 0 ? result.Negated() : result;
    }

    /// <summary>acosh x for x at least 1.</summary>
    public static BigReal Acosh(BigReal x, int p)
    {
        var t = x.Minus(One);
        if (t.M.Sign < 0) throw new ArgumentOutOfRangeException(nameof(x), "Internal: acosh is asked of a BigReal below 1.");
        var q = p + 8;
        if (t.Top < 0) return Ln1p(t.Plus(t.Times(t.Plus(Whole(2)), q).Sqrt(q)), p);
        return Ln(x.Plus(x.Times(x, 2 * q).Minus(One).Sqrt(q)), p);
    }

    /// <summary>atanh x for |x| below 1: half ln(1 + 2|x|/(1 - |x|)).</summary>
    public static BigReal Atanh(BigReal x, int p)
    {
        var a = x.Abs();
        var result = Ln1p(a.Scaled(1).DividedBy(One.Minus(a), p + 8), p + 4).Scaled(-1).Kept(p);
        return x.M.Sign < 0 ? result.Negated() : result;
    }

    /// <summary>asin x for |x| below 1: atan(x / sqrt((1 - x)(1 + x))).</summary>
    public static BigReal Asin(BigReal x, int p)
    {
        var q = p + 8;
        var cosine = One.Minus(x).Times(One.Plus(x), q).Sqrt(q);
        return Atan(x.DividedBy(cosine, q), p);
    }

    /// <summary>acos x for |x| below 1: 2 atan(sqrt((1 - x)/(1 + x))).</summary>
    public static BigReal Acos(BigReal x, int p)
    {
        var q = p + 8;
        return Atan(One.Minus(x).DividedBy(One.Plus(x), q).Sqrt(q), q).Scaled(1).Kept(p);
    }

    /// <summary>The angle of (x, y), neither 0: atan(|y|/|x|), from pi when x is negative, with y's sign.</summary>
    public static BigReal Atan2(BigReal y, BigReal x, int p)
    {
        var q = p + 8;
        var angle = Atan(y.Abs().DividedBy(x.Abs(), q), q);
        var result = x.M.Sign < 0 ? Pi(q).Minus(angle) : angle;
        return (y.M.Sign < 0 ? result.Negated() : result).Kept(p);
    }
}
