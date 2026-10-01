using System.Globalization;

namespace ExGrid.Data.Csv;

/// <summary>
/// A Schema checked before any byte is read: every column named once and matched once, every reading
/// one that can be applied, and the Record Key a declared Text or Integer column. A Schema that
/// contradicts itself is the Consumer's mistake, not the file's, so it is refused with an
/// <see cref="ArgumentException"/> rather than a <see cref="SnapshotException"/>.
/// </summary>
internal sealed class CsvPlan
{
    private CsvPlan(CsvSchema schema, CsvColumn[] columns, IReadOnlyList<string>[] blankTexts, int key)
    {
        Schema = schema;
        Columns = columns;
        BlankTexts = blankTexts;
        KeyOrdinal = key;
        Separator = schema.Separator switch
        {
            CsvSeparator.Comma => (byte)',',
            CsvSeparator.Tab => (byte)'\t',
            _ => (byte)';',
        };
    }

    public CsvSchema Schema { get; }

    public CsvColumn[] Columns { get; }

    /// <summary>Each column's blank texts: its own, or the Schema's.</summary>
    public IReadOnlyList<string>[] BlankTexts { get; }

    /// <summary>The Record Key's place among the columns, or -1.</summary>
    public int KeyOrdinal { get; }

    public byte Separator { get; }

    /// <summary>Checks <paramref name="schema"/> and copies what it declares, so that a list changed
    /// while the file is read changes nothing.</summary>
    /// <exception cref="ArgumentException">The Schema contradicts itself.</exception>
    public static CsvPlan Of(CsvSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Columns is null || schema.Columns.Count == 0)
            throw Wrong("A Schema declares at least one column.");
        if (!Enum.IsDefined(schema.Separator))
            throw Wrong($"The separator {(int)schema.Separator} is not one a CSV is read with: a comma, a tab or a semicolon.");
        if (schema.Encoding is null)
            throw Wrong("The Schema declares no encoding.");
        var schemaBlanks = Texts(schema.BlankText, "The Schema's blank texts") ?? [];

        var columns = schema.Columns.ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var positions = new Dictionary<int, string>();
        var blankTexts = new IReadOnlyList<string>[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i] ?? throw Wrong($"The Schema's column {i} is null.");
            if (string.IsNullOrEmpty(column.Name))
                throw Wrong($"The Schema's column {i} has no name; every column has one.");
            var name = column.Name;
            if (!names.Add(name))
                throw Wrong($"A column named '{name}' is declared twice; a column's name is unique within a Snapshot.");
            if (!Enum.IsDefined(column.Kind))
                throw Wrong($"Column '{name}' is declared with the kind {(int)column.Kind}, which a Snapshot does not hold.");
            if (column.Header is { Length: 0 })
                throw Wrong($"Column '{name}' matches an empty header; declare its Position instead.");
            if (column.Position < 0)
                throw Wrong($"Column '{name}' is declared at position {column.Position}; a position is counted from zero.");

            var position = column.Position ?? (schema.HasHeader ? (int?)null : i);
            if (position is { } at)
            {
                if (!positions.TryAdd(at, name))
                    throw Wrong($"Columns '{positions[at]}' and '{name}' are both declared at position {at}.");
            }
            else if (!headers.TryAdd(column.MatchedHeader, name))
            {
                throw Wrong($"Columns '{headers[column.MatchedHeader]}' and '{name}' both match the header '{column.MatchedHeader}'.");
            }

