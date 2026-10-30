using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ExGrid.Columns;

namespace ExGrid;

/// <summary>
/// ExGrid's own entry point for the View State's JSON (ADR-0207), as ExPivot's
/// <c>PivotReportJson</c> is for a report: a whole <see cref="GridViewState"/>, or a
/// <see cref="GridQuery"/> alone, written as JSON and read back as what was written. A Consumer needs
/// no options, no converters and no context of their own, and the form is the same under reflection
/// and under trimming, as on WebAssembly.
///
/// <para><b>The form.</b> A document carries <c>"version": 1</c>. A View State holds its columns in
/// display order, each as <c>{"column", "width", "pinned"}</c>, a width being <c>"Auto"</c> — the
/// intent, never the width an Auto column was painted at (ADR-0016) — or a number of pixels; then the
/// <c>"sorts"</c>, each <c>{"column", "direction"}</c>, and the <c>"filter"</c>, left out when there
/// is none, whose <c>"columns"</c> hold each column's <c>{"combinator", "clauses"}</c>. A Query holds
/// its <c>"range"</c> (<c>{"start", "count"}</c>), its sorts and its filter. Enum members are written
/// by their names. Each operand carries its type: <c>{"type", "value"}</c>, where the tag is one of
/// <c>String</c>, <c>Boolean</c>, <c>Decimal</c>, <c>Double</c>, <c>Single</c>, <c>Int64</c>,
/// <c>Int32</c>, <c>Int16</c>, <c>SByte</c>, <c>UInt64</c>, <c>UInt32</c>, <c>UInt16</c>,
/// <c>Byte</c>, <c>DateTime</c>, <c>DateOnly</c> and <c>DateTimeOffset</c> — the operand types the
/// filter model admits (ADR-0023) — and the value is its text in the invariant culture, always a
/// JSON string: a <c>decimal</c> as its exact digits, so that money survives a reader that would
/// parse a JSON number as a <c>double</c>; a <c>Boolean</c> as <c>true</c> or <c>false</c>; a date
/// as <c>yyyy-MM-dd</c>, a <c>DateTime</c> as <c>yyyy-MM-ddTHH:mm:ss.fffffff</c> with its
/// <c>"kind"</c> (<c>Unspecified</c>, <c>Utc</c> or <c>Local</c>) beside it, and a
/// <c>DateTimeOffset</c> with its offset, <c>yyyy-MM-ddTHH:mm:ss.fffffffzzz</c>. A null among the
/// operands of <c>In</c> is written as <c>null</c>: it selects Blanks.</para>
///
/// <para><b>What reading refuses</b>, with a <see cref="JsonException"/> whose message names the
/// part, and whose <see cref="JsonException.Path"/> says where it stands: text that is not JSON; a
/// document that is not an object; a version that is missing, not a number, or not
/// <see cref="Version"/> — an earlier one or a later one; a member this version does not have, a
/// member it requires that is missing or null, a member of the wrong JSON type, and a member named
/// twice; a column the grid does not have, in the View State's columns, its sort or its
/// filter (<see cref="Read"/> is told the grid's columns); a column named twice among the columns, or
/// pinned after one that is not; a width that is neither <c>"Auto"</c> nor a finite, positive number;
/// a sort direction, a combinator or an operator this version does not name; an operand whose tag
/// is none of the above, whose value does not parse as its tag's type in the invariant culture — a
/// <c>Decimal</c> that is not the exact digits the writer writes, so that one with more digits than a
/// <c>decimal</c> holds is refused rather than rounded, and a <c>Double</c> or <c>Single</c> that
/// writes a figure other than zero and reads as zero — a <c>DateTime</c> with no kind, and a kind on
/// any other type; and a Query's range that no grid could
/// ask for (ADR-0001). Reading never guesses a type and never drops a part it does not
/// understand.</para>
///
/// <para><b>What writing refuses</b>, with a <see cref="NotSupportedException"/>: an operand of a type
/// the filter model does not admit, or a <c>Double</c> or <c>Single</c> that is not finite, which
/// the filter refuses too (ADR-0023); and an Opaque Filter, whose meaning is the Consumer's alone
/// (ADR-0002), so the Consumer keeps it beside the View State and writes the filter without it. And,
/// with an <see cref="ArgumentException"/> naming the part, what reading would refuse, so that nothing
/// written fails to read back: a sort level, a column's filter, its clauses or a clause that is null;
/// a sort level or a filter that names no column; a sort direction, a combinator or an operator its
/// enum does not name; and a Query's range that is no range — a <see cref="RowRange"/> left at its
/// default, with no rows.</para>
///
/// <para>A <see cref="GridViewState"/>, a <see cref="ColumnViewState"/> or a
/// <see cref="ColumnWidth"/> handed to <c>System.Text.Json</c> directly is refused with a
/// <see cref="NotSupportedException"/> naming this entry point: written that way, an Auto width
/// threw, a Fixed one read back as Auto, and an operand as a <see cref="JsonElement"/>.</para>
/// </summary>
public static class GridViewStateJson
{
    /// <summary>The version this writer writes and the only one this reader reads (ADR-0207).</summary>
    public const int Version = 1;

