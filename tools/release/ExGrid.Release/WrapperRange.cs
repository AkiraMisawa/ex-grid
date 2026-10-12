using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ExGrid.Release;

/// <summary>
/// A NuGet package version, as nuget.org lists it and a project's range bounds it: one to four numbers, an
/// optional prerelease label after <c>-</c>, and build metadata after <c>+</c>, which is ignored. Ordered as
/// NuGet orders versions: by the numbers, a missing one read as 0, then a prerelease below its release.
/// </summary>
public sealed record PackageVersion(int Major, int Minor, int Patch, int Revision, string Prerelease) : IComparable<PackageVersion>
{
    /// <summary>Whether this version has no prerelease label: a stable version.</summary>
    public bool IsStable => Prerelease.Length == 0;

    /// <summary>Reads a version, or null when <paramref name="text"/> is not one.</summary>
    public static PackageVersion? Parse(string text)
    {
        var value = text.Trim();
        var plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];
        var dash = value.IndexOf('-');
        var prerelease = dash >= 0 ? value[(dash + 1)..] : "";
        if (dash >= 0 && (prerelease.Length == 0 || prerelease.Split('.').Any(p => p.Length == 0 || !p.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))))
            return null;
        var numbers = (dash >= 0 ? value[..dash] : value).Split('.');
        if (numbers.Length > 4) return null;
        var parts = new int[4];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (numbers[i].Length == 0 || !numbers[i].All(char.IsAsciiDigit) || !int.TryParse(numbers[i], out parts[i])) return null;
        }
        return new PackageVersion(parts[0], parts[1], parts[2], parts[3], prerelease);
    }

    /// <inheritdoc />
    public int CompareTo(PackageVersion? other)
    {
        if (other is null) return 1;
        var numbers = (Major, Minor, Patch, Revision).CompareTo((other.Major, other.Minor, other.Patch, other.Revision));
        if (numbers != 0) return numbers;
        if (IsStable || other.IsStable) return IsStable == other.IsStable ? 0 : IsStable ? 1 : -1;
        var mine = Prerelease.Split('.');
        var theirs = other.Prerelease.Split('.');
        for (var i = 0; i < Math.Min(mine.Length, theirs.Length); i++)
        {
            var a = mine[i];
            var b = theirs[i];
            var aNumber = a.All(char.IsAsciiDigit);
            var bNumber = b.All(char.IsAsciiDigit);
            var order = aNumber && bNumber ? (a.Length, a).CompareTo((b.Length, b))
                : aNumber ? -1
                : bNumber ? 1
                : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            if (order != 0) return order;
        }
        return mine.Length.CompareTo(theirs.Length);
    }

    /// <summary>Whether two versions are the same version, as NuGet compares them: 9.6 is 9.6.0.</summary>
    public bool SameAs(PackageVersion other) => CompareTo(other) == 0;

    /// <summary>The version as NuGet normalises it: three numbers, a fourth only when it is not 0.</summary>
    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}" + (Revision != 0 ? $".{Revision}" : "") + (IsStable ? "" : $"-{Prerelease}");
}

/// <summary>
/// A NuGet version range as a project declares it: <c>9.0.0</c>, which is 9.0.0 and above; <c>[9.6.0]</c>,
/// exactly that; or an interval, <c>[9.6.0,10.0.0)</c>, either end open or left out. A floating version
/// (<c>9.*</c>) is not read: the versions it takes cannot be said from the project alone.
/// </summary>
public sealed record VersionRange(string Text, PackageVersion? Minimum, bool MinimumIncluded, PackageVersion? Maximum, bool MaximumIncluded)
{
    /// <summary>Reads a range, or null when <paramref name="text"/> is not one this check can read.</summary>
    public static VersionRange? Parse(string text)
    {
        var value = text.Trim();
        if (value.Length == 0 || value.Contains('*')) return null;
        if (value[0] is not ('[' or '('))
            return PackageVersion.Parse(value) is { } floor ? new VersionRange(value, floor, true, null, false) : null;
        if (value.Length < 2 || value[^1] is not (']' or ')')) return null;
        var lowerIncluded = value[0] == '[';
        var upperIncluded = value[^1] == ']';
        var inner = value[1..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0)
        {
            // [1.0] is exactly 1.0; (1.0) and [1.0) name nothing.
            return lowerIncluded && upperIncluded && PackageVersion.Parse(inner) is { } exact
                ? new VersionRange(value, exact, true, exact, true)
                : null;
        }
        if (inner.IndexOf(',', comma + 1) >= 0) return null;
        var lower = inner[..comma].Trim();
        var upper = inner[(comma + 1)..].Trim();
        PackageVersion? minimum = null, maximum = null;
        if (lower.Length > 0 && (minimum = PackageVersion.Parse(lower)) is null) return null;
        if (upper.Length > 0 && (maximum = PackageVersion.Parse(upper)) is null) return null;
        if (minimum is null && maximum is null) return null;
        return new VersionRange(value, minimum, minimum is not null && lowerIncluded, maximum, maximum is not null && upperIncluded);
    }

