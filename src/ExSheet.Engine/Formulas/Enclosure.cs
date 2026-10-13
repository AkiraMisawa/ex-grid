using System.Collections.Concurrent;
using System.Numerics;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// A dyadic number, <see cref="Mantissa"/> times 2^<see cref="Exponent"/>, held exactly: an end of an
/// <see cref="Enclosure"/>. Its sums and products are exact, its exponent has no double's limits, and
/// <see cref="Down"/> and <see cref="Up"/> round it to a number of bits toward -∞ and +∞.
/// </summary>
internal readonly record struct Dyadic(BigInteger Mantissa, int Exponent)
{
    public static readonly Dyadic Zero = new(BigInteger.Zero, 0);
    public static readonly Dyadic One = new(BigInteger.One, 0);

    /// <summary>A finite double, exactly.</summary>
    public static Dyadic Of(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Only a finite double has a dyadic value.");
        if (value == 0) return Zero;
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biased = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        var mantissa = biased == 0 ? new BigInteger(fraction) : new BigInteger(fraction | (1L << 52));
        return new(value < 0 ? -mantissa : mantissa, biased == 0 ? -1074 : biased - 1075);
    }

    public int Sign => Mantissa.Sign;

    public bool IsZero => Mantissa.IsZero;

    /// <summary>The exponent of the leading bit: a number other than 0 lies in [2^Top, 2^(Top+1)) in magnitude.</summary>
    public int Top => Exponent + (int)BigInteger.Abs(Mantissa).GetBitLength() - 1;

    public Dyadic Negate() => new(-Mantissa, Exponent);

    public Dyadic Abs() => new(BigInteger.Abs(Mantissa), Exponent);

    /// <summary>This number times 2^<paramref name="power"/>, exactly.</summary>
    public Dyadic Scale(int power) => new(Mantissa, Exponent + power);

    public static Dyadic operator +(Dyadic a, Dyadic b)
    {
        if (a.IsZero) return b;
        if (b.IsZero) return a;
        var exponent = Math.Min(a.Exponent, b.Exponent);
        return new((a.Mantissa << (a.Exponent - exponent)) + (b.Mantissa << (b.Exponent - exponent)), exponent);
    }

    public static Dyadic operator -(Dyadic a, Dyadic b) => a + b.Negate();

    public static Dyadic operator *(Dyadic a, Dyadic b) => new(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);

    public static int Compare(Dyadic a, Dyadic b) => (a - b).Sign;

    /// <summary>The largest number of <paramref name="bits"/> significant bits not above this one.</summary>
    public Dyadic Down(int bits)
    {
        if (IsZero) return this;
        var shift = (int)BigInteger.Abs(Mantissa).GetBitLength() - bits;
        // BigInteger's right shift is arithmetic: it rounds a negative mantissa toward -∞ as well.
        return shift <= 0 ? this : new(Mantissa >> shift, Exponent + shift);
    }

    /// <summary>The smallest number of <paramref name="bits"/> significant bits not below this one.</summary>
    public Dyadic Up(int bits) => Negate().Down(bits).Negate();

    /// <summary><paramref name="a"/> / <paramref name="b"/> rounded to <paramref name="bits"/> bits, up (toward +∞) or down.</summary>
    public static Dyadic Quotient(Dyadic a, Dyadic b, int bits, bool up)
    {
        if (b.IsZero) throw new DivideByZeroException();
        if (a.IsZero) return Zero;
        var (quotient, inexact, exponent) = Divide(a, b, bits + 2);
        var positive = a.Sign == b.Sign;
        // The magnitude is quotient plus a fraction below 1: away from 0 is up for a positive quotient and
        // down for a negative one.
        if (inexact && positive == up) quotient += 1;
        var result = new Dyadic(positive ? quotient : -quotient, exponent);
        return up ? result.Up(bits) : result.Down(bits);
    }

    /// <summary>The double nearest <paramref name="a"/> / <paramref name="b"/>, ties to even.</summary>
    public static double QuotientToDouble(Dyadic a, Dyadic b)
    {
        if (b.IsZero) throw new DivideByZeroException();
        if (a.IsZero) return 0;
        var (quotient, inexact, exponent) = Divide(a, b, 66);
        // A last bit for any remainder, so that a tie is a tie only where the quotient is exact.
        var magnitude = CorrectlyRounded.RoundToDouble((quotient << 1) | (inexact ? BigInteger.One : BigInteger.Zero), exponent - 1);
        return a.Sign == b.Sign ? magnitude : -magnitude;
    }

    /// <summary>The double nearest this number, ties to even, subnormal and infinite results included.</summary>
    public double ToDouble() => CorrectlyRounded.RoundToDouble(Mantissa, Exponent);

    /// <summary>|a/b| truncated to a whole number of at least <paramref name="bits"/> bits, whether a remainder was dropped, and its exponent.</summary>
    private static (BigInteger Quotient, bool Inexact, int Exponent) Divide(Dyadic a, Dyadic b, int bits)
    {
        var numerator = BigInteger.Abs(a.Mantissa);
        var denominator = BigInteger.Abs(b.Mantissa);
        var shift = Math.Max(0, bits + (int)denominator.GetBitLength() - (int)numerator.GetBitLength());
        var quotient = BigInteger.DivRem(numerator << shift, denominator, out var remainder);
        return (quotient, !remainder.IsZero, a.Exponent - b.Exponent - shift);
    }
}

