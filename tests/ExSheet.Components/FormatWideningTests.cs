using System.Globalization;
using Bunit;
using ExGrid;
using ExGrid.Columns;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// What the twelfth Windows run settled about the formatting keys (ticket 58; ADR-0071, cases 17
/// to 19; <c>verification/2026-10-01-windows-excel-12/cell-format-12.md</c>): a Number Format
/// widens a column the user has not sized whose numbers it no longer fits, as one undo step with
/// the format, and recorded as a width an entry widened (SH-26); a column the user sized never
/// widens; and the date and time keys show Excel's built-ins in the Sheet culture's own form. The
/// fourteenth run added Ctrl+5, which widens as a Number Format key does, where Ctrl+B and the Fill
/// do not (ticket 100; ADR-0071, "What the fourteenth Windows run settled", case 7;
/// <c>verification/2026-10-01-windows-excel-14/cell-format-14.md</c>). The seventeenth run added
/// Ctrl+U, which widens as Ctrl+5 does, where Ctrl+I does not (its Part H, h7;
/// <c>verification/2026-10-11-windows-17/report.md</c>), and Format Cells' Font tab, which widened
/// for Bold. From that, the user decided on 2026-10-12: Format Cells' OK widens for any part of the
/// Font it sets, and each Sheet Toolbar toggle as the key it names. The readings of 2026-10-12 (R6)
/// then found Excel's <c>Range.Font</c> set through COM acting as the keys, and the user decided the
/// same day that <c>SetCellFormatAsync</c> widens as the keys do, not as OK does.
/// </summary>
public class FormatWideningTests : SheetTestContext
{
    private static SheetDocument DocumentIn(string culture, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));

    private static CellTextMetrics Metrics => GridMetrics.Resolve(GridDensity.Compact).CellMetrics;

    /// <summary>
    /// The fourteenth run's case 7: A1 holds 1234567.5 in <c>0.00</c>, which shows <c>####</c> at the
    /// default width, with <paramref name="also"/> set on it too. The format is in the document, as
    /// Excel's passes b to d set it on the empty A1 before the number, so nothing widened it. The
    /// seventeenth run's h7 and h8, at the standard width, held 123456789.12 there instead, set
    /// through COM, which <paramref name="held"/> names.
    /// </summary>
    private static SheetDocument Hashed(CellFormatChange? also = null, string held = "1234567.5")
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-GB"));
        sheet.Enter(CellAddress.Parse("A1"), held);
        sheet.SetCellFormat([CellRange.Parse("A1")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") });
        if (also is not null) sheet.SetCellFormat([CellRange.Parse("A1")], also);
        return sheet.ToDocument();
    }

    private static double WidthOf(IRenderedComponent<ExSheet> cut, int column) =>
        Grid(cut).Instance.Columns[column].Width.Width.FixedPx;

    private static CellFormat FormatAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    private static Task ResizeAsync(IRenderedComponent<ExSheet> cut, string column, double widthPx)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(column, widthPx)));
    }

    /// <summary>The width in pixels a column is widened to for <paramref name="text"/>: the grid's estimate of the text, each glyph at its own width, not floored at a digit a character (ticket 91).</summary>
    private static double WidenedFor(string text) => Math.Ceiling(Metrics.EstimatePx(text));

    // ---- Case 17: which keys widen a column at the default width ----

    [Theory] // ADR-0071 case 17: every Number Format key widens a column still at the default width whose text no longer fits, so the text shows rather than ####
    [InlineData("#", false, "46000.5", "09-Dec-25")]  // UK: # needs no Shift
    [InlineData("$", true, "1234567.5", "£1,234,567.50")]
    [InlineData("!", true, "1234567.5", "1,234,567.50")]
    [InlineData("%", true, "1234567.5", "123456750%")]
    [InlineData("^", true, "-1234567.5", "-1.23E+06")]
    public async Task A_number_format_key_widens_a_default_column_its_text_no_longer_fits(string key, bool shift, string typed, string shown)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("B1", typed))));
        await GoToAsync(cut, "B1");
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));

        await PressAsync(cut, key, ctrl: true, shift: shift);

        Assert.Equal(shown, CellText(cut, "B1"));
        Assert.Equal(WidenedFor(shown), WidthOf(cut, 1), 6);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
    }

    [Theory] // ADR-0071 case 17 (pass a): a key whose text still fits leaves the column at the default width
    [InlineData("%", "0.123456", "12%")]
    [InlineData("@", "46000", "00:00")]
    [InlineData("^", "1234567.5", "1.23E+06")]
    public async Task A_number_format_key_whose_text_fits_leaves_the_default_width(string key, string typed, string shown)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("B1", typed))));
        await GoToAsync(cut, "B1");

        await PressAsync(cut, key, ctrl: true, shift: true);

        Assert.Equal(shown, CellText(cut, "B1"));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
    }

    [Fact] // ADR-0071 case 17, ADR-0048: the widening is part of the key's one undo step, undone and redone with the format
    public async Task The_widening_is_part_of_the_keys_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("A1", "1234567.5"))));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "$", ctrl: true, shift: true);
        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(NumberFormat.General, FormatAt(cut, "A1").NumberFormat);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        // General fitted to the default column again (ADR-0047).
        Assert.Equal("1234568", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal("£1,234,567.50", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("£1,234,567.50"), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071 case 17, ADR-0046 SH-26: the column leaves the default width as a width an entry widened, so a longer number — a later key's among them — widens it again
    public async Task The_widened_column_is_recorded_as_widened_by_entry()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentIn("en-GB", ("A1", "1234567.5")))
            .Add(s => s.DocumentChanged, (SheetDocument d) => raised = d));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "!", ctrl: true, shift: true);

        var width = Assert.Single(raised!.ColumnWidths);
        Assert.Equal((0, 0), (width.First, width.Last));
        Assert.Equal(SheetColumnWidthKind.WidenedByEntry, width.Kind);
        Assert.True(width.IsCustom);
        Assert.Equal(WidthOf(cut, 0), SheetColumns.PxOf(width.Width, Metrics), 6);

        // The currency's text is longer still: the widened column widens again, as for a longer entry.
        var widened = WidthOf(cut, 0);
        await PressAsync(cut, "$", ctrl: true, shift: true);
        Assert.True(WidthOf(cut, 0) > widened);
        Assert.Equal("£1,234,567.50", CellText(cut, "A1"));
        Assert.Equal(SheetColumnWidthKind.WidenedByEntry, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);

        // A shorter text never narrows it.
        var again = WidthOf(cut, 0);
        await PressAsync(cut, "%", ctrl: true, shift: true);
        await PressAsync(cut, "!", ctrl: true, shift: true);
        Assert.Equal(again, WidthOf(cut, 0), 6);
    }

    // ---- Case 18: a column the user sized ----

    [Fact] // ADR-0071 case 18, SH-26: a column the user has sized never widens, at the default width or narrower, and its number shows ####
    public async Task A_column_the_user_sized_never_widens()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("A1", "1234567.5"), ("B1", "1234567.5"))));
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await ResizeAsync(cut, "B", 60);
        await GoToAsync(cut, "A1:B1");

        await PressAsync(cut, "$", ctrl: true, shift: true);

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Equal(60, WidthOf(cut, 1), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Matches("^#+$", CellText(cut, "B1"));
        Assert.All(cut.Instance.ToDocument().ColumnWidths, w => Assert.Equal(SheetColumnWidthKind.SetByUser, w.Kind));
    }

    // ---- What is widened ----

    [Fact] // ADR-0071 case 17: over a Selection of several columns each column widens to hold its widest number, and a column whose cells fit, or hold text, stays
    public async Task Each_column_of_the_selection_widens_to_its_widest_number()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB",
            ("A1", "1234567.5"), ("A2", "12345678.5"), ("B1", "1.5"), ("C1", "abc"), ("C2", "x"))));
        await GoToAsync(cut, "A1:C2");

        await PressAsync(cut, "!", ctrl: true, shift: true);

        Assert.Equal(WidenedFor("12,345,678.50"), WidthOf(cut, 0), 6);
        Assert.Equal("1,234,567.50", CellText(cut, "A1"));
        Assert.Equal("12,345,678.50", CellText(cut, "A2"));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
        Assert.Equal([0], cut.Instance.ToDocument().ColumnWidths.Select(w => w.First));
        // One step: the format on six cells and the width of one column.
        Assert.True(await cut.Instance.UndoAsync());
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 case 17, ADR-0047 (SH-21): a key over a whole column widens it for every number it holds, however far down
    public async Task A_whole_column_widens_for_every_number_it_holds()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB",
            ("B5", "1234567.5"), ("B1000000", "123456789.5"))));
        await GoToAsync(cut, "B:B");

        await PressAsync(cut, "!", ctrl: true, shift: true);

        Assert.Equal(WidenedFor("123,456,789.50"), WidthOf(cut, 1), 6);
        Assert.Equal("1,234,567.50", CellText(cut, "B5"));
    }

    [Fact] // ADR-0071 case 17, the fourteenth run's case 7 and the seventeenth's h7: Ctrl+B, Ctrl+I and a Fill leave the width though a number already shows ####, and so do the Border keys, a reading since no run pressed them over ####; a Number Format widens it
    public async Task Bold_italic_a_fill_and_a_border_never_widen()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("A1", "1234567890"))));
        // A Consumer's edit is done as written, and widens nothing (DoAsync).
        await cut.Instance.DoAsync(SheetEdit.SetCellFormat([CellRange.Parse("A1")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0") }));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "b", ctrl: true);
        await PressAsync(cut, "i", ctrl: true);
        await PressAsync(cut, "&", ctrl: true, shift: true);
        await PressAsync(cut, "_", ctrl: true, shift: true);
        // The Fill, as a toolbar's Fill Colour button sets it (the fourteenth run set it with the mouse).
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));
        Assert.True(FormatAt(cut, "A1").Font is { Bold: true, Italic: true, Underline: false });
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));

        await PressAsync(cut, "!", ctrl: true, shift: true);
        Assert.Equal("1,234,567,890.00", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("1,234,567,890.00"), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071 case 17, read for the commands: SetCellFormatAsync and Format Cells' OK widen as the key does, each in its one undo step; DoAsync widens nothing
    public async Task Set_cell_format_and_format_cells_widen_as_the_key_does()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB",
            ("A1", "1234567.5"), ("B1", "1234567.5"), ("C1", "1234567.5"))));
        var number = NumberFormat.Parse("#,##0.00");

        await GoToAsync(cut, "A1");
        Assert.True(await cut.Instance.SetNumberFormatAsync(number));
        Assert.Equal(WidenedFor("1,234,567.50"), WidthOf(cut, 0), 6);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.True(await cut.Instance.RedoAsync());

        await GoToAsync(cut, "B1");
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ex-format-cells")));
        await cut.FindAll(".ex-format-cells-categories label").Single(l => l.TextContent.Trim() == "Custom")
            .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = "on" });
        await cut.Find(".ex-format-cells-code").InputAsync(new ChangeEventArgs { Value = number.Code });
        await cut.Find("form.ex-format-cells").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-format-cells")));
        Assert.Equal("1,234,567.50", CellText(cut, "B1"));
        Assert.Equal(WidenedFor("1,234,567.50"), WidthOf(cut, 1), 6);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal(NumberFormat.General, FormatAt(cut, "B1").NumberFormat);

        await cut.Instance.DoAsync(SheetEdit.SetCellFormat([CellRange.Parse("C1")], new CellFormatChange { NumberFormat = number }));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
        Assert.Matches("^#+$", CellText(cut, "C1"));
    }

    // ---- The fourteenth run's case 7: Ctrl+5 widens as a Number Format key does ----

    [Fact] // ADR-0071, the fourteenth run's case 7: Ctrl+5 widens a column at the default width whose number shows ####, as a Number Format key does, recorded as a width an entry widened
    public async Task Ctrl_5_widens_as_a_number_format_key_does_case_14_7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed()));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "5", ctrl: true);

        Assert.True(FormatAt(cut, "A1").Font.Strikethrough);
        Assert.Equal("1234567.50", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("1234567.50"), WidthOf(cut, 0), 6);
        Assert.Equal(SheetColumnWidthKind.WidenedByEntry, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
    }

    [Theory] // ADR-0071, the fourteenth run's case 7 (passes b, c and d): of Ctrl+B, Ctrl+5 and the Fill, in any order, only Ctrl+5 widens
    [InlineData("b", "5", "fill")]
    [InlineData("5", "b", "fill")]
    [InlineData("fill", "b", "5")]
    public async Task Of_bold_strikethrough_and_the_fill_only_ctrl_5_widens_case_14_7(string first, string second, string third)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed()));
        await GoToAsync(cut, "A1");
        var widened = SheetColumns.DefaultWidthPx;

        foreach (var step in new[] { first, second, third })
        {
            if (step == "fill") Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));
            else await PressAsync(cut, step, ctrl: true);

            // Ctrl+5 widens to the text as the cell paints it then, bold where Ctrl+B came first.
            if (step == "5") widened = Math.Ceiling((FormatAt(cut, "A1").Font.Bold ? Metrics.Bold : Metrics).EstimatePx("1234567.50"));
            Assert.Equal(widened, WidthOf(cut, 0), 6);
        }
        Assert.True(widened > SheetColumns.DefaultWidthPx);
        Assert.Equal(new CellFont(Bold: true, Strikethrough: true), FormatAt(cut, "A1").Font);
        Assert.Equal(Yellow, FormatAt(cut, "A1").Fill);
    }

    [Fact] // ADR-0071, the fourteenth run's case 7, read for taking it off (the run pressed Ctrl+5 only to set it): Ctrl+5 over a struck number that shows #### takes strikethrough off and widens the same way
    public async Task Ctrl_5_taking_strikethrough_off_widens_the_same_way_case_14_7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(new CellFormatChange { Strikethrough = true })));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "5", ctrl: true);

        Assert.False(FormatAt(cut, "A1").Font.Strikethrough);
        Assert.Equal("1234567.50", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("1234567.50"), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071, the fourteenth run's case 7, ADR-0048: Ctrl+5's widening is part of its one undo step, undone and redone with the strikethrough
    public async Task Ctrl_5s_widening_is_part_of_its_one_undo_step_case_14_7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed()));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "5", ctrl: true);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.False(FormatAt(cut, "A1").Font.Strikethrough);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.True(FormatAt(cut, "A1").Font.Strikethrough);
        Assert.Equal("1234567.50", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("1234567.50"), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071, the fourteenth run's case 7 with the twelfth's case 18: Ctrl+5 never widens a column the user sized
    public async Task Ctrl_5_never_widens_a_column_the_user_sized_case_14_7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed()));
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "5", ctrl: true);

        Assert.True(FormatAt(cut, "A1").Font.Strikethrough);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Equal(SheetColumnWidthKind.SetByUser, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
    }

    // ---- The seventeenth run's h7: Ctrl+U widens as Ctrl+5 does, and Ctrl+I does not ----

    /// <summary>h7's number, 123456789.12 in <c>0.00</c>, as it shows once a column holds it.</summary>
    private const string H7Shown = "123456789.12";

    [Theory] // ADR-0071, the seventeenth run's h7: Ctrl+U widens a column at the default width whose number shows ####, as Ctrl+5 and a Number Format key do, recorded as a width an entry widened. Ctrl+4 is the same key (ADR-0071, "Keys"), so it widens too: a reading, since the run pressed Ctrl+U
    [InlineData("u")]
    [InlineData("U")]
    [InlineData("4")]
    public async Task Ctrl_u_widens_as_a_number_format_key_does_h7(string key)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, key, ctrl: true);

        Assert.True(FormatAt(cut, "A1").Font.Underline);
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedFor(H7Shown), WidthOf(cut, 0), 6);
        var width = Assert.Single(cut.Instance.ToDocument().ColumnWidths);
        Assert.Equal((0, 0), (width.First, width.Last));
        Assert.Equal(SheetColumnWidthKind.WidenedByEntry, width.Kind);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
    }

    [Fact] // ADR-0071, the seventeenth run's h7, which pressed each key in a workbook of its own: Ctrl+I leaves the default width and the number shows #### in italic; Ctrl+U pressed after it widens the column, a reading of the two together
    public async Task Ctrl_i_never_widens_and_ctrl_u_then_does_h7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "i", ctrl: true);
        Assert.True(FormatAt(cut, "A1").Font.Italic);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));

        await PressAsync(cut, "u", ctrl: true);
        Assert.Equal(new CellFont(Italic: true, Underline: true), FormatAt(cut, "A1").Font);
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedFor(H7Shown), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071, the seventeenth run's h7, read for taking it off (the run pressed Ctrl+U only to set it): Ctrl+U over an underlined number that shows #### takes underline off and widens the same way
    public async Task Ctrl_u_taking_underline_off_widens_the_same_way_h7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(new CellFormatChange { Underline = true }, held: "123456789.12")));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "u", ctrl: true);

        Assert.False(FormatAt(cut, "A1").Font.Underline);
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedFor(H7Shown), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071, the seventeenth run's h7, ADR-0048: Ctrl+U's widening is part of its one undo step, undone and redone with the underline
    public async Task Ctrl_us_widening_is_part_of_its_one_undo_step_h7()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "u", ctrl: true);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.False(FormatAt(cut, "A1").Font.Underline);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.True(FormatAt(cut, "A1").Font.Underline);
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedFor(H7Shown), WidthOf(cut, 0), 6);
    }

    [Fact] // ADR-0071, the seventeenth run's h7 with the twelfth's case 18: Ctrl+U never widens a column the user sized, at the default width or at h7's width 5, and neither does Ctrl+I
    public async Task Ctrl_u_never_widens_a_column_the_user_sized_h7()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-GB"));
        sheet.Enter(CellAddress.Parse("A1"), "123456789.12");
        sheet.Enter(CellAddress.Parse("B1"), "123456789.12");
        sheet.SetCellFormat([CellRange.Parse("A1:B1")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") });
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        var five = SheetColumns.PxOf(5, Metrics);
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await ResizeAsync(cut, "B", five);
        await GoToAsync(cut, "A1:B1");

        await PressAsync(cut, "i", ctrl: true);
        await PressAsync(cut, "u", ctrl: true);

        Assert.Equal(new CellFont(Italic: true, Underline: true), FormatAt(cut, "B1").Font);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Equal(five, WidthOf(cut, 1), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Matches("^#+$", CellText(cut, "B1"));
        Assert.All(cut.Instance.ToDocument().ColumnWidths, w => Assert.Equal(SheetColumnWidthKind.SetByUser, w.Kind));
    }

    // ---- Decided with the user, 2026-10-12: Format Cells' OK, SetCellFormatAsync and the toolbar ----

    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);

    /// <summary>The width a column is widened to for h7's number as A1 paints it now: bold where it is bold.</summary>
    private static double WidenedForA1(IRenderedComponent<ExSheet> cut) =>
        Math.Ceiling((FormatAt(cut, "A1").Font.Bold ? Metrics.Bold : Metrics).EstimatePx(H7Shown));

    private static async Task OpenFormatCellsOnAsync(IRenderedComponent<ExSheet> cut, string selection, string tab)
    {
        await GoToAsync(cut, selection);
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ex-format-cells")));
        await cut.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == tab).ClickAsync(new MouseEventArgs());
    }

    private static async Task OkFormatCellsAsync(IRenderedComponent<ExSheet> cut)
    {
        await cut.Find("form.ex-format-cells").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-format-cells")));
    }

    // One choice in Format Cells, on the tab shown, as the user makes it.
    private static Task ChooseInFormatCellsAsync(IRenderedComponent<ExSheet> cut, string choice) => choice switch
    {
        "Bold" or "Italic" or "Bold Italic" => cut.FindAll(".ex-format-cells-font-styles label").Single(l => l.TextContent.Trim() == choice)
            .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = "on" }),
        "Underline" => cut.Find(".ex-format-cells-underline").ChangeAsync(new ChangeEventArgs { Value = "Single" }),
        "Strikethrough" => cut.Find(".ex-format-cells-strikethrough").ChangeAsync(new ChangeEventArgs { Value = true }),
        "Normal font" => cut.Find(".ex-format-cells-normal-font").ChangeAsync(new ChangeEventArgs { Value = true }),
        "Center" => cut.Find(".ex-format-cells-alignment").ChangeAsync(new ChangeEventArgs { Value = "Center" }),
        "Outline" => cut.FindAll(".ex-format-cells-preset").Single(b => b.TextContent == "Outline").ClickAsync(new MouseEventArgs()),
        _ => cut.FindAll(".ex-format-cells-swatch").First(s => s.GetAttribute("aria-label") == choice).ChangeAsync(new ChangeEventArgs { Value = "on" }),
    };

    [Theory] // ADR-0071, decided 2026-10-12: Format Cells' OK widens a column at the default width whose number shows #### for any part of the Font it sets, as Excel's Font tab did for Bold (the seventeenth run's h7) and for Italic, a single underline and a colour (the readings of 2026-10-12 (R6), case (1)); strikethrough and the Normal font box are readings. The width is part of OK's one undo step
    [InlineData("Bold")]
    [InlineData("Italic")]
    [InlineData("Underline")]
    [InlineData("Red")]
    [InlineData("Strikethrough")]
    [InlineData("Normal font")]
    public async Task Format_cells_ok_widens_for_any_part_of_the_font_it_sets_h7(string choice)
    {
        // Normal font is shown checked over the default Font, so A1 starts with a colour of its own there.
        var cut = RenderSheet(ps => ps.Add(s => s.Document,
            Hashed(choice == "Normal font" ? new CellFormatChange { FontColour = Red } : null, held: "123456789.12")));
        Assert.Matches("^#+$", CellText(cut, "A1"));
        await OpenFormatCellsOnAsync(cut, "A1", "Font");

        await ChooseInFormatCellsAsync(cut, choice);
        await OkFormatCellsAsync(cut);

        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
        Assert.Equal(SheetColumnWidthKind.WidenedByEntry, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Theory] // ADR-0071, decided 2026-10-12: Format Cells' OK with a Fill, an alignment or a Border widens nothing over ####, as Excel's Fill tab and its Alignment tab's vertical alignment did not (the readings of 2026-10-12 (R6), case (2)); its Border tab did not over a General number shown rounded (the fourteenth run, case 9), so a Border over #### and the horizontal alignment set here are readings
    [InlineData("Fill", "Yellow")]
    [InlineData("Alignment", "Center")]
    [InlineData("Border", "Outline")]
    public async Task Format_cells_ok_with_a_fill_an_alignment_or_a_border_never_widens(string tab, string choice)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await OpenFormatCellsOnAsync(cut, "A1", tab);

        await ChooseInFormatCellsAsync(cut, choice);
        await OkFormatCellsAsync(cut);

        Assert.True(cut.Instance.CanUndo);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
    }

    [Fact] // ADR-0071, decided 2026-10-12, with the twelfth run's case 18: Format Cells' OK with Bold never widens a column the user sized, at the default width or at h7's width 5, as Excel's did not at a width set through COM (h7, over 123456.78 showing #####)
    public async Task Format_cells_ok_never_widens_a_column_the_user_sized_h7()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-GB"));
        sheet.Enter(CellAddress.Parse("A1"), "123456789.12");
        sheet.Enter(CellAddress.Parse("B1"), "123456.78");
        sheet.SetCellFormat([CellRange.Parse("A1:B1")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") });
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        var five = SheetColumns.PxOf(5, Metrics);
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await ResizeAsync(cut, "B", five);
        await OpenFormatCellsOnAsync(cut, "A1:B1", "Font");

        await ChooseInFormatCellsAsync(cut, "Bold");
        await OkFormatCellsAsync(cut);

        Assert.True(FormatAt(cut, "B1").Font.Bold);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Equal(five, WidthOf(cut, 1), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Matches("^#+$", CellText(cut, "B1"));
        Assert.All(cut.Instance.ToDocument().ColumnWidths, w => Assert.Equal(SheetColumnWidthKind.SetByUser, w.Kind));
    }

    [Theory] // ADR-0071, decided 2026-10-12 from the readings of 2026-10-12 (R6), case (5): SetCellFormatAsync widens as the formatting keys do, not as Format Cells' OK does — for underline and strikethrough, as Ctrl+U and Ctrl+5 do and as Excel's Range.Font.Underline set through COM did, in its one undo step, and bold where the change sets it too; bold, italic and a Font colour widen nothing, as Ctrl+B and Ctrl+I do not and as Range.Font.Bold did not; nor do a Fill, an alignment or Borders
    [InlineData("Bold", false)]
    [InlineData("Italic", false)]
    [InlineData("Colour", false)]
    [InlineData("Bold and italic", false)]
    [InlineData("Underline", true)]
    [InlineData("Strikethrough", true)]
    [InlineData("Bold and underline", true)]
    [InlineData("Fill", false)]
    [InlineData("Alignment", false)]
    [InlineData("Borders", false)]
    public async Task Set_cell_format_widens_as_the_keys_do_r6(string part, bool widens)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await GoToAsync(cut, "A1");
        var change = part switch
        {
            "Bold" => new CellFormatChange { Bold = true },
            "Italic" => new CellFormatChange { Italic = true },
            "Colour" => new CellFormatChange { FontColour = Red },
            "Bold and italic" => new CellFormatChange { Bold = true, Italic = true },
            "Underline" => new CellFormatChange { Underline = true },
            "Strikethrough" => new CellFormatChange { Strikethrough = true },
            "Bold and underline" => new CellFormatChange { Bold = true, Underline = true },
            "Fill" => new CellFormatChange { Fill = Yellow },
            "Alignment" => new CellFormatChange { Alignment = HorizontalAlignment.Center },
            _ => new CellFormatChange { Borders = BorderChange.Outline(new BorderLine(BorderLineStyle.Thin)) },
        };

        Assert.True(await cut.Instance.SetCellFormatAsync(change));

        Assert.True(cut.Instance.CanUndo);
        if (widens)
        {
            Assert.Equal(H7Shown, CellText(cut, "A1"));
            // Bold where the change sets it: the column holds the number as A1 paints it.
            Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
            Assert.Equal(SheetColumnWidthKind.WidenedByEntry, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
            Assert.True(await cut.Instance.UndoAsync());
            Assert.False(cut.Instance.CanUndo);
        }
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
    }

    [Fact] // ADR-0071, decided 2026-10-12: one change, bold, widens through Format Cells' OK, as Excel's Font tab did (the seventeenth run, h7), and not through SetCellFormatAsync, which widens as Ctrl+B does not
    public async Task Bold_widens_through_format_cells_ok_and_not_through_set_cell_format_h7_r6()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-GB"));
        sheet.Enter(CellAddress.Parse("A1"), "123456789.12");
        sheet.Enter(CellAddress.Parse("B1"), "123456789.12");
        sheet.SetCellFormat([CellRange.Parse("A1:B1")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") });
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));

        await GoToAsync(cut, "B1");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Bold = true }));
        Assert.True(FormatAt(cut, "B1").Font.Bold);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Matches("^#+$", CellText(cut, "B1"));

        await OpenFormatCellsOnAsync(cut, "A1", "Font");
        await ChooseInFormatCellsAsync(cut, "Bold");
        await OkFormatCellsAsync(cut);
        Assert.True(FormatAt(cut, "A1").Font.Bold);
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Matches("^#+$", CellText(cut, "B1"));
    }

    [Fact] // ADR-0071, decided 2026-10-12, with the twelfth run's case 18: SetCellFormatAsync never widens a column the user sized
    public async Task Set_cell_format_never_widens_a_column_the_user_sized()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await GoToAsync(cut, "A1");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Bold = true, Underline = true }));

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Equal(SheetColumnWidthKind.SetByUser, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
    }

    [Fact] // ADR-0100, ADR-0071, decided 2026-10-12: SetCellFormatFromFocusAsync, the toggles' command, widens as the keys do, as SetCellFormatAsync does: bold and italic widen nothing, as Ctrl+B and Ctrl+I do not; underline widens, as Ctrl+U does
    public async Task Set_cell_format_from_focus_widens_as_the_keys_do()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Hashed(held: "123456789.12")));
        await GoToAsync(cut, "A1");

        Assert.True(await cut.Instance.SetCellFormatFromFocusAsync(focus => new() { Bold = !focus.Font.Bold, Italic = !focus.Font.Italic }));
        Assert.Equal(new CellFont(Bold: true, Italic: true), FormatAt(cut, "A1").Font);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));

        Assert.True(await cut.Instance.SetCellFormatFromFocusAsync(focus => new() { Underline = !focus.Font.Underline }));
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
    }

    private IRenderedComponent<ExSheet> RenderToolbarSheet(SheetDocument document) =>
        RenderSheet(ps => ps.Add(s => s.ShowToolbar, true).Add(s => s.Document, document));

    // A Toolbar Item pressed with the pointer, its face where it is a split control.
    private static Task PressItemAsync(IRenderedComponent<ExSheet> cut, string name)
    {
        var item = cut.FindAll(".ex-sheet-toolbar [aria-label]").Single(e => e.GetAttribute("aria-label") == name
            && !e.ClassList.Contains("ex-sheet-toolbar-arrow") && !e.ClassList.Contains("ex-sheet-toolbar-face"));
        var face = item.ClassList.Contains("ex-sheet-toolbar-split") ? item.QuerySelector(".ex-sheet-toolbar-face")! : item;
        return face.ClickAsync(new MouseEventArgs());
    }

    [Theory] // ADR-0100, ADR-0071, decided 2026-10-12: each Sheet Toolbar toggle widens as the key it names — Underline as Ctrl+U (h7), Strikethrough as Ctrl+5 (the fourteenth run's case 7) — in the press's one undo step; Bold and Italic widen nothing, as Ctrl+B and Ctrl+I do not, and as Excel's Home tab's Bold and Italic buttons did not (R6)
    [InlineData("Bold", false)]
    [InlineData("Italic", false)]
    [InlineData("Underline", true)]
    [InlineData("Strikethrough", true)]
    public async Task Each_toolbar_toggle_widens_as_the_key_it_names(string item, bool widens)
    {
        var cut = RenderToolbarSheet(Hashed(held: "123456789.12"));
        await GoToAsync(cut, "A1");

        await PressItemAsync(cut, item);

        cut.WaitForAssertion(() => Assert.True(cut.Instance.CanUndo));
        if (widens)
        {
            Assert.Equal(H7Shown, CellText(cut, "A1"));
            Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
            Assert.Equal(SheetColumnWidthKind.WidenedByEntry, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
            Assert.True(await cut.Instance.UndoAsync());
            Assert.False(cut.Instance.CanUndo);
        }
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));
    }

    [Fact] // ADR-0100, ADR-0071, decided 2026-10-12, with the twelfth run's case 18: no toolbar toggle widens a column the user sized
    public async Task No_toolbar_toggle_widens_a_column_the_user_sized()
    {
        var cut = RenderToolbarSheet(Hashed(held: "123456789.12"));
        await ResizeAsync(cut, "A", SheetColumns.DefaultWidthPx);
        await GoToAsync(cut, "A1");

        foreach (var item in new[] { "Bold", "Italic", "Underline", "Strikethrough" }) await PressItemAsync(cut, item);

        cut.WaitForAssertion(() => Assert.True(FormatAt(cut, "A1").Font.Strikethrough));
        Assert.Equal(new CellFont(Bold: true, Italic: true, Underline: true, Strikethrough: true), FormatAt(cut, "A1").Font);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0), 6);
        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Equal(SheetColumnWidthKind.SetByUser, Assert.Single(cut.Instance.ToDocument().ColumnWidths).Kind);
    }

    [Fact] // ADR-0100, ADR-0071, decided 2026-10-12: the Font Colour face, and a colour from its list, set through SetCellFormatAsync, which widens as the keys do, so neither widens; Excel's Font Color button was not read over #### (the readings of 2026-10-12 (R6) pressed only the Home tab's Bold, Italic and Underline, case (3)), so that it widens nothing is a reading. The Fill Colour face widens nothing, as the fourteenth run's Fill Colour button did not (case 7); Underline then widens, as Ctrl+U does
    public async Task Neither_colour_face_nor_a_colour_from_its_list_widens()
    {
        var cut = RenderToolbarSheet(Hashed(held: "123456789.12"));
        await GoToAsync(cut, "A1");

        await PressItemAsync(cut, "Fill Colour");
        cut.WaitForAssertion(() => Assert.Equal(Yellow, FormatAt(cut, "A1").Fill));
        await PressItemAsync(cut, "Font Colour");
        cut.WaitForAssertion(() => Assert.Equal(Red, FormatAt(cut, "A1").Font.Colour));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));

        var fontColour = cut.FindAll(".ex-sheet-toolbar [aria-label]").Single(e => e.GetAttribute("aria-label") == "Font Colour"
            && e.ClassList.Contains("ex-sheet-toolbar-split"));
        await fontColour.QuerySelector(".ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        await cut.FindAll(".ex-popover .ex-sheet-swatch-choice").First(b => b.GetAttribute("aria-label") == "Blue").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Equal(CellColour.FromRgb(0x0070C0), FormatAt(cut, "A1").Font.Colour));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
        Assert.Matches("^#+$", CellText(cut, "A1"));

        await PressItemAsync(cut, "Underline");
        cut.WaitForAssertion(() => Assert.True(FormatAt(cut, "A1").Font.Underline));
        Assert.Equal(H7Shown, CellText(cut, "A1"));
        Assert.Equal(WidenedForA1(cut), WidthOf(cut, 0), 6);
    }

    // ---- Case 19: the date and time keys' built-ins under three cultures ----

    [Theory] // ADR-0071 case 19: the date key writes built-in 15 and the time key 20, or the AM/PM built-in under en-US, each shown in the Sheet culture's own form and recorded as the built-in
    [InlineData("en-GB", "05-Jan-26", "d-mmm-yy", "09:05", "h:mm")]
    [InlineData("en-US", "5-Jan-26", "d-mmm-yy", "9:05 AM", "h:mm AM/PM")]
    [InlineData("ja-JP", "05-1-26", "d-mmm-yy", "9:05", "h:mm")]
    public async Task The_date_and_time_keys_show_their_built_ins_in_the_cultures_form(
        string culture, string date, string dateCode, string time, string timeCode)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn(culture, ("A1", "=46027"), ("A2", "=545/1440"))));

        await GoToAsync(cut, "A1");
        await PressAsync(cut, "#", ctrl: true, shift: true);
        await GoToAsync(cut, "A2");
        await PressAsync(cut, "@", ctrl: true, shift: true);

        Assert.Equal(date, CellText(cut, "A1"));
        Assert.Equal(time, CellText(cut, "A2"));
        Assert.Equal(dateCode, FormatAt(cut, "A1").NumberFormat.Code);
        Assert.Equal(timeCode, FormatAt(cut, "A2").NumberFormat.Code);
    }

    [Fact] // ADR-0071 cases 17 and 19, ADR-0016 (ticket 91): under en-GB the date key widens to what 05-Jan-26's glyphs need, not to nine digits
    public async Task Under_en_gb_the_date_keys_text_widens_to_what_its_glyphs_need()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-GB", ("A1", "=46027"))));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "#", ctrl: true, shift: true);

        // Excel fitted it at its standard 8.09 (the twelfth run, case 19). The core's widths cover
        // DejaVu Sans Bold at 600, so its estimate is still past the default 99px, but it widens to
        // that estimate (102px), short of the 104 nine digits took.
        Assert.Equal("05-Jan-26", CellText(cut, "A1"));
        Assert.Equal(WidenedFor("05-Jan-26"), WidthOf(cut, 0), 6);
        Assert.True(WidthOf(cut, 0) < Math.Ceiling(SheetColumns.PxOf("05-Jan-26".Length, Metrics)));
    }

    [Fact] // ADR-0050 item 15, ADR-0016 (ticket 91): a bold number widens its column by the bold widths it is painted in
    public async Task A_bold_number_widens_by_the_bold_widths()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-GB"));
        sheet.Enter(CellAddress.Parse("A1"), "1234567.5");
        sheet.Enter(CellAddress.Parse("B1"), "1234567.5");
        sheet.SetCellFormat([CellRange.Parse("B1")], new CellFormatChange { Bold = true });
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        Assert.True(Math.Ceiling(Metrics.Bold.EstimatePx("123456750%")) > WidenedFor("123456750%"));

        await GoToAsync(cut, "A1:B1");
        await PressAsync(cut, "%", ctrl: true, shift: true);

        Assert.Equal(WidenedFor("123456750%"), WidthOf(cut, 0), 6);
        Assert.Equal(Math.Ceiling(Metrics.Bold.EstimatePx("123456750%")), WidthOf(cut, 1), 6);
    }

    [Fact] // ADR-0071 case 19, ADR-0016 (ticket 83): under en-US the date key's 5-Jan-26 and the time key's 9:05 AM fit the default width, which stays, as Excel's did
    public async Task Under_en_us_the_date_and_time_keys_texts_fit_the_default_width()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "=46027"), ("A2", "=545/1440"))));

        await GoToAsync(cut, "A1");
        await PressAsync(cut, "#", ctrl: true, shift: true);
        await GoToAsync(cut, "A2");
        await PressAsync(cut, "@", ctrl: true, shift: true);

        // Their letters are charged their measured widths: as the other class, J, a and n came to
        // 46.38px and widened the column to 112px.
        Assert.Equal("5-Jan-26", CellText(cut, "A1"));
        Assert.Equal("9:05 AM", CellText(cut, "A2"));
        Assert.True(Metrics.EstimatePx("5-Jan-26") <= SheetColumns.DefaultWidthPx);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Empty(cut.Instance.ToDocument().ColumnWidths);
    }
}