            blankTexts[i] = Texts(column.BlankText, $"Column '{name}''s blank texts") ?? schemaBlanks;
            switch (column.Kind)
            {
                case SnapshotKind.Decimal or SnapshotKind.Double or SnapshotKind.Integer:
                    CheckNumber(column);
                    break;
                case SnapshotKind.Date:
                    CheckDate(column);
                    break;
                case SnapshotKind.Boolean:
                    CheckBoolean(column, blankTexts[i]);
                    break;
            }
        }

        var key = -1;
        if (schema.RecordKey is { } keyName)
        {
            key = Array.FindIndex(columns, c => c.Name == keyName);
            if (key < 0)
                throw Wrong($"The Record Key names '{keyName}', which is not a declared column.");
            if (columns[key].Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
                throw Wrong($"Only a Text or an Integer column can be the Record Key; '{keyName}' is {columns[key].Kind}.");
        }
        return new CsvPlan(schema, columns, blankTexts, key);
    }

    private static void CheckNumber(CsvColumn column)
    {
        var format = (column.Culture ?? CultureInfo.InvariantCulture).NumberFormat;
        var point = column.DecimalPoint ?? (column.Culture is null ? "." : format.NumberDecimalSeparator);
        var thousands = column.ThousandsSeparator ?? (column.Culture is null ? "" : format.NumberGroupSeparator);
        if (point.Length == 0)
            throw Wrong($"Column '{column.Name}' reads an empty decimal point.");
        if (point.AsSpan().IndexOfAny("0123456789+- ") >= 0)
            throw Wrong($"Column '{column.Name}' reads '{point}' as its decimal point, which holds a digit, a sign or a space.");
        if (thousands.AsSpan().IndexOfAny("0123456789+-") >= 0)
            throw Wrong($"Column '{column.Name}' reads '{thousands}' as its thousands separator, which holds a digit or a sign.");
        if (thousands.Length > 0 && (point.StartsWith(thousands, StringComparison.Ordinal) || thousands.StartsWith(point, StringComparison.Ordinal)))
            throw Wrong($"Column '{column.Name}' reads its decimal point and its thousands separator alike, as '{point}' and '{thousands}'.");
    }

    private static void CheckDate(CsvColumn column)
    {
        if (column.DateFormats is null)
            return;
        if (column.DateFormats.Count == 0)
            throw Wrong($"Column '{column.Name}' declares no date format.");
        var culture = column.Culture ?? CultureInfo.InvariantCulture;
        foreach (var format in column.DateFormats)
        {
            if (string.IsNullOrEmpty(format))
                throw Wrong($"Column '{column.Name}' declares an empty date format.");
            if (!DateFormat.IsValid(format, culture, out var why))
                throw Wrong($"Column '{column.Name}' declares the date format '{format}', which .NET cannot read: {why}");
        }
    }

    private static void CheckBoolean(CsvColumn column, IReadOnlyList<string> blanks)
    {
        var trues = Texts(column.TrueText, $"Column '{column.Name}''s spellings of true") ?? CsvColumn.ExcelTrue;
        var falses = Texts(column.FalseText, $"Column '{column.Name}''s spellings of false") ?? CsvColumn.ExcelFalse;
        if (trues.Count == 0 || falses.Count == 0)
            throw Wrong($"Column '{column.Name}' declares no spelling of {(trues.Count == 0 ? "true" : "false")}.");
        foreach (var spelling in trues.Concat(falses))
        {
            if (spelling.Length == 0)
                throw Wrong($"Column '{column.Name}' declares an empty spelling of true or false; an empty field is a Blank.");
        }
        foreach (var spelling in trues)
        {
            if (falses.Contains(spelling, StringComparer.OrdinalIgnoreCase))
                throw Wrong($"Column '{column.Name}' reads '{spelling}' as both true and false.");
        }
        foreach (var blank in blanks)
        {
            if (trues.Concat(falses).Contains(blank, StringComparer.OrdinalIgnoreCase))
                throw Wrong($"Column '{column.Name}' reads '{blank}' as both a Blank and a Boolean.");
        }
    }

    private static IReadOnlyList<string>? Texts(IReadOnlyList<string>? texts, string what)
    {
        if (texts is null)
            return null;
        var copy = texts.ToArray();
        if (Array.Exists(copy, t => t is null))
            throw Wrong($"{what} hold a null.");
        return copy;
    }

    private static ArgumentException Wrong(string message) => new(message, "schema");
}
