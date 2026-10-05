using ExGrid.Data;

namespace ExGrid.Summarizing;

/// <summary>
/// A Selection Summary's answer (ADR-0130): each figure asked for, or a decline with its reason —
/// never a partial figure. A figure that is not a number (no number among the cells, or an error
/// among them) is shown nowhere; Count still is.
/// </summary>
public sealed class GridSummaryResult
{
    private readonly Dictionary<SummaryFigures, AggregateResult> _figures;

    private GridSummaryResult(Dictionary<SummaryFigures, AggregateResult> figures, string? declined)
    {
        _figures = figures;
        DeclineReason = declined;
    }

    /// <summary>The answerer will not summarise this selection — a million rows on a server, say.
    /// The reason is shown in place of the figures.</summary>
    public static GridSummaryResult Declined(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new([], reason);
    }

    /// <summary>The figures answered, each a single figure.</summary>
    public static GridSummaryResult Answered(IReadOnlyDictionary<SummaryFigures, AggregateResult> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        var copy = new Dictionary<SummaryFigures, AggregateResult>();
        foreach (var (figure, result) in figures)
        {
            if (!SummaryFigureOrder.Each.Contains(figure))
                throw new ArgumentException($"'{figure}' is not a single figure (ADR-0130).", nameof(figures));
            copy[figure] = result;
        }
        return new(copy, null);
    }

    /// <summary>Whether the answerer declined.</summary>
    public bool IsDeclined => DeclineReason is not null;

    /// <summary>Why the answerer declined, or null.</summary>
    public string? DeclineReason { get; }

    /// <summary>The figures answered.</summary>
    public SummaryFigures Figures
    {
        get
        {
            var figures = SummaryFigures.None;
            foreach (var figure in _figures.Keys)
                figures |= figure;
            return figures;
        }
    }

    /// <summary>A figure's answer, or null where it was not answered.</summary>
    public AggregateResult? this[SummaryFigures figure] => _figures.TryGetValue(figure, out var result) ? result : null;
}
