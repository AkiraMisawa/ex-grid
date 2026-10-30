namespace ExSheet.Engine.Formulas;

/// <summary>
/// LOG and the trigonometric and hyperbolic functions, each to Microsoft's documentation of it
/// (ticket 07) and to what the Windows runs observed of Excel. Angles are in radians.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The size of argument, 2^27, at and above which SIN, COS, TAN, COT, CSC and SEC are <c>#NUM!</c>.
    /// Microsoft documents it for COT, CSC and SEC; the seventeenth Windows run observed it for all six
    /// (SIN-005, COS-005, TAN-005, COT-004, CSC-002, SEC-003). COTH, CSCH and SECH, for which
    /// Microsoft documents it too, answer past it in Excel (COTH-002, CSCH-002, SECH-003), so they do
    /// here.
    /// </summary>
    private const double TrigonometricLimit = 134_217_728;

    // ---- LOG ------------------------------------------------------------------------------------

    /// <summary>
    /// LOG: to <c>base</c>, 10 when left out. A number or base not above 0 is <c>#NUM!</c>; base 1,
    /// whose logarithm is 0, is <c>#DIV/0!</c>. Base 10 goes through LOG10's own logarithm, so
    /// <c>LOG(1000)</c> is 3 as LOG10's is.
    /// </summary>
    private static Operand Log(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        var @base = 10.0;
        if (call.Has(1) && !TryNumber(call, 1, out @base, out failure)) return failure;
        if (number <= 0 || @base <= 0) return Operand.Of(ErrorValue.Num);
        if (@base == 1) return Operand.Of(ErrorValue.Div0);
        var result = @base == 10 ? PortableMath.Log10(number) : PortableMath.Log(number) / PortableMath.Log(@base);
        return Operand.Of(Evaluator.Number(result));
    }

    // ---- SIN, COS, TAN, COT, CSC, SEC ------------------------------------------------------------

    /// <summary>SIN: the sine of the angle reduced as Excel reduces it (<see cref="Reduced"/>).</summary>
    private static Operand Sin(FunctionCall call) => Angle(call, n => Reduced(n).Sine.Value);

    /// <summary>COS: the cosine of the angle reduced as Excel reduces it (<see cref="Reduced"/>).</summary>
    private static Operand Cos(FunctionCall call) => Angle(call, n => Reduced(n).Cosine.Value);

    /// <summary>TAN: the sine over the cosine of the reduced angle, rounded once (<see cref="Reduced"/>).</summary>
    private static Operand Tan(FunctionCall call) => Angle(call, Tangent);

    /// <summary>COT: 1/TAN; <c>COT(0)</c> is <c>#DIV/0!</c>, as documented.</summary>
    private static Operand Cot(FunctionCall call) => Angle(call, n => 1 / Tangent(n), zeroIsDiv0: true);

    /// <summary>CSC: 1/SIN, divided as doubles (CSC-001); at 0, where SIN is 0, <c>#DIV/0!</c>.</summary>
    private static Operand Csc(FunctionCall call) => Angle(call, n => 1 / Reduced(n).Sine.Value, zeroIsDiv0: true);

    /// <summary>SEC: 1/COS, divided as doubles.</summary>
    private static Operand Sec(FunctionCall call) => Angle(call, n => 1 / Reduced(n).Cosine.Value);

    /// <summary>
    /// A function of an angle whose absolute value must be below 2^27 (<see cref="TrigonometricLimit"/>);
    /// at or past it, <c>#NUM!</c>. Where the function has a pole at 0, 0 is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Angle(FunctionCall call, Func<double, double> function, bool zeroIsDiv0 = false)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (Math.Abs(number) >= TrigonometricLimit) return Operand.Of(ErrorValue.Num);
        if (zeroIsDiv0 && number == 0) return Operand.Of(ErrorValue.Div0);
        return Operand.Of(Evaluator.Number(function(number)));
    }

    /// <summary>TAN, and COT's divisor: the reduced sine over the reduced cosine, rounded once.</summary>
    private static double Tangent(double angle)
    {
        var (sine, cosine) = Reduced(angle);
        return (sine / cosine).Value;
    }

    /// <summary>
    /// The double nearest pi/2, the high part of the pi/2 Excel reduces an angle by: pi rounded to a
    /// 64-bit significand, the x87's <c>FLDPI</c> (<c>0xC90FDAA22168C235</c> × 2^-62), halved, is this plus
    /// <see cref="HalfPiLow"/> exactly. <c>SIN(PI())</c> is the difference between that pi and the double
    /// <c>PI()</c> gives, 1.22514845490862E-16 (SIN-004), where the true sine of that double is
    /// 1.2246467991473532E-16; the seventeenth Windows run observed it, with <c>COS(PI()/2)</c> and
    /// <c>TAN(PI()/2)</c> (ADR-0047; <c>spikes/exsheet-trig-reduction/</c> holds the experiment).
    /// </summary>
    private const double HalfPiHigh = Math.PI / 2;

    /// <summary>The low part of Excel's pi/2, 565 × 2^-63 (6.1257422745431E-17).</summary>
    private static readonly double HalfPiLow = Math.ScaleB(565, -63);

    /// <summary>
    /// The sine and cosine of <paramref name="angle"/> (below 2^27 in absolute value), as Excel answers
    /// them: the angle less the nearest whole number <c>k</c> of Excel's pi/2 (<see cref="HalfPiHigh"/> +
    /// <see cref="HalfPiLow"/>), exactly, leaves <c>r</c> within about pi/4 of 0; the sine and cosine of
    /// <c>r</c>, to about 100 bits, give those of the angle by the quadrant <c>k</c> names. Each is
    /// rounded to a double once, by <see cref="DoubleDouble.Value"/>. Nothing here is the platform's
    /// <see cref="Math.Sin"/>, so every runtime gives the same doubles.
    /// </summary>
    private static (DoubleDouble Sine, DoubleDouble Cosine) Reduced(double angle)
    {
        var k = Math.Round(angle / HalfPiHigh);
        // k·HalfPiHigh = p + pRest exactly. angle − p is exact (the two are within a factor of 2 of each
        // other whenever k is not 0), and so is its difference with pRest: both are multiples of 2^-53
        // and the result is below 1. k·HalfPiLow is exact (k is below 2^27, 565 below 2^10), so r is
        // angle − k·(HalfPiHigh + HalfPiLow) to the last bit.
        var p = k * HalfPiHigh;
        var pRest = Math.FusedMultiplyAdd(k, HalfPiHigh, -p);
        var r = DoubleDouble.Sum(angle - p - pRest, -(k * HalfPiLow));
        var r2 = r * r;
        var sine = r * Series(r2, odd: true);
        var cosine = Series(r2, odd: false);
        return ((long)k & 3) switch
        {
            0 => (sine, cosine),
            1 => (cosine, -sine),
            2 => (-sine, -cosine),
            _ => (-cosine, sine),
        };
    }

    /// <summary>The terms of the series <see cref="Reduced"/> sums: enough for |r| up to pi/4, to 2^-110.</summary>
    private const int SeriesTerms = 14;

    /// <summary>
    /// sin(r)/r (<paramref name="odd"/>) or cos(r) from r², by Horner's rule:
    /// 1 − r²/(2·3)·(1 − r²/(4·5)·(…)) or 1 − r²/(1·2)·(1 − r²/(3·4)·(…)).
    /// </summary>
    private static DoubleDouble Series(DoubleDouble r2, bool odd)
    {
        var inner = DoubleDouble.One;
        for (var n = SeriesTerms; n >= 1; n--)
        {
            var denominator = odd ? (2.0 * n) * (2 * n + 1) : (2.0 * n - 1) * (2 * n);
            inner = DoubleDouble.One - inner * r2 / denominator;
        }
        return inner;
    }

    /// <summary>
    /// A number held as the unevaluated sum of two doubles, <see cref="High"/> the double nearest it:
    /// about 106 bits, enough that rounding it to a double gives the double nearest the true value. The
    /// operations are Hida, Li and Bailey's (the QD library's "accurate" ones), on <see cref="Math.FusedMultiplyAdd"/>.
    /// </summary>
    private readonly record struct DoubleDouble(double High, double Low)
    {
        public static DoubleDouble One => new(1, 0);

        /// <summary>The double nearest the number.</summary>
        public double Value => High + Low;

        /// <summary>a + b exactly (Knuth's TwoSum).</summary>
        public static DoubleDouble Sum(double a, double b)
        {
            var s = a + b;
            var bb = s - a;
            return new(s, (a - (s - bb)) + (b - bb));
        }

        /// <summary>a + b exactly, where |a| ≥ |b| (Dekker's FastTwoSum).</summary>
        private static DoubleDouble QuickSum(double a, double b)
        {
            var s = a + b;
            return new(s, b - (s - a));
        }

        public static DoubleDouble operator -(DoubleDouble a) => new(-a.High, -a.Low);

        public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
        {
            var (s1, s2) = Sum(a.High, b.High);
            var (t1, t2) = Sum(a.Low, b.Low);
            s2 += t1;
            (s1, s2) = QuickSum(s1, s2);
            s2 += t2;
            return QuickSum(s1, s2);
        }

        public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b) => a + -b;

        public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
        {
            var p1 = a.High * b.High;
            var p2 = Math.FusedMultiplyAdd(a.High, b.High, -p1);
            p2 += a.High * b.Low + a.Low * b.High;
            return QuickSum(p1, p2);
        }

        public static DoubleDouble operator *(DoubleDouble a, double b)
        {
            var p1 = a.High * b;
            var p2 = Math.FusedMultiplyAdd(a.High, b, -p1);
            p2 += a.Low * b;
            return QuickSum(p1, p2);
        }

        public static DoubleDouble operator /(DoubleDouble a, double b)
        {
            var q1 = a.High / b;
            var p1 = q1 * b;
            var p2 = Math.FusedMultiplyAdd(q1, b, -p1);
            var (s, e) = Sum(a.High, -p1);
            e -= p2;
            e += a.Low;
            var q2 = (s + e) / b;
            return QuickSum(q1, q2);
        }

        public static DoubleDouble operator /(DoubleDouble a, DoubleDouble b)
        {
            var q1 = a.High / b.High;
            var r = a - b * q1;
            var q2 = r.High / b.High;
            r -= b * q2;
            var q3 = r.High / b.High;
            return QuickSum(q1, q2) + new DoubleDouble(q3, 0);
        }
    }

    // ---- COTH, CSCH, SECH -------------------------------------------------------------------------

    /// <summary>
    /// COTH: the hyperbolic cotangent; at 0 <c>#DIV/0!</c>. Microsoft documents a limit of 2^27 on the
    /// argument, but Excel answers past it: <c>COTH(2^27)</c> is 1 (COTH-002).
    /// </summary>
    private static Operand Coth(FunctionCall call) => Hyperbolic(call, n => 1 / PortableMath.Tanh(n), zeroIsDiv0: true);

    /// <summary>CSCH: 1/SINH; at 0 <c>#DIV/0!</c>; past what SINH holds, 0, as Excel's (CSCH-002, CSCH-004).</summary>
    private static Operand Csch(FunctionCall call) => Hyperbolic(call, n => 1 / PortableMath.Sinh(n), zeroIsDiv0: true);

    /// <summary>SECH: 1/COSH; past what COSH holds, 0, as Excel's (SECH-003).</summary>
    private static Operand Sech(FunctionCall call) => Hyperbolic(call, n => 1 / PortableMath.Cosh(n));

    /// <summary>A hyperbolic function of any number; where it has a pole at 0, 0 is <c>#DIV/0!</c>.</summary>
    private static Operand Hyperbolic(FunctionCall call, Func<double, double> function, bool zeroIsDiv0 = false)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (zeroIsDiv0 && number == 0) return Operand.Of(ErrorValue.Div0);
        return Operand.Of(Evaluator.Number(function(number)));
    }

    // ---- ASIN, ACOS, ATAN, ATAN2, ACOT -----------------------------------------------------------

    /// <summary>ASIN: a number outside -1 to 1 is <c>#NUM!</c>.</summary>
    private static Operand Asin(FunctionCall call) => Bounded(call, n => Math.Abs(n) <= 1, PortableMath.Asin);

    /// <summary>ACOS: a number outside -1 to 1 is <c>#NUM!</c>.</summary>
    private static Operand Acos(FunctionCall call) => Bounded(call, n => Math.Abs(n) <= 1, PortableMath.Acos);

    private static Operand Atan(FunctionCall call) => Bounded(call, _ => true, PortableMath.Atan);

    /// <summary>ACOT: an angle from 0 to pi.</summary>
    private static Operand Acot(FunctionCall call) => Bounded(call, _ => true, n => Math.PI / 2 - PortableMath.Atan(n));

    /// <summary>
    /// ATAN2: the angle of the point (<c>x_num</c>, <c>y_num</c>), above -pi and up to pi. Excel takes
    /// x first, the reverse of <see cref="Math.Atan2"/>. Both 0 is <c>#DIV/0!</c>, as documented.
    /// </summary>
    private static Operand Atan2(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var x, out var failure)) return failure;
        if (!TryNumber(call, 1, out var y, out failure)) return failure;
        if (x == 0 && y == 0) return Operand.Of(ErrorValue.Div0);
        return Operand.Of(Evaluator.Number(PortableMath.Atan2(y, x)));
    }

    // ---- SINH, COSH, TANH, ASINH, ACOSH, ATANH, ACOTH --------------------------------------------

    /// <summary>SINH: past what a double holds, <c>#NUM!</c>.</summary>
    private static Operand Sinh(FunctionCall call) => Bounded(call, _ => true, PortableMath.Sinh);

    /// <summary>COSH: past what a double holds, <c>#NUM!</c>.</summary>
    private static Operand Cosh(FunctionCall call) => Bounded(call, _ => true, PortableMath.Cosh);

    private static Operand Tanh(FunctionCall call) => Bounded(call, _ => true, PortableMath.Tanh);

    private static Operand Asinh(FunctionCall call) => Bounded(call, _ => true, PortableMath.Asinh);

    /// <summary>ACOSH: a number below 1 is <c>#NUM!</c>.</summary>
    private static Operand Acosh(FunctionCall call) => Bounded(call, n => n >= 1, PortableMath.Acosh);

    /// <summary>ATANH: a number not strictly between -1 and 1 is <c>#NUM!</c>.</summary>
    private static Operand Atanh(FunctionCall call) => Bounded(call, n => Math.Abs(n) < 1, PortableMath.Atanh);

    /// <summary>
    /// ACOTH: ½·ln((x+1)/(x−1)), for an absolute value above 1. Microsoft's page names both
    /// <c>#NUM!</c> and <c>#VALUE!</c> for a smaller one; Excel gives <c>#NUM!</c> (ACOTH-003, observed
    /// by the seventeenth Windows run).
    /// </summary>
    private static Operand Acoth(FunctionCall call) => Bounded(call, n => Math.Abs(n) > 1, n => 0.5 * PortableMath.Log((n + 1) / (n - 1)));

    // ---- DEGREES, RADIANS ------------------------------------------------------------------------

    private static Operand Degrees(FunctionCall call) => Bounded(call, _ => true, n => n * 180 / Math.PI);

    private static Operand Radians(FunctionCall call) => Bounded(call, _ => true, n => n * Math.PI / 180);

    /// <summary>A function of one number, <c>#NUM!</c> where <paramref name="inDomain"/> refuses it or the result is not finite.</summary>
    private static Operand Bounded(FunctionCall call, Func<double, bool> inDomain, Func<double, double> function)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        return inDomain(number) ? Operand.Of(Evaluator.Number(function(number))) : Operand.Of(ErrorValue.Num);
    }
}
