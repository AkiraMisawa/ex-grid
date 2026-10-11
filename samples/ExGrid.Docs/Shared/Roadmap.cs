namespace ExGrid.Docs;

/// <summary>Where an open ticket stands, by the state of its decision (ADR-0191).</summary>
internal enum RoadmapState
{
    /// <summary>Its decision is recorded, and it waits only to be built: <c>ready-for-agent</c> or <c>ready-for-human</c>.</summary>
    Decided,

    /// <summary>It needs a decision first: <c>needs-triage</c> or <c>needs-info</c>.</summary>
    Waiting,
}

/// <summary>What a row of a product's scope table says of its feature (ADR-0046, ADR-0059).</summary>
internal enum RoadmapScope
{
    /// <summary>In the product's first version: ✓ under First version.</summary>
    FirstVersion,

    /// <summary>Left for later: ✓ under Later.</summary>
    Later,

    /// <summary>Refused: ✗.</summary>
    NotPlanned,
}

/// <summary>Which of the Roadmap's six sections a part of its page draws (ADR-0191).</summary>
public enum RoadmapPartKind
{
    /// <summary>The Change Notes: on main, not in a version yet.</summary>
    OnMain,

    /// <summary>Tickets whose decision is recorded.</summary>
    Decided,

    /// <summary>Tickets that need a decision.</summary>
    Waiting,

    /// <summary>The scope tables' Later rows.</summary>
    Later,

    /// <summary>What the next stable version requires, written by hand.</summary>
    TowardStable,

    /// <summary>The scope tables' refused rows, and the refusals written by hand.</summary>
    NotPlanned,
}

/// <summary>An open ticket, as the Roadmap lists it: read from its file when the site was built.</summary>
/// <param name="Feature">Its spec's folder under <c>docs/specs/</c>: <c>exsheet-functions</c>.</param>
/// <param name="Number">Its number in that folder.</param>
/// <param name="TitleHtml">Its title, the first line without its number, rendered.</param>
/// <param name="Title">The same as plain text, for the search.</param>
/// <param name="Products">The products its Roadmap line names.</param>
/// <param name="SentenceHtml">Its Roadmap line's sentence, with any ADR it names linked.</param>
/// <param name="Sentence">The same as plain text, for the search.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Status">Its <c>Status:</c> role, as the ticket writes it.</param>
/// <param name="Path">Its file, from the repository's root.</param>
internal sealed record RoadmapTicket(
    string Feature, string Number, string TitleHtml, string Title, IReadOnlyList<string> Products,
    string SentenceHtml, string Sentence, RoadmapState State, string Status, string Path)
{
    /// <summary>The ticket at the tag or the commit the site was built from.</summary>
    public string Url => DocsBuild.BlobUrl(Path);

    /// <summary>What the link to it says: its spec and number.</summary>
    public string Name => $"{Feature} #{Number}";

    /// <summary>What it waits for, in plain words: a decision or information, never a promise.</summary>
    public string? Waiting => Status switch
    {
        "needs-triage" => "Waits for a decision to be recorded",
        "needs-info" => "Waits for what its decision needs to know",
        _ => null,
    };
}

/// <summary>A row of ADR-0046's or ADR-0059's scope table, read when the site was built.</summary>
/// <param name="Product">The product the table is of.</param>
/// <param name="Adr">The decision record's number.</param>
/// <param name="AdrFile">Its file under <c>docs/adr/</c>.</param>
/// <param name="Scope">Which mark the row carries.</param>
/// <param name="LabelHtml">What the row is about, rendered.</param>
/// <param name="Label">The same as plain text.</param>
/// <param name="WhyHtml">Its "Why later", rendered; empty when the table gives none.</param>
/// <param name="Why">The same as plain text.</param>
internal sealed record RoadmapScopeRow(
    string Product, string Adr, string AdrFile, RoadmapScope Scope, string LabelHtml, string Label, string WhyHtml, string Why)
{
    /// <summary>The decision record, at the tag or the commit the site was built from.</summary>
    public string AdrUrl => DocsBuild.BlobUrl("docs/adr/" + AdrFile);
}

/// <summary>A record a hand-written row cites, as the build found it.</summary>
/// <param name="Name">The citation as written: <c>ADR-0031</c>, <c>DoD Step 7</c>.</param>
/// <param name="Title">What a link to it says.</param>
/// <param name="Path">Its file, from the repository's root.</param>
/// <param name="Anchor">The heading it names in that file, as GitHub anchors it; <see langword="null"/> for the whole file.</param>
internal sealed record RoadmapCite(string Name, string Title, string Path, string? Anchor)
{
    /// <summary>The record at the tag or the commit the site was built from.</summary>
    public string Url => DocsBuild.BlobUrl(Path) + (Anchor is null ? "" : "#" + Anchor);
}

