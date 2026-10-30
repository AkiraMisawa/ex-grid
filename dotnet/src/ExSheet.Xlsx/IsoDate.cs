using System.Globalization;
using System.Text.RegularExpressions;

namespace ExSheet.Xlsx;

/// <summary>
/// A date cell's ISO 8601 text (<c>t="d"</c>, ECMA-376 Part 1, §18.18.11) as the 1900 date system's serial (ADR-0330 §4),
/// read invariantly: never by the server's culture, its time zone or today's date. A date with a time is its day's serial
/// and the time's fraction of a day; a time alone is its fraction (a serial below 1, as Excel holds a time of day). A
/// cell of Excel's has no time zone, so a written offset (<c>Z</c>, <c>+09:00</c>) is the writer's own, and the clock time
/// written beside it is the one its reader saw: that clock time is carried, and the offset converts nothing.
/// </summary>
internal static partial class IsoDate
{
    // [0-9], never \d: \d matches every script's digits, which ISO 8601 does not write.
    [GeneratedRegex(@"^(?:(?<y>[0-9]{4})-(?<mo>[0-9]{2})-(?<d>[0-9]{2}))?(?:(?(y)T)(?<h>[0-9]{2}):(?<mi>[0-9]{2})(?::(?<s>[0-9]{2})(?<f>\.[0-9]+)?)?)?(?<z>Z|[+-][0-9]{2}(?::?[0-9]{2})?)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    /// <summary>The serial of <paramref name="text"/>, or null when it is not an ISO 8601 date or time, or lies outside 1900 to 9999.</summary>
    public static double? Serial(string text)
    {
        var match = Shape().Match(text);
        if (!match.Success) return null;
        var hasDate = match.Groups["y"].Success;
        var hasTime = match.Groups["h"].Success;
        if (!hasDate && !hasTime) return null;

        double time = 0;
        if (hasTime)
        {
            var hours = Int(match.Groups["h"]);
            var minutes = Int(match.Groups["mi"]);
            var seconds = match.Groups["s"].Success ? Int(match.Groups["s"]) : 0;
            var fraction = match.Groups["f"].Success ? double.Parse("0" + match.Groups["f"].Value, CultureInfo.InvariantCulture) : 0;
            if (hours > 23 || minutes > 59 || seconds > 59) return null;
            time = (hours * 3600 + minutes * 60 + seconds + fraction) / 86400;
        }
        if (!hasDate) return time;

        // The engine's own count of the 1900 date system, its 29 February 1900 (60) included.
        return ExSheet.Engine.DateSerial.FromDate(Int(match.Groups["y"]), Int(match.Groups["mo"]), Int(match.Groups["d"])) is { } serial ? serial + time : null;
    }

    private static int Int(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);
}
