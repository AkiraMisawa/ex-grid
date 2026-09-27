using System.Globalization;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// The declared set, ADR-0047's first list. Each function is written to Microsoft's documented
/// behaviour for it: how it treats blanks, text, booleans and Error Values typed as arguments and
/// found inside ranges, and its rounding. Where ExSheet cannot give Excel's answer — a result that
/// would spill an array, XLOOKUP's binary search — it gives an Error Value instead of another
/// answer.
/// </summary>
internal static partial class FunctionLibrary
{
    private const int Open = 255;

    private static partial IEnumerable<FunctionDefinition> Declare() =>
    [
        new("SUM", 1, Open, "number1, [number2], ...", "Adds its arguments.", Sum),
        new("AVERAGE", 1, Open, "number1, [number2], ...", "Returns the average (arithmetic mean) of its arguments.", Average),
        new("MIN", 1, Open, "number1, [number2], ...", "Returns the smallest number in a set of values.", Min),
        new("MAX", 1, Open, "number1, [number2], ...", "Returns the largest number in a set of values.", Max),
        new("COUNT", 1, Open, "value1, [value2], ...", "Counts how many numbers are in the list of arguments.", Count),
        new("COUNTA", 1, Open, "value1, [value2], ...", "Counts how many values are in the list of arguments.", CountA),
        new("IF", 2, 3, "logical_test, value_if_true, [value_if_false]", "Returns one value if a condition is TRUE and another if it is FALSE.", If),
        new("ROUND", 2, 2, "number, num_digits", "Rounds a number to a specified number of digits, half away from zero.", Round),
        new("IFERROR", 2, 2, "value, value_if_error", "Returns value_if_error if value is an Error Value, and value otherwise. #GETTING_DATA is not an error to it.", IfError),
        new("ISERROR", 1, 1, "value", "Returns TRUE if value is an Error Value. #GETTING_DATA is not an error to it.", IsError),
        new("XLOOKUP", 3, 6, "lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode]", "Searches a range for a match and returns the corresponding item of a second range.", XLookup),
    ];

    // ---- Aggregates: SUM, AVERAGE, MIN, MAX ----------------------------------------------

