using ExGrid.Cells;
using Xunit;

namespace ExGrid.Tests;

public class CellStateTests
{
    [Fact] // ADR-0006: a cell nobody annotated is Normal, so the default value has to be Normal
    public void The_default_state_is_normal()
    {
        Assert.Equal(CellState.Normal, default(CellState));
    }
}
