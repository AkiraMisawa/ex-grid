using ExGrid.Components;

namespace ExChart.MudBlazor;

/// <summary>
/// ExChart.MudBlazor's stylesheet, linked at the address of the file this build of the Wrapper ships
/// (ADR-0123): the fingerprinted address where the host knows it, else the plain one with the file's
/// content hash as <c>?v=</c>. Place it with ExChart's and ExGrid.MudBlazor's: in <c>App.razor</c>'s
/// <c>&lt;head&gt;</c> in a Blazor Web App, or as a root component at <c>head::after</c> in a
/// standalone application:
/// <code>builder.RootComponents.Add&lt;MudExChartStylesheet&gt;("head::after");</code>
/// </summary>
public sealed class MudExChartStylesheet : PackageStylesheet
{
    /// <inheritdoc />
    protected override string Path => ShippedAssets.StylesheetPath;

    /// <inheritdoc />
    protected override string Version => ShippedAssets.StylesheetVersion;
}
