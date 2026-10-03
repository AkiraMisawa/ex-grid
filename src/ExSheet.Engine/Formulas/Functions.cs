using System.Globalization;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// The declared set, ADR-0047's first list. Each function is written to Microsoft's documented
/// behaviour for it: how it treats blanks, text, booleans and Error Values typed as arguments and
/// found inside ranges, and its rounding. Where ExSheet cannot give Excel's answer — a result that
/// would spill an array, XLOOKUP's binary search over data it cannot show to be sorted — it gives
/// an Error Value instead of another answer.
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
        new("XLOOKUP", 3, 6, "lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode]", "Searches a range for a match and returns the corresponding item of a second range.", XLookup)
        {
            // Excel's lists, character for character: match_mode's as the Windows runs of
            // 2026-09-27 (item 14) and 2026-09-30 (case 13) saw them, search_mode's as the
            // second of them saw it (case 12).
            Values = new Dictionary<int, IReadOnlyList<ArgumentValue>>
            {
                [4] =
                [
                    new("0", "0 - Exact match"),
                    new("-1", "-1 - Exact match or next smaller item"),
                    new("1", "1 - Exact match or next larger item"),
                    new("2", "2 - Wildcard character match"),
                    new("3", "3 - Regex match"),
                ],
                [5] =
                [
                    new("1", "1 - Search first-to-last"),
                    new("-1", "-1 - Search last-to-first"),
                    new("2", "2 - Binary search (sorted ascending order)"),
                    new("-2", "-2 - Binary search (sorted descending order)"),
                ],
            },
        },
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
        // SUM's last addition cancels as a Formula's final addition does (ADR-0047, second run:
        // =SUM(0.1,0.2,-0.3) is 0), wherever SUM stands in the Formula.
        var total = 0.0;
        for (var i = 0; i < numbers.Count; i++) total = i == numbers.Count - 1 ? Arithmetic.FinalAdd(total, numbers[i]) : total + numbers[i];
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
    /// An empty cell as either argument gives an empty value: a Formula whose result it is shows
    /// 0, as Excel was observed to show (verification/2026-09-27-windows-excel), and joined to
    /// text it is "" (IFERROR-013, verification/2026-09-27-windows-excel-2). A value_if_error
    /// left out is empty too, not 0 (IFERROR-016, verification/2026-09-28-windows-excel-3). An Error Value is caught — but not <c>#GETTING_DATA</c>,
    /// which waits (ADR-0049), nor <c>#CIRC!</c>, which a Formula reading a cycle shows whatever it
    /// computes (ADR-0047).
    /// </summary>
    private static Operand IfError(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (IsArray(operand)) return Operand.Of(ErrorValue.Value);
        var value = call.Evaluator.ScalarOf(operand);
        if (value is { IsError: true } e && e.Error is not (ErrorValue.GettingData or ErrorValue.Circ))
        {
            if (!call.Has(1)) return Operand.Blank;
            var fallback = call.Operand(1);
            if (IsArray(fallback)) return Operand.Of(ErrorValue.Value);
            return call.Evaluator.ScalarOf(fallback) is { } caught ? Operand.Of(caught) : Operand.Blank;
        }
        return value is { } v ? Operand.Of(v) : Operand.Blank;
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
            if (matchMode is not (0 or -1 or 1 or 2 or 3)) return Operand.Of(ErrorValue.Value);
        }
        var searchMode = 1;
        if (call.Has(5))
        {
            var mode = evaluator.ToNumber(evaluator.ScalarOf(call.Operand(5)), out var modeError);
            if (modeError is { } me) return Operand.Of(me);
            searchMode = (int)Math.Truncate(mode);
            if (searchMode is not (1 or -1 or 2 or -2)) return Operand.Of(ErrorValue.Value);
        }

        // A range of one row or one column; a Linked Table's column runs down.
        if (Vector.Of(lookupArray) is not { } vector) return Operand.Of(ErrorValue.Value);
        // The return array lies along the lookup array; more than one cell across it would spill.
        if (Vector.Of(returnArray) is not { } result || result.Vertical != vector.Vertical || result.Length != vector.Length)
        {
            return Operand.Of(ErrorValue.Value);
        }

        int? found = null;
        if (searchMode is 2 or -2)
        {
            if (!TryBinarySearch(vector, evaluator, lookup, matchMode, ascending: searchMode == 2, out found)) return Operand.Of(ErrorValue.Value);
        }
        else if (lookup is { } wanted)
        {
            var candidates = vector.Candidates(evaluator);
            if (searchMode == -1) candidates = candidates.Reverse();
            if (matchMode == 3)
            {
                // Numbers and booleans are matched by their text; Error Values match nothing.
                var texts = candidates.Where(c => !c.Value.IsError).Select(c => (c.Index, evaluator.ToText(c.Value)));
                if (!PortableRegex.TryFirstMatch(evaluator.ToText(wanted), texts, out found)) return Operand.Of(ErrorValue.Value);
            }
            else
            {
                found = matchMode switch
                {
                    0 => FirstExact(candidates, wanted),
                    2 => FirstWildcard(candidates, wanted),
                    _ => Nearest(candidates, wanted, matchMode),
                };
            }
        }
        else if (matchMode == 0)
        {
            // A blank lookup value matches a blank cell, as Excel was observed to (XLOOKUP-067).
            found = vector.FirstBlank(evaluator, fromEnd: searchMode == -1);
        }

        if (found is not { } index)
        {
            return call.Has(3) ? call.Operand(3) : Operand.Of(ErrorValue.NA);
        }
        return result.ItemAt(index);
    }

    /// <summary>
    /// XLOOKUP's binary search (<c>search_mode</c> 2 and −2), admitted only over a lookup array that
    /// is sorted as the mode says, ascending for 2 and descending for −2 (ADR-0047).
    /// <see langword="false"/> is a refusal (<c>#VALUE!</c>): the array is not sorted as the mode
    /// says; it holds blanks, Error Values, or values of more than one kind (Excel's ordering across
    /// kinds is not pinned here, so mixed kinds count as unsorted); the lookup value is blank or of
    /// another kind; text holds anything but ASCII letters, digits and spaces (Excel's collation of
    /// other characters is not pinned here); or the match mode is the wildcard or the regular
    /// expression one.
    /// </summary>
    /// <remarks>
    /// Where the matching key appears more than once, the one returned is the one Excel was observed
    /// to return (verification/2026-09-27-windows-excel, Part A item 9; ADR-0047). A key equal to the
    /// lookup value is the first of its run ascending and the last descending. A nearest key that is
    /// not equal is the one of its run closest to where the lookup value would sit: ascending, the
    /// last of the next smaller and the first of the next larger; descending, the first of the next
    /// smaller and the last of the next larger. Layouts longer than the eight observed are corpus
    /// cases still to be asked.
    /// </remarks>
    /// <param name="vector">The lookup array.</param>
    /// <param name="evaluator">What reads its cells.</param>
    /// <param name="lookup">The lookup value; blank as <see langword="null"/>.</param>
    /// <param name="matchMode">XLOOKUP's <c>match_mode</c>: 0, −1, 1, 2 or 3.</param>
    /// <param name="ascending">Whether the mode is 2 (ascending) rather than −2 (descending).</param>
    /// <param name="found">The position matched, or <see langword="null"/> when nothing matches.</param>
    private static bool TryBinarySearch(Vector vector, Evaluator evaluator, Value? lookup, int matchMode, bool ascending, out int? found)
    {
        found = null;
        if (matchMode is 2 or 3 || lookup is not { } wanted || wanted.IsError || !Orderable(wanted)) return false;
        var candidates = vector.Candidates(evaluator).ToList();
        if (candidates.Count != vector.Length) return false;
        foreach (var (_, value) in candidates)
        {
            if (value.Kind != wanted.Kind || !Orderable(value)) return false;
        }
        for (var i = 1; i < candidates.Count; i++)
        {
            var order = Evaluator.Compare(candidates[i - 1].Value, candidates[i].Value);
            if (ascending ? order > 0 : order < 0) return false;
        }

        // The value that matches: the wanted one, or failing that the nearest smaller (−1) or larger (1).
        Value? match = null;
        foreach (var (_, value) in candidates)
        {
            var order = Evaluator.Compare(value, wanted);
            if (order == 0)
            {
                match = value;
                break;
            }
            if (matchMode == -1 && order < 0 && (match is null || Evaluator.Compare(value, match) > 0)) match = value;
            if (matchMode == 1 && order > 0 && (match is null || Evaluator.Compare(value, match) < 0)) match = value;
        }
        if (match is not { } matched || (matchMode == 0 && Evaluator.Compare(matched, wanted) != 0)) return true;

        // Of a run of equal keys, the one Excel was observed to return (Part A item 9): an equal key
        // is the first ascending and the last descending; a nearest one is the end of its run
        // nearer the lookup value.
        var first = Evaluator.Compare(matched, wanted) == 0
            ? ascending
            : matchMode == 1 ? ascending : !ascending;
        foreach (var (i, value) in candidates)
        {
            if (Evaluator.Compare(value, matched) != 0) continue;
            found = i;
            if (first) break;
        }
        return true;

        static bool Orderable(Value value) =>
            value.Kind is ValueKind.Number or ValueKind.Boolean
            || (value.Kind == ValueKind.Text && value.Text.All(c => char.IsAsciiLetterOrDigit(c) || c == ' '));
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

        /// <summary>The first blank position in search order, from the end when <paramref name="fromEnd"/>; <see langword="null"/> when none is blank.</summary>
        public int? FirstBlank(Evaluator evaluator, bool fromEnd)
        {
            var filled = new HashSet<int>(Candidates(evaluator).Select(c => c.Index));
            if (filled.Count == Length) return null;
            for (var k = 0; k < Length; k++)
            {
                var index = fromEnd ? Length - 1 - k : k;
                if (!filled.Contains(index)) return index;
            }
            return null;
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

    /// <summary>
    /// An exact match first; otherwise the nearest smaller (mode −1) or larger (mode 1) value, the
    /// first such in search order. Values of another kind take part in Excel's order across kinds —
    /// numbers before text, text before booleans — so over 10, x, 30, y the next smaller value
    /// than "m" is 30, as Excel was observed to answer (XLOOKUP-071). Error Values take no part.
    /// </summary>
    private static int? Nearest(IEnumerable<(int Index, Value Value)> candidates, Value wanted, int mode)
    {
        int? best = null;
        Value bestValue = default;
        foreach (var (index, value) in candidates)
        {
            if (value.Kind == ValueKind.Error) continue;
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
