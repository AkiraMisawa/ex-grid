using System.Globalization;

namespace ExGrid;

/// <summary>
/// The text a value shows when its column declares no <c>Format</c> (ADR-0006, note of
/// 2026-10-01): a date or a time in one ISO form by its type, written in the invariant culture,
/// so the same value reads the same under every culture — on the Server host, the server's culture
/// is not the reader's, and a day and a month read the wrong way round are quietly wrong. A
/// <see cref="double"/> reads as Excel's General does, in at most 15 significant digits, so
/// 0.1 + 0.2 shows 0.3 and not the 0.30000000000000004 a binary fraction leaves (decided with the
/// user, 2026-10-26; ADR-0350 section 16, which the Script Core shares). Anything else shows its own
/// <c>ToString()</c>, as before: a <see cref="decimal"/> is exact and keeps its scale.
/// </summary>
internal static class DisplayText
{
    /// <summary>The text of <paramref name="value"/>: <c>yyyy-MM-dd</c> for a <see cref="DateOnly"/>,
    /// <c>yyyy-MM-dd HH:mm:ss</c> for a <see cref="DateTime"/>, <c>yyyy-MM-dd HH:mm:ss zzz</c> for a
    /// <see cref="DateTimeOffset"/>, <c>HH:mm:ss</c> for a <see cref="TimeOnly"/>, a finite
    /// <see cref="double"/> rounded to 15 significant digits and written in its shortest text, and the
    /// value's own <c>ToString()</c> for anything else. The form follows the type, so a column never
    /// mixes forms.</summary>
    internal static string Of(object value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        double number when double.IsFinite(number) => General(number),
        _ => value.ToString() ?? "",
    };

    /// <summary>The entry a cell's editor opens on where the Consumer gives none, which Replace rewrites
    /// too: <see cref="Of"/>'s text, but a <see cref="double"/> whole, so an edit committed unchanged, or
    /// a Replace of other digits, never writes the 15 digits it shows back over the value (ADR-0006, note
    /// of 2026-10-26).</summary>
    internal static string EntryOf(object value)
        => value is double number ? (number + 0.0).ToString() : Of(value);

    // Excel's General: rounded to 15 significant digits through the invariant culture, then written in
    // the reader's shortest text, as ToString() wrote it before. Adding zero makes a negative zero the
    // zero it reads as, as JavaScript writes it.
    private static string General(double number)
        => (double.Parse(number.ToString("G15", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + 0.0).ToString();
}
