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

/// <summary>How the horizontal axis places its positions (ADR-0251).</summary>
public enum AxisScale
{
    /// <summary>Proportional to the positions: dates by their day, numbers by their value.</summary>
    Proportional,

    /// <summary>Evenly spaced, in the declared order.</summary>
    Categories,
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
