using System.Globalization;

namespace ExSheet.Engine.Formulas;

internal static partial class FunctionLibrary
{
    private static Operand SumIf(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Sum, single: true);
    private static Operand SumIfs(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Sum, single: false);
    private static Operand CountIf(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Count, single: true);
    private static Operand CountIfs(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Count, single: false);
    private static Operand AverageIf(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Average, single: true);
    private static Operand AverageIfs(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Average, single: false);
    private static Operand MaxIfs(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Maximum, single: false);
    private static Operand MinIfs(FunctionCall call) => ConditionalAggregate(call, CriteriaAggregate.Minimum, single: false);

    private static Operand CountBlank(FunctionCall call)
    {
        var operand = call.Operand(0);
        if (operand.IsError) return operand;
        if (CriteriaRange.From(operand, call.Evaluator) is not { } range) return Operand.Of(ErrorValue.Value);
        var count = range.Count;
        foreach (var position in range.Occupied())
            if (range.Read(position) is { } value && !(value.Kind == ValueKind.Text && value.Text.Length == 0)) count--;
        return Operand.Of(Value.FromNumber(count));
    }

    private enum CriteriaAggregate { Sum, Count, Average, Maximum, Minimum }

    private static Operand ConditionalAggregate(FunctionCall call, CriteriaAggregate aggregate, bool single)
    {
        var counting = aggregate == CriteriaAggregate.Count;
        var from = single || counting ? 0 : 1;
        var conditions = new List<(CriteriaRange Range, Criterion Test)>();
        for (var i = from; i < (single ? 2 : call.Count); i += 2)
        {
            var operand = call.Operand(i);
            if (operand.IsError) return operand;
            if (CriteriaRange.From(operand, call.Evaluator) is not { } range) return Operand.Of(ErrorValue.Value);
            var criterionOperand = call.Operand(i + 1);
            if (Evaluator.IsMulti(criterionOperand)) return Operand.Of(ErrorValue.Value);
            var criterion = Criterion.Parse(call.Evaluator.ScalarOf(criterionOperand), call.Evaluator.Culture);
            if (criterion is null) return Operand.Of(ErrorValue.Value);
            if (conditions.Count > 0 && !range.SameShape(conditions[0].Range)) return Operand.Of(ErrorValue.Value);
            conditions.Add((range, criterion));
        }
        var first = conditions[0].Range;
        CriteriaRange? results = null;
        if (!counting)
        {
            var operand = single ? call.Count > 2 ? call.Operand(2) : call.Operand(0) : call.Operand(0);
            if (operand.IsError) return operand;
            results = CriteriaRange.From(operand, call.Evaluator);
            if (results is null) return Operand.Of(ErrorValue.Value);
            if (!results.SameShape(first))
            {
                // SUMIF/AVERAGEIF read a footprint starting at the result's top-left cell.
                // A computed Reference that escapes its named dependencies stays refused.
                if (!single || operand.Kind != OperandKind.Area || call.Count <= 2
                    || LiteralReference(call.Argument(0)) is not { Spilled: false }
                    || LiteralReference(call.Argument(2)) is not { Spilled: false }) return Operand.Of(ErrorValue.Value);
                var a = operand.Area;
                if ((long)a.Row1 + first.Rows > Sheet.RowCount || (long)a.Column1 + first.Columns > Sheet.ColumnCount)
                    return Operand.Of(ErrorValue.Ref);
                results = CriteriaRange.From(Operand.Of(new Area(a.Row1, a.Column1,
                    a.Row1 + first.Rows - 1, a.Column1 + first.Columns - 1)), call.Evaluator)!;
            }
        }

        // A range's absent cells matter to conditions. Count them without materialising an
        // entire column, then visit the union of occupied positions to correct that count.
        var positions = new SortedSet<long>();
        if (counting)
        {
            foreach (var (range, _) in conditions) positions.UnionWith(range.Occupied());
        }
        else positions.UnionWith(results!.Occupied());
        var emptyMatches = counting && conditions.All(c => c.Test.Matches(null) == true);
        var count = emptyMatches ? first.Count - positions.Count : 0L;
        double total = 0, extreme = 0;
        foreach (var position in positions)
        {
            var matches = true;
            foreach (var (range, criterion) in conditions)
            {
                var match = criterion.Matches(range.Read(position));
                if (match is null) return Operand.Of(ErrorValue.Value);
                if (!match.Value) matches = false;
            }
            if (!matches) continue;
            if (counting) { count++; continue; }
            var value = results!.Read(position);
            if (value is { IsError: true } error) return Operand.Of(error);
            if (value is not { Kind: ValueKind.Number } number) continue;
            if (count == 0) extreme = number.Number;
            else extreme = aggregate == CriteriaAggregate.Maximum ? Math.Max(extreme, number.Number) : Math.Min(extreme, number.Number);
            total += number.Number;
            count++;
        }
        return aggregate switch
        {
            CriteriaAggregate.Count => Operand.Of(Value.FromNumber(count)),
            CriteriaAggregate.Average => count == 0 ? Operand.Of(ErrorValue.Div0) : Operand.Of(Evaluator.Number(total / count)),
            CriteriaAggregate.Maximum or CriteriaAggregate.Minimum => Operand.Of(Value.FromNumber(extreme)),
            _ => Operand.Of(Evaluator.Number(total)),
        };
    }

