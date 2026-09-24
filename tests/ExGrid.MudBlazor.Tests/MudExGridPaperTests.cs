using Bunit;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The Wrapper's outer element (ADR-0030): its own classes, the presentation it
/// cascades, and — the promise that matters — that a change on the paper re-renders
/// nothing inside the grid.
/// </summary>
public class MudExGridPaperTests : MudTestContext
{
    private IRenderedComponent<MudExGridPaper> RenderPaper(
        Action<Bunit.ComponentParameterCollectionBuilder<MudExGridPaper>>? extra = null,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<Trade>>>? grid = null)
        => Render<MudExGridPaper>(ps =>
        {
            ps.AddChildContent<ExGrid<Trade>>(g =>
            {
                g.Add(x => x.Window, Rows(5))
                 .Add(x => x.TotalCount, 5)
                 .Add(x => x.Columns, Columns())
                 .Add(x => x.ViewportHeight, 200)
                 .Add(x => x.ViewportWidth, 400);
                grid?.Invoke(g);
            });
            extra?.Invoke(ps);
        });

    [Fact] // ADR-0030: the paper is its own element with MudBlazor's own classes; the grid is inside it
    public void The_paper_wraps_the_grid_with_its_own_classes()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Elevation, 2).Add(p => p.Bordered, true));

        var paper = cut.Find(".mud-ex-grid");
        Assert.Contains("mud-elevation-2", paper.ClassName);
        Assert.Contains("mud-ex-grid-bordered", paper.ClassName);
        Assert.NotNull(paper.QuerySelector(".ex-grid"));
    }

    [Fact] // ADR-0030: Outlined suppresses the elevation, as on MudBlazor's tables
    public void Outlined_suppresses_elevation()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Elevation, 3).Add(p => p.Outlined, true));

        var paper = cut.Find(".mud-ex-grid");
        Assert.DoesNotContain("mud-elevation", paper.ClassName);
        Assert.Contains("mud-ex-grid-outlined", paper.ClassName);
    }

    [Fact] // ADR-0030: Square and Class reach the paper's element; nothing reaches the grid's root
    public void Square_and_class_are_the_papers_own()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Square, true).Add(p => p.Class, "my-paper"));

        var paper = cut.Find(".mud-ex-grid");
        Assert.Contains("mud-ex-grid-square", paper.ClassName);
        Assert.Contains("my-paper", paper.ClassName);
        Assert.DoesNotContain("my-paper", cut.Find(".ex-grid").ClassName);
    }

    [Fact] // ADR-0027/0030: Roboto's widths reach the grid — the twelve-digit amount fits
    public void Roboto_widths_reach_the_grid()
    {
        var cut = RenderPaper();

        var amount = cut.FindAll(".ex-row .ex-cell")[1].TextContent;
        Assert.Equal("123456789012", amount);
    }

    [Fact] // ADR-0028/0030: Dense is Compact, not dense is Standard; a Density on the grid wins
    public void Dense_maps_onto_the_presets_and_the_grid_wins()
    {
        Assert.Contains("--ex-row-height: 32px", RenderPaper().Find(".ex-grid").GetAttribute("style"));
        Assert.Contains("--ex-row-height: 28px", RenderPaper(ps => ps.Add(p => p.Dense, true)).Find(".ex-grid").GetAttribute("style"));
        Assert.Contains("--ex-row-height: 20px",
            RenderPaper(ps => ps.Add(p => p.Dense, true), g => g.Add(x => x.Density, GridDensity.Excel))
                .Find(".ex-grid").GetAttribute("style"));
    }

    [Fact] // ADR-0029/0030: Hover turns the grid's band on through the cascade
    public async Task Hover_turns_the_band_on()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Hover, true));
        var grid = cut.FindComponent<ExGrid<Trade>>();

        await cut.InvokeAsync(() => grid.Instance.OnPointerRowAsync(50, 40));

        Assert.NotEmpty(cut.FindAll(".ex-hover-row"));
    }

    [Fact] // ADR-0029: without Hover no band, whatever the browser reports
    public async Task Without_hover_there_is_no_band()
    {
        var cut = RenderPaper();
        var grid = cut.FindComponent<ExGrid<Trade>>();

        await cut.InvokeAsync(() => grid.Instance.OnPointerRowAsync(50, 40));

        Assert.Empty(cut.FindAll(".ex-hover-row"));
    }

    [Fact] // WR-6 / ADR-0038/0030: Striped turns the grid's Row Stripes on through the cascade
    public void Striped_turns_row_stripes_on()
    {
        Assert.Empty(RenderPaper().FindAll(".ex-row-stripe"));

        var striped = RenderPaper(ps => ps.Add(p => p.Striped, true));
        var rows = striped.FindAll(".ex-row");
        Assert.All(rows, row => Assert.Equal(
            (int.Parse(row.GetAttribute("aria-rowindex")!) - 1) % 2 == 1, row.ClassList.Contains("ex-row-stripe")));
        Assert.NotEmpty(striped.FindAll(".ex-row-stripe"));
    }

    [Fact] // WR-6 / ADR-0030: the grid's own StripeRows beats the paper's Striped, either way
    public void The_grids_own_stripe_rows_wins()
    {
        Assert.Empty(RenderPaper(ps => ps.Add(p => p.Striped, true), g => g.Add(x => x.StripeRows, false))
            .FindAll(".ex-row-stripe"));
        Assert.NotEmpty(RenderPaper(ps => ps.Add(p => p.Striped, false), g => g.Add(x => x.StripeRows, true))
            .FindAll(".ex-row-stripe"));
    }

    [Fact] // ADR-0030: each combination of the paper's words is one cascaded instance, and Striped is part of it
    public void Striped_is_one_of_the_cascaded_combinations()
    {
        Assert.True(MudExGridPresentation.For(true, true, striped: true).StripeRows);
        Assert.False(MudExGridPresentation.For(true, true).StripeRows);
        Assert.Same(MudExGridPresentation.For(false, true, true), MudExGridPresentation.For(false, true, true));
        Assert.NotSame(MudExGridPresentation.For(false, true, true), MudExGridPresentation.For(false, true, false));
        Assert.Equal(GridDensity.Compact, MudExGridPresentation.For(true, false, true).Density);
        Assert.True(MudExGridPresentation.For(MudExGridFont.Roboto, false, false, striped: true).StripeRows);
    }

    [Fact] // RR-1 / ADR-0030: a change on the paper re-renders no row, and hands the grid the same defaults
    public void A_paper_change_re_renders_no_row()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Elevation, 1));
        var grid = cut.FindComponent<ExGrid<Trade>>();
        var root = grid.RenderCount;
        var rows = cut.FindComponents<ExGridRow<Trade>>().Select(r => r.RenderCount).ToList();
        var cascaded = cut.FindComponent<CascadingValue<GridPresentationDefaults>>().Instance.Value;

        cut.Render(ps => ps.Add(p => p.Elevation, 4).Add(p => p.Outlined, true).Add(p => p.Style, "margin: 8px"));

        Assert.Contains("mud-ex-grid-outlined", cut.Find(".mud-ex-grid").ClassName);
        // A parent's render reaches the child's SetParametersAsync — Blazor's rule, not
        // the Wrapper's — so the root renders once; what it hands the rows is unchanged
        // and every row skips (ADR-0003). A THEME change is CSS and reaches no render
        // at all; the browser suite asserts that one.
        Assert.InRange(grid.RenderCount, root, root + 1);
        Assert.Equal(rows, cut.FindComponents<ExGridRow<Trade>>().Select(r => r.RenderCount).ToList());
        // The same defaults, not merely equal ones: neither Dense nor Hover moved.
        Assert.Same(cascaded, cut.FindComponent<CascadingValue<GridPresentationDefaults>>().Instance.Value);
    }

    [Fact] // ADR-0030: the toolbar slot is above the grid, outside the instance root
    public void The_toolbar_slot_is_outside_the_root()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.ToolBarContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span id='title'>Trades</span>"))));

        var paper = cut.Find(".mud-ex-grid");
        var toolbar = paper.QuerySelector(".mud-ex-grid-toolbar");
        Assert.NotNull(toolbar);
        Assert.NotNull(toolbar!.QuerySelector("#title"));
        Assert.Null(paper.QuerySelector(".ex-grid #title"));
        Assert.True(toolbar.CompareDocumentPosition(paper.QuerySelector(".ex-grid")!).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
    }

    [Fact] // ADR-0027/0030: another font brings its widths in the same value, and the paper writes both
    public void Another_font_brings_its_widths_in_one_value()
    {
        // A font with a 10px digit: twelve digits plus padding is 136px, past the 112px column.
        var inter = new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14);
        var cut = RenderPaper(ps => ps.Add(p => p.Font, inter).Add(p => p.Style, "margin: 4px"));

        Assert.Contains("--ex-font-family: Inter, sans-serif;", cut.Find(".mud-ex-grid").GetAttribute("style"));
        Assert.Contains("margin: 4px", cut.Find(".mud-ex-grid").GetAttribute("style"));
        Assert.StartsWith("#", cut.FindAll(".ex-row .ex-cell")[1].TextContent);
        // Roboto is the stylesheet's, so nothing is written inline for it.
        Assert.DoesNotContain("--ex-font-family", RenderPaper().Find(".mud-ex-grid").GetAttribute("style") ?? "");
    }

    [Fact] // ADR-0030: an elevation MudBlazor does not have is refused, not silently clamped
    public void An_impossible_elevation_is_refused()
    {
        Assert.ThrowsAny<Exception>(() => RenderPaper(ps => ps.Add(p => p.Elevation, 26)));
    }
}