    private const string AutoWidth = "Auto";
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";
    private const string DateOnlyFormat = "yyyy-MM-dd";
    private const string DateTimeOffsetFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly GridViewStateJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
    });

    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowDuplicateProperties = false };

    // ---- Writing --------------------------------------------------------------------------------

    /// <summary>A whole View State as JSON. Refuses an operand of a type the filter model does not
    /// admit, a non-finite <c>Double</c> or <c>Single</c>, and an Opaque Filter, with a
    /// <see cref="NotSupportedException"/> naming the part, and what reading would refuse with an
    /// <see cref="ArgumentException"/> naming it (the class lists both).</summary>
    /// <param name="viewState">The View State to write.</param>
    public static string Write(GridViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        var document = new ViewStateDocument
        {
            Version = Version,
            Columns = [.. viewState.Columns.Select(column => new ColumnDocument
            {
                Column = column.Column,
                Width = WidthElement(column.Width),
                Pinned = column.Pinned,
            })],
            Sorts = WriteSorts(viewState.Sorts, nameof(viewState)),
            Filter = WriteFilter(viewState.Filter, nameof(viewState)),
        };
        return JsonSerializer.Serialize(document, Context.ViewStateDocument);
    }

    /// <summary>A Query alone as JSON — for a Consumer's own server, or to keep the sort and the
    /// filter without the columns. Refuses what <see cref="Write"/> refuses.</summary>
    /// <param name="query">The Query to write.</param>
    public static string WriteQuery(GridQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Range.Count < 1)
        {
            // Only a RowRange left at its default holds no rows; its constructor refuses one (ADR-0001).
            throw new ArgumentException(FormattableString.Invariant(
                $"A Query's range is a run of at least one row, and {query.Range.Count} rows at {query.Range.Start} is not: a RowRange left at its default (ADR-0001)."),
                nameof(query));
        }
        var document = new QueryDocument
        {
            Version = Version,
            Range = new RangeDocument { Start = query.Range.Start, Count = query.Range.Count },
            Sorts = WriteSorts(query.Sorts ?? throw new ArgumentException("A Query's sorts are a list, empty for none.", nameof(query)), nameof(query)),
            Filter = WriteFilter(query.Filter, nameof(query)),
        };
        return JsonSerializer.Serialize(document, Context.QueryDocument);
    }

    private static JsonElement WidthElement(ColumnWidth width)
    {
        using var parsed = JsonDocument.Parse(width.IsAuto
            ? "\"" + AutoWidth + "\""
            : width.FixedPx.ToString("R", Invariant));
        return parsed.RootElement.Clone();
    }

    private static List<SortDocument> WriteSorts(IReadOnlyList<SortSpec> sorts, string parameter)
    {
        var written = new List<SortDocument>(sorts.Count);
        for (var i = 0; i < sorts.Count; i++)
        {
            var where = $"Sort level {i + 1}";
            var sort = sorts[i] ?? throw new ArgumentException($"{where} is null.", parameter);
            if (string.IsNullOrEmpty(sort.Column))
                throw new ArgumentException($"{where} names no column.", parameter);
            written.Add(new SortDocument { Column = sort.Column, Direction = Name(sort.Direction, where, parameter) });
        }
        return written;
    }

    private static FilterDocument? WriteFilter(GridFilter? filter, string parameter)
    {
        if (filter is null)
            return null;
        if (filter.Columns is null)
            throw new ArgumentException("The filter's columns are a dictionary, empty for none.", parameter);
        if (filter.Opaque is not null)
        {
            throw new NotSupportedException(
                "The filter's Opaque Filter has no JSON form here: its meaning is the Consumer's alone (ADR-0002, ADR-0023). "
                + "Keep it beside the View State, and write the filter without it (filter with { Opaque = null }) (ADR-0207).");
        }
        var columns = new Dictionary<string, FilterSpecDocument>(StringComparer.Ordinal);
        foreach (var (column, spec) in filter.Columns)
        {
            if (string.IsNullOrEmpty(column))
                throw new ArgumentException("The filter names no column.", parameter);
            var on = $"the filter on '{column}'";
            if (spec is null)
                throw new ArgumentException($"{Capital(on)} is null.", parameter);
            if (spec.Clauses is null)
                throw new ArgumentException($"{Capital(on)} has no list of clauses; its clauses are a list, empty for none.", parameter);
            columns[column] = new FilterSpecDocument
            {
                Combinator = Name(spec.Combinator, on, parameter),
                Clauses = [.. spec.Clauses.Select((clause, i) =>
                {
                    var where = $"{on}, clause {i + 1}";
                    if (clause is null)
                        throw new ArgumentException($"{Capital(where)} is null.", parameter);
                    return new ClauseDocument
                    {
                        Operator = Name(clause.Operator, where, parameter),
                        Value = clause.Value is null ? null : WriteOperand(clause.Value, where),
                        Values = clause.Values is null
                            ? null
                            : [.. clause.Values.Select((value, j) => value is null ? null : WriteOperand(value, $"{where}, value {j + 1}"))],
                    };
                })],
            };
        }
        return new FilterDocument { Columns = columns };
    }

    private static OperandDocument WriteOperand(object value, string where) => value switch
    {
        string text => Operand("String", text),
        bool boolean => Operand("Boolean", boolean ? "true" : "false"),
        decimal number => Operand("Decimal", number.ToString(Invariant)),
        double number when double.IsFinite(number) => Operand("Double", number.ToString("R", Invariant)),
        float number when float.IsFinite(number) => Operand("Single", number.ToString("R", Invariant)),
        double or float => throw new NotSupportedException(
            $"{Capital(where)}: {value} is not a finite number, which no filter takes (ADR-0023)."),
        long number => Operand("Int64", number.ToString(Invariant)),
        int number => Operand("Int32", number.ToString(Invariant)),
        short number => Operand("Int16", number.ToString(Invariant)),
        sbyte number => Operand("SByte", number.ToString(Invariant)),
        ulong number => Operand("UInt64", number.ToString(Invariant)),
        uint number => Operand("UInt32", number.ToString(Invariant)),
        ushort number => Operand("UInt16", number.ToString(Invariant)),
        byte number => Operand("Byte", number.ToString(Invariant)),
        DateTime date => new OperandDocument { Type = "DateTime", Value = date.ToString(DateTimeFormat, Invariant), Kind = Name(date.Kind) },
        DateOnly date => Operand("DateOnly", date.ToString(DateOnlyFormat, Invariant)),
        DateTimeOffset date => Operand("DateTimeOffset", date.ToString(DateTimeOffsetFormat, Invariant)),
        _ => throw new NotSupportedException(
            $"{Capital(where)}: an operand of type {value.GetType().Name} has no JSON form; a filter takes text, a Boolean, "
            + "a number, a DateTime, a DateOnly or a DateTimeOffset (ADR-0023, ADR-0207)."),
    };

    private static OperandDocument Operand(string type, string value) => new() { Type = type, Value = value };

    // ---- Reading --------------------------------------------------------------------------------

    /// <summary>
    /// A View State read back from <see cref="Write"/>'s JSON, every column it names checked against
    /// <paramref name="columns"/>: the names of every column the grid can show, hidden ones included.
    /// Refuses, with a <see cref="JsonException"/> naming the part, everything the class lists —
    /// among them a version other than <see cref="Version"/>, a column the grid does not have, a
    /// width that is neither form, an unknown operand type and a value that does not parse as its
    /// type.
    /// </summary>
    /// <param name="json">The JSON <see cref="Write"/> wrote.</param>
    /// <param name="columns">The names of the grid's columns, as <see cref="GridColumn{TRow}.Name"/>
    /// holds them.</param>
    public static GridViewState Read(string json, IEnumerable<string> columns)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(columns);
        var known = new HashSet<string>(columns, StringComparer.Ordinal);
        var document = Parse(json, "A View State", Context.ViewStateDocument);

        var shown = new List<ColumnViewState>(document.Columns.Count);
        var named = new HashSet<string>(StringComparer.Ordinal);
        var leading = true;
        for (var i = 0; i < document.Columns.Count; i++)
        {
            var column = document.Columns[i] ?? throw Refused($"The View State's column {i + 1} is null.");
            var name = Known(column.Column, known, $"The View State's column {i + 1}");
            if (!named.Add(name))
                throw Refused($"Column '{name}' appears twice in the View State's columns; a column stands in one place.");
            if (column.Pinned && !leading)
                throw Refused($"Column '{name}' is pinned after a column that is not; the pinned columns are the leading ones (ADR-0004).");
            leading &= column.Pinned;
            shown.Add(new ColumnViewState(name, ReadWidth(column.Width, name), column.Pinned));
        }
        return new GridViewState(shown, ReadSorts(document.Sorts, known), ReadFilter(document.Filter, known));
    }

    /// <summary>
    /// A Query read back from <see cref="WriteQuery"/>'s JSON. Refuses what <see cref="Read"/>
    /// refuses, with a <see cref="JsonException"/> naming the part, and a range no grid could ask
    /// for (ADR-0001). The column names it carries are not checked here — a server reading it has no
    /// grid — but where it is applied: <c>GridSource.From</c> refuses an unknown column by name
    /// (ADR-0023).
    /// </summary>
    /// <param name="json">The JSON <see cref="WriteQuery"/> wrote.</param>
    public static GridQuery ReadQuery(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = Parse(json, "A Query", Context.QueryDocument);
        var (start, count) = (document.Range.Start, document.Range.Count);
        if (start < 0 || count < 1)
        {
            throw Refused(FormattableString.Invariant(
                $"A Query's range is a run of at least one row at a position of 0 or more, and {count} rows at {start} is not (ADR-0001)."));
        }
        return new GridQuery(new RowRange(start, count), ReadSorts(document.Sorts, null), ReadFilter(document.Filter, null));
    }

    private static T Parse<T>(string json, string what, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            // One exception type for every refusal, whatever the parser's own subtype: text that is not
            // JSON, and a member named twice, which is never settled by whichever came last.
            throw new JsonException($"{what} cannot be read: {ex.Message}", ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
        }
        using var document = parsed;
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw Refused($"{what} is a JSON object.");
        // The version is read first and alone: a later version's members are not this version's, and
        // would be refused as unknown under a message that hides the reason (ADR-0207).
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            throw Refused($"{what} names its version as a number; this reader reads version {Version} (ADR-0207).");
        if (!version.TryGetInt32(out var number) || number != Version)
            throw Refused($"{what} of version {version.GetRawText()} cannot be read; this reader reads version {Version} (ADR-0207).");
        CheckShape(root, typeInfo, "$", "the document", what);
        try
        {
            return root.Deserialize(typeInfo) ?? throw Refused($"{what} is a JSON object.");
        }
        catch (JsonException ex)
        {
            // The shape was held to the form above, so the serializer has nothing left to refuse; were
            // it to, the refusal is still the form's, never the serializer's own words, which name the
            // internal types the form is read into.
            throw new JsonException($"{what} cannot be read at {ex.Path ?? "$"}: it is not in the form of version {Version} (ADR-0207).",
                ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
        }
    }

    // ---- The form's shape -----------------------------------------------------------------------
    //
    // Held on the parsed document before the serializer reads it, against the documents' own metadata
    // (GridViewStateJsonContext), so that a member this version does not have, a member it requires
    // that is missing or null, and a member of the wrong JSON type are refused in the form's words,
    // naming the member and where it stands — never in the serializer's, which name the internal types
    // the form is read into. A null inside a list or a dictionary is left to the reader, which refuses
    // it by what it stands for, or takes it: a Blank among In's operands.

    private static void CheckShape(JsonElement element, JsonTypeInfo type, string path, string label, string what)
    {
        switch (type.Kind)
        {
            case JsonTypeInfoKind.Object:
                if (element.ValueKind != JsonValueKind.Object)
                    throw Misshapen(what, path, $"{label} is an object, and here it is {Described(element)}");
                foreach (var member in element.EnumerateObject())
                {
                    var property = PropertyNamed(type, member.Name)
                        ?? throw Misshapen(what, path, $"it has no member '{member.Name}' in version {Version}");
                    CheckMember(member.Value, property, $"{path}.{member.Name}", what);
                }
                foreach (var property in type.Properties)
                {
                    if (property.IsRequired && !element.TryGetProperty(property.Name, out _))
                        throw Misshapen(what, path, $"its member '{property.Name}' is missing");
                }
                return;
            case JsonTypeInfoKind.Enumerable:
            {
                if (element.ValueKind != JsonValueKind.Array)
                    throw Misshapen(what, path, $"{label} is an array, and here it is {Described(element)}");
                var item = TypeInfoOf(type.ElementType!);
                var i = 0;
                foreach (var value in element.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.Null)
                        CheckShape(value, item, $"{path}[{i}]", $"item {i + 1} of {label}", what);
                    i++;
                }
                return;
            }
            case JsonTypeInfoKind.Dictionary:
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw Misshapen(what, path, $"{label} is an object, and here it is {Described(element)}");
                var entry = TypeInfoOf(type.ElementType!);
                foreach (var member in element.EnumerateObject())
                {
                    if (member.Value.ValueKind != JsonValueKind.Null)
                        CheckShape(member.Value, entry, $"{path}['{member.Name}']", $"'{member.Name}' in {label}", what);
                }
                return;
            }
            default:
                var fits = type.Type == typeof(string) ? element.ValueKind == JsonValueKind.String
                    : type.Type == typeof(bool) ? element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    : type.Type == typeof(int) ? element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _)
                    : type.Type == typeof(JsonElement);
                if (!fits)
                    throw Misshapen(what, path, $"{label} is {Expected(type)}, and here it is {Described(element)}");
                return;
        }
    }

    private static void CheckMember(JsonElement value, JsonPropertyInfo property, string path, string what)
    {
        // A width is any JSON here: ReadWidth refuses what is neither form, naming its column.
        if (property.PropertyType == typeof(JsonElement))
            return;
        var type = TypeInfoOf(property.PropertyType);
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (property.IsSetNullable)
                return;
            throw Misshapen(what, path, $"'{property.Name}' is null, and it is {Expected(type)}");
        }
        CheckShape(value, type, path, $"'{property.Name}'", what);
    }

    private static JsonPropertyInfo? PropertyNamed(JsonTypeInfo type, string name)
    {
        foreach (var property in type.Properties)
        {
            if (string.Equals(property.Name, name, StringComparison.Ordinal))
                return property;
        }
        return null;
    }

    private static JsonTypeInfo TypeInfoOf(Type type)
        => Context.GetTypeInfo(type) ?? throw new System.Diagnostics.UnreachableException($"The View State's form has no metadata for {type}.");

    private static string Expected(JsonTypeInfo type) => type.Kind switch
    {
        JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary => "an object",
        JsonTypeInfoKind.Enumerable => "an array",
        _ when type.Type == typeof(string) => "a string",
        _ when type.Type == typeof(bool) => "true or false",
        _ when type.Type == typeof(int) => "a whole number",
        _ => "a value of this form",
    };

    private static string Described(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => $"the number {element.GetRawText()}",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => "null",
    };

    private static JsonException Misshapen(string what, string path, string text)
        => new($"{what} cannot be read at {path}: {text} (ADR-0207).", path, null, null);

    private static string Known(string? column, HashSet<string>? known, string where)
    {
        if (string.IsNullOrEmpty(column))
            throw Refused($"{where} names no column.");
        if (known is not null && !known.Contains(column))
            throw Refused($"{where} names column '{column}', which the grid does not have (ADR-0207).");
        return column;
    }

    private static ColumnWidth ReadWidth(JsonElement width, string column)
    {
        if (width.ValueKind == JsonValueKind.String && width.ValueEquals(AutoWidth))
            return ColumnWidth.Auto;
        if (width.ValueKind == JsonValueKind.Number && width.TryGetDouble(out var px) && double.IsFinite(px) && px > 0)
            return ColumnWidth.Fixed(px);
        throw Refused(
            $"Column '{column}': a width is \"{AutoWidth}\" or a finite, positive number of pixels, and {width.GetRawText()} is neither (ADR-0016, ADR-0207).");
    }

    private static SortSpec[] ReadSorts(List<SortDocument> sorts, HashSet<string>? known)
    {
        var read = new SortSpec[sorts.Count];
        for (var i = 0; i < sorts.Count; i++)
        {
            var where = $"Sort level {i + 1}";
            var sort = sorts[i] ?? throw Refused($"{where} is null.");
            read[i] = new SortSpec(Known(sort.Column, known, where), ParseDirection(sort.Direction, where));
        }
        return read;
    }

    private static GridFilter? ReadFilter(FilterDocument? filter, HashSet<string>? known)
    {
        if (filter is null)
            return null;
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var (column, spec) in filter.Columns)
        {
            Known(column, known, "The filter");
            var where = $"the filter on '{column}'";
            if (spec is null)
                throw Refused($"{Capital(where)} is null.");
            var clauses = new FilterClause[spec.Clauses.Count];
            for (var i = 0; i < clauses.Length; i++)
            {
                var at = $"{where}, clause {i + 1}";
                var clause = spec.Clauses[i] ?? throw Refused($"{Capital(at)} is null.");
                clauses[i] = new FilterClause(
                    ParseOperator(clause.Operator, at),
                    clause.Value is null ? null : ReadOperand(clause.Value, at),
                    clause.Values is null
                        ? null
                        : [.. clause.Values.Select((value, j) => value is null ? null : ReadOperand(value, $"{at}, value {j + 1}"))]);
            }
            columns.Add(column, new FilterSpec(clauses, ParseCombinator(spec.Combinator, where)));
        }
        return new GridFilter(columns);
    }

    private static object ReadOperand(OperandDocument operand, string where)
    {
        var text = operand.Value;
        if (operand.Kind is not null && operand.Type != "DateTime")
            throw Refused($"{Capital(where)}: a kind belongs to a DateTime, and this operand is a {operand.Type}.");
        object? value = operand.Type switch
        {
            "String" => text,
            "Boolean" => text switch { "true" => true, "false" => false, _ => null },
            // Its exact digits, as the writer writes them: a text with more digits than a decimal holds
            // would be rounded, or flattened to zero, by the parse, and money read back as another
            // amount is quietly wrong (principle 1).
            "Decimal" => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant, out var number)
                         && string.Equals(number.ToString(Invariant), text, StringComparison.Ordinal) ? number : null,
            // A figure too small for the type reads as zero: refused, not flattened, unless it was written as zero.
            "Double" => double.TryParse(text, FloatStyles, Invariant, out var number) && double.IsFinite(number)
                        && (number != 0 || WrittenAsZero(text)) ? number : null,
            "Single" => float.TryParse(text, FloatStyles, Invariant, out var number) && float.IsFinite(number)
                        && (number != 0 || WrittenAsZero(text)) ? number : null,
            "Int64" => long.TryParse(text, NumberStyles.AllowLeadingSign, Invariant, out var number) ? number : null,
            "Int32" => int.TryParse(text, NumberStyles.AllowLeadingSign, Invariant, out var number) ? number : null,
            "Int16" => short.TryParse(text, NumberStyles.AllowLeadingSign, Invariant, out var number) ? number : null,
            "SByte" => sbyte.TryParse(text, NumberStyles.AllowLeadingSign, Invariant, out var number) ? number : null,
            "UInt64" => ulong.TryParse(text, NumberStyles.None, Invariant, out var number) ? number : null,
            "UInt32" => uint.TryParse(text, NumberStyles.None, Invariant, out var number) ? number : null,
            "UInt16" => ushort.TryParse(text, NumberStyles.None, Invariant, out var number) ? number : null,
            "Byte" => byte.TryParse(text, NumberStyles.None, Invariant, out var number) ? number : null,
            "DateTime" => ReadDateTime(text, operand.Kind, where),
            "DateOnly" => DateOnly.TryParseExact(text, DateOnlyFormat, Invariant, DateTimeStyles.None, out var date) ? date : null,
            "DateTimeOffset" => DateTimeOffset.TryParseExact(text, DateTimeOffsetFormat, Invariant, DateTimeStyles.None, out var date) ? date : null,
            _ => throw Refused(
                $"{Capital(where)}: '{operand.Type}' is not an operand type this reader knows; it reads String, Boolean, Decimal, "
                + "Double, Single, Int64, Int32, Int16, SByte, UInt64, UInt32, UInt16, Byte, DateTime, DateOnly and DateTimeOffset (ADR-0207)."),
        };
        return value ?? throw Refused($"{Capital(where)}: '{text}' is not a {operand.Type} written in the invariant culture (ADR-0207).");
    }

    private const NumberStyles FloatStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    /// <summary>Whether a floating-point text that parsed writes zero: no digit but 0 before its
    /// exponent. A text that writes another figure and reads as zero has underflowed the type.</summary>
    private static bool WrittenAsZero(string text)
    {
        var exponent = text.AsSpan().IndexOfAny('e', 'E');
        return !(exponent < 0 ? text.AsSpan() : text.AsSpan(0, exponent)).ContainsAnyInRange('1', '9');
    }

    private static object? ReadDateTime(string text, string? kind, string where)
    {
        var readKind = kind switch
        {
            "Unspecified" => DateTimeKind.Unspecified,
            "Utc" => DateTimeKind.Utc,
            "Local" => DateTimeKind.Local,
            null => throw Refused($"{Capital(where)}: a DateTime names its kind — Unspecified, Utc or Local (ADR-0207)."),
            _ => throw Refused($"{Capital(where)}: '{kind}' is not a DateTime kind; a kind is Unspecified, Utc or Local (ADR-0207)."),
        };
        // Read as the wall clock it was written as, then given its kind: a DateTime compares by its
        // wall-clock ticks (ADR-0023), so a Local one read on a machine in another zone is the same value.
        return DateTime.TryParseExact(text, DateTimeFormat, Invariant, DateTimeStyles.None, out var date)
            ? DateTime.SpecifyKind(date, readKind)
            : null;
    }

    // ---- Names ----------------------------------------------------------------------------------
    //
    // Written out rather than read from the enums, so that a name is the form's and not whatever a
    // rename would make it, and an integer or a different case is refused rather than taken.

    private static readonly (FilterOperator Value, string Name)[] Operators =
    [
        (FilterOperator.Equals, "Equals"), (FilterOperator.NotEquals, "NotEquals"),
        (FilterOperator.GreaterThan, "GreaterThan"), (FilterOperator.GreaterThanOrEqual, "GreaterThanOrEqual"),
        (FilterOperator.LessThan, "LessThan"), (FilterOperator.LessThanOrEqual, "LessThanOrEqual"),
        (FilterOperator.Contains, "Contains"), (FilterOperator.DoesNotContain, "DoesNotContain"),
        (FilterOperator.StartsWith, "StartsWith"), (FilterOperator.EndsWith, "EndsWith"),
        (FilterOperator.In, "In"), (FilterOperator.IsBlank, "IsBlank"), (FilterOperator.IsNotBlank, "IsNotBlank"),
    ];

    private static string Name(FilterOperator value, string where, string parameter)
    {
        foreach (var (member, name) in Operators)
        {
            if (member == value)
                return name;
        }
        throw new ArgumentException($"{Capital(where)}: {value} is not a FilterOperator this version names.", parameter);
    }

    private static FilterOperator ParseOperator(string name, string where)
    {
        foreach (var (member, known) in Operators)
        {
            if (string.Equals(known, name, StringComparison.Ordinal))
                return member;
        }
        throw Refused($"{Capital(where)}: '{name}' is not an operator this reader knows (ADR-0023, ADR-0207).");
    }

    private static string Name(SortDirection direction, string where, string parameter) => direction switch
    {
        SortDirection.Ascending => "Ascending",
        SortDirection.Descending => "Descending",
        _ => throw new ArgumentException($"{where}: {direction} is not a SortDirection this version names.", parameter),
    };

    private static SortDirection ParseDirection(string name, string where) => name switch
    {
        "Ascending" => SortDirection.Ascending,
        "Descending" => SortDirection.Descending,
        _ => throw Refused($"{where}: '{name}' is not a sort direction; a direction is Ascending or Descending (ADR-0207)."),
    };

    private static string Name(FilterCombinator combinator, string where, string parameter) => combinator switch
    {
        FilterCombinator.And => "And",
        FilterCombinator.Or => "Or",
        _ => throw new ArgumentException($"{Capital(where)}: {combinator} is not a FilterCombinator this version names.", parameter),
    };

    private static FilterCombinator ParseCombinator(string name, string where) => name switch
    {
        "And" => FilterCombinator.And,
        "Or" => FilterCombinator.Or,
        _ => throw Refused($"{Capital(where)}: '{name}' is not a combinator; a combinator is And or Or (ADR-0009, ADR-0207)."),
    };

    private static string Name(DateTimeKind kind) => kind switch
    {
        DateTimeKind.Unspecified => "Unspecified",
        DateTimeKind.Utc => "Utc",
        DateTimeKind.Local => "Local",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown DateTimeKind."),
    };

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static JsonException Refused(string message) => new(message);
}