    /// <summary>Whether <paramref name="version"/> is inside this range.</summary>
    public bool Contains(PackageVersion version)
    {
        if (Minimum is not null)
        {
            var below = version.CompareTo(Minimum);
            if (below < 0 || (below == 0 && !MinimumIncluded)) return false;
        }
        if (Maximum is not null)
        {
            var above = version.CompareTo(Maximum);
            if (above > 0 || (above == 0 && !MaximumIncluded)) return false;
        }
        return true;
    }
}

/// <summary>
/// A version nuget.org has published, and when: <see cref="Published"/> is null where nuget.org gives no date,
/// as for an unlisted version, whose registration reads 1900-01-01.
/// </summary>
public sealed record PublishedVersion(string Version, DateTimeOffset? Published);

/// <summary>The versions of MudBlazor nuget.org has published, with their dates, as the release check asks for them.</summary>
public interface IPublishedVersions
{
    /// <summary>Where the list is read from, as a message names it.</summary>
    string Source { get; }

    /// <summary>
    /// Every version the source lists, prereleases among them, each with the date it was published; or false,
    /// with why, when the list could not be read. The check never passes on a list it could not read.
    /// </summary>
    bool TryRead(out IReadOnlyList<PublishedVersion> versions, out string failure);
}

/// <summary>
/// NuGet's registration index of a package, read once: its pages, each with the versions it holds, and for each
/// version its catalog entry's <c>version</c>, <c>published</c> and <c>listed</c>. A page the index does not
/// inline is read from its <c>@id</c>. Read from nuget.org by its address, or from a file when given a path (a
/// copy saved for a run without the network, its pages inlined). <paramref name="read"/> replaces how an address
/// or a path is read, as a test gives the data without the network.
/// </summary>
public sealed class NuGetRegistration(string index, Func<string, string>? read = null) : IPublishedVersions
{
    private IReadOnlyList<PublishedVersion>? _versions;
    private string? _failure;

    /// <inheritdoc />
    public string Source => index;