    private sealed class CriteriaRange(Operand operand, Evaluator evaluator, int rows, int columns)
    {
        public int Rows => rows;
        public int Columns => columns;
        public long Count => (long)rows * columns;
        public bool SameShape(CriteriaRange other) => rows == other.Rows && columns == other.Columns;
        public static CriteriaRange? From(Operand operand, Evaluator evaluator) => operand.Kind switch
        {
            OperandKind.Area => new(operand, evaluator, operand.Area.Rows, operand.Area.Columns),
            OperandKind.Column => new(operand, evaluator, operand.Column!.Count, 1),
            _ => null,
        };
        public Value? Read(long position) => operand.Kind == OperandKind.Area
            ? evaluator.Cells.Read(new CellAddress(operand.Area.Row1 + (int)(position / columns), operand.Area.Column1 + (int)(position % columns)))
            : operand.Column![(int)position];
        public IEnumerable<long> Occupied()
        {
            if (operand.Kind == OperandKind.Area)
            {
                foreach (var address in evaluator.Cells.NonBlankIn(operand.Area))
                    yield return (long)(address.Row - operand.Area.Row1) * columns + address.Column - operand.Area.Column1;
            }
            else
            {
                for (var i = 0; i < rows; i++) if (operand.Column![i] is not null) yield return i;
            }
        }
    }

    /// <summary>The implicit footprint is a dependency as much as a written Reference (ADR-0047).</summary>
    internal static IEnumerable<Reference> CriteriaResultReferences(FunctionNode function)
    {
        if (function.Function is null || function.Name is not ("SUMIF" or "AVERAGEIF") || function.Arguments.Count != 3)
            yield break;
        if (LiteralReference(function.Arguments[0]) is not { Spilled: false } criteria
            || LiteralReference(function.Arguments[2]) is not { Spilled: false } result) yield break;
        var a = criteria.Area;
        var b = result.Area;
        if ((long)b.Row1 + a.Rows > Sheet.RowCount || (long)b.Column1 + a.Columns > Sheet.ColumnCount) yield break;
        yield return Reference.Rectangle(result.SheetName, b.Row1, b.Column1,
            b.Row1 + a.Rows - 1, b.Column1 + a.Columns - 1, true, true, true, true);
    }

    private static Reference? LiteralReference(Node node) => node switch
    {
        ReferenceNode r => r.Reference,
        ParenthesesNode p => LiteralReference(p.Inner),
        _ => null,
    };

    private sealed record Criterion(string Operator, Value? Wanted, bool OnlyAbsent = false)
    {
        private bool DotDecimal { get; init; } = true;

        public static Criterion? Parse(Value? value, CultureInfo culture)
        {
            var dotDecimal = culture.NumberFormat.NumberDecimalSeparator == ".";
            if (!dotDecimal && value is { Kind: ValueKind.Text } text && text.Text.Any(c => c == '.')) return null;
            return ParseValue(value) is { } criterion ? criterion with { DotDecimal = dotDecimal } : null;
        }

