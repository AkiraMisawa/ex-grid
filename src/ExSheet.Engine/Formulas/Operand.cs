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

    /// <summary>
    /// A 3D Reference (ADR-0331 §7): one rectangle on each Sheet of a span, read as one range, Sheet by
    /// Sheet in tab order, by the functions Excel reads one in (<see cref="ThreeDArgument.Range"/>).
    /// Read as one Value it is an Error Value (<see cref="Evaluator.ScalarOf"/>).
    /// </summary>
    ThreeD,
}

/// <summary>
/// What a node evaluates to before it is used: a Value (or blank), or a rectangle of cells that a
/// function reads as a range. Excel's functions tell the two apart — <c>SUM</c> ignores text in a
/// range but not text typed as an argument.
/// </summary>
internal readonly struct Operand
{
    private Operand(OperandKind kind, Value? scalar, Area area, IReadOnlyList<Value?>? column = null, ValueArray? array = null, bool isSpillRange = false, bool keptBlank = false, ICellReader? cells = null, IReadOnlyList<ICellReader>? sheets = null, bool handedOn = false)
    {
        Kind = kind;
        Sheets = sheets;
        HandedOn = handedOn;
        Cells = cells;
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

    /// <summary>
    /// For <see cref="OperandKind.Area"/>: the Sheet its cells are on, as the recalculation reads it
    /// (ADR-0331). A rectangle derived from one (<c>INDEX</c>, <c>OFFSET</c>) is on the same Sheet.
    /// </summary>
    public ICellReader? Cells { get; }

    /// <summary>For <see cref="OperandKind.ThreeD"/>: the Sheets of the span, in tab order, as the recalculation reads them (ADR-0331 §7).</summary>
    public IReadOnlyList<ICellReader>? Sheets { get; }

    /// <summary>
    /// For <see cref="OperandKind.ThreeD"/>: whether a function handed it on (<c>IF</c>, <c>CHOOSE</c>, a
    /// <c>LET</c> name), rather than its Reference standing where it is read. Read as one Value, one written
    /// there is <c>#REF!</c> and one handed on <c>#VALUE!</c>, as Excel answered (ticket 12).
    /// </summary>
    public bool HandedOn { get; }

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
    /// Whether this holds a kept blank: a blank that <c>UNIQUE</c>, <c>SORT</c>, <c>SORTBY</c>,
    /// <c>FILTER</c> or <c>TRANSPOSE</c> kept from its array (ADR-0125), as one of its Values or as the one
    /// Value <c>INDEX</c> picked from it. The area passes saw Excel's <c>COUNTA</c> count it in
    /// <c>SORT</c>'s and <c>UNIQUE</c>'s array (SORT-021, UNIQUE-017), its <c>ISBLANK</c> find the one
    /// <c>INDEX</c> took out of <c>SORT</c>'s blank (SORT-022), and its <c>=</c> find the one taken out of
    /// <c>UNIQUE</c>'s equal to empty text (UNIQUE-018), which the engine takes for the empty value a blank
    /// cell holds. The other three are taken to keep the same blank, which no run has read. A function
    /// reads such an operand only where the passes read it so (observed), where it gives the same answer
    /// for every reading of it (by agreement: <c>SUMPRODUCT</c>, <c>CONCAT</c>, <c>SUM</c> where both
    /// readings give the one sum, a lookup that never reads it), or where what the passes read is carried
    /// to it (by generalisation, which no run has read: <c>COUNTA</c> over <c>SORTBY</c>'s, <c>FILTER</c>'s
    /// and <c>TRANSPOSE</c>'s array, as over <c>SORT</c>'s; <c>INDEX</c> taking it out of those three, and
    /// out of <c>UNIQUE</c>'s, still blank, as out of <c>SORT</c>'s; <c>ISBLANK</c> beyond <c>SORT</c>'s,
    /// and <c>LEN</c>, <c>ISTEXT</c>, <c>MATCH</c>'s lookup value and every other one Value read through
    /// <see cref="FunctionCall.OneValue"/>, reading it as a blank cell's; every operator reading each Value
    /// so, which <c>=</c> finding <c>UNIQUE</c>'s equal to empty text fits). Such a function takes it
    /// through <see cref="FunctionCall.KeepingBlanks"/> or <see cref="FunctionCall.OneValue"/>, and an
    /// operator reads it as it reads a blank; anywhere else the Formula is refused
    /// (<see cref="FunctionCall.Operand"/>), <c>XLOOKUP</c>'s and <c>XMATCH</c>'s lookup value and
    /// <c>ROUND</c>'s arguments among them.
    /// </summary>
    public bool KeptBlank { get; }

    /// <summary>Whether this is a Reference of cells: a rectangle, or a Spill Range read through <c>A1#</c>.</summary>
    public bool IsReference => Kind == OperandKind.Area || IsSpillRange;

    /// <summary>Whether this is a range a function reads cell by cell: a rectangle of cells, or a Linked Table's column.</summary>
    public bool IsRange => Kind is OperandKind.Area or OperandKind.Column or OperandKind.Array or OperandKind.ThreeD;

    public static Operand Missing { get; } = new(OperandKind.Missing, null, default);

    public static Operand Blank { get; } = new(OperandKind.Scalar, null, default);

    public static Operand Of(Value value) => new(OperandKind.Scalar, value, default);

    public static Operand Of(ErrorValue error) => new(OperandKind.Scalar, Value.FromError(error), default);

    /// <summary>A rectangle of the Sheet <paramref name="cells"/> reads (ADR-0331).</summary>
    public static Operand Of(Area area, ICellReader cells) => new(OperandKind.Area, null, area, cells: cells);

    /// <summary>A 3D Reference's rectangle on each Sheet of its span, <paramref name="sheets"/> in tab order (ADR-0331 §7).</summary>
    public static Operand Of(Area area, IReadOnlyList<ICellReader> sheets) => new(OperandKind.ThreeD, null, area, sheets: sheets);

    /// <summary>This operand as a function hands it on (<see cref="HandedOn"/>): a 3D Reference marked so, anything else as it is.</summary>
    public Operand HandOn() => Kind == OperandKind.ThreeD ? new(OperandKind.ThreeD, null, Area, sheets: Sheets, handedOn: true) : this;

    /// <summary>Another rectangle of the same Sheet as this one, which must be a rectangle.</summary>
    public Operand SameSheet(Area area) => Kind == OperandKind.Area ? Of(area, Cells!) : throw new InvalidOperationException("Not a rectangle of cells.");

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
    /// This operand marked as holding a kept blank (<see cref="KeptBlank"/>), when it holds a blank at
    /// all; as it is otherwise.
    /// </summary>
    public Operand WithKeptBlank() => HoldsBlank ? new(Kind, Scalar, Area, Column, Array, IsSpillRange, keptBlank: true, Cells) : this;
}
