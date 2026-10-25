using System.Globalization;
using System.Text.Json.Nodes;
using ExPivot.Engine;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>A report as the shared pivot cases read it back (<c>tests/cases/README.md</c>), as
/// <c>src/npm/pivot-engine/test/readings.ts</c> reads one: its leaf headers, a line a row, its Header Group
/// spans, and the readings <c>also</c> asks for. tools/pivot-oracle compiles it too.</summary>
internal static class PivotReadings
{
    /// <summary>A report as a case reads it.</summary>
    public static JsonObject Read(PivotReport report, IReadOnlyList<string> also, PivotCases.DataSet data)
    {
        var read = new JsonObject
        {
            ["headers"] = Strings(Pivot.Headers(report)),
            ["lines"] = Strings(Pivot.Lines(report)),
            ["spans"] = Strings(Pivot.Spans(report)),
        };
        foreach (var more in also)
        {
            read[more] = more switch
            {
                "names" => Strings(report.LabelColumns.Select(c => c.Name).Concat(report.ValueColumns.Select(c => c.Name))),
                "keys" => new JsonArray([.. report.Rows.Select(r => Key(r.Key))]),
                "numbers" => Numbers(report),
                "captions" => Strings(report.ValueCaptions),
                "empty" => JsonValue.Create(report.IsEmpty),
                "toggles" => new JsonArray([.. report.Rows.Select(r => new JsonArray([.. r.Labels.Select(l => Toggle(l.Toggle))]))]),
                "columns" => new JsonArray([.. report.ValueColumns.Select(c => new JsonObject { ["role"] = PivotCases.Name(c.Role), ["valueField"] = c.ValueField })]),
                "tiers" => JsonValue.Create(report.HeaderTierCount),
                "keyCalls" => Strings(data.KeyCalls.Order(StringComparer.Ordinal)),
                _ => throw new InvalidOperationException($"'{more}' is not a reading of a report the cases name."),
            };
        }
        return read;
    }

    // Each row's numbers: a label's number where it shows one, then each value's — its exact decimal where
    // it was computed in one, its double otherwise, its error's text for an error, null for an empty cell —
    // and each value column's header number.
    private static JsonObject Numbers(PivotReport report)
    {
        var rows = new JsonArray();
        foreach (var row in report.Rows)
        {
            var labels = new JsonArray([.. row.Labels.Select(l => l.Number is { } n ? JsonValue.Create(Double(n)) : null)]);
            var values = new JsonArray();
            for (var column = 0; column < report.ValueColumns.Count; column++)
            {
                var value = report.ValueAt(row, column);
                values.Add(value switch
                {
                    null => null,
                    { Error: { } error } => JsonValue.Create(error),
                    { Exact: { } exact } => JsonValue.Create("m" + exact.ToString(CultureInfo.InvariantCulture)),
                    _ => JsonValue.Create(Double(value.Number)),
                });
            }
            rows.Add(new JsonObject { ["labels"] = labels, ["values"] = values });
        }
        return new JsonObject
        {
            ["rows"] = rows,
            ["headers"] = new JsonArray([.. report.ValueColumns.Select(c => c.HeaderNumber is { } n ? JsonValue.Create(Double(n)) : null)]),
        };
    }

    // A label's button: its field, its Item, the Item's label and whether it is collapsed.
    private static JsonNode? Toggle(PivotToggle? toggle) => toggle is null ? null : new JsonObject
    {
        ["field"] = toggle.Field,
        ["item"] = PivotCases.ItemKey(toggle.Item),
        ["itemLabel"] = toggle.ItemLabel,
        ["isCollapsed"] = toggle.IsCollapsed,
    };

    // A row's key: its role, its Value Field and its Items.
    private static JsonObject Key(PivotRowKey key) => new()
    {
        ["role"] = PivotCases.Name(key.Role),
        ["valueField"] = key.ValueField,
        ["items"] = new JsonArray([.. key.Items.Select(PivotCases.ItemKey)]),
    };

    // A double as a case writes one: its shortest round-trip text, which JSON numbers cannot always carry.
    private static string Double(double value) => "d" + value.ToString("R", CultureInfo.InvariantCulture);

    private static JsonArray Strings(IEnumerable<string> strings) => new([.. strings.Select(s => JsonValue.Create(s))]);
}
