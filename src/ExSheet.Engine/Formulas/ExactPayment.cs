namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT's payment at a rate above -1 other than 0: the exact payment over its arguments' doubles, rounded once
/// to a double, or null where PMT refuses it (<c>Functions.Financial.cs</c>, ADR-0047's exception for PMT).
/// The payment at the end of each period is -rate (pv g + fv) / (g - 1), g = (1+rate)^nper, computed in
/// <see cref="Enclosure"/>s, which hold it between two bounds; the double is given only where both bounds
/// round to it, so that it is the exact payment's nearest double, and the enclosures are computed again with
/// twice the bits while they do not decide it. The payment at the start is that double divided by 1+rate
/// exactly, rounded once. 1+rate is never rounded: g - 1 is raised from the rate itself, so its relative
/// precision does not fall however small the rate is.
/// </summary>
/// <remarks>
/// Refused (null) where the readings of PMT, the evaluations of the identity Excel's observed payments leave,
/// can part in kind, or beyond the last digits through a double's range: where some quantity one of them
/// computes in doubles would pass a double's range, or fall below its normal numbers, which is where some give
/// an Error Value or a number of few digits and others the payment. <see cref="Quantities"/> lists those quantities, each a product of
/// powers of the rate, pv, fv, pv+fv, g, g-1, 1+rate and 1+rate*type, and each must lie between 2^-1021 and
/// 2^1022, a factor of 2 inside the normal numbers and of 4 below the largest, so that a reading's own
/// rounding, or a sum of two of them, cannot carry it out; and the growth must be 2^-52 or more
/// (<see cref="LeastGrowth"/>). Refused as well where 1536 bits do not decide the double: where the exact
/// payment is a tie between two doubles, or 0, and the enclosures cannot reach it exactly. Where the readings
/// part beyond the last digits by cancellation, the payment is given; ticket 13 holds that question.
/// </remarks>
internal static class ExactPayment
{
    private const int FirstBits = 96;
    private const int MostBits = 1536;
    private static readonly Dyadic Least = Dyadic.One.Scale(-1021);
    private static readonly Dyadic Most = Dyadic.One.Scale(1022);
    private static readonly Dyadic Half = Dyadic.One.Scale(-1);

    /// <summary>
    /// The least growth answered: a reading that takes g as 1 + (g-1), g-1 a double, holds a growth below 2^-54
    /// as 0, and its forms that divide by g give an Error Value where the others give the payment.
    /// </summary>
    private static readonly Dyadic LeastGrowth = Dyadic.One.Scale(-52);

    /// <summary>
    /// The exact payment rounded once, or null where PMT refuses. The caller has answered or refused a rate of 0,
    /// a rate of -1 or below, an <paramref name="periods"/> of 0 and one past 2^53.
    /// </summary>
    public static double? Of(double rate, double periods, double present, double future, bool atStart)
    {
        var r = Dyadic.Of(rate);
        var onePlusRate = Dyadic.One + r;
        var pv = Dyadic.Of(present);
        var fv = Dyadic.Of(future);
        var sum = pv + fv;
        if (!Fits(r) || !Fits(pv) || !Fits(fv) || !Fits(sum)) return null;
        // log2 of the growth, estimated in doubles: far outside the range, the enclosures would be vast.
        if (!(Math.Abs(periods * Math.Log2(1 + rate)) <= 1100)) return null;
        for (var bits = FirstBits; bits <= MostBits; bits *= 2)
        {
            var (growth, lessOne) = Growth(r, onePlusRate, periods, bits);
            if (bits == FirstBits && (!QuantitiesFit(r, pv, fv, sum, growth, lessOne, onePlusRate, atStart) || Dyadic.Compare(growth.Low, LeastGrowth) < 0)) return null;
            // pv g + fv, and pv (g-1) + (pv+fv), which is narrower where g is near 1: each holds it.
            var owed = Enclosure.Both(
                Enclosure.Add(Enclosure.Multiply(Enclosure.Exactly(pv), growth, bits), Enclosure.Exactly(fv), bits),
                Enclosure.Add(Enclosure.Multiply(Enclosure.Exactly(pv), lessOne, bits), Enclosure.Exactly(sum), bits));
            var atEnd = Enclosure.Divide(Enclosure.Multiply(Enclosure.Exactly(r.Negate()), owed, bits), lessOne, bits);
            var low = atEnd.Low.ToDouble();
            if (low != atEnd.High.ToDouble()) continue;
            if (low == 0) return 0;
            return atStart ? Dyadic.QuotientToDouble(Dyadic.Of(low), onePlusRate) : low;
        }
        return null;
    }

    /// <summary>
    /// g = (1+rate)^periods and g - 1. A whole number of periods raises g - 1 from the rate by
    /// (1+a)(1+b) - 1 = a + b + ab and (1+a)^2 - 1 = a (a + 2), and g by squaring 1+rate where g is below 1/2;
    /// a negative one is 1 over the power to its absolute value. Any other is e^(periods ln(1+rate)), and g - 1
    /// e^x - 1 by its own series.
    /// </summary>
    private static (Enclosure Growth, Enclosure LessOne) Growth(Dyadic rate, Dyadic onePlusRate, double periods, int bits)
    {
        var one = Enclosure.Exactly(Dyadic.One);
        var magnitude = Math.Abs(periods);
        if (magnitude != Math.Floor(magnitude))
        {
            var exponent = Enclosure.Multiply(Enclosure.Log1p(rate, bits), Enclosure.Exactly(periods), bits);
            var lessOne = Enclosure.Expm1(exponent, bits);
            return (Dyadic.Compare(lessOne.Low, Half.Negate()) >= 0 ? Enclosure.Add(one, lessOne, bits) : Enclosure.Exp(exponent, bits), lessOne);
        }
        var n = (long)magnitude;
        Enclosure? raised = null;
        var square = Enclosure.Exactly(rate);
        var two = Enclosure.Exactly(new Dyadic(2, 0));
        for (var k = n; ; )
        {
            if ((k & 1) != 0) raised = raised is { } sofar ? Enclosure.Add(Enclosure.Add(sofar, square, bits), Enclosure.Multiply(sofar, square, bits), bits) : square;
            k >>= 1;
            if (k == 0) break;
            square = Enclosure.Multiply(square, Enclosure.Add(square, two, bits), bits);
        }
        var less = raised!.Value;
        var power = Dyadic.Compare(less.Low, Half.Negate()) >= 0 ? Enclosure.Add(one, less, bits) : Raise(onePlusRate, n, bits);
        if (periods > 0) return (power, less);
        // (1+rate)^-n = 1 / (1+rate)^n, and less 1, -((1+rate)^n - 1) / (1+rate)^n.
        return (Enclosure.Divide(one, power, bits), Enclosure.Divide(less.Negate(), power, bits));
    }

