using Xunit;

namespace ExGrid.Release.Tests;

public class TagVersionTests
{
    [Theory] // ADR-0042, ADR-0205: the suffix says what a tag publishes
    [InlineData("v0.1.0", "0.1.0", ReleaseKind.Stable)]
    [InlineData("v10.20.30", "10.20.30", ReleaseKind.Stable)]
    [InlineData("v0.1.0-rc.1", "0.1.0-rc.1", ReleaseKind.Candidate)]
    [InlineData("v0.1.1-rc.12", "0.1.1-rc.12", ReleaseKind.Candidate)]
    [InlineData("v0.1.0-beta.3", "0.1.0-beta.3", ReleaseKind.Prerelease)]
    [InlineData("v0.2.0-beta.1", "0.2.0-beta.1", ReleaseKind.Prerelease)]
    [InlineData("v0.1.0-alpha", "0.1.0-alpha", ReleaseKind.Prerelease)]
    public void A_tag_names_its_version_and_kind(string tag, string version, ReleaseKind kind)
    {
        var report = new Report();
        var read = TagVersion.Read(tag, report);

        Assert.Empty(report.Refusals);
        Assert.Equal(new TagVersion(tag, version, kind), read);
        Assert.Equal(kind != ReleaseKind.Prerelease, read!.RehearsesStable);
    }

    [Theory] // ADR-0042: a tag that does not name a version is refused, and so is one NuGet would publish under another
    [InlineData("0.1.0", "does not begin with v")]
    [InlineData("v0.1", "does not name a version")]
    [InlineData("v01.0.0", "does not name a version")]
    [InlineData("v0.01.0", "does not name a version")]
    [InlineData("v0.1.0+build.5", "does not name a version")]
    [InlineData("v0.1.0-", "does not name a version")]
    public void A_tag_that_names_no_version_is_refused(string tag, string why)
    {
        var report = new Report();

        Assert.Null(TagVersion.Read(tag, report));
        Assert.Contains(why, Assert.Single(report.Refusals).Message);
    }

    [Theory] // ADR-0205: a candidate spelt otherwise than -rc.N would publish without the checks it rehearses
    [InlineData("v0.1.0-rc1")]
    [InlineData("v0.1.0-rc")]
    [InlineData("v0.1.0-RC.1")]
    [InlineData("v0.1.0-rc.0")]
    [InlineData("v0.1.0-rc.01")]
    [InlineData("v0.1.0-rc.1.2")]
    [InlineData("v0.1.0-rcx.1")]
    public void A_candidate_is_written_rc_dot_n(string tag)
    {
        var report = new Report();

        Assert.Null(TagVersion.Read(tag, report));
        Assert.Contains("a candidate is written -rc.N with N from 1", Assert.Single(report.Refusals).Message);
    }

    [Theory] // ADR-0205: a stable version exists once a tag without a suffix does
    [InlineData("v0.1.0", true)]
    [InlineData("v1.0.0", true)]
    [InlineData("v0.1.0-rc.1", false)]
    [InlineData("v0.1.0-beta.2", false)]
    [InlineData("0.1.0", false)]
    [InlineData("v0.1", false)]
    [InlineData("verified/2026-10-12", false)]
    public void A_stable_tag_is_one_without_a_suffix(string tag, bool stable) =>
        Assert.Equal(stable, TagVersion.IsStableTag(tag));

    [Theory] // ADR-0205: stable versions are ordered by SemVer precedence, each number as a number
    [InlineData("v0.2.0", "v0.1.1", 1)]
    [InlineData("v0.1.1", "v0.2.0", -1)]
    [InlineData("v0.10.0", "v0.9.0", 1)]
    [InlineData("v1.0.0", "v0.99.99", 1)]
    [InlineData("v0.1.10", "v0.1.9", 1)]
    [InlineData("v0.1.0", "v0.1.0", 0)]
    [InlineData("v12345678901234567890.0.0", "v9.0.0", 1)]
    public void Stable_versions_are_ordered_by_their_numbers(string a, string b, int order) =>
        Assert.Equal(order, TagVersion.CompareNumbers(a, b));
}
