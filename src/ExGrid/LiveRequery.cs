namespace ExGrid;

/// <summary>
/// One row's change since the result was last computed, net of everything gathered meanwhile
/// (ADR-0141): the version the result was computed from, and the version held now, each with its
/// place in the base order. A row added has no old version, a row removed no new one, and a key
/// removed and added again within one gathering has both, at different places.
/// </summary>
internal readonly record struct RowChange<TRow>(
    object Key,
    bool HasOld, TRow Old, long OldOrdinal,
    bool HasNew, TRow New, long NewOrdinal);

/// <summary>
/// What <see cref="LiveRequery.Apply"/> made: the new result and each row's place in the base
/// order beside it — the same arrays as before when no change reached the result — where each
/// change's old version stood in the previous result and its new version stands in this one
/// (−1 for none), and whether the result's sequence moved anywhere.
/// </summary>
internal sealed record LiveRequeryOutcome<TRow>(
    TRow[] Result, long[] Ordinals, int[] OldIndex, int[] NewIndex, bool Moved);

/// <summary>
/// The incremental requery of a live in-memory source (ADR-0141), ported from the prototype that
/// M5 checked (<c>spikes/live-update/Requery/IncrementalQuery.cs</c>; ag-grid's <c>deltaSort</c>
/// on this engine's semantics).
///
/// <para>Each row of the base carries an ordinal that grows with its place in the base order: a
/// changed row keeps its own, an added row takes the next. The result is ordered by (sort keys,
/// ordinal), a strict total order, and that is exactly <see cref="GridQueryEngine.Apply{TRow}"/>'s
/// order: its sort is stable, so ties fall in base order (ADR-0023). So the rows that left —
/// removed rows and the old versions of changed ones — are found in the previous result by binary
/// search on their own keys, which they still have, being immutable (ADR-0003); the rows that
/// entered are filtered and sorted by the engine itself, handed over in ordinal order so its stable
/// sort breaks their ties as the full requery would; and each is placed among the previous result
/// by binary search, and the two merged block by block.</para>
///
/// <para>Whether the sequence moved is answered from the changes alone: it moved when a row left
/// or entered the result, or a changed row stands at another position than its old version stood
/// at. With every change at its old position and nothing in or out, every other row keeps its
/// position too. The changes are paired by Row Key, so a key removed and added again at the same
/// position has not moved the sequence.</para>
///
/// <para>Its result must equal <see cref="GridQueryEngine.Apply{TRow}"/> over the new base exactly,
/// the stable tie order included; a layer-1 property test holds it to that (LV-5).</para>
/// </summary>
internal static class LiveRequery
{
    /// <summary>
    /// Past this share of the base, a batch is requeried whole. Measured on the prototype (M4,
    /// <c>verification/2026-10-05-macos-live-update-measure</c>): the incremental path costs about
    /// 1.2–2.5 µs a changed row and the whole requery a fixed 25–400 ms, so they cross at about a
    /// fifth of the base at 10⁵ rows (13.7 against 25.8 ms at 10,000 changes, 59.6 against 31.3 at
    /// 50,000) and at about a sixth at 10⁶ (250 against 406 ms at 100,000). A sixth switches at or
    /// before the crossing at both sizes, where the two cost about the same; the result is the same
    /// either way, which is why the choice is the source's own (ADR-0141).
    /// </summary>
    public const int WholeRequeryShare = 6;

