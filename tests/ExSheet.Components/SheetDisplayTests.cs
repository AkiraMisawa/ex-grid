using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// What a cell shows (ticket 05): the engine's formatted text under the Sheet's culture, a kind
/// per cell through ExGrid's <c>CellType</c> (ADR-0050, item 6), and <c>####</c> for a number that
/// does not fit, decided by ExGrid's own rule (ADR-0016, DC-26, SH-10).
/// </summary>
public class SheetDisplayTests : SheetTestContext
{
    private static SheetDocument DocumentOf(string culture, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    [Fact] // ADR-0050 item 6, DC-26: a number is right-aligned and a text left-aligned in the same column
    public void The_kind_is_per_cell()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "Amount"), ("A2", "12.5"))));

        Assert.DoesNotContain("ex-cell-numeric", Cell(cut, "A1").ClassName);
        Assert.Contains("ex-cell-numeric", Cell(cut, "A2").ClassName);
    }

    [Fact] // ADR-0016, SH-10: a number too wide for its column is ####, and its accessible name is the number
    public void A_number_too_wide_is_hashed_by_the_grid()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "123456789012345"))));

        var cell = Cell(cut, "A1");
        Assert.Matches("^#+$", cell.TextContent);
        Assert.Equal("123456789012345", cell.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0016: a text too wide is never hashed; it is cut, visibly (ADR-0046)
    public void A_text_too_wide_is_not_hashed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "A heading far wider than its column"))));

        Assert.Equal("A heading far wider than its column", CellText(cut, "A1"));
    }

    [Fact] // ADR-0047, SH-10: at most 15 significant digits; the grid hashes what does not fit, never a shorter number
    public void Fifteen_significant_digits_and_no_shorter_number()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "=1/3"))));

        var cell = Cell(cut, "A1");
        Assert.Equal("0.333333333333333", cell.GetAttribute("aria-label"));
        Assert.Matches("^#+$", cell.TextContent);
    }

    [Fact] // ADR-0048: a date typed under ja-JP is a date, shown under the Sheet's culture, right-aligned as a number
    public void A_date_typed_under_ja_jp_shows_as_a_date()
    {
        var document = DocumentOf("ja-JP", ("A1", "2026/9/26"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, document));

        var expected = Sheet.Open(document).GetDisplay(CellAddress.Parse("A1")).Text;
        Assert.Equal("2026/09/26", expected);
        // Ten characters are wider than the default column in the grid's font, so the grid
        // hashes it (ADR-0016) and the date is the cell's accessible name.
        var cell = Cell(cut, "A1");
        Assert.Equal(expected, cell.GetAttribute("aria-label") ?? cell.TextContent);
        Assert.Contains("ex-cell-numeric", Cell(cut, "A1").ClassName);
    }

    [Fact] // ADR-0047: a number no format can show — a negative date — is #### at any width, as in Excel
    public async Task A_number_that_cannot_be_shown_is_hashed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "-1"))));
        await GoToAsync(cut, "A1");

        Assert.True(await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("yyyy-mm-dd")));

        Assert.Matches("^#+$", CellText(cut, "A1"));
    }

    [Fact] // ADR-0046: a number format set on the selection is one step, painted from the engine's text
    public async Task A_number_format_on_the_selection_repaints_its_cells()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "1234.5"), ("A2", "0.25"))));
        await GoToAsync(cut, "A1:A2");

        Assert.True(await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("#,##0.00")));

        Assert.Equal("1,234.50", CellText(cut, "A1"));
        Assert.Equal("0.25", CellText(cut, "A2"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("1234.5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a percent format under de-DE writes the culture's decimal separator
    public async Task A_format_follows_the_sheets_culture()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("de-DE", ("A1", "0,125"))));
        await GoToAsync(cut, "A1");

        await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("0.0%"));

        Assert.Equal("12,5%", CellText(cut, "A1"));
    }

    [Fact] // ADR-0046: alignment is recorded on the selection as one step and travels in the Sheet Document
    public async Task Alignment_on_the_selection_is_recorded()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(cut, "B2", "x");
        await GoToAsync(cut, "B2");

        Assert.True(await cut.Instance.SetAlignmentAsync(HorizontalAlignment.Center));

        Assert.Equal(HorizontalAlignment.Center, Assert.Single(raised!.Cells).Alignment);
    }

    [Fact] // ADR-0050 item 7, DC-29: General leaves the kind to align — numbers right, text left — with no class of its own
    public void General_alignment_leaves_the_kind_to_decide()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "Amount"), ("A2", "12.5"))));

        Assert.DoesNotContain("ex-align-", Cell(cut, "A1").ClassName);
        Assert.DoesNotContain("ex-align-", Cell(cut, "A2").ClassName);
        Assert.Contains("ex-cell-numeric", Cell(cut, "A2").ClassName);
    }

    [Fact] // ADR-0050 item 7, DC-29: booleans and Error Values are centred under General, as Excel centres them
    public void Booleans_and_error_values_are_centred()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "TRUE"), ("A2", "=1/0"))));

        Assert.Contains("ex-align-center", Cell(cut, "A1").ClassName);
        Assert.Contains("ex-align-center", Cell(cut, "A2").ClassName);
    }

    [Fact] // ADR-0050 item 7, DC-29: a user's own alignment paints the cell, and undoing it paints the kind's again
    public async Task A_users_alignment_paints_the_cell()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "12.5"), ("A2", "text"))));
        await GoToAsync(cut, "A1:A2");

        Assert.True(await cut.Instance.SetAlignmentAsync(HorizontalAlignment.Left));

        Assert.Contains("ex-align-left", Cell(cut, "A1").ClassName);
        Assert.Contains("ex-align-left", Cell(cut, "A2").ClassName);
        await cut.Instance.SetAlignmentAsync(HorizontalAlignment.Center);
        Assert.Contains("ex-align-center", Cell(cut, "A1").ClassName);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.True(await cut.Instance.UndoAsync());
        Assert.DoesNotContain("ex-align-", Cell(cut, "A1").ClassName);
    }

    [Fact] // ADR-0050 item 7 / ADR-0003: the alignment delegate is one held instance, and an alignment repaints only its rows
    public async Task An_alignment_repaints_only_its_rows()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf("en-US", ("A1", "1"), ("A3", "3"))));
        var held = Grid(cut).Instance.CellAlign;
        await GoToAsync(cut, "A3");
        var before = cut.FindComponents<global::ExGrid.Components.ExGridRow<SheetRow>>()
            .ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

        await cut.Instance.SetAlignmentAsync(HorizontalAlignment.Right);

        Assert.Same(held, Grid(cut).Instance.CellAlign);
        Assert.Contains("ex-align-right", Cell(cut, "A3").ClassName);
        foreach (var row in cut.FindComponents<global::ExGrid.Components.ExGridRow<SheetRow>>())
        {
            var (instance, renders) = before[row.Instance.RowIndex];
            if (row.Instance.RowIndex == 2) Assert.NotSame(instance, row.Instance.Row);
            else
            {
                Assert.Same(instance, row.Instance.Row);
                Assert.Equal(renders, row.RenderCount);
            }
        }
    }

    [Fact] // Principle 1: formatting a million cells at once is refused by name, and nothing changes
    public async Task Formatting_whole_columns_is_refused_by_name()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B:B");

        Assert.False(await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("0.00")));

        Assert.Contains("1,048,576 cells", cut.Find(".ex-sheet-notice").TextContent);
        Assert.False(cut.Instance.CanUndo);
    }
}
