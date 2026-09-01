using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The value list's reference semantics (ADR-0009), against GridSource.From — the
/// reference implementation: the distinct values under all applied filters except the
/// column's own.
/// </summary>
public class DistinctValueTests
{
    private sealed record Trade(string Book, string Currency);

    private static InMemoryGridSource<Trade> Source(out ColumnInfo<Trade>[] columns)
    {
        var source = GridSource.From<Trade>(
        [
            new("Alpha", "USD"),
            new("Alpha", "EUR"),
            new("Beta", "JPY"),
            new("Beta", "USD"),
        ]);
        columns =
        [
            new ColumnInfo<Trade>("Book", ColumnType.Text, t => t.Book),
            new ColumnInfo<Trade>("Currency", ColumnType.Text, t => t.Currency),
        ];
        source.OnColumnsChanged(columns);
        return source;
    }

    [Fact] // ADR-0009 / FL-2: another column's filter narrows the list
    public async Task The_list_reflects_the_other_columns_filters()
    {
        var source = Source(out _);
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.Equals, "Alpha")]),
        }));

        var currencies = await source.GetDistinctValuesAsync("Currency", CancellationToken.None);

        Assert.Equal(["USD", "EUR"], currencies.Values);
    }

    [Fact] // ADR-0009 / FL-2: the column's own filter is excluded — never a dead end
    public async Task The_columns_own_filter_is_excluded()
    {
        var source = Source(out _);
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.Equals, "Alpha")]),
        }));

        // Opening Book itself still offers Book's full domain.
        var books = await source.GetDistinctValuesAsync("Book", CancellationToken.None);

        Assert.Equal(["Alpha", "Beta"], books.Values);
    }

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

    [Fact] // ADR-0023: a Blank appears as a null entry, so the panel can offer it
    public async Task A_blank_appears_as_a_null_entry()
    {
        var source = GridSource.From<Trade>([new("Alpha", "USD"), new(null!, "EUR")]);
        source.OnColumnsChanged([new ColumnInfo<Trade>("Book", ColumnType.Text, t => t.Book)]);

        var books = await source.GetDistinctValuesAsync("Book", CancellationToken.None);

        Assert.Equal(["Alpha", null], books.Values);
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
