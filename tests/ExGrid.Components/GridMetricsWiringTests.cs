using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The resolved geometry reaching the component (ADR-0028): the inline Geometry
/// Tokens, the density presets, the re-anchoring on a geometry change, and what a
/// Fill axis does with the browser's report.
/// </summary>
public class GridMetricsWiringTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(3))
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            extra?.Invoke(ps);
        });

    [Fact] // ADR-0027/0028 / UX-2: the Geometry Tokens are emitted inline on the instance root
    public void The_geometry_tokens_are_inline_on_the_root()
    {
        var cut = RenderGrid();

        var style = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("--ex-row-height: 28px", style);
        Assert.Contains("--ex-header-height: 28px", style);
        Assert.Contains("--ex-font-size: 14px", style);
        Assert.Contains("--ex-cell-padding-x: 8px", style);
        Assert.Contains("--ex-action-padding-x: 6px", style);
        Assert.Contains("--ex-action-border-width: 1px", style);
        Assert.Contains("--ex-action-gap: 4px", style);
    }

    [Fact] // ADR-0028: a preset resolves whole, and an explicit value beats it per value
    public void Density_resolves_and_an_explicit_value_wins()
    {
        var cut = RenderGrid(ps => ps
            .Add(g => g.Density, GridDensity.Excel)
            .Add(g => g.RowHeight, 22d));

        var style = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("--ex-row-height: 22px", style);
        Assert.Contains("--ex-font-size: 12px", style);
        Assert.Contains("--ex-cell-padding-x: 4px", style);
    }

    [Fact] // ADR-0028: the header's height is its own number; the spacer and tokens carry it
    public void Header_height_is_separate_from_row_height()
    {
        var cut = RenderGrid(ps => ps
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, 36d));

        var style = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("--ex-row-height: 20px", style);
        Assert.Contains("--ex-header-height: 36px", style);
        // The spacer is header + rows: 36 + 200×20.
        Assert.Contains("height: 4036px", cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0028 / VZ-11: a geometry change re-anchors on the first visible row, not the pixel
    public async Task A_density_change_re_anchors_on_the_first_visible_row()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.RowHeight, 20d));
        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * 20, 0); // first visible row 100
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Equal("101", cut.FindAll(".ex-row")[0].GetAttribute("aria-rowindex"));

        cut.Render(ps => ps.Add(g => g.RowHeight, 30d));

        // The browser was told the anchored offset through the allowlisted write…
        Assert.Equal((100 * 30d, 0d), Js.ScrolledTo[^1]);
        // …and the model already paints from it: the first visible row is unchanged.
        Assert.Equal("101", cut.FindAll(".ex-row")[0].GetAttribute("aria-rowindex"));
    }

    [Fact] // ADR-0028 / RR-2: a row-height-only change re-renders no surviving row
    public async Task A_row_height_change_skips_surviving_rows()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.RowHeight, 20d));
        var before = cut.FindComponents<ExGridRow<TestRow>>()
            .ToDictionary(r => r.Instance.RowIndex, r => r.RenderCount);

        cut.Render(ps => ps.Add(g => g.RowHeight, 18d));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            if (before.TryGetValue(row.Instance.RowIndex, out var count))
                Assert.Equal(count, row.RenderCount);
        }
    }

    [Fact] // ADR-0028 / RR-3: a change that moves digit width re-renders every visible row once
    public void A_cell_metrics_change_re_renders_every_visible_row_once()
    {
        var cut = RenderGrid();
        var before = cut.FindComponents<ExGridRow<TestRow>>()
            .ToDictionary(r => r.Instance.RowIndex, r => r.RenderCount);

        cut.Render(ps => ps.Add(g => g.CellMetrics, new CellTextMetrics(11, 8)));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            if (before.TryGetValue(row.Instance.RowIndex, out var count))
                Assert.Equal(count + 1, row.RenderCount);
        }
    }

    [Fact] // ADR-0028 / VZ-12: a reported size of 0 paints nothing and throws nothing
    public async Task A_fill_viewport_reported_zero_paints_nothing()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350));

        Assert.Empty(cut.FindAll(".ex-row"));

        // The report arrives: the size is the browser's, and the rows appear.
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, 128));
        Assert.NotEmpty(cut.FindAll(".ex-row"));
        // 128px minus the 28px header leaves a 100px band: ⌈100/28⌉ + the straddling
        // row = 5 (ADR-0013).
        Assert.Equal(5, cut.FindAll(".ex-row").Count);

        // Hidden again — a Tuesday, not a bug.
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 0, 0));
        Assert.Empty(cut.FindAll(".ex-row"));
    }

    [Fact] // ADR-0028 / VZ-12: a declared 12px still throws — the refusal keys on declared-vs-reported
    public void A_declared_viewport_shorter_than_the_header_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 12)
            .Add(g => g.ViewportWidth, 350)));
    }

    [Fact] // ADR-0028: a Fill axis writes no inline size — the Consumer's CSS owns it
    public void A_fill_axis_leaves_the_scroller_unsized()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350));

        var style = cut.Find(".ex-scroller").GetAttribute("style") ?? "";
        Assert.DoesNotContain("height", style);
        Assert.Contains("width: 350px", style);
    }


    [Fact] // ADR-0028: the menu button's box is a Geometry Token — the same number the
           // Auto header estimate adds, so the label and the ▾ cannot overlap
    public void The_menu_button_tokens_are_emitted_and_scale_with_the_preset()
    {
        var cut = RenderGrid();
        var style = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("--ex-menu-button-width: 16px", style);
        Assert.Contains("--ex-menu-button-inset: 6px", style);

        var excel = RenderGrid(ps => ps.Add(g => g.Density, GridDensity.Excel));
        Assert.Contains("--ex-menu-button-width: 14px", excel.Find(".ex-grid").GetAttribute("style"));
    }
}
