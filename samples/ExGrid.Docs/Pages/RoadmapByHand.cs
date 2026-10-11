namespace ExGrid.Docs;

/// <summary>
/// The Roadmap's rows written by hand (ADR-0191): what the next stable version requires, the records the
/// page links rather than copies, and what the family will not do beyond the scope tables' refused rows.
/// Every row cites the record that requires or refused it. The build reads this class and refuses a row
/// whose text holds a date, a quarter or a version, whose product is not one of the four, or whose
/// citation leads nowhere. A row says only what its record says: change the record first.
/// </summary>
internal static class RoadmapByHand
{
    /// <summary>
    /// What the next version without a suffix requires (ADR-0042). Kept to what the records already
    /// require; what a stable version holds is decided in the change that rewrites ADR-0042.
    /// </summary>
    public static IReadOnlyList<RoadmapNote> TowardStable { get; } =
    [
        new("The Definition of Done is signed off: every MUST passes, every SHOULD passes or is accepted failing with a written reason, and the run is recorded.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["DoD Step 7"]),
        new("The public C# surface is reviewed: a decision states which types are a Consumer's contract, the rest stops being public, and every later change to the surface shows in review.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0042"]),
        new("The release workflow stops refusing a version without a suffix, in the change that rewrites the decision on prereleases.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0042"]),
    ];

    /// <summary>The records the section links at the ref the site was built from, rather than copies.</summary>
    public static IReadOnlyList<RoadmapRecord> Linked { get; } =
    [
        new("What the Definition of Done still owes", "docs/implementation-status.md",
            "A snapshot of what each product has left to verify, kept beside the code."),
        new("Reserved with a named trigger", "DoD §21.11",
            "Questions a decision settles when the feature that triggers them is built. One with no trigger named there is not planned."),
    ];

    /// <summary>What the family will not do, each refused by a decision, beside the scope tables' refused rows.</summary>
    public static IReadOnlyList<RoadmapNote> NotPlanned { get; } =
    [
        new("Right-to-left layout, with columns flowing from the right. Right-to-left text in a cell is shown as Excel shows it on a left-to-right sheet.",
            ["ExGrid", "ExSheet", "ExPivot"], ["ADR-0031"]),
        new("Touch, pen and phones, the iPhone and the iPad among them. The grid is made for a keyboard and a mouse on a desktop, and follows its box as a window is resized.",
            ["ExGrid", "ExSheet", "ExPivot"], ["ADR-0045"]),
        new("Safari and Firefox. Chrome and Edge are the browsers the family is built for and verified on.",
            ["ExGrid", "ExSheet", "ExPivot"], ["ADR-0017"]),
        new("The grid sorting, filtering or grouping rows itself. The Consumer, or a bundled source such as GridSource.From, decides the rows and their order, and the grid shows them as given.",
            ["ExGrid"], ["ADR-0001"]),
        new("Merged cells. A selection is a rectangle of whole cells, and a merge is a cell that is not one.",
            ["ExSheet"], ["ADR-0046"]),
        new("Reading an Arrow timestamp in a zone other than UTC. Converting it would need a zone database and an answer to whose midnight it is, so it is refused, and a producer writes UTC or local clock values instead.",
            ["Data"], ["ADR-0065"]),
    ];
}
