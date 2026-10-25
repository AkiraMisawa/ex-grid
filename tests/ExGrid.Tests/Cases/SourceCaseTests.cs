using System.Text.Json;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// Grid Sources (<c>tests/cases/source</c>, kind <c>source</c>; ADR-0350 section 5), run against
/// <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/>: a source over the case's rows, and the steps
/// the grid takes on it in turn, each one thing it does to the source or what it then reads.
/// </summary>
public class SourceCaseTests
{
    private const string Area = "source";

    /// <summary>A row of a case: its values, and the place in the case's rows it stands for, which every
    /// version of it keeps. A new object for every version, since identity is what says a row changed.</summary>
    private sealed class Row(int position, object?[] values)
    {
        public int Position { get; } = position;

        public object?[] Values { get; } = values;
    }

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "source");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a source case holds on the C# core
    public async Task Source(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var declared = entry.TryGetProperty("columns", out var own) ? own : document.GetProperty("columns");
        var columns = declared.EnumerateArray()
            .Select((column, index) => new ColumnInfo<Row>(
                column.GetProperty("name").GetString()!,
                QueryCaseTests.Member<ColumnType>(column.GetProperty("type")),
                row => row.Values[index],
                DateType: column.TryGetProperty("dateType", out var dateType) ? QueryCaseTests.Member<DateType>(dateType) : DateType.DateTime))
            .ToArray();
        var held = entry.GetProperty("rows").EnumerateArray().Select((written, position) => RowOf(position, written)).ToArray();
        var source = GridSource.From<Row>(held);
        var heard = 0;
        source.StateChanged += () => heard++;

        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("expect", out var expected))
            {
                Check(expected, source, heard);
                heard = 0;
                continue;
            }
            if (!step.TryGetProperty("throws", out var refusal))
            {
                await Take(step);
                continue;
            }
            SharedCases.Refused(() => Take(step).GetAwaiter().GetResult(), refusal.GetString()!,
                step.TryGetProperty("saying", out var saying) ? saying.GetString() : null);
        }

        async Task Take(JsonElement step)
        {
            if (step.TryGetProperty("setColumns", out var names))
            {
                source.OnColumnsChanged(names.ValueKind == JsonValueKind.True
                    ? columns
                    : names.EnumerateArray().Select(n => columns.Single(c => c.Name == n.GetString())).ToArray());
            }
            else if (step.TryGetProperty("setSorts", out var sorts))
            {
                source.OnSortChanged(sorts.EnumerateArray()
                    .Select(sort => new SortSpec(sort.GetProperty("column").GetString()!, QueryCaseTests.Member<SortDirection>(sort.GetProperty("direction"))))
                    .ToArray());
            }
            else if (step.TryGetProperty("setFilter", out var filter))
            {
                source.OnFilterChanged(filter.ValueKind == JsonValueKind.Null ? null : QueryCaseTests.FilterOf(filter));
            }
            else if (step.TryGetProperty("replaceRow", out var target))
            {
                var row = target.ValueKind == JsonValueKind.Number
                    ? held[target.GetInt32()]
                    : RowOf(-1, target.GetProperty("stranger"));
                var written = step.GetProperty("with");
                var replacement = written.ValueKind == JsonValueKind.String && written.GetString() == "itself"
                    ? row
                    : RowOf(row.Position, written);
                source.ReplaceRow(row, replacement);
                if (row.Position >= 0)
                    held[row.Position] = replacement;
            }
            else if (step.TryGetProperty("needRange", out var range))
            {
                await source.OnRangeNeededAsync(RangeOf(range));
            }
            else if (step.TryGetProperty("rows", out var asked))
            {
                var rows = await source.GetRowsAsync(RangeOf(asked), CancellationToken.None);
                Assert.Equal(step.GetProperty("is").EnumerateArray().Select(p => p.GetInt32()), rows.Select(r => r.Position));
            }
            else if (step.TryGetProperty("distinctValues", out var column))
            {
                var answer = await source.GetDistinctValuesAsync(column.GetString()!, CancellationToken.None);
                if (step.TryGetProperty("tooMany", out _))
                    Assert.True(answer.IsTooMany);
                else
                    Assert.Equal(step.GetProperty("is").EnumerateArray().Select(QueryCaseTests.Value), answer.Values);
            }
            else
            {
                throw new InvalidOperationException($"A step of \"{name}\" takes nothing: {step}.");
            }
        }
    }

    private static Row RowOf(int position, JsonElement written)
        => new(position, written.EnumerateArray().Select(QueryCaseTests.Value).ToArray());

    private static RowRange RangeOf(JsonElement range) => new(range.GetProperty("start").GetInt32(), range.GetProperty("count").GetInt32());

    private static void Check(JsonElement expected, InMemoryGridSource<Row> source, int heard)
    {
        if (expected.TryGetProperty("window", out var window))
            Assert.Equal(window.EnumerateArray().Select(p => p.GetInt32()), source.Window.Select(r => r.Position));
        if (expected.TryGetProperty("values", out var values))
        {
            var rows = values.EnumerateArray().Select(row => row.EnumerateArray().Select(QueryCaseTests.Value).ToArray()).ToArray();
            Assert.Equal(rows.Length, source.Window.Count);
            for (var i = 0; i < rows.Length; i++)
                Assert.Equal(rows[i], source.Window[i].Values);
        }
        if (expected.TryGetProperty("windowStart", out var start))
            Assert.Equal(start.GetInt32(), source.WindowStart);
        if (expected.TryGetProperty("totalCount", out var total))
            Assert.Equal(total.ValueKind == JsonValueKind.Null ? null : total.GetInt32(), source.TotalCount);
        if (expected.TryGetProperty("loading", out var loading))
            Assert.Equal(loading.GetBoolean(), source.IsLoading);
        if (expected.TryGetProperty("version", out var version))
            Assert.Equal(version.GetInt32(), source.RowSequenceVersion);
        if (expected.TryGetProperty("sorts", out var sorts))
        {
            Assert.Equal(
                sorts.EnumerateArray().Select(sort => new SortSpec(sort.GetProperty("column").GetString()!, QueryCaseTests.Member<SortDirection>(sort.GetProperty("direction")))),
                source.Sorts);
        }
        if (expected.TryGetProperty("filter", out var filter))
        {
            Assert.True(GridFilters.Equal(filter.ValueKind == JsonValueKind.Null ? null : QueryCaseTests.FilterOf(filter), source.Filter),
                $"the filter in force is not {filter}");
        }
        if (expected.TryGetProperty("heard", out var times))
            Assert.Equal(times.GetInt32(), heard);
    }
}
