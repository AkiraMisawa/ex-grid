namespace ExGrid.Release;

/// <summary>
/// The checks of what a tagged commit holds that only a version on the stable path needs (ADR-0205,
/// ADR-0208). A stable version runs all of them. A release candidate runs all but the two that read
/// a sign-off — the "Signed off" block and the structural check — so that the stable path is
/// rehearsed before it is used. A plain prerelease runs none.
/// </summary>
public static class Gate
{
    /// <summary>
    /// Runs the checks <paramref name="version"/>'s kind needs against <paramref name="tagCommit"/>,
    /// saying every refusal. The rows checked by hand are judged against the signed-off commit X for a
    /// stable version, and against the tagged commit for a candidate, which has no sign-off.
    /// </summary>
    public static void Run(Git git, TagVersion version, string tagCommit, Report report, bool fetchMissing = false)
    {
        if (!version.RehearsesStable)
        {
            report.Notice($"{version.Tag} is a prerelease: no sign-off, no corpus check and no rows checked by hand gate it (ADR-0042).");
            return;
        }

        var tag = new CommitTree(git, tagCommit);
        string? signedOff = null;
        if (version.Kind == ReleaseKind.Stable)
        {
            signedOff = SignOff.Check(git, tag, version, tagCommit, report);
        }
        else
        {
            report.Notice($"{version.Tag} is a release candidate: it runs a stable version's checks but the sign-off's, the Signed off block and the structural check (ADR-0205).");
        }

        Corpus.Check(tag, report);

        var target = signedOff ?? tagCommit;
        if (version.Kind == ReleaseKind.Stable && signedOff is null)
            report.Notice($"The notes name no signed-off commit this repository holds, so the rows checked by hand are judged against the tagged commit {tagCommit[..12]} instead.");
        ByHand.Check(git, tag, target, report, fetchMissing);
    }
}
