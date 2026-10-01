using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// A column of a Snapshot: its name, unique within the Snapshot, its caption and its kind
/// (ADR-0063). A column read from one version may be handed to the slices of any other version of
/// the same Snapshot — the ones a Change Batch made from it, or it from — and means the same column
/// there; handed to another Snapshot's, it is refused.
/// </summary>
public abstract class SnapshotColumn
{
    private protected SnapshotColumn(ColumnShape shape) => Shape = shape;

    /// <summary>The column's name, unique within the Snapshot and told apart ordinally.</summary>
    public string Name => Shape.Name;

    /// <summary>The column's caption: the data's own label for it, which a reader shows by default.
    /// It is the name unless one was declared.</summary>
    public string Caption => Shape.Caption;

    /// <summary>The kind every value of the column is, when it is not a Blank.</summary>
    public SnapshotKind Kind => Shape.Kind;

    /// <summary>The column's place among <see cref="Snapshot.Columns"/>, from zero.</summary>
    public int Ordinal => Shape.Ordinal;

    internal ColumnShape Shape { get; }

    /// <summary>The column's name and kind.</summary>
    public override string ToString() => $"{Name} ({Kind})";
}

/// <summary>
/// A Text column. Each row holds a code into <see cref="Dictionary"/>, which holds every distinct
/// value once, exactly as written; a Blank is the code -1 (ADR-0063).
/// </summary>
public sealed class TextColumn : SnapshotColumn
{
    internal TextColumn(ColumnShape shape, TextStore store, int count)
        : base(shape)
        => Dictionary = new TextDictionary(store, count);

    /// <summary>
    /// The column's dictionary as of this version: in the order each value first appeared when the
    /// Snapshot was built, and then in the order Change Batches brought new ones. It only grows, so a
    /// code means the same text in every version, and text no record carries any more stays in it.
    /// </summary>
    public TextDictionary Dictionary { get; }
}

/// <summary>
/// A Decimal column, held exactly: in each slice as scaled 64-bit integers when every value fits at
/// one power of ten, and as <see cref="decimal"/> otherwise (<see cref="DecimalValues"/>). It holds
/// values, not the scale each was written with, so <c>1.5</c> and <c>1.50</c> read back alike.
/// </summary>
public sealed class DecimalColumn : SnapshotColumn
{
    internal DecimalColumn(ColumnShape shape)
        : base(shape)
    {
    }
}

/// <summary>A Double column, holding each value as it came, non-finite ones included; what they
/// mean is the reader's to say.</summary>
public sealed class DoubleColumn : SnapshotColumn
{
    internal DoubleColumn(ColumnShape shape)
        : base(shape)
    {
    }
}

/// <summary>An Integer column: 64-bit integers.</summary>
public sealed class IntegerColumn : SnapshotColumn
{
    internal IntegerColumn(ColumnShape shape)
        : base(shape)
    {
    }
}

/// <summary>
/// A Date column, holding each value as the clock value it shows, in ticks: a <see cref="DateTime"/>'s
/// ticks with its <see cref="DateTime.Kind"/> ignored, a <see cref="DateOnly"/>'s midnight, a
/// <see cref="DateTimeOffset"/>'s clock with its offset dropped (ADR-0063).
/// </summary>
public sealed class DateColumn : SnapshotColumn
{
    private const int Unknown = 0;
    private const int Midnights = 1;
    private const int Times = 2;

    private readonly Snapshot owner;
    private int hasTime;

    internal DateColumn(ColumnShape shape, Snapshot owner)
        : base(shape)
        => this.owner = owner;

    /// <summary>Whether any value this version holds is not a midnight — whether the column holds
    /// times as well as dates.</summary>
    public bool HasTime
    {
        get
        {
            var known = Volatile.Read(ref hasTime);
            if (known == Unknown)
            {
                known = owner.AnyTime(Ordinal) ? Times : Midnights;
                Volatile.Write(ref hasTime, known);
            }
            return known == Times;
        }
    }
}

/// <summary>A Boolean column: true or false.</summary>
public sealed class BooleanColumn : SnapshotColumn
{
    internal BooleanColumn(ColumnShape shape)
        : base(shape)
    {
    }
}
