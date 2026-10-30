namespace ExGrid.Summarizing;

/// <summary>Where a Selection Summary stands (ADR-0130).</summary>
public enum SelectionSummaryStatus
{
    /// <summary>Nothing to show: fewer than two cells are selected, no figure is shown, or nobody
    /// can answer.</summary>
    None = 0,

    /// <summary>The question for the current selection is out; no figure is shown meanwhile —
    /// never the previous selection's.</summary>
    Pending,

    /// <summary>The answer to the current selection's question stands.</summary>
    Answered,

    /// <summary>The answerer declined, with its reason; no figure is shown.</summary>
    Declined,
}

/// <summary>
/// A grid's Selection Summary as it stands (ADR-0130), for a Consumer that shows the figures
/// elsewhere — an application-wide status bar, which resolves which grid is meant (ADR-0018).
/// <see cref="Result"/> answers <see cref="Request"/>, and nothing else, whenever it is set.
/// </summary>
/// <param name="Status">Where it stands.</param>
/// <param name="Request">The question for the current selection; null when there is none.</param>
/// <param name="Result">The answer to <see cref="Request"/>, once <see cref="Status"/> is
/// Answered or Declined; null otherwise.</param>
public sealed record SelectionSummary(SelectionSummaryStatus Status, GridSummaryRequest? Request, GridSummaryResult? Result)
{
    /// <summary>Nothing to show.</summary>
    public static SelectionSummary None { get; } = new(SelectionSummaryStatus.None, null, null);
}
