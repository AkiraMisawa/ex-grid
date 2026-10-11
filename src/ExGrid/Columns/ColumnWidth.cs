using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExGrid.Columns;

/// <summary>
/// A column's declared width: Auto (computed from content) or a Fixed number of pixels
/// (ADR-0016). The zero state IS Auto, so an unassigned width means the sensible
/// default rather than an invalid value.
///
/// <para>A width is View State, which a Consumer persists (<c>CONTEXT.md</c>), so it has a JSON
/// form: <c>System.Text.Json</c> writes Auto as the string <c>"Auto"</c> — the intent, never the
/// width an Auto column was painted at — and a Fixed width as its number of pixels, and reads
/// back only those two, refusing anything else with a <see cref="JsonException"/> (ST-4). The
/// converter is internal, so only reflection-based serialisation reaches it: a Consumer's
/// <c>JsonSerializerContext</c> cannot (SYSLIB1220). Which form a width is kept in, and how a
/// source-generated context reaches it, wait on a decision no ADR records yet.</para>
/// </summary>
[JsonConverter(typeof(ColumnWidthJsonConverter))]
public readonly record struct ColumnWidth
{
    private readonly double _px; // 0 is Auto; Fixed is always positive

    private ColumnWidth(double px) => _px = px;

    /// <summary>Computed from content, and never narrowing on its own (ADR-0016). The
    /// zero state.</summary>
    public static ColumnWidth Auto => default;

    /// <summary>A fixed number of pixels, finite and positive — declared, or the user's
    /// intent after a drag or Size to fit (ADR-0016).</summary>
    public static ColumnWidth Fixed(double px)
    {
        if (!double.IsFinite(px) || px <= 0)
            throw new ArgumentOutOfRangeException(nameof(px), px,
                "A Fixed width is a finite, positive number of pixels.");
        return new(px);
    }

    /// <summary>Whether this is an Auto width.</summary>
    public bool IsAuto => _px == 0;

    /// <summary>The Fixed width in pixels. Throws when Auto: an Auto width is resolved
    /// through its <see cref="AutoWidth"/> tracker, never read from here (ADR-0016).</summary>
    public double FixedPx => IsAuto
        ? throw new InvalidOperationException(
            "An Auto width has no fixed pixel value; resolve it through its AutoWidth tracker (ADR-0016).")
        : _px;

    /// <summary><c>Auto</c> or <c>Fixed(120px)</c>. Written by hand: the record's own
    /// would read <see cref="FixedPx"/>, which throws for Auto, so printing a width for a
    /// message or a log would throw instead.</summary>
    public override string ToString()
        => IsAuto ? "Auto" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Fixed({_px}px)");
}

/// <summary>
/// <see cref="ColumnWidth"/>'s JSON form (ST-4): <c>"Auto"</c>, or a Fixed width's pixels as a
/// number. Written by hand because the struct keeps its state private: the default contract
/// read <see cref="ColumnWidth.FixedPx"/>, which throws for Auto, and wrote nothing back into a
/// struct with no setters, so a stored Fixed width came back Auto without a word.
/// </summary>
internal sealed class ColumnWidthJsonConverter : JsonConverter<ColumnWidth>
{
    private const string AutoText = "Auto";

    public override ColumnWidth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(AutoText))
            return ColumnWidth.Auto;
        // What ColumnWidth.Fixed would refuse is refused here, as the reader's own exception, so
        // a stored setting that cannot be a width says so where it is read (ADR-0016).
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var px) && double.IsFinite(px) && px > 0)
            return ColumnWidth.Fixed(px);
        throw new JsonException(
            $"A ColumnWidth is \"{AutoText}\" or a Fixed width's pixels, a finite, positive number (ADR-0016).");
    }

    public override void Write(Utf8JsonWriter writer, ColumnWidth value, JsonSerializerOptions options)
    {
        if (value.IsAuto)
            writer.WriteStringValue(AutoText);
        else
            writer.WriteNumberValue(value.FixedPx);
    }
}
