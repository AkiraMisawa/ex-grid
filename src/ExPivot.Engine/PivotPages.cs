using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// What a Pivot Source answers a <see cref="PivotItemsQuery"/> with (ADR-0066): the field's Items
/// over all the data — not narrowed by other fields' Hidden Items — that match the search, at most
/// as many as asked, with how many match; or a refusal. The Items stand in the source's invariant
/// order — numbers, dates, text, Booleans, <c>#NUM!</c>, then <c>(blank)</c>; numbers and dates by
/// value, text ordinally ignoring case — since the report's culture is not the source's to know;
/// the Field List orders them as the field is ordered. Immutable and serialisable.
/// </summary>
public sealed class PivotItemPage
{
    private readonly string? _sourceVersion;
    private readonly PivotItemKey[] _items;

    /// <summary>A page of Items.</summary>
    /// <param name="sourceVersion">The Source Version the Items came from.</param>
    /// <param name="items">The Items, at most as many as asked.</param>
    /// <param name="total">How many Items match, the ones on the page included.</param>
    public PivotItemPage(string sourceVersion, IReadOnlyList<PivotItemKey> items, int total)
    {
        ArgumentNullException.ThrowIfNull(sourceVersion);
        _items = PivotQuery.Copy(items ?? throw new ArgumentNullException(nameof(items)), nameof(items));
        if (total < _items.Length)
            throw new ArgumentOutOfRangeException(nameof(total), total, $"A page of {_items.Length} Items has at least that many in total.");
        _sourceVersion = sourceVersion;
        _total = total;
    }

    private PivotItemPage(PivotSourceRefusal refusal)
    {
        Refusal = refusal;
        _items = [];
    }

    /// <summary>The source's refusal (ADR-0066): an unknown field, or a Source Version it can no
    /// longer answer under.</summary>
    public static PivotItemPage Refused(PivotSourceRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new PivotItemPage(refusal);
    }

    /// <summary>Why the source did not answer, or null when it did.</summary>
    public PivotSourceRefusal? Refusal { get; }

    /// <summary>Whether the source refused.</summary>
    public bool IsRefused => Refusal is not null;

    /// <summary>The Source Version the Items came from. Refused on a refusal.</summary>
    public string SourceVersion => _sourceVersion ?? throw RefusedError();

    /// <summary>The Items, in the source's invariant order. Refused on a refusal, which is not
    /// an empty list.</summary>
    public IReadOnlyList<PivotItemKey> Items => IsRefused ? throw RefusedError() : _items;

    /// <summary>How many Items match, the ones on the page included. Refused on a refusal.</summary>
    public int Total => IsRefused ? throw RefusedError() : _total;

    private readonly int _total;

    private InvalidOperationException RefusedError() => new($"The source refused to list Items: {Refusal!.Message}");
}

/// <summary>
/// What a Pivot Source answers a <see cref="PivotDetailsQuery"/> with (ADR-0063/0066): one page of
/// the records behind the cell, in the data's order, with how many there are; or a refusal. Each
/// record carries its values in the order of <see cref="Fields"/>. Immutable, and serialisable
/// but for the Consumer's own record objects, which never leave the process.
/// </summary>
public sealed class PivotDetailPage
{
    private readonly string? _sourceVersion;
    private readonly PivotField[] _fields;
    private readonly PivotDetailRecord[] _records;

    /// <summary>A page of the records behind a cell.</summary>
    /// <param name="sourceVersion">The Source Version the records came from.</param>
    /// <param name="fields">The fields each record's values are in, in order — the source's.</param>
    /// <param name="start">The first record's position among the records behind the cell.</param>
    /// <param name="total">How many records are behind the cell.</param>
    /// <param name="records">The page's records.</param>
    public PivotDetailPage(
        string sourceVersion,
        IReadOnlyList<PivotField> fields,
        int start,
        long total,
        IReadOnlyList<PivotDetailRecord> records)
    {
        ArgumentNullException.ThrowIfNull(sourceVersion);
        _fields = PivotQuery.Copy(fields ?? throw new ArgumentNullException(nameof(fields)), nameof(fields));
        _records = PivotQuery.Copy(records ?? throw new ArgumentNullException(nameof(records)), nameof(records));
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (_records.Length > 0 && start + (long)_records.Length > total)
            throw new ArgumentOutOfRangeException(nameof(total), total, $"A page of {_records.Length} records from {start} needs at least {start + (long)_records.Length} in total.");
        foreach (var record in _records)
        {
            if (record.Values.Count != _fields.Length)
                throw new ArgumentException($"A record carries {record.Values.Count} values, and the page has {_fields.Length} fields.", nameof(records));
        }
        _sourceVersion = sourceVersion;
        _start = start;
        _total = total;
    }

