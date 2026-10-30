namespace ExGrid;

/// <summary>
/// One axis of the Viewport: a declared number of pixels, or <see cref="Stretch"/> — the
/// Consumer sizes the box in CSS however it likes and the browser's report <em>is</em>
/// the size (ADR-0028). A type rather than a flag, so the contradiction
/// "<c>ViewportHeight=480 FillViewport=true</c>" cannot be written at all — the same
/// reason Source-plus-Window is refused by name (ADR-0001).
///
/// <para>The implicit conversion keeps <c>ViewportHeight="480"</c> compiling unchanged.
/// A declared size is validated as a number a Consumer wrote; under Stretch a reported
/// zero is a hidden tab, not a bug — the refusal keys on which of the two it was.</para>
/// </summary>
public readonly record struct ViewportSize
{
    private readonly double _px;
    private readonly bool _stretch;

    private ViewportSize(double px, bool stretch)
    {
        _px = px;
        _stretch = stretch;
    }

    /// <summary>The element's size is whatever the CSS makes it, observed rather than
    /// asserted. The arithmetic still never reads the DOM; it is told (ADR-0028).</summary>
    public static ViewportSize Stretch { get; } = new(0, stretch: true);

    /// <summary>A declared size in pixels — what keeps <c>ViewportHeight="480"</c>
    /// compiling. It is validated where the grid uses it, as a number the Consumer
    /// wrote.</summary>
    public static implicit operator ViewportSize(double px) => new(px, stretch: false);

    /// <summary>Whether this axis is <see cref="Stretch"/>: sized by the CSS and taken from
    /// the browser's report rather than declared.</summary>
    public bool IsStretch => _stretch;

    /// <summary>The declared pixels. Only meaningful when not <see cref="IsStretch"/> —
    /// reading it under Stretch is a caller bug, refused rather than answered with 0.</summary>
    public double Px => _stretch
        ? throw new InvalidOperationException("A Stretch Viewport has no declared size; the browser's report is the size (ADR-0028).")
        : _px;

    /// <summary><c>Stretch</c>, or the declared size written as <c>480px</c>, culture-invariantly.</summary>
    public override string ToString() => _stretch ? "Stretch" : FormattableString.Invariant($"{_px}px");
}
