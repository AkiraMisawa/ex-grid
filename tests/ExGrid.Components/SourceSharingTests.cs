using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ST-5 (ADR-0018 §5): a bundled Grid Source belongs to one circuit. Each bUnit context
/// has its own renderer, so two contexts stand for two users' circuits on one Server
/// process; two grids in one context stand for two grids on one page.
/// </summary>
public class SourceSharingTests : GridTestContext
{
    private sealed class OtherCircuit : GridTestContext;

    private static IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridTestContext context, IGridSource<TestRow> source) =>
        context.Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns()));

    public static TheoryData<string> BundledSources => ["From", "Fetch"];

    private static IGridSource<TestRow> Bundled(string kind) => kind switch
    {
        "From" => GridSource.From(TestRows.Window()),
        _ => GridSource.Fetch<TestRow>(
            (query, _) => ValueTask.FromResult(new GridPage<TestRow>(TestRows.Window(), 0, 3))),
    };

    [Theory] // ADR-0018: one bundled source, two circuits — refused by name, not shared quietly
    [MemberData(nameof(BundledSources))]
    public void A_bundled_source_refuses_a_grid_on_another_circuit(string kind)
    {
        var source = Bundled(kind);
        RenderGrid(this, source);
        using var other = new OtherCircuit();

        var refusal = Assert.Throws<InvalidOperationException>(() => RenderGrid(other, source));

        Assert.Contains("ADR-0018", refusal.Message);
        Assert.Contains("circuit", refusal.Message);
    }

    [Theory] // ADR-0018: two grids on one page sharing a source stay as they were
    [MemberData(nameof(BundledSources))]
    public void Two_grids_on_one_page_still_share_a_bundled_source(string kind)
    {
        var source = Bundled(kind);
        RenderGrid(this, source);

        var second = RenderGrid(this, source);

        Assert.Single(second.FindAll(".ex-grid"));
    }

    [Theory] // ADR-0018: the binding is released with the grid that held it
    [MemberData(nameof(BundledSources))]
    public async Task Another_circuit_may_attach_once_the_first_grid_is_gone(string kind)
    {
        var source = Bundled(kind);
        RenderGrid(this, source);
        await DisposeComponentsAsync();
        using var other = new OtherCircuit();

        var grid = RenderGrid(other, source);

        Assert.Single(grid.FindAll(".ex-grid"));
    }

    [Fact] // ADR-0018: a Consumer's own source is its own promise — the grid does not check it
    public void A_consumer_source_is_not_checked()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        RenderGrid(this, source);
        using var other = new OtherCircuit();

        var grid = RenderGrid(other, source);

        Assert.Equal(3, grid.FindComponents<ExGridRow<TestRow>>().Count);
    }
}
