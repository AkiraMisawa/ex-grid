using ExGrid;

namespace Requery;

/// <summary>
/// Candidate 4's prototype: ag-grid's <c>deltaSort</c> applied to the bundled source's semantics.
/// It keeps the previous result and, for a batch, takes out the rows that left or changed, filters
/// and sorts only the rows that entered or changed, and merges them in. Its result must equal
/// <see cref="GridQueryEngine.Apply{TRow}"/> over the new base exactly, the stable tie order
/// included: ties fall in base order, so every base row carries an ordinal that grows with its
/// position in the base (a replaced row keeps its own, an added row takes the next), and the
/// result is ordered by (sort keys, ordinal) — a strict total order. M5 checks it.
///
/// <para>Only the merge compares rows here, through <see cref="CompareKeys"/>, a transcription of
/// the engine's private comparison (nulls last in both directions, text by
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, numbers as decimal). Filtering and sorting the
/// rows that entered goes through <see cref="GridQueryEngine.Apply{TRow}"/> itself.</para>
/// </summary>
public sealed class IncrementalQuery
{
    private readonly IReadOnlyList<ColumnInfo<Trade>> _columns;
    private readonly GridFilter? _filter;
    private readonly IReadOnlyList<SortSpec> _sorts;
    private readonly (Func<Trade, object?> Value, ColumnType Type, SortDirection Direction)[] _levels;
    private readonly Dictionary<Trade, long> _ordinalOf;
    private long _nextOrdinal;
    private Trade[] _result;
    private long[] _resultOrdinals;

    public IncrementalQuery(IReadOnlyList<Trade> rows, IReadOnlyList<ColumnInfo<Trade>> columns, GridFilter? filter, IReadOnlyList<SortSpec> sorts)
    {
        _columns = columns;
        _filter = filter;
        _sorts = sorts;
        _levels = sorts.Select(s =>
        {
            var column = columns.Single(c => c.Name == s.Column);
            return (column.Value, column.Type, s.Direction);
        }).ToArray();
        _ordinalOf = new Dictionary<Trade, long>(rows.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < rows.Count; i++)
            _ordinalOf.Add(rows[i], i);
        _nextOrdinal = rows.Count;
        _result = [.. GridQueryEngine.Apply(rows, columns, filter, sorts)];
        _resultOrdinals = new long[_result.Length];
        for (var i = 0; i < _result.Length; i++)
            _resultOrdinals[i] = _ordinalOf[_result[i]];
    }

    private IncrementalQuery(IncrementalQuery other)
    {
        _columns = other._columns;
        _filter = other._filter;
        _sorts = other._sorts;
        _levels = other._levels;
        _ordinalOf = new Dictionary<Trade, long>(other._ordinalOf, ReferenceEqualityComparer.Instance);
        _nextOrdinal = other._nextOrdinal;
        _result = other._result;
        _resultOrdinals = other._resultOrdinals;
    }

    /// <summary>A copy whose next batch does not touch this one's state (the result arrays are
    /// never written in place, so they are shared).</summary>
    public IncrementalQuery Clone() => new(this);

    /// <summary>The result: the Window a source would hand the grid.</summary>
    public IReadOnlyList<Trade> Window => _result;

