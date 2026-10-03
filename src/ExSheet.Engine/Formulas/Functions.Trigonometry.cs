namespace ExSheet.Engine.Formulas;

/// <summary>
/// LOG and the trigonometric and hyperbolic functions, each to Microsoft's documentation of it
/// (ticket 07). Angles are in radians.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The size of argument, 2^27, at and above which a trigonometric function is <c>#NUM!</c>.
    /// Microsoft documents it for COT, CSC, SEC, COTH, CSCH and SECH; for SIN, COS and TAN it is
    /// asked of the next Windows run (SIN-005 and its kin), and until then such an argument is refused
    /// rather than computed.
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
        var result = @base == 10 ? Math.Log10(number) : Math.Log(number) / Math.Log(@base);
        return Operand.Of(Evaluator.Number(result));
    }

    // ---- SIN, COS, TAN, COT, CSC, SEC ------------------------------------------------------------

    private static Operand Sin(FunctionCall call) => Angle(call, Math.Sin);

    private static Operand Cos(FunctionCall call) => Angle(call, Math.Cos);

    private static Operand Tan(FunctionCall call) => Angle(call, Math.Tan);

    /// <summary>COT: <c>COT(0)</c> is <c>#DIV/0!</c>, as documented.</summary>
    private static Operand Cot(FunctionCall call) => Angle(call, n => 1 / Math.Tan(n), zeroIsDiv0: true);

    /// <summary>CSC: 1/SIN; at 0, where SIN is 0, <c>#DIV/0!</c>.</summary>
    private static Operand Csc(FunctionCall call) => Angle(call, n => 1 / Math.Sin(n), zeroIsDiv0: true);

    private static Operand Sec(FunctionCall call) => Angle(call, n => 1 / Math.Cos(n));

    /// <summary>COTH: as COT, the hyperbolic cotangent; at 0 <c>#DIV/0!</c>.</summary>
    private static Operand Coth(FunctionCall call) => Angle(call, n => 1 / Math.Tanh(n), zeroIsDiv0: true);

    /// <summary>CSCH: 1/SINH; at 0 <c>#DIV/0!</c>.</summary>
    private static Operand Csch(FunctionCall call) => Angle(call, n => 1 / Math.Sinh(n), zeroIsDiv0: true);

    private static Operand Sech(FunctionCall call) => Angle(call, n => 1 / Math.Cosh(n));

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

    // ---- ASIN, ACOS, ATAN, ATAN2, ACOT -----------------------------------------------------------

    /// <summary>ASIN: a number outside -1 to 1 is <c>#NUM!</c>.</summary>
    private static Operand Asin(FunctionCall call) => Bounded(call, n => Math.Abs(n) <= 1, Math.Asin);

    /// <summary>ACOS: a number outside -1 to 1 is <c>#NUM!</c>.</summary>
    private static Operand Acos(FunctionCall call) => Bounded(call, n => Math.Abs(n) <= 1, Math.Acos);

    private static Operand Atan(FunctionCall call) => Bounded(call, _ => true, Math.Atan);

    /// <summary>ACOT: an angle from 0 to pi.</summary>
    private static Operand Acot(FunctionCall call) => Bounded(call, _ => true, n => Math.PI / 2 - Math.Atan(n));

    /// <summary>
    /// ATAN2: the angle of the point (<c>x_num</c>, <c>y_num</c>), above -pi and up to pi. Excel takes
    /// x first, the reverse of <see cref="Math.Atan2"/>. Both 0 is <c>#DIV/0!</c>, as documented.
    /// </summary>
    private static Operand Atan2(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var x, out var failure)) return failure;
        if (!TryNumber(call, 1, out var y, out failure)) return failure;
        if (x == 0 && y == 0) return Operand.Of(ErrorValue.Div0);
        return Operand.Of(Evaluator.Number(Math.Atan2(y, x)));
    }

    // ---- SINH, COSH, TANH, ASINH, ACOSH, ATANH, ACOTH --------------------------------------------

    /// <summary>SINH: past what a double holds, <c>#NUM!</c>.</summary>
    private static Operand Sinh(FunctionCall call) => Bounded(call, _ => true, Math.Sinh);

    /// <summary>COSH: past what a double holds, <c>#NUM!</c>.</summary>
    private static Operand Cosh(FunctionCall call) => Bounded(call, _ => true, Math.Cosh);

    private static Operand Tanh(FunctionCall call) => Bounded(call, _ => true, Math.Tanh);

    private static Operand Asinh(FunctionCall call) => Bounded(call, _ => true, Math.Asinh);

    /// <summary>ACOSH: a number below 1 is <c>#NUM!</c>.</summary>
    private static Operand Acosh(FunctionCall call) => Bounded(call, n => n >= 1, Math.Acosh);

    /// <summary>ATANH: a number not strictly between -1 and 1 is <c>#NUM!</c>.</summary>
    private static Operand Atanh(FunctionCall call) => Bounded(call, n => Math.Abs(n) < 1, Math.Atanh);

    /// <summary>
    /// ACOTH: ½·ln((x+1)/(x−1)), for an absolute value above 1. Microsoft's page names both
    /// <c>#NUM!</c> and <c>#VALUE!</c> for a smaller one; it is <c>#NUM!</c> here until Excel is asked
    /// (ACOTH-003).
    /// </summary>
    private static Operand Acoth(FunctionCall call) => Bounded(call, n => Math.Abs(n) > 1, n => 0.5 * Math.Log((n + 1) / (n - 1)));

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
