using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The value list's reference semantics (ADR-0009) that the shared cases
/// (<c>tests/cases/source/from.json</c>) do not hold: the cap, and a fetching source with no delegate.
/// </summary>
public class DistinctValueTests
{
    private sealed record Trade(string Book, string Currency);

    [Fact] // ADR-0009: past the cap the honest answer is TooMany, not a truncated list
    public async Task Past_the_cap_the_answer_is_too_many()
    {
        var rows = new Trade[InMemoryGridSource<Trade>.DistinctValueCap + 1];
        for (var i = 0; i < rows.Length; i++)
            rows[i] = new Trade($"B{i}", "USD");
        var source = GridSource.From(rows);
        source.OnColumnsChanged([new ColumnInfo<Trade>("Book", ColumnType.Text, t => t.Book)]);

        var answer = await source.GetDistinctValuesAsync("Book", CancellationToken.None);

        Assert.True(answer.IsTooMany);
        Assert.Empty(answer.Values);
    }

    [Fact] // ADR-0009: a fetching source without a delegate answers TooMany, honestly
    public async Task A_fetching_source_without_a_delegate_degrades()
    {
        var source = GridSource.Fetch<Trade>((query, ct) =>
            ValueTask.FromResult(new GridPage<Trade>([], 0, 0)));

        var answer = await source.GetDistinctValuesAsync("Book", CancellationToken.None);

        Assert.True(answer.IsTooMany);
    }
}
