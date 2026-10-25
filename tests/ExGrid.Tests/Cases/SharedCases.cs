using System.Text.Json;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// Reads the shared cases of <c>tests/cases/</c> (ADR-0350, section 5), which Vitest runs against the
/// Script Core as well. <c>tests/cases/README.md</c> says what a file holds. A case says what the grid
/// does, not how a language spells it: "no Focus" is <c>null</c> there, and a refusal is named by its
/// kind.
/// </summary>
internal static class SharedCases
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases");

    /// <summary>Every file of the area's directory whose kind is the one asked for, by name.</summary>
    public static IEnumerable<string> FilesOf(string area, string kind) =>
        Directory.GetFiles(Path.Combine(Root, area), "*.json")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Where(file => Read(area, file!).GetProperty("kind").GetString() == kind)!;

    /// <summary>The file, parsed.</summary>
    public static JsonElement Read(string area, string file)
    {
        using var stream = File.OpenRead(Path.Combine(Root, area, file));
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    /// <summary>Every case of every file of the kind, as (file, name) rows a theory runs.</summary>
    public static TheoryData<string, string> Of(string area, string kind)
    {
        var rows = new TheoryData<string, string>();
        foreach (var file in FilesOf(area, kind))
            foreach (var entry in Read(area, file).GetProperty("cases").EnumerateArray())
                rows.Add(file, entry.GetProperty("name").GetString()!);
        return rows;
    }

    /// <summary>The case of the file named so, and the file it is in.</summary>
    public static (JsonElement File, JsonElement Case) Find(string area, string file, string name)
    {
        var document = Read(area, file);
        foreach (var entry in document.GetProperty("cases").EnumerateArray())
        {
            if (entry.GetProperty("name").GetString() == name)
                return (document, entry);
        }
        throw new InvalidOperationException($"{area}/{file} holds no case named \"{name}\".");
    }

    /// <summary>A cell, as [row, column] or in A1 notation.</summary>
    public static CellPosition Cell(JsonElement text)
    {
        if (text.ValueKind == JsonValueKind.Array)
            return new(text[0].GetInt32(), text[1].GetInt32());
        var a1 = text.GetString()!;
        var i = 0;
        var column = 0;
        while (i < a1.Length && char.IsAsciiLetterUpper(a1[i]))
            column = column * 26 + (a1[i++] - 'A' + 1);
        if (i == 0 || i == a1.Length)
            throw new FormatException($"\"{a1}\" is not a cell in A1 notation.");
        return new(int.Parse(a1[i..], System.Globalization.CultureInfo.InvariantCulture) - 1, column - 1);
    }

    /// <summary>A range, as [top, left, rows, columns] or in A1 notation.</summary>
    public static SelectionRange Range(JsonElement text)
    {
        if (text.ValueKind == JsonValueKind.Array)
            return new(text[0].GetInt32(), text[1].GetInt32(), text[2].GetInt32(), text[3].GetInt32());
        return RangeOf(text.GetString()!);
    }

    private static SelectionRange RangeOf(string a1)
    {
        var parts = a1.Split(':');
        return SelectionRange.FromCorners(CellOf(parts[0]), CellOf(parts[^1]));
    }

    private static CellPosition CellOf(string a1) => Cell(JsonSerializer.SerializeToElement(a1));

    /// <summary>A list of ranges, as an array of ranges or as Excel's Selection.Address.</summary>
    public static SelectionRange[] Ranges(JsonElement text)
    {
        if (text.ValueKind == JsonValueKind.String)
        {
            var address = text.GetString()!;
            return address.Length == 0 ? [] : [.. address.Split(',').Select(RangeOf)];
        }
        return [.. text.EnumerateArray().Select(Range)];
    }

    /// <summary>A grid's extent, from [rows, columns].</summary>
    public static GridExtent Extent(JsonElement pair) => new(pair[0].GetInt32(), pair[1].GetInt32());

    /// <summary>
    /// Runs the action and holds it to the refusal named: <c>outOfRange</c> is an argument outside what
    /// the grid has, and <c>invalidOperation</c> anything else refused.
    /// </summary>
    public static void Refused(Action action, string refusal)
    {
        var thrown = Assert.ThrowsAny<Exception>(action);
        switch (refusal)
        {
            case "outOfRange":
                Assert.IsType<ArgumentOutOfRangeException>(thrown);
                break;
            case "invalidOperation":
                Assert.IsType<InvalidOperationException>(thrown);
                break;
            default:
                throw new InvalidOperationException($"\"{refusal}\" is not a refusal the cases name.");
        }
    }
}
