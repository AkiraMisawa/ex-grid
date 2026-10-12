namespace ExSheet.Engine.Formulas;

/// <summary>ROUNDUP, ROUNDDOWN, ABS, INT, MOD, PRODUCT, MEDIAN, LARGE, SMALL, TRUNC, POWER and SQRT, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The quotient at and above which MOD is <c>#NUM!</c>. Excel 2007 and earlier failed once the
    /// quotient reached 2^27; Excel 365 does not (MOD-008: <c>MOD(2^27,1)</c> is 0, the seventeenth
    /// Windows run). Microsoft documents no limit. A Microsoft 365 user reported on Microsoft's Q&amp;A
    /// (2021) that <c>MOD(1125899999999,1)</c> is 0 and <c>MOD(1125900000000,1)</c> is <c>#NUM!</c>, and
    /// dated that limit from Excel 2010; the engine answers below it and refuses from it, as the area
    /// pass of 2026-10-12 read Excel on both sides of it (MOD-010 to MOD-016).
    /// </summary>
    private const double ModQuotientLimit = 1_125_900_000_000;

    private static Operand RoundUp(FunctionCall call) => RoundBy(call, Rounding.AwayFromZero);

    private static Operand RoundDown(FunctionCall call) => RoundBy(call, Rounding.TowardZero);

    private static Operand RoundBy(FunctionCall call, Rounding rounding)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var digits, out failure)) return failure;
        return Operand.Of(Evaluator.Number(RoundAt(number, Math.Truncate(digits), rounding)));
    }

    private static Operand Abs(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Value.FromNumber(Math.Abs(number))) : failure;

    /// <summary>INT rounds down, toward negative infinity: INT(-8.9) is -9.</summary>
    private static Operand Int(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Value.FromNumber(Math.Floor(number))) : failure;

    /// <summary>
    /// MOD: <c>number - divisor*INT(number/divisor)</c>, as Microsoft defines it, so the result takes
    /// the divisor's sign, where every evaluation that gives each answer Excel was seen to give gives
    /// the same double, and <c>#VALUE!</c> where they part (ADR-0047, ADR-0202). Excel's
    /// <c>MOD(123456789012,0.7)</c> is 0.5000078322611261 (MOD-017), the identity computed exactly and
    /// rounded once, where in doubles it is 0.5000152587890625; no evaluation in doubles gives it.
    /// Seven classes of evaluation give all seventeen observed answers
    /// (<c>spikes/exsheet-financial-evaluation/mod-search.cs</c>), and they part in four places:
    /// <list type="bullet">
    /// <item>where the double quotient rounds up onto a whole number the exact quotient is below, as in
    /// <c>MOD(1,0.1)</c>: the exact remainder is about the divisor, a fused multiply-add over that
    /// quotient a hair of the wrong sign, and that taken as 0 is 0. The multiply-add differs from the
    /// exact remainder there and only there.</item>
    /// <item>where the remainder is not 0 but within 2^-49 of the number, as in <c>MOD(1.1,0.1)</c>:
    /// Excel's cancellation of a final addition (<see cref="Arithmetic"/>) would make it 0.</item>
    /// <item>where the remainder plus the divisor rounds to the divisor itself, as in
    /// <c>MOD(-1E-20,1)</c>: the divisor, or 0.</item>
    /// <item>where that addition, made first in an x87 register's 64 bits, would round twice to another
    /// double, as in <c>MOD(-0.087626,6630)</c>, at the last bit.</item>
    /// </list>
    /// Elsewhere it answers the exact remainder rounded once: <c>%</c> on doubles is exact, with the
    /// number's sign, and adding the divisor where that is not the divisor's is the one rounding.
    /// MOD-020 to MOD-032 ask Excel where the classes part. A divisor of 0 is <c>#DIV/0!</c>, and a
    /// quotient of <see cref="ModQuotientLimit"/> or more <c>#NUM!</c>.
    /// </summary>
    private static Operand Mod(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var divisor, out failure)) return failure;
        if (divisor == 0) return Operand.Of(ErrorValue.Div0);
        var quotient = number / divisor;
        if (Math.Abs(quotient) >= ModQuotientLimit) return Operand.Of(ErrorValue.Num);
        var remainder = number % divisor;
        var turned = remainder != 0 && (remainder < 0) != (divisor < 0);
        var exact = turned ? remainder + divisor : remainder;
        if (Math.FusedMultiplyAdd(-divisor, Math.Floor(quotient), number) != exact
            || (exact != 0 && Math.Abs(exact) <= Math.Abs(number) * Arithmetic.CancelTolerance)
            || (turned && (exact == divisor || RoundsTwice(remainder, divisor))))
            return Operand.Of(ErrorValue.Value);
        return Operand.Of(Evaluator.Number(exact));
    }

    /// <summary>
    /// Whether <paramref name="a"/> + <paramref name="b"/>, where |a| &lt; |b|, rounded first to an x87
    /// register's 64-bit significand and then to a double, is another double than the sum rounded once.
    /// That happens where the exact sum lies within half a 64-bit unit of the midpoint between two
    /// doubles and the sum rounded once is the odd one, which a tie then leaves. The error of the sum
    /// is exact (Fast2Sum), and so is each comparison.
    /// </summary>
    private static bool RoundsTwice(double a, double b)
    {
        var sum = a + b;
        var error = a - (sum - b);
        if (error == 0 || (BitConverter.DoubleToInt64Bits(sum) & 1) == 0) return false;
        var gap = Math.BitIncrement(Math.Abs(sum)) - Math.Abs(sum);
        return Math.Abs(error) >= gap * (2047.0 / 4096);
    }

    // ---- PRODUCT, MEDIAN, LARGE, SMALL --------------------------------------------------------

    /// <summary>PRODUCT: the numbers SUM takes, multiplied; with none, 0, as Excel's is.</summary>
    private static Operand Product(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        return ProductOf(numbers);
    }

    /// <summary>PRODUCT of the numbers taken; with none, 0.</summary>
    private static Operand ProductOf(List<double> numbers)
    {
        if (numbers.Count == 0) return Operand.Of(Value.FromNumber(0));
        var product = 1.0;
        foreach (var n in numbers) product *= n;
        return Operand.Of(Evaluator.Number(product));
    }

    /// <summary>MEDIAN: the middle of the numbers SUM takes, or the mean of the middle two; with none, <c>#NUM!</c>.</summary>
    private static Operand Median(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        return MedianOf(numbers);
    }

    /// <summary>MEDIAN of the numbers taken, which it sorts; with none, <c>#NUM!</c>.</summary>
    private static Operand MedianOf(List<double> numbers)
    {
        if (numbers.Count == 0) return Operand.Of(ErrorValue.Num);
        numbers.Sort();
        var middle = numbers.Count / 2;
        return Operand.Of(Evaluator.Number(numbers.Count % 2 == 1 ? numbers[middle] : (numbers[middle - 1] + numbers[middle]) / 2));
    }

    private static Operand Large(FunctionCall call) => Ranked(call, largest: true);

    private static Operand Small(FunctionCall call) => Ranked(call, largest: false);

    /// <summary>
    /// LARGE and SMALL: the numbers of the array (a range's numbers only; a typed value coerced),
    /// and the k-th of them (<see cref="KthOf"/>: LARGE rounds a k that is not whole up, and SMALL
    /// truncates it). A k below 1, or past their count once rounded, is <c>#NUM!</c>.
    /// </summary>
    private static Operand Ranked(FunctionCall call, bool largest)
    {
        var numbers = new List<double>();
        var array = call.Operand(0);
        if (array.IsRange)
        {
            foreach (var value in call.Evaluator.RangeValues(array))
            {
                if (value.IsError) return Operand.Of(value);
                if (value.Kind == ValueKind.Number) numbers.Add(value.Number);
            }
        }
        else
        {
            if (!TryNumber(call, 0, out var single, out var arrayFailure)) return arrayFailure;
            numbers.Add(single);
        }
        if (!TryNumber(call, 1, out var k, out var failure)) return failure;
        return KthOf(numbers, k, largest);
    }

    /// <summary>
    /// The k-th largest or smallest of the numbers taken, which it sorts, as Excel reads k. k is read
    /// at Excel's 15 significant digits, so a k a hair from a whole number is that number. A k below 1
    /// is <c>#NUM!</c>, whether whole or not (LARGE-004, LARGE-011: 0.5 is not rounded up to 1). A k
    /// that is not whole is rounded up for the k-th largest (LARGE-007 to 010, AGGREGATE-038 and 059:
    /// 1.2 and 1.5 are the 2nd largest) and truncated for the k-th smallest (SMALL-004, 005,
    /// AGGREGATE-060: 1.2 and 1.5 are the smallest). The two are one rule: the k-th largest with k
    /// rounded up is the (n+1-k)-th smallest with that index truncated. A k past the count of the
    /// numbers once so rounded is <c>#NUM!</c>.
    /// </summary>
    private static Operand KthOf(List<double> numbers, double k, bool largest)
    {
        k = Arithmetic.AtFifteenDigits(k);
        if (k < 1) return Operand.Of(ErrorValue.Num);
        k = largest ? Math.Ceiling(k) : Math.Floor(k);
        if (k > numbers.Count) return Operand.Of(ErrorValue.Num);
        numbers.Sort();
        var at = (int)k - 1;
        return Operand.Of(Value.FromNumber(largest ? numbers[^(at + 1)] : numbers[at]));
    }

    // ---- TRUNC, POWER, SQRT ---------------------------------------------------------------------

    /// <summary>TRUNC: toward zero at <c>num_digits</c>, 0 when left out — ROUNDDOWN with an optional second argument.</summary>
    private static Operand Trunc(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        var digits = 0.0;
        if (call.Has(1) && !TryNumber(call, 1, out digits, out failure)) return failure;
        return Operand.Of(Evaluator.Number(RoundAt(number, Math.Truncate(digits), Rounding.TowardZero)));
    }

    /// <summary>POWER: the <c>^</c> operator's answer, its Error Values included (0^0 is <c>#NUM!</c>, 0 to a negative power <c>#DIV/0!</c>).</summary>
    private static Operand Power(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var power, out failure)) return failure;
        if (number == 0 && power == 0) return Operand.Of(ErrorValue.Num);
        if (number == 0 && power < 0) return Operand.Of(ErrorValue.Div0);
        return Arithmetic.Power(number, power) is { } result ? Operand.Of(Evaluator.Number(result)) : Operand.Of(ErrorValue.Num);
    }

    /// <summary>SQRT: a negative number is <c>#NUM!</c>.</summary>
    private static Operand Sqrt(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        return number < 0 ? Operand.Of(ErrorValue.Num) : Operand.Of(Value.FromNumber(Math.Sqrt(number)));
    }

    // ---- RANK.EQ, STDEV.S, STDEV.P, VAR.S, VAR.P ---------------------------------------------------

    /// <summary>
    /// RANK.EQ: the number's rank among the numbers of <c>ref</c>, from the largest when <c>order</c>
    /// is 0 or left out and from the smallest otherwise; equal numbers share the top rank. A number
    /// not among them is <c>#N/A</c>, and a <c>ref</c> that is no range is <c>#VALUE!</c>.
    /// </summary>
    private static Operand RankEq(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        var reference = call.Operand(1);
        if (!reference.IsRange) return reference.IsError ? reference : Operand.Of(ErrorValue.Value);
        var order = 0.0;
        if (call.Has(2) && !TryNumber(call, 2, out order, out failure)) return failure;
        var found = false;
        var ahead = 0;
        foreach (var value in call.Evaluator.RangeValues(reference))
        {
            if (value.IsError) return Operand.Of(value);
            if (value.Kind != ValueKind.Number) continue;
            if (value.Number == number) found = true;
            else if (order == 0 ? value.Number > number : value.Number < number) ahead++;
        }
        return found ? Operand.Of(Value.FromNumber(ahead + 1)) : Operand.Of(ErrorValue.NA);
    }

    private static Operand StdevS(FunctionCall call) => Spread(call, sample: true, root: true);

    private static Operand StdevP(FunctionCall call) => Spread(call, sample: false, root: true);

    private static Operand VarS(FunctionCall call) => Spread(call, sample: true, root: false);

    private static Operand VarP(FunctionCall call) => Spread(call, sample: false, root: false);

    /// <summary>
    /// VAR.S, VAR.P and their square roots STDEV.S and STDEV.P, over the numbers SUM takes, the
    /// squared deviations taken from their mean. Fewer than two numbers for a sample, or none for a
    /// population, is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Spread(FunctionCall call, bool sample, bool root)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        return SpreadOf(numbers, sample, root);
    }

    /// <summary>VAR.S, VAR.P, STDEV.S or STDEV.P of the numbers taken (<see cref="Spread"/>).</summary>
    private static Operand SpreadOf(List<double> numbers, bool sample, bool root)
    {
        var divisor = sample ? numbers.Count - 1 : numbers.Count;
        if (divisor < 1) return Operand.Of(ErrorValue.Div0);
        var mean = numbers.Sum() / numbers.Count;
        var squares = numbers.Sum(n => (n - mean) * (n - mean));
        var variance = squares / divisor;
        return Operand.Of(Evaluator.Number(root ? Math.Sqrt(variance) : variance));
    }

    // ---- SIGN, EXP, LN, LOG10, PI -------------------------------------------------------------------

    private static Operand Sign(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Value.FromNumber(Math.Sign(number))) : failure;

    /// <summary>EXP: e to the number; past what a double holds, <c>#NUM!</c>.</summary>
    private static Operand Exp(FunctionCall call) =>
        TryNumber(call, 0, out var number, out var failure) ? Operand.Of(Evaluator.Number(Math.Exp(number))) : failure;

    /// <summary>LN: a number not above 0 is <c>#NUM!</c>.</summary>
    private static Operand Ln(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        return number <= 0 ? Operand.Of(ErrorValue.Num) : Operand.Of(Value.FromNumber(Math.Log(number)));
    }

    /// <summary>LOG10: a number not above 0 is <c>#NUM!</c>.</summary>
    private static Operand Log10(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        return number <= 0 ? Operand.Of(ErrorValue.Num) : Operand.Of(Value.FromNumber(Math.Log10(number)));
    }

    private static Operand Pi(FunctionCall call) => Operand.Of(Value.FromNumber(Math.PI));
}
