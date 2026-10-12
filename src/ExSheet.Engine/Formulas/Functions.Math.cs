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
    /// MOD: <c>number - divisor*INT(number/divisor)</c>, as Microsoft defines it, computed exactly
    /// and rounded once, so the result takes the divisor's sign. That is Excel's double on every case
    /// a Windows run has read: <c>MOD(123456789012,0.7)</c> is 0.5000078322611261 (MOD-017), where the
    /// identity evaluated in doubles gives 0.5000152587890625, and <c>MOD(0.3,0.1)</c> is
    /// 0.09999999999999998 (MOD-009). <c>%</c> on doubles is the exact remainder, with the number's
    /// sign; where that is not the divisor's, adding the divisor is the one rounding. Other evaluations
    /// fit the same cases (a fused multiply-add, an x87 register), and MOD-020 to MOD-029 ask Excel
    /// where they part (<c>spikes/exsheet-financial-evaluation/mod-search.cs</c>). A divisor of 0 is
    /// <c>#DIV/0!</c>, and a quotient of <see cref="ModQuotientLimit"/> or more <c>#NUM!</c>.
    /// </summary>
    private static Operand Mod(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var divisor, out failure)) return failure;
        if (divisor == 0) return Operand.Of(ErrorValue.Div0);
        if (Math.Abs(number / divisor) >= ModQuotientLimit) return Operand.Of(ErrorValue.Num);
        var remainder = number % divisor;
        if (remainder != 0 && (remainder < 0) != (divisor < 0)) remainder += divisor;
        return Operand.Of(Evaluator.Number(remainder));
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
