namespace ExSheet.Engine.Formulas;

internal enum OperandKind
{
    /// <summary>A Value, or a blank read from one cell.</summary>
    Scalar,

    /// <summary>A rectangle of cells, not yet read.</summary>
    Area,

    /// <summary>An argument left empty.</summary>
    Missing,

    /// <summary>A column of a Linked Table's snapshot, read by name (ADR-0049): a range that is not cells.</summary>
    Column,

    /// <summary>Values a Formula computed as a rectangle (ADR-0125): read as a range, and spilled where it is the result.</summary>
    Array,
}

/// <summary>
/// What a node evaluates to before it is used: a Value (or blank), or a rectangle of cells that a
/// function reads as a range. Excel's functions tell the two apart — <c>SUM</c> ignores text in a
/// range but not text typed as an argument.
/// </summary>
internal readonly struct Operand
{
    private Operand(OperandKind kind, Value? scalar, Area area, IReadOnlyList<Value?>? column = null, ValueArray? array = null)
    {
        Kind = kind;
        Scalar = scalar;
        Area = area;
        Column = column;
        Array = array;
    }

    public OperandKind Kind { get; }

    /// <summary>For <see cref="OperandKind.Scalar"/>: the Value, or <see langword="null"/> for a blank.</summary>
    public Value? Scalar { get; }

    public Area Area { get; }

    /// <summary>For <see cref="OperandKind.Column"/>: the column's Values top to bottom, blank as <see langword="null"/>.</summary>
    public IReadOnlyList<Value?>? Column { get; }

    /// <summary>For <see cref="OperandKind.Array"/>: the computed Values.</summary>
    public ValueArray? Array { get; }

    /// <summary>Whether this is a range a function reads cell by cell: a rectangle of cells, or a Linked Table's column.</summary>
    public bool IsRange => Kind is OperandKind.Area or OperandKind.Column or OperandKind.Array;

    public static Operand Missing { get; } = new(OperandKind.Missing, null, default);

    public static Operand Blank { get; } = new(OperandKind.Scalar, null, default);

    public static Operand Of(Value value) => new(OperandKind.Scalar, value, default);

    public static Operand Of(ErrorValue error) => new(OperandKind.Scalar, Value.FromError(error), default);

    public static Operand Of(Area area) => new(OperandKind.Area, null, area);

    public static Operand Of(IReadOnlyList<Value?> column) => new(OperandKind.Column, null, default, column);

    /// <summary>A computed array; one of a single Value is that Value.</summary>
    public static Operand Of(ValueArray array) => array.IsSingle ? new(OperandKind.Scalar, array[0, 0], default) : new(OperandKind.Array, null, default, null, array);

    public bool IsError => Kind == OperandKind.Scalar && Scalar is { IsError: true };
}
