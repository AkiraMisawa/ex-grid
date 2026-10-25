using Microsoft.JSInterop;

namespace ExGrid.Docs;

/// <summary>
/// Where the framework the reader chose is kept between visits (ADR-0350 §12): the browser's
/// <c>localStorage</c>, the Docs Site's second use of JavaScript (ADR-0110). It is read synchronously
/// as the site's state is made, so the first page drawn is already the framework chosen last time.
/// </summary>
public sealed class FrameworkMemory
{
    // GitHub Pages serves every project site of an account from one origin, so the key names the site.
    private const string Key = "ex-grid-docs.framework";

    private readonly IJSInProcessRuntime _js;

    /// <summary>Takes the WebAssembly runtime's in-process interop; the site runs nowhere else.</summary>
    public FrameworkMemory(IJSRuntime js) => _js = (IJSInProcessRuntime)js;

    /// <summary>
    /// Whether the browser keeps what the site writes. A browser that blocks a site's storage throws on
    /// every read and write of it, and the switch then says that the choice lasts only for this visit.
    /// </summary>
    public bool Kept { get; private set; } = true;

    /// <summary>
    /// The framework chosen on an earlier visit; <see langword="null"/> when none was, when the browser
    /// keeps nothing for the site, or when what is kept is not a choice this site writes.
    /// </summary>
    public DocsFramework? Recall()
    {
        string? kept;
        try
        {
            kept = _js.Invoke<string?>("localStorage.getItem", Key);
        }
        catch (JSException)
        {
            Kept = false;
            return null;
        }
        return kept switch
        {
            "blazor" => DocsFramework.Blazor,
            "react" => DocsFramework.React,
            _ => null,
        };
    }

    /// <summary>Keeps the choice for the next visit, where the browser allows it.</summary>
    public void Remember(DocsFramework framework)
    {
        try
        {
            _js.InvokeVoid("localStorage.setItem", Key, framework == DocsFramework.React ? "react" : "blazor");
            Kept = true;
        }
        catch (JSException)
        {
            Kept = false;
        }
    }
}