/// <summary>
/// A line of the Roadmap written by hand (ADR-0191), in <c>RoadmapByHand</c>: what the next stable version
/// requires, or what the family will not do. The build reads every one: its text holds no date, quarter
/// or version, its products are the four, and each record it cites exists.
/// </summary>
/// <param name="Text">What it says, in plain text.</param>
/// <param name="Products">The products it reaches.</param>
/// <param name="Cites">The records that require or refused it: <c>ADR-0031</c>, <c>DoD Step 7</c>, <c>DoD §21.11</c>.</param>
internal sealed record RoadmapNote(string Text, IReadOnlyList<string> Products, IReadOnlyList<string> Cites);

/// <summary>A record the Roadmap links rather than copies, so that it never reads as planned.</summary>
/// <param name="Title">What the link says.</param>
/// <param name="Cite">The record, as a <see cref="RoadmapNote"/> cites one.</param>
/// <param name="About">A sentence that says what the reader finds there.</param>
internal sealed record RoadmapRecord(string Title, string Cite, string About);

/// <summary>
/// The Roadmap (ADR-0191), read from the repository when the site is built: the open tickets, the scope
/// tables' Later and refused rows, and the rows written by hand with the records they cite. The Roadmap
/// page and the search read it here. Nothing is fetched.
/// </summary>
internal static partial class Roadmap
{
    /// <summary>The page's route.</summary>
    public const string Route = "roadmap";

    /// <summary>The tickets on <c>main</c>, which may be newer than the ref the site was built from.</summary>
    public const string TicketsOnMain = DocsBuild.Repository + "/tree/main/docs/specs";

    /// <summary>The open tickets a Consumer would care about, by product, then spec and number.</summary>
    public static IReadOnlyList<RoadmapTicket> Tickets { get; } = ReadTickets();

    /// <summary>Every row of ADR-0046's and ADR-0059's scope tables, in the tables' order.</summary>
    public static IReadOnlyList<RoadmapScopeRow> Scope { get; } = ReadScope();

    private static readonly Dictionary<string, RoadmapCite> Cites = ReadCites().ToDictionary(c => c.Name, StringComparer.Ordinal);

    /// <summary>
    /// The record <paramref name="cite"/> names, as the build found it. Every citation <c>RoadmapByHand</c>
    /// writes is here: the build refuses one that leads nowhere.
    /// </summary>
    public static RoadmapCite Cite(string cite) =>
        Cites.TryGetValue(cite, out var found) ? found : new RoadmapCite(cite, cite, "docs", null);

    /// <summary>Whether <paramref name="products"/> holds <paramref name="product"/>, or no product is chosen.</summary>
    public static bool Shows(IEnumerable<string> products, string? product) =>
        product is null || products.Contains(product);

    /// <summary>
    /// Each item of the Roadmap as a place the search leads to: the tickets, the scope tables' Later and
    /// refused rows, and the rows written by hand. The Change Notes are found as the Release Notes' lines.
    /// </summary>
    public static IEnumerable<SiteSearchEntry> SearchEntries()
    {
        foreach (var ticket in Tickets)
        {
            var section = ticket.State == RoadmapState.Decided ? Decided : Waiting;
            yield return Entry(ticket.Title + " — " + ticket.Sentence, section, ticket.Products);
        }
        foreach (var row in Scope.Where(r => r.Scope == RoadmapScope.Later))
            yield return Entry(row.Label, Later, [row.Product]);
        foreach (var note in RoadmapByHand.TowardStable)
            yield return Entry(note.Text, TowardStable, note.Products);
        foreach (var row in Scope.Where(r => r.Scope == RoadmapScope.NotPlanned))
            yield return Entry(row.Label, NotPlanned, [row.Product]);
        foreach (var note in RoadmapByHand.NotPlanned)
            yield return Entry(note.Text, NotPlanned, note.Products);
    }

    private static SiteSearchEntry Entry(string title, string section, IEnumerable<string> products) =>
        new(title, $"Roadmap › {section} · {string.Join(", ", products)}", $"{Route}#{DocsAnchor.Of(section)}", SiteSearchKind.Roadmap);

    // The build writes these from the repository (ExGrid.Docs.Generator).
    private static partial RoadmapTicket[] ReadTickets();

    private static partial RoadmapScopeRow[] ReadScope();

    private static partial RoadmapCite[] ReadCites();
}
