using System.Globalization;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047: SIN, COS, TAN, COT, CSC and SEC give Excel's doubles. Excel reduces an angle by pi rounded to
/// the x87's 64-bit significand (<c>FLDPI</c>, 0xC90FDAA22168C235 × 2^-62), not by the true pi and not by
/// <c>PI()</c>'s double, so <c>SIN(PI())</c> is the difference between those two, 1.22514845490862E-16,
/// where the double's own sine is 1.2246467991473532E-16. The seventeenth Windows run observed it, with
/// <c>COS(PI()/2)</c> and <c>TAN(PI()/2)</c>. SIN, COS and TAN are the doubles nearest the exact values of
/// the reduced angle; COT, CSC and SEC are 1 over them, divided as doubles. The experiment that found the
/// computation, and that gives the known answers below from 120-digit arithmetic, is
/// <c>spikes/exsheet-trig-reduction/compare.py</c> (its <c>--known</c> table).
/// </summary>
public class TrigonometryReductionTests
{
    private static string Reads(Sheet sheet, string formula) => sheet.Evaluate(formula) switch
    {
        { Kind: ValueKind.Number } v => v.Number.ToString("R", CultureInfo.InvariantCulture),
        { Kind: ValueKind.Error } v => v.Error.ToText(),
        var v => v.ToString()!,
    };

    private static string R(double number) => number.ToString("R", CultureInfo.InvariantCulture);

    [Theory] // ADR-0047: the seventeenth Windows run's SIN-004, COS-004 and TAN-004
    [InlineData("=SIN(PI())", 1.22514845490862e-16)]
    [InlineData("=COS(PI()/2)", 6.1257422745431e-17)]
    [InlineData("=TAN(PI()/2)", 16324552277619072)]
    public void ADR0047_An_angle_is_reduced_by_the_x87_pi_as_Excel_reduces_it(string formula, double excel) =>
        Assert.Equal(R(excel), Reads(NewSheet(), formula));

    [Theory] // ADR-0047: every other full double the runs observed for the six, to the last bit
    [InlineData("=SIN(PI()/2)", 1)]
    [InlineData("=SIN(30*PI()/180)", 0.49999999999999994)]
    [InlineData("=SIN(RADIANS(30))", 0.49999999999999994)]
    [InlineData("=SIN(2^27-1)", -0.9559933287726706)]
    [InlineData("=COS(1.047)", 0.5001710745970701)]
    [InlineData("=COS(60*PI()/180)", 0.5000000000000001)]
    [InlineData("=COS(RADIANS(60))", 0.5000000000000001)]
    [InlineData("=TAN(0.785)", 0.9992039901050427)]
    [InlineData("=TAN(45*PI()/180)", 0.9999999999999999)]
    [InlineData("=TAN(RADIANS(45))", 0.9999999999999999)]
    [InlineData("=COT(30)", -0.15611995216165922)]
    [InlineData("=COT(45)", 0.6173696237835551)]
    [InlineData("=CSC(15)", 1.5377805615408537)]
    [InlineData("=SEC(45)", 1.9035944074044246)]
    [InlineData("=SEC(30)", 6.482921234962678)]
    [InlineData("=SEC(0)", 1)]
    public void ADR0047_Every_observed_double_is_Excels_to_the_last_bit(string formula, double excel) =>
        Assert.Equal(R(excel), Reads(NewSheet(), formula));

