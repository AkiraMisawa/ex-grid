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
    /// What the next version without a suffix requires: the first stable version and what it carries
    /// (ADR-0200), its public surface (ADR-0201), what it holds of ExSheet and ExPivot and where it says what
    /// it does not do (ADR-0202), the gaps the Definition of Done found (ADR-0203, ADR-0204, ADR-0206,
    /// ADR-0207), and how it is published (ADR-0205). Kept to what those records require, without the version
    /// they name: the Roadmap carries none (ADR-0191).
    /// </summary>
    public static IReadOnlyList<RoadmapNote> TowardStable { get; } =
    [
        new("The Definition of Done is signed off: every MUST passes, every SHOULD passes or is accepted failing with a written reason, and the run is recorded.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["DoD Step 7"]),
        new("The whole family becomes stable together: all ten packages, at one version, from one tag. So every product's own criteria gate it, not only ExGrid's: those of ExGrid's MudBlazor Wrapper, of ExSheet, of ExPivot and of the Snapshot's packages. It promises that sign-off, not a frozen API; where a later minor version breaks one, its Release Notes say so.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0200", "DoD §2"]),
        new("ExGrid is declared to run on Blazor Server, by a decision written once every Blazor Server criterion passes.",
            ["ExGrid"], ["ADR-0200", "DoD §24"]),
        new("The public C# surface is reviewed: a type stays public when a Consumer, a Chrome or a sibling product uses it, or a public signature names it, and the rest becomes internal. A type a prerelease exported that becomes internal is named under the Release Notes' breaking changes, and from then on a baseline file makes every change to the surface show in review.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0201"]),
        new("ExSheet's decided functions are built: LET, RAND, RANDBETWEEN, SUBTOTAL, AGGREGATE, ERROR.TYPE and INDIRECT. Every Excel case its engine is checked against is put to Excel itself on Windows, and a function that disagrees is corrected, or refuses with an Error Value the arguments where it disagrees. F4's edge cases and the toolbar's keys are run there by hand.",
            ["ExSheet"], ["ADR-0202", "DoD §27"]),
        new("ExPivot's readings of Excel are run beside Excel. Where one that changes a number on screen disagrees, the decision is corrected first and the code after it; a difference in layout or wording is said as a known limitation.",
            ["ExPivot"], ["ADR-0202", "DoD §29"]),
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0202"]),
        new("A refusal is worded by default: the built-in Chrome says and announces why a paste or a copy was refused, and what to do next where something can be done, unless your application words it.",
            ["ExGrid"], ["ADR-0203"]),
        new("The page follows the cell a key moves to, the same way whether the cell is reached or edited, and a focus the grid takes a round trip after a gesture is taken only while the keyboard is still the grid's.",
            ["ExGrid"], ["ADR-0204"]),
        new("On a Sheet, a border stays as drawn under the Selection's shade in a pinned column too, as it does elsewhere.",
            ["ExSheet"], ["ADR-0206"]),
        new("A saved view, its sort, filters, widths, column order and pinned columns, is written to JSON and read back equal through an entry point of ExGrid's own, each filter value with its type.",
            ["ExGrid"], ["ADR-0207"]),
        new("The stable version is grown on a release branch and rehearsed by release candidates, published through the same checks a stable tag runs, their release notes included. The release workflow publishes a version without a suffix only when its notes name the sign-off and the commit it was made at, and the tagged commit adds nothing to that one but the records and the notes.",
            ["ExGrid", "ExSheet", "ExPivot", "Data"], ["ADR-0205"]),
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
