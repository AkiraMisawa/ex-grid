using AngleSharp.Dom;
using Bunit;
using ExGrid.Cells;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A Pinned Column's cell paints its row's rule (ticket 92; ADR-0029, ADR-0004): its opaque ground
/// hides the rule the row paints beneath it, so the cell paints the same rule itself, named in
/// <c>--ex-row-rule</c>, where the scrollable cells show the row's — above a Row Stripe, beneath a
/// Missing state's tint and a cell's lines — and not over a Fill, nor in a group or total row, whose
/// row paints none. Read from the shipped stylesheet's cascade over the rendered cells.
/// </summary>
public class PinnedRowRuleTests : GridTestContext
{
    private static readonly RgbColour Yellow = RgbColour.FromRgb(0xFFFF00);

    /// <summary>Rows by position: 0 a detail row, 1 a striped detail row, 2 a group row, 3 a striped
    /// total row, 4 a detail row whose pinned Book is Missing, and 7, away from it, a striped one
    /// whose Book is filled. One Pinned Column, Book.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.StripeRows, true)
            .Add(g => g.RowKind, (Func<TestRow, RowKind>)(row => (int)row.Amount switch
            {
                2 => RowKind.Group,
                3 => RowKind.Total,
                _ => RowKind.Detail,
            }))
            .Add(g => g.CellState, (row, column) => (int)row.Amount == 4 && column.Name == "Book" ? CellState.Missing : CellState.Normal)
            .Add(g => g.CellAppearance, (row, column) => (int)row.Amount == 7 && column.Name == "Book" ? new CellAppearance { Fill = Yellow } : default));

    private static IElement Cell(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.FindAll(".ex-viewport [role=row]")[row].QuerySelectorAll(".ex-cell")[column];

    private static string? Winning(IElement element, string property) => ShippedStylesheetTests.Winning(element, property);

    [Fact] // Ticket 92 / ADR-0029: a pinned cell paints the row's own rule, transparent until a theme turns it on, so a bare grid paints as before
    public void A_pinned_cell_paints_its_rows_rule()
    {
        var cut = RenderGrid();
        var row = cut.FindAll(".ex-viewport [role=row]")[0];
        var pinned = Cell(cut, 0, 0);
        Assert.Contains("ex-pinned", pinned.ClassList);

        // The same image the row paints, from the same token, transparent by default.
        Assert.Equal(Winning(row, "background-image"), Winning(pinned, "--ex-row-rule"));
        Assert.Contains("var(--ex-row-rule-color, transparent)", Winning(pinned, "--ex-row-rule"));
        Assert.Equal("var(--ex-row-rule)", Winning(pinned, "background-image"));
        // A scrollable cell is transparent over the row's own, and paints none of its own.
        var scrollable = Cell(cut, 0, 1);
        Assert.Null(Winning(scrollable, "--ex-row-rule"));
        Assert.Null(Winning(scrollable, "background-image"));
    }

    [Fact] // Ticket 92 (CI, 2026-10-01): a row paints the grid's own ground, so a translucent rule is blended onto it as the row paints, as on a pinned cell, and a package's row ground wins whatever the order
    public void A_row_paints_the_grids_ground_beneath_its_rule()
    {
        var cut = RenderGrid();
        var row = cut.FindAll(".ex-viewport [role=row]")[0];

        Assert.Equal("var(--ex-background, Canvas)", Winning(row, "background-color"));
        Assert.Equal("var(--ex-background, Canvas)", Winning(cut.Find(".ex-grid"), "background"));
        var (selectors, _) = Assert.Single(ShippedStylesheetTests.UnconditionalRules(),
            rule => ShippedStylesheetTests.Declarations(rule.Body).Any(declared => declared.Property == "background-color")
                && rule.Selectors.Any(selector => !selector.Contains("::", StringComparison.Ordinal) && row.Matches(selector)));
        var selector = Assert.Single(selectors);
        // Below ExSheet's Paper, whose own selector has one class's specificity.
        Assert.True(ShippedStylesheetTests.Specificity(selector).CompareTo(ShippedStylesheetTests.Specificity(":where(.ex-sheet) .ex-row")) < 0);
    }

    [Fact] // Ticket 92: the rule is the body's alone — a pinned header cell keeps the header's own rule
    public void A_pinned_header_cell_keeps_the_headers_rule()
    {
        var cut = RenderGrid();
        var header = cut.Find(".ex-header .ex-pinned");

        Assert.Null(Winning(header, "--ex-row-rule"));
        Assert.Contains("--ex-header-rule-color", Winning(header, "background-image"));
    }

    [Fact] // Ticket 92 / ADR-0038: on a striped row, the rule lies above the stripe, as the row paints it above its own
    public void On_a_striped_row_the_rule_lies_above_the_stripe()
    {
        var cut = RenderGrid();
        var pinned = Cell(cut, 1, 0);
        Assert.Contains("ex-row-stripe", cut.FindAll(".ex-viewport [role=row]")[1].ClassList);

        Assert.Equal("var(--ex-row-rule, none), var(--ex-tint)", Winning(pinned, "background-image"));
        Assert.Contains("var(--ex-row-rule-color, transparent)", Winning(pinned, "--ex-row-rule"));
    }

    [Fact] // Ticket 92 / ADR-0024: a group or total row paints no rule, on its pinned cells as on the row
    public void A_group_or_total_row_paints_no_rule()
    {
        var cut = RenderGrid();

        foreach (var index in new[] { 2, 3 })
        {
            var row = cut.FindAll(".ex-viewport [role=row]")[index];
            Assert.Equal("none", Winning(row, "background-image"));
            Assert.Equal("none", Winning(Cell(cut, index, 0), "--ex-row-rule"));
            Assert.Equal("var(--ex-tint)", Winning(Cell(cut, index, 0), "background-image"));
        }
    }

    [Fact] // Ticket 92 / ADR-0006: a Missing state's tint lies over the rule, as a scrollable cell's lies over its row's
    public void A_missing_states_tint_lies_over_the_rule()
    {
        var cut = RenderGrid();
        var missing = Cell(cut, 4, 0);
        Assert.Contains("ex-state-missing", missing.ClassList);

        Assert.Equal("var(--ex-tint), var(--ex-row-rule, none)", Winning(missing, "background-image"));
        Assert.Contains("var(--ex-row-rule-color, transparent)", Winning(missing, "--ex-row-rule"));
    }

    [Fact] // Ticket 92 / ADR-0071: a Fill covers the rule — the generated rule names none, and outranks the pinned cell's by specificity
    public void A_fill_covers_the_rule()
    {
        var cut = RenderGrid();
        var filled = Cell(cut, 7, 0);
        Assert.Contains("ex-fill-ffff00", filled.ClassList);

        Assert.Contains(".ex-cell.ex-fill-ffff00{background-color:#ffff00;--ex-column-rule-color:transparent;--ex-row-rule:none}", cut.Find(".ex-grid > style").TextContent);
        Assert.True(ShippedStylesheetTests.Specificity(".ex-cell.ex-fill-ffff00").CompareTo(ShippedStylesheetTests.Specificity(".ex-pinned:where(.ex-cell)")) > 0);
    }
}