    /// <inheritdoc />
    public bool TryRead(out IReadOnlyList<PublishedVersion> versions, out string failure)
    {
        if (_versions is null && _failure is null)
        {
            try
            {
                var fetch = read ?? Fetch;
                var text = fetch(index);
                if (Pages(text, out var pages, out var why) is not { } inlined) _failure = why;
                else
                {
                    var all = new List<PublishedVersion>(inlined);
                    foreach (var page in pages)
                    {
                        if (Items(fetch(page), page, out why) is { } items) all.AddRange(items);
                        else
                        {
                            _failure = why;
                            break;
                        }
                    }
                    if (_failure is null) _versions = all;
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Whatever stopped the read, the list was not read, and the check says so rather than passing.
                _failure = e.Message;
            }
        }
        versions = _versions ?? [];
        failure = _failure ?? "";
        return _versions is not null;
    }

    private static string Fetch(string address)
    {
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllText(address);
        using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ExGrid.Release");
        return http.GetStringAsync(address).GetAwaiter().GetResult();
    }

    /// <summary>
    /// The versions a registration index inlines, and the addresses of the pages it does not; null, with why, when
    /// <paramref name="json"/> is not a registration index.
    /// </summary>
    public static IReadOnlyList<PublishedVersion>? Pages(string json, out IReadOnlyList<string> pages, out string failure)
    {
        pages = [];
        failure = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("items", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                failure = "it is not a registration index, {\"items\": [pages]}";
                return null;
            }
            var inlined = new List<PublishedVersion>();
            var away = new List<string>();
            foreach (var page in list.EnumerateArray())
            {
                if (page.ValueKind == JsonValueKind.Object && page.TryGetProperty("items", out _))
                {
                    if (Leaves(page, "the index", out failure) is not { } leaves) return null;
                    inlined.AddRange(leaves);
                }
                else if (page.ValueKind == JsonValueKind.Object && page.TryGetProperty("@id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    away.Add(id.GetString()!);
                }
                else
                {
                    failure = "a page of the registration index has neither its versions nor its address ('items' or '@id')";
                    return null;
                }
            }
            pages = away;
            return inlined;
        }
        catch (JsonException e)
        {
            failure = $"it is not JSON ({e.Message})";
            return null;
        }
    }

    /// <summary>The versions a registration page holds; null, with why, when <paramref name="json"/> is not a page.</summary>
    public static IReadOnlyList<PublishedVersion>? Items(string json, string page, out string failure)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Leaves(document.RootElement, page, out failure);
        }
        catch (JsonException e)
        {
            failure = $"{page} is not JSON ({e.Message})";
            return null;
        }
    }

    // A page's leaves: each catalog entry's version and when it was published. A listed version whose date is
    // missing or unreadable refuses the whole list, rather than letting the version go unasked; an unlisted one,
    // which nuget.org dates 1900-01-01, has no date.
    private static IReadOnlyList<PublishedVersion>? Leaves(JsonElement page, string where, out string failure)
    {
        failure = "";
        if (page.ValueKind != JsonValueKind.Object || !page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            failure = $"{where} is not a registration page, {{\"items\": [versions]}}";
            return null;
        }
        var read = new List<PublishedVersion>();
        foreach (var leaf in items.EnumerateArray())
        {
            if (leaf.ValueKind != JsonValueKind.Object
                || !leaf.TryGetProperty("catalogEntry", out var entry) || entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String)
            {
                failure = $"a version in {where} has no catalog entry naming it ('catalogEntry', 'version')";
                return null;
            }
            var listed = !entry.TryGetProperty("listed", out var flag) || flag.ValueKind != JsonValueKind.False;
            DateTimeOffset? published = null;
            if (entry.TryGetProperty("published", out var date) && date.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(date.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when))
            {
                if (listed && when.Year > 1900) published = when;
            }
            else if (listed)
            {
                failure = $"{version.GetString()} in {where} is listed and gives no date it was published ('published')";
                return null;
            }
            read.Add(new PublishedVersion(version.GetString()!, published));
        }
        return read;
    }
}

/// <summary>
/// The MudBlazor range the Wrappers declare, and the versions WR-10's records must name within it
/// (ADR-0205 and ADR-0208, 2026-10-12). A Wrapper is a project <c>src/*.MudBlazor/*.csproj</c>, and its range
/// is the <c>Version</c> of its <c>PackageReference</c> to MudBlazor.
/// <para>Decided with the user on 2026-10-12: "A MudBlazor 9.x published after the floor record and before X
/// asks for the layer-2 runs at that version only. The floor run (layer 3 at 9.6.0) holds while its paths are
/// unchanged." And: a layer-2 run at a 9.x published after the floor record is a run of its own, recorded in a
/// directory of its own and tagged by it, and WR-10's entry in the index names its record besides the floor
/// record.</para>
/// <para>How the check reads it: each version's date is the <c>published</c> date nuget.org's registration
/// index gives, and X's time is X's commit time, as git gives it (the tagged commit for a release candidate).
/// Every stable version inside the range the Wrappers declare at X, published before X's time, must be named by
/// one of WR-10's records that holds: one published before the floor record by the floor run's layer-2 runs,
/// which cover every published version of the range; one published after it by them or by a later record. A
/// version published after X's time is not asked for. A version nuget.org gives no date for, as an unlisted one,
/// is asked for, since whether it came before X cannot be said. A list it cannot read refuses the tag: it never
/// passes unread.</para>
/// </summary>
public static class WrapperRange
{
    /// <summary>The row whose records name the versions their layer-2 runs covered.</summary>
    public const string Row = "WR-10";

