namespace ExChart.Engine;

/// <summary>
/// A function exported from a JavaScript module the Consumer ships (ADR-0420 section 3): what a format code cannot
/// say, written by the Consumer's own script and called by a chart drawn in the browser as it draws, at once. It
/// stands where a format code stands, on an axis, a series, a surface axis and a surface's values, and where a
/// sampler stands on a series. Nothing is evaluated from a string: the module is a file the Consumer serves as any
/// other static asset, imported by its address. Only a chart drawn in the browser
/// (<c>ExChart.Script.ExChart</c>, <c>ExChart.Script.ExSurface</c>) can call it; a chart drawn in .NET refuses a
/// definition that names one.
/// </summary>
public sealed record ScriptFunction
{
    private ScriptFunction(string module, string export) => (Module, Export) = (module, export);

    /// <summary>
    /// The module's address: a path under the application's base, such as <c>./js/chart-formats.js</c>, or an
    /// <c>http</c> or <c>https</c> address. It is read against the page's base address, as a stylesheet's is.
    /// </summary>
    public string Module { get; }

    /// <summary>The name the function is exported by.</summary>
    public string Export { get; }

    /// <summary>The function <paramref name="export"/> of the module at <paramref name="module"/>.</summary>
    /// <exception cref="ArgumentException">The module or the export is empty, or the module's address names a scheme
    /// other than <c>http</c> or <c>https</c>, such as <c>data:</c>, which would carry the script in the string.</exception>
    public static ScriptFunction Of(string module, string export)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(export);
        if (Uri.TryCreate(module, UriKind.Absolute, out var address) && address.Scheme is not ("http" or "https") && !module.StartsWith('/'))
        {
            throw new ArgumentException(
                $"The module \"{module}\" is addressed by the scheme \"{address.Scheme}\"; a script function's module is a file the application serves, "
                + "at a path or an http or https address (ADR-0420).", nameof(module));
        }
        return new ScriptFunction(module, export);
    }

    /// <summary>How the function is named in a refusal: its export and its module.</summary>
    public override string ToString() => $"\"{Export}\" of \"{Module}\"";
}