    /// <summary>
    /// Each argument with SIN, COS, TAN, COT, CSC and SEC of it, from <c>compare.py --known</c>: SIN, COS and
    /// TAN the doubles nearest the exact values of the angle reduced by the x87 pi; COT, CSC and SEC 1 over them.
    /// </summary>
    public static TheoryData<string, double, double, double, double, double, double> KnownAnswers => new()
    {
        { "PI()", 1.22514845490862e-16, -1.0, -1.22514845490862e-16, -8162276138809536.0, 8162276138809536.0, -1.0 },
        { "PI()/2", 1.0, 6.1257422745431e-17, 1.6324552277619072e+16, 6.1257422745431e-17, 1.0, 1.6324552277619072e+16 },
        { "PI()/4", 0.7071067811865475, 0.7071067811865476, 0.9999999999999999, 1.0000000000000002, 1.4142135623730951, 1.414213562373095 },
        { "2*PI()", -2.45029690981724e-16, 1.0, -2.45029690981724e-16, -4081138069404768.0, -4081138069404768.0, 1.0 },
        { "3*PI()/2", -1.0, -1.83772268236293e-16, 5441517425873024.0, 1.83772268236293e-16, -1.0, -5441517425873024.0 },
        { "-PI()", -1.22514845490862e-16, -1.0, 1.22514845490862e-16, 8162276138809536.0, -8162276138809536.0, -1.0 },
        { "-PI()/2", -1.0, 6.1257422745431e-17, -1.6324552277619072e+16, -6.1257422745431e-17, -1.0, 1.6324552277619072e+16 },
        { "0.785", 0.706825181105366, 0.7073882691671998, 0.9992039901050427, 1.000796644031489, 1.4147769869151432, 1.4136508104344008 },
        { "1.047", 0.8659266112878228, 0.5001710745970701, 1.7312608730630805, 0.5776137008345384, 1.1548322767362245, 1.9993159356637809 },
        { "1", 0.8414709848078965, 0.5403023058681398, 1.5574077246549023, 0.6420926159343306, 1.1883951057781212, 1.8508157176809255 },
        { "-1", -0.8414709848078965, 0.5403023058681398, -1.5574077246549023, -0.6420926159343306, -1.1883951057781212, 1.8508157176809255 },
        { "0.5", 0.479425538604203, 0.8775825618903728, 0.5463024898437905, 1.830487721712452, 2.085829642933488, 1.139493927324549 },
        { "-2.5", -0.5984721441039565, -0.8011436155469337, 0.7470222972386603, 1.3386481283041514, -1.6709215455586797, -1.2482156514688179 },
        { "9", 0.4121184852417566, -0.9111302618846769, -0.45231565944180985, -2.210845410999195, 2.426486643551989, -1.097537906304962 },
        { "30", -0.9880316240928618, 0.15425144988758405, -6.405331196646276, -0.15611995216165922, -1.012113353070178, 6.482921234962678 },
        { "45", 0.8509035245341184, 0.5253219888177297, 1.6197751905438615, 0.6173696237835551, 1.175221363135749, 1.9035944074044246 },
        { "1E-8", 1e-08, 1.0, 1e-08, 100000000.0, 100000000.0, 1.0 },
        { "1E-300", 1e-300, 1.0, 1e-300, 9.999999999999999e+299, 9.999999999999999e+299, 1.0 },
        { "3.14159265358979", 3.2311393144413003e-15, -1.0, -3.2311393144413003e-15, -309488357722796.3, 309488357722796.3, -1.0 },
        { "1.5707963267949", 1.0, -3.49145625605507e-15, -286413440886090.6, -3.4914562560550695e-15, 1.0, -286413440886090.6 },
        { "7.85398163397448", 1.0, 2.9708223728275307e-15, 336607132471616.94, 2.9708223728275307e-15, 1.0, 336607132471616.94 },
        { "355", -3.0144353359482782e-05, -0.999999999545659, 3.0144353373178597e-05, 33173.70877458481, -33173.70878965699, -1.000000000454341 },
        { "-710", -6.028870669157393e-05, 0.999999998182636, -6.028870680114046e-05, -16586.854372220227, -16586.85440236458, 1.000000001817364 },
        { "103993", -1.912933578008433e-05, 0.9999999998170342, -1.9129335783584344e-05, -52275.73039196376, -52275.73040152843, 1.0000000001829659 },
        { "12345678.9", -0.8973068124813468, 0.4414073903714856, -2.0328314207113283, -0.4919247065012799, -1.1144460134373357, 2.2654808728018954 },
        { "100000000", 0.9316390271103062, -0.3633850893542029, -2.56377890674047, -0.3900492344994667, 1.0733771030414334, -2.751901575755819 },
        { "-100000000", -0.9316390271103062, -0.3633850893542029, 2.56377890674047, 0.3900492344994667, -1.0733771030414334, -2.751901575755819 },
        { "134217727", -0.9559933287726706, -0.2933884035577216, 3.258456425611884, 0.3068937771086555, -1.0460324041003783, -3.408451008539125 },
        { "-134217727", 0.9559933287726706, -0.2933884035577216, -3.258456425611884, -0.3068937771086555, 1.0460324041003783, -3.408451008539125 },
        { "134217727.5", -0.9796209680103136, 0.20085506972575065, -4.877252883623486, -0.20503345302388015, -1.0208029765135365, 4.978714260812083 },
    };