/// <summary>
/// A real number known to lie between two dyadic numbers, <see cref="Low"/> and <see cref="High"/>: interval
/// arithmetic, each operation rounding the low end of its result down and the high end up, so that the exact
/// result of the operation on any numbers within its operands' enclosures lies within the result's. PMT
/// computes its exact payment in it, and refuses where the enclosure does not decide the double nearest it
/// (<see cref="ExactPayment"/>). The functions, <see cref="Log1p"/>, <see cref="Expm1"/> and
/// <see cref="Exp"/>, are series whose truncation is bounded and added to the enclosure.
/// </summary>
internal readonly record struct Enclosure(Dyadic Low, Dyadic High)
{
    private static readonly Dyadic Two = new(2, 0);

    public static Enclosure Exactly(Dyadic value) => new(value, value);

    public static Enclosure Exactly(double value) => Exactly(Dyadic.Of(value));

    public bool ContainsZero => Low.Sign <= 0 && High.Sign >= 0;

    public Enclosure Negate() => new(High.Negate(), Low.Negate());

    /// <summary>This enclosure times 2^<paramref name="power"/>, exactly.</summary>
    public Enclosure Scale(int power) => new(Low.Scale(power), High.Scale(power));

    /// <summary>The least and the greatest magnitude within it; not for an enclosure of 0.</summary>
    public (Dyadic Least, Dyadic Most) Magnitudes()
    {
        if (ContainsZero) throw new InvalidOperationException("An enclosure of 0 has no least magnitude.");
        return Low.Sign > 0 ? (Low, High) : (High.Abs(), Low.Abs());
    }

    /// <summary>The narrower enclosure both say: each encloses the same number.</summary>
    public static Enclosure Both(Enclosure a, Enclosure b) =>
        new(Dyadic.Compare(a.Low, b.Low) >= 0 ? a.Low : b.Low, Dyadic.Compare(a.High, b.High) <= 0 ? a.High : b.High);

    public static Enclosure Add(Enclosure a, Enclosure b, int bits) => new((a.Low + b.Low).Down(bits), (a.High + b.High).Up(bits));

    public static Enclosure Subtract(Enclosure a, Enclosure b, int bits) => Add(a, b.Negate(), bits);

    public static Enclosure Multiply(Enclosure a, Enclosure b, int bits)
    {
        if (b.High.Sign <= 0 && b.Low.Sign < 0) return Multiply(a, b.Negate(), bits).Negate();
        if (b.Low.Sign < 0)
        {
            // b holds 0 inside it: take the one that does not, if either does not.
            if (a.Low.Sign >= 0 || a.High.Sign <= 0) return Multiply(b, a, bits);
            var low = Least(a.Low * b.High, a.High * b.Low);
            var high = Greatest(a.Low * b.Low, a.High * b.High);
            return new(low.Down(bits), high.Up(bits));
        }
        // b is 0 or above.
        var least = a.Low.Sign >= 0 ? a.Low * b.Low : a.Low * b.High;
        var most = a.High.Sign >= 0 ? a.High * b.High : a.High * b.Low;
        return new(least.Down(bits), most.Up(bits));
    }

    /// <summary><paramref name="a"/> / <paramref name="b"/>, where <paramref name="b"/> does not hold 0.</summary>
    public static Enclosure Divide(Enclosure a, Enclosure b, int bits)
    {
        if (b.ContainsZero) throw new DivideByZeroException();
        if (b.High.Sign < 0) return Divide(a, b.Negate(), bits).Negate();
        // b is above 0: a / b grows with a, and falls with b where a is above 0 and rises where it is below.
        var low = Dyadic.Quotient(a.Low, a.Low.Sign >= 0 ? b.High : b.Low, bits, up: false);
        var high = Dyadic.Quotient(a.High, a.High.Sign >= 0 ? b.Low : b.High, bits, up: true);
        return new(low, high);
    }

    /// <summary>
    /// ln 2 to <paramref name="bits"/> bits: 2 atanh(1/3), the sum of 2 / ((2j+1) 3^(2j+1)), in fixed point
    /// with every term truncated, so that the truncations and the terms left out bound it from above.
    /// </summary>
    public static Enclosure Ln2(int bits) => Ln2s.GetOrAdd(bits, static bits =>
    {
        var scale = bits + 8;
        var two = BigInteger.One << (scale + 1);
        var sum = BigInteger.Zero;
        var terms = 0;
        var power = new BigInteger(3);
        for (var j = 0; ; j++)
        {
            var term = two / ((2 * j + 1) * power);
            if (term.IsZero) break; // this term and the rest, each a ninth of the one before, sum to below 2 units
            sum += term;
            terms++;
            power *= 9;
        }
        return new(new Dyadic(sum, -scale).Down(bits), new Dyadic(sum + terms + 2, -scale).Up(bits));
    });

    private static readonly ConcurrentDictionary<int, Enclosure> Ln2s = new();

    /// <summary>
    /// ln(1 + <paramref name="d"/>) for an exact <paramref name="d"/> above -1: 2 atanh(d / (2 + d)) where |d|
    /// is below 1/2, which keeps the logarithm's relative precision however small d is; otherwise k ln 2 plus
    /// that of the rest, 1 + d being m 2^k with m within a factor of √2 of 1.
    /// </summary>
    public static Enclosure Log1p(Dyadic d, int bits)
    {
        if (d.IsZero) return Exactly(Dyadic.Zero);
        if (Dyadic.Compare(d, Dyadic.One.Negate()) <= 0) throw new ArgumentOutOfRangeException(nameof(d), "ln(1+d) needs d above -1.");
        var working = bits + 16;
        if (d.Top < -1)
        {
            var z = Divide(Exactly(d), Exactly(d + Two), working);
            return Multiply(z, AtanhSeries(z, working), working).Scale(1);
        }
        var onePlus = Dyadic.One + d;
        var k = onePlus.Top;
        var leading = onePlus.Scale(-k); // in [1, 2)
        if (Dyadic.Compare(leading * leading, Two) >= 0) k++;
        var rest = onePlus.Scale(-k) - Dyadic.One; // in [1/√2 - 1, √2 - 1)
        return Add(Multiply(Exactly(new Dyadic(k, 0)), Ln2(working + 16), working), Log1p(rest, working), working);
    }

    /// <summary>The sum of z^(2j) / (2j+1) for |z| at most 1/3, so that 2z times it is 2 atanh z = ln((1+z)/(1-z)).</summary>
    private static Enclosure AtanhSeries(Enclosure z, int bits)
    {
        var square = Multiply(z, z, bits);
        var top = square.High.Top + 1; // square < 2^top, top at most -3
        var count = Math.Max(0, (int)Math.Ceiling((bits + 2.0) / -top) - 1); // square^(count+1) < 2^-(bits+2)
        var sum = Reciprocal(2 * count + 1, bits);
        for (var j = count - 1; j >= 0; j--) sum = Add(Reciprocal(2 * j + 1, bits), Multiply(square, sum, bits), bits);
        // The terms left out sum to below square^(count+1) / (1 - square) < 2^(top (count+1) + 1).
        return Add(sum, new(Dyadic.Zero, Dyadic.One.Scale((top * (count + 1)) + 1)), bits);
    }

    private static Enclosure Reciprocal(int k, int bits)
    {
        var whole = new Dyadic(k, 0);
        return new(Dyadic.Quotient(Dyadic.One, whole, bits, up: false), Dyadic.Quotient(Dyadic.One, whole, bits, up: true));
    }

    /// <summary>e^y - 1 over an enclosure: it rises with y, so it lies between its values at the two ends.</summary>
    public static Enclosure Expm1(Enclosure y, int bits)
    {
        var low = Expm1At(y.Low, bits);
        return Dyadic.Compare(y.Low, y.High) == 0 ? low : new(low.Low, Expm1At(y.High, bits).High);
    }

    /// <summary>e^y over an enclosure, as <see cref="Expm1"/>.</summary>
    public static Enclosure Exp(Enclosure y, int bits)
    {
        var low = ExpAt(y.Low, bits);
        return Dyadic.Compare(y.Low, y.High) == 0 ? low : new(low.Low, ExpAt(y.High, bits).High);
    }

    /// <summary>
    /// e^y - 1 for an exact y. Below 1/2 in magnitude, its Taylor series at y / 2^s, s halvings bringing it below
    /// 2^-8, keeps its relative precision however small y is, and each halving is undone by
    /// e^(2u) - 1 = (e^u - 1)(e^u - 1 + 2); otherwise e^y less 1.
    /// </summary>
    private static Enclosure Expm1At(Dyadic y, int bits)
    {
        if (y.IsZero) return Exactly(Dyadic.Zero);
        var working = bits + 24;
        if (y.Top >= -1) return Subtract(ExpAt(y, bits), Exactly(Dyadic.One), working);
        var halvings = Math.Max(0, y.Top + 9);
        var u = y.Scale(-halvings);
        var top = u.Top + 1; // |u| < 2^top, top at most -8
        var degree = Math.Max(1, (int)Math.Ceiling((working + 3.0) / -top)); // |u|^degree < 2^-(working+3)
        // u (1 + u/2 (1 + u/3 (... (1 + u/degree)))): the series to u^degree / degree!, whose rest is below
        // 2|u|^(degree+1) / (degree+1)!, so below |u| 2^-(working+2).
        var exactly = Exactly(u);
        var one = Exactly(Dyadic.One);
        var inner = one;
        for (var k = degree; k >= 2; k--) inner = Add(one, Divide(Multiply(exactly, inner, working), Exactly(new Dyadic(k, 0)), working), working);
        var rest = u.Abs().Scale(-(working + 2));
        var result = Add(Multiply(exactly, inner, working), new(rest.Negate(), rest), working);
        for (var i = 0; i < halvings; i++) result = Multiply(result, Add(result, Exactly(Two), working), working);
        return result;
    }

    /// <summary>e^y for an exact y: 2^k (1 + (e^t - 1)), t = y - k ln 2 below 1/2 in magnitude.</summary>
    private static Enclosure ExpAt(Dyadic y, int bits)
    {
        var working = bits + 24;
        if (y.Top < -1) return Add(Exactly(Dyadic.One), Expm1At(y, bits), working);
        // Any whole k leaves |t| below 1/2 here, ln 2 / 2 and a rounding; ToDouble and the division are IEEE's,
        // so k is the same on every platform.
        var k = Math.Round(y.ToDouble() / 0.6931471805599453);
        var t = Subtract(Exactly(y), Multiply(Exactly(new Dyadic(new BigInteger(k), 0)), Ln2(working + 16), working + 16), working + 16);
        var fraction = new Enclosure(Expm1At(t.Low, working).Low, Expm1At(t.High, working).High);
        return Add(Exactly(Dyadic.One), fraction, working).Scale((int)k);
    }

    private static Dyadic Least(Dyadic a, Dyadic b) => Dyadic.Compare(a, b) <= 0 ? a : b;

    private static Dyadic Greatest(Dyadic a, Dyadic b) => Dyadic.Compare(a, b) >= 0 ? a : b;
}
