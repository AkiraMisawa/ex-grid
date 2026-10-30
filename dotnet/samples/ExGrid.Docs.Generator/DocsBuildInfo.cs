using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExGrid.Docs.Generator;

/// <summary>What a Docs Site build was made from (ADR-0110), as the site states it.</summary>
public enum DocsBuildKind
{
    /// <summary>Built here, with no <c>DocsRef</c>: <c>dotnet run</c>, a local publish, or CI's build.</summary>
    Local,

    /// <summary>Published by the release from a <c>v*</c> tag, once nuget.org has the version.</summary>
    Tag,

    /// <summary>Published on demand from a branch: it describes changes that are not on nuget.org.</summary>
    Branch,

    /// <summary>Published on demand from a <c>lab/*</c> branch: an experiment.</summary>
    Experiment,
}

/// <summary>
/// The three inputs a Docs Site build is given, read together (ADR-0110, ADR-0190):
/// <list type="bullet">
/// <item><c>DocsVersion</c>, the version a release published, or <c>dev</c>;</item>
/// <item><c>DocsRef</c>, the tag (<c>v0.1.0-beta.3</c>), or the branch and its commit
/// (<c>main@4a17e3a…</c>), or nothing for a local build;</item>
/// <item><c>DocsPublishedOn</c>, the UTC day the release pushed the version to nuget.org.</item>
/// </list>
/// Inputs that disagree are refused, because a site that states one thing and links another would
/// be quietly wrong about the product.
/// </summary>
/// <param name="Kind">What the build was made from.</param>
/// <param name="Version">The version a tag's build states; <c>dev</c> when there is none.</param>
/// <param name="Branch">The branch an on-demand build was published from; empty otherwise.</param>
/// <param name="Commit">The commit an on-demand build was published from; empty otherwise.</param>
/// <param name="PublishedOn">The day the version reached nuget.org, as <c>yyyy-MM-dd</c>; empty when not given.</param>
public sealed record DocsBuildInfo(DocsBuildKind Kind, string Version, string Branch, string Commit, string PublishedOn)
{
    /// <summary>The prefix that makes a branch an experiment (ADR-0110).</summary>
    public const string LabPrefix = "lab/";

    /// <summary>A local build's inputs: what the project file defaults to.</summary>
    public static DocsBuildInfo Local { get; } = new(DocsBuildKind.Local, "dev", "", "", "");

    /// <summary>
    /// What a link into the repository points at: the tag, the commit, or <c>main</c> for a local
    /// build, so that a site published from a tag cites the decisions as they stood then.
    /// </summary>
    public string GitRef => Kind switch
    {
        DocsBuildKind.Tag => "v" + Version,
        DocsBuildKind.Branch or DocsBuildKind.Experiment => Commit,
        _ => "main",
    };

    private static readonly Regex VersionPattern = new(
        @"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);

    private static readonly Regex BranchRefPattern = new(
        @"^(?<branch>[A-Za-z0-9._/-]+)@(?<commit>[0-9a-f]{7,40})$", RegexOptions.CultureInvariant);

    private static readonly Regex DayPattern = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads the three inputs. Returns <see langword="null"/>, with <paramref name="error"/> saying
    /// what disagrees and how to pass it, when they cannot all be true of one build.
    /// </summary>
    /// <param name="version">The <c>DocsVersion</c> property; empty or <c>dev</c> for none.</param>
    /// <param name="docsRef">The <c>DocsRef</c> property; empty for a local build.</param>
    /// <param name="publishedOn">The <c>DocsPublishedOn</c> property; empty when not given.</param>
    /// <param name="error">Why the inputs were refused, or <see langword="null"/>.</param>
    public static DocsBuildInfo? Parse(string? version, string? docsRef, string? publishedOn, out string? error)
    {
        error = null;
        version = (version ?? "").Trim();
        docsRef = (docsRef ?? "").Trim();
        publishedOn = (publishedOn ?? "").Trim();
        if (version.Length == 0)
            version = "dev";

        if (version != "dev" && !VersionPattern.IsMatch(version))
        {
            error = $"DocsVersion '{version}' is not a version. Pass the version a release published, as -p:DocsVersion=0.1.0-beta.3, or leave it out.";
            return null;
        }
        if (publishedOn.Length > 0 && !IsDay(publishedOn))
        {
            error = $"DocsPublishedOn '{publishedOn}' is not a day. Pass the UTC day the release pushed the packages to nuget.org, as -p:DocsPublishedOn=2026-10-12 (ADR-0190).";
            return null;
        }

        if (docsRef.Length == 0)
        {
            if (publishedOn.Length > 0)
            {
                error = "DocsPublishedOn is the day a version reached nuget.org (ADR-0190), and a build with no DocsRef publishes no version. Pass the tag as -p:DocsRef too, or leave DocsPublishedOn out.";
                return null;
            }
            return new DocsBuildInfo(DocsBuildKind.Local, version, "", "", "");
        }

        if (docsRef[0] == 'v' && docsRef.IndexOf('@') < 0)
        {
            var tagged = docsRef.Substring(1);
            if (!VersionPattern.IsMatch(tagged))
            {
                error = $"DocsRef '{docsRef}' is not a version's tag. A tag names its version, as v0.1.0-beta.3 (ADR-0042).";
                return null;
            }
            if (version != tagged)
            {
                error = $"DocsRef names the tag {docsRef}, but DocsVersion is '{version}'. A site published from a tag states that tag's version: pass -p:DocsVersion={tagged}.";
                return null;
            }
            return new DocsBuildInfo(DocsBuildKind.Tag, version, "", "", publishedOn);
        }

        var match = BranchRefPattern.Match(docsRef);
        if (!match.Success)
        {
            error = $"DocsRef '{docsRef}' is neither a tag (v0.1.0-beta.3) nor a branch and its commit (main@4a17e3a, the commit's hash in lower case). Leave it out for a local build.";
            return null;
        }
        var branch = match.Groups["branch"].Value;
        if (version != "dev")
        {
            error = $"DocsRef names the branch {branch}, but DocsVersion is '{version}'. A site published from a branch states its commit and never a version, because nuget.org has none of it (ADR-0110): leave DocsVersion out.";
            return null;
        }
        if (publishedOn.Length > 0)
        {
            error = $"DocsRef names the branch {branch}, but DocsPublishedOn is given. Only a version reaches nuget.org on a day (ADR-0190): leave DocsPublishedOn out.";
            return null;
        }
        var kind = branch.StartsWith(LabPrefix, StringComparison.Ordinal) ? DocsBuildKind.Experiment : DocsBuildKind.Branch;
        return new DocsBuildInfo(kind, version, branch, match.Groups["commit"].Value, "");
    }

    private static bool IsDay(string text) =>
        DayPattern.IsMatch(text)
        && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
