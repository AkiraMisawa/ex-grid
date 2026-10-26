using System.Globalization;
using System.Text.Json;

namespace ExGrid.Data;

/// <summary>
/// A Board Input (ADR-0341): the name a Board gives a piece of its data, and each column's name and
/// kind, written by whoever composed the Board while they knew what the data means. A workflow binds
/// it to a source at run time; the source's shape is checked against it before any row is drawn, and
/// a mismatch is refused naming the input and the column. Kinds are never inferred from rows.
/// </summary>
public sealed class BoardInput
{
    private static readonly string[] DeclarationKeys = ["columns"];
    private static readonly string[] ColumnKeys = ["name", "type"];

    private static readonly Dictionary<string, SnapshotKind> Kinds = new(StringComparer.Ordinal)
    {
        ["text"] = SnapshotKind.Text,
        ["decimal"] = SnapshotKind.Decimal,
        ["double"] = SnapshotKind.Double,
        ["integer"] = SnapshotKind.Integer,
        ["date"] = SnapshotKind.Date,
        ["boolean"] = SnapshotKind.Boolean,
    };

    private static readonly string[] DateForms =
        ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"];

    /// <summary>An input named <paramref name="name"/> declaring <paramref name="columns"/>, in
    /// order. An empty name, no columns, a column named twice and a kind a Snapshot does not hold are
    /// refused.</summary>
    public BoardInput(string name, IEnumerable<DataShapeColumn> columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Shape = new DataShape(columns);
        if (Shape.Columns.Count == 0)
            throw new ArgumentException($"Input '{name}' declares no column.", nameof(columns));
    }

    /// <summary>The input's name, as the Board's views name it in their <c>data</c>.</summary>
    public string Name { get; }

    /// <summary>The columns declared, in order.</summary>
    public IReadOnlyList<DataShapeColumn> Columns => Shape.Columns;

    /// <summary>The declaration as a shape.</summary>
    public DataShape Shape { get; }

    /// <summary>The JSON name of each kind, as a declaration writes it: <c>text</c>, <c>decimal</c>,
    /// <c>double</c>, <c>integer</c>, <c>date</c> and <c>boolean</c>.</summary>
    public static IReadOnlyCollection<string> KindNames => Kinds.Keys;

    /// <summary>The JSON name of <paramref name="kind"/>.</summary>
    public static string NameOf(SnapshotKind kind) =>
        Kinds.First(k => k.Value == kind).Key;

