using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;

namespace ExGrid.Data.Tests.Cases;

/// <summary>
/// The shared CSV suggestion cases (<c>tests/cases/data</c>; ADR-0064 Q33, ADR-0370 section 3), run against
/// ExGrid.Data: a Schema is suggested for each case's file, read from a stream that hands over as much as is
/// asked of it, and the suggestion, with every mark's words, or how it was refused, is held to the case. The
/// Script Core's runner is <c>src/npm/data/test/csv-suggest-cases.test.ts</c>; <c>tests/cases/README.md</c> says
/// what a file holds. With <c>EXGRID_WRITE_DATA_CASES=1</c>, the runner writes what ExGrid.Data answers into
/// each case of the source files instead.
/// </summary>
public class CsvSuggestCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "data");

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = CsvReadCaseTests.Read(path);
            if ((string?)document["kind"] != "csvSuggest")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0064 (Q33): a CSV suggestion case holds on ExGrid.Data
    public void Csv_suggest(string file, string name)
    {
        var entry = CsvReadCaseTests.Read(Path.Combine(Root, file))["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        var answer = Answer(entry);
        if (CsvReadCaseTests.Writing)
        {
            CsvReadCaseTests.Write(file, name, answer);
            return;
        }
        var expected = CsvReadCaseTests.Must(entry, "expect");
        Assert.True(JsonNode.DeepEquals(expected, answer), $"Expected {expected.ToJsonString()}, answered {answer.ToJsonString()}.");
    }

    /// <summary>The suggestion made, as the TypeScript `CsvSuggestion` writes one, or how it was refused.</summary>
    private static JsonNode Answer(JsonObject entry)
    {
        CsvSuggestion suggestion;
        try
        {
            var bytes = CsvReadCaseTests.Bytes(CsvReadCaseTests.Must(entry, "file"));
            var options = Options(entry["options"]?.AsObject());
            suggestion = CsvSchema.SuggestAsync(new MemoryStream(bytes), options, TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
        }
        catch (SnapshotException refusal)
        {
            return new JsonObject { ["refused"] = new JsonObject { ["message"] = refusal.Message } };
        }
        catch (ArgumentException)
        {
            return new JsonObject { ["refused"] = "argument" };
        }
        return new JsonObject
        {
            ["schema"] = Schema(suggestion.Schema),
            ["columns"] = new JsonArray([.. suggestion.Columns.Select(column => (JsonNode)new JsonObject
            {
                ["marks"] = Marks(column.Marks),
                ["isUnclear"] = column.IsUnclear,
                ["examples"] = new JsonArray([.. column.Examples.Select(e => (JsonNode)e)]),
            })]),
            ["marks"] = Marks(suggestion.Marks),
            ["sampledRows"] = suggestion.SampledRows,
            ["isUnclear"] = suggestion.IsUnclear,
        };
    }

    private static JsonArray Marks(IReadOnlyList<CsvMark> marks)
        => new([.. marks.Select(m => (JsonNode)new JsonObject { ["doubt"] = Camel(m.Doubt.ToString()), ["note"] = m.Note })]);

    /// <summary>The Schema as the TypeScript `CsvSchema` writes one: a key left unset is left out.</summary>
    private static JsonObject Schema(CsvSchema schema)
    {
        var columns = new JsonArray();
        foreach (var column in schema.Columns)
        {
            var written = new JsonObject { ["name"] = column.Name, ["kind"] = column.Kind.ToString().ToLowerInvariant() };
            if (column.Position is { } position)
                written["position"] = position;
            if (column.DecimalPoint is { } point)
                written["decimalPoint"] = point;
            if (column.ThousandsSeparator is { } thousands)
                written["thousandsSeparator"] = thousands;
            if (column.DateFormats is { } formats)
                written["dateFormats"] = new JsonArray([.. formats.Select(f => (JsonNode)f)]);
            if (column.BlankText is { } blanks)
                written["blankText"] = new JsonArray([.. blanks.Select(b => (JsonNode)b)]);
            columns.Add(written);
        }
        return new JsonObject
        {
            ["columns"] = columns,
            ["separator"] = Camel(schema.Separator.ToString()),
            ["hasHeader"] = schema.HasHeader,
            ["encoding"] = schema.Encoding == CsvEncoding.ShiftJis ? "shift-jis" : "utf-8",
        };
    }

    /// <summary>The case's options, as the TypeScript `CsvSuggestionOptions` writes them. A separator or an
    /// encoding the cases do not know stands for one ExGrid.Data does not either.</summary>
    private static CsvSuggestionOptions Options(JsonObject? written)
    {
        if (written is null)
            return new CsvSuggestionOptions();
        return new CsvSuggestionOptions
        {
            Rows = written["rows"] is { } rows ? (int)rows : 1_000,
            Encoding = written["encoding"] is { } encoding
                ? (string)encoding! switch
                {
                    "utf-8" => CsvEncoding.Utf8,
                    "shift-jis" => CsvEncoding.ShiftJis,
                    _ => null!,
                }
                : CsvEncoding.Utf8,
            Separator = written["separator"] is { } separator
                ? (string)separator! switch
                {
                    "comma" => CsvSeparator.Comma,
                    "tab" => CsvSeparator.Tab,
                    "semicolon" => CsvSeparator.Semicolon,
                    _ => (CsvSeparator)99,
                }
                : null,
            HasHeader = (bool?)written["hasHeader"],
            Culture = written["culture"] is { } culture ? CultureInfo.GetCultureInfo((string)culture!) : null,
        };
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
