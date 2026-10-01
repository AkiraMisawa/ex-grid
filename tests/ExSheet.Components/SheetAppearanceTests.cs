using System.Globalization;
using Bunit;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet paints a Cell Format's Font and Fill (ticket 48, ADR-0071): it declares ADR-0050 item 15
/// from the engine, a Number Format's colour in the Font colour's place (SH-40), a level's Fill or
/// Font on the cells that hold nothing as well, and a row repaints only when its Values or its Cell
/// Format changed (SH-4, DC-58). The Paper and the Ink are the stylesheet's, read in layer 3.
/// </summary>
public class SheetAppearanceTests : SheetTestContext
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellColour Blue = CellColour.FromRgb(0x0000FF);
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));

    private static SheetDocument DocumentOf(Action<Sheet>? prepare = null, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        prepare?.Invoke(sheet);
        return sheet.ToDocument();
    }

    private static void Format(Sheet sheet, string range, CellFormatChange change) =>
        sheet.SetCellFormat([CellRange.Parse(range)], change);

    private static Dictionary<int, (SheetRow Row, int Renders)> Rows(IRenderedComponent<ExSheet> cut) =>
        cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

    private static string Classes(IRenderedComponent<ExSheet> cut, string address) => Cell(cut, address).ClassName ?? "";

    [Fact] // ADR-0050 item 15, DC-58: ExSheet declares each cell's Font and Fill from the engine's Cell Format
    public void The_font_and_the_fill_are_declared_from_the_cell_format()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "A1", new CellFormatChange { FontColour = Red, Bold = true, Italic = true, Underline = true, Strikethrough = true });
            Format(sheet, "B1", new CellFormatChange { Fill = Yellow });
            Format(sheet, "C1", new CellFormatChange { Bold = true });
        }, ("A1", "abc"), ("C1", "x"), ("D1", "plain"))));

        Assert.NotNull(Grid(cut).Instance.CellAppearance);
        Assert.Contains("ex-font-ff0000bius", Classes(cut, "A1"));
        Assert.Contains("ex-fill-ffff00", Classes(cut, "B1"));
        // Automatic is no colour of the cell's own: the Ink is the stylesheet's.
        Assert.Contains("ex-font-xb", Classes(cut, "C1"));
        Assert.DoesNotContain("ex-font-", Classes(cut, "D1"));
        Assert.DoesNotContain("ex-fill-", Classes(cut, "D1"));
    }

    [Fact] // ADR-0071, SH-40 (the eleventh Windows run, case 2): a Number Format's colour wins over the Font colour
    public void A_number_formats_colour_wins_over_the_font_colour()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "A1:B1", new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]-0"), FontColour = Blue }),
            ("A1", "-5"), ("B1", "5"))));

        Assert.Contains("ex-font-ff0000", Classes(cut, "A1"));
        Assert.Contains("ex-font-0000ff", Classes(cut, "B1"));
    }

    [Theory] // ADR-0071, SH-40 (case 1): each of the eight names is painted in Excel's legacy palette
    [InlineData("Black", "000000")]
    [InlineData("Blue", "0000ff")]
    [InlineData("Cyan", "00ffff")]
    [InlineData("Green", "00ff00")]
    [InlineData("Magenta", "ff00ff")]
    [InlineData("Red", "ff0000")]
    [InlineData("White", "ffffff")]
    [InlineData("Yellow", "ffff00")]
    public void Each_named_colour_is_excels(string name, string hex)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "A1", new CellFormatChange { NumberFormat = NumberFormat.Parse($"[{name}]0") }), ("A1", "5"))));

        Assert.Contains($"ex-font-{hex}", Classes(cut, "A1"));
    }

    [Fact] // ADR-0071, ADR-0047, SH-40 (case 3b): where no section shows the Value, there is no colour, and 5 in 0;[Red]@ is shown by an uncoloured section
    public void No_section_that_shows_the_value_means_no_colour()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "A1:A3", new CellFormatChange { NumberFormat = NumberFormat.Parse("[Red]0") });
            Format(sheet, "A4", new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]@") });
        }, ("A1", "abc"), ("A2", "TRUE"), ("A3", "=1/0"), ("A4", "5"))));

        Assert.Equal("5", CellText(cut, "A4"));
        foreach (var address in new[] { "A1", "A2", "A3", "A4" }) Assert.DoesNotContain("ex-font-", Classes(cut, address));
    }

    [Fact] // ADR-0071, SH-40 (case 3c): a #### keeps its section's colour
    public void A_number_that_does_not_fit_keeps_its_colour()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "A1", new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]-0") });
            sheet.Do(SheetEdit.SetColumnWidth(CellRange.WholeColumns(0, 0), 8));
        }, ("A1", "-123456789"))));

        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Contains("ex-font-ff0000", Classes(cut, "A1"));
    }

    [Fact] // ADR-0071, ticket 48: a Fill on a whole column paints its cells that hold nothing
    public async Task A_fill_on_a_whole_column_paints_the_cells_that_hold_nothing()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "C:C");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));

        var rows = cut.FindAll(".ex-row").Select(r => r.GetAttribute("aria-rowindex")!).ToList();
        Assert.True(rows.Count > 5);
        foreach (var row in rows)
        {
            Assert.Contains("ex-fill-ffff00", Classes(cut, $"C{row}"));
            Assert.DoesNotContain("ex-fill-", Classes(cut, $"B{row}"));
        }
    }

    [Fact] // ADR-0071, ticket 48: a Font colour on a whole row paints its cells that hold nothing
    public async Task A_font_colour_on_a_whole_row_paints_the_cells_that_hold_nothing()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "4:4");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { FontColour = Red }));

        Assert.Contains("ex-font-ff0000", Classes(cut, "A4"));
        Assert.Contains("ex-font-ff0000", Classes(cut, "F4"));
        Assert.DoesNotContain("ex-font-", Classes(cut, "A5"));
    }

    [Fact] // ADR-0071, SH-4, DC-58: a Fill on a whole row repaints that row and the row above, whose gridline it covers, and no other
    public async Task A_fill_on_a_whole_row_repaints_that_row_alone()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(null, ("A1", "1"), ("B6", "x"))));
        await GoToAsync(cut, "4:4");
        var before = Rows(cut);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));

        var after = Rows(cut);
        // A Fill covers the four gridlines at its edges, and the gridline above a cell is the
        // bottom of the cell above it: row 3 paints it in the Fill's colour (ticket 47).
        Assert.True(after[3].Renders > before[3].Renders, "the filled row repaints");
        Assert.True(after[2].Renders > before[2].Renders, "the row above repaints its gridline");
        foreach (var (index, (row, renders)) in after)
        {
            if (index is 2 or 3) continue;
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].Renders, renders);
        }
    }

    [Fact] // ADR-0071, ticket 48: a Fill on a whole column repaints every painted row, and undoing it every one again
    public async Task A_fill_on_a_whole_column_repaints_every_painted_row()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B:B");
        var before = Rows(cut);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));
        var filled = Rows(cut);
        Assert.All(filled, r => Assert.True(r.Value.Renders > before[r.Key].Renders, $"row {r.Key} repaints"));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.All(Rows(cut), r => Assert.True(r.Value.Renders > filled[r.Key].Renders, $"row {r.Key} repaints again"));
        Assert.Empty(cut.FindAll("[class*='ex-fill-']"));
    }

    [Fact] // ADR-0071, SH-4, DC-58: a Fill on one cell repaints its row and the row above, whose gridline it covers, and no other
    public async Task A_fill_on_a_cell_repaints_its_row_alone()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(null, ("A1", "1"), ("C3", "x"))));
        await GoToAsync(cut, "C3");
        var before = Rows(cut);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = Yellow }));

        var after = Rows(cut);
        Assert.Contains("ex-fill-ffff00", Classes(cut, "C3"));
        Assert.Contains("ex-lb-cover-ffff00", Classes(cut, "C2"));
        foreach (var (index, (row, renders)) in after)
        {
            if (index is 1 or 2) continue;
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].Renders, renders);
        }
    }

    [Fact] // ADR-0071, SH-4: an entry on a Sheet whose levels are formatted repaints its row alone
    public async Task An_entry_beside_formatted_levels_repaints_its_row_alone()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "B:B", new CellFormatChange { Fill = Yellow });
            Format(sheet, "3:3", new CellFormatChange { Bold = true });
        })));
        var before = Rows(cut);

        await EnterAsync(cut, "D5", "7");

        foreach (var (index, (row, renders)) in Rows(cut))
        {
            if (index == 4) continue;
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].Renders, renders);
        }
        Assert.Contains("ex-fill-ffff00", Classes(cut, "B5"));
    }

    [Fact] // ADR-0046, ADR-0071: a row inserted under a filled row takes its Fill, and is painted with it
    public async Task A_row_inserted_under_a_filled_row_is_painted_with_its_fill()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet => Format(sheet, "2:2", new CellFormatChange { Fill = Yellow }))));

        await cut.Instance.DoAsync(SheetEdit.InsertRows(2));

        Assert.Contains("ex-fill-ffff00", Classes(cut, "D2"));
        Assert.Contains("ex-fill-ffff00", Classes(cut, "D3"));
        Assert.DoesNotContain("ex-fill-", Classes(cut, "D4"));
    }

    [Fact] // ADR-0071, SH-41: a bold number is #### exactly when it does not fit at the bold widths
    public void A_bold_number_that_fits_only_the_regular_widths_is_hashes()
    {
        // 12.346%: five digits at 9.742, a narrow 6.398 and a wide %, 14.028 regular and 14.35 bold —
        // 69.136 px and 69.458 px, in a column whose content is 7.11 digits, 69.266 px.
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "A1:A2", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.000%") });
            Format(sheet, "A2", new CellFormatChange { Bold = true });
            sheet.Do(SheetEdit.SetColumnWidth(CellRange.WholeColumns(0, 0), 7.11));
        }, ("A1", "0.123456"), ("A2", "0.123456"))));

        Assert.Equal("12.346%", CellText(cut, "A1"));
        Assert.Matches("^#+$", CellText(cut, "A2"));
    }

    [Fact] // ADR-0071, SH-41: General fits a bold number to the bold widths
    public void General_fits_a_bold_number_to_the_bold_widths()
    {
        // A Wrapper whose bold digit is wider than its regular one: the default column's 83 px of
        // content holds ten regular digits and nine bold ones.
        var cut = RenderSheetUnder(new global::ExGrid.GridPresentationDefaults(12, 8, 4, 14, 12.5, 9, 4.2), ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "A2", new CellFormatChange { Bold = true }), ("A1", "=1/3"), ("A2", "=1/3"))));

        Assert.Equal("0.33333333", CellText(cut, "A1"));
        Assert.Equal("0.3333333", CellText(cut, "A2"));
    }
}
