using Xunit;

namespace ExGrid.Tests;

public class AllowedOperatorTests
{
    [Fact] // ADR-0009: the allowlist the engine validates against cannot be mutated through a cast
    public void The_allowlist_is_not_a_castable_mutable_array()
    {
        foreach (var type in Enum.GetValues<ColumnType>())
            Assert.IsNotType<FilterOperator[]>(FilterOperators.AllowedFor(type));
    }
}
