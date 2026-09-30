namespace ExPivot.Engine;

/// <summary>
/// What the Field List and the layout rules need of a Pivot Field, without its accessor:
/// its name, its caption, its declared type (ADR-0060). A Pivot Layout names fields by
/// <see cref="Name"/>; the Field List and the report show <see cref="Caption"/>.
/// </summary>
/// <param name="Name">What the field is addressed by in a Pivot Layout. Unique among the
/// declared fields.</param>
/// <param name="Caption">What the Field List and the report call it.</param>
/// <param name="Type">The declared type, which decides the defaults (ADR-0059).</param>
public sealed record PivotFieldInfo(string Name, string Caption, PivotFieldType Type);

/// <summary>
/// A Pivot Field as a Pivot Source offers it (ADR-0065): its name, caption and declared type,
/// the format its Items are labelled with, and the order its Items are declared in (Excel's
/// custom lists) — everything but how it is read from a record, which is the source's own. A
/// source's <see cref="PivotSource.Fields"/> are these; the Field List and the report need
/// nothing more. Immutable; a changed declaration is a new instance.
/// </summary>
public class PivotField
{
    /// <summary>Declares one Pivot Field.</summary>
    /// <param name="name">What a Pivot Layout addresses it by; unique among the fields.</param>
    /// <param name="type">The declared type (ADR-0059).</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string an Item's number or date is labelled with, under
    /// the report's culture; the value's own text when left out.</param>
    /// <param name="itemOrder">Values whose Items come first, in this order, when the field is
    /// sorted by label — the months of a year, a scale of ratings. The other Items follow.</param>
    public PivotField(
        string name,
        PivotFieldType type,
        string? caption = null,
        string? format = null,
        IReadOnlyList<object>? itemOrder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (type is not (PivotFieldType.Text or PivotFieldType.Number or PivotFieldType.Date or PivotFieldType.Boolean))
            throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown PivotFieldType for Pivot Field '{name}'.");
        if (caption is not null && caption.Length == 0)
            throw new ArgumentException($"Pivot Field '{name}' has an empty caption; leave it out for the name.", nameof(caption));
        if (format is not null && FormatProblem(type, format) is { } problem)
            throw new ArgumentException($"Pivot Field '{name}' declares the format '{format}', which {problem}.", nameof(format));

        Format = format;
        ItemOrder = itemOrder?.ToArray() ?? [];
        Info = new PivotFieldInfo(name, caption ?? name, type);
    }

    /// <summary>The name, caption and type, as the layout rules take them.</summary>
    public PivotFieldInfo Info { get; }

    /// <summary>What a Pivot Layout addresses the field by.</summary>
    public string Name => Info.Name;

    /// <summary>What the Field List and the report call it.</summary>
    public string Caption => Info.Caption;

    /// <summary>The declared type.</summary>
    public PivotFieldType Type => Info.Type;

    /// <summary>The .NET format string an Item's number or date is labelled with, or null.</summary>
    public string? Format { get; }

    /// <summary>The values whose Items come first when sorted by label, in this order.</summary>
    public IReadOnlyList<object> ItemOrder { get; }

    // A number's format is held to what a Value Field's is (PivotNumberFormat); a date's needs
    // only to format a date, and cannot run away the way a standard number format's precision
    // can.
    private static string? FormatProblem(PivotFieldType type, string format)
    {
        if (type != PivotFieldType.Date)
            return PivotNumberFormat.Check(format);
        if (format.Length is 0 or > PivotNumberFormat.MaxLength)
            return $"is empty or longer than {PivotNumberFormat.MaxLength} characters";
        try
        {
            _ = new DateTime(2026, 9, 30, 13, 45, 0).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            return null;
        }
        catch (FormatException)
        {
            return "cannot format a date";
        }
    }
}

/// <summary>
/// A Pivot Field as the Consumer declares it over records in memory (ADR-0058): a
/// <see cref="PivotField"/> and how it is read from one record. <see cref="PivotSource.From{TRecord}"/>
/// reads the records through these. Immutable; a changed declaration is a new instance.
/// </summary>
/// <typeparam name="TRecord">The Consumer's record type.</typeparam>
public sealed class PivotField<TRecord> : PivotField
{
    /// <summary>Declares one Pivot Field.</summary>
    /// <param name="name">What a Pivot Layout addresses it by; unique among the fields.</param>
    /// <param name="type">The declared type (ADR-0059).</param>
    /// <param name="value">Reads the field from a record; null is a Blank.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string an Item's number or date is labelled with, under
    /// the report's culture; the value's own text when left out.</param>
    /// <param name="itemOrder">Values whose Items come first, in this order, when the field is
    /// sorted by label — the months of a year, a scale of ratings. The other Items follow.</param>
    public PivotField(
        string name,
        PivotFieldType type,
        Func<TRecord, object?> value,
        string? caption = null,
        string? format = null,
        IReadOnlyList<object>? itemOrder = null)
        : base(name, type, caption, format, itemOrder)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>Reads the field from a record; null is a Blank.</summary>
    public Func<TRecord, object?> Value { get; }
}
