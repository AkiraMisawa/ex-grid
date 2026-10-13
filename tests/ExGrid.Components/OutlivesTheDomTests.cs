using ExGrid.Cells;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// SL-7 (ADR-0027 P7, ADR-0008/0010): Selection, Focus and uncommitted editor text outlive the DOM
/// elements they are painted over. Scrolled far enough that every row they stand on leaves the
/// painted slice, and back, they are what they were — in the model and on screen. 500 rows of
/// 20px in a 120px Viewport, two 100px editable columns.
/// </summary>
public class OutlivesTheDomTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(List<GridSelection> told)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(500))
            .Add(g => g.TotalCount, 500)
            .Add(g => g.Columns, (GridColumn<TestRow>[])
            [
                new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, told.Add)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));

    private Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false));

    private async Task ScrollAndSettleAsync(IRenderedComponent<ExGrid<TestRow>> cut, double top)
    {
        await ScrollToAsync(cut.Find(".ex-scroller"), top);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(150)));
    }

    private static string[] PaintedBooks(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance.Row.Book)];

    /// <summary>What the overlay paints, as markup: the range rectangles and the Focus outline.</summary>
    private static string Overlay(IRenderedComponent<ExGrid<TestRow>> cut)
        => string.Join("\n", cut.FindAll(".ex-selection > *").Select(e => e.OuterHtml));

    [Fact] // SL-7, ADR-0027 P7: Selection and Focus scrolled out of the painted slice and back are unchanged, model and paint
    public async Task SL7_selection_and_focus_outlive_the_rows_they_are_painted_over()
    {
        var told = new List<GridSelection>();
        var cut = RenderGrid(told);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 }); // (1, 1)
        await KeyAsync(cut, "ArrowDown", shift: true);
        await KeyAsync(cut, "ArrowLeft", shift: true);  // (1, 1)-(2, 0), Focus at (1, 1)
        var selection = told[^1];
        var overlay = Overlay(cut);
        Assert.NotEmpty(cut.FindAll(".ex-selection > .ex-range"));
        Assert.NotEmpty(cut.FindAll(".ex-selection > .ex-focus"));

        // Far enough that rows 1 and 2 are out of the DOM, in ordinary steps and a jump.
        await ScrollAndSettleAsync(cut, 80);
        await ScrollAndSettleAsync(cut, 6_000);
        Assert.DoesNotContain("Row 000001", PaintedBooks(cut));
        Assert.DoesNotContain("Row 000002", PaintedBooks(cut));

        await ScrollAndSettleAsync(cut, 0);

        Assert.Contains("Row 000001", PaintedBooks(cut));
        Assert.Same(selection, told[^1]);                  // nothing was told: nothing changed
        Assert.Equal(new CellPosition(1, 1), told[^1].Focus);
        Assert.Equal(overlay, Overlay(cut));
    }

    [Fact] // SL-7, ADR-0027 P7 / ADR-0010: uncommitted editor text outlives the row it is typed over
    public async Task SL7_uncommitted_editor_text_outlives_the_row_it_is_typed_over()
    {
        var told = new List<GridSelection>();
        var cut = RenderGrid(told);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 30 }); // (1, 0)
        await KeyAsync(cut, "x");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "xyz uncommitted" });
        var style = cut.Find("input.ex-editor").GetAttribute("style");

        await ScrollAndSettleAsync(cut, 80);
        await ScrollAndSettleAsync(cut, 6_000);
        Assert.DoesNotContain("Row 000001", PaintedBooks(cut));

        await ScrollAndSettleAsync(cut, 0);

        var editor = cut.Find("input.ex-editor");
        Assert.Equal("xyz uncommitted", editor.GetAttribute("value"));
        Assert.Equal(style, editor.GetAttribute("style"));
        Assert.DoesNotContain("ex-editor-away", editor.ClassList);
        Assert.Equal(new CellPosition(1, 0), told[^1].Focus);
    }
}
