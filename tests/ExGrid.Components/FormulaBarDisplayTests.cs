using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The editor opens on the Consumer's text, and the Formula Bar shows it (ADR-0051;
/// DC-16, DC-21, DC-23). 50 rows of 20px — so a 20px header and a 20px bar — in a
/// 350 × 200 Viewport. Book is editable and 100px wide; Amount is a 60px Number, narrow
/// enough to paint #### for most values.
/// </summary>
public class FormulaBarDisplayTests : GridTestContext
{
    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(60)), format: v => ((decimal)v).ToString("N0", CultureInfo.InvariantCulture)),
    ];

    private static TestRow[] Rows()
    {
        var rows = TestRows.Many(50);
        rows[3].Amount = 123456789.125m;
        return rows;
    }

    // The Consumer's opening text: an "Entry" for Book on row 2 only; null everywhere else.
    private static string? Entry(TestRow row, GridColumn<TestRow> column)
        => column.Name == "Book" && row.Book == "Row 000002" ? "=A1*2" : null;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null,
        Action<GridSelection>? onSelection = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, Rows())
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }));
            extra?.Invoke(ps);
        });

    private static void WithBar(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.ShowFormulaBar, true)
             .Add(g => g.EditorTextOf, Entry)
             .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static string BarText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    [Fact] // ADR-0051 / DC-16: F2 on a cell with an opening text opens the editor on it, not on the value
    public async Task F2_opens_on_the_consumers_text()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.EditorTextOf, Entry));
        await ClickAsync(cut, 50, 45);

        await PressAsync(cut, "F2");

        Assert.Equal("=A1*2", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0051 / DC-16: a double click opens Caret on the opening text too
    public async Task A_double_click_opens_on_the_consumers_text()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.EditorTextOf, Entry));

        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 45 });

        Assert.Equal("=A1*2", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0051/0010 / DC-16: Overwrite keeps its meaning — the typed character replaces
    public async Task Typing_still_replaces_with_the_character()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.EditorTextOf, Entry));
        await ClickAsync(cut, 50, 45);

        await PressAsync(cut, "7");

        Assert.Equal("7", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0051 / DC-16: a null answer, or no function at all, opens on the value as before
    public async Task Without_an_answer_the_editor_opens_on_the_value()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.EditorTextOf, Entry));
        await ClickAsync(cut, 50, 25);

        await PressAsync(cut, "F2");

        Assert.Equal("Row 000001", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0051 / DC-21: declared, a band inside the root, with the Name Box's label and the Focus cell's text
    public async Task The_bar_follows_the_focus_with_the_label_and_the_opening_text()
    {
        var cut = RenderGrid(WithBar);
        var bar = cut.Find(".ex-grid > .ex-formula-bar");
        Assert.Equal("", BarText(cut));

        await ClickAsync(cut, 50, 45);
        Assert.Equal("R3C1", cut.Find(".ex-name-box").GetAttribute("value"));
        Assert.Equal("=A1*2", BarText(cut));

        await PressAsync(cut, "ArrowDown");
        Assert.Equal("R4C1", cut.Find(".ex-name-box").GetAttribute("value"));
        Assert.Equal("Row 000003", BarText(cut));
        Assert.NotNull(bar);
    }

    [Fact] // ADR-0051/0016 / DC-21: on a display grid the bar shows the full value behind a ####
    public async Task On_a_display_grid_the_bar_shows_the_full_value_behind_hashes()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFormulaBar, true));

        await ClickAsync(cut, 130, 65);

        var cell = cut.FindAll(".ex-row")[3].Children[1];
        Assert.DoesNotContain(cell.TextContent, c => c != '#');
        Assert.Equal(123456789.125m.ToString(null, CultureInfo.CurrentCulture), BarText(cut));
        // No label supplied: the grid names no cell in words of its own.
        Assert.Equal("", cut.Find(".ex-name-box").GetAttribute("value") ?? "");
    }

    [Fact] // ADR-0051/0028 / DC-23: the bar's height comes out of the Viewport and the rows take what it leaves
    public async Task The_rows_take_what_the_bar_leaves()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(WithBar, s => selection = s);

        Assert.Contains("padding-top: 20px", cut.Find(".ex-grid").GetAttribute("style"));
        Assert.StartsWith("height: 20px", cut.Find(".ex-formula-bar").GetAttribute("style"));
        Assert.Contains("height: 180px", cut.Find(".ex-scroller").GetAttribute("style"));
        // 200 - 20 (bar) - 20 (header) = 160px of rows: eight fit, so PageDown moves eight.
        await ClickAsync(cut, 50, 5);
        await PressAsync(cut, "PageDown");
        Assert.Equal(new CellPosition(8, 0), selection!.Focus);
    }

    [Fact] // ADR-0051/0028 / DC-23: switched off, the geometry is as before
    public async Task Switched_off_the_geometry_is_as_before()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelection: s => selection = s);

        Assert.Empty(cut.FindAll(".ex-formula-bar"));
        Assert.DoesNotContain("padding-top", cut.Find(".ex-grid").GetAttribute("style"));
        Assert.Contains("height: 200px", cut.Find(".ex-scroller").GetAttribute("style"));
        await ClickAsync(cut, 50, 5);
        await PressAsync(cut, "PageDown");
        Assert.Equal(new CellPosition(9, 0), selection!.Focus);
    }

    [Fact] // ADR-0051/0028: a Viewport that cannot hold the bar and the header is refused by name
    public void A_viewport_too_short_for_the_bar_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, Rows())
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 40)
              .Add(g => g.ViewportWidth, 350);
            WithBar(ps);
        }));
        Assert.Contains("Formula Bar", refusal.Message);
    }

    [Fact] // ADR-0051/0040: a popover stands under the header band, which now starts below the bar
    public async Task A_column_popover_stands_below_the_bar_and_the_header()
    {
        var cut = RenderGrid(ps =>
        {
            WithBar(ps);
            ps.Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { });
        });

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        Assert.Contains("top: 40px", cut.Find(".ex-popover").GetAttribute("style"));
    }

    [Fact] // ADR-0003: the bar following the Focus renders no row
    public async Task The_bar_following_the_focus_renders_no_row()
    {
        var cut = RenderGrid(WithBar);
        await ClickAsync(cut, 50, 45);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "ArrowRight");

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0036/0051: the bar's accessible names are the built-in Chrome's words, by id, and English only as the fallback
    public void The_bars_accessible_names_come_through_command_label()
    {
        var english = RenderGrid(WithBar);
        Assert.Equal("Name Box", english.Find("input.ex-name-box").GetAttribute("aria-label"));
        Assert.Equal("Formula Bar", english.Find("input.ex-formula-bar-text").GetAttribute("aria-label"));

        var renamed = RenderGrid(ps =>
        {
            WithBar(ps);
            ps.Add(g => g.CommandLabel, id => id switch
            {
                global::ExGrid.Chrome.GridLabelIds.NameBox => "Namenfeld",
                global::ExGrid.Chrome.GridLabelIds.FormulaBar => "Bearbeitungsleiste",
                _ => null,
            });
        });

        Assert.Equal("Namenfeld", renamed.Find("input.ex-name-box").GetAttribute("aria-label"));
        Assert.Equal("Bearbeitungsleiste", renamed.Find("input.ex-formula-bar-text").GetAttribute("aria-label"));
    }
}
