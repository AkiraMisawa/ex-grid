using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// Starts typed field declarations (ADR-0063/0065, Q53) — the standard way to hand ExPivot records:
/// <code>
/// var fields = PivotFields.Of&lt;Trade&gt;()
///     .Text("Region", t =&gt; t.Region)
///     .Number("Pnl", t =&gt; t.Pnl, caption: "P&amp;L", format: "#,##0.00")
///     .Date("TradeDate", t =&gt; t.TradeDate, caption: "Trade date")
///     .Month("TradeMonth", of: "TradeDate");
/// var source = PivotSource.From(trades, fields);
/// </code>
/// </summary>
public static class PivotFields
{
    /// <summary>Typed field declarations over records of type <typeparamref name="T"/>.</summary>
    public static PivotFields<T> Of<T>() => new();
}

/// <summary>
/// Typed field declarations (ADR-0063/0065, Q53): each field is declared once, with a typed
/// accessor and its pivot settings, and makes both a Snapshot column — read without boxing a value
/// — and the Pivot Field over it. <see cref="PivotSource.From{TRecord}(IReadOnlyList{TRecord}, PivotFields{TRecord}, PivotSlicing?)"/>
/// builds the Snapshot and answers from it.
/// <list type="bullet">
/// <item>A Number takes <c>decimal?</c> — an exact Decimal column, money summed exactly —
/// <c>double?</c> (a Double column), or <c>long?</c> and <c>int?</c> (an Integer column).</item>
/// <item>A Date takes <c>DateTime?</c>, <c>DateOnly?</c> or <c>DateTimeOffset?</c>, held as the
/// clock value it shows (ADR-0063); <see cref="Year"/>, <see cref="Quarter"/> and
/// <see cref="Month"/> declare a part of one (ADR-0059).</item>
/// <item>null is a Blank in every kind; the empty string is a value.</item>
/// <item><see cref="Key(string)"/> names the Record Key, so the source takes Change Batches that
/// change and remove records (<see cref="Batch"/>, ADR-0066).</item>
/// </list>
/// A declaration is reused: every build and every batch reads the records through the same columns.
/// </summary>
/// <typeparam name="T">The type of the Consumer's records.</typeparam>
public sealed class PivotFields<T>
{
    private readonly List<PivotField> _fields = [];
    private readonly Dictionary<string, (SnapshotKind Kind, string Caption)> _columns = new(StringComparer.Ordinal);

    internal PivotFields()
    {
    }

    /// <summary>The Pivot Fields declared, in order: what the Field List offers.</summary>
    public IReadOnlyList<PivotField> Fields => _fields.AsReadOnly();

    /// <summary>The Snapshot columns declared: what the records are read into. A column for the Record
    /// Key alone (<see cref="Key(string, Func{T, string?})"/>) is one of them and is not a field.</summary>
    public SnapshotBuilder<T> Columns { get; } = new();

    /// <summary>Declares a text field. <see langword="null"/> is a Blank; the empty string is a value.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the text.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="itemOrder">Values whose Items come first, in this order — Excel's custom lists.</param>
    /// <param name="orderKey">The field's Order Key, from an Item's text (<see cref="PivotField.OrderKey"/>).</param>
    public PivotFields<T> Text(string name, Func<T, string?> value, string? caption = null,
        IReadOnlyList<object>? itemOrder = null, Func<string, IComparable?>? orderKey = null)
    {
        var field = new PivotField(name, PivotFieldType.Text, caption, itemOrder: itemOrder)
        {
            OrderKey = orderKey is null ? null : item => orderKey((string)item),
        };
        Claim(name);
        Columns.Text(name, value, caption);
        return Add(SnapshotKind.Text, field);
    }

    /// <summary>Declares a number field held exactly — a Decimal column, summed exactly (ADR-0059).</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the number.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    /// <param name="orderKey">The field's Order Key, from an Item's number.</param>
    public PivotFields<T> Number(string name, Func<T, decimal?> value, string? caption = null, string? format = null,
        Func<double, IComparable?>? orderKey = null)
    {
        var field = NumberField(name, caption, format, orderKey);
        Claim(name);
        Columns.Decimal(name, value, caption);
        return Add(SnapshotKind.Decimal, field);
    }

