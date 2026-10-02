using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Setting and reading a Cell Format (ticket 50, ADR-0071 "The commands"; SH-44):
/// <c>SetCellFormatAsync</c> sets only the parts its change names over every range of the
/// Selection, as one undo step, with Borders relative to each range; <c>SetNumberFormatAsync</c>
/// and <c>SetAlignmentAsync</c> are shorthands that behave as the change they abbreviate; and
/// <c>CellFormatAt</c> answers a cell's Cell Format as it shows, cell over row over column. The
/// refusal while an edit is open is in <see cref="RefusedWhileEditingTests"/>, one per command.
/// </summary>
public class CellFormatCommandTests : SheetTestContext
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill Blue = CellFill.Solid(CellColour.FromRgb(0x00B0F0));
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine DoubleRed = new(BorderLineStyle.Double, Red);
    private static readonly NumberFormat TwoPlaces = NumberFormat.Parse("#,##0.00");
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");

    // Every part set, each to something other than its default: what a change naming one part is
    // set over, so a part it does not name has something to keep.
    private static readonly CellFormatChange Everything = new()
    {
        NumberFormat = TwoPlaces,
        Alignment = HorizontalAlignment.Center,
        FontColour = Red,
        Bold = true,
        Fill = Yellow,
        Borders = BorderChange.Outline(Thin),
    };

    private static SheetDocument DocumentOf(Action<Sheet>? prepare = null, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        prepare?.Invoke(sheet);
        return sheet.ToDocument();
    }

    private static CellFormat FormatAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    // ---- Only the parts the change names (SH-44) ----

    [Theory] // ADR-0071, SH-44: a change sets the part it names, and every other part stays as the cell has it
    [InlineData("NumberFormat")]
    [InlineData("Alignment")]
    [InlineData("FontColour")]
    [InlineData("Bold")]
    [InlineData("Italic")]
    [InlineData("Underline")]
    [InlineData("Strikethrough")]
    [InlineData("Fill")]
    [InlineData("Borders")]
    [InlineData("NoBorders")]
    public async Task A_change_sets_the_part_it_names_and_no_other(string part)
    {
        var (change, expected) = Naming(part);
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => sheet.SetCellFormat([CellRange.Parse("B2:C2")], Everything), ("B2", "1234.5"))));
        var before = FormatAt(cut, "B2");
        var neighbour = FormatAt(cut, "C2");
        await GoToAsync(cut, "B2");

        Assert.True(await cut.Instance.SetCellFormatAsync(change));

        Assert.Equal(expected(before), FormatAt(cut, "B2"));
        // Outside the Selection, nothing changed.
        Assert.Equal(neighbour, FormatAt(cut, "C2"));
    }

    // The change naming one part, and what a cell that showed a Cell Format shows once it is set.
    private static (CellFormatChange Change, Func<CellFormat, CellFormat> Expected) Naming(string part) => part switch
    {
        "NumberFormat" => (new() { NumberFormat = Percent }, f => f with { NumberFormat = Percent }),
        "Alignment" => (new() { Alignment = HorizontalAlignment.Right }, f => f with { Alignment = HorizontalAlignment.Right }),
        "FontColour" => (new() { FontColour = CellColour.Automatic }, f => f with { Font = f.Font with { Colour = CellColour.Automatic } }),
        "Bold" => (new() { Bold = false }, f => f with { Font = f.Font with { Bold = false } }),
        "Italic" => (new() { Italic = true }, f => f with { Font = f.Font with { Italic = true } }),
        "Underline" => (new() { Underline = true }, f => f with { Font = f.Font with { Underline = true } }),
        "Strikethrough" => (new() { Strikethrough = true }, f => f with { Font = f.Font with { Strikethrough = true } }),
        "Fill" => (new() { Fill = CellFill.None }, f => f with { Fill = CellFill.None }),
        "Borders" => (new() { Borders = new BorderChange { Top = DoubleRed } }, f => f with { Borders = f.Borders with { Top = DoubleRed } }),
        "NoBorders" => (new() { Borders = BorderChange.None }, f => f with { Borders = CellBorders.None }),
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Not a part of a Cell Format."),
    };

    [Fact] // ADR-0071, SH-44: setting bold keeps each cell's own italic, Fill and Number Format, cell by cell
    public async Task A_change_keeps_what_differs_from_cell_to_cell()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            sheet.SetCellFormat([CellRange.Parse("A1")], new CellFormatChange { Italic = true, Fill = Yellow });
            sheet.SetCellFormat([CellRange.Parse("A2")], new CellFormatChange { NumberFormat = Percent, Underline = true });
        }, ("A1", "1234.5"), ("A2", "0.25"))));
        await GoToAsync(cut, "A1:A2");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Bold = true }));

        Assert.Equal(CellFormat.Default with { Font = new CellFont(Bold: true, Italic: true), Fill = Yellow }, FormatAt(cut, "A1"));
        Assert.Equal(CellFormat.Default with { NumberFormat = Percent, Font = new CellFont(Bold: true, Underline: true) }, FormatAt(cut, "A2"));
        // What the cells show did not change with them.
        Assert.Equal("1234.5", CellText(cut, "A1"));
        Assert.Equal("25%", CellText(cut, "A2"));
    }

    // ---- One undo step (SH-44, ADR-0048) ----

    [Fact] // ADR-0071 / ADR-0048, SH-44: a change over several ranges is one operation, and one undo puts every cell back
    public async Task A_change_over_several_ranges_is_one_undo_step()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(null, ("A1", "1"), ("D4", "4")))
            .Add(s => s.DocumentChanged, raised.Add));
        var before = cut.Instance.ToDocument().ToJson();
        await GoToAsync(cut, "A1:B2");
        await CtrlClickAsync(cut, "D4");

        Assert.True(await cut.Instance.SetCellFormatAsync(Everything with { Italic = true }));

        var after = cut.Instance.ToDocument().ToJson();
        Assert.NotEqual(before, after);
        Assert.Single(raised);
        Assert.Equal("1.00", CellText(cut, "A1"));
        Assert.Equal("4.00", CellText(cut, "D4"));

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        foreach (var address in new[] { "A1", "B1", "A2", "B2", "D4" }) Assert.Equal(CellFormat.Default, FormatAt(cut, address));
        Assert.Equal("1", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal(after, cut.Instance.ToDocument().ToJson());
    }

    // ---- Borders relative to each range (SH-44) ----

    [Fact] // ADR-0071, SH-44: over a Selection of several ranges, each range gets its own outline, read from the cells beside it too (the eleventh Windows run, cases 13 and 14), as in Excel
    public async Task Each_selected_range_gets_its_own_outline()
    {
        GridSelection? selection = null;
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, s => selection = s));
        await GoToAsync(cut, "A1:C3");
        await CtrlClickAsync(cut, "E5");
        await PressAsync(cut, "ArrowRight", shift: true);
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Equal(2, selection!.Ranges.Count);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = BorderChange.Outline(Thin) }));

        CellRange[] ranges = [CellRange.Parse("A1:C3"), CellRange.Parse("E5:F6")];
        for (var row = 0; row < 7; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                var at = new CellAddress(row, column);
                // A side lies on a range's outline from inside the range or from the cell beside it.
                var expected = new CellBorders(
                    Top: ranges.Any(r => Across(r, at) && (at.Row == r.First.Row || at.Row == r.Last.Row + 1)) ? Thin : BorderLine.None,
                    Bottom: ranges.Any(r => Across(r, at) && (at.Row == r.Last.Row || at.Row == r.First.Row - 1)) ? Thin : BorderLine.None,
                    Left: ranges.Any(r => Along(r, at) && (at.Column == r.First.Column || at.Column == r.Last.Column + 1)) ? Thin : BorderLine.None,
                    Right: ranges.Any(r => Along(r, at) && (at.Column == r.Last.Column || at.Column == r.First.Column - 1)) ? Thin : BorderLine.None);
                var shown = cut.Instance.CellFormatAt(at).Borders;
                Assert.True(expected == shown, $"{at}: expected {expected}, was {shown}");
            }
        }

        // Whether the cell lies across the range's columns (for a top or bottom edge), or along its rows (for a left or right edge).
        static bool Across(CellRange range, CellAddress at) => at.Column >= range.First.Column && at.Column <= range.Last.Column;
        static bool Along(CellRange range, CellAddress at) => at.Row >= range.First.Row && at.Row <= range.Last.Row;
    }

    // ---- The shorthands (SH-44) ----

    [Theory] // ADR-0071, SH-44: SetNumberFormatAsync and SetAlignmentAsync behave as the change they abbreviate
    [InlineData("NumberFormat")]
    [InlineData("General")]
    [InlineData("Alignment")]
    public async Task A_shorthand_behaves_as_the_change_it_abbreviates(string shorthand)
    {
        // Whole columns, a whole row and a cell, over a Sheet that records a Cell Format at every
        // level, so the shorthand and the change meet every way a part is recorded.
        var document = DocumentOf(sheet =>
        {
            sheet.SetCellFormat([CellRange.WholeColumns(1, 1)], new CellFormatChange { NumberFormat = Percent, Bold = true });
            sheet.SetCellFormat([CellRange.WholeRows(3, 3)], new CellFormatChange { Alignment = HorizontalAlignment.Left, Fill = Yellow });
            sheet.SetCellFormat([CellRange.Parse("D2")], Everything);
        }, ("B2", "0.5"), ("D2", "1234.5"), ("D4", "text"));
        var byShorthand = RenderSheet(ps => ps.Add(s => s.Document, document));
        var byChange = RenderSheet(ps => ps.Add(s => s.Document, document));
        foreach (var cut in new[] { byShorthand, byChange })
        {
            await GoToAsync(cut, "B:C");
            await CtrlClickAsync(cut, "D2");
        }
        var (abbreviated, change) = shorthand switch
        {
            "NumberFormat" => (byShorthand.Instance.SetNumberFormatAsync(TwoPlaces), new CellFormatChange { NumberFormat = TwoPlaces }),
            "General" => (byShorthand.Instance.SetNumberFormatAsync(null), new CellFormatChange { NumberFormat = NumberFormat.General }),
            "Alignment" => (byShorthand.Instance.SetAlignmentAsync(HorizontalAlignment.Right), new CellFormatChange { Alignment = HorizontalAlignment.Right }),
            _ => throw new ArgumentOutOfRangeException(nameof(shorthand), shorthand, "Not a shorthand."),
        };

        Assert.True(await abbreviated);
        Assert.True(await byChange.Instance.SetCellFormatAsync(change));

        Assert.Equal(byChange.Instance.ToDocument().ToJson(), byShorthand.Instance.ToDocument().ToJson());
        foreach (var address in new[] { "B2", "C4", "D2", "D4" })
        {
            Assert.Equal(FormatAt(byChange, address), FormatAt(byShorthand, address));
            Assert.Equal(CellText(byChange, address), CellText(byShorthand, address));
        }
        // One undo step each, putting back the same Sheet.
        Assert.True(await byShorthand.Instance.UndoAsync());
        Assert.True(await byChange.Instance.UndoAsync());
        Assert.Equal(document.ToJson(), byShorthand.Instance.ToDocument().ToJson());
        Assert.Equal(document.ToJson(), byChange.Instance.ToDocument().ToJson());
    }

    // ---- What a change must name ----

    [Fact] // ADR-0071: a change that names no part is the caller's error, and changes nothing
    public async Task A_change_that_names_nothing_is_an_argument_error()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(null, ("A1", "1"))));
        var before = cut.Instance.ToDocument().ToJson();
        await GoToAsync(cut, "A1");

        await Assert.ThrowsAsync<ArgumentException>(() => cut.Instance.SetCellFormatAsync(new CellFormatChange()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cut.Instance.SetCellFormatAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cut.Instance.SetCellFormatAsync(new CellFormatChange { Alignment = (HorizontalAlignment)99 }));

        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        Assert.False(cut.Instance.CanUndo);
    }

    // ---- Reading it back (SH-44) ----

    [Fact] // ADR-0071 / ADR-0047, SH-44: CellFormatAt answers each part from the cell, else its row, else its column, else the default
    public async Task CellFormatAt_answers_cell_over_row_over_column()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B:B");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { NumberFormat = TwoPlaces, Bold = true, Fill = Yellow }));
        await GoToAsync(cut, "3:3");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Alignment = HorizontalAlignment.Center, Fill = Blue }));
        await GoToAsync(cut, "B3");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Italic = true }));
        await GoToAsync(cut, "C3");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Fill = CellFill.None }));

        // Nothing recorded at any level: every part's default.
        Assert.Equal(CellFormat.Default, FormatAt(cut, "D5"));
        // The column's.
        Assert.Equal(CellFormat.Default with { NumberFormat = TwoPlaces, Font = new CellFont(Bold: true), Fill = Yellow }, FormatAt(cut, "B5"));
        // The row's.
        Assert.Equal(CellFormat.Default with { Alignment = HorizontalAlignment.Center, Fill = Blue }, FormatAt(cut, "D3"));
        // The row's Fill over the column's, the column's Number Format where the row records none,
        // and the cell's italic over the column's bold Font it was set on.
        Assert.Equal(new CellFormat(TwoPlaces, HorizontalAlignment.Center, new CellFont(Bold: true, Italic: true), Blue, CellBorders.None), FormatAt(cut, "B3"));
        // The cell's own No Fill over the row's.
        Assert.Equal(CellFormat.Default with { Alignment = HorizontalAlignment.Center }, FormatAt(cut, "C3"));
        // It answers the Sheet as it stands, as the Sheet Document records it.
        var reopened = Sheet.Open(cut.Instance.ToDocument());
        foreach (var address in new[] { "B3", "C3", "D3", "B5", "D5" })
        {
            Assert.Equal(reopened.GetCellFormat(CellAddress.Parse(address)), FormatAt(cut, address));
        }
    }

    private static async Task CtrlClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);
        await cut.Find(".ex-viewport").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true,
            OffsetX = heading + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
        await cut.Find(".ex-viewport").MouseUpAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });
    }
}
