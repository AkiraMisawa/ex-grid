using System.Globalization;
using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// The records added, the records changed and the Record Keys removed since a Snapshot, applied as
/// one to make the next Snapshot (<see cref="Snapshot.Apply"/>, ADR-0063). The records added and the
/// records changed are small Snapshots with the Snapshot's columns, so any way in — objects, columns,
/// and the readers built on columns — can make one. A changed record is found by its Record Key.
/// </summary>
public sealed class ChangeBatch
{
    private ChangeBatch(Snapshot? added, Snapshot? changed, IReadOnlyList<object> removedKeys)
    {
        Added = added;
        Changed = changed;
        RemovedKeys = removedKeys;
    }

    /// <summary>The records the batch adds; they go at the end of the order.</summary>
    public Snapshot? Added { get; }

    /// <summary>The records the batch changes, each found by its Record Key; each keeps the place of
    /// the record it replaces.</summary>
    public Snapshot? Changed { get; }

    /// <summary>The Record Keys of the records the batch removes: each a <see cref="string"/> for a Text
    /// key, a <see cref="long"/> for an Integer key.</summary>
    public IReadOnlyList<object> RemovedKeys { get; }

    /// <summary>
    /// A batch of <paramref name="added"/> records, <paramref name="changed"/> records and
    /// <paramref name="removedKeys"/>. A key is a <see cref="string"/> for a Text Record Key and an
    /// integer for an Integer one; any integer type is taken, as a <see cref="long"/>. Whether the keys
    /// are the Snapshot's is decided when the batch is applied.
    /// </summary>
    /// <exception cref="ArgumentException">A removed key is <see langword="null"/>, or neither a string
    /// nor an integer; or the added and the changed records do not have the same columns.</exception>
    public static ChangeBatch Of(Snapshot? added = null, Snapshot? changed = null, IReadOnlyList<object>? removedKeys = null)
    {
        if (added is not null && changed is not null)
            SameColumns(added, changed);
        var keys = new object[removedKeys?.Count ?? 0];
        for (var i = 0; i < keys.Length; i++)
        {
            var key = removedKeys![i]
                ?? throw new ArgumentException("A removed key is null; a Record Key is never Blank.", nameof(removedKeys));
            if (key is string)
                keys[i] = key;
            else if (Integers.TryWiden(key, out var integer))
                keys[i] = integer;
            else
                throw new ArgumentException(
                    $"A removed key is a string or an integer; {Convert.ToString(key, CultureInfo.InvariantCulture)} ({key.GetType().Name}) is neither.",
                    nameof(removedKeys));
        }
        return new ChangeBatch(added, changed, Array.AsReadOnly(keys));
    }

    private static void SameColumns(Snapshot added, Snapshot changed)
    {
        foreach (var column in added.Columns)
        {
            if (!changed.TryGetColumn(column.Name, out var other) || other.Kind != column.Kind)
                throw new ArgumentException($"The added and the changed records have different columns: '{column.Name}' is {column.Kind} in the added records and {(other is null ? "missing" : other.Kind.ToString())} in the changed.");
        }
        if (changed.Columns.Count != added.Columns.Count)
        {
            var extra = changed.Columns.First(c => !added.TryGetColumn(c.Name, out _));
            throw new ArgumentException($"The added and the changed records have different columns: '{extra.Name}' is missing from the added records.");
        }
    }
}

/// <summary>
/// What <see cref="Snapshot.Apply"/> made, and what a reader needs to fold the batch into what it
/// computed rather than start again (ADR-0066): subtract what <see cref="Removed"/> held in
/// <see cref="Before"/>, add what <see cref="Added"/> holds in <see cref="After"/>.
/// </summary>
public sealed class SnapshotChange
{
    internal SnapshotChange(Snapshot before, Snapshot after, SnapshotRow[] removed, SnapshotRow[] added, bool compacted)
    {
        Before = before;
        After = after;
        Removed = Array.AsReadOnly(removed);
        Added = Array.AsReadOnly(added);
        Compacted = compacted;
    }

    /// <summary>The Snapshot the batch was applied to, exactly as it was.</summary>
    public Snapshot Before { get; }

    /// <summary>The next Snapshot.</summary>
    public Snapshot After { get; }

    /// <summary>The rows of <see cref="Before"/> that <see cref="After"/> does not hold — the records
    /// removed, and the old versions of the records changed — in slice order.</summary>
    public IReadOnlyList<SnapshotRow> Removed { get; }

    /// <summary>The rows of <see cref="After"/> that <see cref="Before"/> did not hold — the records
    /// added, and the new versions of the records changed — in slice order.</summary>
    public IReadOnlyList<SnapshotRow> Added { get; }

    /// <summary>
    /// Whether a compaction moved rows. <see cref="After"/> holds what it would have held, with the same
    /// values, codes and order, but rows it kept from <see cref="Before"/> may be at new addresses:
    /// once the slices batches made pile up, the rows in them are merged into fewer slices; once the
    /// rows batches made grow large beside the rest, or most stored rows are no longer held, every row
    /// is copied into new slices. A reader that keeps rows by their <see cref="SnapshotRow"/> starts
    /// again from <see cref="After"/>; one that keeps only what it computed folds the change as ever.
    /// </summary>
    public bool Compacted { get; }
}
