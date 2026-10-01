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
/// the format its Items are labelled with, the order its Items are declared in (Excel's custom
/// lists) and its Order Key — everything but how it is read from a record, which is the source's
/// own. A source's <see cref="PivotSource.Fields"/> are these; the Field List and the report need
/// nothing more. Immutable; a changed declaration is a new instance.
/// <para>
/// Over a Snapshot (<see cref="PivotSource.From(ExGrid.Data.Snapshot, IReadOnlyList{PivotField}?, PivotSlicing?)"/>)
/// a field reads the column its <see cref="Column"/> names, or the column of its own name, and may
/// be a <see cref="DatePart"/> of a Date column. <see cref="PivotFields.Of{T}"/> declares both the
/// columns and the fields in one go, and is the standard way.
/// </para>
/// </summary>
public class PivotField
{
    private readonly string? _column;
    private readonly PivotDatePart? _datePart;
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

    /// <summary>
    /// The Snapshot column the field reads, when it is not the column of the field's own name — a
    /// date part reads the Date column it is a part of. Null for the column of the field's name.
    /// A source over records read through accessors does not read it.
    /// </summary>
    public string? Column
    {
        get => _column;
        init => _column = value is { Length: 0 }
            ? throw new ArgumentException($"Pivot Field '{Name}' names an empty column; leave it out for the column '{Name}'.", nameof(Column))
            : value;
    }

    /// <summary>
    /// The part of a Date column the field is — its year, quarter or month (ADR-0059) — or null for
    /// the column's own values. Its Items are the part's numbers, labelled as Excel labels them in
    /// the report's words and ordered by the calendar; the part of a Blank is a Blank. A date part
    /// is declared <see cref="PivotFieldType.Date"/>, so a ticked one goes to Rows.
    /// </summary>
    /// <exception cref="ArgumentException">The field is not declared <see cref="PivotFieldType.Date"/>.</exception>
    public PivotDatePart? DatePart
    {
        get => _datePart;
        init
        {
            if (value is { } part && !Enum.IsDefined(part))
                throw new ArgumentOutOfRangeException(nameof(DatePart), part, $"Unknown PivotDatePart for Pivot Field '{Name}'.");
            if (value is not null && Type != PivotFieldType.Date)
                throw new ArgumentException($"Pivot Field '{Name}' is a date part, and is declared {Type}; a date part is declared Date.", nameof(DatePart));
            _datePart = value;
        }
    }

    /// <summary>
    /// The field's Order Key (ADR-0059): a function from an Item's value — the text of a text Item
    /// (its first spelling), the <see cref="double"/> of a number, the <see cref="DateTime"/> of a
    /// date, the <see cref="bool"/> of a Boolean — to what the Items are ordered by, ascending, ties
    /// falling back to the label. It is called once per Item, where the report is laid out, so a
    /// server never sees it.
    /// <list type="bullet">
    /// <item>An Item it gives no key (null) comes after the keyed ones, in label order;
    /// <c>(blank)</c> and <c>#NUM!</c> are never keyed, and <c>(blank)</c> stays last.</item>
    /// <item>Descending reverses the whole order. A declared <see cref="ItemOrder"/> still comes
    /// first. A sort by a Value Field does not read it.</item>
    /// <item>The keys of one field are of one type. A function that throws is refused, naming the
    /// field and the value: <c>The Order Key of Tenor failed on '7Y'.</c></item>
    /// <item>It orders Items and never merges them: <c>18M</c> and <c>1Y6M</c> stay two Items.</item>
    /// </list>
    /// </summary>
    public Func<object, IComparable?>? OrderKey { get; init; }

    /// <summary>
    /// Declares a field as a part of a Date column (ADR-0059): its year, quarter or month, labelled
    /// <c>2026</c>, <c>Qtr3</c>, <c>Sep</c> in the report's words and ordered by the calendar.
    /// </summary>
    /// <param name="name">What a Pivot Layout addresses it by; unique among the fields.</param>
    /// <param name="column">The Date column it is a part of.</param>
    /// <param name="part">The part.</param>
    /// <param name="caption">What the Field List and the report call it; <c>Months (Trade date)</c>,
    /// as Excel calls its own, when left out — the part's English and the column's name.</param>
    public static PivotField DatePartOf(string name, string column, PivotDatePart part, string? caption = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        return new PivotField(name, PivotFieldType.Date, caption ?? DefaultCaption(part, column))
        {
            Column = column,
            DatePart = part,
        };
    }

    /// <summary>The caption a date part takes when it is given none: Excel's <c>Months (Trade date)</c>.</summary>
    internal static string DefaultCaption(PivotDatePart part, string column) => part switch
    {
        PivotDatePart.Year => $"Years ({column})",
        PivotDatePart.Quarter => $"Quarters ({column})",
        _ => $"Months ({column})",
    };

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
/// <see cref="PivotField"/> and how it is read from one record. <see cref="PivotSource.From{TRecord}(IReadOnlyList{TRecord}, IReadOnlyList{PivotField{TRecord}}, PivotSlicing?)"/>
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
