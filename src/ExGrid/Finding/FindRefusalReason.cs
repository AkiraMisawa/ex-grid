namespace ExGrid.Finding;

/// <summary>
/// Why a Find step moved nothing (ADR-0055). A Refusal like any other: the grid holds no
/// sentence for it, so Chrome words it — into a live region (ADR-0033).
/// </summary>
public enum FindRefusalReason
{
    /// <summary>Ctrl+F on a grid where nothing can search: no Grid Source that finds and no
    /// <c>OnFind</c>. The key is still taken, because the browser's own find sees only the
    /// painted rows and would look complete without being so.</summary>
    Unavailable,

    /// <summary>Nothing matches anywhere the step looked.</summary>
    NotFound,

    /// <summary>The order moved while the question was out, so the answer names a position
    /// in an order that no longer stands, and is discarded (ADR-0011).</summary>
    OrderChanged,
}

/// <summary>What the last Find step came to, for the panel to show (ADR-0055).</summary>
public enum FindOutcome
{
    /// <summary>No step has been answered since the panel opened.</summary>
    None,

    /// <summary>The Focus moved to a match.</summary>
    Found,

    /// <summary>Nothing matched.</summary>
    NotFound,

    /// <summary>The order changed while the step was out; nothing moved.</summary>
    OrderChanged,
}
