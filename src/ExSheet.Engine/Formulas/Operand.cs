namespace ExSheet.Engine.Formulas;

internal enum OperandKind
{
    /// <summary>A Value, or a blank read from one cell.</summary>
    Scalar,

    /// <summary>A rectangle of cells, not yet read.</summary>
    Area,

    /// <summary>An argument left empty.</summary>
    Missing,
}

/// <summary>
/// What a node evaluates to before it is used: a Value (or blank), or a rectangle of cells that a
/// function reads as a range. Excel's functions tell the two apart — <c>SUM</c> ignores text in a
/// range but not text typed as an argument.
/// </summary>
internal readonly struct Operand
{
    private Operand(OperandKind kind, Value? scalar, Area area)
    {
        Kind = kind;
        Scalar = scalar;
        Area = area;
    }

    public OperandKind Kind { get; }

    /// <summary>For <see cref="OperandKind.Scalar"/>: the Value, or <see langword="null"/> for a blank.</summary>
    public Value? Scalar { get; }

    public Area Area { get; }

    public static Operand Missing { get; } = new(OperandKind.Missing, null, default);

    public static Operand Blank { get; } = new(OperandKind.Scalar, null, default);

    public static Operand Of(Value value) => new(OperandKind.Scalar, value, default);

    public static Operand Of(ErrorValue error) => new(OperandKind.Scalar, Value.FromError(error), default);

    public static Operand Of(Area area) => new(OperandKind.Area, null, area);

    public bool IsError => Kind == OperandKind.Scalar && Scalar is { IsError: true };
}
