using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A double click where no edit opens is heard by the Consumer (ADR-0062, DC-52): once, with
/// the cell's position, after the click has placed the Focus there. Where an edit opens, the
/// edit is the double click's meaning; a control inside an Action, Template or Mark cell stops
/// it. 20px rows; Book is editable, Amount is not.
/// </summary>
public class CellDoubleClickTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly RenderFragment<TemplateCellContext<TestRow>> Button =
        context => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "ex-interactive in-template");
            builder.AddContent(2, context.Row.Book);
            builder.CloseElement();
        };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(List<CellPosition>? heard, bool listen = true)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(20))
              .Add(g => g.Columns, [
                  new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                  new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
                  GridColumn<TestRow>.TemplateColumn("Open", ColumnType.Text, r => r.Book, Button, width: Fixed100),
                  GridColumn<TestRow>.ActionColumn("Act", [new GridAction("go", "Go")], width: Fixed100),
              ])
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 160)
              .Add(g => g.ViewportWidth, 450)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { });
            if (listen)
                ps.Add(g => g.OnCellDoubleClick, (CellPosition cell) => heard?.Add(cell));
        });

    private static Task DoubleClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0062 / DC-52: a double click on a cell that is not Editable is heard once, with its position
    public async Task A_double_click_where_no_edit_opens_is_heard()
    {
        var heard = new List<CellPosition>();
        var cut = RenderGrid(heard);

        await DoubleClickAsync(cut, 150, 45);

        Assert.Equal([new CellPosition(2, 1)], heard);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0062 / DC-52: a double click on the empty part of a Template cell names that cell
    public async Task A_double_click_beside_a_templates_control_names_the_template_cell()
    {
        var heard = new List<CellPosition>();
        var cut = RenderGrid(heard);

        await DoubleClickAsync(cut, 290, 5);

        Assert.Equal([new CellPosition(0, 2)], heard);
    }

    [Fact] // ADR-0062 / ADR-0010: where an edit opens, the edit is the double click's meaning, and nothing is heard
    public async Task A_double_click_on_an_editable_cell_opens_the_editor_and_is_not_heard()
    {
        var heard = new List<CellPosition>();
        var cut = RenderGrid(heard);

        await DoubleClickAsync(cut, 50, 5);

        Assert.Empty(heard);
        Assert.Single(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0062: while an edit is open the double click is the editor's
    public async Task A_double_click_while_editing_is_not_heard()
    {
        var heard = new List<CellPosition>();
        var cut = RenderGrid(heard);
        await DoubleClickAsync(cut, 50, 5);

        await DoubleClickAsync(cut, 150, 5);

        Assert.Empty(heard);
    }

    [Fact] // ADR-0062 / DC-1: without a listener a double click on a cell that is not Editable does nothing
    public async Task Without_a_listener_nothing_changes()
    {
        var cut = RenderGrid(null, listen: false);

        await DoubleClickAsync(cut, 150, 5);

        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0062 / ADR-0020: a double click on a control inside a Template or Action cell stops at the cell
    public async Task A_double_click_on_a_cells_control_stops_at_the_cell()
    {
        var heard = new List<CellPosition>();
        var cut = RenderGrid(heard);
        var click = new MouseEventArgs { Button = 0, OffsetX = 3, OffsetY = 3 };

        // bUnit walks the ancestors for a handler and honours the stop, so it finds none —
        // which is the contract: the Viewport never reads the control's offsets.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => cut.FindAll(".in-template")[1].DoubleClickAsync(click));
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => cut.FindAll(".ex-action")[1].DoubleClickAsync(click));

        Assert.Empty(heard);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }
}
