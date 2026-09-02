using System.Globalization;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The column's display format (ADR-0006): one text for a value, shown wherever the
/// grid shows the value — the cell, copy's text/plain, the value list, the editor's
/// opening text — and never in copy's raw text/html (ADR-0005).
/// </summary>
public class ColumnFormatTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    // Wide enough for the ISO text at the test metrics: at 100px the overflow rule paints
    // #### for the date — correctly, and beside the point here.
    private static readonly ColumnWidthSpec Fixed140 = new(ColumnWidth.Fixed(140));

    private static string Iso(object value) => ((DateTime)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed140, editable: true, format: Iso, filterUi: FilterUiMode.Both),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100,
            format: v => ((decimal)v).ToString("N2", CultureInfo.InvariantCulture)),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(TestSource? source = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            if (source is null)
                ps.Add(g => g.Window, TestRows.Window());
            else
                ps.Add(g => g.Source, source);
            ps.Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0006: the cell paints the column's format, not the value's ToString
    public void The_cell_paints_the_format()
    {
        var cut = RenderGrid();

        var cells = cut.FindAll(".ex-row").First().QuerySelectorAll(".ex-cell").Select(c => c.TextContent).ToList();
        Assert.Equal(["2026-01-05", "100.50"], cells);
    }

    [Fact] // ADR-0005 / CP-4: copy's text/plain is the format; its text/html stays raw and ISO
    public async Task Copy_shows_the_format_in_text_and_the_raw_value_in_html()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("2026-01-05\t100.50\r\n", payload.Text);
        Assert.Equal("<table><tr><td>2026-01-05T00:00:00</td><td>100.5</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0009: the value list offers the values in the column's format
    public async Task The_value_list_speaks_the_format()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        source.DistinctAnswer = DistinctValues.Of([new DateTime(2026, 1, 5), new DateTime(2026, 1, 6), null]);
        var cut = RenderGrid(source);

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".ex-popover button[role=menuitem]")
            .Single(b => b.TextContent == "Filter").ClickAsync(new MouseEventArgs());

        var labels = cut.FindAll(".ex-popover-list label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["2026-01-05", "2026-01-06", "(Blanks)"], labels);
    }

    [Fact] // ADR-0010: the editor opens on the formatted text, the one the reader was looking at
    public async Task The_editor_opens_with_the_format()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        Assert.Equal("2026-01-05", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0023: a null value is a Blank and paints empty; the format is never asked about it
    public void A_null_value_paints_empty_without_asking_the_format()
    {
        var asked = false;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, [new TestRow { Book = "" }])
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, _ => null, width: Fixed100,
                format: _ => { asked = true; return "x"; })])
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350));

        Assert.Equal("", cut.Find(".ex-cell").TextContent);
        Assert.False(asked);
    }
}