    /// <summary>Declares a number field held as <c>double</c> — a Double column, summed in
    /// <c>double</c>; a non-finite value is <c>#NUM!</c>.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the number.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    /// <param name="orderKey">The field's Order Key, from an Item's number.</param>
    public PivotFields<T> Number(string name, Func<T, double?> value, string? caption = null, string? format = null,
        Func<double, IComparable?>? orderKey = null)
    {
        var field = NumberField(name, caption, format, orderKey);
        Claim(name);
        Columns.Double(name, value, caption);
        return Add(SnapshotKind.Double, field);
    }

    /// <summary>Declares a number field of 64-bit integers — an Integer column, summed exactly.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the number.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    /// <param name="orderKey">The field's Order Key, from an Item's number.</param>
    public PivotFields<T> Number(string name, Func<T, long?> value, string? caption = null, string? format = null,
        Func<double, IComparable?>? orderKey = null)
    {
        var field = NumberField(name, caption, format, orderKey);
        Claim(name);
        Columns.Integer(name, value, caption);
        return Add(SnapshotKind.Integer, field);
    }

    /// <summary>Declares a number field read through an <see cref="int"/> accessor — an Integer
    /// column, summed exactly.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the number.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    /// <param name="orderKey">The field's Order Key, from an Item's number.</param>
    public PivotFields<T> Number(string name, Func<T, int?> value, string? caption = null, string? format = null,
        Func<double, IComparable?>? orderKey = null)
    {
        var field = NumberField(name, caption, format, orderKey);
        Claim(name);
        Columns.Integer(name, value, caption);
        return Add(SnapshotKind.Integer, field);
    }

    /// <summary>Declares a date field. A <see cref="DateTime"/> is held as its ticks, with its
    /// <see cref="DateTime.Kind"/> ignored (ADR-0063).</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the date.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with; the culture's short date
    /// (with the time when there is one) when left out.</param>
    public PivotFields<T> Date(string name, Func<T, DateTime?> value, string? caption = null, string? format = null)
    {
        var field = new PivotField(name, PivotFieldType.Date, caption, format);
        Claim(name);
        Columns.Date(name, value, caption);
        return Add(SnapshotKind.Date, field);
    }

    /// <summary>Declares a date field. A <see cref="DateOnly"/> is held as its midnight.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the date.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    public PivotFields<T> Date(string name, Func<T, DateOnly?> value, string? caption = null, string? format = null)
    {
        var field = new PivotField(name, PivotFieldType.Date, caption, format);
        Claim(name);
        Columns.Date(name, value, caption);
        return Add(SnapshotKind.Date, field);
    }

    /// <summary>Declares a date field. A <see cref="DateTimeOffset"/> is held as the clock it shows,
    /// with its offset dropped: a Consumer that means instants passes UTC (ADR-0059).</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the date.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    /// <param name="format">A .NET format string its Items are labelled with.</param>
    public PivotFields<T> Date(string name, Func<T, DateTimeOffset?> value, string? caption = null, string? format = null)
    {
        var field = new PivotField(name, PivotFieldType.Date, caption, format);
        Claim(name);
        Columns.Date(name, value, caption);
        return Add(SnapshotKind.Date, field);
    }

    /// <summary>Declares a Boolean field, labelled <c>TRUE</c> and <c>FALSE</c>.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by, and the column's name.</param>
    /// <param name="value">Reads the value.</param>
    /// <param name="caption">What the Field List and the report call it; the name when left out.</param>
    public PivotFields<T> Boolean(string name, Func<T, bool?> value, string? caption = null)
    {
        var field = new PivotField(name, PivotFieldType.Boolean, caption);
        Claim(name);
        Columns.Boolean(name, value, caption);
        return Add(SnapshotKind.Boolean, field);
    }

    /// <summary>Declares a field that is the year of a date field declared before it (ADR-0059):
    /// <c>2026</c>, ordered by the calendar.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by.</param>
    /// <param name="of">The date field it is a part of.</param>
    /// <param name="caption">What the Field List and the report call it; <c>Years (Trade date)</c>
    /// when left out, as Excel calls its own.</param>
    public PivotFields<T> Year(string name, string of, string? caption = null) => AddPart(name, of, PivotDatePart.Year, caption);

    /// <summary>Declares a field that is the quarter of a date field declared before it (ADR-0059):
    /// <c>Qtr3</c> in the report's words, ordered by the calendar.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by.</param>
    /// <param name="of">The date field it is a part of.</param>
    /// <param name="caption">What the Field List and the report call it; <c>Quarters (Trade date)</c>
    /// when left out.</param>
    public PivotFields<T> Quarter(string name, string of, string? caption = null) => AddPart(name, of, PivotDatePart.Quarter, caption);

