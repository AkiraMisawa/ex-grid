using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press on the cell whose Cell Editor stands, landing on the rows (ADR-0361): made before the editor was
/// drawn over the cell, at once after the key that opened the edit. Made slowly it lands in the editor, so it
/// keeps the edit. 20px rows; Book 0–100 and Amount 100–200, both editable.
/// </summary>
public class EditedCellPressTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(List<GridEditIntent<TestRow>> edits, int rows = 50,
        bool headings = false)
    {
        var source = new TestSource();
        source.Push(TestRows.Many(rows), totalCount: rows);
        return Render<ExGrid<TestRow>>(ps =>
        {
            if (headings)
            {
                ps.Add(g => g.RowHeadings, row => (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Add(g => g.RowHeadingWidth, 40d);
            }
            ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns,
            [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => edits.Add(i));
        });
    }

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, Detail = 1, ShiftKey = shift });

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static string? EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-viewport input.ex-editor") is [var editor] ? editor.GetAttribute("value") : null;

    [Fact] // ADR-0361: a press on the edited cell, landing on the rows, keeps the edit
    public async Task ADR0361_a_press_on_the_edited_cell_keeps_the_edit()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits);
        await PressAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Assert.Equal("5", EditorText(cut));

        await PressAsync(cut, 50, 10);

        Assert.Empty(edits);
        Assert.Equal("5", EditorText(cut));
        Assert.Equal(new CellPosition(0, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    [Theory] // ADR-0361 / ADR-0010: a press that is not on the edited cell itself still commits — another cell, past the last column, below the last row
    [InlineData(150, 10, 50, 10)]    // Amount on row 0, then Book beside it
    [InlineData(150, 10, 300, 10)]   // past the last column, which a drag would clamp to the edited Amount
    [InlineData(150, 50, 150, 90)]   // below the last of three rows, which a drag would clamp to the edited row 2
    public async Task ADR0361_a_press_not_on_the_edited_cell_still_commits(double editX, double editY, double x, double y)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, rows: 3);
        await PressAsync(cut, editX, editY);
        await KeyAsync(cut, "5");
        Assert.Equal("5", EditorText(cut));

        await PressAsync(cut, x, y);

        Assert.Equal("5", Assert.Single(edits).Value);
        Assert.Null(EditorText(cut));
    }

    [Fact] // ADR-0361 / ADR-0050: a press on the edited row's Row Heading still commits and selects the row
    public async Task ADR0361_a_press_on_the_edited_rows_heading_still_commits()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, headings: true);
        await PressAsync(cut, 90, 10);                           // Book, row 0 (the columns start at 40)
        await KeyAsync(cut, "5");
        Assert.Equal("5", EditorText(cut));

        await PressAsync(cut, 20, 10);

        Assert.Equal("5", Assert.Single(edits).Value);
        Assert.Equal([new SelectionRange(0, 0, 1, 2)], cut.Instance.ReadSelection().Selection.Ranges);
    }
}
