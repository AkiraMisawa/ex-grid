using System.Globalization;

namespace ExGrid;

/// <summary>
/// The text a value shows when its column declares no <c>Format</c> (ADR-0006, note of
/// 2026-10-01): a date or a time in one ISO form by its type, written in the invariant culture,
/// so the same value reads the same under every culture — on the Server host, the server's culture
/// is not the reader's, and a day and a month read the wrong way round are quietly wrong. Anything
/// else shows its own <c>ToString()</c>, as before.
/// </summary>
internal static class DisplayText
{
    /// <summary>The text of <paramref name="value"/>: <c>yyyy-MM-dd</c> for a <see cref="DateOnly"/>,
    /// <c>yyyy-MM-dd HH:mm:ss</c> for a <see cref="DateTime"/>, <c>yyyy-MM-dd HH:mm:ss zzz</c> for a
    /// <see cref="DateTimeOffset"/>, <c>HH:mm:ss</c> for a <see cref="TimeOnly"/>, and the value's
    /// own <c>ToString()</c> for anything else. The form follows the type, so a column never mixes
    /// forms.</summary>
    internal static string Of(object value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
