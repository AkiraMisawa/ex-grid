namespace ExGrid.Release;

/// <summary>
/// The checks of what a tagged commit holds that only a version on the stable path needs (ADR-0205,
/// ADR-0208). A stable version runs all of them. A release candidate runs all but the two that read
/// a sign-off — the "Signed off" block, read in full, and the structural check — so that the stable
/// path is rehearsed before it is used. A plain prerelease runs none.
/// <para>A release candidate's notes carry a stable version's blocks, its Signed off block in a pending
/// form of its own that says the sign-off is being made against this candidate, and they are read by
/// form only, by the Docs Site's build, which refuses a candidate's block in the stable form and a
/// stable version's in the pending one. The sign-off is read in full only on a stable tag, whose commit
/// is the candidate's commit X plus only records and notes (ADR-0190 and ADR-0205, 2026-10-12).</para>
/// </summary>
public static class Gate
{
    /// <summary>
    /// Runs the checks <paramref name="version"/>'s kind needs against <paramref name="tagCommit"/>,
    /// saying every refusal. The rows checked by hand are judged against the signed-off commit X for a
    /// stable version, and against the tagged commit for a candidate, which has no sign-off; WR-10's records
    /// are held besides to the MudBlazor versions <paramref name="mudBlazor"/> gives as published before X's
    /// commit time, or with no date, and listed (2026-10-12).
    /// </summary>
    public static void Run(Git git, TagVersion version, string tagCommit, Report report, bool fetchMissing = false, IPublishedVersions? mudBlazor = null)
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
            report.Notice($"{version.Tag} is a release candidate: it runs a stable version's checks but the two that read a sign-off, the Signed off block and the structural check. Its notes' blocks are read by form, by the Docs Site's build, its Signed off block in the pending form that says the sign-off is being made against this candidate; the stable tag that follows is this commit plus only records and notes (ADR-0190, ADR-0205, 2026-10-12).");
        }

        Corpus.Check(tag, report);

        var target = signedOff ?? tagCommit;
        if (version.Kind == ReleaseKind.Stable && signedOff is null)
            report.Notice($"The notes name no signed-off commit this repository holds, so the rows checked by hand are judged against the tagged commit {tagCommit[..12]} instead.");
        ByHand.Check(git, tag, target, report, fetchMissing, mudBlazor);
    }
}
