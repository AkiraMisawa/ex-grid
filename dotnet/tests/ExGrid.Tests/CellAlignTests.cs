using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The per-cell alignment's precedence (ADR-0050, item 7; DC-29): the cell's answer where it
/// is not Auto, then the column's, then the kind's default (ADR-0016).
/// </summary>
public class CellAlignTests
{
    private sealed record Cell(object? Value, CellAlign Align);

    private static readonly Func<Cell, GridColumn<Cell>, CellAlign> ByCell = static (cell, _) => cell.Align;

    [Fact] // ADR-0050 item 7 / DC-1: without a lookup, the column's alignment stands
    public void Without_a_lookup_the_columns_alignment_stands()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value, align: CellAlign.Left);

        Assert.Equal(CellAlign.Left, column.AlignAt(new Cell(1m, CellAlign.Center), cellAlign: null));
    }

    [Fact] // ADR-0050 item 7 / DC-29: the cell's answer beats the column's
    public void The_cells_answer_beats_the_columns()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value, align: CellAlign.Left);

        Assert.Equal(CellAlign.Center, column.AlignAt(new Cell(true, CellAlign.Center), ByCell));
        Assert.Equal(CellAlign.Right, column.AlignAt(new Cell(1m, CellAlign.Right), ByCell));
    }

    [Fact] // ADR-0050 item 7 / DC-29: Auto from the cell falls back to the column, and Auto there to the kind
    public void Auto_falls_back_to_the_column_then_the_kind()
    {
        var aligned = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value, align: CellAlign.Center);
        var derived = new GridColumn<Cell>("B", ColumnType.Number, c => c.Value);
        var cell = new Cell(1m, CellAlign.Auto);

        Assert.Equal(CellAlign.Center, aligned.AlignAt(cell, ByCell));
        // Auto is the kind's default, which the numeric class paints (ADR-0016).
        Assert.Equal(CellAlign.Auto, derived.AlignAt(cell, ByCell));
    }

    [Fact] // ADR-0050 item 7: an undefined alignment is refused by name, never painted as some default
    public void An_undefined_alignment_is_refused_naming_the_column()
    {
        var column = new GridColumn<Cell>("Amount", ColumnType.Number, c => c.Value);

        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => column.AlignAt(new Cell(1m, (CellAlign)99), ByCell));
        Assert.Contains("'Amount'", refused.Message);
    }
}
