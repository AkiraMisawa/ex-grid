namespace ExGrid.Docs;

/// <summary>
/// A key of one of the products, as the Keyboard reference lists it (ADR-0110), written by hand in
/// <see cref="KeyboardByHand"/> under the property named for its product.
/// </summary>
/// <param name="Group">The heading it is listed under within its product: <c>Moving and selecting</c>.</param>
/// <param name="Keys">The keys, each in brackets for the page to draw as a key: <c>[Ctrl] + [Shift] + arrow</c>.</param>
/// <param name="What">What they do, in plain text; a key named in it is in brackets too.</param>
/// <param name="Explained">The section of the page that explains it: <c>exgrid/selection#the-keyboard</c>.</param>
/// <param name="Cites">The decisions behind it: <c>ADR-0012</c>.</param>
public sealed record KeyRow(string Group, string Keys, string What, string Explained, IReadOnlyList<string> Cites);

/// <summary>
/// The Keyboard reference (ADR-0110): every key of ExGrid, ExSheet and ExPivot, each linking the section of
/// the page that explains it and citing the decision behind it, from <see cref="KeyboardByHand"/>. The build
/// checks every link and every citation. A page whose own key table is part of the reference draws it from
/// here, so the two cannot differ.
/// </summary>
internal static partial class Keyboard
{
    /// <summary>The page's route.</summary>
    public const string Route = "reference/keyboard";

    private static readonly Dictionary<string, DocsCite> Cites = ReadCites().ToDictionary(c => c.Name, StringComparer.Ordinal);

    /// <summary>The products whose keys the page lists, each with its rows, in order.</summary>
    public static IReadOnlyList<(string Product, IReadOnlyList<KeyRow> Rows)> Products { get; } =
    [
        ("ExGrid", KeyboardByHand.ExGrid),
        ("ExSheet", KeyboardByHand.ExSheet),
        ("ExPivot", KeyboardByHand.ExPivot),
    ];

    /// <summary>The record <paramref name="cite"/> names. The build refuses a citation that leads nowhere.</summary>
    public static DocsCite Cite(string cite) =>
        Cites.TryGetValue(cite, out var found) ? found : new DocsCite(cite, cite, "docs", null);

    /// <summary>The records a row cites.</summary>
    public static IReadOnlyList<DocsCite> CitesOf(KeyRow row) => row.Cites.Select(Cite).ToArray();

    /// <summary>The rows a section of a page explains, in the reference's order: what that page's own key table draws.</summary>
    /// <param name="explained">The section, as a row links it: <c>exgrid/selection#the-keyboard</c>.</param>
    public static IReadOnlyList<KeyRow> ExplainedAt(string explained) =>
        Products.SelectMany(p => p.Rows).Where(r => string.Equals(r.Explained, explained, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>
    /// What a row's link says: the page and the section that explain it, as the site map names them. The
    /// product is the table's, so it is left out.
    /// </summary>
    public static string ExplainedTitle(KeyRow row)
    {
        var hash = row.Explained.IndexOf('#');
        var route = hash < 0 ? row.Explained : row.Explained[..hash];
        var anchor = hash < 0 ? null : row.Explained[(hash + 1)..];
        if (SiteMap.Find(route) is not { } page)
            return row.Explained;
        var title = page.Title ?? route;
        var section = page.Sections.FirstOrDefault(s => s.Anchor == anchor);
        return section is null ? title : $"{title} › {section.Title}";
    }

    /// <summary>The keys without their brackets, as the search shows them: <c>Ctrl + Shift + arrow</c>.</summary>
    public static string Plain(string text) => text.Replace("[", "").Replace("]", "");

    /// <summary>Each row, as a place the search leads to: its product's section of the Keyboard page.</summary>
    public static IEnumerable<SiteSearchEntry> SearchEntries()
    {
        foreach (var (product, rows) in Products)
        {
            foreach (var row in rows)
            {
                yield return new SiteSearchEntry($"{Plain(row.Keys)} — {Plain(row.What)}", $"Keyboard › {product} › {row.Group}",
                    $"{Route}#{DocsAnchor.Of(product)}", SiteSearchKind.Key);
            }
        }
    }

    // The build writes this from the records KeyboardByHand cites (ExGrid.Docs.Generator).
    private static partial DocsCite[] ReadCites();
}
