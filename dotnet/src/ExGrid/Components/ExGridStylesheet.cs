namespace ExGrid.Components;

/// <summary>
/// ExGrid's stylesheet, linked at the address of the file this build of ExGrid ships (ADR-0123):
/// the fingerprinted address where the host knows it, else the plain one with the file's content
/// hash as <c>?v=</c>, so a normal reload after an upgrade brings the new stylesheet. Place it in
/// <c>App.razor</c>'s <c>&lt;head&gt;</c> in a Blazor Web App, or add it as a root component at
/// <c>head::after</c> in a standalone application:
/// <code>builder.RootComponents.Add&lt;ExGridStylesheet&gt;("head::after");</code>
/// Every product's grid is drawn with it, so a page of ExSheet or ExPivot places it too.
/// </summary>
public sealed class ExGridStylesheet : PackageStylesheet
{
    /// <inheritdoc />
    protected override string Path => ShippedAssets.StylesheetPath;

    /// <inheritdoc />
    protected override string Version => ShippedAssets.StylesheetVersion;
}
