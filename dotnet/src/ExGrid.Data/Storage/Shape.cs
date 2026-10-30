namespace ExGrid.Data.Storage;

/// <summary>A column as declared: the same object in every version of a lineage, which is how a
/// column taken from one version is known in another.</summary>
internal sealed class ColumnShape(string name, string caption, SnapshotKind kind, int ordinal)
{
    public string Name { get; } = name;

    public string Caption { get; } = caption;

    public SnapshotKind Kind { get; } = kind;

    public int Ordinal { get; } = ordinal;
}

/// <summary>
/// What every version of one Snapshot's lineage has in common: its columns, its Record Key, the type
/// of the records it keeps, and its tuning. A Change Batch never changes it.
/// </summary>
internal sealed class Shape
{
    private readonly Dictionary<string, int> ordinals;

    public Shape(IReadOnlyList<(string Name, string Caption, SnapshotKind Kind)> columns, string? key, Type? recordType, SnapshotTuning tuning)
    {
        ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        Columns = new ColumnShape[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var (name, caption, kind) = columns[i];
            if (!ordinals.TryAdd(name, i))
                throw new ArgumentException($"Two columns are named '{name}'; a column's name is unique within a Snapshot.");
            Columns[i] = new ColumnShape(name, caption, kind, i);
        }
        KeyOrdinal = -1;
        if (key is not null)
        {
            if (!ordinals.TryGetValue(key, out var ordinal))
                throw new ArgumentException($"The Record Key names '{key}', which is not a declared column.");
            if (Columns[ordinal].Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
                throw new ArgumentException($"Only a Text or an Integer column can be the Record Key; '{key}' is {Columns[ordinal].Kind}.");
            KeyOrdinal = ordinal;
        }
        RecordType = recordType;
        Tuning = tuning;
    }

    public ColumnShape[] Columns { get; }

    /// <summary>The Record Key's ordinal, or -1 when the lineage has none.</summary>
    public int KeyOrdinal { get; }

    public ColumnShape? Key => KeyOrdinal < 0 ? null : Columns[KeyOrdinal];

    /// <summary>The type of the records kept, or <see langword="null"/> when none are.</summary>
    public Type? RecordType { get; }

    public SnapshotTuning Tuning { get; }

    public bool TryGetOrdinal(string name, out int ordinal) => ordinals.TryGetValue(name, out ordinal);
}
