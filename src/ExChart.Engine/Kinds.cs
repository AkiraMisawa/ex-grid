namespace ExChart.Engine;

/// <summary>What a series' numbers are, which decides how it is drawn (ADR-0253). Declared, never defaulted.</summary>
public enum SeriesKind
{
    /// <summary>Not declared: a series in this state is refused.</summary>
    Undeclared = 0,

    /// <summary>A quantity at an instant: points, joined only to their neighbours in the data.</summary>
    Level,

    /// <summary>A rate constant over an interval: a step with a filled end where it takes the value and an open end where it does not.</summary>
    RateOverInterval,

    /// <summary>A total over its interval: a step whose height is the amount per year, from zero.</summary>
    AmountPerPeriod,

    /// <summary>
    /// One thing measured on two quantities, such as a name's leverage and spread: a marker, never
    /// joined, its points neither ordered nor unique in x (ADR-0290).
    /// </summary>
    Observation,

    /// <summary>
    /// One period's open, high, low and close, and optionally its volume: a candle, never a line, its
    /// axis not from zero (ADR-0300).
    /// </summary>
    PriceRange,
}

/// <summary>Which interval a step's point stands for.</summary>
public enum StepSide
{
    /// <summary>(tᵢ₋₁, tᵢ]: the value holds up to and at its own point.</summary>
    Preceding,

    /// <summary>[tᵢ, tᵢ₊₁): the value holds from its own point to the next.</summary>
    Following,
}

/// <summary>The vertical axis a series reads on.</summary>
public enum AxisSide
{
    /// <summary>The left axis.</summary>
    Left,

    /// <summary>The right axis.</summary>
    Right,
}

/// <summary>How an axis places its positions (ADR-0251); a vertical axis is proportional or logarithmic (ADR-0290).</summary>
public enum AxisScale
{
    /// <summary>Proportional to the positions: dates by their day, numbers by their value.</summary>
    Proportional,

    /// <summary>Evenly spaced, in the declared order.</summary>
    Categories,

    /// <summary>Proportional to the logarithm of the positions, which must all be above zero (ADR-0290).</summary>
    Logarithmic,
}

/// <summary>What the horizontal positions are.</summary>
public enum AxisValues
{
    /// <summary>Dates, each position a <see cref="DateOnly.DayNumber"/>.</summary>
    Dates,

    /// <summary>Numbers, such as tenor years declared per column.</summary>
    Numbers,

    /// <summary>Labels, each position its index in the declared order.</summary>
    Labels,
}

/// <summary>
/// The calendar period candles merge into past the plot's pixels (ADR-0302): each is the market's own,
/// so a merged candle is the week's or the month's bar, never an arbitrary run of rows.
/// </summary>
public enum CandlePeriod
{
    /// <summary>One candle a row, as handed in.</summary>
    Row,

    /// <summary>A week, Monday to Sunday.</summary>
    Week,

    /// <summary>A calendar month.</summary>
    Month,

    /// <summary>A calendar quarter.</summary>
    Quarter,

    /// <summary>A calendar year.</summary>
    Year,
}

/// <summary>How a series' Interval is drawn (ADR-0303): the data is the same either way.</summary>
public enum IntervalStyle
{
    /// <summary>A vertical whisker with a cap at each bound, at each point.</summary>
    Whiskers,

    /// <summary>A band between the lower and the upper bounds, joining only neighbours.</summary>
    Band,
}
