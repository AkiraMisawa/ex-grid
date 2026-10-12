using System.Numerics;
using ExSheet.Engine.Formulas;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047, the exception for PMT: PMT's exact payment is computed in <see cref="Enclosure"/>s, which hold a
/// number between two dyadic bounds, and given only where both bounds round to the same double. These check
/// that each operation rounds its bounds outward, and that the functions' enclosures hold the values
/// <see cref="CorrectlyRounded"/>'s 256-bit fixed point gives, however small the argument.
/// </summary>
public class EnclosureTests
{
    [Fact] // ADR-0047: Down rounds toward -infinity and Up toward +infinity, on either side of 0
    public void Down_and_Up_round_outward()
    {
        var seven = new Dyadic(7, 0); // 111 in binary
        Assert.Equal(0, Dyadic.Compare(new Dyadic(6, 0), seven.Down(2)));
        Assert.Equal(0, Dyadic.Compare(new Dyadic(8, 0), seven.Up(2)));
        Assert.Equal(0, Dyadic.Compare(new Dyadic(-8, 0), seven.Negate().Down(2)));
        Assert.Equal(0, Dyadic.Compare(new Dyadic(-6, 0), seven.Negate().Up(2)));
        Assert.Equal(0, Dyadic.Compare(seven, seven.Down(3)));
    }

    [Fact] // ADR-0047: a quotient rounded down lies below the exact one and rounded up above it, by at most a unit of its bits
    public void A_quotient_is_rounded_in_the_direction_asked()
    {
        foreach (var (a, b) in new[] { (1, 3), (-1, 3), (1, -3), (-2, -7), (10, 4) })
        {
            var low = Dyadic.Quotient(new Dyadic(a, 0), new Dyadic(b, 0), 60, up: false);
            var high = Dyadic.Quotient(new Dyadic(a, 0), new Dyadic(b, 0), 60, up: true);
            // low * b <= a <= high * b where b > 0, reversed where it is below 0
            var sign = Math.Sign(b);
            Assert.True(sign * Dyadic.Compare(low * new Dyadic(b, 0), new Dyadic(a, 0)) <= 0, $"{a}/{b} low");
            Assert.True(sign * Dyadic.Compare(high * new Dyadic(b, 0), new Dyadic(a, 0)) >= 0, $"{a}/{b} high");
            Assert.True(Dyadic.Compare(high - low, Dyadic.One.Scale(-58)) <= 0);
        }
        Assert.Equal(Dyadic.Quotient(new Dyadic(10, 0), new Dyadic(4, 0), 60, up: false), Dyadic.Quotient(new Dyadic(10, 0), new Dyadic(4, 0), 60, up: true));
    }

    [Fact] // ADR-0047: the payment at the start is the payment at the end over 1+rate, rounded once, ties to even
    public void A_quotient_to_a_double_is_rounded_once_ties_to_even()
    {
        var two53 = BigInteger.One << 53;
        Assert.Equal(Math.ScaleB(1, 53), Dyadic.QuotientToDouble(new Dyadic(two53 + 1, 0), Dyadic.One));
        Assert.Equal(Math.ScaleB(1, 53) + 4, Dyadic.QuotientToDouble(new Dyadic(two53 + 3, 0), Dyadic.One));
        Assert.Equal(1.0 / 3, Dyadic.QuotientToDouble(Dyadic.One, new Dyadic(3, 0)));
        Assert.Equal(-147.95035898410148, Dyadic.QuotientToDouble(Dyadic.Of(-162.74539488251162), Dyadic.One + Dyadic.Of(0.1)));
    }

    [Fact] // ADR-0047: ln 2 lies within its enclosure, at any number of bits
    public void Ln2_is_enclosed()
    {
        foreach (var bits in new[] { 60, 96, 200, 1000 })
        {
            var ln2 = Enclosure.Ln2(bits);
            Assert.Equal(0.6931471805599453, ln2.Low.ToDouble());
            Assert.Equal(0.6931471805599453, ln2.High.ToDouble());
            Assert.True(Dyadic.Compare(ln2.High - ln2.Low, Dyadic.One.Scale(-(bits - 2))) <= 0);
        }
    }

    [Fact] // ADR-0047: ln(1+d) for d carried exactly lies between the bounds, and keeps its relative precision however small d is
    public void Log1p_encloses_the_correctly_rounded_logarithm()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 2000; i++)
        {
            // y - 1 exact: y in [1/2, 2] (Sterbenz), or y = 2^k
            var y = i % 3 == 0 ? Math.ScaleB(1, random.Next(-1000, 1000)) : 0.5 + (random.NextDouble() * 1.5);
            var log = Enclosure.Log1p(Dyadic.Of(y) - Dyadic.One, 96);
            Within(CorrectlyRounded.LogExact(y), log);
        }
        foreach (var tiny in new[] { 1e-300, -1e-300, 2.2250738585072014E-308, 1e-20, -3e-17 })
        {
            var log = Enclosure.Log1p(Dyadic.Of(tiny), 96);
            Assert.Equal(tiny, log.Low.ToDouble());
            Assert.Equal(tiny, log.High.ToDouble());
        }
    }

    [Fact] // ADR-0047: e^y and e^y - 1 lie between the bounds, from the range's edges to below the normal numbers
    public void Exp_and_Expm1_enclose_the_correctly_rounded_exponential()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 2000; i++)
        {
            var x = (random.NextDouble() * 1400) - 700;
            Within(CorrectlyRounded.ExpExact(x), Enclosure.Exp(Enclosure.Exactly(x), 96));
        }
        Assert.Equal(1.7182818284590453, Enclosure.Expm1(Enclosure.Exactly(1.0), 96).Low.ToDouble());
        Assert.Equal(1.7182818284590453, Enclosure.Expm1(Enclosure.Exactly(1.0), 96).High.ToDouble());
        foreach (var tiny in new[] { 1e-300, -1e-300, 2.2250738585072014E-308, 1e-20, -3e-17 })
        {
            var expm1 = Enclosure.Expm1(Enclosure.Exactly(tiny), 96);
            Assert.Equal(tiny, expm1.Low.ToDouble());
            Assert.Equal(tiny, expm1.High.ToDouble());
        }
    }

    /// <summary>The correctly rounded double lies between the rounded bounds, and the bounds are within 2^-80 of each other relatively.</summary>
    private static void Within(double correctlyRounded, Enclosure enclosure)
    {
        Assert.True(enclosure.Low.ToDouble() <= correctlyRounded && correctlyRounded <= enclosure.High.ToDouble(), $"{correctlyRounded} outside [{enclosure.Low.ToDouble()}, {enclosure.High.ToDouble()}]");
        var width = enclosure.High - enclosure.Low;
        Assert.True(width.IsZero || width.Top <= enclosure.High.Abs().Top - 80, $"{correctlyRounded}: the bounds are {width.Top} against {enclosure.High.Top}");
    }
}
