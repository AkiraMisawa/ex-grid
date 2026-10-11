namespace ExGrid.Docs;

/// <summary>
/// A record a reference page's hand-written row cites (ADR-0110), as the build found it: a decision
/// record, or a section or a step of the Definition of Done. The build refuses a citation that leads
/// nowhere, so every one a page shows exists.
/// </summary>
/// <param name="Name">The citation as written: <c>ADR-0017</c>, <c>DoD §24</c>.</param>
/// <param name="Title">What a link to it says.</param>
/// <param name="Path">Its file, from the repository's root.</param>
/// <param name="Anchor">The heading it names in that file, as GitHub anchors it; <see langword="null"/> for the whole file.</param>
public sealed record DocsCite(string Name, string Title, string Path, string? Anchor)
{
    /// <summary>The record at the tag or the commit the site was built from.</summary>
    public string Url => DocsBuild.BlobUrl(Path) + (Anchor is null ? "" : "#" + Anchor);
}
