using ExGrid.Data;
using Xunit;

namespace ExGrid.Tests;

/// <summary>The bundled fetching source answers its shape before any row (ADR-0341).</summary>
public class FetchedShapeTests
{
    private sealed record Trade(string Desk);

    [Fact] // ADR-0341: the source says the shape its server answers, and none when given none, fetching no row
    public async Task The_fetching_source_says_its_servers_shape_and_fetches_no_row()
    {
        var fetched = 0;
        using var source = GridSource.Fetch<Trade>((query, cancellation) =>
        {
            fetched++;
            return ValueTask.FromResult(new GridPage<Trade>([], 0, 0));
        });
        Assert.Null(await source.ShapeAsync(TestContext.Current.CancellationToken));

        var shape = new DataShape([new DataShapeColumn("desk", SnapshotKind.Text)]);
        source.Shape = _ => ValueTask.FromResult(shape);

        Assert.Same(shape, await new BoardInput("trades", [new("desk", SnapshotKind.Text)]).CheckAsync(source, TestContext.Current.CancellationToken));
        Assert.Equal(0, fetched);
    }
}