    /// <summary>
    /// The result after <paramref name="changes"/>, from <paramref name="previous"/> (the result
    /// before them, under the same Query) and its ordinals.
    /// </summary>
    /// <param name="previous">The previous result, in its order.</param>
    /// <param name="previousOrdinals">Each row of <paramref name="previous"/>'s place in the base order.</param>
    /// <param name="changes">The rows that changed since, one entry per Row Key.</param>
    /// <param name="columns">The columns the Query reads.</param>
    /// <param name="filter">The Filter in force.</param>
    /// <param name="sorts">The Sorts in force.</param>
    /// <param name="baseCount">How many rows the base holds now: what decides the whole requery.</param>
    /// <param name="wholeBase">The base now, in its order, with each row's ordinal: asked for only
    /// when the batch is requeried whole.</param>
    public static LiveRequeryOutcome<TRow> Apply<TRow>(
        TRow[] previous,
        long[] previousOrdinals,
        IReadOnlyList<RowChange<TRow>> changes,
        IReadOnlyList<ColumnInfo<TRow>> columns,
        GridFilter? filter,
        IReadOnlyList<SortSpec> sorts,
        int baseCount,
        Func<(TRow[] Rows, long[] Ordinals)> wholeBase)
    {
        var order = GridQueryEngine.OrderOf(columns, sorts);
        var passes = GridQueryEngine.PredicateOf(columns, filter);

        // 1. Where each old version stood in the previous result, if it stood there at all.
        var oldIndex = new int[changes.Count];
        var touchesResult = false;
        var whole = (long)changes.Count * WholeRequeryShare > baseCount;
        for (var j = 0; j < changes.Count; j++)
        {
            var change = changes[j];
            oldIndex[j] = -1;
            if (!change.HasOld)
                continue;
            var at = LowerBound(previous, previousOrdinals, 0, previous.Length, order, order.KeysOf(change.Old), change.OldOrdinal);
            if (at < previous.Length && ReferenceEquals(previous[at], change.Old))
            {
                oldIndex[j] = at;
                touchesResult = true;
            }
            else if (passes?.Invoke(change.Old) ?? true)
            {
                // An old version that passes the Filter was in the previous result, so a search
                // that does not find it means its sort keys are no longer what they were: the row
                // was rewritten in place (ADR-0003). The search over everything else could be off
                // as well, so the base is requeried whole, which reads the rows as they are.
                whole = true;
                touchesResult = true;
            }
        }

        // 2. The new versions, filtered and sorted by the engine itself, handed over in ordinal
        // order so its stable sort breaks their ties as the full requery would.
        var entering = new List<int>(changes.Count);
        for (var j = 0; j < changes.Count; j++)
        {
            if (changes[j].HasNew)
                entering.Add(j);
        }
        entering.Sort((a, b) => changes[a].NewOrdinal.CompareTo(changes[b].NewOrdinal));
        var candidates = new TRow[entering.Count];
        for (var i = 0; i < candidates.Length; i++)
            candidates[i] = changes[entering[i]].New;
        var passed = GridQueryEngine.ApplyPositions(candidates, columns, filter, sorts);
        touchesResult |= passed.Length > 0;

        var newIndex = new int[changes.Count];
        Array.Fill(newIndex, -1);
        if (!touchesResult)
        {
            // No change reached the result: it is the previous one, the same arrays, and the grid
            // has nothing new to paint (ADR-0023's no-op principle).
            return new LiveRequeryOutcome<TRow>(previous, previousOrdinals, oldIndex, newIndex, Moved: false);
        }

        TRow[] result;
        long[] ordinals;
        if (whole)
        {
            var (rows, rowOrdinals) = wholeBase();
            var positions = GridQueryEngine.ApplyPositions(rows, columns, filter, sorts);
            result = new TRow[positions.Length];
            ordinals = new long[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                result[i] = rows[positions[i]];
                ordinals[i] = rowOrdinals[positions[i]];
            }
            foreach (var p in passed)
            {
                var j = entering[p];
                var at = LowerBound(result, ordinals, 0, result.Length, order, order.KeysOf(changes[j].New), changes[j].NewOrdinal);
                if (at < result.Length && ReferenceEquals(result[at], changes[j].New))
                    newIndex[j] = at;
            }
        }
        else
        {
            (result, ordinals) = Merge(previous, previousOrdinals, changes, oldIndex, entering, passed, order, newIndex);
        }

        // 3. Whether the sequence moved, from the changes alone (see the summary).
        var moved = false;
        for (var j = 0; j < changes.Count && !moved; j++)
            moved = oldIndex[j] != newIndex[j];
        return new LiveRequeryOutcome<TRow>(result, ordinals, oldIndex, newIndex, moved);
    }

    private static (TRow[] Result, long[] Ordinals) Merge<TRow>(
        TRow[] previous,
        long[] previousOrdinals,
        IReadOnlyList<RowChange<TRow>> changes,
        int[] oldIndex,
        List<int> entering,
        int[] passed,
        GridQueryEngine.RowOrder<TRow> order,
        int[] newIndex)
    {
        var removedAt = new List<int>(changes.Count);
        foreach (var at in oldIndex)
        {
            if (at >= 0)
                removedAt.Add(at);
        }
        removedAt.Sort();

        // Where each entering row goes among the previous result: monotone, because they are sorted.
        var insertAt = new int[passed.Length];
        var lower = 0;
        for (var i = 0; i < passed.Length; i++)
        {
            var change = changes[entering[passed[i]]];
            lower = LowerBound(previous, previousOrdinals, lower, previous.Length, order, order.KeysOf(change.New), change.NewOrdinal);
            insertAt[i] = lower;
        }

        var count = previous.Length - removedAt.Count + passed.Length;
        var result = new TRow[count];
        var ordinals = new long[count];
        int source = 0, target = 0, r = 0;
        for (var i = 0; i <= passed.Length; i++)
        {
            var until = i < passed.Length ? insertAt[i] : previous.Length;
            // previous[source, until), without the rows that left.
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
            if (i < passed.Length)
            {
                var j = entering[passed[i]];
                result[target] = changes[j].New;
                ordinals[target] = changes[j].NewOrdinal;
                newIndex[j] = target;
                target++;
            }
        }
        if (target != count)
            throw new InvalidOperationException($"The incremental requery wrote {target} of {count} rows (ADR-0141).");
        return (result, ordinals);
    }

    /// <summary>The first position in <paramref name="rows"/>[<paramref name="from"/>,
    /// <paramref name="length"/>) whose (keys, ordinal) is not before the probe's.</summary>
    private static int LowerBound<TRow>(
        TRow[] rows, long[] ordinals, int from, int length,
        GridQueryEngine.RowOrder<TRow> order, object?[] keys, long ordinal)
    {
        int lo = from, hi = length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            var compared = order.Compare(rows[mid], keys);
            if (compared == 0)
                compared = ordinals[mid].CompareTo(ordinal);
            if (compared < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
