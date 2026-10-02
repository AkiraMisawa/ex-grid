using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Position = ExSheet.Components.Tests.Support.ScopedSheets.Position;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// What a Pointing Scope's grids draw while a Sheet points (ticket 38, ADR-0058 "What is drawn";
/// ADR-0057, "In the grids of a Pointing Scope"; SH-34's layer 2 half): the Linked Table columns the
/// Formula reads are outlined in the registered grid, through its correspondence, with no
/// <c>OutlinedColumns</c> written by the page, until the edit ends; the pressed cell gets only dashes,
/// found by its row's key, and a pressed column header dashes the column; the dashes go when Point
/// ends; and the written lookup lies on the grey ground as a whole, unless it follows the <c>=</c>.
/// The page is <see cref="Support.ScopedSheets"/>, whose registered grid names the table's PV
/// <c>Value</c>.
/// </summary>
public partial class PointingScopeTests
{
    // A column outline in the first colour, and in the second.
    private const string FirstColour = "ex-reference-outline ex-reference-1";
    private const string SecondColour = "ex-reference-outline ex-reference-2";

    /// <summary>The column outlines a grid draws, as their class and their left edge in px.</summary>
    private static List<(string? Class, double Left)> ColumnOutlines(IRenderedComponent<ExGrid<Position>> grid)
        => [.. grid.FindAll(".ex-selection .ex-reference-outline").Select(e => (e.GetAttribute("class"), LeftOf(e.GetAttribute("style"))))];

    /// <summary>The dashes a grid draws, by their style.</summary>
    private static List<string?> DashesOf(IRenderedComponent<ExGrid<Position>> grid)
        => [.. grid.FindAll(".ex-point-dashes").Select(e => e.GetAttribute("style"))];

    private static double LeftOf(string? style)
        => double.Parse(Regex.Match(style ?? "", @"left: (-?[\d.]+)px").Groups[1].Value, CultureInfo.InvariantCulture);

    /// <summary>The dashes over the cell of the Value column in the <paramref name="row"/>th row.</summary>
    private static string ValueCellDashes(int row) => FormattableString.Invariant($"left: 200px; top: {row * 20}px; width: 100px; height: 20px");

    /// <summary>What the layer beneath a Sheet's Cell Editor draws, as markup.</summary>
    private static string CellEditorLayer(IRenderedComponent<SheetComponent> sheet)
        => global::ReferenceText.ColouredText.Of(Grid(sheet).Find(".ex-viewport > .ex-reference-text"));

    private static int ScrollsAsked(BunitJSInterop js)
        => js.Invocations.Count(i => i.Identifier is "setScrollOffset" or "anchorScrollTop");

    [Fact] // ADR-0058 / ADR-0057 / SH-34: the Linked Table columns a Formula reads are outlined in the registered grid, through its correspondence, in no grid outside the Scope, until the edit ends
    public async Task The_columns_a_formula_reads_are_outlined_in_the_registered_grid()
    {
        var page = await RenderAsync();

        await StartTypingAsync(page.Left, "D2", "=XLOOKUP(\"R-1\", positions[id], Positions[PV])");

        // Id in the first colour over the grid's Id, PV in the second over Value, the grid column
        // that is the table's PV; the page wired no OutlinedColumns.
        page.Positions.WaitForAssertion(() => Assert.Equal([(FirstColour, 0), (SecondColour, 200)], ColumnOutlines(page.Positions)));
        Assert.Empty(page.Unregistered.FindAll(".ex-reference-outline"));
        Assert.Empty(DashesOf(page.Positions));

        // The keyboard leaving the Sheet takes none away: they stand while the edit is open.
        await KeyboardOutAsync(page.Left);
        Assert.Equal([(FirstColour, 0), (SecondColour, 200)], ColumnOutlines(page.Positions));
        await KeyboardInAsync(page.Left);

        await PressAsync(page.Left, "Escape");
        page.Positions.WaitForAssertion(() => Assert.Empty(ColumnOutlines(page.Positions)));
    }

    [Fact] // ADR-0058 / ADR-0057 / SH-34: two Sheets of a Scope each have their columns outlined, the later told over the earlier, and each goes when its own edit ends
    public async Task Each_sheets_columns_are_outlined_until_its_own_edit_ends()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(Positions[PV])");
        page.Positions.WaitForAssertion(() => Assert.Equal([(FirstColour, 200)], ColumnOutlines(page.Positions)));

