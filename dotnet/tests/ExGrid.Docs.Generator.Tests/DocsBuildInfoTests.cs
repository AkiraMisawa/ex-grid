using ExGrid.Docs.Generator;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// What a Docs Site build states it was made from, read from <c>DocsVersion</c>, <c>DocsRef</c> and
/// <c>DocsPublishedOn</c> together (ADR-0110, ADR-0190): a version's tag, a branch and its commit,
/// or a local build. Inputs that cannot all be true of one build are refused.
/// </summary>
public class DocsBuildInfoTests
{
    private const string Sha = "4a17e3a0c2b14f7e9d3a5b6c7d8e9f0a1b2c3d4e";

    [Theory] // ADR-0110: a build given no DocsRef is a local one, and its links point at main
    [InlineData(null, null, "dev")]
    [InlineData("", "", "dev")]
    [InlineData("dev", "", "dev")]
    [InlineData("0.1.0-beta.3", "", "0.1.0-beta.3")]
    public void A_build_with_no_ref_is_local(string? version, string? docsRef, string expected)
    {
        var info = DocsBuildInfo.Parse(version, docsRef, "", out var error);

        Assert.Null(error);
        Assert.NotNull(info);
        Assert.Equal(DocsBuildKind.Local, info.Kind);
        Assert.Equal(expected, info.Version);
        Assert.Equal("main", info.GitRef);
    }

    [Fact] // ADR-0110, ADR-0190: the release's build states the version, its tag and the day of the push
    public void A_tag_build_states_its_version_and_links_at_the_tag()
    {
        var info = DocsBuildInfo.Parse("0.1.0-beta.3", "v0.1.0-beta.3", "2026-10-12", out var error);

        Assert.Null(error);
        Assert.Equal(new DocsBuildInfo(DocsBuildKind.Tag, "0.1.0-beta.3", "", "", "2026-10-12"), info);
        Assert.Equal("v0.1.0-beta.3", info!.GitRef);
    }

    [Fact] // ADR-0042: a stable version's tag is a tag like any other to the site
    public void A_stable_tag_is_a_tag()
    {
        var info = DocsBuildInfo.Parse("1.0.0", "v1.0.0", "", out var error);

        Assert.Null(error);
        Assert.Equal(DocsBuildKind.Tag, info!.Kind);
        Assert.Equal("", info.PublishedOn);
    }

    [Fact] // ADR-0110: on demand from main, the site states the branch and the commit, and links at the commit
    public void A_main_build_states_its_branch_and_commit()
    {
        var info = DocsBuildInfo.Parse("dev", "main@" + Sha, "", out var error);

        Assert.Null(error);
        Assert.Equal(new DocsBuildInfo(DocsBuildKind.Branch, "dev", "main", Sha, ""), info);
        Assert.Equal(Sha, info!.GitRef);
    }

    [Theory] // ADR-0110: a lab/* site is an experiment
    [InlineData("lab/wsl-scroll@abc1234", "lab/wsl-scroll", DocsBuildKind.Experiment)]
    [InlineData("lab/a/b@abc1234", "lab/a/b", DocsBuildKind.Experiment)]
    [InlineData("laboratory@abc1234", "laboratory", DocsBuildKind.Branch)]
    [InlineData("feature/lab/x@abc1234", "feature/lab/x", DocsBuildKind.Branch)]
    public void Only_a_lab_branch_is_an_experiment(string docsRef, string branch, DocsBuildKind kind)
    {
        var info = DocsBuildInfo.Parse("", docsRef, "", out var error);

        Assert.Null(error);
        Assert.Equal(kind, info!.Kind);
        Assert.Equal(branch, info.Branch);
        Assert.Equal("abc1234", info.Commit);
    }

    [Theory] // ADR-0110: inputs that disagree are refused, each with what to pass instead
    [InlineData("", "v0.1.0-beta.3", "", "states that tag's version: pass -p:DocsVersion=0.1.0-beta.3")]
    [InlineData("0.1.0-beta.2", "v0.1.0-beta.3", "", "states that tag's version: pass -p:DocsVersion=0.1.0-beta.3")]
    [InlineData("0.1.0-beta.3", "main@" + Sha, "", "never a version")]
    [InlineData("", "main@" + Sha, "2026-10-12", "leave DocsPublishedOn out")]
    [InlineData("", "", "2026-10-12", "Pass the tag as -p:DocsRef too")]
    [InlineData("0.1.0-beta.3", "v0.1.0-beta.3", "12/10/2026", "is not a day")]
    [InlineData("0.1.0-beta.3", "v0.1.0-beta.3", "2026-02-30", "is not a day")]
    [InlineData("latest", "", "", "is not a version")]
    [InlineData("", "vnext", "", "is not a version's tag")]
    [InlineData("", "main", "", "neither a tag")]
    [InlineData("", "main@", "", "neither a tag")]
    [InlineData("", "main@XYZ1234", "", "neither a tag")]
    [InlineData("", "main@ABCDEF1", "", "neither a tag")]
    [InlineData("", "main@abc12", "", "neither a tag")]
    [InlineData("", "lab/a b@abc1234", "", "neither a tag")]
    [InlineData("", "lab/a;b@abc1234", "", "neither a tag")]
    public void Inputs_that_disagree_are_refused(string version, string docsRef, string publishedOn, string expected)
    {
        var info = DocsBuildInfo.Parse(version, docsRef, publishedOn, out var error);

        Assert.Null(info);
        Assert.NotNull(error);
        Assert.Contains(expected, error);
    }
}
