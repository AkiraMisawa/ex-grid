namespace ExGrid.Chrome;

/// <summary>What a value list's "(Select All)" shows over the values the list shows now
/// (ADR-0009): the three states of Excel's own.</summary>
public enum SelectAllState
{
    /// <summary>Every value shown is chosen.</summary>
    Checked,

    /// <summary>No value shown is chosen.</summary>
    Unchecked,

    /// <summary>Some values shown are chosen and some are not.</summary>
    Mixed,
}