    /// <summary>The Wrappers' projects.</summary>
    public const string Projects = ":(glob)src/*.MudBlazor/*.csproj";

    /// <summary>The package whose range they declare.</summary>
    public const string Package = "MudBlazor";

    /// <summary>
    /// nuget.org's registration index of MudBlazor, which the release workflow reads: the SemVer 2.0.0 hive, since
    /// the SemVer 1.0.0 one leaves out a version whose dependencies name a SemVer 2.0.0 version, and a version left
    /// out would go unasked.
    /// </summary>
    public const string Registration = "https://api.nuget.org/v3/registration5-gz-semver2/mudblazor/index.json";

    /// <summary>A Wrapper's project, and the range it declares.</summary>
    public sealed record Declared(string Project, VersionRange Range);

    /// <summary>
    /// The range each Wrapper declares at <paramref name="tree"/>, or null, refused, when any of them cannot be
    /// read: no Wrapper, a project that is not XML, no reference to MudBlazor, or a version that is not a range.
    /// </summary>
    public static IReadOnlyList<Declared>? Read(ISourceTree tree, Report report, string forRow)
    {
        var projects = tree.Matches(Projects);
        if (projects.Count == 0)
        {
            report.Refuse($"{forRow}: {tree.Name} has no Wrapper project ({Projects}), so the {Package} range its layer-2 runs cover cannot be read (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
            return null;
        }
        var declared = new List<Declared>();
        var good = true;
        foreach (var project in projects)
        {
            var text = tree.ReadText(project) ?? "";
            XDocument document;
            try
            {
                document = XDocument.Parse(text);
            }
            catch (XmlException e)
            {
                report.Refuse($"{forRow}: {project} is not XML ({e.Message}), so the {Package} range it declares cannot be read (ADR-0205, ADR-0208, 2026-10-12).", project);
                good = false;
                continue;
            }
            var versions = document.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference"
                    && string.Equals((string?)e.Attribute("Include"), Package, StringComparison.OrdinalIgnoreCase))
                .Select(e => (string?)e.Attribute("Version") ?? e.Elements().FirstOrDefault(v => v.Name.LocalName == "Version")?.Value)
                .ToList();
            if (versions.Count == 0 || versions.Any(v => string.IsNullOrWhiteSpace(v)))
            {
                report.Refuse($"{forRow}: {project} declares no {Package} version on its PackageReference, so the range its layer-2 runs cover cannot be read (ADR-0205, ADR-0208, 2026-10-12).", project);
                good = false;
                continue;
            }
            foreach (var version in versions)
            {
                if (VersionRange.Parse(version!) is { } range)
                {
                    declared.Add(new Declared(project, range));
                }
                else
                {
                    report.Refuse($"{forRow}: {project} declares {Package} '{version}', which this check cannot read as a range, as [9.6.0,10.0.0) (ADR-0205, ADR-0208, 2026-10-12).", project);
                    good = false;
                }
            }
        }
        return good ? declared : null;
    }

    /// <summary>
    /// The stable versions of <paramref name="published"/> inside any range in <paramref name="declared"/>, published
    /// before <paramref name="before"/> or with no date, that <paramref name="covered"/> does not name, in order.
    /// </summary>
    public static IReadOnlyList<PublishedVersion> Missing(IReadOnlyList<Declared> declared, IEnumerable<PublishedVersion> published, IEnumerable<string> covered, DateTimeOffset before)
    {
        var named = covered.Select(PackageVersion.Parse).OfType<PackageVersion>().ToList();
        return Asked(declared, published, before)
            .Where(p => !named.Any(n => n.SameAs(PackageVersion.Parse(p.Version)!)))
            .ToList();
    }