    /// <summary>
    /// Reads a declaration as a Board writes it, <c>{"columns": [{"name": "desk", "type": "text"}, …]}</c>.
    /// A member the declaration does not have, a missing or empty name, a type that is none of
    /// <see cref="KindNames"/>, a column named twice and an empty list are refused, naming the path.
    /// </summary>
    /// <param name="name">The input's name, the key it stands under in the Board.</param>
    /// <param name="declaration">The declaration.</param>
    /// <param name="path">Where the declaration stands, such as <c>inputs.trades</c>, for the refusal.</param>
    /// <exception cref="BoardRefusedException">The declaration cannot be read.</exception>
    public static BoardInput Read(string name, JsonElement declaration, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);
        if (name.Length == 0)
            throw new BoardRefusedException(path, "An input has an empty name.");
        if (declaration.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException(path, $"Input '{name}' is not an object.");
        foreach (var member in declaration.EnumerateObject())
        {
            if (Array.IndexOf(DeclarationKeys, member.Name) < 0)
                throw new BoardRefusedException($"{path}.{member.Name}", $"Input '{name}' has no member '{member.Name}'.");
        }
        if (!declaration.TryGetProperty("columns", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new BoardRefusedException($"{path}.columns", $"Input '{name}' declares its columns as a list.");
        var columns = new List<DataShapeColumn>();
        var named = new HashSet<string>(StringComparer.Ordinal);
        var i = 0;
        foreach (var column in list.EnumerateArray())
        {
            var at = $"{path}.columns[{i++}]";
            if (column.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException(at, $"A column of input '{name}' is not an object.");
            foreach (var member in column.EnumerateObject())
            {
                if (Array.IndexOf(ColumnKeys, member.Name) < 0)
                    throw new BoardRefusedException($"{at}.{member.Name}", $"A column of input '{name}' has no member '{member.Name}'.");
            }
            if (!column.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String || nameValue.GetString() is not { Length: > 0 } columnName)
                throw new BoardRefusedException($"{at}.name", $"A column of input '{name}' has no name.");
            if (!named.Add(columnName))
                throw new BoardRefusedException($"{at}.name", $"Input '{name}' declares column '{columnName}' twice.");
            if (!column.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String
                || !Kinds.TryGetValue(typeValue.GetString()!, out var kind))
            {
                throw new BoardRefusedException($"{at}.type",
                    $"Column '{columnName}' of input '{name}' declares no type; it is one of {string.Join(", ", Kinds.Keys)}, never guessed from the data.");
            }
            columns.Add(new DataShapeColumn(columnName, kind));
        }
        if (columns.Count == 0)
            throw new BoardRefusedException($"{path}.columns", $"Input '{name}' declares no column.");
        return new BoardInput(name, columns);
    }

    /// <summary>
    /// Checks a source's shape against the declaration: every declared column is there, of the kind
    /// declared. A column the source has and the input does not declare is no mismatch; the views
    /// cannot name it.
    /// </summary>
    /// <exception cref="BoardRefusedException">A declared column is missing, or of another kind; the
    /// message names the input and the column.</exception>
    public void Check(DataShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        foreach (var declared in Columns)
        {
            if (!shape.TryGetColumn(declared.Name, out var held))
                throw new BoardRefusedException(Name, $"Input '{Name}' declares column '{declared.Name}', which its source does not have.");
            if (held.Kind != declared.Kind)
            {
                throw new BoardRefusedException(Name,
                    $"Input '{Name}', column '{declared.Name}': declared {NameOf(declared.Kind)}, and its source holds {NameOf(held.Kind)}.");
            }
        }
    }

    /// <summary>
    /// Asks <paramref name="source"/> its shape, before any row, and checks it against the declaration
    /// (<see cref="Check"/>). A source that says no shape is refused: its kinds are never inferred from
    /// rows, which a first page of Blanks, or <c>1</c> beside <c>1.5</c>, would get wrong.
    /// </summary>
    /// <returns>The shape the source answered.</returns>
    /// <exception cref="BoardRefusedException">The source says no shape, or one that does not match the
    /// declaration; the message names the input and the column.</exception>
    public async ValueTask<DataShape> CheckAsync(IShapedSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var shape = await source.ShapeAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new BoardRefusedException(Name, $"Input '{Name}' is bound to a source that does not say its shape, and its kinds are not guessed from its rows.");
        Check(shape);
        return shape;
    }

    /// <summary>Binds <paramref name="snapshot"/> to this input: its shape checked, and the Snapshot
    /// itself returned, unchanged.</summary>
    /// <exception cref="BoardRefusedException">Its shape does not match the declaration.</exception>
    public Snapshot Bind(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Check(DataShape.Of(snapshot));
        return snapshot;
    }

    /// <summary>
    /// Data that arrived as JSON, a list of objects, made a Snapshot by the declared kinds: one column
    /// per declared column, in order. A member missing or <c>null</c> is a Blank; a member the input
    /// does not declare is not read. A value that does not convert is refused by input, row and
    /// column, never dropped or read as zero: a <c>decimal</c> is a JSON number or its digits as a
    /// string, read exactly; a <c>double</c> a number, or <c>"NaN"</c>, <c>"Infinity"</c> or
    /// <c>"-Infinity"</c>; an <c>integer</c> a whole number, or its digits as a string; a
    /// <c>date</c> a string <c>yyyy-MM-dd</c>, or with a time <c>yyyy-MM-ddTHH:mm[:ss[.fffffff]]</c>,
    /// and never with an offset, whose clock value would be guessed; <c>text</c> a string, and a
    /// <c>boolean</c> <c>true</c> or <c>false</c>.
    /// </summary>
    /// <param name="json">The rows, a JSON array of objects.</param>
    /// <exception cref="BoardRefusedException">The JSON is not a list of objects, or a value does
    /// not convert; the path is <c>input[row].column</c>, the row counted from 0.</exception>
    public Snapshot FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            throw new BoardRefusedException(Name, $"The data of input '{Name}' is not JSON: {e.Message}");
        }
        using (document)
            return FromJson(document.RootElement);
    }

    /// <summary><see cref="FromJson(string)"/> for rows already parsed.</summary>
    /// <exception cref="BoardRefusedException">The rows are not a list of objects, or a value does
    /// not convert.</exception>
    public Snapshot FromJson(JsonElement rows)
    {
        if (rows.ValueKind != JsonValueKind.Array)
            throw new BoardRefusedException(Name, $"The data of input '{Name}' is not a list of rows.");
        var builder = new SnapshotColumnsBuilder();
        var appenders = Columns.Select(c => Appender(builder, c)).ToArray();
        var row = 0;
        foreach (var element in rows.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException($"{Name}[{row}]", string.Create(CultureInfo.InvariantCulture, $"Input '{Name}', row {row + 1:N0}: is not an object."));
            for (var c = 0; c < Columns.Count; c++)
            {
                var column = Columns[c];
                if (!element.TryGetProperty(column.Name, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    builder.Columns[c].AppendBlank();
                    continue;
                }
                if (!appenders[c](value))
                {
                    throw new BoardRefusedException($"{Name}[{row}].{column.Name}", string.Create(CultureInfo.InvariantCulture,
                        $"Input '{Name}', row {row + 1:N0}, column '{column.Name}': {Shown(value)} is not {Article(column.Kind)}."));
                }
            }
            row++;
        }
        return builder.Build();
    }

    private static Func<JsonElement, bool> Appender(SnapshotColumnsBuilder builder, DataShapeColumn column)
    {
        const NumberStyles Digits = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        var invariant = CultureInfo.InvariantCulture;
        switch (column.Kind)
        {
            case SnapshotKind.Text:
                var text = builder.Text(column.Name);
                return v =>
                {
                    if (v.ValueKind != JsonValueKind.String)
                        return false;
                    text.Append(v.GetString());
                    return true;
                };
            case SnapshotKind.Decimal:
                var money = builder.Decimal(column.Name);
                return v =>
                {
                    decimal number = 0;
                    var converts = v.ValueKind == JsonValueKind.Number ? v.TryGetDecimal(out number)
                        : v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), Digits, invariant, out number);
                    // A decimal parse rounds digits past what a decimal holds, which would hand on money that
                    // was never written: such a value is refused, never rounded (spine rule 1).
                    converts = converts && Exact(v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString()!, number);
                    if (converts)
                        money.Append(number);
                    return converts;
                };
            case SnapshotKind.Double:
                var real = builder.Double(column.Name);
                return v =>
                {
                    double? number = v.ValueKind switch
                    {
                        JsonValueKind.Number when v.TryGetDouble(out var n) && double.IsFinite(n) => n,
                        JsonValueKind.String => v.GetString() switch
                        {
                            "NaN" => double.NaN,
                            "Infinity" => double.PositiveInfinity,
                            "-Infinity" => double.NegativeInfinity,
                            _ => null,
                        },
                        _ => null,
                    };
                    if (number is { } value)
                        real.Append(value);
                    return number is not null;
                };
            case SnapshotKind.Integer:
                var whole = builder.Integer(column.Name);
                return v =>
                {
                    long number = 0;
                    var converts = v.ValueKind == JsonValueKind.Number ? v.TryGetInt64(out number)
                        : v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.AllowLeadingSign, invariant, out number);
                    if (converts)
                        whole.Append(number);
                    return converts;
                };
            case SnapshotKind.Date:
                var date = builder.Date(column.Name);
                return v =>
                {
                    if (v.ValueKind != JsonValueKind.String
                        || !DateTime.TryParseExact(v.GetString(), DateForms, invariant, DateTimeStyles.None, out var clock))
                        return false;
                    date.Append(clock);
                    return true;
                };
            case SnapshotKind.Boolean:
                var flag = builder.Boolean(column.Name);
                return v =>
                {
                    if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        return false;
                    flag.Append(v.GetBoolean());
                    return true;
                };
            default:
                throw new InvalidOperationException($"Unknown kind {column.Kind}.");
        }
    }

    // Whether the number written as raw (a JSON number, or the digits decimal.TryParse read) is exactly parsed.
    private static bool Exact(string raw, decimal parsed)
    {
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"^([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[4].Success ? match.Groups[4].Value : "0", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
            return false;
        var digits = match.Groups[2].Value + match.Groups[3].Value;
        var written = System.Numerics.BigInteger.Parse(digits.Length == 0 ? "0" : digits, CultureInfo.InvariantCulture);
        if (match.Groups[1].Value == "-")
            written = -written;
        var places = match.Groups[3].Value.Length - exponent;
        var bits = decimal.GetBits(parsed);
        var held = (new System.Numerics.BigInteger((uint)bits[2]) << 64) | (new System.Numerics.BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
        if (bits[3] < 0)
            held = -held;
        var scale = (bits[3] >> 16) & 0xFF;
        // Both at the larger of the two scales, compared whole. A scale far past the other's, from an exponent, can
        // only match where both are zero, and is not raised to a power to find that out.
        if (Math.Abs(places - scale) > digits.Length + 30)
            return written.IsZero && held.IsZero;
        return places > scale
            ? held * System.Numerics.BigInteger.Pow(10, places - scale) == written
            : written * System.Numerics.BigInteger.Pow(10, scale - places) == held;
    }

    private static string Shown(JsonElement value)
    {
        var raw = value.GetRawText();
        return raw.Length <= 40 ? raw : string.Concat(raw.AsSpan(0, 37), "...");
    }

    private static string Article(SnapshotKind kind) => kind switch
    {
        SnapshotKind.Integer => "an integer",
        SnapshotKind.Text => "text",
        _ => "a " + NameOf(kind),
    };
}