    private static Enclosure Raise(Dyadic value, long exponent, int bits)
    {
        var result = Enclosure.Exactly(Dyadic.One);
        var square = Enclosure.Exactly(value);
        while (true)
        {
            if ((exponent & 1) != 0) result = Enclosure.Multiply(result, square, bits);
            exponent >>= 1;
            if (exponent == 0) return result;
            square = Enclosure.Multiply(square, square, bits);
        }
    }

    /// <summary>
    /// The quantities the readings of PMT compute in doubles, each a product of powers of r (the rate), pv, fv,
    /// s (pv+fv), g, e (g-1), b (1+rate) and c (1+rate*type: 1, or b at the start), numerator before the slash:
    /// those of every form of the identity <c>spikes/exsheet-financial-evaluation/Search.cs</c> tries (M1 to
    /// M41, LibreOffice's), and of the growth. A sum such as pv g + fv is bounded by its terms, each listed.
    /// </summary>
    private static readonly string[] Quantities =
    [
        "r", "pv", "fv", "s", "g", "/ g", "e", "b", "/ b", "c", "/ c",
        "e / r", "r / e", "e / g", "e / g r", "c e", "c e / r", "c e / g", "c e / g r", "c / r", "r / c",
        "b g", "b e", "r g",
        "pv g", "fv / g", "pv r", "fv r", "pv r g", "fv r / g",
        "pv g / e", "fv / e", "r g / e", "pv r g / e", "fv r / e",
        "s r", "s / e", "s r / e",
        "pv r g / c e", "fv r / c e",
    ];

    /// <summary>At the start, also the ways from the payment at the end to it: the payment at the end times the rate, and over 1+rate.</summary>
    private static readonly string[] AtStartQuantities = ["pv r r g / e", "fv r r / e", "pv r r g / b e", "fv r r / b e"];

    private static readonly string[] Factors = ["r", "pv", "fv", "s", "g", "e", "b", "c"];

    private static readonly int[][] AtEndPowers = [.. Quantities.Select(PowersOf)];

    private static readonly int[][] AtStartPowers = [.. Quantities.Concat(AtStartQuantities).Select(PowersOf)];

    /// <summary>A quantity as the power of each factor, in the order of <see cref="Factors"/>.</summary>
    private static int[] PowersOf(string quantity)
    {
        var powers = new int[Factors.Length];
        var sign = 1;
        foreach (var name in quantity.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (name == "/") sign = -1;
            else powers[Array.IndexOf(Factors, name)] += sign;
        }
        return powers;
    }

    private static bool Fits(Dyadic value) =>
        value.IsZero || (Dyadic.Compare(value.Abs(), Least) >= 0 && Dyadic.Compare(value.Abs(), Most) <= 0);

    private static bool QuantitiesFit(Dyadic r, Dyadic pv, Dyadic fv, Dyadic sum, Enclosure growth, Enclosure lessOne, Dyadic onePlusRate, bool atStart)
    {
        if (growth.ContainsZero || lessOne.ContainsZero) return false;
        (Dyadic, Dyadic)? Exact(Dyadic value) => value.IsZero ? null : (value.Abs(), value.Abs());
        var one = (Dyadic.One, Dyadic.One);
        (Dyadic Least, Dyadic Most)?[] magnitudes =
        [
            Exact(r), Exact(pv), Exact(fv), Exact(sum), growth.Magnitudes(), lessOne.Magnitudes(), Exact(onePlusRate),
            atStart ? Exact(onePlusRate) : one,
        ];
        foreach (var powers in atStart ? AtStartPowers : AtEndPowers)
        {
            Dyadic numeratorLeast = Dyadic.One, numeratorMost = Dyadic.One, denominatorLeast = Dyadic.One, denominatorMost = Dyadic.One;
            var zero = false;
            for (var f = 0; f < Factors.Length && !zero; f++)
            {
                var power = powers[f];
                if (power == 0) continue;
                if (magnitudes[f] is not { } m)
                {
                    zero = true; // exactly 0 in every reading
                    continue;
                }
                for (var i = 0; i < Math.Abs(power); i++)
                {
                    if (power > 0)
                    {
                        numeratorLeast *= m.Least;
                        numeratorMost *= m.Most;
                    }
                    else
                    {
                        denominatorLeast *= m.Least;
                        denominatorMost *= m.Most;
                    }
                }
            }
            if (zero) continue;
            // The quantity lies within [numeratorLeast / denominatorMost, numeratorMost / denominatorLeast].
            if (Dyadic.Compare(numeratorLeast, Least * denominatorMost) < 0) return false;
            if (Dyadic.Compare(numeratorMost, Most * denominatorLeast) > 0) return false;
        }
        return true;
    }
}
