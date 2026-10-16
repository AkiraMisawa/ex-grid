namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT's payment at a rate above -1 other than 0, or null where PMT refuses it (<c>Functions.Financial.cs</c>,
/// ADR-0047's exception for PMT). The payment at the end of each period is the exact payment over its
/// arguments' doubles, -rate (pv g + fv) / (g - 1), g = (1+rate)^nper, rounded once to a double: it is
/// computed in <see cref="Enclosure"/>s, which hold it between two bounds, and the double is given only where
/// both bounds round to it, the enclosures being computed again with twice the bits while they do not decide
/// it. 1+rate is never rounded: g - 1 is raised from the rate itself, so that its relative precision does not
/// fall however small the rate is. The payment at the start is the exact payment at the start,
/// -rate (pv g + fv) / ((g - 1)(1+rate)), rounded once, held between bounds as the payment at the end is
/// (2026-10-13, decided with the user: the payment at the start of each period is the exact payment rounded
/// once too). It is a unit in the last place from Excel's double at PMT-008, -147.95035898410146 where Excel
/// gave -147.95035898410148, which the financial area's earlier rule, the payment at the end's double over
/// 1+rate rounded again, happened to give; that rule lay a unit from the exact payment rounded once at 809 of
/// the 4,132 payments at the start other than 0 that <c>pmt_exact.py</c> draws (20%).
/// </summary>
/// <remarks>
/// Refused (null), the financial area's rules (2026-10-12): where the readings of PMT, the evaluations of the
/// identity Excel's observed payments left, could part in kind or beyond the last digits. Run 18a leaves no
/// reading (no evaluation searched gives PMT-048, 049, 051 or 052), and the rules stand as the engine's, but
/// where Excel's own answers lifted one (<see cref="MayPassAbove"/>, <see cref="MayFallBelow"/>).
/// <list type="bullet">
/// <item>Through a double's range: where some quantity one of them computes in doubles would pass a double's
/// range, or fall below its normal numbers, where some give an Error Value or a number of few digits and
/// others the payment. <see cref="Quantities"/> lists those quantities, each a product of powers of the
/// rate, pv, fv, pv+fv, g, g-1, 1+rate and 1+rate*type, and each must lie between 2^-1021 and 2^1022, a
/// factor of 2 inside the normal numbers and of 4 below the largest, so that a reading's own rounding, or a
/// sum of two of them, cannot carry it out; but those Excel was seen to carry past the range while it gave the
/// exact payment rounded once (run 18a, PMT-038 to PMT-041 and PMT-044). The growth below the normal numbers
/// is refused (PMT-042, where Excel gave <c>#NUM!</c>, which the exact payment does not give); a growth below
/// 2^-52 is not (PMT-045, where Excel gave the exact payment: no reading that takes the growth as 1 + (g-1)
/// and divides by it is Excel's).</item>
/// <item>Where a reading magnifies its own roundings more than 2^6 times (<see cref="Conditioned"/>): where
/// the terms it adds nearly cancel, as pv g and fv at =PMT(0.1,2,1000,-1210), where readings give 0, numbers
/// of the other sign, or a few significant digits; where a rounding of the exponent moves the payment far;
/// and, at the start, where LibreOffice's reading divides by a difference that can come out as 0, as at a
/// tiny nper, whose nper+1 rounds to 1, or where the payment at the end less its rate's share cancels.</item>
/// <item>Where 1536 bits do not decide the double, as at a tie between two doubles that the enclosures cannot
/// reach exactly.</item>
/// </list>
/// </remarks>
internal static class ExactPayment
{
    private const int FirstBits = 96;
    private const int MostBits = 1536;
    private static readonly Dyadic Least = Dyadic.One.Scale(-1021);
    private static readonly Dyadic Most = Dyadic.One.Scale(1022);
    private static readonly Dyadic Half = Dyadic.One.Scale(-1);

