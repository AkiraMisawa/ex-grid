namespace ExGrid.Columns;

/// <summary>
/// The Overflow decision (ADR-0016): a value that does not fit is cut with an ellipsis
/// when Text, and becomes <c>####</c> when Number or Date — a truncated number looks
/// like a different, perfectly valid number. The decision is total over every column
/// type so no consumer re-derives the classification: Text and Boolean always answer
/// "show the value" (their ellipsis is CSS presentation, not a per-cell decision), and
/// that they never hash is a rule of the core.
///
/// Formatting is not this rule's business: it receives the already-formatted string;
/// default formats and culture live elsewhere.
/// </summary>
public static class OverflowRules
{
    public static OverflowDecision Decide(
        ColumnType type,
        string formattedText,
        double resolvedWidthPx,
        CellTextMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(formattedText);
        if (!double.IsFinite(resolvedWidthPx) || resolvedWidthPx < 0)
            throw new ArgumentOutOfRangeException(nameof(resolvedWidthPx), resolvedWidthPx,
                "A resolved width is a finite, non-negative number of pixels.");
        // default(CellTextMetrics) sidesteps the constructor's validation; refuse it by
        // name rather than let a zero digit width make everything quietly fit.
        if (metrics.DigitWidthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(metrics), metrics,
                "default(CellTextMetrics) is not valid; construct it with the theme's real metrics.");

        switch (type)
        {
            case ColumnType.Text:
            case ColumnType.Boolean:
                // A truncated "fal…" is visibly truncated — the quietly-wrong danger
                // motivating #### is absent — and proportional letters cannot be
                // estimated by the tabular-digit model anyway (ADR-0016).
                return OverflowDecision.ShowValue(formattedText);

            case ColumnType.Number:
            case ColumnType.Date:
                if (metrics.EstimatePx(formattedText.Length) <= resolvedWidthPx)
                    return OverflowDecision.ShowValue(formattedText); // exactly fitting still shows
                var hashCount = Math.Max(1, (int)Math.Floor(
                    metrics.ContentWidthPx(resolvedWidthPx) / metrics.DigitWidthPx));
                return OverflowDecision.Hashes(new string('#', hashCount));

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
