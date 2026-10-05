namespace ExGrid;

/// <summary>
/// A Change Batch for <c>GridSource.From</c> with a Row Key (ADR-0141): the rows added, the rows
/// changed and the Row Keys removed since the source's last version, applied as one by
/// <see cref="InMemoryGridSource{TRow}.Apply(GridChangeBatch{TRow})"/>. A changed row is found by
/// its key and keeps the place in the base order of the row it replaces; an added row goes at the
/// end of the base order. The grid shows the version before the batch or the one after it, never
/// half of it.
///
/// <para>The rows are the Consumer's own objects (ADR-0140). A changed row is a <b>new instance</b>
/// under the same key: identity, not mutation, is the change signal (ADR-0003). Whether the keys
/// are the source's is decided when the batch is applied; here the lists are copied, so a list the
/// Consumer goes on changing cannot reach a batch already handed over.</para>
///
/// <para>Named apart from <c>ExGrid.Data.ChangeBatch</c>, which makes a Snapshot's next Snapshot
/// by Record Key: this one is a grid source's rows, by Row Key.</para>
/// </summary>
public sealed class GridChangeBatch<TRow>
{
    /// <summary>A batch of <paramref name="added"/> rows, <paramref name="changed"/> rows and
    /// <paramref name="removedKeys"/>; any of them may be left out.</summary>
    /// <exception cref="ArgumentException">A row is null, or a removed key is null: a Row Key never
    /// is (ADR-0140).</exception>
    public GridChangeBatch(
        IReadOnlyList<TRow>? added = null,
        IReadOnlyList<TRow>? changed = null,
        IReadOnlyList<object>? removedKeys = null)
    {
        Added = Copy(added, nameof(added));
        Changed = Copy(changed, nameof(changed));
        var keys = new object[removedKeys?.Count ?? 0];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = removedKeys![i]
                ?? throw new ArgumentException(
                    $"Removed key {i} is null. A Row Key names a row, and no row is named by nothing (ADR-0140).",
                    nameof(removedKeys));
        }
        RemovedKeys = Array.AsReadOnly(keys);
    }

    /// <summary>The rows the batch adds. They go at the end of the base order, in this order.</summary>
    public IReadOnlyList<TRow> Added { get; }

    /// <summary>The rows the batch changes, each a new instance found by its Row Key; each keeps
    /// the place in the base order of the row it replaces.</summary>
    public IReadOnlyList<TRow> Changed { get; }

    /// <summary>The Row Keys of the rows the batch removes.</summary>
    public IReadOnlyList<object> RemovedKeys { get; }

    /// <summary>Whether the batch says nothing: no row added, changed or removed.</summary>
    public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && RemovedKeys.Count == 0;

    private static IReadOnlyList<TRow> Copy(IReadOnlyList<TRow>? rows, string name)
    {
        var copy = new TRow[rows?.Count ?? 0];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = rows![i]
                ?? throw new ArgumentException($"Row {i} of {name} is null; a batch carries rows.", name);
        }
        return Array.AsReadOnly(copy);
    }
}
