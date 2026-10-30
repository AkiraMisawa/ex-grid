using Bunit;
using ExGrid.Columns;
using Microsoft.AspNetCore.Components;
using Xunit;
using Grid = global::ExGrid.Script.ExGrid<ExGrid.MudBlazor.Tests.MudTestContext.Trade>;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The Wrapper's outer element (ADR-0030): its own classes, and the presentation it cascades to the grid inside it,
/// which the grid hands the Script Core as <c>presentationDefaults</c> where the Consumer wrote nothing of its own.
/// What the core paints from them is its own, tested there.
/// </summary>
public class MudExGridPaperTests : MudTestContext
{
    private IRenderedComponent<MudExGridPaper> RenderPaper(
        Action<Bunit.ComponentParameterCollectionBuilder<MudExGridPaper>>? extra = null,
        Action<Bunit.ComponentParameterCollectionBuilder<Grid>>? grid = null)
        => Render<MudExGridPaper>(ps =>
        {
            ps.AddChildContent<Grid>(g =>
            {
                g.Add(x => x.Rows, Rows(5))
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
        // The grid's markup is read from its own fragment, so its root is found among the paper's children by what it is.
        var root = cut.FindComponent<Grid>().Find("div");
        Assert.Contains(paper.Children, child => child.OuterHtml == root.OuterHtml);
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
        Assert.Null(cut.FindComponent<Grid>().Find("div").GetAttribute("class"));
    }

    [Fact] // ADR-0027/0030: Roboto's widths reach the grid
    public void Roboto_widths_reach_the_grid()
    {
        RenderPaper();

        var sent = Attached().GetProperty("presentationDefaults");
        Assert.Equal(MudExGridPresentation.Roboto.DigitWidthPx, sent.GetProperty("digitWidthPx").GetDouble());
        Assert.Equal(MudExGridPresentation.Roboto.OtherWidthPx, sent.GetProperty("otherWidthPx").GetDouble());
    }

    [Fact] // ADR-0028/0030: Dense is Compact, not dense is Standard; a Density on the grid wins
    public void Dense_maps_onto_the_presets_and_the_grid_wins()
    {
        RenderPaper();
        Assert.Equal("standard", Attached().GetProperty("presentationDefaults").GetProperty("density").GetString());
        RenderPaper(ps => ps.Add(p => p.Dense, true));
        Assert.Equal("compact", Attached().GetProperty("presentationDefaults").GetProperty("density").GetString());
        RenderPaper(ps => ps.Add(p => p.Dense, true), g => g.Add(x => x.Density, GridDensity.Excel));
        Assert.Equal("excel", Attached().GetProperty("density").GetString());
    }

    [Fact] // ADR-0029/0030: Hover turns the grid's band on through the cascade, and without it there is none
    public void Hover_turns_the_band_on()
    {
        RenderPaper(ps => ps.Add(p => p.Hover, true));
        Assert.True(Attached().GetProperty("presentationDefaults").GetProperty("highlightHoverRow").GetBoolean());

        RenderPaper();
        Assert.False(Attached().GetProperty("presentationDefaults").GetProperty("highlightHoverRow").GetBoolean());
    }

    [Fact] // WR-6 / ADR-0038/0030: Striped turns the grid's Row Stripes on through the cascade
    public void Striped_turns_row_stripes_on()
    {
        RenderPaper();
        Assert.False(Attached().GetProperty("presentationDefaults").GetProperty("stripeRows").GetBoolean());

        RenderPaper(ps => ps.Add(p => p.Striped, true));
        Assert.True(Attached().GetProperty("presentationDefaults").GetProperty("stripeRows").GetBoolean());
        Assert.False(Attached().TryGetProperty("stripeRows", out _));
    }

    [Fact] // WR-6 / ADR-0030: the grid's own StripeRows beats the paper's Striped, either way
    public void The_grids_own_stripe_rows_wins()
    {
        RenderPaper(ps => ps.Add(p => p.Striped, true), g => g.Add(x => x.StripeRows, false));
        Assert.False(Attached().GetProperty("stripeRows").GetBoolean());
        RenderPaper(ps => ps.Add(p => p.Striped, false), g => g.Add(x => x.StripeRows, true));
        Assert.True(Attached().GetProperty("stripeRows").GetBoolean());
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

    [Fact] // RR-1 / ADR-0030: a change on the paper hands the grid the same defaults, and the grid's markup is not drawn again
    public void A_paper_change_hands_the_grid_the_same_defaults()
    {
        var cut = RenderPaper(ps => ps.Add(p => p.Elevation, 1));
        var grid = cut.FindComponent<Grid>();
        var root = grid.RenderCount;
        var cascaded = cut.FindComponent<CascadingValue<GridPresentationDefaults>>().Instance.Value;

        cut.Render(ps => ps.Add(p => p.Elevation, 4).Add(p => p.Outlined, true).Add(p => p.Style, "margin: 8px"));

        Assert.Contains("mud-ex-grid-outlined", cut.Find(".mud-ex-grid").ClassName);
        // A parent's render reaches the child's SetParametersAsync — Blazor's rule, not the Wrapper's — and the grid,
        // handed nothing new, draws nothing. A THEME change is CSS and reaches no render at all.
        Assert.Equal(root, grid.RenderCount);
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
        // The grid's markup is read from its own fragment, so its root is found in the paper's by what it is.
        var drawn = cut.FindComponent<Grid>().Find("div").OuterHtml;
        var root = Assert.Single(paper.Children, child => child.OuterHtml == drawn);
        Assert.Null(root.QuerySelector("#title"));
        Assert.True(toolbar.CompareDocumentPosition(root).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
    }

    [Fact] // ADR-0027/0030: another font brings its widths in the same value, and the paper writes both
    public void Another_font_brings_its_widths_in_one_value()
    {
        // A font with a 10px digit.
        var inter = new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14);
        var cut = RenderPaper(ps => ps.Add(p => p.Font, inter).Add(p => p.Style, "margin: 4px"));

        Assert.Contains("--ex-font-family: Inter, sans-serif;", cut.Find(".mud-ex-grid").GetAttribute("style"));
        Assert.Contains("margin: 4px", cut.Find(".mud-ex-grid").GetAttribute("style"));
        Assert.Equal(10, Attached().GetProperty("presentationDefaults").GetProperty("digitWidthPx").GetDouble());
        // Roboto is the stylesheet's, so nothing is written inline for it.
        Assert.DoesNotContain("--ex-font-family", RenderPaper().Find(".mud-ex-grid").GetAttribute("style") ?? "");
    }

    [Fact] // ADR-0016 / ADR-0030 / ticket 83: a font's other class is cascaded with its widths, or derived by the allowance
    public void Another_fonts_other_class_is_cascaded_or_derived()
    {
        var measured = new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14, 12.5, 10.5, 5.5, 15, 16);
        var unmeasured = new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14);

        var given = MudExGridPresentation.For(measured, dense: false, hover: false);
        var derived = MudExGridPresentation.For(unmeasured, dense: false, hover: false);

        Assert.Equal(15, given.OtherWidthPx);
        Assert.Equal(16, given.BoldOtherWidthPx);
        Assert.Equal(10 * CellTextMetrics.OtherWidthAllowance, derived.OtherWidthPx);
        Assert.Equal(10 * CellTextMetrics.BoldWidthAllowance * CellTextMetrics.OtherWidthAllowance, derived.BoldOtherWidthPx, precision: 9);
        Assert.Equal(MudExGridPresentation.RobotoOtherPx, MudExGridPresentation.For(MudExGridFont.Roboto, false, false).OtherWidthPx);
        Assert.Equal(MudExGridPresentation.Roboto.OtherWidthPx, MudExGridPresentation.For(dense: true, hover: true).OtherWidthPx);
    }

    [Fact] // ADR-0016 / ADR-0030 / ticket 83: Roboto's glyph table is cascaded with its widths, and a font's own with it
    public void The_glyph_table_is_cascaded_with_the_widths()
    {
        var table = new GlyphWidthTable(14, [("M", 13, 14)]);
        var withTable = new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14, GlyphWidths: table);

        Assert.Same(MudExGridPresentation.RobotoGlyphWidths, MudExGridPresentation.Roboto.GlyphWidths);
        Assert.Same(MudExGridPresentation.RobotoGlyphWidths, MudExGridPresentation.For(dense: true, hover: false).GlyphWidths);
        Assert.Same(MudExGridPresentation.RobotoGlyphWidths, MudExGridPresentation.For(MudExGridFont.Roboto, false, false).GlyphWidths);
        Assert.Same(table, MudExGridPresentation.For(withTable, false, false).GlyphWidths);
        Assert.Null(MudExGridPresentation.For(new MudExGridFont("Inter, sans-serif", 12, 10, 5, 14), false, false).GlyphWidths);
        var metrics = GridMetrics.Resolve(GridDensity.Standard, defaults: MudExGridPresentation.Roboto).CellMetrics;
        Assert.True(metrics.WidthOf('i') < metrics.DigitWidthPx);
        Assert.True(metrics.WidthOf('M') < MudExGridPresentation.RobotoOtherPx);
    }

    [Fact] // ADR-0030: an elevation MudBlazor does not have is refused, not silently clamped
    public void An_impossible_elevation_is_refused()
    {
        Assert.ThrowsAny<Exception>(() => RenderPaper(ps => ps.Add(p => p.Elevation, 26)));
    }
}
