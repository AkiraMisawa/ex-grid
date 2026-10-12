using ExSheet.Engine.Formulas;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047: PV, FV and XNPV raise 1+rate to a number that is not whole as e^(n ln(1+rate)) with the
/// exponential and the logarithm correctly rounded, which gives Excel's XNPV-013 where the platform's
/// <see cref="Math.Exp"/> does not (the second area pass of 2026-10-12). <see cref="CorrectlyRounded"/>
/// computes each in double-double and falls back to 256-bit fixed point near a rounding boundary; these
/// check that the two paths give the same double, which the fixed point rounds correctly. PMT raises 1+rate,
/// carried exactly, by the double-double functions unrounded (ADR-0047's exception for PMT).
/// </summary>
public class CorrectlyRoundedTests
{
    [Fact] // ADR-0047: the double-double path gives the fixed point's double, over every range PV, FV and XNPV reach and beyond
    public void Exp_is_the_exact_exponential_rounded_once()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 4000; i++)
        {
            var x = (i % 4) switch
            {
                0 => (random.NextDouble() * 1400) - 700,
                1 => (random.NextDouble() - 0.5) * Math.ScaleB(1, -random.Next(1, 60)),
                2 => (random.NextDouble() * 4) - 2,
                _ => (random.NextDouble() * 40) - 20,
            };
            Assert.Equal(CorrectlyRounded.ExpExact(x), CorrectlyRounded.Exp(x));
        }
    }

    [Fact] // ADR-0047: as Exp, for the logarithm, normal and subnormal arguments and those near 1
    public void Log_is_the_exact_logarithm_rounded_once()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 4000; i++)
        {
            var x = (i % 4) switch
            {
                0 => Math.ScaleB(1 + random.NextDouble(), random.Next(-1020, 1020)),
                1 => 1 + ((random.NextDouble() - 0.5) * Math.ScaleB(1, -random.Next(1, 50))),
                2 => (random.NextDouble() * 3) + 1E-9,
                _ => Math.ScaleB(random.NextDouble() + 1E-3, -1060),
            };
            Assert.Equal(CorrectlyRounded.LogExact(x), CorrectlyRounded.Log(x));
        }
    }

    [Theory] // ADR-0047: values the exact functions are known by, and the edges of the double's range
    [InlineData(0, 1)]
    [InlineData(1, 2.718281828459045)]
    [InlineData(-1, 0.36787944117144233)]
    [InlineData(709.782712893384, 1.7976931348622732E+308)]
    [InlineData(710, double.PositiveInfinity)]
    [InlineData(-745.1332191019411, 4.9406564584124654E-324)]
    [InlineData(-745.1332191019412, 0)]
    [InlineData(-746, 0)]
    public void Exp_at_known_values(double x, double expected) =>
        Assert.Equal(expected, CorrectlyRounded.Exp(x));

    [Theory] // ADR-0047: as Exp_at_known_values, for the logarithm
    [InlineData(1, 0)]
    [InlineData(2, 0.6931471805599453)]
    [InlineData(10, 2.302585092994046)]
    [InlineData(4.9406564584124654E-324, -744.4400719213812)]
    [InlineData(1.7976931348623157E+308, 709.782712893384)]
    public void Log_at_known_values(double x, double expected) =>
        Assert.Equal(expected, CorrectlyRounded.Log(x));

    [Fact] // ADR-0047, the exception for PMT: the double-double exponential and logarithm PMT raises 1+rate by lie within a rounding of the exact ones, so they round to the fixed point's double
    public void The_double_double_exponential_and_logarithm_round_to_the_exact_ones()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 4000; i++)
        {
            var x = i % 2 == 0 ? (random.NextDouble() * 1400) - 700 : (random.NextDouble() - 0.5) * Math.ScaleB(1, -random.Next(1, 60));
            Assert.Equal(CorrectlyRounded.ExpExact(x), CorrectlyRounded.ExpWide(x).High);
            var y = i % 2 == 0 ? Math.ScaleB(1 + random.NextDouble(), random.Next(-1020, 1020)) : 1 + ((random.NextDouble() - 0.5) * Math.ScaleB(1, -random.Next(1, 50)));
            Assert.Equal(CorrectlyRounded.LogExact(y), CorrectlyRounded.LogWide(y).High);
        }
    }

    [Theory] // ADR-0047, the exception for PMT: ln(1+rate) with 1+rate carried exactly, where the double 1+rate loses the rate's low bits; Python's decimal at 200 digits, rounded once
    [InlineData(1E-10, 9.999999999500001E-11)]
    [InlineData(0.08 / 12, 0.006644542718668579)]
    [InlineData(-0.5 + 9.094947017729282E-13, -0.6931471805581263)]
    [InlineData(3.0, 1.3862943611198906)]
    public void The_double_double_logarithm_of_one_plus_rate_carried_exactly(double rate, double expected) =>
        Assert.Equal(expected, CorrectlyRounded.LogWide(Wide.Sum(1, rate)).High);

    [Fact] // ADR-0047, the exception for PMT: e^(10.5 ln(1+rate)) at PMT-011's rate, against Python's decimal at 200 digits, rounded once
    public void The_double_double_power_of_one_plus_rate_carried_exactly() =>
        Assert.Equal(1.0722590649804624, CorrectlyRounded.ExpWide(CorrectlyRounded.LogWide(Wide.Sum(1, 0.08 / 12)) * 10.5).High);

    [Fact] // ADR-0047: what has no logarithm or exponential is NaN or infinite, as Math's
    public void Outside_the_domain_is_as_Maths()
    {
        Assert.True(double.IsNaN(CorrectlyRounded.Log(-1)));
        Assert.Equal(double.NegativeInfinity, CorrectlyRounded.Log(0));
        Assert.Equal(double.PositiveInfinity, CorrectlyRounded.Log(double.PositiveInfinity));
        Assert.True(double.IsNaN(CorrectlyRounded.Exp(double.NaN)));
        Assert.Equal(0, CorrectlyRounded.Exp(double.NegativeInfinity));
        Assert.Equal(double.PositiveInfinity, CorrectlyRounded.Exp(double.PositiveInfinity));
    }
}
