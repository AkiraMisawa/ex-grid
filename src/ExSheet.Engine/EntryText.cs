using System.Globalization;

namespace ExSheet.Engine;

/// <summary>
/// Writes a constant back as the text a user would type for it under a culture, so that the Cell
/// Editor opens on something that, typed again, gives the same Entry: a date as the culture's
/// short date, a percentage with <c>%</c>, any other number in General form.
/// </summary>
internal static class EntryText
{
    public static string Write(Value constant, NumberFormat format, CultureInfo culture)
    {
        switch (constant.Kind)
        {
            case ValueKind.Number:
                var number = constant.Number;
                if (format.IsDate && number >= 0 && number < DateSerial.Maximum + 1) return DateTimeText(number, culture);
                if (format.IsPercent) return NumberText.Written(number * 100, culture) + "%";
                return NumberText.Written(number, culture);
            case ValueKind.Boolean:
            case ValueKind.Error:
                return constant.ToString();
            default:
                var text = constant.Text;
                // Text that would be read back as something else keeps Excel's leading apostrophe.
                if (text.Length == 0 || text[0] == '=' || text[0] == '\'' || ConstantParser.Parse(text, culture).Kind != ValueKind.Text)
                {
                    return "'" + text;
                }
                return text;
        }
    }

    private static string DateTimeText(double serial, CultureInfo culture)
    {
        var whole = Math.Floor(serial);
        var hasTime = serial != whole;
        var twelveHour = culture.DateTimeFormat.LongTimePattern.Contains('t', StringComparison.Ordinal);
        var time = NumberFormat.Parse(twelveHour ? "h:mm:ss AM/PM" : "h:mm:ss");
        if (whole == 0 && hasTime) return time.Format(Value.FromNumber(serial), culture).Text;
        var date = NumberFormat.Parse(ConstantParser.DateCode(culture)).Format(Value.FromNumber(whole), culture).Text;
        return hasTime ? date + " " + time.Format(Value.FromNumber(serial), culture).Text : date;
    }
}
