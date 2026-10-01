using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Escape with nothing left to dismiss releases Tab, not DOM focus (ADR-0012, rewritten
/// 2026-10-01; KB-8). The core's half: it tells the gate to release Tab, moves no focus, and keeps
/// no state of its own, so every key after the release means what it always means. Which keys
/// the gate then leaves to the browser, and that the next Tab really leaves, is layer 3's. Book
/// (A) edits; 50 rows of 20px in a 350 × 200 Viewport.
/// </summary>
public class EscapeReleasesTabTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(List<GridSelection> selections)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.SelectionChanged, (GridSelection s) => selections.Add(s)));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, editorText: text, editorCaret: caret));

    private static string? EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-viewport .ex-editor").SingleOrDefault()?.GetAttribute("value");

    [Fact] // ADR-0012 (rewritten 2026-10-01) / KB-8 / ED-3: the Escape that cancels an edit releases nothing; the next releases Tab, and a character then opens an edit in the selected cell
    public async Task Escape_escape_then_a_character_opens_an_edit_in_the_selected_cell()
    {
        var selections = new List<GridSelection>();
        var cut = RenderGrid(selections);
        await ClickAsync(cut, 50, 25);
        await PressAsync(cut, "Q");
        Assert.Equal("Q", EditorText(cut));

        await PressAsync(cut, "Escape", text: "Q", caret: 1);
        Assert.Null(EditorText(cut));
        Assert.Equal(0, Js.TabReleases);

        await PressAsync(cut, "Escape");
        Assert.Equal(1, Js.TabReleases);
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);

        await PressAsync(cut, "k");
        Assert.Equal("k", EditorText(cut));
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);
    }

    [Fact] // ADR-0012 (rewritten 2026-10-01) / KB-8: after the release an arrow moves and Tab cycles; the core keeps no release of its own
    public async Task After_the_release_an_arrow_moves_and_tab_cycles()
    {
        var selections = new List<GridSelection>();
        var cut = RenderGrid(selections);
        await ClickAsync(cut, 50, 25);

        await PressAsync(cut, "Escape");
        await PressAsync(cut, "ArrowDown");
        Assert.Equal(new CellPosition(2, 0), selections[^1].Focus);

        await PressAsync(cut, "Tab");
        Assert.Equal(new CellPosition(2, 1), selections[^1].Focus);
        Assert.Equal(1, Js.TabReleases);
    }

    [Fact] // ADR-0012 (rewritten 2026-10-01) / KB-8: each Escape with nothing to dismiss releases Tab again
    public async Task Each_escape_with_nothing_to_dismiss_releases_tab_again()
    {
        var cut = RenderGrid([]);
        await ClickAsync(cut, 50, 25);

        await PressAsync(cut, "Escape");
        await PressAsync(cut, "Escape");

        Assert.Equal(2, Js.TabReleases);
    }
}
