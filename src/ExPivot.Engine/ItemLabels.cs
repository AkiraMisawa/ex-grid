using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// How an Item is painted (ADR-0059): text as it is, a number by the field's format or at most
/// 15 significant digits, a date by the field's format or the culture's short date (with the
/// time when there is one), a Boolean as <c>TRUE</c> / <c>FALSE</c>, a Blank as <c>(blank)</c>.
/// Each Item is labelled once per report, however many branches it stands in.
/// </summary>
internal sealed class ItemLabels(PivotOptions options)
{
    private readonly Dictionary<ItemRef, string> _labels = new(ReferenceEqualityComparer.Instance);

    public string Of(ItemRef item, FieldMeta meta)
    {
        if (!_labels.TryGetValue(item, out var label))
        {
            label = Compute(item, meta);
            _labels[item] = label;
        }
        return label;
    }

    private string Compute(ItemRef item, FieldMeta meta)
    {
        var culture = options.Culture;
        switch (item.Key.Kind)
        {
            case PivotItemKind.Blank:
                return options.Word(PivotWords.Blank);
            case PivotItemKind.Error:
                return PivotItemKey.ErrorText;
            case PivotItemKind.Boolean:
                return item.Key.Ticks == 1 ? "TRUE" : "FALSE";
            case PivotItemKind.Number:
                return item.FirstValue is IFormattable number
                    ? PivotNumberFormat.Apply(number, meta.Format ?? "G15", culture)
                    : item.Key.Number.ToString("G15", culture);
            case PivotItemKind.Date:
                var date = new DateTime(item.Key.Ticks);
                var format = meta.Format ?? (date.TimeOfDay == TimeSpan.Zero ? "d" : "G");
                return PivotNumberFormat.Apply(date, format, culture);
            default:
                return item.FirstValue as string ?? Convert.ToString(item.FirstValue, culture) ?? "";
        }
    }
}

/// <summary>
/// The label order of a field's Items (ADR-0059): the declared Items first, in their declared
/// order; then numbers, dates, text, Booleans, <c>#NUM!</c>, each in its own order — text by the
/// culture's comparison ignoring case, ties broken ordinally so the order is total; and
/// <c>(blank)</c> last in both directions.
/// </summary>
internal sealed class ItemOrder(FieldMeta meta, ItemLabels labels, CultureInfo culture, bool descending)
    : IComparer<ItemRef>
{
    private readonly CompareInfo _compare = culture.CompareInfo;

    public int Compare(ItemRef? x, ItemRef? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        var xBlank = x.Key.Kind == PivotItemKind.Blank;
        var yBlank = y.Key.Kind == PivotItemKind.Blank;
        if (xBlank || yBlank)
            return xBlank == yBlank ? 0 : xBlank ? 1 : -1;
        var ascending = Ascending(x, y);
        return descending ? -ascending : ascending;
    }

    private int Ascending(ItemRef x, ItemRef y)
    {
        var xDeclared = meta.DeclaredOrder.TryGetValue(x.Key, out var xAt);
        var yDeclared = meta.DeclaredOrder.TryGetValue(y.Key, out var yAt);
        if (xDeclared || yDeclared)
            return xDeclared && yDeclared ? xAt.CompareTo(yAt) : xDeclared ? -1 : 1;
        if (x.Key.Kind != y.Key.Kind)
            return x.Key.Kind.CompareTo(y.Key.Kind);
        switch (x.Key.Kind)
        {
            case PivotItemKind.Number:
                return x.Key.Number.CompareTo(y.Key.Number);
            case PivotItemKind.Date:
            case PivotItemKind.Boolean:
                return x.Key.Ticks.CompareTo(y.Key.Ticks);
            case PivotItemKind.Text:
                var xText = labels.Of(x, meta);
                var yText = labels.Of(y, meta);
                var compared = _compare.Compare(xText, yText, CompareOptions.IgnoreCase);
                return compared != 0 ? compared : string.CompareOrdinal(xText, yText);
            default:
                return 0;
        }
    }
}

/// <summary>
/// The .NET format strings a Value Field or an Item may be shown in (ADR-0059/0060). A format is
/// refused when it cannot format a number, is longer than <see cref="MaxLength"/> characters,
/// or asks a standard format for more than <see cref="MaxPrecision"/> digits: <c>N999999999</c>
/// is a valid .NET format that writes a billion zeros, and no report asked for that.
/// </summary>
public static class PivotNumberFormat
{
    /// <summary>The longest format accepted.</summary>
    public const int MaxLength = 64;

    /// <summary>The most digits a standard format's precision may ask for.</summary>
    public const int MaxPrecision = 30;

    /// <summary>Why <paramref name="format"/> cannot be used — a phrase that completes "the
    /// format … " — or null when it can.</summary>
    public static string? Check(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.Length == 0)
            return "is empty";
        if (format.Length > MaxLength)
            return $"is longer than {MaxLength} characters";
        if (char.IsAsciiLetter(format[0]) && format.Length > 1 && format.AsSpan(1).ContainsOnlyDigits()
            && int.TryParse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var precision)
            && precision > MaxPrecision)
            return $"asks for {precision} digits, more than {MaxPrecision}";
        try
        {
            _ = 1234.5m.ToString(format, CultureInfo.InvariantCulture);
            _ = (-1234.5).ToString(format, CultureInfo.InvariantCulture);
            _ = 0.0.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return "cannot format a number";
        }
        return null;
    }

    /// <summary>A value in a format already checked, or in its own general form when the format
    /// does not apply to it (a date's format on a number).</summary>
    internal static string Apply(IFormattable value, string format, CultureInfo culture)
    {
        try
        {
            return value.ToString(format, culture);
        }
        catch (FormatException)
        {
            return value.ToString(null, culture);
        }
    }
}

internal static class SpanDigits
{
    public static bool ContainsOnlyDigits(this ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }
        return true;
    }
}
