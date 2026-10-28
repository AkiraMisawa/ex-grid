using System.Text.Json;
using ExGrid.Selection;

namespace ExGrid.Script;

/// <summary>
/// The spellings the bridge and .NET share: an enum's member is the Script Core's string of the same name in
/// camelCase, and a position, a range and a Selection are the core's objects as JSON. A name the bridge sends
/// that no member answers is refused, never read as another.
/// </summary>
internal static class ScriptJson
{
    /// <summary>A member's name as the Script Core spells it.</summary>
    internal static string Camel<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = Enum.GetName(value) ?? throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a member of {typeof(TEnum).Name}.");
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>The member the Script Core's string names.</summary>
    internal static TEnum Member<TEnum>(JsonElement text) where TEnum : struct, Enum
    {
        var name = text.GetString();
        if (string.IsNullOrEmpty(name)
            || !Enum.TryParse<TEnum>(char.ToUpperInvariant(name[0]) + name[1..], ignoreCase: false, out var member)
            || !Enum.IsDefined(member)
            || Camel(member) != name)
        {
            throw new InvalidOperationException($"Internal: the Script Core sent '{name}', which no {typeof(TEnum).Name} names.");
        }
        return member;
    }

    internal static CellPosition Cell(JsonElement json) => new(json.GetProperty("row").GetInt32(), json.GetProperty("column").GetInt32());

    internal static SelectionRange Range(JsonElement json) => new(
        json.GetProperty("topRow").GetInt32(), json.GetProperty("leftColumn").GetInt32(),
        json.GetProperty("rowCount").GetInt32(), json.GetProperty("columnCount").GetInt32());

    internal static IReadOnlyList<SelectionRange> Ranges(JsonElement json) => [.. json.EnumerateArray().Select(Range)];

    internal static IReadOnlyList<string> Texts(JsonElement json) => [.. json.EnumerateArray().Select(text => text.GetString()
        ?? throw new InvalidOperationException("Internal: the Script Core sent null among texts."))];

    /// <summary>A text member the Script Core always sends.</summary>
    internal static string Text(JsonElement json, string name)
        => json.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Internal: the Script Core sent no {name}.");

    /// <summary>A member that may be absent or null.</summary>
    internal static bool Present(JsonElement json, string name, out JsonElement value)
        => json.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    /// <summary>A Selection as the bridge writes it: its ranges, its Focus, and which range holds it.</summary>
    internal static GridSelection Selection(JsonElement json)
    {
        var ranges = Ranges(json.GetProperty("ranges"));
        // An empty Selection has no Focus to read.
        if (ranges.Count == 0)
            return GridSelection.Empty;
        return GridSelection.Reported(ranges, Cell(json.GetProperty("focus")), json.GetProperty("focusRangeIndex").GetInt32());
    }
}
