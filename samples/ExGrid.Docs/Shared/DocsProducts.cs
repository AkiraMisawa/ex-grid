namespace ExGrid.Docs;

/// <summary>
/// Which product an Example belongs to, read from its folder under <c>Examples/</c>, which is its
/// namespace: what the site says of it when it is read for React (ADR-0350 §12).
/// </summary>
internal static class DocsProducts
{
    private const string Examples = "ExGrid.Docs.Examples.";

    /// <summary>The product of an Example's component, as a page's Product names it.</summary>
    /// <exception cref="ArgumentException">The component is not in a product's folder under <c>Examples/</c>.</exception>
    public static string OfExample(Type example)
    {
        var ns = example.Namespace ?? "";
        var folder = ns.StartsWith(Examples, StringComparison.Ordinal) ? ns[Examples.Length..].Split('.')[0] : "";
        return folder switch
        {
            "Grid" => "ExGrid",
            "Sheet" => "ExSheet",
            "Pivot" => "ExPivot",
            "Chart" => "ExChart",
            "Board" => "ExBoard",
            "Snapshot" => "ExGrid.Data",
            _ => throw new ArgumentException(
                $"{example.FullName} is not in a product's folder under Examples/, so the site cannot say what it is under React. Add the folder here.",
                nameof(example)),
        };
    }
}
