using System.Globalization;
using System.Text.Json.Nodes;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// What the C# engine answers to a Pivot Source case (kind <c>pivotSource</c>; <see cref="PivotSourceCaseTests"/>
/// says what a case holds). tools/pivot-oracle compiles it too, so a differential run's random cases are
/// answered by the very runner the shared cases are.
/// </summary>
internal static class PivotSourceCases
{
    // A source and how often its server was asked for Leaf Aggregates.
    private sealed class Bench
    {
        public required PivotSource Source { get; set; }
        public required Func<PivotSource> Again { get; init; }
        public required PivotCases.DataSet Data { get; init; }
        public int Asked { get; set; }
        public string? Version { get; set; }
    }

    /// <summary>The C# engine's answer to a source case: each step's answer, or the refusal that made the
    /// source.</summary>
    public static async Task<JsonNode> Answer(JsonObject document, JsonObject entry, CancellationToken ct)
    {
        Bench bench;
        try
        {
            bench = Open(document, entry);
        }
        catch (ArgumentException thrown)
        {
            return PivotCases.Refusal(thrown);
        }
        var options = PivotCases.Options(document, entry);
        var steps = new JsonArray();
        foreach (var node in PivotCases.Must(entry, "asks").AsArray())
        {
            try
            {
                steps.Add(await Ask(bench, node!.AsObject(), options, ct));
            }
            catch (Exception thrown) when (thrown is ArgumentException or InvalidOperationException)
            {
                steps.Add(PivotCases.Refusal(thrown));
            }
        }
        return steps;
    }

    // The source a case asks: over the data set's Snapshot or its records, and behind a fetching source when
    // the case names one.
    private static Bench Open(JsonObject document, JsonObject entry)
    {
        var snapshot = PivotCases.SnapshotOf(document, entry);
        var data = snapshot is null ? PivotCases.Data(document, entry) : new PivotCases.DataSet([], [], []);
        PivotField[]? declared = entry["declare"] is { } fields ? [.. fields.AsArray().Select(f => Declared(f!.AsObject()))] : null;
        Func<PivotSource> bundled = snapshot is null
            ? () => PivotSource.From(data.Records, data.Fields)
            : () => PivotSource.From(snapshot, declared);
        var fetch = entry["fetch"]?.AsObject();
        Bench? bench = null;
        PivotSource Make()
        {
            var inner = bundled();
            if (fetch is null)
                return inner;
            var features = fetch["offers"] is { } offers
                ? new PivotSourceFeatures(offers.AsArray().Select(a => PivotCases.Member<PivotAggregation>((string)a!)))
                : inner.Features;
            var instead = fetch["answers"] is { } other ? Query(other.AsObject()) : null;
            return PivotSource.Fetch(inner.Fields, features,
                (query, ct) =>
                {
                    bench!.Asked++;
                    return inner.AggregateAsync(instead ?? query, ct);
                },
                (query, ct) => inner.ItemsAsync(query, ct),
                (query, ct) => inner.DetailsAsync(query, ct));
        }
        bench = new Bench { Source = Make(), Again = Make, Data = data };
        return bench;
    }

    private static async Task<JsonNode> Ask(Bench bench, JsonObject step, PivotOptions options, CancellationToken ct)
    {
        var source = bench.Source;
        if (step["fields"] is not null)
            return new JsonArray([.. source.Fields.Select(Field)]);
        if (step["aggregate"] is { } asked)
        {
            var answer = await source.AggregateAsync(Query(asked.AsObject()), ct);
            if (answer.IsRefused)
                return Refused(answer.Refusal!, null);
            bench.Version = answer.SourceVersion;
            return AnswerJson(answer);
        }
        if (step["items"] is { } field)
        {
            var under = Under(bench, step);
            var page = await source.ItemsAsync(new PivotItemsQuery((string)field!, under, (string?)step["search"], step["max"] is { } max ? (int)max : PivotItemsQuery.DefaultMax), ct);
            if (page.IsRefused)
                return Refused(page.Refusal!, under);
            return new JsonObject
            {
                ["version"] = page.SourceVersion == under ? "asked" : page.SourceVersion,
                ["total"] = page.Total,
                ["items"] = new JsonArray([.. page.Items.Select(PivotCases.ItemKey)]),
            };
        }
        if (step["details"] is { } cell)
        {
            var under = Under(bench, step);
            var page = await source.DetailsAsync(Details(under, cell.AsObject()), ct);
            if (page.IsRefused)
                return Refused(page.Refusal!, under);
            return new JsonObject
            {
                ["version"] = page.SourceVersion == under ? "asked" : page.SourceVersion,
                ["start"] = page.Start,
                ["total"] = page.Total,
                ["fields"] = new JsonArray([.. page.Fields.Select(f => JsonValue.Create(f.Name))]),
                ["records"] = new JsonArray([.. page.Records.Select(r => (JsonNode)new JsonArray([.. r.Values.Select(DetailValue)]))]),
            };
        }
        if (step["report"] is { } written)
        {
            var layout = PivotCases.Layout(written);
            var query = PivotQuery.For(layout);
            var answer = await source.AggregateAsync(query, ct);
            if (answer.IsRefused)
                return Refused(answer.Refusal!, null);
            var report = PivotEngine.Report(PivotEngine.Cube(query, answer, source.Fields), layout, options);
            var also = step["also"] is { } readings ? readings.AsArray().Select(r => (string)r!).ToArray() : [];
            return PivotReadings.Read(report, also, bench.Data);
        }
        if (step["cube"] is { } cubed)
        {
            var answer = await source.AggregateAsync(Query(PivotCases.Must(step, "from").AsObject()), ct);
            PivotEngine.Cube(Query(cubed.AsObject()), answer, source.Fields);
            return new JsonObject { ["built"] = true };
        }
        if (step["asked"] is not null)
            return JsonValue.Create(bench.Asked);
        if (step["refresh"] is not null)
        {
            bench.Source = bench.Again();
            return new JsonObject { ["refreshed"] = true };
        }
        throw new InvalidOperationException($"{step.ToJsonString()} asks nothing the cases ask.");
    }

