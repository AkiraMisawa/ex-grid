namespace ExSheet.Engine.Formulas;

internal static partial class FunctionLibrary
{
    private static Operand VLookup(FunctionCall call) => LegacyLookup(call, vertical: true);

    private static Operand HLookup(FunctionCall call) => LegacyLookup(call, vertical: false);

    /// <summary>
    /// VLOOKUP and HLOOKUP: first exact match, or the last smaller/equal key in an ascending
    /// table. Unlike INDEX, their result is a Value, and a blank target becomes numeric zero.
    /// </summary>
    private static Operand LegacyLookup(FunctionCall call, bool vertical)
    {
        if (!TryScalar(call, 0, out var lookup, out var failure)) return failure;
        var table = ArrayOrValue(call.Operand(1));
        if (!table.IsRange) return table.IsError ? table : Operand.Of(ErrorValue.Value);
        if (!TryNumber(call, 2, out var index, out failure)) return failure;
        index = Math.Truncate(index);
        if (index < 1) return Operand.Of(ErrorValue.Value);
        var (rows, columns) = table.Kind switch
        {
            OperandKind.Area => (table.Area.Rows, table.Area.Columns),
            OperandKind.Array => (table.Array!.Rows, table.Array.Columns),
            _ => (table.Column!.Count, 1),
        };
        if (index > (vertical ? columns : rows)) return Operand.Of(ErrorValue.Ref);
        var mode = 1.0;
        if (call.Count > 3 && !TryNumber(call, 3, out mode, out failure)) return failure;

        var keys = LegacyTableLine(table, vertical, 0);
        var outcome = LegacySearch(call.Evaluator, keys, lookup, mode == 0 ? 0 : 1);
        if (outcome.Failure is { } error) return error;
        if (outcome.Found is not { } found) return Operand.Of(ErrorValue.NA);
        var result = LegacyTableLine(table, vertical, (int)index - 1).ItemAt(found);
        return Operand.Of(call.Evaluator.ScalarOf(result) ?? Value.FromNumber(0));
    }

    /// <summary>The indicated column or row of a table, retaining References and sparse ranges.</summary>
    private static Vector LegacyTableLine(Operand table, bool vertical, int index)
    {
        if (table.Kind == OperandKind.Area)
        {
            var area = table.Area;
            var line = vertical
                ? new Area(area.Row1, area.Column1 + index, area.Row2, area.Column1 + index)
                : new Area(area.Row1 + index, area.Column1, area.Row1 + index, area.Column2);
            return new Vector(Operand.Of(line), vertical, vertical ? area.Rows : area.Columns);
        }
        if (table.Kind == OperandKind.Column)
        {
            return vertical ? new Vector(table, true, table.Column!.Count)
                : new Vector(Operand.AsArray(table.Column![index]), false, 1);
        }
        var array = table.Array!;
        var slice = vertical ? Slice(array, 0, array.Rows - 1, index, index) : Slice(array, index, index, 0, array.Columns - 1);
        return new Vector(slice.IsSingle
            ? Operand.AsArray(slice[0, 0]) : Operand.Of(slice), vertical, vertical ? array.Rows : array.Columns);
    }

    /// <summary>MATCH's position, from 1; descending approximate matches take the first equal key.</summary>
    private static Operand Match(FunctionCall call)
    {
        if (!TryScalar(call, 0, out var lookup, out var failure)) return failure;
        var array = ArrayOrValue(call.Operand(1));
        if (!array.IsRange) return array.IsError ? array : Operand.Of(ErrorValue.Value);
        if (Vector.Of(array) is not { } vector) return Operand.Of(ErrorValue.Value);
        var mode = 1.0;
        if (call.Count > 2 && !TryNumber(call, 2, out mode, out failure)) return failure;
        var outcome = LegacySearch(call.Evaluator, vector, lookup, Math.Sign(mode));
        if (outcome.Failure is { } error) return error;
        return outcome.Found is { } found ? Operand.Of(Value.FromNumber(found + 1)) : Operand.Of(ErrorValue.NA);
    }

