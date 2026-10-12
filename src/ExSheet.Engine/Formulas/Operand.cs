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
    private Operand(OperandKind kind, Value? scalar, Area area, IReadOnlyList<Value?>? column = null, ValueArray? array = null, bool isSpillRange = false, bool keptBlank = false)
    {
        Kind = kind;
        Scalar = scalar;
        Area = area;
        Column = column;
        Array = array;
        IsSpillRange = isSpillRange;
        KeptBlank = keptBlank;
    }

    public OperandKind Kind { get; }

    /// <summary>For <see cref="OperandKind.Scalar"/>: the Value, or <see langword="null"/> for a blank.</summary>
    public Value? Scalar { get; }

    public Area Area { get; }

    /// <summary>For <see cref="OperandKind.Column"/>: the column's Values top to bottom, blank as <see langword="null"/>.</summary>
    public IReadOnlyList<Value?>? Column { get; }

    /// <summary>For <see cref="OperandKind.Array"/>: the computed Values.</summary>
    public ValueArray? Array { get; }

    /// <summary>
    /// For <see cref="OperandKind.Array"/>: whether the Values are a Spill Range's, read through
    /// <c>A1#</c> (ADR-0125). It is a Reference to cells in Excel, and a function whose answer
    /// differs between a Reference and an array reads it as a Reference (<c>INDEX</c>).
    /// </summary>
    public bool IsSpillRange { get; }

    /// <summary>
    /// Whether this holds a blank that <c>UNIQUE</c>, <c>SORT</c> or <c>SORTBY</c> kept from its array
    /// (ADR-0125): one of its Values, or the one Value <c>INDEX</c> picked from it. Excel's is the empty
    /// value a blank cell holds (SORT-022), which <c>COUNTA</c> counts in the array (SORT-021,
    /// UNIQUE-017). A function reads such an operand only where the area passes settled how
    /// (<see cref="FunctionCall.KeepingBlanks"/>, <see cref="FunctionCall.OneValue"/>); anywhere else
    /// the Formula is refused (<see cref="FunctionCall.Operand"/>).
    /// </summary>
    public bool KeptBlank { get; }

    /// <summary>Whether this is a Reference of cells: a rectangle, or a Spill Range read through <c>A1#</c>.</summary>
    public bool IsReference => Kind == OperandKind.Area || IsSpillRange;

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

    /// <summary>A Spill Range's Values, read through <c>A1#</c>: an array that is a Reference (<see cref="IsSpillRange"/>).</summary>
    public static Operand OfSpillRange(ValueArray array) => new(OperandKind.Array, null, default, null, array, isSpillRange: true);

    /// <summary>
    /// One Value as an array of one, for a function that takes an array and is given what a
    /// computation reduced to one Value: <c>(T[K]="a")</c> over a table of one row (ADR-0125).
    /// </summary>
    public static Operand AsArray(Value? value) => new(OperandKind.Array, null, default, null, ValueArray.Single(value));

    public bool IsError => Kind == OperandKind.Scalar && Scalar is { IsError: true };

    /// <summary>Whether this is a blank, or an array that holds one.</summary>
    public bool HoldsBlank => Kind switch
    {
        OperandKind.Scalar => Scalar is null,
        OperandKind.Array => Array!.All().Any(v => v is null),
        _ => false,
    };

    /// <summary>
    /// This operand marked as holding a blank <c>UNIQUE</c>, <c>SORT</c> or <c>SORTBY</c> kept from its
    /// array (<see cref="KeptBlank"/>), when it holds a blank at all; as it is otherwise.
    /// </summary>
    public Operand WithKeptBlank() => HoldsBlank ? new(Kind, Scalar, Area, Column, Array, IsSpillRange, keptBlank: true) : this;
}