    // The version a page is asked under: the last answer's, or "stale", one no source held.
    private static string Under(Bench bench, JsonObject step)
        => (string?)step["under"] == "stale" ? "stale"
            : bench.Version ?? throw new InvalidOperationException("A page is asked for under an answer's version, and no question was answered yet.");

    // ---- What a case writes ----------------------------------------------------------------------

    /// <summary>A field declared over a Snapshot's columns: its name, type, caption, format, and the column
    /// it reads, or the part of a Date column it is.</summary>
    private static PivotField Declared(JsonObject declared)
    {
        var name = (string)declared["name"]!;
        if (declared["datePart"] is { } part)
            return PivotField.DatePartOf(name, (string)PivotCases.Must(declared, "column")!, PivotCases.Member<PivotDatePart>((string)part!), (string?)declared["caption"]);
        return new PivotField(name, PivotCases.Member<PivotFieldType>((string)declared["type"]!), (string?)declared["caption"], (string?)declared["format"])
        {
            Column = (string?)declared["column"],
        };
    }

    /// <summary>A question as a case writes it: each placed field by name, or with its Hidden Items; each
    /// value by name (its counts), or with the parts it asks for; and the most leaves.</summary>
    internal static PivotQuery Query(JsonObject written)
    {
        IReadOnlyList<PivotQueryField> Placed(string area) => written[area] is { } fields
            ? [.. fields.AsArray().Select(f => f is JsonValue
                ? new PivotQueryField((string)f!)
                : new PivotQueryField((string)f!["field"]!, [.. (f["hiddenItems"]?.AsArray() ?? []).Select(k => PivotCases.ItemKeyOf(k!))]))]
            : [];
        IReadOnlyList<PivotQueryValue> values = written["values"] is { } asked
            ? [.. asked.AsArray().Select(v => v is JsonValue
                ? new PivotQueryValue((string)v!)
                : new PivotQueryValue((string)v!["field"]!, (v["parts"]?.AsArray() ?? []).Aggregate(PivotParts.Counts, (parts, p) => parts | PivotCases.Member<PivotParts>((string)p!))))]
            : [];
        return new PivotQuery(Placed("rows"), Placed("columns"), Placed("filters"), values, written["maxLeaves"] is { } max ? (int)max : PivotQuery.DefaultMaxLeaves);
    }

    // The records behind a cell as a case writes them: the row's and the column's Items, each a field and
    // its Item's key, the Hidden Items of placed fields, and the page.
    private static PivotDetailsQuery Details(string version, JsonObject written)
    {
        IReadOnlyList<PivotFieldItem> Path(string key) => written[key] is { } path
            ? [.. path.AsArray().Select(s => new PivotFieldItem((string)s!["field"]!, PivotCases.ItemKeyOf(s["item"]!)))]
            : [];
        IReadOnlyList<PivotQueryField> hidden = written["hiddenItems"] is { } fields
            ? [.. fields.AsArray().Select(f => new PivotQueryField((string)f!["field"]!, [.. f["hiddenItems"]!.AsArray().Select(k => PivotCases.ItemKeyOf(k!))]))]
            : [];
        return new PivotDetailsQuery(version, Path("rows"), Path("columns"), hidden,
            written["start"] is { } start ? (int)start : 0, written["count"] is { } count ? (int)count : int.MaxValue);
    }

    // ---- What a source says ----------------------------------------------------------------------

    private static JsonNode Field(PivotField field)
    {
        var json = new JsonObject { ["name"] = field.Name, ["caption"] = field.Caption, ["type"] = PivotCases.Name(field.Type) };
        if (field.Format is { } format)
            json["format"] = format;
        if (field.Column is { } column)
            json["column"] = column;
        if (field.DatePart is { } part)
            json["datePart"] = PivotCases.Name(part);
        return json;
    }

