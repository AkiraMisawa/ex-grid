using System.Text.Json;
using System.Text.Json.Serialization;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0207: what ExGrid's JSON entry point for the View State refuses that only the C# core can be handed.
/// What reading and writing refuse, and the text written, are the shared cases of
/// <c>tests/cases/view-state</c> (ADR-0370 section 5), which <c>Cases/ViewStateJsonCaseTests</c> runs here
/// and Vitest runs against the Script Core. What stays here has no counterpart there: operands of .NET types
/// the Script Core does not have, a refusal's parameter name, and <c>System.Text.Json</c> handed a View State
/// directly or through a Consumer's own source-generated context.
/// </summary>
public class GridViewStateJsonTests
{
    public static TheoryData<object> Unwritable() => new()
    {
        Guid.Empty,
        new TimeOnly(9, 30),
        'x',
        new object(),
    };

    [Theory]
    [MemberData(nameof(Unwritable))] // ADR-0023 / ADR-0207: an operand of a .NET type the filter does not admit has no JSON form and is refused by name (the Script Core refuses an operand without its type, in its own tests)
    public void ADR0207_an_operand_the_filter_does_not_admit_is_refused_when_written(object operand)
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { ["Notional"] = new([new FilterClause(FilterOperator.Equals, operand)]) });

        var refusal = Assert.Throws<NotSupportedException>(() => GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), [], filter)));
        Assert.Contains("'Notional'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains($"an operand of type {operand.GetType().Name} has no JSON form", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => GridViewStateJson.Write(new GridViewState([], [], filter)));
    }

    [Fact] // ADR-0207: what reading would refuse is refused on the way out naming the parameter that held it, which the shared cases cannot say
    public void ADR0207_a_writing_refusal_names_its_parameter()
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { [""] = new([new FilterClause(FilterOperator.IsBlank)]) });

        Assert.Equal("query", Assert.Throws<ArgumentException>(() => GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), [], filter))).ParamName);
        Assert.Equal("viewState", Assert.Throws<ArgumentException>(() => GridViewStateJson.Write(new GridViewState([], [], filter))).ParamName);
    }

    // ---- What has no JSON form but the entry point's ------------------------------------------

    public static TheoryData<object> ViewStateParts() => new()
    {
        new GridViewState([new ColumnViewState("Book", ColumnWidth.Fixed(80))], [],
            new GridFilter(new Dictionary<string, FilterSpec> { ["Book"] = new([new FilterClause(FilterOperator.GreaterThan, 1_000_000m)]) })),
        new ColumnViewState("Book", ColumnWidth.Fixed(80)),
        ColumnWidth.Auto,
        ColumnWidth.Fixed(80),
    };

    [Theory]
    [MemberData(nameof(ViewStateParts))] // ADR-0207: a View State, a column of one or a width handed to System.Text.Json directly is refused by name, pointing to the entry point — written that way an Auto width threw, a Fixed one read back as Auto, and an operand as a JsonElement
    public void ADR0207_a_view_state_handed_to_system_text_json_directly_is_refused_by_name(object part)
    {
        var type = part.GetType();

        var refusal = Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(part, type));
        Assert.Contains($"A {type.Name} has no JSON form of its own", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("GridViewStateJson.Write", refusal.Message, StringComparison.Ordinal);
        refusal = Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize(part is ColumnWidth ? "120" : "{}", type));
        Assert.Contains("GridViewStateJson.Read", refusal.Message, StringComparison.Ordinal);
        // Inside a Consumer's own document, too.
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new Dictionary<string, object> { ["view"] = part }));
    }

    [Fact] // ADR-0207: under a Consumer's own source-generated context a View State is refused too, never written quietly wrong — the generator cannot reach the refusal, so the build warns (SYSLIB1220, naming it; SYSLIB1030) and nothing is generated for the type, which the serializer then refuses
    public void ADR0207_a_view_state_under_a_consumers_source_generated_context_is_refused()
    {
        var kept = new ConsumerSettings { View = new GridViewState([new ColumnViewState("Book", ColumnWidth.Fixed(80))], [], null) };

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(kept, ConsumerSettingsContext.Default.ConsumerSettings));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize("""{"View":{"columns":[],"sorts":[]}}""", ConsumerSettingsContext.Default.ConsumerSettings));
    }
}

/// <summary>A Consumer's own settings, holding a View State as a member, to be written by a
/// source-generated context of the Consumer's own (ADR-0207).</summary>
public sealed class ConsumerSettings
{
    /// <summary>The view kept.</summary>
    public GridViewState? View { get; set; }
}

// What a Consumer's build says when a View State is put into a context of their own: the refusal is
// internal, so the generator cannot use it (SYSLIB1220, naming OnlyThroughGridViewStateJson) and
// generates nothing for the type (SYSLIB1030). Both are suppressed in ExGrid.Tests.csproj, which says
// why, so the test can run.
[JsonSerializable(typeof(ConsumerSettings))]
internal sealed partial class ConsumerSettingsContext : JsonSerializerContext;
