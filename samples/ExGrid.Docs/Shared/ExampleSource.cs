namespace ExGrid.Docs;

/// <summary>One source file of an Example, as the build read it (ADR-0110).</summary>
/// <param name="Path">Its path in the site's project: <c>Examples/Grid/FirstGrid.razor</c>.</param>
/// <param name="Language"><c>razor</c> or <c>cs</c>.</param>
/// <param name="Text">The file, verbatim: what the copy button copies.</param>
/// <param name="Html">The file, highlighted: a span with a <c>tk-</c> class on every token.</param>
public sealed record ExampleSource(string Path, string Language, string Text, string Html)
{
    /// <summary>The file's name, as its tab shows it.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>The Examples' source files, embedded by the build (ExGrid.Docs.Generator).</summary>
internal static partial class ExampleSources
{
    /// <summary>The file at <paramref name="path"/>.</summary>
    /// <exception cref="InvalidOperationException">No such file was built in: a page whose code
    /// cannot be shown says so rather than show something else (principle 1).</exception>
    public static ExampleSource Get(string path) => All.TryGetValue(path, out var source)
        ? source
        : throw new InvalidOperationException($"{path} is not an Example source. Examples live under Examples/, as .razor or .cs.");

    /// <summary>The source of the Example component <paramref name="component"/>: the .razor
    /// file its namespace and name say it was compiled from.</summary>
    public static ExampleSource For(Type component)
    {
        const string root = "ExGrid.Docs.";
        var ns = component.Namespace ?? "";
        if (!ns.StartsWith(root + "Examples", StringComparison.Ordinal))
            throw new InvalidOperationException($"{component.FullName} is not an Example: Examples live in {root}Examples.");
        return Get(ns[root.Length..].Replace('.', '/') + "/" + component.Name + ".razor");
    }
}
