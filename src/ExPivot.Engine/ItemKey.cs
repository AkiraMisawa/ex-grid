using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// An Item's identity while the engine sorts records into Items (ADR-0059): its kind, and a
/// number, a clock value or a text, compared as Items are told apart — text ignoring case.
/// A struct, so that sorting a million records allocates nothing for the numbers and dates.
/// </summary>
internal readonly struct ItemKey : IEquatable<ItemKey>
{
    private ItemKey(PivotItemKind kind, double number, long ticks, string? text)
    {
        Kind = kind;
        Number = number;
        Ticks = ticks;
        Text = text;
    }

    public PivotItemKind Kind { get; }

    /// <summary>The number of a <see cref="PivotItemKind.Number"/> Item.</summary>
    public double Number { get; }

    /// <summary>The clock ticks of a date; 0 or 1 for a Boolean.</summary>
    public long Ticks { get; }

    /// <summary>The text of a text Item.</summary>
    public string? Text { get; }

    public static ItemKey Blank => new(PivotItemKind.Blank, 0, 0, null);

    public static ItemKey Error => new(PivotItemKind.Error, 0, 0, null);

    public static ItemKey OfText(string text) => new(PivotItemKind.Text, 0, 0, text);

    public static ItemKey OfNumber(double number)
        => double.IsFinite(number) ? new(PivotItemKind.Number, number == 0 ? 0 : number, 0, null) : Error;

    public static ItemKey OfDate(DateTime date) => new(PivotItemKind.Date, 0, date.Ticks, null);

    public static ItemKey OfBoolean(bool value) => new(PivotItemKind.Boolean, 0, value ? 1 : 0, null);

    /// <summary>The Item a value belongs to (ADR-0059).</summary>
    public static ItemKey Of(object? value) => value switch
    {
        null => Blank,
        string s => OfText(s),
        bool b => OfBoolean(b),
        DateTime d => OfDate(d),
        DateOnly d => OfDate(d.ToDateTime(TimeOnly.MinValue)),
        DateTimeOffset d => OfDate(d.DateTime),
        double d => OfNumber(d),
        float f => OfNumber(f),
        decimal m => OfNumber((double)m),
        int i => OfNumber(i),
        long l => OfNumber(l),
        short s => OfNumber(s),
        byte b => OfNumber(b),
        sbyte s => OfNumber(s),
        uint u => OfNumber(u),
        ulong u => OfNumber(u),
        ushort u => OfNumber(u),
        _ => OfText(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
    };

    public static ItemKey FromPublic(PivotItemKey key) => key.Kind switch
    {
        PivotItemKind.Blank => Blank,
        PivotItemKind.Error => Error,
        PivotItemKind.Text => OfText(key.Value!),
        PivotItemKind.Number => OfNumber(double.Parse(key.Value!, NumberStyles.Float, CultureInfo.InvariantCulture)),
        PivotItemKind.Date => OfDate(key.DateValue),
        PivotItemKind.Boolean => OfBoolean(key.Value == "TRUE"),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "Unknown PivotItemKind."),
    };

    public PivotItemKey ToPublic() => Kind switch
    {
        PivotItemKind.Blank => PivotItemKey.Blank,
        PivotItemKind.Error => PivotItemKey.Error,
        PivotItemKind.Text => PivotItemKey.Text(Text!),
        PivotItemKind.Number => PivotItemKey.Number(Number),
        PivotItemKind.Date => PivotItemKey.Date(new DateTime(Ticks)),
        PivotItemKind.Boolean => PivotItemKey.Boolean(Ticks == 1),
        _ => throw new InvalidOperationException("Unknown PivotItemKind."),
    };

    public bool Equals(ItemKey other)
    {
        if (Kind != other.Kind)
            return false;
        return Kind switch
        {
            PivotItemKind.Number => Number.Equals(other.Number),
            PivotItemKind.Date or PivotItemKind.Boolean => Ticks == other.Ticks,
            PivotItemKind.Text => string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    public override bool Equals(object? obj) => obj is ItemKey other && Equals(other);

    public override int GetHashCode() => Kind switch
    {
        PivotItemKind.Number => HashCode.Combine(Kind, Number),
        PivotItemKind.Date or PivotItemKind.Boolean => HashCode.Combine(Kind, Ticks),
        PivotItemKind.Text => HashCode.Combine(Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(Text!)),
        _ => (int)Kind,
    };
}
