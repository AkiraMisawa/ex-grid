using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A focusable descendant — a Consumer's Template Column control, an action button —
/// owns everything but the way out (ADR-0020): the capture listener forwards only its
/// Escape, marked as coming from a descendant, and the core answers by taking the
/// keyboard back rather than by leaving the grid (ADR-0012's layering).
/// </summary>
public class DescendantKeyTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.TotalCount, 3)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));

    [Fact] // ADR-0020: Escape from a descendant is the way out of the cell, not of the grid
    public async Task Escape_from_a_descendant_reclaims_without_blurring()
    {
        var cut = RenderGrid();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "Escape", ctrl: false, shift: false, alt: false, meta: false, metaIsPrimary: false,
            fromDescendant: true));

        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0012 / KB-8: Escape on the root itself still releases the grid's focus
    public async Task Escape_on_the_root_still_leaves()
    {
        var cut = RenderGrid();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "Escape", ctrl: false, shift: false, alt: false, meta: false, metaIsPrimary: false));

        Assert.Equal(1, Js.BlurCount);
    }
}