    /// <summary>Declares a field that is the month of a date field declared before it (ADR-0059):
    /// <c>Sep</c> in the report's words, ordered by the calendar, January first.</summary>
    /// <param name="name">What a Pivot Layout addresses the field by.</param>
    /// <param name="of">The date field it is a part of.</param>
    /// <param name="caption">What the Field List and the report call it; <c>Months (Trade date)</c>
    /// when left out.</param>
    public PivotFields<T> Month(string name, string of, string? caption = null) => AddPart(name, of, PivotDatePart.Month, caption);

    /// <summary>
    /// Names the Record Key (ADR-0063): a declared Text or Integer field whose value tells each record
    /// from every other. A build then refuses a Blank key and a key carried twice, naming it, and
    /// the source takes Change Batches that change and remove records by it.
    /// </summary>
    /// <exception cref="ArgumentException">No such column is declared, or it is neither Text nor Integer.</exception>
    public PivotFields<T> Key(string name)
    {
        Columns.Key(name);
        return this;
    }

    /// <summary>Declares a Text Record Key that is not offered as a field — a trade's id — and names
    /// it the Record Key (ADR-0063).</summary>
    /// <param name="name">The column's name.</param>
    /// <param name="value">Reads the key.</param>
    public PivotFields<T> Key(string name, Func<T, string?> value)
    {
        Claim(name);
        Columns.Text(name, value);
        _columns.Add(name, (SnapshotKind.Text, name));
        return Key(name);
    }

    /// <summary>Declares an Integer Record Key that is not offered as a field, and names it the Record
    /// Key (ADR-0063).</summary>
    /// <param name="name">The column's name.</param>
    /// <param name="value">Reads the key.</param>
    public PivotFields<T> Key(string name, Func<T, long?> value)
    {
        Claim(name);
        Columns.Integer(name, value);
        _columns.Add(name, (SnapshotKind.Integer, name));
        return Key(name);
    }

    /// <summary>Builds the Snapshot of <paramref name="records"/> on the calling thread.</summary>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or
    /// carried twice: the build fails whole, naming the row and the column (ADR-0063).</exception>
    public Snapshot Build(IReadOnlyList<T> records) => Columns.Build(records);

    /// <summary>Builds the Snapshot of <paramref name="records"/> in slices, yielding between them and
    /// reporting progress (<see cref="SnapshotLoadOptions"/>).</summary>
    /// <exception cref="SnapshotException">A value could not be read, or a Record Key is Blank or
    /// carried twice.</exception>
    public ValueTask<Snapshot> BuildAsync(IReadOnlyList<T> records, SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
        => Columns.BuildAsync(records, options, cancellationToken);

    /// <summary>A Change Batch read through these declarations (ADR-0063/0066): the records added,
    /// the records changed (found by their Record Key) and the keys removed — a <see cref="string"/>
    /// for a Text key, an integer for an Integer one.</summary>
    public ChangeBatch Batch(IEnumerable<T>? added = null, IEnumerable<T>? changed = null, IEnumerable<object>? removedKeys = null)
        => Columns.Batch(added, changed, removedKeys);

    private static PivotField NumberField(string name, string? caption, string? format, Func<double, IComparable?>? orderKey)
        => new(name, PivotFieldType.Number, caption, format)
        {
            OrderKey = orderKey is null ? null : item => orderKey((double)item),
        };

    private PivotFields<T> AddPart(string name, string of, PivotDatePart part, string? caption)
    {
        ArgumentException.ThrowIfNullOrEmpty(of);
        if (!_columns.TryGetValue(of, out var column))
            throw new ArgumentException($"No date field named '{of}' is declared; declare it before its {part}.", nameof(of));
        if (column.Kind != SnapshotKind.Date)
            throw new ArgumentException($"'{of}' is a {column.Kind} field; a {part} is a part of a Date field.", nameof(of));
        var field = PivotField.DatePartOf(name, of, part, caption ?? PivotField.DefaultCaption(part, column.Caption));
        Claim(name);
        _fields.Add(field);
        return this;
    }

    // A name is the field's and its column's, and is unique among both; it is claimed before
    // anything is declared, so a refused declaration leaves nothing behind.
    private void Claim(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_columns.ContainsKey(name) || _fields.Exists(f => f.Name == name))
            throw new ArgumentException($"A field or column named '{name}' is already declared; a name is unique.", nameof(name));
    }

    private PivotFields<T> Add(SnapshotKind kind, PivotField field)
    {
        _columns.Add(field.Name, (kind, field.Caption));
        _fields.Add(field);
        return this;
    }
}
