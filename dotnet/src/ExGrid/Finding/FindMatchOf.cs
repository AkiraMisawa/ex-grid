namespace ExGrid.Finding;

/// <summary>
/// Whether one painted cell matches what the find panel holds, for the Match Mark
/// (ADR-0332 §4). The grid asks it of the cells it paints, and only of those: the mark then
/// follows the scroll with no round trip to whoever answers Find. It must judge exactly as that
/// answer does, or the mark would show a match the next step does not reach.
/// <see cref="GridFind.Matches(string, GridFindPattern)"/> over the column's displayed text
/// (<see cref="ColumnInfo{TRow}.TextOf"/>) is the reference rule, the one a Grid Source answers by.
/// </summary>
/// <param name="row">The row the cell is in.</param>
/// <param name="column">The cell's column: one Find can land on — a value or Template column, whose
/// displayed text <see cref="ColumnInfo{TRow}.TextOf"/> gives — never an Action or Mark column.</param>
/// <param name="pattern">What the find panel holds. Its text is never empty.</param>
public delegate bool FindMatchOf<TRow>(TRow row, GridColumn<TRow> column, GridFindPattern pattern);
