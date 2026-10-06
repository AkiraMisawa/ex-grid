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
    private readonly Dictionary<SummaryFigures, string> _texts;

    private GridSummaryResult(Dictionary<SummaryFigures, AggregateResult> figures, Dictionary<SummaryFigures, string> texts, string? declined)
    {
        _figures = figures;
        _texts = texts;
        DeclineReason = declined;
    }

    /// <summary>The answerer will not summarise this selection — a million rows on a server, say.
    /// The reason is shown in place of the figures.</summary>
    public static GridSummaryResult Declined(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new([], [], reason);
    }

    /// <summary>The figures answered, each a single figure, and — where the answerer formats cells
    /// itself — the text each is shown with, in the format of the request's Focus. A figure with no
    /// text is formatted by the grid.</summary>
    public static GridSummaryResult Answered(
        IReadOnlyDictionary<SummaryFigures, AggregateResult> figures, IReadOnlyDictionary<SummaryFigures, string>? texts = null)
    {
        ArgumentNullException.ThrowIfNull(figures);
        var copy = new Dictionary<SummaryFigures, AggregateResult>();
        foreach (var (figure, result) in figures)
        {
            if (!SummaryFigureOrder.Each.Contains(figure))
                throw new ArgumentException($"'{figure}' is not a single figure (ADR-0130).", nameof(figures));
            copy[figure] = result;
        }
        var shown = new Dictionary<SummaryFigures, string>();
        foreach (var (figure, text) in texts ?? new Dictionary<SummaryFigures, string>())
        {
            if (!copy.ContainsKey(figure))
                throw new ArgumentException($"'{figure}' has a text and no answer (ADR-0130).", nameof(texts));
            shown[figure] = text;
        }
        return new(copy, shown, null);
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

    /// <summary>The text the answerer gave a figure, or null for the grid to format it.</summary>
    public string? TextOf(SummaryFigures figure) => _texts.TryGetValue(figure, out var text) ? text : null;

    /// <summary>A figure's answer, or null where it was not answered.</summary>
    public AggregateResult? this[SummaryFigures figure] => _figures.TryGetValue(figure, out var result) ? result : null;
}
