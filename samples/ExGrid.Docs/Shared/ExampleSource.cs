using System.Text.RegularExpressions;

namespace ExGrid.Docs;

/// <summary>One source file of an Example, as the build read it (ADR-0110).</summary>
/// <param name="Path">Its path in the site's project: <c>Examples/Grid/FirstGrid.razor</c>.</param>
/// <param name="Language"><c>razor</c>, <c>cs</c>, <c>tsx</c>, <c>ts</c>, or a snippet's <c>html</c>, <c>sh</c> or <c>css</c>.</param>
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
        : throw new InvalidOperationException($"{path} is not an Example source. Examples live under Examples/, as .razor, .cs, .tsx or .ts, with the snippets a page quotes as .html, .sh or .css.");

    /// <summary>The source of the Example component <paramref name="component"/>: the .razor
    /// file its namespace and name say it was compiled from.</summary>
    public static ExampleSource For(Type component) => Get(PathOf(component) + ".razor");

    /// <summary>
    /// The React counterpart of the Example component <paramref name="component"/> (ADR-0350 section 12): the .tsx
    /// file beside its .razor, and the files of the Example's own folder it imports, in the order it imports them;
    /// <see langword="null"/> when it has none.
    /// </summary>
    public static ReactExample? ReactFor(Type component) => ReactExamples.GetOrAdd(component, ReadReact);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ReactExample?> ReactExamples = new();

    private static ReactExample? ReadReact(Type component)
    {
        var path = PathOf(component);
        if (!All.TryGetValue(path + ".tsx", out var example))
            return null;
        var folder = path[..(path.LastIndexOf('/') + 1)];
        List<ExampleSource> files = [example];
        for (var i = 0; i < files.Count; i++)
        {
            foreach (Match import in LocalImport().Matches(files[i].Text))
            {
                // Only a file beside it, named without its extension: anything else would be code the page could
                // not show, so it is refused rather than left out.
                var imported = import.Groups["path"].Value;
                if (!SiblingImport().IsMatch(imported))
                    throw new InvalidOperationException($"{files[i].Path} imports {imported}: a React Example imports only files of its own folder, as './Expenses', so that the page shows every file it runs.");
                var name = folder + imported[2..];
                var file = All.GetValueOrDefault(name + ".tsx") ?? All.GetValueOrDefault(name + ".ts")
                    ?? throw new InvalidOperationException($"{files[i].Path} imports {imported}, which is not an Example source beside it.");
                if (!files.Contains(file))
                    files.Add(file);
            }
        }
        return new ReactExample(path["Examples/".Length..], files);
    }

    // The Example's path without its extension: Examples/Sheet/FirstSheet.
    private static string PathOf(Type component)
    {
        const string root = "ExGrid.Docs.";
        var ns = component.Namespace ?? "";
        if (!ns.StartsWith(root + "Examples", StringComparison.Ordinal))
            throw new InvalidOperationException($"{component.FullName} is not an Example: Examples live in {root}Examples.");
        return ns[root.Length..].Replace('.', '/') + "/" + component.Name;
    }

    // An import by a relative path, with either quote: from './Expenses'.
    [GeneratedRegex(@"from\s+(['""])(?<path>\.[^'""]*)\1")]
    private static partial Regex LocalImport();

    // A file of the same folder, named without its extension.
    [GeneratedRegex(@"^\./[A-Za-z0-9_-]+$")]
    private static partial Regex SiblingImport();
}

/// <summary>A React Example (ADR-0350 section 12): what its frame is asked to run, and the code it runs.</summary>
/// <param name="Name">Its path under <c>Examples/</c>, without the extension: <c>Sheet/FirstSheet</c>.</param>
/// <param name="Sources">Its .tsx file first, then the files it imports.</param>
public sealed record ReactExample(string Name, IReadOnlyList<ExampleSource> Sources);
