using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ExGrid.Data;
using ExGrid.Summarizing;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>
/// DA-18's inspection, made a test: the eleven Aggregations have one definition, in
/// <c>ExGrid.Data</c>, and both <c>ExPivot.Engine</c> and ExGrid's Selection Summary answer through it
/// (ADR-0130/0060). Read from each compiled assembly's own references, so a copy of the arithmetic
/// written into either one, and read instead of ExGrid.Data's, shows as a reference that is gone.
/// </summary>
public class OneAggregationDefinitionTests
{
    // The methods of ExGrid.Data's Aggregation types an assembly calls, as "Type.Method".
    private static HashSet<string> CallsIntoTheDefinition(System.Reflection.Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var calls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
                continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(type.Namespace) != "ExGrid.Data")
                continue;
            var name = reader.GetString(type.Name);
            if (name is "AggregateArithmetic" or "AggregateAccumulator")
                calls.Add($"{name}.{reader.GetString(member.Name)}");
        }
        return calls;
    }

    [Fact] // ADR-0130 / ADR-0060 / DA-18: ExPivot.Engine finishes and reads every Aggregation through ExGrid.Data's arithmetic
    public void DA18_the_pivot_engine_answers_through_ExGrid_Data()
    {
        var calls = CallsIntoTheDefinition(typeof(PivotAggregation).Assembly);

        Assert.Contains("AggregateArithmetic.Read", calls);
        Assert.Contains("AggregateArithmetic.Finish", calls);
    }

    [Fact] // ADR-0130 / ADR-0060 / DA-18: ExGrid's Selection Summary folds its values into ExGrid.Data's accumulator and reads its answers there
    public void DA18_the_selection_summary_answers_through_ExGrid_Data()
    {
        var calls = CallsIntoTheDefinition(typeof(GridSummary).Assembly);

        Assert.Contains("AggregateAccumulator.Add", calls);
        Assert.Contains("AggregateAccumulator.Read", calls);
    }

    [Fact] // ADR-0130 / DA-18: the two products name the same eleven Aggregations — one list, not two that may drift
    public void DA18_the_pivot_names_exactly_the_eleven_aggregations_of_ExGrid_Data()
    {
        Assert.Equal(11, Enum.GetValues<Aggregation>().Length);
        Assert.Equal(
            Enum.GetNames<Aggregation>().Order(StringComparer.Ordinal),
            Enum.GetNames<PivotAggregation>().Order(StringComparer.Ordinal));
    }
}