        private static Criterion? ParseValue(Value? value)
        {
            if (value is null) return new("=", Value.FromNumber(0));
            if (value is not { Kind: ValueKind.Text } text) return new("=", value);
            var s = text.Text;
            if (s.Length > 255) return null;
            var op = "=";
            if (s.StartsWith("<>", StringComparison.Ordinal) || s.StartsWith("<=", StringComparison.Ordinal) || s.StartsWith(">=", StringComparison.Ordinal))
                (op, s) = (s[..2], s[2..]);
            else if (s.StartsWith('=') || s.StartsWith('<') || s.StartsWith('>')) (op, s) = (s[..1], s[1..]);
            if (s.Length == 0 && text.Text.Length > 0)
                return op is "=" or "<>" ? new(op, null, OnlyAbsent: true) : null;
            foreach (var error in Enum.GetValues<ErrorValue>())
                if (string.Equals(s, error.ToText(), StringComparison.OrdinalIgnoreCase))
                    return op is "=" or "<>" ? new(op, Value.FromError(error)) : null;
            // Criteria numbers are culture-sensitive; only the invariant numeric spelling is
            // admitted until the other cultures have been observed. Unicode text is likewise
            // refused instead of delegating its matching to a platform-dependent collation.
            if (s.Any(c => c > 127 || c is ',' or '/' or ':' or '%' or '$')) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return double.IsFinite(number) && ShortNumericText(s) ? new(op, Value.FromNumber(number)) : null;
            if (bool.TryParse(s, out var boolean)) return op is "=" or "<>" ? new(op, Value.FromBoolean(boolean)) : null;
            return op is "=" or "<>" ? new(op, Value.FromText(s)) : null;
        }

        public bool? Matches(Value? candidate)
        {
            if (OnlyAbsent) return Operator switch { "=" => candidate is null, "<>" => candidate is not null, _ => false };
            var wanted = Wanted!.Value;
            if (wanted.Kind == ValueKind.Number)
            {
                if (candidate is { Kind: ValueKind.Number } n) return Compare(Evaluator.Compare(n, wanted));
                if (Operator == "=" && candidate is { Kind: ValueKind.Text } t)
                {
                    if (t.Text.Any(c => c > 127 || c is ',' or '/' or ':' or '%' or '$')
                        || !DotDecimal && t.Text.Contains('.')) return null;
                    if (double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        return double.IsFinite(number) && ShortNumericText(t.Text)
                            ? Evaluator.Compare(Value.FromNumber(number), wanted) == 0 : null;
                }
                return Operator == "<>";
            }
            if (wanted.Kind is ValueKind.Boolean or ValueKind.Error)
            {
                var same = candidate is { } v && v.Kind == wanted.Kind && v == wanted;
                return Operator == "=" ? same : Operator == "<>" && !same;
            }
            if (candidate is null) return wanted.Text.Length == 0 && Operator == "=" || wanted.Text.Length > 0 && Operator == "<>";
            if (candidate is not { Kind: ValueKind.Text } value) return Operator == "<>";
            if (value.Text.Any(c => c > 127)) return null;
            var sameText = MatchesCriteriaText(wanted.Text, value.Text);
            return Operator == "=" ? sameText : !sameText;
        }

        private bool Compare(int comparison) => Operator switch
        {
            "=" => comparison == 0, "<>" => comparison != 0, "<" => comparison < 0,
            ">" => comparison > 0, "<=" => comparison <= 0, ">=" => comparison >= 0, _ => false,
        };

        private static bool ShortNumericText(string text)
        {
            var mantissa = text.Split('e', 'E')[0];
            return mantissa.Count(char.IsAsciiDigit) <= 15;
        }
    }

    private static bool MatchesCriteriaText(string pattern, string text)
    {
        // Criteria escape '*' and '?' with '~'; an ordinary or repeated tilde stays literal.
        // Keep a last-star position for backtracking without recursion or a regex engine.
        var p = 0; var t = 0; var star = -1; var retry = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*') { star = ++p; retry = t; continue; }
            if (p < pattern.Length)
            {
                var c = pattern[p];
                var size = 1;
                var any = c == '?';
                if (c == '~' && p + 1 < pattern.Length && pattern[p + 1] is '*' or '?') { c = pattern[p + 1]; size = 2; }
                if (any || char.ToUpperInvariant(c) == char.ToUpperInvariant(text[t])) { p += size; t++; continue; }
            }
            if (star < 0) return false;
            p = star; t = ++retry;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }
}
