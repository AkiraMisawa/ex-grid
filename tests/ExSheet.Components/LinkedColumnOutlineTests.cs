using Bunit;
using ExGrid.Cells;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet tells its Consumer which Linked Table columns the Formula being edited reads, and in
/// which colour (ticket 30, ADR-0057 "A structured reference is outlined by whoever shows its
/// table", ADR-0049; SH-31's layer 2 half): each column once, under the names the Consumer
/// declared, in the colour its References wear; raised when that changes and never when it does
/// not, and emptied when the edit ends. A table or column not declared names nothing, and is not
/// told. ExSheet outlines none of it itself: it does not know where the table is shown.
/// </summary>
public class LinkedColumnOutlineTests : SheetTestContext
{
    private static readonly string[] Columns = ["Id", "Book", "PV"];

    private static LinkedColumnColour Told(string table, string column, int place) =>
        new(new LinkedTableColumn(table, column), new ReferenceColour(place));

    /// <summary>A Sheet with <c>Positions</c> declared and waiting for its data, and what it tells.</summary>
    private async Task<IRenderedComponent<SheetComponent>> RenderDeclaredAsync(List<LinkedColumnColours> told)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.OnLinkedColumnColoursChanged, (LinkedColumnColours columns) => told.Add(columns)));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        return cut;
    }

    /// <summary>Goes to <paramref name="address"/>, opens Overwrite with the first character and types the rest.</summary>
    private static async Task StartTypingAsync(IRenderedComponent<SheetComponent> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1) await TypeAsync(cut, typed);
    }

    /// <summary>Asserts each told column carries the names exactly as declared — ordinally, where
    /// <see cref="LinkedTableColumn"/>'s own equality ignores case.</summary>
    private static void AssertDeclaredNames(IReadOnlyList<LinkedColumnColour> expected, IReadOnlyList<LinkedColumnColour> actual)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(
            expected.Select(c => (c.Column.Table, c.Column.Column)),
            actual.Select(c => (c.Column.Table, c.Column.Column)));
    }

    [Fact] // ADR-0057 / SH-31: =SUM(Positions[PV]) tells Positions[PV] in the first colour, and outlines nothing on the Sheet
    public async Task A_formula_reading_a_linked_column_tells_it_and_its_colour()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);

        await StartTypingAsync(cut, "E5", "=SUM(Positions[PV])");

        AssertDeclaredNames([Told("Positions", "PV", 1)], Assert.Single(told));
        // The table is not on this grid: ExSheet has nowhere to outline it.
        Assert.Empty(cut.FindAll(".ex-reference-outline"));
    }

    [Fact] // ADR-0057 / SH-31: a column's colour is its place in order of first appearance, cells counted
    public async Task A_columns_colour_follows_the_references_before_it()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);

        await StartTypingAsync(cut, "E5", "=A1+XLOOKUP(\"R-1\", Positions[Id], Positions[PV])");

        AssertDeclaredNames([Told("Positions", "Id", 2), Told("Positions", "PV", 3)], told[^1]);
        Assert.Equal(["ex-reference-outline ex-reference-1"], cut.FindAll(".ex-selection .ex-reference-outline").Select(e => e.GetAttribute("class")));
    }

    [Fact] // ADR-0057 / SH-31: a column read twice, in any case, is told once, under the names as the Consumer declared them
    public async Task A_column_read_twice_in_any_case_is_told_once_as_declared()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);

        await StartTypingAsync(cut, "E5", "=positions[pv]+POSITIONS[Pv]");

        AssertDeclaredNames([Told("Positions", "PV", 1)], Assert.Single(told));
    }

    [Fact] // ADR-0057 / SH-31 / DC-49: raised when the columns or their colours change, and not on a keystroke that changes neither
    public async Task Told_only_when_the_columns_or_their_colours_change()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);
        await StartTypingAsync(cut, "E5", "=SUM(Positions[PV])");
        Assert.Single(told);

        await TypeAsync(cut, "=SUM(Positions[PV])+");
        await TypeAsync(cut, "=SUM(Positions[PV])+1");
        await TypeAsync(cut, "=SUM(Positions[PV])+1+positions[PV]");
        Assert.Single(told);

        // A range before it moves its colour on.
        await TypeAsync(cut, "=B2+SUM(Positions[PV])+1+positions[PV]");
        Assert.Equal(2, told.Count);
        AssertDeclaredNames([Told("Positions", "PV", 2)], told[^1]);
    }

    [Fact] // ADR-0057 / SH-31: Enter commits and tells the empty list, once
    public async Task A_commit_tells_the_empty_list()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);
        await StartTypingAsync(cut, "E5", "=SUM(Positions[PV])");

        await PressInEditorAsync(cut, "Enter", "=SUM(Positions[PV])", 19);

        Assert.Equal(2, told.Count);
        Assert.Empty(told[^1]);
        cut.Render();
        Assert.Equal(2, told.Count);
    }

    [Fact] // ADR-0057 / SH-31: Escape cancels and tells the empty list
    public async Task A_cancel_tells_the_empty_list()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);
        await StartTypingAsync(cut, "E5", "=Positions[Id]+Positions[PV]");

        await PressAsync(cut, "Escape");

        Assert.Equal(2, told.Count);
        AssertDeclaredNames([Told("Positions", "Id", 1), Told("Positions", "PV", 2)], told[0]);
        Assert.Empty(told[1]);
    }

    [Fact] // ADR-0057 "coloured only when its table and column are declared" / SH-31: an undeclared table or column is not told
    public async Task An_undeclared_table_or_column_is_not_told()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);

        await StartTypingAsync(cut, "E5", "=Nope[PV]+Positions[Nope]");
        Assert.Empty(told);

        await TypeAsync(cut, "=Nope[PV]+Positions[Nope]+Positions[Book]");
        AssertDeclaredNames([Told("Positions", "Book", 1)], Assert.Single(told));
    }

    [Fact] // ADR-0057 / SH-31: a Formula of cells alone reads no Linked Table, and tells nothing from start to end
    public async Task A_formula_of_cells_tells_nothing()
    {
        var told = new List<LinkedColumnColours>();
        var cut = await RenderDeclaredAsync(told);

        await StartTypingAsync(cut, "E5", "=A1+B2:C3");
        await PressAsync(cut, "Escape");

        Assert.Empty(told);
    }

    [Fact] // ADR-0057 / SH-31: with no table declared, Positions[PV] names nothing and is not told
    public async Task Nothing_is_told_before_the_table_is_declared()
    {
        var told = new List<LinkedColumnColours>();
        var cut = RenderSheet(ps => ps.Add(s => s.OnLinkedColumnColoursChanged, (LinkedColumnColours columns) => told.Add(columns)));

        await StartTypingAsync(cut, "E5", "=SUM(Positions[PV])");

        Assert.Empty(told);
    }

    [Fact] // ADR-0057 / DC-1: a Consumer that does not listen changes nothing: the Sheet still outlines its own References
    public async Task Without_a_listener_the_sheet_is_as_before()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);

        await StartTypingAsync(cut, "E5", "=A1+SUM(Positions[PV])");

        Assert.Equal(["ex-reference-outline ex-reference-1"], cut.FindAll(".ex-selection .ex-reference-outline").Select(e => e.GetAttribute("class")));
        await PressAsync(cut, "Escape");
        Assert.Empty(cut.FindAll(".ex-reference-outline"));
    }

    [Fact] // ADR-0057 / SH-31 / ADR-0049: a page passing the list to the grid that shows the table outlines its column there, over all its rows, until the edit ends
    public async Task A_page_passes_the_list_to_the_grid_that_shows_the_table()
    {
        var page = RenderPage<SheetBesideTable>();
        await page.Instance.Sheet!.DeclareLinkedTableAsync("Positions", Columns);
        var cut = page.FindComponent<SheetComponent>();
        var positions = page.FindComponent<global::ExGrid.Components.ExGrid<SheetBesideTable.Position>>();

        await StartTypingAsync(cut, "E5", "=A1+SUM(Positions[PV])");

        // PV is the third of three columns of 90, 90 and 120, and the table's three rows are 20px each.
        var outline = Assert.Single(positions.FindAll(".ex-reference-outline"));
        Assert.Equal("ex-reference-outline ex-reference-2", outline.GetAttribute("class"));
        Assert.Equal("left: 180px; top: 0px; width: 120px; height: 60px", outline.GetAttribute("style"));
        Assert.Equal(["ex-reference-outline ex-reference-1"], Grid(cut).FindAll(".ex-reference-outline").Select(e => e.GetAttribute("class")));

        await PressAsync(cut, "Escape");
        Assert.Empty(positions.FindAll(".ex-reference-outline"));
    }
}