    [Theory, MemberData(nameof(KnownAnswers))] // ADR-0047: the double nearest the exact value, on every scale below 2^27
    public void ADR0047_Each_function_is_the_double_nearest_the_exact_value_of_the_reduced_angle(
        string angle, double sin, double cos, double tan, double cot, double csc, double sec)
    {
        var sheet = NewSheet();
        Assert.Equal(
            new[] { R(sin), R(cos), R(tan), R(cot), R(csc), R(sec) },
            new[] { "SIN", "COS", "TAN", "COT", "CSC", "SEC" }.Select(f => Reads(sheet, $"={f}({angle})")).ToArray());
    }

    [Theory] // ADR-0047: Microsoft documents 2^27 for COT, CSC and SEC; the run observed it for all six
    [InlineData("SIN")]
    [InlineData("COS")]
    [InlineData("TAN")]
    [InlineData("COT")]
    [InlineData("CSC")]
    [InlineData("SEC")]
    public void ADR0047_The_six_refuse_2_to_the_27_and_answer_just_below_it(string function)
    {
        var sheet = NewSheet();
        Assert.Equal("#NUM!", Reads(sheet, $"={function}(2^27)"));
        Assert.Equal("#NUM!", Reads(sheet, $"={function}(-2^27)"));
        Assert.Equal(ValueKind.Number, sheet.Evaluate($"={function}(2^27-1)").Kind);
        Assert.Equal(ValueKind.Number, sheet.Evaluate($"={function}(-(2^27-1))").Kind);
    }

    [Theory] // ADR-0047: the poles at 0 stay #DIV/0!; SEC(0) and the rest of 0 are exact
    [InlineData("=COT(0)", "#DIV/0!")]
    [InlineData("=CSC(0)", "#DIV/0!")]
    [InlineData("=SIN(0)", "0")]
    [InlineData("=COS(0)", "1")]
    [InlineData("=TAN(0)", "0")]
    [InlineData("=SEC(0)", "1")]
    public void ADR0047_Zero_is_exact_and_the_poles_at_it_are_DIV0(string formula, string expected) =>
        Assert.Equal(expected, Reads(NewSheet(), formula));

    [Theory] // ADR-0047: Microsoft documents 2^27 for COTH, CSCH and SECH, but Excel answers past it (COTH-002, CSCH-002, SECH-003)
    [InlineData("=COTH(2^27)", "1")]
    [InlineData("=COTH(-2^27)", "-1")]
    [InlineData("=CSCH(2^27)", "0")]
    [InlineData("=CSCH(-2^27)", "0")]
    [InlineData("=SECH(2^27)", "0")]
    [InlineData("=SECH(-2^27)", "0")]
    [InlineData("=COTH(1E300)", "1")]
    [InlineData("=CSCH(1E300)", "0")]
    [InlineData("=SECH(1E300)", "0")]
    [InlineData("=COTH(0)", "#DIV/0!")]
    [InlineData("=CSCH(0)", "#DIV/0!")]
    [InlineData("=SECH(0)", "1")]
    public void ADR0047_COTH_CSCH_and_SECH_answer_past_2_to_the_27_as_Excel_does(string formula, string expected) =>
        Assert.Equal(expected, Reads(NewSheet(), formula));
}
