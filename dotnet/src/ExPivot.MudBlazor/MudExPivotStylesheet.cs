using ExGrid.Components;

namespace ExPivot.MudBlazor;

/// <summary>
/// ExPivot.MudBlazor's stylesheet, linked at the address of the file this build of the Wrapper ships
/// (ADR-0123): the fingerprinted address where the host knows it, else the plain one with the file's
/// content hash as <c>?v=</c>, so a normal reload after an upgrade brings the new stylesheet. Place it
/// with ExGrid's, ExPivot's and ExGrid.MudBlazor's: in <c>App.razor</c>'s <c>&lt;head&gt;</c> in a
/// Blazor Web App, or as a root component at <c>head::after</c> in a standalone application:
/// <code>builder.RootComponents.Add&lt;MudExPivotStylesheet&gt;("head::after");</code>
/// </summary>
public sealed class MudExPivotStylesheet : PackageStylesheet
{
    /// <inheritdoc />
    protected override string Path => ShippedAssets.StylesheetPath;

    /// <inheritdoc />
    protected override string Version => ShippedAssets.StylesheetVersion;
}
