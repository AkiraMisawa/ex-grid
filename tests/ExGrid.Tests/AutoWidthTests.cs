using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>Transcribes the grow-only diagram of ADR-0016: initial from the first
/// Window, a longer value widens up to MaxWidth, a screen of short values does not
/// narrow.</summary>
public class AutoWidthTests
{
    private static readonly ColumnWidthSpec Spec = new(ColumnWidth.Auto, minWidthPx: 40, maxWidthPx: 400);

    [Fact] // ADR-0016: before any Window the column stands at MinWidth
    public void Before_any_observation_the_tracker_stands_at_min_width()
    {
        Assert.Equal(40d, new AutoWidth(Spec).CurrentPx);
    }

    [Fact] // ADR-0016: "initially: computed from the first Window" — observations grow the floor
    public void Observing_wider_content_grows_to_it()
    {
        var tracker = new AutoWidth(Spec).Observe(70).Observe(120);

        Assert.Equal(120d, tracker.CurrentPx);
    }

    [Fact] // ADR-0016: "a longer value appears → widen, up to MaxWidth"
    public void An_observation_past_max_width_clamps_at_max()
    {
        Assert.Equal(400d, new AutoWidth(Spec).Observe(900).CurrentPx);
    }

    [Fact] // ADR-0016: "a screen with only short values → DO NOT narrow" — no judder
    public void Shorter_observations_never_narrow_the_width()
    {
        var grown = new AutoWidth(Spec).Observe(200);

        Assert.Equal(200d, grown.Observe(50).Observe(120).Observe(0).CurrentPx);
    }

    [Fact] // ADR-0016: grow-only means the same observations settle at the same width in any order
    public void The_same_observations_settle_identically_in_any_order()
    {
        var forward = new AutoWidth(Spec).Observe(50).Observe(120).Observe(80);
        var backward = new AutoWidth(Spec).Observe(80).Observe(120).Observe(50);
        var sorted = new AutoWidth(Spec).Observe(120).Observe(80).Observe(50);

        Assert.Equal(forward.CurrentPx, backward.CurrentPx);
        Assert.Equal(forward.CurrentPx, sorted.CurrentPx);
        Assert.Equal(120d, forward.CurrentPx);
    }

    [Fact] // ADR-0016: a nonsense observation is a caller bug, not a clamp
    public void A_non_finite_or_negative_observation_throws()
    {
        var tracker = new AutoWidth(Spec);

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Observe(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Observe(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Observe(-1));
    }

    [Fact] // ADR-0016: size-to-fit is the only narrowing — an explicit user act on the spec
    public void Only_size_to_fit_narrows_a_grown_column()
    {
        var grown = new AutoWidth(Spec).Observe(200);

        var refitted = Spec.SizeToFit(90);

        Assert.Equal(200d, grown.CurrentPx);          // the tracker itself never narrowed
        Assert.Equal(90d, refitted.FixedPx);          // the user's size-to-fit did, as Fixed
        Assert.True(refitted.FixedPx < grown.CurrentPx);
    }
}
