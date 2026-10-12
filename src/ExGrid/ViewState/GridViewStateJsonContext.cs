using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExGrid;

// The View State's JSON form, version 1 (ADR-0207), as the documents the source-generated context
// below reads and writes. Internal: a Consumer goes through GridViewStateJson and needs no options,
// no converters and no context of their own, and the form is the same under reflection and under
// trimming. A member is the part it names and nothing is guessed: the enum names, a width and an
// operand are read as text and turned into the model by GridViewStateJson, which refuses by name.

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ViewStateDocument))]
[JsonSerializable(typeof(QueryDocument))]
internal sealed partial class GridViewStateJsonContext : JsonSerializerContext;

/// <summary>A whole View State.</summary>
internal sealed class ViewStateDocument
{
    public required int Version { get; init; }
    public required List<ColumnDocument> Columns { get; init; }
    public required List<SortDocument> Sorts { get; init; }
    public FilterDocument? Filter { get; init; }
}

/// <summary>A Query alone.</summary>
internal sealed class QueryDocument
{
    public required int Version { get; init; }
    public required RangeDocument Range { get; init; }
    public required List<SortDocument> Sorts { get; init; }
    public FilterDocument? Filter { get; init; }
}

internal sealed class RangeDocument
{
    public required int Start { get; init; }
    public required int Count { get; init; }
}

/// <summary>One column: its name, its width — <c>"Auto"</c> or a number of pixels — and whether it
/// is pinned.</summary>
internal sealed class ColumnDocument
{
    public required string Column { get; init; }
    public required JsonElement Width { get; init; }
    public required bool Pinned { get; init; }
}

internal sealed class SortDocument
{
    public required string Column { get; init; }
    public required string Direction { get; init; }
}

internal sealed class FilterDocument
{
    public required Dictionary<string, FilterSpecDocument> Columns { get; init; }
}

internal sealed class FilterSpecDocument
{
    public required string Combinator { get; init; }
    public required List<ClauseDocument> Clauses { get; init; }
}

/// <summary>One condition: its operator, and the operand or the operands it takes; a null among the
/// operands of <c>In</c> selects Blanks (ADR-0023).</summary>
internal sealed class ClauseDocument
{
    public required string Operator { get; init; }
    public OperandDocument? Value { get; init; }
    public List<OperandDocument?>? Values { get; init; }
}

/// <summary>An operand with its type (ADR-0207): the tag names one of the types the filter model
/// admits, the value is its text in the invariant culture, and a <c>DateTime</c> names its kind.</summary>
internal sealed class OperandDocument
{
    public required string Type { get; init; }
    public required string Value { get; init; }
    public string? Kind { get; init; }
}
