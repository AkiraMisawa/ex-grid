using System.Globalization;
using Bunit;
using ExGrid.Components;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Inserting and deleting rows and columns from the Context Menu (ticket 13, ADR-0046/0047/0048):
/// References keep naming the same cells, a deleted target is <c>#REF!</c>, the Row Sequence
/// Version and the Selection stay where they were, and each command is one undo step.
/// </summary>
public class StructureCommandTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    /// <summary>A secondary click on the cell at <paramref name="address"/>, which opens the Context Menu.</summary>
    private static Task SecondaryClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        return cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs
        {
            Button = 2,
            OffsetX = HeadingWidth(cut) + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
    }

    private static Task ChooseAsync(IRenderedComponent<ExSheet> cut, string label) =>
        cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == label).ClickAsync(new MouseEventArgs());

    private static string FormulaBar(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    [Fact] // ADR-0036/0046: the Context Menu carries ExSheet's four structural commands, in its words
    public async Task The_menu_carries_the_structural_commands()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");

        var labels = cut.FindAll("[role=menu] button[role=menuitem]").Select(b => b.TextContent).ToList();
        Assert.Contains("Insert rows above", labels);
        Assert.Contains("Delete rows", labels);
        Assert.Contains("Insert columns to the left", labels);
        Assert.Contains("Delete columns", labels);
    }

    [Fact] // ADR-0036: a Consumer's wording for ExSheet's ids stands over ExSheet's own
    public async Task The_consumer_words_the_commands()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.CommandLabel, id => id == SheetCommandIds.InsertRows ? "Zeilen einfügen" : null));
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");

        var labels = cut.FindAll("[role=menu] button[role=menuitem]").Select(b => b.TextContent).ToList();
        Assert.Contains("Zeilen einfügen", labels);
        Assert.Contains("Delete rows", labels);
    }

    [Fact] // ADR-0046/0047, SH-5: inserting above a referenced cell rewrites every Reference to it; the Selection stays
    public async Task Inserting_above_a_referenced_cell_rewrites_the_reference_and_keeps_the_selection()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A3", "5"), ("C1", "=A3*2"), ("C2", "=SUM(A1:A3)")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "A2");
        var selected = selections[^1];
        var told = selections.Count;

        await SecondaryClickAsync(cut, "A2");
        await ChooseAsync(cut, "Insert rows above");

        Assert.Equal("", CellText(cut, "A3"));
        Assert.Equal("5", CellText(cut, "A4"));
        Assert.Equal("10", CellText(cut, "C1"));
        // Rows are places: the grid was never told of a new Selection, and it stands where it was.
        Assert.Equal(told, selections.Count);
        Assert.Equal(selected, selections[^1]);
        Assert.Equal(0, Grid(cut).Instance.RowSequenceVersion);
        await GoToAsync(cut, "C1");
        Assert.Equal("=A4*2", FormulaBar(cut));
        // C2 was on the inserted row, so it moved down with it; its range grew by the row inserted inside it.
        await GoToAsync(cut, "C3");
        Assert.Equal("=SUM(A1:A4)", FormulaBar(cut));
    }

    [Fact] // ADR-0046: a Selection of two rows inserts two rows
    public async Task A_selection_of_two_rows_inserts_two()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"))));
        await GoToAsync(cut, "A2:B3");

        await SecondaryClickAsync(cut, "A2");
        await ChooseAsync(cut, "Insert rows above");

        Assert.Equal("", CellText(cut, "A2"));
        Assert.Equal("x", CellText(cut, "A4"));
    }

    [Fact] // ADR-0047, SH-5: deleting a referenced cell makes the Formula #REF!
    public async Task Deleting_a_referenced_cell_is_ref()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B2", "5"), ("D1", "=B2+1"))));
        await GoToAsync(cut, "B2");

        await SecondaryClickAsync(cut, "B2");
        await ChooseAsync(cut, "Delete columns");

        Assert.Equal("#REF!", CellText(cut, "C1"));
        await GoToAsync(cut, "C1");
        Assert.Equal("=#REF!+1", FormulaBar(cut));
    }

    [Fact] // ADR-0048: one undo restores the structure and every Reference
    public async Task One_undo_restores_the_structure_and_every_reference()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "5"), ("B1", "=A2*2"))));
        await GoToAsync(cut, "A2");
        await SecondaryClickAsync(cut, "A2");
        await ChooseAsync(cut, "Delete rows");
        Assert.Equal("#REF!", CellText(cut, "B1"));

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("5", CellText(cut, "A2"));
        Assert.Equal("10", CellText(cut, "B1"));
        await GoToAsync(cut, "B1");
        Assert.Equal("=A2*2", FormulaBar(cut));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0046/0003, SH-4: an insertion repaints the rows it changed, and every other row skips its render
    public async Task An_insertion_repaints_only_the_rows_it_changed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "top"), ("A4", "4"))));
        await GoToAsync(cut, "A3");
        var before = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => r.Instance.Row);

        await SecondaryClickAsync(cut, "A3");
        await ChooseAsync(cut, "Insert rows above");

        foreach (var row in cut.FindComponents<ExGridRow<SheetRow>>())
        {
            if (row.Instance.RowIndex is 3 or 4) Assert.NotSame(before[row.Instance.RowIndex], row.Instance.Row);
            else Assert.Same(before[row.Instance.RowIndex], row.Instance.Row);
        }
    }

    [Fact] // ADR-0047/0035: an insertion the Sheet refuses changes nothing and says why
    public async Task A_refused_insertion_says_why()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1048576", "last"))));
        await GoToAsync(cut, "A2");

        await SecondaryClickAsync(cut, "A2");
        await ChooseAsync(cut, "Insert rows above");

        Assert.Contains("A1048576", cut.Find(".ex-sheet-notice").TextContent);
        Assert.False(cut.Instance.CanUndo);
    }
}
