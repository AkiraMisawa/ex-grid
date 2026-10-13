using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// UX-5's layer-2 half (ADR-0027 P3): a Visual Token set on an ancestor recolours the grid with
/// no .NET render of the grid. The Consumer's wrapping element changes its <c>style</c> — a
/// render of the Consumer, which hands the grid the very same parameter instances — and the
/// grid's root and its rows render nothing.
/// </summary>
public class VisualTokenAncestorTests : GridTestContext
{
    /// <summary>A Consumer that wraps the grid in an element whose style it owns. Every
    /// parameter the grid is handed is held in a field, so a render of the wrapper hands
    /// the grid nothing new.</summary>
    private sealed class TokenWrapper : ComponentBase
    {
        private readonly TestRow[] _window = TestRows.Many(200);
        private readonly IReadOnlyList<GridColumn<TestRow>> _columns = TestRows.Columns();

        [Parameter] public string Style { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "consumer-theme");
            builder.AddAttribute(2, "style", Style);
            builder.OpenComponent<ExGrid<TestRow>>(3);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.Window), _window);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.TotalCount), (int?)_window.Length);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.Columns), _columns);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.CloseComponent();
            builder.CloseElement();
        }
    }

    [Fact] // UX-5, ADR-0027 P3: recolouring through the wrapping element's style renders nothing in the grid
    public void UX5_a_visual_token_set_on_the_wrapping_element_renders_nothing_in_dotnet()
    {
        var host = Render<TokenWrapper>(ps => ps.Add(w => w.Style, ""));
        var grid = host.FindComponent<ExGrid<TestRow>>();
        var rows = host.FindComponents<ExGridRow<TestRow>>();
        Assert.NotEmpty(rows);
        var gridRenders = grid.RenderCount;
        var rowRenders = rows.Select(r => r.RenderCount).ToArray();

        host.Render(ps => ps.Add(w => w.Style, "--ex-header-background: rgb(200, 12, 12); --ex-cell-color: navy"));

        // The token reached the ancestor of the grid's root ...
        Assert.Contains("--ex-header-background: rgb(200, 12, 12)", host.Find(".consumer-theme").GetAttribute("style"));
        Assert.Equal("consumer-theme", host.Find(".ex-grid").ParentElement!.ClassName);
        // ... and the grid, root and rows alike, rendered nothing for it.
        Assert.Equal(gridRenders, grid.RenderCount);
        Assert.Equal(rowRenders, rows.Select(r => r.RenderCount).ToArray());
    }

    /// <summary>A Consumer that changes what it handed the grid in place — a row replaced in the
    /// same array — and then renders, restyling its element.</summary>
    private sealed class InPlaceWrapper : ComponentBase
    {
        public TestRow[] Window { get; } = TestRows.Many(50);

        public List<GridColumn<TestRow>> Columns { get; } = [.. TestRows.Columns()];

        [Parameter] public string Style { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "style", Style);
            builder.OpenComponent<ExGrid<TestRow>>(2);
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.Window), Window);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.TotalCount), (int?)Window.Length);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)Columns);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)800);
            builder.CloseComponent();
            builder.CloseElement();
        }
    }

    [Fact] // UX-5, ADR-0027 P3 / ADR-0003: a row replaced in the same Window array repaints, as a new instance does
    public void UX5_a_row_replaced_in_the_window_in_place_still_repaints()
    {
        var host = Render<InPlaceWrapper>(ps => ps.Add(w => w.Style, ""));
        Assert.Contains("Row 000000", host.Markup);

        host.Instance.Window[0] = new TestRow { Book = "Replaced row", Amount = 1 };
        host.Render(ps => ps.Add(w => w.Style, "--ex-cell-color: navy"));

        Assert.Contains("Replaced row", host.Markup);
        Assert.DoesNotContain("Row 000000", host.Markup);
    }
}
