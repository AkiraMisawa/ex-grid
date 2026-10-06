namespace ExGrid.Cells;

/// <summary>Why the grid cannot identify an Action's original target (ADR-0154).</summary>
public enum ActionRefusalReason
{
    /// <summary>The original address is unavailable, its row has left the Window, or its command
    /// is no longer declared. This never means that the row's displayed values changed.</summary>
    RenderNoLongerKept,
}

/// <summary>An Action refused before reaching its Consumer (ADR-0154). Only detached addresses
/// are retained; no obsolete TRow is included. RowKey is the Consumer's supplied identity.</summary>
/// <param name="ColumnName">The original column name; null when its address is no longer held.</param>
/// <param name="ActionName">The original command; null when its address is no longer held.</param>
/// <param name="Reason">Why the original target cannot be resolved.</param>
/// <param name="RowKey">The original Consumer Row Key, when one was declared and is still known.</param>
/// <param name="RowIndex">The original absolute row position, when known.</param>
/// <param name="RowSequenceVersion">The order that position belonged to, when known.</param>
public readonly record struct GridActionRefusal(string? ColumnName, string? ActionName,
    ActionRefusalReason Reason, object? RowKey = null, int? RowIndex = null, int? RowSequenceVersion = null);
