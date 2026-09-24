namespace ExGrid.Components;

/// <summary>
/// Both scroll offsets as one value, because they must be read as one.
///
/// A trackball or a trackpad moves both axes at once. Reading them in two calls means
/// awaiting twice, and the browser is free to process the next scroll event in between:
/// the rows would then be painted from the offset at t0 and the columns from the offset
/// at t1, so what is on screen belongs to no single moment. One call is one snapshot
/// (ADR-0021 permits reading the offsets; it does not say how many round-trips to take,
/// and a Blazor Server Consumer pays for each one).
/// </summary>
/// <param name="Top">The scroller's <c>scrollTop</c>.</param>
/// <param name="Left">The scroller's <c>scrollLeft</c>.</param>
public readonly record struct ScrollOffset(double Top, double Left);
