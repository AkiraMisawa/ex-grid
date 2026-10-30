namespace ExGrid.Data;

/// <summary>
/// A source's shape (ADR-0341): its columns' names and kinds, answered before any row, so that a
/// Board Input is checked against it before anything is drawn. A Snapshot answers its own
/// (<see cref="Of(Snapshot)"/>); a source held where the data lives answers from its schema.
/// </summary>
public sealed class DataShape
{
    private readonly Dictionary<string, DataShapeColumn> byName;

    /// <summary>A shape of <paramref name="columns"/>, in order. A column named twice, or with no
    /// name, is refused.</summary>
    public DataShape(IEnumerable<DataShapeColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        Columns = [.. columns];
        byName = new Dictionary<string, DataShapeColumn>(StringComparer.Ordinal);
        foreach (var column in Columns)
        {
            ArgumentException.ThrowIfNullOrEmpty(column.Name, nameof(columns));
            if (!Enum.IsDefined(column.Kind))
                throw new ArgumentOutOfRangeException(nameof(columns), column.Kind, $"Column '{column.Name}' has no kind a Snapshot holds.");
            if (!byName.TryAdd(column.Name, column))
                throw new ArgumentException($"Column '{column.Name}' is named twice in the shape.", nameof(columns));
        }
    }

    /// <summary>The columns, in the source's order.</summary>
    public IReadOnlyList<DataShapeColumn> Columns { get; }

    /// <summary>The column named <paramref name="name"/> (ordinal), when the shape has one.</summary>
    public bool TryGetColumn(string name, out DataShapeColumn column) => byName.TryGetValue(name, out column);

    /// <summary>The shape of <paramref name="snapshot"/>: its columns' names and kinds.</summary>
    public static DataShape Of(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new DataShape(snapshot.Columns.Select(c => new DataShapeColumn(c.Name, c.Kind)));
    }
}

/// <summary>One column of a <see cref="DataShape"/>, or of a Board Input's declaration.</summary>
/// <param name="Name">The column's name, matched ordinally.</param>
/// <param name="Kind">The kind its values are of.</param>
public readonly record struct DataShapeColumn(string Name, SnapshotKind Kind);