        await KeyboardOutAsync(page.Left);
        await StartTypingAsync(page.Right, "E5", "=COUNTA(Positions[Id])");

        page.Positions.WaitForAssertion(() => Assert.Equal([(FirstColour, 200), (FirstColour, 0)], ColumnOutlines(page.Positions)));
        await PressAsync(page.Right, "Escape");
        page.Positions.WaitForAssertion(() => Assert.Equal([(FirstColour, 200)], ColumnOutlines(page.Positions)));
    }

    [Fact] // ADR-0058 / SH-34 / DC-53: a pressed cell gets only dashes, found by its row's key — through a sort and new instances — drawn nowhere while the row is not painted, and never scrolled to
    public async Task A_pressed_cell_is_dashed_by_its_rows_key()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(1,");

        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal("=SUM(1," + LookupR2, EditorText(page.Left));
        page.Positions.WaitForAssertion(() => Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions)));
        // No outline of its own: the Formula reads that cell by key, not by position. Its two
        // References are outlined over their columns.
        page.Positions.WaitForAssertion(() => Assert.Equal([(FirstColour, 0), (SecondColour, 200)], ColumnOutlines(page.Positions)));
        Assert.Empty(page.Unregistered.FindAll(".ex-point-dashes"));

        // The Consumer sorts: R-2 is first.
        var rows = Support.ScopedSheets.Positions;
        await page.Cut.Instance.ShowAsync([rows[1], rows[0], rows[2], rows[3]]);
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));

        // A Window of new instances, in another order: found by its key all the same.
        await page.Cut.Instance.ShowAsync([.. rows.Reverse().Select(p => p with { PV = p.PV + 1 })]);
        Assert.Equal([ValueCellDashes(2)], DashesOf(page.Positions));

        // Far down the grid, and not painted: nothing is drawn, and the grid does not scroll to it.
        var scrolls = ScrollsAsked(JSInterop);
        Position[] far = [.. Enumerable.Range(0, 60).Select(i => new Position($"F-{i}", "Filler", i, "")), rows[1]];
        await page.Cut.Instance.ShowAsync(far);
        Assert.Empty(DashesOf(page.Positions));
        Assert.Equal(scrolls, ScrollsAsked(JSInterop));

        await page.Cut.Instance.ShowAsync(rows);
        Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions));
        Assert.Equal("=SUM(1," + LookupR2, EditorText(page.Left));
    }

    [Fact] // ADR-0058 / SH-34: a pressed column header dashes the whole of that column's body
    public async Task A_pressed_column_header_dashes_the_column()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressHeaderAsync(page.Positions, ValueX);

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        page.Positions.WaitForAssertion(() =>
            Assert.StartsWith("left: 200px; top: 0px; width: 100px; height: ", Assert.Single(DashesOf(page.Positions))));
    }

    [Theory] // ADR-0058 / SH-34: the dashes go when Point ends — an operator typed, the caret moved, a commit or a cancel — and the column outlines stay until the edit ends
    [InlineData("an operator")]
    [InlineData("the caret")]
    [InlineData("Enter")]
    [InlineData("Escape")]
    public async Task The_dashes_go_when_point_ends(string how)
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1+");
        await PressCellAsync(page.Positions, ValueX, Row(1));
        page.Positions.WaitForAssertion(() => Assert.Single(DashesOf(page.Positions)));
        // The keyboard leaving the Sheet ends nothing: the edit, and Point in it, stand.
        await KeyboardOutAsync(page.Left);
        Assert.Single(DashesOf(page.Positions));
        await KeyboardInAsync(page.Left);

        var written = "=1+" + LookupR2;
        switch (how)
        {
            case "an operator":
                await TypeAsync(page.Left, written + "+");
                break;
            case "the caret":
                await ReportCaretAsync(page.Left, written, caret: 3);
                break;
            default:
                await PressAsync(page.Left, how);
                break;
        }

        page.Positions.WaitForAssertion(() => Assert.Empty(DashesOf(page.Positions)));
        if (how is "an operator" or "the caret")
            Assert.Equal([(FirstColour, 0), (SecondColour, 200)], ColumnOutlines(page.Positions));
        else
            page.Positions.WaitForAssertion(() => Assert.Empty(ColumnOutlines(page.Positions)));
    }

    [Fact] // ADR-0058 / SH-34: a press on the Sheet's own cell replaces what the press on the grid wrote, and its dashes go
    public async Task A_press_on_the_sheet_takes_the_dashes_away()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1+");
        await PressCellAsync(page.Positions, ValueX, Row(1));
        page.Positions.WaitForAssertion(() => Assert.Single(DashesOf(page.Positions)));

        await PressSheetCellAsync(page.Left, row: 2, column: 1);

        Assert.Equal("=1+B3", EditorText(page.Left));
        page.Positions.WaitForAssertion(() => Assert.Empty(DashesOf(page.Positions)));
    }

    [Fact] // ADR-0058 / SH-34: a further press moves the dashes, and a drag that takes back what its press wrote takes its dashes back with it
    public async Task The_dashes_follow_what_stands_written()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        var viewport = page.Positions.Find(".ex-viewport");

        // A press, and its drag onto another cell: the dashes go with the text.
        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = ValueX, OffsetY = Row(1), ClientX = ValueX, ClientY = 40 + Row(1) });
        page.Positions.WaitForAssertion(() => Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions)));
        await page.Positions.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = ValueX, OffsetY = Row(2), ClientX = ValueX, ClientY = 40 + Row(2) });
        Assert.Equal("=SUM(", EditorText(page.Left));
        page.Positions.WaitForAssertion(() => Assert.Empty(DashesOf(page.Positions)));
        await page.Positions.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = ValueX, OffsetY = Row(2) });

        // R-1 pressed, then R-2's Id pressed: the dashes move.
        await PressCellAsync(page.Positions, ValueX, Row(0));
        page.Positions.WaitForAssertion(() => Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions)));
        await page.Positions.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = IdX, OffsetY = Row(1), ClientX = IdX, ClientY = 40 + Row(1) });
        Assert.Equal("=SUM(XLOOKUP(\"R-2\", Positions[Id], Positions[Id])", EditorText(page.Left));
        page.Positions.WaitForAssertion(() => Assert.Equal(["left: 0px; top: 20px; width: 100px; height: 20px"], DashesOf(page.Positions)));

        // That press dragged: R-1's lookup stands again, and so do its dashes.
        await page.Positions.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = IdX, OffsetY = Row(2), ClientX = IdX, ClientY = 40 + Row(2) });

        Assert.Equal("=SUM(XLOOKUP(\"R-1\", Positions[Id], Positions[PV])", EditorText(page.Left));
        page.Positions.WaitForAssertion(() => Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions)));
    }

    [Fact] // ADR-0058 / ADR-0057 / SH-34: the written lookup lies on the grey ground as one span, its two column references in their colours inside it
    public async Task The_written_lookup_is_shown_selected_as_a_whole()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(1,");

        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal(
            "=SUM(1,<span class=\"ex-reference-pointed\">XLOOKUP(\"R-2\", <span class=\"ex-reference-1\">Positions[Id]</span>, "
                + "<span class=\"ex-reference-2\">Positions[PV]</span>)</span>",
            CellEditorLayer(page.Left));

        // A column header's structured reference is one Reference, which wears the look itself.
        await PressHeaderAsync(page.Positions, ValueX);
        Assert.Equal("=SUM(1,<span class=\"ex-reference-1 ex-reference-pointed\">Positions[PV]</span>", CellEditorLayer(page.Left));

        // Typed on, Point ends, and so does the look.
        await TypeAsync(page.Left, "=SUM(1,Positions[PV])");
        Assert.Equal("=SUM(1,<span class=\"ex-reference-1\">Positions[PV]</span>)", CellEditorLayer(page.Left));
    }

    [Fact] // ADR-0058 / ADR-0057 (cases 19, 20x) / SH-34: a lookup written straight after the = is not shown selected
    public async Task A_lookup_written_straight_after_the_equals_sign_is_not_shown_selected()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal(
            "=XLOOKUP(\"R-2\", <span class=\"ex-reference-1\">Positions[Id]</span>, <span class=\"ex-reference-2\">Positions[PV]</span>)",
            CellEditorLayer(page.Left));
        // It is dashed all the same.
        page.Positions.WaitForAssertion(() => Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions)));
    }
}
