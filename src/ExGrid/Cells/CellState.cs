namespace ExGrid.Cells;

/// <summary>
/// The generic vocabulary the grid understands for "this cell is in an unusual state"
/// (ADR-0006). The Consumer's own vocabulary — as-of stamps, scenario Overrides,
/// staleness policies — stays outside: it is mapped onto these five on the way in, and
/// whatever data accompanies it stays opaque and is rendered by the Consumer.
///
/// <para>Distinct from the two value-derived reasons a cell can look different. Format
/// and value-derived decoration (red when negative) are decided by looking at the value
/// and belong to the Column; a Cell State cannot be derived from the value at all and
/// can only be supplied from outside.</para>
///
/// <para><see cref="Normal"/> is deliberately the default: a cell nobody said anything
/// about is an ordinary cell, so an unanswered lookup and an explicit "normal" are the
/// same thing and neither needs a branch on the render path.</para>
///
/// <para>Adding a value later is easy; changing what one means is a breaking change.
/// These five are induced from two Consumers that arrived at the same shape
/// independently, and should be revisited when a third appears.</para>
/// </summary>
public enum CellState
{
    /// <summary>Nothing unusual. Painted exactly as an unannotated cell.</summary>
    Normal = 0,

    /// <summary>The value is real but no longer current — a close-of-business figure
    /// beside an intraday one. Degree is not expressed: how stale goes in the
    /// accompanying data, not here.</summary>
    Stale,

    /// <summary>No value could be obtained. Distinct from Blank, which is a value the
    /// Consumer really holds and which sorts and filters as Blank (ADR-0023).</summary>
    Missing,

    /// <summary>Obtaining or computing the value failed.</summary>
    Error,

    /// <summary>The value was overridden — by a user's edit through the Overlay
    /// (ADR-0007), or by a what-if scenario the Consumer holds.</summary>
    Modified,
}
