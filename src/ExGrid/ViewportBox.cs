namespace ExGrid;

/// <summary>
/// What is actually visible inside the declared Viewport: the outer size the Consumer
/// asked for, minus the Scrollbar Gutter the browser took out of it.
///
/// A classic (non-overlay) scrollbar is drawn <em>inside</em> the box the element
/// declares — the element stays <c>ViewportWidth</c> wide and its <c>clientWidth</c>
/// shrinks by about 15px — while macOS's overlay scrollbars take nothing at all. Both
/// <see cref="ViewportGeometry"/> and <see cref="ColumnGeometry"/> were built on the
/// outer size, so on Windows and Linux the last row and the last column sat behind their
/// scrollbars: ADR-0012's "the Focus must always be visible" was broken on two platforms
/// out of three, and invisibly so on the third, where every measurement is 0
/// (ADR-0013).
///
/// The gutter cannot be assumed, computed, or measured once — it is 0 until content
/// overflows, it differs per platform and per user setting, and what it does under zoom
/// is not something this project has been able to measure. So it is <em>observed</em>:
/// the browser reports it whenever it changes (ADR-0021), and this type is where the
/// subtraction happens, once, for both axes.
///
/// <para><b>With a gutter of 0 the results are bit-for-bit what they were before this
/// type existed</b> — <c>x - 0</c> is exactly <c>x</c> for every finite positive
/// <c>x</c>, so no rounding enters on a platform with overlay scrollbars. The Layer 1
/// tests hold that invariant, because it is the whole basis for calling this a fix
/// rather than a change.</para>
/// </summary>
public readonly record struct ViewportBox
{
    /// <param name="outerWidthPx">The width the Consumer declared — <c>ViewportWidth</c>,
    /// which keeps meaning the element's outer size (ADR-0013).</param>
    /// <param name="outerHeightPx">The height the Consumer declared, header included.</param>
    /// <param name="gutterWidthPx">What a vertical scrollbar takes off the width. 0 on a
    /// platform with overlay scrollbars, and 0 before the content overflows anywhere.</param>
    /// <param name="gutterHeightPx">What a horizontal scrollbar takes off the height.</param>
    public ViewportBox(
        double outerWidthPx, double outerHeightPx, double gutterWidthPx, double gutterHeightPx)
    {
        Check(outerWidthPx, nameof(outerWidthPx), gutterWidthPx, nameof(gutterWidthPx), "wide", "vertical");
        Check(outerHeightPx, nameof(outerHeightPx), gutterHeightPx, nameof(gutterHeightPx), "tall", "horizontal");

        OuterWidthPx = outerWidthPx;
        OuterHeightPx = outerHeightPx;
        GutterWidthPx = gutterWidthPx;
        GutterHeightPx = gutterHeightPx;
    }

    /// <summary>A box with no scrollbar taken out of it — what every platform with
    /// overlay scrollbars reports, and what the grid assumes until the browser says
    /// otherwise.</summary>
    public static ViewportBox Declared(double outerWidthPx, double outerHeightPx)
        => new(outerWidthPx, outerHeightPx, 0, 0);

    public double OuterWidthPx { get; }

    public double OuterHeightPx { get; }

    /// <summary>How much of the width a vertical scrollbar occupies.</summary>
    public double GutterWidthPx { get; }

    /// <summary>How much of the height a horizontal scrollbar occupies.</summary>
    public double GutterHeightPx { get; }

    /// <summary>The width the columns actually have — what the browser calls
    /// <c>clientWidth</c>, arrived at without a layout read of our own.</summary>
    public double VisibleWidthPx => OuterWidthPx - GutterWidthPx;

    /// <summary>The height the header and the rows actually have.</summary>
    public double VisibleHeightPx => OuterHeightPx - GutterHeightPx;

    /// <summary>The same box with a different gutter — how an observation is applied,
    /// leaving the Consumer's declared size untouched.</summary>
    public ViewportBox WithGutter(double gutterWidthPx, double gutterHeightPx)
        => new(OuterWidthPx, OuterHeightPx, gutterWidthPx, gutterHeightPx);

    /// <summary>One axis alone, validated and subtracted — what a grid whose other axis
    /// is <c>Fill</c> needs (ADR-0028): the declared axis keeps its refusals while the
    /// filled one is an observation with none to make.</summary>
    public static double VisibleWidthOf(double outerWidthPx, double gutterWidthPx)
    {
        Check(outerWidthPx, nameof(outerWidthPx), gutterWidthPx, nameof(gutterWidthPx), "wide", "vertical");
        return outerWidthPx - gutterWidthPx;
    }

    /// <summary>The height half of <see cref="VisibleWidthOf"/>.</summary>
    public static double VisibleHeightOf(double outerHeightPx, double gutterHeightPx)
    {
        Check(outerHeightPx, nameof(outerHeightPx), gutterHeightPx, nameof(gutterHeightPx), "tall", "horizontal");
        return outerHeightPx - gutterHeightPx;
    }

    // Both axes are checked identically, and each refusal names the gutter rather than
    // the remainder: a Consumer told "ViewportWidth is not positive" about a width it
    // set to 12 would have nothing to go on, because the number it wrote is not the
    // number that went wrong.
    private static void Check(
        double outerPx, string outerName, double gutterPx, string gutterName, string adjective, string bar)
    {
        if (!double.IsFinite(outerPx) || outerPx <= 0)
        {
            throw new ArgumentOutOfRangeException(outerName, outerPx,
                $"The Viewport is a finite, positive number of pixels {adjective} (ADR-0013).");
        }
        if (!double.IsFinite(gutterPx) || gutterPx < 0)
        {
            throw new ArgumentOutOfRangeException(gutterName, gutterPx,
                $"A Scrollbar Gutter is a finite, non-negative number of pixels: the {bar} scrollbar either " +
                "takes a strip out of the declared box or takes nothing (ADR-0013).");
        }
        if (gutterPx >= outerPx)
        {
            throw new ArgumentOutOfRangeException(gutterName, gutterPx,
                $"The {bar} scrollbar takes {gutterPx}px out of a Viewport only {outerPx}px {adjective}, " +
                "leaving nothing to paint in. Declare a Viewport large enough to hold its own scrollbars " +
                "(ADR-0013).");
        }
    }
}
