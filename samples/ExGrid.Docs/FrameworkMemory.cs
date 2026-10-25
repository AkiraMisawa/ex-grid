using Microsoft.JSInterop;

namespace ExGrid.Docs;

/// <summary>
/// Where the framework the reader chose is kept between visits (ADR-0350 §12): the browser's
/// <c>localStorage</c>, the Docs Site's second use of JavaScript (ADR-0110). It is read before the
/// site first renders, so the first page drawn is already the framework chosen last time.
/// </summary>
public sealed class FrameworkMemory(IJSRuntime js)
{
    // GitHub Pages serves every project site of an account from one origin, so the key names the site.
    private const string Key = "ex-grid-docs.framework";

    private IJSInProcessObjectReference? _module;

    /// <summary>
    /// Whether the browser keeps what the site writes. A browser that refuses a site storage refuses
    /// every read and write of it, and the switch then says that the choice lasts only for this visit.
    /// </summary>
    public bool Kept { get; private set; }

    /// <summary>
    /// The framework chosen on an earlier visit; <see langword="null"/> when none was, when the browser
    /// keeps nothing for the site, or when what is kept is not a choice this site writes.
    /// </summary>
    public DocsFramework? Recalled { get; private set; }

    /// <summary>
    /// Loads the site's script and reads the choice kept, once, before the site renders. The
    /// WebAssembly runtime's in-process interop then lets a choice be kept as it is made.
    /// </summary>
    public async Task LoadAsync()
    {
        _module = await js.InvokeAsync<IJSInProcessObjectReference>("import", "./js/docs.js");
        var read = _module.Invoke<Stored>("readFramework", Key);
        Kept = read.Kept;
        Recalled = read.Value switch
        {
            "blazor" => DocsFramework.Blazor,
            "react" => DocsFramework.React,
            _ => null,
        };
    }

    /// <summary>Keeps the choice for the next visit, where the browser allows it.</summary>
    public void Remember(DocsFramework framework)
    {
        var module = _module ?? throw new InvalidOperationException("The framework memory is used before Program.cs loaded it.");
        Kept = module.Invoke<bool>("writeFramework", Key, framework == DocsFramework.React ? "react" : "blazor");
    }

    private sealed record Stored(bool Kept, string? Value);
}
