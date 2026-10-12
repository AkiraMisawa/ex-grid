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

/// <summary>The versions of MudBlazor nuget.org has published, as the release check asks for them.</summary>
public interface IPublishedVersions
{
    /// <summary>Where the list is read from, as a message names it.</summary>
    string Source { get; }

    /// <summary>
    /// Every version the source lists, prereleases among them; or false, with why, when the list could not
    /// be read. The check never passes on a list it could not read.
    /// </summary>
    bool TryRead(out IReadOnlyList<string> versions, out string failure);
}

/// <summary>
/// NuGet's flat-container index of a package, <c>{"versions": [...]}</c>, read once: from nuget.org by its
/// address, or from a file when given a path (a copy saved for a run without the network). It lists every
/// version published, unlisted ones among them.
/// </summary>
public sealed class NuGetFlatContainer(string index) : IPublishedVersions
{
    private IReadOnlyList<string>? _versions;
    private string? _failure;

    /// <inheritdoc />
    public string Source => index;

    /// <inheritdoc />
    public bool TryRead(out IReadOnlyList<string> versions, out string failure)
    {
        if (_versions is null && _failure is null)
        {
            string? text = null;
            try
            {
                if (index.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || index.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ExGrid.Release");
                    text = http.GetStringAsync(index).GetAwaiter().GetResult();
                }
                else
                {
                    text = File.ReadAllText(index);
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Whatever stopped the read, the list was not read, and the check says so rather than passing.
                _failure = e.Message;
            }
            if (text is not null)
            {
                if (Parse(text) is { } read) _versions = read;
                else _failure = "it is not a flat-container index, {\"versions\": [...]}";
            }
        }
        versions = _versions ?? [];
        failure = _failure ?? "";
        return _versions is not null;
    }

    /// <summary>The versions a flat-container index lists, or null when <paramref name="json"/> is not one.</summary>
    public static IReadOnlyList<string>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("versions", out var list)
                || list.ValueKind != JsonValueKind.Array)
                return null;
            var versions = new List<string>();
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) return null;
                versions.Add(item.GetString()!);
            }
            return versions;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The MudBlazor range the Wrappers declare, and the versions WR-10's record must name within it
/// (ADR-0205 and ADR-0208, 2026-10-12). A Wrapper is a project <c>src/*.MudBlazor/*.csproj</c>, and its range
/// is the <c>Version</c> of its <c>PackageReference</c> to MudBlazor. Beside the floor run, each Wrapper's
/// layer-2 suite runs at every published version of that range, and the record names the versions it ran.
/// <para>A stable version published after the record, inside the range, asks for the layer-2 runs at that
/// version only; the floor run, layer 3 at the floor, holds while its paths are unchanged (decided with the
/// user on 2026-10-12). nuget.org's flat-container index carries no dates, so the check asks for every
/// stable version published inside the range by the time it runs, and refuses the tag naming each one the
/// record does not name. A list it cannot read refuses the tag: it never passes unread.</para>
/// </summary>
public static class WrapperRange
{
    /// <summary>The row whose record names the versions its layer-2 runs covered.</summary>
    public const string Row = "WR-10";

    /// <summary>The Wrappers' projects.</summary>
    public const string Projects = ":(glob)src/*.MudBlazor/*.csproj";

    /// <summary>The package whose range they declare.</summary>
    public const string Package = "MudBlazor";

    /// <summary>nuget.org's flat-container index of MudBlazor, which the release workflow reads.</summary>
    public const string FlatContainer = "https://api.nuget.org/v3-flatcontainer/mudblazor/index.json";

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
    /// The stable versions of <paramref name="published"/> inside any range in <paramref name="declared"/>
    /// that <paramref name="covered"/> does not name, in order.
    /// </summary>
    public static IReadOnlyList<PackageVersion> Missing(IReadOnlyList<Declared> declared, IEnumerable<string> published, IEnumerable<string> covered)
    {
        var named = covered.Select(PackageVersion.Parse).OfType<PackageVersion>().ToList();
        return published
            .Select(PackageVersion.Parse)
            .OfType<PackageVersion>()
            .Where(v => v.IsStable && declared.Any(d => d.Range.Contains(v)) && !named.Any(n => n.SameAs(v)))
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>
    /// Refuses the tag unless <paramref name="row"/>'s record names every stable version published inside the
    /// range the Wrappers declare at <paramref name="target"/>. Returns the versions it does not name, or null
    /// when that could not be checked (refused too).
    /// </summary>
    public static IReadOnlyList<PackageVersion>? Check(ByHandRow row, ISourceTree target, IPublishedVersions? published, Report report)
    {
        var record = row.Record!;
        var declared = Read(target, report, row.Name);
        if (declared is null) return null;
        var ranges = string.Join(", ", declared.Select(d => d.Range.Text).Distinct(StringComparer.Ordinal));
        if (published is null)
        {
            report.Refuse($"{row.Name}: no list of {Package}'s published versions was given, so whether its record's layer-2 runs cover every version published in the range the Wrappers declare ({ranges}) cannot be checked (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
            return null;
        }
        if (!published.TryRead(out var listed, out var failure))
        {
            report.Refuse($"{row.Name}: {Package}'s published versions could not be read from {published.Source} ({failure}), so whether its record's layer-2 runs cover every version published in the range the Wrappers declare ({ranges}) cannot be checked. The tag is refused as unchecked (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
            return null;
        }
        var missing = Missing(declared, listed, record.Versions ?? []);
        if (missing.Count > 0)
        {
            var named = (record.Versions ?? []).Count == 0 ? "none" : string.Join(", ", record.Versions!);
            report.Refuse($"{row.Name}: {Package} {string.Join(", ", missing)}, published within the range the Wrappers declare ({ranges}), "
                + $"{(missing.Count == 1 ? "is" : "are")} not among the versions its record's layer-2 runs covered ({named}). "
                + $"A version published after the floor record asks for each Wrapper's layer-2 suite at that version only, recorded and named in the record's 'versions' in {ByHand.IndexPath}; "
                + "the floor run holds while its paths are unchanged (ADR-0205, ADR-0208, 2026-10-12).", ByHand.IndexPath);
        }
        return missing;
    }
}