    /// <summary>
    /// The exact payment at the end, or at the start, rounded once, or null
    /// where PMT refuses (the remarks above). The caller has answered or refused a rate of 0, a rate of -1 or
    /// below, an <paramref name="periods"/> of 0 and one past 2^53.
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
            if (bits == FirstBits && !QuantitiesFit(r, pv, fv, sum, growth, lessOne, onePlusRate, atStart)) return null;
            // pv g + fv, and pv (g-1) + (pv+fv), which is narrower where g is near 1: each holds it.
            var owed = Enclosure.Both(
                Enclosure.Add(Enclosure.Multiply(Enclosure.Exactly(pv), growth, bits), Enclosure.Exactly(fv), bits),
                Enclosure.Add(Enclosure.Multiply(Enclosure.Exactly(pv), lessOne, bits), Enclosure.Exactly(sum), bits));
            if (bits == FirstBits && !Conditioned(r, onePlusRate, periods, pv, fv, sum, growth, lessOne, owed, atStart, bits)) return null;
            var atEnd = Enclosure.Divide(Enclosure.Multiply(Enclosure.Exactly(r.Negate()), owed, bits), lessOne, bits);
            var payment = atStart ? Enclosure.Divide(atEnd, Enclosure.Exactly(onePlusRate), bits) : atEnd;
            var low = payment.Low.ToDouble();
            if (low != payment.High.ToDouble()) continue;
            return low == 0 ? 0 : low;
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

    /// <summary>
    /// The quantities Excel was seen to carry above a double's range while it gave the exact payment rounded once
    /// (run 18a: at =PMT(1,1100,1000), =PMT(1,1020,1000), =PMT(0.5,1740,1000) and =PMT(1E10,30,1), PMT-038 to
    /// PMT-041, each of these lies above 2^1022 at one of them): those that grow with the growth and hold no fv.
    /// They no longer refuse above the range; elsewhere than at those four, by generalisation.
    /// </summary>
    private static readonly HashSet<string> MayPassAbove = ["g", "e", "e / r", "c e", "c e / r", "b g", "b e", "r g", "pv g", "pv r g"];

    /// <summary>
    /// The quantities Excel was seen to carry below a double's normal numbers while it gave the exact payment
    /// rounded once (run 18a): those the growth shrinks with no fv among them, at =PMT(1,1100,1000) (PMT-038), and
    /// pv's and pv+fv's products with the rate, at =PMT(-1.64435447903749E-307,283.5,1E-05,0) (PMT-044). They no
    /// longer refuse below the range; elsewhere than at those two, by generalisation. pv r g lay below it at
    /// =PMT(-0.99,160,1000,500) too (PMT-042), where Excel gave <c>#NUM!</c>; the growth's own bound refuses that.
    /// </summary>
    private static readonly HashSet<string> MayFallBelow = ["/ g", "r / e", "s / e", "s r / e", "pv r", "pv r g", "s r"];

    private static readonly string[] Factors = ["r", "pv", "fv", "s", "g", "e", "b", "c"];

    private static readonly Bound[] AtEndBounds = [.. Quantities.Select(BoundOf)];

    private static readonly Bound[] AtStartBounds = [.. Quantities.Concat(AtStartQuantities).Select(BoundOf)];

    /// <summary>A quantity as the power of each factor, and which sides of the range it must lie within.</summary>
    private readonly record struct Bound(int[] Powers, bool Above, bool Below);

    private static Bound BoundOf(string quantity) => new(PowersOf(quantity), !MayPassAbove.Contains(quantity), !MayFallBelow.Contains(quantity));

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

