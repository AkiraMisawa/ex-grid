using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

/// <summary>
/// A package's stylesheet, linked at the address of the file the package was built with
/// (ADR-0123): the base of <see cref="ExGridStylesheet"/> and of the stylesheet each sibling
/// product and Wrapper ships. A Consumer places those, never this.
/// </summary>
/// <remarks>
/// <para>A browser that keeps an old stylesheet beside newer assemblies paints with rules that do not
/// match the component, and no user should have to know that a hard refresh exists. So the address
/// changes with the file's content in every hosting model, and the Consumer writes no
/// <c>_content/</c> path:</para>
/// <list type="bullet">
/// <item>Where the host knows the file's fingerprinted address (<see cref="ComponentBase.Assets"/>, a
/// Blazor Web App on <c>MapStaticAssets</c>), the link is that address, which the host serves
/// <c>immutable</c>.</item>
/// <item>Otherwise it is the plain address with the first twelve hex digits of the shipped file's
/// SHA-256 as <c>?v=</c>: a standalone WebAssembly application, on GitHub Pages or any static
/// host.</item>
/// </list>
/// <para>In a Blazor Web App, place the components in <c>App.razor</c>'s <c>&lt;head&gt;</c>. In a
/// standalone application, add each as a root component at <c>head::after</c>, beside
/// <c>HeadOutlet</c>, so that no page's <c>&lt;HeadContent&gt;</c> displaces it.</para>
/// </remarks>
public abstract class PackageStylesheet : ComponentBase
{
    /// <summary>Where the package serves its minified stylesheet, under the application's base:
    /// <c>_content/&lt;Package&gt;/&lt;name&gt;.min.css</c>.</summary>
    protected abstract string Path { get; }

    /// <summary>The first twelve hex digits of the SHA-256 of that file as the package ships it,
    /// which the plain address carries as <c>?v=</c> (ADR-0123).</summary>
    protected abstract string Version { get; }

    /// <summary>The address a package's file is asked for at (ADR-0123): the host's fingerprinted
    /// address for <paramref name="path"/> where <paramref name="assets"/> knows one, else
    /// <paramref name="path"/> with <paramref name="version"/> as <c>?v=</c>. Either way a new
    /// version of the file is a new address, so a normal reload brings it. Relative, like the
    /// path: it resolves against the document's base.</summary>
    /// <param name="assets">The host's static assets: <see cref="ComponentBase.Assets"/>.</param>
    /// <param name="path">The file's path under the application's base, as the package serves it.</param>
    /// <param name="version">The first twelve hex digits of the SHA-256 of the file as shipped.</param>
    internal static string AddressOf(ResourceAssetCollection assets, string path, string version)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(version);
        // The collection answers the key itself for a file it does not know: no MapStaticAssets,
        // or a standalone application, whose host page is not rendered by .NET at all.
        var fingerprinted = assets[path];
        return fingerprinted != path ? fingerprinted : $"{path}?v={version}";
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "link");
        builder.AddAttribute(1, "rel", "stylesheet");
        builder.AddAttribute(2, "href", AddressOf(Assets, Path, Version));
        builder.CloseElement();
    }
}
