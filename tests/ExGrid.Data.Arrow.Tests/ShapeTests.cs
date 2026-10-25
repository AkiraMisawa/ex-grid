using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// A stream's schema answers its shape before any row (ADR-0341): read from the Consumer's Stream, it leaves
/// the stream at its first record batch, which the shared cases (<see cref="Cases.ArrowCaseTests"/>), handed
/// bytes, cannot see. The shapes and their refusals are cases under <c>tests/cases/arrow/</c>.
/// </summary>
public class ShapeTests
{
    [Fact] // ADR-0341: the shape is the schema's, read from its first message alone, by ADR-0065's table
    public async Task A_streams_shape_is_read_from_its_schema_alone()
    {
        var snapshot = Deals().Build(Make(70_000));
        var bytes = await WriteAsync(snapshot);
        using var stream = new MemoryStream(bytes);

        var shape = await SnapshotArrow.ReadShapeAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(DataShape.Of(snapshot).Columns, shape.Columns);
        // Only the schema was read: the stream stands at its first record batch, far short of the rows.
        Assert.InRange(stream.Position, 1, 4096);
    }
}