    /// <summary>
    /// Whether no reading of PMT magnifies its own roundings more than 2^<see cref="MostMagnification"/> times
    /// (the financial area's bound, 2026-10-12, the fourth round). Each factor below is a sum of the magnitudes
    /// of the terms some reading adds, over the magnitude of what they add up to, or how far a rounding of the
    /// exponent moves the payment; where one is large the readings' results part far beyond their last digits
    /// (to 0, the other sign, or a few significant digits, as at =PMT(0.1,2,1000,-1210), where pv g and fv
    /// cancel), and PMT refuses. g = (1+rate)^nper, e = g - 1, N = pv g + fv, s = pv + fv, b = 1 + rate,
    /// x = nper ln(1+rate):
    /// <list type="bullet">
    /// <item>(|pv| (1 + g) + |fv|) / |N|: the sum pv g + fv, and pv (g-1) + s and pv r + s r/e, which
    /// add to the same, and a growth taken as 1 + (g-1), which holds |pv| in place of |pv g| (as at
    /// =PMT(-0.5,36.25,-150,0,0), where g is 1.2E-11);</item>
    /// <item>|x| |s| g / (|e| |N|): the exponent, which the readings through ln(1+rate) round, moves g and e
    /// together (as at =PMT(1,1000,0,1000), where x is 693);</item>
    /// <item>at the start, |b| + |r|: the payment at the end less its rate's share, p - p r/b;</item>
    /// <item>at the start, (|nper+1| |ln b| b g + |r|) / (b |e|) and |x| |pv| g / |N|: LibreOffice's ScGetPMT,
    /// which divides by e^((nper+1) ln b) - 1 - rate, a difference that comes out as 0 at a tiny nper,
    /// where nper+1 rounds to 1 (=PMT(0.1,1E-17,1000,0,1)), and its own power of pv.</item>
    /// </list>
    /// With no pv and no fv every reading adds only zeros and the payment is 0, and only LibreOffice's divisor is
    /// weighed, which gives 0/0 where it comes out as 0 (=PMT(0.1,1E-17,0,0,1)).
    /// No observed payment has a factor above 2^5 (PMT-020, at 32.4). Where every factor is within the bound,
    /// every reading the spike runs lies within 93 units in the last place of the engine's payment over
    /// <c>pmt_exact.py</c>'s 20,000 inputs (measured again with the payment at the start rounded once,
    /// 2026-10-13), and within 48 over the grids of <c>Search.cs --refusals</c> (<c>--pmt-readings</c>
    /// requires 128, twice the bound). The bound is kept (2026-10-13, decided with the user). Run 18a read
    /// Excel's answer where it refuses: the exact payment rounded once at =PMT(0.1,1E-17,1000,-1000) (PMT-050),
    /// one or two units in the last place from it at PMT-047, 051 and 053, and another evaluation's at PMT-048
    /// (its 10th significant digit), PMT-049 (the other sign) and PMT-052 (0); the corpus keeps each beside the
    /// refusal, and the bound is kept and the eight stay refused (2026-10-16, decided with the user).
    /// </summary>
    private static bool Conditioned(Dyadic r, Dyadic onePlusRate, double periods, Dyadic pv, Dyadic fv, Dyadic sum, Enclosure growth, Enclosure lessOne, Enclosure owed, bool atStart, int bits)
    {
        if (lessOne.ContainsZero || growth.ContainsZero) return false;
        // With no pv and no fv every reading adds only zeros, and its payment is 0; but LibreOffice's divisor at
        // the start, which can come out as 0 and give 0/0.
        var owesNothing = pv.IsZero && fv.IsZero;
        if (!owesNothing && owed.ContainsZero) return false;
        var most = growth.Magnitudes().Most;
        var lessOneLeast = lessOne.Magnitudes().Least;
        var log = Enclosure.Log1p(r, bits);
        var logMost = Dyadic.Compare(log.High.Abs(), log.Low.Abs()) >= 0 ? log.High.Abs() : log.Low.Abs();
        var exponentMost = Dyadic.Of(Math.Abs(periods)) * logMost;
        var limit = Dyadic.One.Scale(MostMagnification);
        bool Within(Dyadic terms, Dyadic result) => Dyadic.Compare(terms, limit * result) <= 0;
        if (!owesNothing)
        {
            var owedLeast = owed.Magnitudes().Least;
            if (!Within((pv.Abs() * (Dyadic.One + most)) + fv.Abs(), owedLeast)) return false;
            if (!Within(exponentMost * sum.Abs() * most, lessOneLeast * owedLeast)) return false;
            if (atStart && !Within(onePlusRate.Abs() + r.Abs(), Dyadic.One)) return false;
            if (atStart && !Within(exponentMost * pv.Abs() * most, owedLeast)) return false;
        }
        var periodsPlusOne = (Dyadic.Of(periods) + Dyadic.One).Abs();
        return !atStart || Within((periodsPlusOne * logMost * onePlusRate * most) + r.Abs(), onePlusRate * lessOneLeast);
    }

    /// <summary>The bound of <see cref="Conditioned"/>, as a power of 2.</summary>
    private const int MostMagnification = 6;

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
        foreach (var (powers, above, below) in atStart ? AtStartBounds : AtEndBounds)
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
            if (below && Dyadic.Compare(numeratorLeast, Least * denominatorMost) < 0) return false;
            if (above && Dyadic.Compare(numeratorMost, Most * denominatorLeast) > 0) return false;
        }
        return true;
    }
}
