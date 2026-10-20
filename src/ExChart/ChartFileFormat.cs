namespace ExChart;

/// <summary>A file a chart saves on a click (ADR-0252, ADR-0282).</summary>
public enum ChartFileFormat
{
    /// <summary>The visible series in the range shown, one row a position, as "View as table" holds them.</summary>
    Csv,

    /// <summary>The drawing, with its stylesheet inside it.</summary>
    Svg,

    /// <summary>The drawing painted by the browser, in the screen's fonts, at the screen's Device Pixel.</summary>
    Png,
}