    /// <summary>
    /// The numbers the aggregates take, per Microsoft's documentation: inside a range or
    /// Reference only numbers count, and blanks, text and booleans are ignored; typed directly as
    /// arguments, booleans and text that reads as a number count too, and other text is
    /// <c>#VALUE!</c>. An Error Value anywhere is the result, the first one found left to right.
    /// </summary>
    private static ErrorValue? CollectNumbers(FunctionCall call, List<double> numbers)
    {
        var evaluator = call.Evaluator;
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column:
                    foreach (var value in evaluator.RangeValues(operand))
                    {
                        if (value.Kind == ValueKind.Number) numbers.Add(value.Number);
                        else if (value.IsError) return value.Error;
                    }
                    break;
                case OperandKind.Missing:
                    numbers.Add(0);
                    break;
                default:
                    if (operand.Scalar is not { } scalar)
                    {
                        numbers.Add(0);
                        break;
                    }
                    var number = evaluator.ToNumber(scalar, out var error);
                    if (error is { } e) return e;
                    numbers.Add(number);
                    break;
            }
        }
        return null;
    }

    private static Operand Sum(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        var total = 0.0;
        foreach (var n in numbers) total += n;
        return Operand.Of(Evaluator.Number(total));
    }

    private static Operand Average(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        if (numbers.Count == 0) return Operand.Of(ErrorValue.Div0);
        var total = 0.0;
        foreach (var n in numbers) total += n;
        return Operand.Of(Evaluator.Number(total / numbers.Count));
    }

    private static Operand Min(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        return Operand.Of(Value.FromNumber(numbers.Count == 0 ? 0 : numbers.Min()));
    }

    private static Operand Max(FunctionCall call)
    {
        var numbers = new List<double>();
        if (CollectNumbers(call, numbers) is { } error) return Operand.Of(error);
        return Operand.Of(Value.FromNumber(numbers.Count == 0 ? 0 : numbers.Max()));
    }

    // ---- COUNT, COUNTA ---------------------------------------------------------------------

    /// <summary>
    /// Inside a range, only numbers are counted. Typed directly, numbers, booleans and text that
    /// reads as a number are counted; Error Values and other text are not, and are not errors.
    /// </summary>
    private static Operand Count(FunctionCall call)
    {
        var evaluator = call.Evaluator;
        var count = 0;
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column:
                    count += evaluator.RangeValues(operand).Count(v => v.Kind == ValueKind.Number);
                    break;
                case OperandKind.Missing:
                    count++;
                    break;
                default:
                    if (operand.Scalar is not { } scalar) break;
                    if (scalar.Kind is ValueKind.Number or ValueKind.Boolean) count++;
                    else if (scalar.Kind == ValueKind.Text && ConstantParser.TryParseNumber(scalar.Text, evaluator.Culture, out _)) count++;
                    break;
            }
        }
        return Operand.Of(Value.FromNumber(count));
    }

    /// <summary>Counts every value that is not blank: Error Values and empty text included.</summary>
    private static Operand CountA(FunctionCall call)
    {
        var evaluator = call.Evaluator;
        var count = 0;
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column:
                    count += evaluator.RangeValues(operand).Count();
                    break;
                case OperandKind.Missing:
                    count++;
                    break;
                default:
                    if (operand.Scalar is not null) count++;
                    break;
            }
        }
        return Operand.Of(Value.FromNumber(count));
    }

    // ---- IF, IFERROR, ISERROR ---------------------------------------------------------------

    /// <summary>
    /// A multi-cell range where one Value is wanted. Excel would spill an array; ExSheet has none,
    /// so it is <c>#VALUE!</c>, and IFERROR and ISERROR do not treat that refusal as a catchable
    /// error — they refuse too, rather than turn "cannot" into a fallback value.
    /// </summary>
    private static bool IsArray(Operand operand) =>
        (operand.Kind == OperandKind.Area && !operand.Area.IsSingleCell) || (operand.Kind == OperandKind.Column && operand.Column!.Count != 1);

    private static Operand If(FunctionCall call)
    {
        var test = call.Operand(0);
        if (IsArray(test)) return Operand.Of(ErrorValue.Value);
        var condition = call.Evaluator.ScalarOf(test);
        bool truth;
        switch (condition)
        {
            case null:
                truth = false;
                break;
            case { IsError: true } e:
                return Operand.Of(e);
            case { Kind: ValueKind.Number } n:
                truth = n.Number != 0;
                break;
            case { Kind: ValueKind.Boolean } b:
                truth = b.Boolean;
                break;
            case { } t when t.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase):
                truth = true;
                break;
            case { } t when t.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase):
                truth = false;
                break;
            default:
                return Operand.Of(ErrorValue.Value);
        }
        if (truth) return call.Has(1) ? call.Operand(1) : Operand.Of(Value.FromNumber(0));
        if (call.Count < 3) return Operand.Of(Value.FromBoolean(false));
        return call.Has(2) ? call.Operand(2) : Operand.Of(Value.FromNumber(0));
    }

    /// <summary>
    /// Microsoft's documentation: an empty cell as either argument is taken as empty text (""). An
    /// Error Value is caught — but not <c>#GETTING_DATA</c>, which waits (ADR-0049), nor
    /// <c>#CIRC!</c>, which a Formula reading a cycle shows whatever it computes (ADR-0047).
    /// </summary>
    private static Operand IfError(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Operand.Of(ErrorValue.Value);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true } e && e.Error is not (ErrorValue.GettingData or ErrorValue.Circ))
        {
            if (!call.Has(1)) return Operand.Of(Value.FromNumber(0));
            var fallback = call.Operand(1);
            if (IsArray(fallback)) return Operand.Of(ErrorValue.Value);
            return Operand.Of(call.Evaluator.ScalarOf(fallback) ?? Value.FromText(""));
        }
        return Operand.Of(value ?? Value.FromText(""));
    }

    private static Operand IsError(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Operand.Of(ErrorValue.Value);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true } e && e.Error is ErrorValue.GettingData or ErrorValue.Circ) return Operand.Of(e);
        return Operand.Of(Value.FromBoolean(value is { IsError: true }));
    }

    // ---- ROUND ---------------------------------------------------------------------------

    private static Operand Round(FunctionCall call)
    {
        var evaluator = call.Evaluator;
        var first = call.Operand(0);
        var second = call.Operand(1);
        if (IsArray(first) || IsArray(second)) return Operand.Of(ErrorValue.Value);
        var number = evaluator.ToNumber(evaluator.ScalarOf(first), out var error);
        if (error is { } e1) return Operand.Of(e1);
        var digits = evaluator.ToNumber(evaluator.ScalarOf(second), out error);
        if (error is { } e2) return Operand.Of(e2);
        return Operand.Of(Evaluator.Number(RoundHalfAwayFromZero(number, Math.Truncate(digits))));
    }

    /// <summary>
    /// Excel's ROUND: half away from zero, applied to the number as Excel holds it to 15
    /// significant digits — so 2.675, which is 2.67499999999999982236431605997495353221893310546875
    /// as a double, rounds to 2.68 as its decimal spelling says.
    /// </summary>
    internal static double RoundHalfAwayFromZero(double number, double digits)
    {
        if (number == 0) return 0;
        // "E14": 15 significant digits, d.dddddddddddddd, then the exponent.
        var spelled = Math.Abs(number).ToString("E14", CultureInfo.InvariantCulture);
        var mantissa = spelled[0] + spelled[2..16];
        var exponent = int.Parse(spelled.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        // How many of the 15 digits sit at or above the 10^-digits place.
        var kept = exponent + 1 + digits;
        if (kept >= 15) return number;
        if (kept < 0) return 0;
        var keep = (int)kept;
        var retained = mantissa[..keep];
        var roundUp = mantissa[keep] >= '5';
        decimal magnitude;
        if (keep == 0)
        {
            magnitude = roundUp ? 1 : 0;
        }
        else
        {
            magnitude = decimal.Parse(retained, CultureInfo.InvariantCulture) + (roundUp ? 1 : 0);
        }
        // magnitude is an integer of at most 16 digits; place it back at 10^(exponent + 1 - keep).
        var scale = exponent + 1 - keep;
        var result = double.Parse(
            magnitude.ToString(CultureInfo.InvariantCulture) + "E" + scale.ToString(CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        return number < 0 ? -result : result;
    }

    // ---- XLOOKUP ---------------------------------------------------------------------------

    private static Operand XLookup(FunctionCall call)
    {
        var evaluator = call.Evaluator;

        var lookupOperand = call.Operand(0);
        if (IsArray(lookupOperand)) return Operand.Of(ErrorValue.Value);
        var lookup = evaluator.ScalarOf(lookupOperand);
        if (lookup is { IsError: true } lookupError) return Operand.Of(lookupError);

        var lookupArray = call.Operand(1);
        var returnArray = call.Operand(2);
        if (!lookupArray.IsRange) return lookupArray.IsError ? lookupArray : Operand.Of(ErrorValue.Value);
        if (!returnArray.IsRange) return returnArray.IsError ? returnArray : Operand.Of(ErrorValue.Value);

        var matchMode = 0;
        if (call.Has(4))
        {
            var mode = evaluator.ToNumber(evaluator.ScalarOf(call.Operand(4)), out var modeError);
            if (modeError is { } me) return Operand.Of(me);
            matchMode = (int)Math.Truncate(mode);
            if (matchMode is not (0 or -1 or 1 or 2)) return Operand.Of(ErrorValue.Value);
        }
        var searchMode = 1;
        if (call.Has(5))
        {
            var mode = evaluator.ToNumber(evaluator.ScalarOf(call.Operand(5)), out var modeError);
            if (modeError is { } me) return Operand.Of(me);
            searchMode = (int)Math.Truncate(mode);
            // Binary search (2, -2) is not admitted: on unsorted data Excel's answer depends on its
            // own search order, which ExSheet does not know, and a different wrong answer would
            // look like a right one.
            if (searchMode is not (1 or -1)) return Operand.Of(ErrorValue.Value);
        }

        // A range of one row or one column; a Linked Table's column runs down.
        if (Vector.Of(lookupArray) is not { } vector) return Operand.Of(ErrorValue.Value);
        // The return array lies along the lookup array; more than one cell across it would spill.
        if (Vector.Of(returnArray) is not { } result || result.Vertical != vector.Vertical || result.Length != vector.Length)
        {
            return Operand.Of(ErrorValue.Value);
        }

        int? found = null;
        if (lookup is { } wanted)
        {
            var candidates = vector.Candidates(evaluator);
            if (searchMode == -1) candidates = candidates.Reverse();
            found = matchMode switch
            {
                0 => FirstExact(candidates, wanted),
                2 => FirstWildcard(candidates, wanted),
                _ => Nearest(candidates, wanted, matchMode),
            };
        }

        if (found is not { } index)
        {
            return call.Has(3) ? call.Operand(3) : Operand.Of(ErrorValue.NA);
        }
        return result.ItemAt(index);
    }

    /// <summary>A range of one row or one column, as XLOOKUP reads it: rectangle of cells or a Linked Table's column.</summary>
    private readonly record struct Vector(Operand Source, bool Vertical, int Length)
    {
        public static Vector? Of(Operand range)
        {
            if (range.Kind == OperandKind.Column) return new Vector(range, true, range.Column!.Count);
            var area = range.Area;
            if (area.Columns == 1) return new Vector(range, true, area.Rows);
            if (area.Rows == 1) return new Vector(range, false, area.Columns);
            return null;
        }

        /// <summary>The Values that are not blank, with their positions along the vector, in order.</summary>
        public IEnumerable<(int Index, Value Value)> Candidates(Evaluator evaluator)
        {
            if (Source.Kind == OperandKind.Column)
            {
                var column = Source.Column!;
                return Enumerable.Range(0, column.Count).Where(i => column[i] is not null).Select(i => (i, column[i]!.Value));
            }
            var area = Source.Area;
            var vertical = Vertical;
            return evaluator.Cells.NonBlankIn(area).Select(a => (vertical ? a.Row - area.Row1 : a.Column - area.Column1, evaluator.Cells.Read(a)!.Value));
        }

        /// <summary>The item at a position: a cell, read as a Reference, or a Linked Table's Value.</summary>
        public Operand ItemAt(int index)
        {
            if (Source.Kind == OperandKind.Column) return Source.Column![index] is { } value ? Operand.Of(value) : Operand.Blank;
            var area = Source.Area;
            var row = Vertical ? area.Row1 + index : area.Row1;
            var column = Vertical ? area.Column1 : area.Column1 + index;
            return Operand.Of(new Area(row, column, row, column));
        }
    }

    private static bool SameKindEqual(Value candidate, Value wanted) =>
        candidate.Kind == wanted.Kind && candidate.Kind != ValueKind.Error && Evaluator.Compare(candidate, wanted) == 0;

    private static int? FirstExact(IEnumerable<(int Index, Value Value)> candidates, Value wanted)
    {
        foreach (var (index, value) in candidates)
        {
            if (SameKindEqual(value, wanted)) return index;
        }
        return null;
    }

    private static int? FirstWildcard(IEnumerable<(int Index, Value Value)> candidates, Value wanted)
    {
        if (wanted.Kind != ValueKind.Text) return FirstExact(candidates, wanted);
        var pattern = wanted.Text;
        foreach (var (index, value) in candidates)
        {
            if (value.Kind == ValueKind.Text && Wildcard.Matches(pattern, value.Text)) return index;
        }
        return null;
    }

    /// <summary>An exact match first; otherwise the nearest smaller (mode −1) or larger (mode 1) value of the same kind, the first such in search order.</summary>
    private static int? Nearest(IEnumerable<(int Index, Value Value)> candidates, Value wanted, int mode)
    {
        int? best = null;
        Value bestValue = default;
        foreach (var (index, value) in candidates)
        {
            if (value.Kind != wanted.Kind || value.Kind == ValueKind.Error) continue;
            var order = Evaluator.Compare(value, wanted);
            if (order == 0) return index;
            if (mode == -1 ? order > 0 : order < 0) continue;
            if (best is null || (mode == -1 ? Evaluator.Compare(value, bestValue) > 0 : Evaluator.Compare(value, bestValue) < 0))
            {
                best = index;
                bestValue = value;
            }
        }
        return best;
    }
}

/// <summary>Excel's wildcards: <c>*</c> any run, <c>?</c> one character, <c>~</c> escapes the next; without regard to case.</summary>
internal static class Wildcard
{
    public static bool Matches(string pattern, string text) => Match(pattern, 0, text, 0);

    private static bool Match(string pattern, int p, string text, int t)
    {
        while (p < pattern.Length)
        {
            var c = pattern[p];
            if (c == '*')
            {
                while (p < pattern.Length && pattern[p] == '*') p++;
                if (p == pattern.Length) return true;
                for (var start = t; start <= text.Length; start++)
                {
                    if (Match(pattern, p, text, start)) return true;
                }
                return false;
            }
            if (t >= text.Length) return false;
            if (c == '?')
            {
                p++;
                t++;
                continue;
            }
            if (c == '~' && p + 1 < pattern.Length && pattern[p + 1] is '*' or '?' or '~')
            {
                c = pattern[p + 1];
                p++;
            }
            if (char.ToUpperInvariant(c) != char.ToUpperInvariant(text[t])) return false;
            p++;
            t++;
        }
        return t == text.Length;
    }
}
