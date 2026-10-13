using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The fill handle as ExSheet wires it (ticket 15, ADR-0050 item 5): the grid owns the gesture and
/// raises a Fill Intent, and ExSheet fills as Excel fills through the engine, as one undo step,
/// or refuses the pattern and says why, leaving the Selection on the source.
/// </summary>
public class FillWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Selects <paramref name="source"/> with the Name Box, grabs the fill handle at its
    /// bottom-right corner and drags it down to the row of <paramref name="to"/>, in the source's
    /// last column, and releases it there.
    /// </summary>
    private static async Task DragDownAsync(IRenderedComponent<ExSheet> cut, string source, string to)
    {
        await GoToAsync(cut, source);
        var range = CellRange.Parse(source);
        var end = CellAddress.Parse(to);
        var width = SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;
        var viewport = cut.Find(".ex-viewport");
        var cornerX = HeadingWidth(cut) + (range.Last.Column + 1) * width;
        var cornerY = (range.Last.Row + 1) * row;
        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = cornerX - 1, OffsetY = cornerY - 1 });
        var x = cornerX - width / 2;
        var y = end.Row * row + row / 2;
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = x, OffsetY = y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    [Fact] // ADR-0050 item 5: ExSheet declares fill, so the handle is painted at the Selection's corner
    public async Task The_handle_is_painted()
    {
        var cut = RenderSheet();

        await GoToAsync(cut, "A1:A2");

        Assert.Single(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5, SH-15: 1, 2 dragged gives 1, 2, 3, 4, and the Selection covers source and target
    public async Task One_two_continues_as_a_series()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2")))
            .Add(s => s.SelectionChanged, selections.Add));

        await DragDownAsync(cut, "A1:A2", "A4");

        Assert.Equal("3", CellText(cut, "A3"));
        Assert.Equal("4", CellText(cut, "A4"));
        Assert.Equal([new SelectionRange(0, 0, 4, 1)], selections[^1].Ranges);
    }

    [Fact] // ADR-0050 item 5, SH-15: a date goes on by day
    public async Task A_date_goes_on_by_day()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "9/26/2026"))));

        await DragDownAsync(cut, "A1", "A3");

        Assert.Equal("9/27/2026", CellText(cut, "A2"));
        Assert.Equal("9/28/2026", CellText(cut, "A3"));
    }

    [Fact] // ADR-0050 item 5, SH-15: a Formula is copied with its References shifted
    public async Task A_formula_shifts_its_references()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("A3", "3"), ("B1", "=A1*10"))));

        await DragDownAsync(cut, "B1", "B3");

        Assert.Equal("20", CellText(cut, "B2"));
        Assert.Equal("30", CellText(cut, "B3"));
    }

    [Fact] // ADR-0050 item 5, SH-15: Item 1 is refused, not filled with copies; the user is told and the Selection stays
    public async Task Item_1_is_refused_and_the_user_is_told()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "Item 1")))
            .Add(s => s.SelectionChanged, selections.Add));

        await DragDownAsync(cut, "A1", "A3");

        Assert.Equal("", CellText(cut, "A2"));
        Assert.Contains("A1", cut.Find(".ex-sheet-notice").TextContent);
        Assert.Equal([new SelectionRange(0, 0, 1, 1)], selections[^1].Ranges);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048, SH-13: a fill is one undo step
    public async Task A_fill_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"))));
        await DragDownAsync(cut, "A1:A2", "A5");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("", CellText(cut, "A3"));
        Assert.Equal("", CellText(cut, "A5"));
        Assert.Equal("2", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }

    // ---- The handle on a whole axis (ADR-0050 item 5; 2026-10-12, decided with the user: it follows Excel; the readings of 2026-10-12, R5) ----

    /// <summary>The fill handle's box, as its inline style places it in its layer.</summary>
    private static (double Left, double Top, double Size) HandleBox(IRenderedComponent<ExSheet> cut)
    {
        var style = cut.Find(".ex-fill-handle").GetAttribute("style")!;
        double Read(string name) => double.Parse(
            System.Text.RegularExpressions.Regex.Match(style, name + @": (-?[0-9.]+)px").Groups[1].Value, CultureInfo.InvariantCulture);
        return (Read("left"), Read("top"), Read("width"));
    }

    /// <summary>What the cell records, as a Formula or as the constant's text or number: rows off screen are read from the Sheet Document.</summary>
    private static string? EntryAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.ToDocument().Cells.SingleOrDefault(c => c.Address == CellAddress.Parse(address))?.Entry is { } entry
            ? entry.Formula ?? (entry.Constant!.Value.Kind == ValueKind.Number
                ? entry.Constant.Value.Number.ToString(CultureInfo.InvariantCulture)
                : entry.Constant.Value.Text)
            : null;

    private static async Task DragAsync(IRenderedComponent<ExSheet> cut, (double X, double Y) from, (double X, double Y) to)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = from.X, OffsetY = from.Y });
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = to.X, OffsetY = to.Y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = to.X, OffsetY = to.Y });
    }

    private static SheetDocument ColumnC() =>
        DocumentOf(("C1", "1"), ("C3", "a"), ("C20", "=ROW()"), ("C1048576", "z"));

    [Fact] // ADR-0050 item 5 / DC-12, DC-13: column C's handle, beside its Heading, dragged two columns right fills C:E as Excel did (observed, R5 (3))
    public async Task A_whole_column_dragged_right_fills_every_row_and_steps_its_lone_number()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, ColumnC()).Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "C:C");
        var width = SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;
        var right = HeadingWidth(cut) + 3 * width;

        // At C1's top-right: centred on C's right edge, its top on row 1's top, beside the Heading.
        var box = HandleBox(cut);
        Assert.Equal((right, 0d), (box.Left + box.Size / 2, box.Top));

        await DragAsync(cut, (right, box.Size / 2), (right + width * 1.5, row * 2.5));   // to E3

        Assert.Equal("2", CellText(cut, "D1"));
        Assert.Equal("3", CellText(cut, "E1"));
        Assert.Equal("", CellText(cut, "D2"));
        Assert.Equal("a", CellText(cut, "D3"));
        Assert.Equal("a", CellText(cut, "E3"));
        Assert.Equal("=ROW()", EntryAt(cut, "E20"));
        Assert.Equal("z", EntryAt(cut, "D1048576"));
        Assert.Equal("z", EntryAt(cut, "E1048576"));
        // The Selection is C:E, whole columns, as Excel's became.
        Assert.Equal([new SelectionRange(0, 2, Sheet.RowCount, 3)], selections[^1].Ranges);

        // One step on the undo stack.
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("", CellText(cut, "D1"));
        Assert.Null(EntryAt(cut, "E1048576"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0050 item 5 / DC-13: a whole column's handle dragged down changes nothing, as Excel's did (observed, R5 (3))
    public async Task A_whole_column_dragged_down_changes_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, ColumnC()));
        await GoToAsync(cut, "C:C");
        var right = HeadingWidth(cut) + 3 * SheetColumns.DefaultWidthPx;
        var box = HandleBox(cut);

        await DragAsync(cut, (right, box.Size / 2), (right - SheetColumns.DefaultWidthPx / 2, ExSheet.DefaultRowHeightPx * 8.5));

        Assert.Equal("", CellText(cut, "D1"));
        Assert.False(cut.Instance.CanUndo);
        Assert.Single(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5 / DC-12, DC-13: row 3's handle, at its bottom-left beside the Heading, dragged two rows down fills rows 4 and 5 as Excel did (observed, R5 (4))
    public async Task A_whole_row_dragged_down_fills_every_column_and_steps_its_lone_number()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A3", "1"), ("C3", "a"), ("H3", "=COLUMN()"), ("XFD3", "z")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "3:3");
        var width = SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;
        var left = HeadingWidth(cut);

        // At A3's bottom-left: its left on A's left, beside the Heading, centred on the row's bottom.
        var box = HandleBox(cut);
        Assert.Equal((left, 3 * row), (box.Left, box.Top + box.Size / 2));

        await DragAsync(cut, (left + box.Size / 2, 3 * row), (left + width / 2, row * 4.5));   // to A5

        Assert.Equal("2", CellText(cut, "A4"));
        Assert.Equal("3", CellText(cut, "A5"));
        Assert.Equal("a", CellText(cut, "C5"));
        Assert.Equal("=COLUMN()", EntryAt(cut, "H4"));
        Assert.Equal("=COLUMN()", EntryAt(cut, "H5"));
        Assert.Equal("z", EntryAt(cut, "XFD5"));
        Assert.Equal([new SelectionRange(2, 0, 3, Sheet.ColumnCount)], selections[^1].Ranges);
    }

    [Fact] // ADR-0050 item 5 / DC-12: every cell (Ctrl+A) shows no handle, as Excel showed none in view (observed, R5 (1))
    public async Task Every_cell_shows_no_handle()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, ColumnC()).Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B2");
        Assert.Single(cut.FindAll(".ex-fill-handle"));

        await PressAsync(cut, "a", ctrl: true);

        Assert.Equal([new SelectionRange(0, 0, Sheet.RowCount, Sheet.ColumnCount)], selections[^1].Ranges);
        Assert.Empty(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: column C scrolled three rows down keeps its handle at the top-right of the part in view, as Excel's stayed (observed, R5 (5))
    public async Task A_whole_columns_handle_stays_in_view_with_row_1_scrolled_away()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, ColumnC()));
        await GoToAsync(cut, "C:C");
        var right = HeadingWidth(cut) + 3 * SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;

        await ScrollToAsync(cut, 3 * row, 0);                   // row 4 at the top, as three presses on the arrow left it

        var box = HandleBox(cut);
        Assert.Equal((right, 0d), (box.Left + box.Size / 2, box.Top));

        // Grabbed where it stands, it fills right as from row 1's.
        await DragAsync(cut, (right, box.Size / 2), (right + SheetColumns.DefaultWidthPx / 2, row * 2.5));
        Assert.Equal("2", EntryAt(cut, "D1"));
        Assert.Equal("z", EntryAt(cut, "D1048576"));
    }

    [Fact] // ADR-0050 item 5 / ADR-0047: a bold column filled right paints its target's blank rows bold at once, written as the target column's own format
    public async Task A_whole_columns_format_reaches_the_targets_blank_rows()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("C1"), "1");
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse("C:C")], new CellFormatChange { Bold = true }));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "C:C");
        var right = HeadingWidth(cut) + 3 * SheetColumns.DefaultWidthPx;
        var box = HandleBox(cut);

        await DragAsync(cut, (right, box.Size / 2), (right + SheetColumns.DefaultWidthPx / 2, ExSheet.DefaultRowHeightPx * 2.5));

        Assert.Contains("ex-font-xb", Cell(cut, "D5").ClassName);
        // C and D record one bold run, and no cell of D records a Font of its own.
        Assert.Equal((2, 3), cut.Instance.ToDocument().Columns.Select(c => (c.First, c.Last)).Single());
        Assert.DoesNotContain(cut.Instance.ToDocument().Cells, c => c.Address.Column == 3 && c.Font is not null);
    }
}