    // The stable versions inside the range published before X's time, or with no date, one each, in order.
    private static IReadOnlyList<PublishedVersion> Asked(IReadOnlyList<Declared> declared, IEnumerable<PublishedVersion> published, DateTimeOffset before) =>
        published
            .Select(p => (Item: p, Version: PackageVersion.Parse(p.Version)))
            .Where(p => p.Version is { IsStable: true } v && declared.Any(d => d.Range.Contains(v)) && (p.Item.Published is null || p.Item.Published < before))
            .DistinctBy(p => p.Version!.ToString())
            .OrderBy(p => p.Version)
            .Select(p => p.Item)
            .ToList();

    /// <summary>
    /// Refuses the tag unless WR-10's records that hold name, between them (<paramref name="covered"/>), every stable
    /// version published before X's time (<paramref name="x"/>) inside the range the Wrappers declare at
    /// <paramref name="target"/>. <paramref name="floor"/> is the floor record's commit time, which says of a version
    /// the records do not name whether the floor run or a later run should have covered it. Returns the versions not
    /// named, or null when that could not be checked (refused too).
    /// </summary>
    public static IReadOnlyList<PublishedVersion>? Check(ByHandRow row, IReadOnlyCollection<string> covered, ISourceTree target, DateTimeOffset x, DateTimeOffset floor, IPublishedVersions? published, Report report)
    {
        var declared = Read(target, report, row.Name);
        if (declared is null) return null;
        var ranges = string.Join(", ", declared.Select(d => d.Range.Text).Distinct(StringComparer.Ordinal));
        if (published is null)
        {
            report.Refuse($"{row.Name}: no list of {Package}'s published versions was given, so whether its records' layer-2 runs cover every version published before X in the range the Wrappers declare ({ranges}) cannot be checked (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
            return null;
        }
        if (!published.TryRead(out var listed, out var failure))
        {
            report.Refuse($"{row.Name}: {Package}'s published versions could not be read from {published.Source} ({failure}), so whether its records' layer-2 runs cover every version published before X in the range the Wrappers declare ({ranges}) cannot be checked. The tag is refused as unchecked (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
            return null;
        }
        var after = listed
            .Where(p => p.Published is { } when && when >= x && PackageVersion.Parse(p.Version) is { IsStable: true } v && declared.Any(d => d.Range.Contains(v)))
            .Select(p => p.Version).Distinct().ToList();
        if (after.Count > 0)
            report.Notice($"{row.Name}: {Package} {string.Join(", ", after)}, published within the range the Wrappers declare after X ({x:yyyy-MM-dd HH:mm:ss}Z, its commit time), {(after.Count == 1 ? "is" : "are")} not asked for (ADR-0205, ADR-0208, 2026-10-12).");
        var missing = Missing(declared, listed, covered, x);
        if (missing.Count > 0)
        {
            var named = covered.Count == 0 ? "none" : string.Join(", ", covered);
            static string When(PublishedVersion p) => p.Published is { } when ? $"{p.Version} ({when:yyyy-MM-dd})" : $"{p.Version} (no date given)";
            var parts = new List<string>();
            var before = missing.Where(p => p.Published is { } when && when < floor).ToList();
            var later = missing.Where(p => p.Published is { } when && when >= floor).ToList();
            var undated = missing.Where(p => p.Published is null).ToList();
            if (before.Count > 0)
                parts.Add($"{string.Join(", ", before.Select(When))}, published before the floor record's commit, which the floor run's layer-2 runs cover, every published version of the range");
            if (later.Count > 0)
                parts.Add($"{string.Join(", ", later.Select(When))}, published after the floor record and before X, which {(later.Count == 1 ? "asks" : "ask")} for each Wrapper's layer-2 suite at that version only, a run of its own whose record goes in a directory of its own, tagged by it and named in WR-10's entry in {ByHand.IndexPath} besides the floor record");
            if (undated.Count > 0)
                parts.Add($"{string.Join(", ", undated.Select(When))}, for which nuget.org gives no date, so whether it came before X cannot be said");
            report.Refuse($"{row.Name}: {Package} {string.Join(", ", missing.Select(m => m.Version))}, published within the range the Wrappers declare ({ranges}), "
                + $"{(missing.Count == 1 ? "is" : "are")} not among the versions its records' layer-2 runs covered ({named}): {string.Join("; ", parts)}. "
                + "The floor run holds while its paths are unchanged (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
        }
        return missing;
    }
}
