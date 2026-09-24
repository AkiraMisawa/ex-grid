namespace ExGrid.Selection;

/// <summary>
/// The order Enter/Tab cycling walks a range — Enter runs down columns, Tab runs across
/// rows; both wrap and return to the start (ADR-0012).
/// </summary>
public enum CycleOrder
{
    /// <summary>Enter: down each column, then on to the next column.</summary>
    ColumnMajor,

    /// <summary>Tab: across each row, then on to the next row.</summary>
    RowMajor,
}
