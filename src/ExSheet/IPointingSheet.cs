using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What a Pointing Scope asks of a Sheet in it (ADR-0058): the Sheet's own declarations, to read a
/// table's columns and key from, and its edit, to write into. The ExSheet component is the one
/// implementation; the Scope never reads another instance's data (ADR-0049, rule 1).
/// </summary>
internal interface IPointingSheet
{
    /// <summary>The Linked Tables the Sheet declares, keys included.</summary>
    IReadOnlyList<LinkedTable> LinkedTables { get; }

    /// <summary>The id of the root of the Sheet's grid, which a registered grid names while the Sheet
    /// points, so that a press on it keeps its place among the Sheet's keys (ADR-0058, "On a
    /// circuit"); null before the grid is rendered.</summary>
    string? RootId { get; }

    /// <summary>Writes text into the Sheet's open edit where Point writes; false when it is no longer
    /// in Point.</summary>
    Task<bool> WritePointedTextAsync(string text);

    /// <summary>Takes back what the last write put there, while it stands.</summary>
    Task<bool> TakeBackPointedTextAsync();

    /// <summary>Tells the Sheet's Consumer why a press wrote nothing.</summary>
    Task TellPointingRefusedAsync(PointingRefusal refusal);
}
