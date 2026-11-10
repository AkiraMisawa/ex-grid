namespace ExGrid;

/// <summary>
/// How a header lays out what stands beside its label (ADR-0500). It is chosen apart from
/// <see cref="GridDensity"/>: the rows' height and the cells' padding are the density's, and
/// neither word here is one of its presets.
/// </summary>
public enum GridHeaderDisplay
{
    /// <summary>The default, the header an untouched grid always drew: the ▾ in a band of its
    /// own, and the sort mark in a box of its own after the label.</summary>
    Standard = 0,

    /// <summary>One Header Box inside the cell's right padding for both: the ▾ while the
    /// column has neither a Sort nor a filter, and the mark of what it has while it has one.
    /// A header needs less width, and a press on the slot opens the column menu either
    /// way.</summary>
    Compact,
}
