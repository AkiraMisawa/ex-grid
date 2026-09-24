using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The seams this package fills (ADR-0010/0030): the Cell Editor — a bare input in
/// the core's box, reporting its text — and the loading bar. Both render and call
/// back; neither decides anything.
/// </summary>
public class MudGridChromeTests : MudTestContext
{
    private IRenderedComponent<ExGrid<Trade>> RenderGrid(MudGridChrome chrome, bool loading = false)
        => Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.IsLoading, loading)
            .Add(g => g.Chrome, chrome));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<Trade>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0010/0030: the editor is a bare input inside the core's box, carrying the typed character
    public async Task The_editor_is_a_bare_input_in_the_cores_box()
    {
        var cut = RenderGrid(MudGridChrome.Default);
        await ClickCellAsync(cut, 50, 30);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        var box = cut.Find(".ex-editor");
        var input = box.QuerySelector("input.mud-ex-editor");
        Assert.NotNull(input);
        Assert.Equal("x", input!.GetAttribute("value"));
        Assert.Null(box.QuerySelector(".mud-input"));
        Assert.Null(cut.Find(".ex-grid").QuerySelector(".mud-textfield"));
    }

    [Fact] // ADR-0010: what is typed reaches the core, and the commit leaves as an intent
    public async Task Typing_reaches_the_core_and_commits_as_an_intent()
    {
        GridEditIntent<Trade>? committed = null;
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.OnEdit, intent => committed = intent));
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "xyz" });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false));

        Assert.NotNull(committed);
        Assert.Equal("xyz", committed!.Value);
        Assert.Equal("Book", committed.Column);
    }

    [Fact] // ADR-0010/0030: the loading bar is a MudProgressLinear in the Chrome's colour, only while loading
    public void The_loading_bar_shows_only_while_loading()
    {
        var chrome = new MudGridChrome { LoadingProgressColor = Color.Secondary };

        var idle = RenderGrid(chrome, loading: false);
        Assert.Empty(idle.FindAll(".mud-ex-grid-loading"));

        var loading = RenderGrid(chrome, loading: true);
        var bar = loading.Find(".mud-ex-grid-loading");
        Assert.Contains("mud-progress-linear", bar.ClassName);
        Assert.Contains("mud-progress-indeterminate", bar.ClassName);
        Assert.Contains("secondary", bar.ClassName);
        Assert.Contains("ex-loading", loading.Find(".ex-grid").ClassName);
    }

    [Fact] // ADR-0034/0030: a Reject reaches the control — aria-invalid, described by the message, the error underline
    public void A_reject_reaches_the_control()
    {
        RenderFragment? fragment = MudGridChrome.Default.CellEditor(new CellEditorContext(
            "Book", ColumnType.Text, "x", CellEditMode.Overwrite, "needs approval",
            _ => { }, () => { }, () => { }, MessageId: "ex1-message", FocusRequest: 3));
        var cut = Render(fragment!);

        var input = cut.Find("input.mud-ex-editor");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal("ex1-message", input.GetAttribute("aria-describedby"));
        Assert.Contains("mud-ex-editor-error", input.ClassName);

        var clean = Render(MudGridChrome.Default.CellEditor(new CellEditorContext(
            "Book", ColumnType.Text, "x", CellEditMode.Overwrite, null, _ => { }, () => { }, () => { }))!);
        Assert.Null(clean.Find("input.mud-ex-editor").GetAttribute("aria-invalid"));
        Assert.DoesNotContain("mud-ex-editor-error", clean.Find("input.mud-ex-editor").ClassName);
    }

    [Fact] // ADR-0010: the filter panel stays the core's until the Wrapper fills it
    public void The_filter_panel_stays_the_cores()
    {
        Assert.Null(MudGridChrome.Default.FilterPanel(new FilterPanelContext(
            "Book", ColumnType.Text, null, [], FilterUiMode.Condition,
            () => Task.FromResult(DistinctValues.Of([])), _ => { }, () => { }, () => { })));
    }

    [Fact] // ADR-0028/0030: Material's dense is Compact, never Excel
    public void Dense_is_compact_not_excel()
    {
        Assert.Equal(GridDensity.Compact, MudExGridPresentation.DensityFor(dense: true));
        Assert.Equal(GridDensity.Standard, MudExGridPresentation.DensityFor(dense: false));
        Assert.Same(MudExGridPresentation.For(true, true), MudExGridPresentation.For(true, true));
        Assert.NotSame(MudExGridPresentation.For(true, true), MudExGridPresentation.For(true, false));
    }
}
