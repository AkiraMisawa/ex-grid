namespace ExGrid.Rows;

/// <summary>
/// What a row represents — the role it plays in the result, not its depth in one
/// (ADR-0024). It is what lets a pivot-like view be painted while the row height stays
/// fixed (ADR-0013): a group row is an ordinary row of ordinary height that happens to
/// look different.
///
/// <para>Distinct from <see cref="Cells.CellState"/>: that names the state of one value,
/// this names the role of a whole row. A total row full of perfectly current figures is
/// Total and Normal at the same time.</para>
///
/// <para>The grid neither computes the aggregate nor decides what is grouped — both are
/// the Consumer's (ADR-0001). It is told which role each row plays and paints it.</para>
/// </summary>
public enum RowKind
{
    /// <summary>An ordinary row of the result. The default.</summary>
    Detail = 0,

    /// <summary>A row standing for a group of rows — the header of a collapsed or
    /// expanded block. Whether it is expanded, and what expanding does to the row count,
    /// is the Consumer's state.</summary>
    Group,

    /// <summary>A row aggregating others: a subtotal or a grand total. Computed by the
    /// Consumer, which pushes it as part of the Window like any other row.</summary>
    Total,
}
