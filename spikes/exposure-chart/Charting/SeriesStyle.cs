namespace ExposureChart.Charting;

/// <summary>How a series' points are joined.</summary>
public enum SeriesDraw
{
    /// <summary>Straight segments between the points: a value between two dates is implied.</summary>
    Line,

    /// <summary>Straight segments, and a marker on every point the data holds.</summary>
    LinePoints,

    /// <summary>Markers only: nothing is implied between the dates.</summary>
    Points,

    /// <summary>
    /// Piecewise constant, each value holding over the interval that ends at its date,
    /// (tᵢ₋₁, tᵢ] — a profile integrated bucket by bucket, as a CVA is. The value is taken at
    /// tᵢ (a filled marker) and not at tᵢ₋₁ (an open one).
    /// </summary>
    StepPreceding,

    /// <summary>Piecewise constant over the interval that starts at its date, [tᵢ, tᵢ₊₁).</summary>
    StepFollowing,
}

/// <summary>The marker drawn on a point.</summary>
public enum MarkerShape { Circle, Square, Diamond, Triangle }

public sealed record SeriesStyle(SeriesDraw Draw, MarkerShape Marker)
{
    public static SeriesStyle Default { get; } = new(SeriesDraw.Line, MarkerShape.Circle);

    public bool IsStep => Draw is SeriesDraw.StepPreceding or SeriesDraw.StepFollowing;
}

/// <summary>Where the page's legend stands beside the chart.</summary>
public enum LegendPosition { Top, Right, Bottom, None }

/// <summary>A list of every series, or a table of rows by colour and columns by drawing.</summary>
public enum LegendLayout { Auto, List, Table }

public sealed record LegendStyle(LegendPosition Position, LegendLayout Layout)
{
    public static LegendStyle Default { get; } = new(LegendPosition.Top, LegendLayout.Auto);
}
