namespace ExGrid;

/// <summary>
/// The declared type of a Column. Declared by the Consumer, never inferred from values
/// (ADR-0002) — it decides which filter operators exist (ADR-0023) and, later, the default
/// format. A value whose runtime type contradicts the declaration is refused, not coerced.
/// </summary>
public enum ColumnType
{
    Text,
    Number,
    Date,
    Boolean,
}