    // A refusal by its kind, the field and the cap it names, and its message, the version asked under
    // written {version} where it names it.
    private static JsonObject Refused(PivotSourceRefusal refusal, string? under)
    {
        var json = new JsonObject { ["refused"] = PivotCases.Name(refusal.Kind) };
        if (refusal.Field is { } field)
            json["field"] = field;
        if (refusal.Limit is { } limit)
            json["limit"] = limit;
        json["saying"] = under is null ? refusal.Message : refusal.Message.Replace(under, "{version}", StringComparison.Ordinal);
        return json;
    }

    /// <summary>The Leaf Aggregates as a case writes them: the leaves, each axis's field and Items, each
    /// leaf's Item at every level (rows, then columns) and its records, and each value's parts and what it
    /// carries at every leaf, its numbers to the last bit.</summary>
    internal static JsonObject AnswerJson(PivotAnswer answer)
    {
        JsonArray Axes(IReadOnlyList<PivotAnswerAxis> axes) => new([.. axes.Select(a => (JsonNode)new JsonObject
        {
            ["field"] = a.Field,
            ["items"] = new JsonArray([.. a.Items.Select(PivotCases.ItemKey)]),
        })]);
        var levels = answer.Rows.Concat(answer.Columns).ToArray();
        var leaves = new JsonArray();
        for (var leaf = 0; leaf < answer.LeafCount; leaf++)
        {
            leaves.Add(new JsonObject
            {
                ["at"] = new JsonArray([.. levels.Select(a => JsonValue.Create(a.ItemAt(leaf)))]),
                ["records"] = answer.RecordsAt(leaf),
            });
        }
        var values = new JsonArray();
        foreach (var value in answer.Values)
        {
            var at = new JsonArray();
            for (var leaf = 0; leaf < answer.LeafCount; leaf++)
            {
                var carried = new JsonObject { ["values"] = value.ValuesAt(leaf), ["numbers"] = value.NumbersAt(leaf) };
                if (value.NonFiniteAt(leaf))
                    carried["nonFinite"] = true;
                if ((value.Parts & PivotParts.Sum) != 0)
                    carried["sum"] = Number(value.SumAt(leaf));
                if ((value.Parts & PivotParts.Extremes) != 0)
                {
                    carried["min"] = Number(value.MinAt(leaf));
                    carried["max"] = Number(value.MaxAt(leaf));
                }
                if ((value.Parts & PivotParts.Product) != 0)
                    carried["product"] = Double(value.ProductAt(leaf));
                if ((value.Parts & PivotParts.Variance) != 0)
                {
                    carried["mean"] = Double(value.MeanAt(leaf));
                    carried["m2"] = Double(value.M2At(leaf));
                }
                at.Add(carried);
            }
            values.Add(new JsonObject
            {
                ["field"] = value.Field,
                ["parts"] = new JsonArray([.. new[] { PivotParts.Sum, PivotParts.Extremes, PivotParts.Product, PivotParts.Variance }
                    .Where(p => (value.Parts & p) != 0).Select(p => JsonValue.Create(PivotCases.Name(p)))]),
                ["leaves"] = at,
            });
        }
        return new JsonObject
        {
            ["leaves"] = answer.LeafCount,
            ["rows"] = Axes(answer.Rows),
            ["columns"] = Axes(answer.Columns),
            ["leaf"] = leaves,
            ["values"] = values,
        };
    }

    // An exact number as {"decimal": its value, trailing zeros dropped}, a double as a number, or as
    // {"double": …} where JSON cannot hold it or would drop its sign (-0).
    private static JsonNode Number(PivotNumber number) => number.IsExact ? Exact(number.ExactValue) : Double(number.Value);

    private static JsonNode Exact(decimal exact) => new JsonObject { ["decimal"] = exact.ToString("G29", CultureInfo.InvariantCulture) };

    private static JsonNode Double(double number)
    {
        if (number == 0 && double.IsNegative(number))
            return new JsonObject { ["double"] = "-0" };
        return double.IsFinite(number) ? JsonValue.Create(number) : new JsonObject { ["double"] = number.ToString(CultureInfo.InvariantCulture) };
    }

    // A value a record behind a cell carries, as a case writes a value.
    private static JsonNode? DetailValue(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        decimal exact => Exact(exact),
        double number => Double(number),
        DateTime date => new JsonObject { ["date"] = DateText(date) },
        _ => throw new InvalidOperationException($"A record behind a cell carries {value.GetType().Name}, which no case writes."),
    };

    /// <summary>A date as a case writes it: <c>yyyy-MM-dd</c>, with its time where it has one, and no
    /// trailing zeros after the second.</summary>
    internal static string DateText(DateTime when) => when.TimeOfDay == TimeSpan.Zero
        ? when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : when.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
}