    /// <summary>
    /// The searches shared by VLOOKUP, HLOOKUP and MATCH. Approximate searches retain ADR-0047's
    /// refusal of data that cannot be established as sorted. They never use XLOOKUP's distinct
    /// duplicate-key rules.
    /// </summary>
    private static SearchOutcome LegacySearch(Evaluator evaluator, Vector vector, Value? lookup, int mode)
    {
        var wanted = lookup ?? Value.FromNumber(0);
        if (wanted.Kind == ValueKind.Text && wanted.Text.Length > 255)
            return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        var candidates = vector.Candidates(evaluator).ToList();
        if (mode == 0) return LegacyExact(candidates, wanted);
        if (candidates.Count != vector.Length || candidates.Any(c => c.Value.IsError))
            return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        var kind = candidates.Count == 0 ? wanted.Kind : candidates[0].Value.Kind;
        if (candidates.Any(c => c.Value.Kind != kind || !LegacyOrderable(c.Value)))
            return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        for (var i = 1; i < candidates.Count; i++)
        {
            var order = Evaluator.Compare(candidates[i - 1].Value, candidates[i].Value);
            if (mode > 0 ? order > 0 : order < 0)
                return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        }
        if (wanted.Kind != kind) return new SearchOutcome(null, null);
        if (!LegacyOrderable(wanted)) return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        int? found = null;
        foreach (var (index, candidate) in candidates)
        {
            var order = Evaluator.Compare(candidate, wanted);
            if (mode > 0 ? order > 0 : order < 0) break;
            found = index;
            if (mode < 0 && order == 0) break;
        }
        // MATCH-018 pins the first equal key descending. No observation yet pins which
        // duplicate wins when a descending search finds the next larger key instead.
        if (mode < 0 && found is { } at && at > 0
            && Evaluator.Compare(candidates[at].Value, wanted) != 0
            && Evaluator.Compare(candidates[at - 1].Value, candidates[at].Value) == 0)
            return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        return new SearchOutcome(found, null);
    }

    private static bool LegacyOrderable(Value value) =>
        value.Kind is ValueKind.Number or ValueKind.Boolean
        || value.Kind == ValueKind.Text && value.Text.All(c => char.IsAsciiLetterOrDigit(c) || c == ' ');

    /// <summary>
    /// Exact legacy matching, including wildcards, is observed for ASCII text only. The Windows
    /// error-key cases establish #N/A for an absent key with #N/A in the vector; they do not pin
    /// whether a later matching key wins over an earlier Error Value. Refuse that unobserved
    /// outcome rather than borrow XLOOKUP's error-skipping rule.
    /// </summary>
    private static SearchOutcome LegacyExact(IEnumerable<(int Index, Value Value)> candidates, Value wanted)
    {
        if (wanted.Kind == ValueKind.Text && !wanted.Text.All(char.IsAscii))
            return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
        var errorSeen = false;
        var unobservedError = false;
        foreach (var (index, candidate) in candidates)
        {
            if (candidate.IsError)
            {
                errorSeen = true;
                unobservedError |= candidate.Error != ErrorValue.NA;
                continue;
            }
            if (candidate.Kind != wanted.Kind) continue;
            if (candidate.Kind == ValueKind.Text && !candidate.Text.All(char.IsAscii))
                return new SearchOutcome(null, Operand.Of(ErrorValue.Value));
            var matches = wanted.Kind == ValueKind.Text
                ? Wildcard.Matches(wanted.Text, candidate.Text) : SameKindEqual(candidate, wanted);
            if (matches) return errorSeen ? new SearchOutcome(null, Operand.Of(ErrorValue.Value)) : new SearchOutcome(index, null);
        }
        return new SearchOutcome(null, unobservedError ? Operand.Of(ErrorValue.Value) : null);
    }
}
