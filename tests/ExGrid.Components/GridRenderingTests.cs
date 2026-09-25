using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

public class GridRenderingTests : GridTestContext
{
    private static IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridTestContext ctx, TestRow[] rows, GridColumn<TestRow>[] columns, double? rowHeight = null)
        => ctx.Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows).Add(g => g.Columns, columns);
            if (rowHeight is { } h)
            {
                ps.Add(g => g.RowHeight, h);
            }
        });

    [Fact] // ADR-0001: the grid paints exactly the Window it is pushed, in order
    public void The_window_rows_render_one_ex_row_each_in_order()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns());

        var rows = cut.FindAll(".ex-row");
        Assert.Equal(3, rows.Count);
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            rows.Select(r => r.QuerySelector(".ex-cell")!.TextContent));
    }

    [Fact] // ADR-0001: an empty Window is a grid with no rows, not an error
    public void An_empty_window_renders_the_grid_chrome_and_no_rows()
    {
        var cut = RenderGrid(this, [], TestRows.Columns());

        Assert.NotNull(cut.Find(".ex-grid"));
        Assert.Equal(4, cut.FindAll(".ex-header-cell").Count);
        Assert.Empty(cut.FindAll(".ex-row"));
    }

    [Fact] // ADR-0013: RowHeight is a C# parameter emitted as --ex-row-height on the instance root
    public void Row_height_is_emitted_as_a_css_variable_on_the_root()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns(), rowHeight: 32);

        Assert.Contains("--ex-row-height: 32px", cut.Find(".ex-grid").GetAttribute("style"));
    }

    [Fact] // ADR-0013: the default row height applies without being set
    public void Row_height_defaults_to_28px()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns());

        Assert.Contains("--ex-row-height: 28px", cut.Find(".ex-grid").GetAttribute("style"));
    }

    [Fact] // ADR-0013: the parameter is the truth — a fractional height reaches CSS unrounded
    public void A_fractional_row_height_is_not_rounded()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns(), rowHeight: 28.125);

        Assert.Contains("--ex-row-height: 28.125px", cut.Find(".ex-grid").GetAttribute("style"));
    }

    [Fact] // ADR-0013: a non-positive RowHeight is refused, not painted
    public void A_non_positive_row_height_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderGrid(this, TestRows.Window(), TestRows.Columns(), rowHeight: 0));
    }

    [Fact] // ADR-0031: the instance root carries dir="ltr" explicitly — an LTR island in an RTL page
    public void The_root_carries_dir_ltr()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns());

        Assert.Equal("ltr", cut.Find(".ex-grid").GetAttribute("dir"));
    }

    [Fact] // ADR-0018: every class carries the ex- prefix; nothing leaks an unprefixed name
    public void All_css_classes_carry_the_ex_prefix()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns());

        var classTokens = cut.FindAll("[class]")
            .SelectMany(e => e.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
        Assert.NotEmpty(classTokens);
        Assert.All(classTokens, token => Assert.StartsWith("ex-", token));
    }

    [Fact] // ADR-0018: two instances on one page are independent — each root carries its own variables
    public void Two_grids_carry_independent_row_heights()
    {
        var rowsA = TestRows.Window();
        var rowsB = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render(builder =>
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, "Window", (IReadOnlyList<TestRow>)rowsA);
            builder.AddComponentParameter(2, "Columns", (IReadOnlyList<GridColumn<TestRow>>)columns);
            builder.AddComponentParameter(3, "RowHeight", 24d);
            builder.CloseComponent();
            builder.OpenComponent<ExGrid<TestRow>>(4);
            builder.AddComponentParameter(5, "Window", (IReadOnlyList<TestRow>)rowsB);
            builder.AddComponentParameter(6, "Columns", (IReadOnlyList<GridColumn<TestRow>>)columns);
            builder.AddComponentParameter(7, "RowHeight", 40d);
            builder.CloseComponent();
        });

        var roots = cut.FindAll(".ex-grid");
        Assert.Equal(2, roots.Count);
        Assert.Contains("--ex-row-height: 24px", roots[0].GetAttribute("style"));
        Assert.Contains("--ex-row-height: 40px", roots[1].GetAttribute("style"));
    }

    [Fact] // ADR-0016: Number and Date cells carry the numeric class — the same classification as the overflow rule
    public void Number_and_date_cells_are_classed_numeric_text_and_boolean_are_not()
    {
        var cut = RenderGrid(this, TestRows.Window(), TestRows.Columns());

        var firstRowCells = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell");
        Assert.Equal(
            [false, true, true, false], // Text, Number, Date, Boolean
            firstRowCells.Select(c => c.ClassList.Contains("ex-cell-numeric")));
    }

    [Fact] // ADR-0016/0029: alignment is a closed enum painted as interned classes; Auto adds nothing
    public void An_explicit_alignment_paints_its_class_and_auto_adds_nothing()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book),
            new("Amount", ColumnType.Number, r => r.Amount, align: Columns.CellAlign.Left,
                headerAlign: Columns.CellAlign.Center),
        ];
        var cut = RenderGrid(this, TestRows.Window(), columns);

        var cells = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell");
        Assert.DoesNotContain("ex-align", cells[0].ClassName);
        // Explicit Left beats the numeric derivation — both classes stand, Left later.
        Assert.Contains("ex-cell-numeric", cells[1].ClassName);
        Assert.Contains("ex-align-left", cells[1].ClassName);
        Assert.Contains("ex-align-center", cut.FindAll(".ex-header-cell")[1].ClassName);
    }

    [Fact] // ADR-0023: a Blank paints an empty cell — the accessor returned null
    public void A_null_accessor_value_renders_an_empty_cell()
    {
        GridColumn<TestRow>[] columns = [new("Blank", ColumnType.Text, _ => null)];
        var cut = RenderGrid(this, TestRows.Window(), columns);

        Assert.All(cut.FindAll(".ex-cell"), cell => Assert.Equal("", cell.TextContent));
    }

    [Fact] // CONTEXT.md "Column": the header renders the label
    public void Header_cells_render_the_column_headers()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, header: "Book name"),
            new("Amount", ColumnType.Number, r => r.Amount),
        ];
        var cut = RenderGrid(this, TestRows.Window(), columns);

        Assert.Equal(
            ["Book name", "Amount"],
            cut.FindAll(".ex-header-cell").Select(h => h.TextContent));
    }
}
