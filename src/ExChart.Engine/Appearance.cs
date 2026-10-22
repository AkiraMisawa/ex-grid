namespace ExChart.Engine;

/// <summary>
/// The look a series' Appearance Keys chose (ADR-0256): slot numbers the stylesheet's Visual Tokens
/// say the look of, never a colour or a pattern.
/// </summary>
/// <param name="Colour">The colour slot, from 0.</param>
/// <param name="LineStyle">The line-style slot, from 0 (solid).</param>
/// <param name="Marker">The marker shape, from 0 (<see cref="MarkerShape.Circle"/>).</param>
public readonly record struct SeriesLook(int Colour, int LineStyle, int Marker)
{
    /// <summary>The marker's shape.</summary>
    public MarkerShape Shape => (MarkerShape)(Marker % Appearance.Markers);
}

/// <summary>The shapes a marker key chooses from, in order.</summary>
public enum MarkerShape
{
    /// <summary>A circle.</summary>
    Circle,

    /// <summary>A square.</summary>
    Square,

    /// <summary>A diamond.</summary>
    Diamond,

    /// <summary>A triangle, point up.</summary>
    Triangle,
}

/// <summary>
/// Chooses each series' look from its Appearance Keys (ADR-0256). A key takes the next slot the first
/// time any series of the definition names it, hidden series included, so the same key keeps the same
/// look when another series is hidden, filtered or added after it.
/// </summary>
public static class Appearance
{
    /// <summary>The line-style slots the stylesheet defines: solid, dashed, dotted, dash-dot.</summary>
    public const int LineStyles = 4;

    /// <summary>The marker shapes there are.</summary>
    public const int Markers = 4;

    /// <summary>The colour slots the stylesheet defines unless a theme declares more.</summary>
    public const int DefaultColourSlots = 10;

    /// <summary>
    /// Each series' look by its identity, and whether more colour keys than <paramref name="colourSlots"/>
    /// made colours repeat. A series that sets no line-style key is solid; one that sets no marker key
    /// takes a circle.
    /// </summary>
    public static (IReadOnlyDictionary<string, SeriesLook> Looks, bool ColoursRepeat) Of(ChartDefinition definition, int colourSlots = DefaultColourSlots)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfLessThan(colourSlots, 1);
        var colours = new Dictionary<string, int>();
        var lines = new Dictionary<string, int>();
        var markers = new Dictionary<string, int>();
        static int Slot(Dictionary<string, int> table, string key) =>
            table.TryGetValue(key, out var i) ? i : table[key] = table.Count;
        var looks = new Dictionary<string, SeriesLook>();
        foreach (var s in definition.Series)
        {
            var k = s.EffectiveKeys;
            looks[s.Id] = new SeriesLook(
                Slot(colours, k.Colour) % colourSlots,
                k.LineStyle is { } l ? Slot(lines, l) % LineStyles : 0,
                k.Marker is { } m ? Slot(markers, m) % Markers : 0);
        }
        return (looks, colours.Count > colourSlots);
    }
}

/// <summary>Where the legend sits (ADR-0271).</summary>
public enum LegendPosition
{
    /// <summary>Above each panel's plot.</summary>
    Top,

    /// <summary>Below it; under the horizontal axis for the last panel.</summary>
    Bottom,

    /// <summary>Left of the plots.</summary>
    Left,

    /// <summary>Right of the plots.</summary>
    Right,

    /// <summary>No legend.</summary>
    None,
}

/// <summary>How the legend is aligned along its side.</summary>
public enum LegendAlign
{
    /// <summary>At the start: left, or top.</summary>
    Start,

    /// <summary>Centred.</summary>
    Centre,

    /// <summary>At the end: right, or bottom.</summary>
    End,
}

/// <summary>The Consumer's placement of the legend: a side and an alignment, never a pixel offset (ADR-0271).</summary>
/// <param name="Position">The side.</param>
/// <param name="Align">The alignment along it.</param>
public sealed record LegendPlacement(LegendPosition Position = LegendPosition.Top, LegendAlign Align = LegendAlign.Start)
{
    /// <summary>Top, at the start.</summary>
    public static LegendPlacement Default { get; } = new();
}

/// <summary>One legend entry, laid out.</summary>
/// <param name="Id">The key a click hides it by: the series' identity, which for quotes and their Interpolated curve is the quotes' and hides both (ADR-0257).</param>
/// <param name="Name">Its name.</param>
/// <param name="Look">Its look, which hiding does not change.</param>
/// <param name="Hidden">Whether it is hidden, which fades the entry.</param>
/// <param name="Box">Where the entry sits, swatch first.</param>
public sealed record LegendEntry(string Id, string Name, SeriesLook Look, bool Hidden, Box Box);
