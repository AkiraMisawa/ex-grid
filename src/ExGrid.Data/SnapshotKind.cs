namespace ExGrid.Data;

/// <summary>
/// The kind a Snapshot column is declared with (ADR-0064). Every value of the column is of this
/// kind, or a Blank.
/// </summary>
public enum SnapshotKind
{
    /// <summary>Text, held exactly as written, as a code into the column's dictionary.</summary>
    Text,

    /// <summary>A decimal number, held exactly: as scaled 64-bit integers where they fit, as
    /// <see cref="decimal"/> otherwise.</summary>
    Decimal,

    /// <summary>A binary floating-point number, non-finite values included.</summary>
    Double,

    /// <summary>A 64-bit integer.</summary>
    Integer,

    /// <summary>A date, or a date and time, held as the clock value it shows.</summary>
    Date,

    /// <summary>True or false.</summary>
    Boolean,
}
