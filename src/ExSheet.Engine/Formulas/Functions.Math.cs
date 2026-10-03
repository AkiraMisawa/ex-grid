namespace ExSheet.Engine.Formulas;

/// <summary>ROUNDUP, ROUNDDOWN, ABS, INT, MOD, PRODUCT, MEDIAN, LARGE, SMALL, TRUNC, POWER and SQRT, each to Microsoft's documentation of it.</summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The quotient at and above which MOD is <c>#NUM!</c>. Older versions of Excel were known to
    /// fail once the quotient reached 2^27; whether a current Excel still does is asked of the next
    /// Windows run (MOD-008). Until it answers, a quotient this large is refused rather than computed.
    /// </summary>
    private const double ModQuotientLimit = 134_217_728;

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
    /// MOD: <c>number - divisor*INT(number/divisor)</c>, so the result takes the divisor's sign.
    /// A divisor of 0 is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Mod(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var number, out var failure)) return failure;
        if (!TryNumber(call, 1, out var divisor, out failure)) return failure;
        if (divisor == 0) return Operand.Of(ErrorValue.Div0);
        var quotient = number / divisor;
        if (Math.Abs(quotient) >= ModQuotientLimit) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Evaluator.Number(number - divisor * Math.Floor(quotient)));
    }

    // ---- PRODUCT, MEDIAN, LARGE, SMALL --------------------------------------------------------

    /// <summary>PRODUCT: the numbers SUM takes, multiplied; with none, 0, as Excel's is.</summary>
    private static Operand Product(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
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
        if (numbers.Count == 0) return Operand.Of(ErrorValue.Num);
        numbers.Sort();
        var middle = numbers.Count / 2;
        return Operand.Of(Evaluator.Number(numbers.Count % 2 == 1 ? numbers[middle] : (numbers[middle - 1] + numbers[middle]) / 2));
    }

    private static Operand Large(FunctionCall call) => Ranked(call, largest: true);

    private static Operand Small(FunctionCall call) => Ranked(call, largest: false);

    /// <summary>
    /// LARGE and SMALL: the numbers of the array (a range's numbers only; a typed value coerced),
    /// and the k-th of them. A k below 1 or past their count is <c>#NUM!</c>. A k that is not a
    /// whole number is refused with <c>#VALUE!</c> until Excel is asked how it reads one (LARGE-007).
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
        if (k != Math.Floor(k)) return Operand.Of(ErrorValue.Value);
        if (k < 1 || k > numbers.Count) return Operand.Of(ErrorValue.Num);
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
}
