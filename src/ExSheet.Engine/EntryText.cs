using System.Globalization;

namespace ExSheet.Engine;

/// <summary>Writes a constant back as the text a user would type for it under a culture.</summary>
internal static class EntryText
{
    public static string Write(Value constant, CultureInfo culture)
    {
        switch (constant.Kind)
        {
            case ValueKind.Number:
                return constant.Number.ToString("G15", culture);
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
}
