using Xunit;

namespace ExGrid.Tests;

/// <summary>The one-axis size (ADR-0028): a declared number, or Fill — and no way to
/// write both at once.</summary>
public class ViewportSizeTests
{
    [Fact] // ADR-0028: ViewportHeight="480" keeps compiling unchanged
    public void A_number_converts_implicitly()
    {
        ViewportSize size = 480;

        Assert.False(size.IsFill);
        Assert.Equal(480, size.Px);
    }

    [Fact] // ADR-0028: under Fill the report is the size; there is no declared one to read
    public void Fill_has_no_declared_pixels()
    {
        Assert.True(ViewportSize.Fill.IsFill);
        Assert.Throws<InvalidOperationException>(() => ViewportSize.Fill.Px);
    }

    [Fact] // Value semantics: two declarations of the same size are the same size
    public void Sizes_compare_by_value()
    {
        Assert.Equal((ViewportSize)480, (ViewportSize)480);
        Assert.NotEqual((ViewportSize)480, ViewportSize.Fill);
        Assert.Equal(ViewportSize.Fill, ViewportSize.Fill);
    }
}
