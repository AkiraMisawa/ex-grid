namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and NPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The annuity's arguments: <c>rate</c>, <c>nper</c>, the third, and the optional fourth (0 when
    /// left out) and <c>type</c>. A <c>type</c> other than 0 or 1 is refused with <c>#VALUE!</c> until
    /// Excel is asked how it reads one (PMT-008).
    /// </summary>
    private static bool TryAnnuity(FunctionCall call, out double rate, out double periods, out double third, out double fourth, out double type, out Operand failure)
    {
        periods = third = fourth = type = 0;
        if (!TryNumber(call, 0, out rate, out failure)) return false;
        if (!TryNumber(call, 1, out periods, out failure)) return false;
        if (!TryNumber(call, 2, out third, out failure)) return false;
        if (call.Has(3) && !TryNumber(call, 3, out fourth, out failure)) return false;
        if (call.Has(4) && !TryNumber(call, 4, out type, out failure)) return false;
        if (type is not (0 or 1))
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        return true;
    }

    /// <summary>PMT: the payment that pays <c>pv</c> down to <c>fv</c> over <c>nper</c> periods; <c>nper</c> of 0 is <c>#NUM!</c>.</summary>
    private static Operand Pmt(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var present, out var future, out var type, out var failure)) return failure;
        if (periods == 0) return Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + future) / periods));
        var growth = Math.Pow(1 + rate, periods);
        return Operand.Of(Evaluator.Number(-(rate * ((present * growth) + future)) / ((1 + (rate * type)) * (growth - 1))));
    }

    /// <summary>PV: what a series of payments and a future value are worth now.</summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var type, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        var growth = Math.Pow(1 + rate, periods);
        return Operand.Of(Evaluator.Number(-(future + (payment * (1 + (rate * type)) * (growth - 1) / rate)) / growth));
    }

    /// <summary>FV: what a present value and a series of payments are worth after <c>nper</c> periods.</summary>
    private static Operand Fv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var present, out var type, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + (payment * periods))));
        var growth = Math.Pow(1 + rate, periods);
        return Operand.Of(Evaluator.Number(-((present * growth) + (payment * (1 + (rate * type)) * (growth - 1) / rate))));
    }

    /// <summary>
    /// NPV: each value discounted by its period, the first one period away; the values are the
    /// numbers SUM takes. A rate of -1 is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Npv(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rate, out var failure)) return failure;
        var values = new List<double>();
        if (CollectNumbers(call, values, from: 1) is { } error) return Operand.Of(error);
        if (rate == -1) return Operand.Of(ErrorValue.Div0);
        var total = 0.0;
        for (var i = 0; i < values.Count; i++) total += values[i] / Math.Pow(1 + rate, i + 1);
        return Operand.Of(Evaluator.Number(total));
    }
}
