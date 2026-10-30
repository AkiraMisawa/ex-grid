using Bunit;
using ExGrid.Chrome;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The seams this package fills that draw from nothing but their view (ADR-0010/0030, ADR-0410): the loading bar, and
/// the density Material's dense maps onto. They render; neither decides anything.
/// </summary>
public class MudGridChromeTests : MudTestContext
{
    [Fact] // ADR-0010/0030: the loading bar is a MudProgressLinear in the Chrome's colour, only while loading
    public void The_loading_bar_shows_only_while_loading()
    {
        IGridChrome chrome = new MudGridChrome { LoadingProgressColor = Color.Secondary };

        Assert.Null(chrome.LoadingIndicator(new GridLoadingView(IsLoading: false, PlaceholderRowCount: 3)));

        var loading = Render(chrome.LoadingIndicator(new GridLoadingView(IsLoading: true, PlaceholderRowCount: 3))!);
        var bar = loading.Find(".mud-ex-grid-loading");
        Assert.Contains("mud-progress-linear", bar.ClassName);
        Assert.Contains("mud-progress-indeterminate", bar.ClassName);
        Assert.Contains("secondary", bar.ClassName);
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