    private PivotDetailPage(PivotSourceRefusal refusal)
    {
        Refusal = refusal;
        _fields = [];
        _records = [];
    }

    /// <summary>The source's refusal (ADR-0066): an unknown field, or a Source Version it can no
    /// longer answer under — the records would no longer add up to the cell.</summary>
    public static PivotDetailPage Refused(PivotSourceRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new PivotDetailPage(refusal);
    }

    /// <summary>Why the source did not answer, or null when it did.</summary>
    public PivotSourceRefusal? Refusal { get; }

    /// <summary>Whether the source refused.</summary>
    public bool IsRefused => Refusal is not null;

    /// <summary>The Source Version the records came from. Refused on a refusal.</summary>
    public string SourceVersion => _sourceVersion ?? throw RefusedError();

    /// <summary>The fields each record's values are in, in order.</summary>
    public IReadOnlyList<PivotField> Fields => IsRefused ? throw RefusedError() : _fields;

    /// <summary>The first record's position among the records behind the cell. Refused on a
    /// refusal.</summary>
    public int Start => IsRefused ? throw RefusedError() : _start;

    /// <summary>How many records are behind the cell. Refused on a refusal.</summary>
    public long Total => IsRefused ? throw RefusedError() : _total;

    private readonly int _start;
    private readonly long _total;

    /// <summary>The page's records, in the data's order. Refused on a refusal, which is not an
    /// empty page.</summary>
    public IReadOnlyList<PivotDetailRecord> Records => IsRefused ? throw RefusedError() : _records;

    private InvalidOperationException RefusedError() => new($"The source refused to show the records: {Refusal!.Message}");
}

/// <summary>
/// One record behind a cell (ADR-0063/0066): its values, one per field of its page, and — from a
/// source in the process — the Consumer's own record object, which is never serialised.
///
/// <para>A value is null for a Blank, or one of six types: <c>string</c> for text, <c>decimal</c>
/// for an integral or <c>decimal</c> number, <c>double</c> for a <c>double</c> or <c>float</c>
/// one, <c>DateTime</c> for a date by its clock value, and <c>bool</c>. Any other value is text,
/// by its invariant text (ADR-0060). A value is normally of its field's declared type; one that is
/// not keeps its own.</para>
/// </summary>
public sealed class PivotDetailRecord
{
    private readonly object?[] _values;

    /// <summary>One record's values, normalised to the six types (<see cref="Normalize"/>).</summary>
    /// <param name="values">One value per field, in the page's field order.</param>
    /// <param name="record">The Consumer's own record, from a source in the process; null from a
    /// server.</param>
    public PivotDetailRecord(IReadOnlyList<object?> values, object? record = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new object?[values.Count];
        for (var i = 0; i < _values.Length; i++)
            _values[i] = Normalize(values[i]);
        Record = record;
    }

    /// <summary>The values, one per field, in the page's field order.</summary>
    public IReadOnlyList<object?> Values => _values;

    /// <summary>The Consumer's own record, from a source in the process; null otherwise.</summary>
    public object? Record { get; }

    /// <summary>
    /// A value as a detail record carries it: null, <c>string</c>, <c>decimal</c> (for every
    /// integral type too), <c>double</c> (for <c>float</c> too), <c>DateTime</c> by its clock value
    /// with its <c>Kind</c> dropped (for <c>DateOnly</c> and <c>DateTimeOffset</c> too, as ADR-0060
    /// reads a date), or <c>bool</c>; anything else by its invariant text.
    /// </summary>
    public static object? Normalize(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b,
        decimal m => m,
        double d => d,
        DateTime d => d.Kind == DateTimeKind.Unspecified ? d : new DateTime(d.Ticks, DateTimeKind.Unspecified),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTimeOffset d => d.DateTime,
        float f => (double)f,
        int i => (decimal)i,
        long l => (decimal)l,
        short s => (decimal)s,
        byte b => (decimal)b,
        sbyte s => (decimal)s,
        uint u => (decimal)u,
        ulong u => (decimal)u,
        ushort u => (decimal)u,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
