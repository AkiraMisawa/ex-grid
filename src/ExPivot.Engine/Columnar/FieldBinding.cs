using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>How a bound column's values are read (ADR-0059/0063).</summary>
internal enum ValueRole : byte
{
    /// <summary>A Text column: codes into its dictionary, folded ignoring case into Items.</summary>
    Text,

    /// <summary>A Decimal column: exact numbers, scaled per segment or held as <c>decimal</c>.</summary>
    Exact,

    /// <summary>An Integer column: exact 64-bit integers.</summary>
    Integer,

    /// <summary>A Double column: numbers in <c>double</c>, a non-finite one <c>#NUM!</c>.</summary>
    Double,

    /// <summary>A Date column: clock ticks.</summary>
    Date,

    /// <summary>A Boolean column.</summary>
    Boolean,
}

/// <summary>One Snapshot column a field reads, and how.</summary>
internal readonly struct BoundColumn(SnapshotColumn column, ValueRole role)
{
    public SnapshotColumn Column { get; } = column;

    public ValueRole Role { get; } = role;

    public int Ordinal => Column.Ordinal;

    public bool IsNumber => Role is ValueRole.Exact or ValueRole.Integer or ValueRole.Double;
}

/// <summary>
/// How a Pivot Field reads its values from a Snapshot: one column — the field's own, or the Date
/// column a date part is a part of — or, for a field read through an untyped accessor, one column
/// per kind of value the accessor returned, a row's value standing in exactly one of them (or in
/// none, for a Blank). The second is how a field "whose values are not all of the declared type is
/// still pivoted as its values are" (ADR-0059): each value keeps its own kind.
/// </summary>
internal sealed class FieldBinding
{
    public FieldBinding(PivotField field, BoundColumn[] columns, PivotDatePart? part)
    {
        Field = field;
        Columns = columns;
        Part = part;
        Single = columns.Length == 1 ? columns[0] : null;
    }

    public PivotField Field { get; }

    public string Name => Field.Name;

    public BoundColumn[] Columns { get; }

    /// <summary>The one column, when the field reads one.</summary>
    public BoundColumn? Single { get; }

    /// <summary>The part of the Date column the field is, or null.</summary>
    public PivotDatePart? Part { get; }

    public static ValueRole RoleOf(SnapshotKind kind) => kind switch
    {
        SnapshotKind.Text => ValueRole.Text,
        SnapshotKind.Decimal => ValueRole.Exact,
        SnapshotKind.Integer => ValueRole.Integer,
        SnapshotKind.Double => ValueRole.Double,
        SnapshotKind.Date => ValueRole.Date,
        SnapshotKind.Boolean => ValueRole.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown SnapshotKind."),
    };

    public static PivotFieldType TypeOf(SnapshotKind kind) => kind switch
    {
        SnapshotKind.Text => PivotFieldType.Text,
        SnapshotKind.Decimal or SnapshotKind.Integer or SnapshotKind.Double => PivotFieldType.Number,
        SnapshotKind.Date => PivotFieldType.Date,
        SnapshotKind.Boolean => PivotFieldType.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown SnapshotKind."),
    };

    /// <summary>
    /// The fields of a Snapshot: <paramref name="fields"/> each bound to the column it names — its
    /// <see cref="PivotField.Column"/>, or its own name — or, with none declared, one field per
    /// column, captioned as the column is and typed by its kind (ADR-0065). An unknown column is
    /// refused by name, and so is a date part of a column that is not a Date column.
    /// </summary>
    public static (PivotField[] Fields, Dictionary<string, FieldBinding> Bindings) Of(Snapshot snapshot, IReadOnlyList<PivotField>? fields)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        PivotField[] declared;
        if (fields is null)
        {
            declared = [.. snapshot.Columns.Select(DefaultField)];
        }
        else
        {
            PivotSource.Declared(fields);
            declared = [.. fields];
        }
        var bindings = new Dictionary<string, FieldBinding>(declared.Length, StringComparer.Ordinal);
        foreach (var field in declared)
        {
            var name = field.Column ?? field.Name;
            if (!snapshot.TryGetColumn(name, out var column))
            {
                throw new ArgumentException(field.Column is null
                    ? $"The Snapshot has no column named '{name}' for the Pivot Field '{field.Name}'."
                    : $"The Pivot Field '{field.Name}' reads the column '{name}', and the Snapshot has no column of that name.", nameof(fields));
            }
            if (field.DatePart is { } part && column.Kind != SnapshotKind.Date)
                throw new ArgumentException($"The Pivot Field '{field.Name}' is the {part} of '{name}', which is a {column.Kind} column, not a Date column.", nameof(fields));
            bindings.Add(field.Name, new FieldBinding(field, [new BoundColumn(column, RoleOf(column.Kind))], field.DatePart));
        }
        return (declared, bindings);
    }

    private static PivotField DefaultField(SnapshotColumn column)
        => new(column.Name, TypeOf(column.Kind), caption: column.Caption.Length == 0 || column.Caption == column.Name ? null : column.Caption);
}
