using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0006's note of 2026-10-01 (ticket 94), as the grid shows it: without a <c>Format</c>, a date
/// or a time shows in one ISO form by its type, whatever the culture the code runs under. It is the
/// one text the grid shows, so the cell, copy's text/plain, the value list, the editor's opening
/// text and the Auto width all read it; copy's text/html stays the raw value (ADR-0005).
/// </summary>
public class DateWithoutFormatTests : GridTestContext
{
    // Wide enough for the offset's 26 characters at the core's widths; narrower, the rule paints ####.
    private static readonly ColumnWidthSpec Wide = new(ColumnWidth.Fixed(280));

    // TestRows.Window()'s first row is as of 2026-01-05, midnight.
    private static DateTime When(TestRow r) => r.AsOf.AddHours(9).AddMinutes(5).AddSeconds(7);

    private static GridColumn<TestRow>[] Columns(FilterUiMode filterUi = FilterUiMode.Condition) =>
    [
        new("When", ColumnType.Date, r => When(r), width: Wide, editable: true, filterUi: filterUi),
        new("Day", ColumnType.Date, r => DateOnly.FromDateTime(r.AsOf), width: Wide),
        new("At", ColumnType.Date, r => new DateTimeOffset(When(r), TimeSpan.FromHours(9)), width: Wide),
        new("Time", ColumnType.Date, r => TimeOnly.FromDateTime(When(r).AddHours(12)), width: Wide),
    ];

    private static readonly string[] FirstRow = ["2026-01-05 09:05:07", "2026-01-05", "2026-01-05 09:05:07 +09:00", "21:05:07"];

    public static TheoryData<string> Cultures() => ["en-US", "en-GB", "ja-JP"];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(TestSource? source = null, GridColumn<TestRow>[]? columns = null, bool bar = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            if (bar)
                ps.Add(g => g.ShowFormulaBar, true);
            if (source is null)
                ps.Add(g => g.Window, TestRows.Window());
            else
                ps.Add(g => g.Source, source);
            ps.Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 1200);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 (2026-10-01): each type's cell shows its ISO form, the same under every culture
    public void The_cells_show_each_types_iso_form(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid();

        var cells = cut.FindAll(".ex-row").First().QuerySelectorAll(".ex-cell").Select(c => c.TextContent).ToArray();

        Assert.Equal(FirstRow, cells);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 / ADR-0005 (CP-4): copy's text/plain is the ISO form; its text/html stays raw
    public async Task Copy_carries_the_iso_form_in_text_and_the_raw_value_in_html(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        for (var i = 0; i < 3; i++)
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal(string.Join('\t', FirstRow) + "\r\n", payload.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>2026-01-05T09:05:07</td><td>2026-01-05</td>"
            + "<td>2026-01-05T09:05:07+09:00</td><td>21:05:07</td></tr></table>", payload.Html);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 / ADR-0009: the value list offers the values in the ISO form
    public async Task The_value_list_offers_the_iso_form(string culture)
    {
        using var _ = new CultureScope(culture);
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        source.DistinctAnswer = DistinctValues.Of([new DateTime(2026, 1, 5, 9, 5, 7), new DateTime(2026, 2, 1), null]);
        var cut = RenderGrid(source, Columns(FilterUiMode.Both));

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        var labels = cut.FindAll(".ex-popover-list label").Select(l => l.TextContent.Trim()).ToArray();
        Assert.Equal(["2026-01-05 09:05:07", "2026-02-01 00:00:00", "(Blanks)"], labels);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 / ADR-0010: the editor opens on the ISO text the reader was looking at
    public async Task The_editor_opens_on_the_iso_form(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        Assert.Equal("2026-01-05 09:05:07", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 (ticket 95) / ADR-0051: the Formula Bar's full value of an unformatted date or time reads in its cell's ISO form
    public async Task The_formula_bar_shows_an_unformatted_date_in_its_iso_form(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid(bar: true);
        await ClickCellAsync(cut, 50, 10);

        var shown = new List<string?>();
        for (var column = 0; column < FirstRow.Length; column++)
        {
            if (column > 0)
                await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));
            shown.Add(cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        }

        Assert.Equal(FirstRow, shown);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0051 / ADR-0006: with a Format, and for a number, the bar's full value is the current culture's, as before
    public async Task The_formula_bar_keeps_a_formatted_date_and_a_number_in_the_cultures_spelling(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid(bar: true, columns:
        [
            new("When", ColumnType.Date, r => When(r), width: Wide, format: v => ((DateTime)v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new("Amount", ColumnType.Number, r => r.Amount, width: Wide),
        ]);
        await ClickCellAsync(cut, 50, 10);
        var formatted = cut.Find(".ex-formula-bar-text").GetAttribute("value");
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));
        var number = cut.Find(".ex-formula-bar-text").GetAttribute("value");

        Assert.Equal(new DateTime(2026, 1, 5, 9, 5, 7).ToString(null, CultureInfo.CurrentCulture), formatted);
        Assert.Equal(100.5m.ToString(null, CultureInfo.CurrentCulture), number);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 / ADR-0016: an Auto column is sized to the ISO text, the same width under every culture
    public void An_auto_column_is_sized_to_the_iso_text(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderGrid(columns: [new GridColumn<TestRow>("W", ColumnType.Date, r => When(r))]);

        var style = cut.Find(".ex-cell").GetAttribute("style")!;
        var width = double.Parse(Regex.Match(style, @"width: ([0-9.]+)px").Groups[1].Value, CultureInfo.InvariantCulture);

        // The window's widest: every row's text is 19 characters of the same classes.
        Assert.Equal(ExGrid<TestRow>.DefaultCellMetrics.EstimatePx("2026-01-05 09:05:07"), width, 9);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 / ADR-0009: a date operand reopens as its ISO text, and OK keeps the same date
    public async Task A_date_operand_reopens_in_iso_form_and_reads_back_the_same(string culture)
    {
        using var _ = new CultureScope(culture);
        var operand = new DateTime(2026, 1, 5, 9, 5, 7);
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["When"] = new([new FilterClause(FilterOperator.Equals, operand)]),
        }));
        var cut = RenderGrid(source);

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Equal("2026-01-05 09:05:07", cut.Find(".ex-popover input:not([type])").GetAttribute("value"));
        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

        // A culture's own text, 05/01/2026 09:05:07 under en-GB, read back invariant was 1 May.
        var clause = Assert.Single(source.FilterChanges[^1]!.Columns["When"].Clauses);
        Assert.Equal(FilterOperator.Equals, clause.Operator);
        Assert.Equal(operand, clause.Value);
    }
}
