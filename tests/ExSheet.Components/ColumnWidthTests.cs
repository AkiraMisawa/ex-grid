using Bunit;
using ExGrid;
using System.Globalization;
using ExGrid.Columns;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Column widths (ADR-0016, ADR-0046, ADR-0047 second and third rounds, SH-20, SH-22): a width is
/// the Sheet Document's, in characters, set by a resize or by a number or date typed into a column
/// still at its default width that does not fit, as Excel's is; each is a step on the undo stack.
/// </summary>
public class ColumnWidthTests : SheetTestContext
{
    private static double WidthOf(Bunit.IRenderedComponent<Components.ExSheet> cut, int column) =>
        Grid(cut).Instance.Columns[column].Width.Width.FixedPx;

    private static Task ResizeAsync(Bunit.IRenderedComponent<Components.ExSheet> cut, string column, double widthPx)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(column, widthPx)));
    }

    [Fact] // ADR-0047 second round, SH-20: a number typed into a default-width column that does not fit widens it
    public async Task A_number_that_does_not_fit_widens_a_default_column()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "B1", "1234567890");

        Assert.True(WidthOf(cut, 1) > SheetColumns.DefaultWidthPx);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        // Widened in the grid's own measure, so the grid does not hash it (ADR-0016).
        Assert.Equal("1234567890", CellText(cut, "B1"));
    }

    [Fact] // ADR-0047 second round, SH-20: a date typed as one widens the column to its whole text
    public async Task A_date_that_does_not_fit_widens_a_default_column()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, new global::ExSheet.Engine.Sheet(System.Globalization.CultureInfo.GetCultureInfo("ja-JP")).ToDocument()));

        await EnterAsync(cut, "A1", "2026/9/26");

        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);
        Assert.Equal("2026/09/26", CellText(cut, "A1"));
    }

    [Fact] // ADR-0047 second round: text, a Formula whose result is text, and numbers that fit never widen a column
    public async Task Text_and_numbers_that_fit_do_not_widen()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "A1", "A heading far wider than its column");
        await EnterAsync(cut, "B1", "=\"A heading far wider than its column\"");
        await EnterAsync(cut, "C1", "1234.5");
        await EnterAsync(cut, "D1", "=1234*10");

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 3));
    }

    [Fact] // ADR-0047: a Formula's numeric result widens a default-width column as a typed number does, as Excel was observed to (WD-007)
    public async Task A_formulas_number_widens_as_a_typed_one_does()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "A1", "=123456789*10");

        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);
        Assert.Equal("1234567890", CellText(cut, "A1"));
    }

    [Fact] // ADR-0046 (2026-09-28), SH-26, CW-028: a column widened by entry is widened again by a longer entry, and never narrowed
    public async Task A_widened_column_is_widened_again_by_a_longer_entry()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234567890");
        var widened = WidthOf(cut, 0);
        Assert.True(widened > SheetColumns.DefaultWidthPx);

        await EnterAsync(cut, "A2", "12345678901");
        var again = WidthOf(cut, 0);
        Assert.True(again > widened);
        Assert.Equal("12345678901", CellText(cut, "A2"));

        // A shorter number never narrows it.
        await EnterAsync(cut, "A3", "123456789");
        Assert.Equal(again, WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0016 / ADR-0047: a resize is recorded as Fixed, and the column is the user's — an entry never widens it
    public async Task A_resized_column_is_the_users()
    {
        var cut = RenderSheet();

        await ResizeAsync(cut, "C", 60);

        Assert.Equal(60, WidthOf(cut, 2));
        await EnterAsync(cut, "C1", "1234567890");
        Assert.Equal(60, WidthOf(cut, 2));
        Assert.Matches("^#+$", CellText(cut, "C1"));
    }

    [Fact] // ADR-0016, ADR-0050 item 12, DC-36: the grips render because ExSheet takes the width change, and no column-menu button does
    public void The_resize_grips_are_offered_without_the_menu()
    {
        var cut = RenderSheet();

        Assert.True(Grid(cut).Instance.OnColumnWidthChanged.HasDelegate);
        Assert.True(Grid(cut).Instance.HideColumnMenu);
        Assert.NotEmpty(cut.FindAll(".ex-resize-grip"));
        Assert.Equal(cut.FindAll(".ex-header-cell").Count, cut.FindAll(".ex-resize-grip").Count);
        Assert.Empty(cut.FindAll(".ex-menu-button"));
    }

    [Fact] // ADR-0050 item 12, DC-36, ADR-0016: a double-click on a column's edge still sizes it to fit, as the user's width
    public async Task A_double_click_on_an_edge_sizes_the_column_to_fit()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "A heading far wider than its column");
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));

        await cut.FindAll(".ex-resize-grip")[0].DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });

        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);
        Assert.Equal("A heading far wider than its column", CellText(cut, "A1"));
    }

    [Fact] // ADR-0003: the column list is replaced only when a width changes, and an ordinary edit leaves it alone
    public async Task The_column_list_changes_only_with_a_width()
    {
        var cut = RenderSheet();
        var columns = Grid(cut).Instance.Columns;

        await EnterAsync(cut, "A1", "text");
        Assert.Same(columns, Grid(cut).Instance.Columns);

        await ResizeAsync(cut, "A", 120);
        Assert.NotSame(columns, Grid(cut).Instance.Columns);
        Assert.Same(columns[1], Grid(cut).Instance.Columns[1]);
    }

    [Fact] // ADR-0048, ADR-0046: a different Sheet Document brings its own widths, and one without any starts at the defaults
    public async Task Replacing_the_document_replaces_the_widths()
    {
        var cut = RenderSheet();
        await ResizeAsync(cut, "A", 120);

        cut.Render(ps => ps.Add(s => s.Document, new Sheet(CultureInfo.GetCultureInfo("en-US")).ToDocument()));

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
    }

    [Fact] // ADR-0046, ADR-0047 third round, SH-22: opening a document paints its widths, characters converted as the painted text converts them
    public void Opening_a_document_applies_its_widths()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.SetColumnWidth(CellRange.Parse("B:C"), 20);
        sheet.SetColumnWidth(CellRange.Parse("E:E"), 2);

        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));

        Assert.Equal(SheetColumns.PxOf(20, Metrics), WidthOf(cut, 1));
        Assert.Equal(SheetColumns.PxOf(20, Metrics), WidthOf(cut, 2));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 3));
        // Narrower than the grid's default MinWidth, and painted as narrow as it is recorded.
        Assert.Equal(SheetColumns.PxOf(2, Metrics), WidthOf(cut, 4));
        Assert.Equal(20, SheetColumns.CharactersIn(Metrics.ContentWidthPx(WidthOf(cut, 1)), Metrics), 9);
    }

    [Fact] // ADR-0046, ADR-0048, SH-22: a resize is recorded in the Sheet Document in characters, raises DocumentChanged, and is one undo step
    public async Task A_resize_is_recorded_in_the_document_as_one_step()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, (SheetDocument d) => raised = d));

        await ResizeAsync(cut, "C", 120);

        Assert.NotNull(raised);
        var width = Assert.Single(raised!.ColumnWidths);
        Assert.Equal((2, 2), (width.First, width.Last));
        Assert.Equal(SheetColumns.CharactersOfColumn(120, Metrics)!.Value, width.Width, 9);
        Assert.Equal(120, WidthOf(cut, 2), 6);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal(120, WidthOf(cut, 2), 6);
    }

    [Fact] // ADR-0016 FN-12c, ADR-0048, SH-22: resizing several whole columns, which the grid reports one column at a time, is one undo step
    public async Task Resizing_several_whole_columns_is_one_step()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B:D");

        await ResizeAsync(cut, "B", 100);
        await ResizeAsync(cut, "C", 100);
        await ResizeAsync(cut, "D", 100);

        Assert.All(new[] { 1, 2, 3 }, c => Assert.Equal(100, WidthOf(cut, c), 6));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.All(new[] { 1, 2, 3 }, c => Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, c)));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048: two resizes one after the other are two steps, a whole-column Selection or not
    public async Task Two_resizes_are_two_steps()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B:C");
        await ResizeAsync(cut, "B", 100);
        await ResizeAsync(cut, "C", 100);

        await ResizeAsync(cut, "B", 150);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(100, WidthOf(cut, 1), 6);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0046, ADR-0047 second round, ADR-0048, SH-22: widening on entry is recorded in the document, in the entry's own undo step
    public async Task Widening_on_entry_is_recorded_with_the_entry()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "B1", "1234567890");

        var width = Assert.Single(cut.Instance.ToDocument().ColumnWidths);
        Assert.Equal((1, 1), (width.First, width.Last));
        Assert.Equal(WidthOf(cut, 1), SheetColumns.PxOf(width.Width, Metrics), 6);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal("", CellText(cut, "B1"));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.True(WidthOf(cut, 1) > SheetColumns.DefaultWidthPx);
        Assert.Equal("1234567890", CellText(cut, "B1"));
    }

    [Fact] // ADR-0046, SH-22: a width moves with an inserted or deleted column, and back with its undo
    public async Task A_width_moves_with_inserted_and_deleted_columns()
    {
        var cut = RenderSheet();
        await ResizeAsync(cut, "B", 120);

        await cut.Instance.DoAsync(SheetEdit.InsertColumns(0));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal(120, WidthOf(cut, 2), 6);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(120, WidthOf(cut, 1), 6);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));

        await cut.Instance.DoAsync(SheetEdit.DeleteColumns(1));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
    }

    // ADR-0046 (2026-09-28), SH-26, CW-018: a width widened by entry is marked custom in the Sheet
    // Document, as Excel's file marks it, while it stays the entry's kind (the test above it).
    [Fact]
    public async Task Widening_on_entry_records_a_custom_width()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "B1", "1234567890");

        var width = Assert.Single(cut.Instance.ToDocument().ColumnWidths);
        Assert.True(width.IsCustom);
    }

    [Fact] // ADR-0046: a drag and a size to fit both record the user's width, custom
    public async Task A_resize_and_a_size_to_fit_record_a_custom_width()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "A heading far wider than its column");

        await ResizeAsync(cut, "C", 120);
        await cut.FindAll(".ex-resize-grip")[0].DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });

        var widths = cut.Instance.ToDocument().ColumnWidths;
        Assert.Equal([(0, true), (2, true)], widths.Select(w => (w.First, w.IsCustom)));
    }

    [Fact] // ADR-0046: a widened column the user then resizes is the user's, and a longer entry no longer widens it
    public async Task A_widened_column_the_user_resizes_stops_widening()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234567890");
        var widened = WidthOf(cut, 0);

        await ResizeAsync(cut, "A", widened);
        await EnterAsync(cut, "A2", "12345678901");

        Assert.Equal(widened, WidthOf(cut, 0), 6);
        Assert.True(Assert.Single(cut.Instance.ToDocument().ColumnWidths).IsCustom);
    }

    [Fact] // ADR-0046 (2026-09-28), SH-26, ADR-0048: a longer entry that widens a widened column is one step with its width, and undoing it puts back the earlier width, still widened by entry
    public async Task A_longer_entry_into_a_widened_column_is_undone_with_its_width()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234567890");
        var widened = WidthOf(cut, 0);
        await EnterAsync(cut, "A2", "12345678901");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal(widened, WidthOf(cut, 0), 6);
        Assert.Equal("", CellText(cut, "A2"));
        // Still the entry's kind: a longer entry widens it again.
        await EnterAsync(cut, "A2", "12345678901");
        Assert.True(WidthOf(cut, 0) > widened);
        Assert.True(await cut.Instance.UndoAsync());

        // The entry that first widened the column is undone with its width.
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0046 (2026-09-28), SH-26, ADR-0048: undoing the user's width over a widened one puts back the entry's kind, and redoing it the user's
    public async Task Undo_and_redo_restore_the_kind_of_a_width()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234567890");
        var widened = WidthOf(cut, 0);
        await ResizeAsync(cut, "A", widened);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.True(await cut.Instance.RedoAsync());
        await EnterAsync(cut, "A2", "12345678901");
        Assert.Equal(widened, WidthOf(cut, 0), 6);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.True(await cut.Instance.UndoAsync());
        await EnterAsync(cut, "A2", "12345678901");
        Assert.True(WidthOf(cut, 0) > widened);
    }

    [Fact] // ADR-0046 (2026-09-28), SH-26: a document reopened keeps each width's kind: one widened by entry is widened again, the user's is not
    public async Task A_reopened_documents_widths_keep_their_kind()
    {
        var first = RenderSheet();
        await EnterAsync(first, "A1", "1234567890");
        await ResizeAsync(first, "B", 80);
        var document = first.Instance.ToDocument();

        var cut = RenderSheet(ps => ps.Add(s => s.Document, document));
        var widened = WidthOf(cut, 0);
        await EnterAsync(cut, "A2", "12345678901");
        await EnterAsync(cut, "B1", "123456789012");

        Assert.True(WidthOf(cut, 0) > widened);
        Assert.Equal(80, WidthOf(cut, 1), 6);
    }

    [Fact] // ADR-0046, ADR-0047 second observation, SH-26: a document that holds an automatic width, as an older one may, widens on a longer entry, and one with a custom width does not
    public async Task An_older_documents_widths_read_as_they_did()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.SetAutomaticColumnWidth(CellRange.Parse("A:A"), 10);
        sheet.SetColumnWidth(CellRange.Parse("B:B"), 10);
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));

        await EnterAsync(cut, "A1", "123456789012345");
        await EnterAsync(cut, "B1", "123456789012345");

        Assert.True(WidthOf(cut, 0) > SheetColumns.PxOf(10, Metrics));
        Assert.Equal(SheetColumns.PxOf(10, Metrics), WidthOf(cut, 1), 6);
    }

    [Fact] // ADR-0046: a width set by a Consumer command is painted, as the user's would be
    public async Task A_width_set_by_a_command_is_painted()
    {
        var cut = RenderSheet();

        await cut.Instance.DoAsync(SheetEdit.SetColumnWidth(CellRange.Parse("D:D"), 30));

        Assert.Equal(SheetColumns.PxOf(30, Metrics), WidthOf(cut, 3));
    }

    // The Cell Metrics a plain ExSheet's grid resolves: no Wrapper cascades any.
    private static CellTextMetrics Metrics => GridMetrics.Resolve(GridDensity.Compact).CellMetrics;
}
