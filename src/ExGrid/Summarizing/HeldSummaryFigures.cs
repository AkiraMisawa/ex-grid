namespace ExGrid.Summarizing;

/// <summary>
/// The figures a Selection Summary shows, held by a Consumer that draws a grid of its own — ExSheet,
/// ExPivot — so that the figures menu works without a binding of its Consumer's (ADR-0130). The
/// menu's choice is held from then on; a value handed in through a parameter replaces it, and the
/// same value handed again does not.
/// </summary>
public sealed class HeldSummaryFigures
{
    private SummaryFigures? _handed;

    /// <summary>The figures shown now: Excel's Average, Count and Sum until anything says otherwise.</summary>
    public SummaryFigures Shown { get; private set; } = SummaryFigures.Default;

    /// <summary>A value handed in through a parameter. Returns whether the figures shown changed.</summary>
    public bool Adopt(SummaryFigures handed)
    {
        if (_handed == handed)
            return false;
        _handed = handed;
        var changed = Shown != handed;
        Shown = handed;
        return changed;
    }

    /// <summary>The figures menu's choice.</summary>
    public void Choose(SummaryFigures figures) => Shown = figures;
}