    /// <summary>
    /// Folds <paramref name="batch"/> in. Returns whether the visible sequence moved, under the
    /// definition <c>InMemoryGridSource.ReplaceRow</c> uses, extended to a batch: the sequence is the
    /// same when every position holds the row it held, or that row's replacement.
    /// </summary>
    public bool Apply(RowBatch batch)
    {
        // 1. Ordinals: a replacement keeps its row's, an added row takes the next.
        var leaving = new List<(Trade Row, long Ordinal, int Pair)>(batch.Replaced.Count + batch.Removed.Count);
        var entering = new List<(Trade Row, long Ordinal, int Pair)>(batch.Replaced.Count + batch.Added.Count);
        for (var j = 0; j < batch.Replaced.Count; j++)
        {
            var (old, replacement) = batch.Replaced[j];
            if (!_ordinalOf.Remove(old, out var ordinal))
                throw new ArgumentException($"Row {old} is not in the base.");
            _ordinalOf.Add(replacement, ordinal);
            leaving.Add((old, ordinal, j));
            entering.Add((replacement, ordinal, j));
        }
        foreach (var removed in batch.Removed)
        {
            if (!_ordinalOf.Remove(removed, out var ordinal))
                throw new ArgumentException($"Row {removed} is not in the base.");
            leaving.Add((removed, ordinal, -1));
        }
        foreach (var added in batch.Added)
        {
            var ordinal = _nextOrdinal++;
            _ordinalOf.Add(added, ordinal);
            entering.Add((added, ordinal, -1));
        }

        var previous = _result;
        var previousOrdinals = _resultOrdinals;

        // 2. Where each leaving row stood in the previous result, if it stood there at all: a
        // binary search by its own (keys, ordinal), which it still has, being immutable.
        var oldIndexOfPair = new int[batch.Replaced.Count];
        Array.Fill(oldIndexOfPair, -1);
        var removedAt = new List<int>(leaving.Count);
        var unpairedLeft = false;
        foreach (var (row, ordinal, pair) in leaving)
        {
            var at = LowerBound(previous, previousOrdinals, previous.Length, row, ordinal);
            if (at < previous.Length && ReferenceEquals(previous[at], row))
            {
                removedAt.Add(at);
                if (pair >= 0)
                    oldIndexOfPair[pair] = at;
                else
                    unpairedLeft = true;
            }
        }
        removedAt.Sort();

        // 3. The entering rows filtered and sorted by the engine itself: handed over in ordinal
        // order, so that its stable sort breaks their ties as the full requery would.
        entering.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));
        var candidates = new Trade[entering.Count];
        for (var i = 0; i < candidates.Length; i++)
            candidates[i] = entering[i].Row;
        var passed = GridQueryEngine.Apply(candidates, _columns, _filter, _sorts);
        var ordinalOfEntering = new Dictionary<Trade, (long Ordinal, int Pair)>(entering.Count, ReferenceEqualityComparer.Instance);
        foreach (var (row, ordinal, pair) in entering)
            ordinalOfEntering.Add(row, (ordinal, pair));

        // 4. Where each one goes among the previous result: monotone, because they are sorted.
        var insertAt = new int[passed.Count];
        var lower = 0;
        for (var i = 0; i < passed.Count; i++)
        {
            var (ordinal, _) = ordinalOfEntering[passed[i]];
            lower = LowerBound(previous, previousOrdinals, previous.Length, passed[i], ordinal, lower);
            insertAt[i] = lower;
        }

        // 5. Merge, block by block.
        var count = previous.Length - removedAt.Count + passed.Count;
        var result = new Trade[count];
        var ordinals = new long[count];
        var newIndexOfPair = new int[batch.Replaced.Count];
        Array.Fill(newIndexOfPair, -1);
        var unpairedEntered = false;
        int source = 0, target = 0, r = 0;
        for (var i = 0; i <= passed.Count; i++)
        {
            var until = i < passed.Count ? insertAt[i] : previous.Length;
            // Copy previous[source, until), skipping the removed positions.
            while (source < until)
            {
                if (r < removedAt.Count && removedAt[r] == source)
                {
                    source++;
                    r++;
                    continue;
                }
                var stop = r < removedAt.Count && removedAt[r] < until ? removedAt[r] : until;
                var length = stop - source;
                Array.Copy(previous, source, result, target, length);
                Array.Copy(previousOrdinals, source, ordinals, target, length);
                target += length;
                source = stop;
            }
            if (i < passed.Count)
            {
                var row = passed[i];
                var (ordinal, pair) = ordinalOfEntering[row];
                result[target] = row;
                ordinals[target] = ordinal;
                if (pair >= 0)
                    newIndexOfPair[pair] = target;
                else
                    unpairedEntered = true;
                target++;
            }
        }
        // Removed positions at the very end are skipped by the loop above; anything else left
        // over would be a defect of this prototype.
        if (target != count)
            throw new InvalidOperationException($"Merge wrote {target} of {count} rows.");

        _result = result;
        _resultOrdinals = ordinals;

        // 6. Whether the sequence moved, from the k pairs alone (see the README for the argument).
        if (unpairedLeft || unpairedEntered)
            return true;
        for (var j = 0; j < oldIndexOfPair.Length; j++)
        {
            if (oldIndexOfPair[j] != newIndexOfPair[j])
                return true;
        }
        return false;
    }

    private int LowerBound(Trade[] rows, long[] ordinals, int length, Trade probe, long probeOrdinal, int from = 0)
    {
        int lo = from, hi = length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (Compare(rows[mid], ordinals[mid], probe, probeOrdinal) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private int Compare(Trade a, long ordinalA, Trade b, long ordinalB)
    {
        foreach (var (value, type, direction) in _levels)
        {
            var result = CompareKeys(type, Normalize(type, value(a)), Normalize(type, value(b)), direction);
            if (result != 0)
                return result;
        }
        return Fault == "ties" ? ordinalB.CompareTo(ordinalA) : ordinalA.CompareTo(ordinalB);
    }

    /// <summary>A defect planted on purpose (REQUERY_FAULT = nulls-first, case, culture, ties), to
    /// show that M5 would catch it. Unset for every result reported.</summary>
    public static readonly string? Fault = Environment.GetEnvironmentVariable("REQUERY_FAULT");

    /// <summary>The engine's <c>CompareKeys</c> and <c>CompareCells</c>, transcribed: blanks last
    /// in both directions, outside the direction's inversion.</summary>
    public static int CompareKeys(ColumnType type, object? x, object? y, SortDirection direction)
    {
        if (x is null || y is null)
            return Fault == "nulls-first" ? (y is null ? 1 : 0) - (x is null ? 1 : 0) : (x is null ? 1 : 0) - (y is null ? 1 : 0);
        var result = type switch
        {
            ColumnType.Text when Fault == "case" => StringComparer.Ordinal.Compare((string)x, (string)y),
            ColumnType.Text when Fault == "culture" => StringComparer.InvariantCultureIgnoreCase.Compare((string)x, (string)y),
            ColumnType.Text => StringComparer.OrdinalIgnoreCase.Compare((string)x, (string)y),
            ColumnType.Number => ((decimal)x).CompareTo((decimal)y),
            ColumnType.Date => ((IComparable)x).CompareTo(y),
            ColumnType.Boolean => ((bool)x).CompareTo((bool)y),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        return direction == SortDirection.Ascending ? result : -Math.Sign(result);
    }

    /// <summary>The engine's normalisation, for the value types this spike's columns return.</summary>
    private static object? Normalize(ColumnType type, object? value) => value switch
    {
        null => null,
        int i when type == ColumnType.Number => (decimal)i,
        _ => value,
    };
}
