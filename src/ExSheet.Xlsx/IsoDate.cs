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
    [GeneratedRegex(@"^(?:(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2}))?(?:(?(y)T)(?<h>\d{2}):(?<mi>\d{2})(?::(?<s>\d{2})(?<f>\.\d+)?)?)?(?<z>Z|[+-]\d{2}(?::?\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    /// <summary>The serial of <paramref name="text"/>, or null when it is not an ISO 8601 date or time, or lies before 1900.</summary>
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

        int year = Int(match.Groups["y"]), month = Int(match.Groups["mo"]), day = Int(match.Groups["d"]);
        if (year < 1900 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        // 1 is 1 January 1900; Excel keeps a 29 February 1900 (60) for Lotus's sake, so from 1 March 1900 days count from 30 December 1899.
        var serial = date < new DateTime(1900, 3, 1) ? (date - new DateTime(1899, 12, 31)).Days : (date - new DateTime(1899, 12, 30)).Days;
        return serial + time;
    }

    private static int Int(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);
}
