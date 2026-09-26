using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    [Fact] // ADR-0028 / VZ-12a: a Fill height takes the parent's height: the root fills it as a column, the scroller takes the rest
    public void A_fill_height_takes_the_parents_height()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350));

        var root = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("height: 100%", root);
        Assert.Contains("flex: 1 1 auto", root);
        Assert.Contains("min-height: 0", root);
        Assert.Contains("display: flex; flex-direction: column", root);
        var scroller = cut.Find(".ex-scroller").GetAttribute("style") ?? "";
        Assert.Contains("flex: 1 1 auto; min-height: 0", scroller);
        Assert.DoesNotContain("height:", scroller.Replace("min-height:", ""));
        Assert.Contains("width: 350px", scroller);
    }

    [Fact] // ADR-0028: a Fill width may shrink in a flex row; a declared grid carries none of it
    public void A_fill_width_may_shrink_and_a_declared_grid_carries_no_fill_values()
    {
        var fillWidth = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, ViewportSize.Fill));
        var declared = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350));

        var root = fillWidth.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("min-width: 0", root);
        Assert.DoesNotContain("height: 100%", root);
        var plain = declared.Find(".ex-grid").GetAttribute("style")!;
        Assert.DoesNotContain("min-width", plain);
        Assert.DoesNotContain("100%", plain);
    }

    [Fact] // ADR-0028 / VZ-12b: a parent with no height is named once, and a real height afterwards paints
    public async Task A_parent_with_no_height_is_named_once_until_a_real_height_arrives()
    {
        var log = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(log));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350));

        // The scroller is as tall as its content, which is the header band alone.
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, 20));
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, 20));

        var warning = Assert.Single(log.Warnings);
        Assert.Contains("parent", warning);
        Assert.Contains("definite height", warning);
        Assert.Empty(cut.FindAll(".ex-row"));

        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, 200));
        Assert.NotEmpty(cut.FindAll(".ex-row"));
        Assert.Single(log.Warnings);

        // Lost again after a real height: named again.
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, 20));
        Assert.Equal(2, log.Warnings.Count);
    }

    [Fact] // ADR-0028: a hidden tab reports 0, which is a Tuesday, not a missing height
    public async Task A_reported_zero_writes_no_warning()
    {
        var log = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(log));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350));

        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 0, 0));

        Assert.Empty(log.Warnings);
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
