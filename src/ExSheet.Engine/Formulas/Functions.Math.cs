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
    /// the divisor's sign, computed as every evaluation that gives each answer Excel was seen to give
    /// computes it, and <c>#VALUE!</c> where they part (ADR-0047, ADR-0202). The remainder is exact:
    /// Excel's <c>MOD(123456789012,0.7)</c> is 0.5000078322611261 (MOD-017), where the identity in doubles
    /// is 0.5000152587890625, and its <c>MOD(1,0.1)</c> 0.09999999999999995 (MOD-020), where the double
    /// quotient, 10, gives 0. Where the remainder has the number's sign, the divisor is added in a
    /// register wider than a double and the sum then stored as one: <c>MOD(-0.087626,6630)</c> is
    /// 6629.9123739999995 (MOD-029), the sum rounded first to an x87 register's 64 bits and then to a
    /// double, where rounded once it is 6629.912374; and a sum stored as the divisor itself is 0:
    /// <c>MOD(-1E-20,1)</c> is 0 (MOD-032), not 1. A remainder a hair from 0 is itself: <c>MOD(1.1,0.1)</c>
    /// is 2.7755575615628914E-17 (MOD-030). Three evaluations give all 32 observed answers
    /// (<c>spikes/exsheet-financial-evaluation/mod-search.cs</c>): the sum rounded to 64 bits, taken as 0
    /// where the double stored is the divisor (<c>B9</c>), or where the register holds it (<c>B10</c>); and
    /// the sum truncated to 64 bits, taken as 0 where the double is the divisor (<c>B12</c>). They part
    /// where the sum is the divisor as a double but not in the register, as at <c>MOD(-1E-17,1)</c>, and
    /// where truncating at 64 bits stores another double than rounding there, as at
    /// <c>MOD(-0.084474,2270)</c>; <see cref="TurnedRemainder"/> refuses those, and MOD-033 to MOD-036 ask
    /// them. A divisor of 0 is <c>#DIV/0!</c>, and a quotient of <see cref="ModQuotientLimit"/> or more
    /// <c>#NUM!</c>.
    /// </summary>
    private static Operand Mod(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var divisor, out failure)) return failure;
        if (divisor == 0) return Operand.Of(ErrorValue.Div0);
        if (Math.Abs(number / divisor) >= ModQuotientLimit) return Operand.Of(ErrorValue.Num);
        // % on doubles is exact, and carries the number's sign.
        var remainder = number % divisor;
        if (remainder == 0 || (remainder < 0) == (divisor < 0)) return Operand.Of(Evaluator.Number(remainder));
        return TurnedRemainder(remainder, divisor) is { } turned
            ? Operand.Of(Evaluator.Number(turned))
            : Operand.Of(ErrorValue.Value);
    }

    /// <summary>
    /// <paramref name="remainder"/> + <paramref name="divisor"/>, where the remainder, exact, has the
    /// sign opposite the divisor's, as each of the three evaluations of <see cref="Mod"/> adds it: held at
    /// 64 bits, rounded to nearest or truncated, then stored as a double, rounded to nearest; and a sum
    /// that is the divisor itself taken as 0, in the double stored or, for the second, in the register.
    /// Null where they give different doubles. The exact sum is the sum rounded once and its error
    /// (Fast2Sum, since |remainder| &lt; |divisor|); the register's last place is 2^-11 of the double's
    /// between the sum and its neighbour on the error's side, so a 64-bit sum is the sum plus a whole
    /// number of those steps, and a sum 1024 steps away is a tie, which the double stored breaks to the
    /// even neighbour. Each comparison is exact.
    /// </summary>
    private static double? TurnedRemainder(double remainder, double divisor)
    {
        // Each evaluation gives MOD(-n,-d) as -MOD(n,d): work with a positive divisor.
        var sign = divisor < 0 ? -1.0 : 1.0;
        var d = sign * divisor;
        var r = sign * remainder;
        var sum = r + d;
        var error = r - (sum - d);
        var gap = error < 0 ? sum - Math.BitDecrement(sum) : Math.BitIncrement(sum) - sum;
        var steps = error / gap * 2048;
        var nearest = Math.Round(steps, MidpointRounding.ToEven);
        var rounded = StoredAsDouble(sum, nearest);
        var truncated = StoredAsDouble(sum, Math.Floor(steps));
        var zeroInDouble = rounded == d ? 0 : rounded;
        var zeroInRegister = sum == d && nearest == 0 ? 0 : rounded;
        var zeroTruncated = truncated == d ? 0 : truncated;
        if (zeroInDouble != zeroInRegister || zeroInDouble != zeroTruncated) return null;
        return sign * zeroInDouble;
    }

    /// <summary>The double a 64-bit sum <paramref name="steps"/> of its last place from <paramref name="sum"/> is stored as.</summary>
    private static double StoredAsDouble(double sum, double steps) =>
        Math.Abs(steps) < 1024 || (BitConverter.DoubleToInt64Bits(sum) & 1) == 0 ? sum
        : steps > 0 ? Math.BitIncrement(sum) : Math.BitDecrement(sum);

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
    /// truncates it). A k below 1, or above their count, is <c>#NUM!</c>.
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
    /// at Excel's 15 significant digits, so a k a hair from a whole number is that number. A k outside
    /// 1 to the count of the numbers is <c>#NUM!</c>, whole or not, before it is rounded: 0.5 and 0.99
    /// are not rounded up to 1 (LARGE-011, LARGE-013, SMALL-007), and <c>SMALL</c>'s 9.5 over nine
    /// numbers is not truncated to 9 (SMALL-008, the second area pass of 2026-10-12). A k within it that
    /// is not whole is rounded up for the k-th largest (LARGE-007 to 009, AGGREGATE-038 and 059: 1.2 and
    /// 1.5 are the 2nd largest) and truncated for the k-th smallest (SMALL-004, 005, 009, AGGREGATE-060
    /// and 066: 1.2 and 1.5 are the smallest). The two are one rule: the k-th largest with k rounded up
    /// is the (n+1-k)-th smallest with that index truncated, and k lies within 1 to n exactly where
    /// n+1-k does.
    /// </summary>
    private static Operand KthOf(List<double> numbers, double k, bool largest)
    {
        k = Arithmetic.AtFifteenDigits(k);
        if (k < 1 || k > numbers.Count) return Operand.Of(ErrorValue.Num);
        k = largest ? Math.Ceiling(k) : Math.Floor(k);
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
