using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

public class ColumnWidthTests
{
    [Fact] // ADR-0016: a Fixed width is a finite, positive number of pixels
    public void Fixed_refuses_non_positive_and_non_finite_pixels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidth.Fixed(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidth.Fixed(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidth.Fixed(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidth.Fixed(double.PositiveInfinity));

        Assert.Equal(120d, ColumnWidth.Fixed(120).FixedPx);
    }

    [Fact] // ADR-0016: the zero state is Auto, and Auto has no fixed pixel value to read
    public void Default_is_auto_and_reading_its_fixed_pixels_throws()
    {
        Assert.True(default(ColumnWidth).IsAuto);
        Assert.True(ColumnWidth.Auto.IsAuto);
        Assert.Throws<InvalidOperationException>(() => ColumnWidth.Auto.FixedPx);
    }

    [Fact] // ADR-0016: the spec's bounds must be coherent
    public void Spec_refuses_incoherent_bounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: 100, maxWidthPx: 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnWidthSpec(ColumnWidth.Auto, maxWidthPx: double.PositiveInfinity));
    }

    [Fact] // ADR-0016 / FN-12: a Fixed width below its own MinWidth is refused, not clamped
    public void A_fixed_width_below_min_width_is_refused_not_clamped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ColumnWidthSpec(ColumnWidth.Fixed(10), minWidthPx: 40, maxWidthPx: 400));

        var valid = new ColumnWidthSpec(ColumnWidth.Fixed(120));
        Assert.Equal(120d, valid.Width.FixedPx);
    }

    [Fact] // ADR-0016 / FN-12: MaxWidth bounds what the grid computes, not what the user asked for
    public void A_fixed_width_above_max_width_is_the_users_and_is_kept()
    {
        // A drag past MaxWidth, recorded by the Consumer as it came, and read back.
        var dragged = new ColumnWidthSpec(ColumnWidth.Fixed(500), minWidthPx: 40, maxWidthPx: 400);

        Assert.Equal(500d, dragged.Width.FixedPx);
        Assert.Equal(500d, dragged.ResolveWidthPx(new AutoWidth(dragged).Observe(900)));
        Assert.Equal(400d, dragged.MaxWidthPx);

        // What the grid computes on its own stays bounded: Size to fit still stops at MaxWidth.
        Assert.Equal(ColumnWidth.Fixed(400), dragged.SizeToFit(900));
    }

    [Fact] // ADR-0016: the default bounds — MaxWidth must exist for #### to be possible
    public void Default_bounds_are_forty_and_four_hundred()
    {
        var spec = new ColumnWidthSpec(ColumnWidth.Auto);

        Assert.Equal(40d, spec.MinWidthPx);
        Assert.Equal(400d, spec.MaxWidthPx);
        Assert.Equal(ColumnWidthSpec.DefaultMinWidthPx, spec.MinWidthPx);
        Assert.Equal(ColumnWidthSpec.DefaultMaxWidthPx, spec.MaxWidthPx);
    }

    [Fact] // ADR-0016: size-to-fit fixes the width at the content of that moment, clamped
    public void Size_to_fit_yields_a_clamped_fixed_width()
    {
        var spec = new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: 40, maxWidthPx: 400);

        Assert.Equal(ColumnWidth.Fixed(40), spec.SizeToFit(10));    // below Min
        Assert.Equal(ColumnWidth.Fixed(400), spec.SizeToFit(900));  // above Max
        Assert.Equal(ColumnWidth.Fixed(150), spec.SizeToFit(150));  // in between
        Assert.False(spec.SizeToFit(150).IsAuto);                   // always Fixed, even from Auto
        Assert.Throws<ArgumentOutOfRangeException>(() => spec.SizeToFit(double.NaN));
    }

    [Fact] // ADR-0016: the Auto-or-Fixed branch lives in one place
    public void Resolve_uses_the_tracker_for_auto_and_ignores_it_for_fixed()
    {
        var auto = new ColumnWidthSpec(ColumnWidth.Auto);
        var tracker = new AutoWidth(auto).Observe(180);
        Assert.Equal(180d, auto.ResolveWidthPx(tracker));

        var pinned = new ColumnWidthSpec(ColumnWidth.Fixed(100));
        Assert.Equal(100d, pinned.ResolveWidthPx(tracker));
    }
}
