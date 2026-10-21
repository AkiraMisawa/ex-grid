using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>A stream's schema answers its shape before any row (ADR-0341).</summary>
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

    [Fact] // ADR-0341: an Arrow file says its schema after its rows, so its shape is not read from it
    public async Task A_file_is_refused_as_saying_its_schema_last()
    {
        using var file = new MemoryStream([.. "ARROW1\0\0"u8, .. new byte[64]]);

        var refused = await Assert.ThrowsAsync<SnapshotException>(async () => await SnapshotArrow.ReadShapeAsync(file, TestContext.Current.CancellationToken));

        Assert.StartsWith("An Arrow file says its schema in its footer", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0341: bytes that are not an Arrow stream say no shape
    public async Task Bytes_that_are_not_arrow_are_refused()
    {
        using var text = new MemoryStream("""{"columns": []}"""u8.ToArray());

        await Assert.ThrowsAsync<SnapshotException>(async () => await SnapshotArrow.ReadShapeAsync(text, TestContext.Current.CancellationToken));
    }
}
