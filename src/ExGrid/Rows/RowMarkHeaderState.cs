namespace ExGrid.Rows;

/// <summary>What the Mark Column's header checkbox shows (ADR-0043).</summary>
public enum RowMarkHeaderState
{
    /// <summary>No row of the current result is marked.</summary>
    None = 0,

    /// <summary>Some, but not all, of the current result's rows are marked.</summary>
    Some,

    /// <summary>Every Detail row of the current result is marked.</summary>
    All,
}
