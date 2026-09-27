using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The per-cell kind (ADR-0050, item 6; DC-26): a Consumer's answer overrides the column's
/// kind for the cell's presentation, and without one the column's kind decides as before.
/// </summary>
public class CellTypeTests
{
    private sealed record Cell(object? Value, ColumnType Kind);

    // Digit 7px, padding 4px per side: "123456789" estimates at 9×7 + 8 = 71px.
    private static readonly CellTextMetrics Metrics = new(digitWidthPx: 7, cellHorizontalPaddingPx: 4);

    private static readonly Func<Cell, GridColumn<Cell>, ColumnType> ByCell = static (cell, _) => cell.Kind;

    [Fact] // ADR-0050 item 6 / DC-1: with no lookup the column's kind decides, exactly as before
    public void Without_a_lookup_the_cell_takes_its_columns_kind()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value);

        Assert.Equal(ColumnType.Number, column.TypeAt(new Cell("x", ColumnType.Text), cellType: null));
    }

    [Fact] // ADR-0050 item 6: the Consumer's answer overrides the column's kind for that cell
    public void The_lookup_answers_the_cells_kind()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value);

        Assert.Equal(ColumnType.Text, column.TypeAt(new Cell("Heading", ColumnType.Text), ByCell));
        Assert.Equal(ColumnType.Date, column.TypeAt(new Cell(DateTime.Today, ColumnType.Date), ByCell));
    }

    [Fact] // ADR-0050 item 6: the lookup is told which column it is asked about
    public void The_lookup_is_handed_the_column()
    {
        var number = new GridColumn<Cell>("N", ColumnType.Number, c => c.Value);
        var text = new GridColumn<Cell>("T", ColumnType.Text, c => c.Value);
        Func<Cell, GridColumn<Cell>, ColumnType> onlyN = static (_, column) =>
            column.Name == "N" ? ColumnType.Text : column.Type;
        var cell = new Cell(1m, ColumnType.Number);

        Assert.Equal(ColumnType.Text, number.TypeAt(cell, onlyN));
        Assert.Equal(ColumnType.Text, text.TypeAt(cell, onlyN));
    }

    [Fact] // ADR-0050 item 6 / ADR-0016: an undefined kind is refused by name, never painted as text
    public void An_undefined_kind_is_refused_naming_the_column()
    {
        var column = new GridColumn<Cell>("Amount", ColumnType.Number, c => c.Value);

        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => column.TypeAt(new Cell(1m, (ColumnType)99), ByCell));
        Assert.Contains("'Amount'", refused.Message);
    }

    [Fact] // ADR-0050 item 6 / ADR-0016 / DC-26: a text cell in a Number column is not numeric and never ####
    public void A_text_cell_in_a_number_column_never_hashes()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value);
        var kind = column.TypeAt(new Cell("A long heading", ColumnType.Text), ByCell);

        Assert.False(OverflowRules.HashesWhenOverflowing(kind));
        var decision = OverflowRules.Decide(kind, "A long heading", 20, Metrics);
        Assert.False(decision.IsHashed);
        Assert.Equal("A long heading", decision.DisplayText);
    }

    [Fact] // ADR-0050 item 6 / ADR-0016 / DC-26: a number in a Text column is numeric and becomes #### when it does not fit
    public void A_number_cell_in_a_text_column_hashes_when_it_does_not_fit()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Text, c => c.Value);
        var kind = column.TypeAt(new Cell(123456789m, ColumnType.Number), ByCell);

        Assert.True(OverflowRules.HashesWhenOverflowing(kind));
        Assert.True(OverflowRules.Decide(kind, "123456789", 40, Metrics).IsHashed);
        Assert.False(OverflowRules.Decide(kind, "123456789", 120, Metrics).IsHashed);
    }

    [Fact] // ADR-0050 item 6: sorting and filtering stay per column — the column's declared kind is untouched
    public void The_columns_own_kind_is_what_the_query_engine_reads()
    {
        var column = new GridColumn<Cell>("A", ColumnType.Number, c => c.Value);
        _ = column.TypeAt(new Cell("x", ColumnType.Text), ByCell);

        Assert.Equal(ColumnType.Number, column.Info.Type);
    }
}
