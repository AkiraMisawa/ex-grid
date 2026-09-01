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
    /// <summary>
    /// The one place the Number/Date-versus-Text/Boolean split lives. It answers both
    /// "does this type become <c>####</c> when it does not fit" and, for rendering,
    /// "is this a numeric presentation" (right-aligned, tabular digits) — the two are
    /// the same classification by construction (ADR-0016), and consumers call this
    /// instead of re-deriving it. An undefined ColumnType is refused, never quietly
    /// treated as text.
    /// </summary>
    public static bool HashesWhenOverflowing(ColumnType type) => type switch
    {
        ColumnType.Text or ColumnType.Boolean => false,
        ColumnType.Number or ColumnType.Date => true,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

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

        // A truncated "fal…" is visibly truncated — the quietly-wrong danger motivating
        // #### is absent — and proportional letters cannot be estimated by the
        // tabular-digit model anyway (ADR-0016).
        if (!HashesWhenOverflowing(type))
            return OverflowDecision.ShowValue(formattedText);

        if (metrics.EstimatePx(formattedText) <= resolvedWidthPx)
            return OverflowDecision.ShowValue(formattedText); // exactly fitting still shows
        var hashCount = Math.Max(1, (int)Math.Floor(
            metrics.ContentWidthPx(resolvedWidthPx) / metrics.DigitWidthPx));
        return OverflowDecision.Hashes(new string('#', hashCount));
    }
}
