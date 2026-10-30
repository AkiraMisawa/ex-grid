using ExGrid.Rows;
using Xunit;

namespace ExGrid.Tests;

public class RowKindTests
{
    [Fact] // ADR-0024: a row nobody said anything about is a detail row
    public void The_default_kind_is_detail()
    {
        Assert.Equal(RowKind.Detail, default(RowKind));
    }
}
