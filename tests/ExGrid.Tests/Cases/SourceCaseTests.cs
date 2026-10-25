using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
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
    /// version of it keeps. A new object for every version, since identity is what says a row changed. Two
    /// rows with the same values are equal, as two records are, so a source that found a row by value
    /// rather than by identity would take a twin or a stranger for it (ADR-0007).</summary>
    private sealed class Row(int position, object?[] values) : IEquatable<Row>
    {
        public int Position { get; } = position;

        public object?[] Values { get; } = values;

        public bool Equals(Row? other) => other is not null && Values.SequenceEqual(other.Values);

        public override bool Equals(object? obj) => Equals(obj as Row);

        public override int GetHashCode() => Values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value));
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
        var clock = new FakeTimeProvider();
        var keyAt = entry.TryGetProperty("rowKey", out var keyName)
            ? declared.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList().IndexOf(keyName.GetString())
            : -1;
        Func<Row, object>? rowKey = keyAt < 0 ? null : row => row.Values[keyAt]!;
        InMemoryGridSource<Row> Make() => rowKey is null ? GridSource.From<Row>(held) : GridSource.From<Row>(held, rowKey, clock);
        if (entry.TryGetProperty("fromThrows", out var fromThrows))
        {
            SharedCases.Refused(() => Make(), fromThrows.GetString()!,
                entry.TryGetProperty("fromSaying", out var fromSaying) ? fromSaying.GetString() : null);
            return;
        }
        var source = Make();
        // The object last handed to the source under each key.
        var handed = rowKey is null ? new Dictionary<object, Row>() : held.ToDictionary(rowKey);
        Row Hand(Row row)
        {
            if (rowKey is not null)
                handed[rowKey(row)] = row;
            return row;
        }
        Row EntryOf(JsonElement written) => written.ValueKind == JsonValueKind.Array
            ? RowOf(-1, written)
            : handed[QueryCaseTests.Value(written.GetProperty("same"))!];
        var heard = 0;
        var lastWindow = source.Window;
        source.StateChanged += () => heard++;

        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("expect", out var expected))
            {
                Check(expected, source, heard);
                if (expected.TryGetProperty("windowKept", out var kept))
                    Assert.Equal(kept.GetBoolean(), ReferenceEquals(source.Window, lastWindow));
                if (expected.TryGetProperty("vouches", out var vouches))
                    Assert.Equal(vouches.GetBoolean(), ((IGridSource<Row>)source).VouchesDistinctRows);
                if (expected.TryGetProperty("gatherInterval", out var interval))
                    Assert.Equal(TimeSpan.FromMilliseconds(interval.GetDouble()), source.GatherInterval);
                if (expected.TryGetProperty("keys", out var keys))
                {
                    Assert.Equal(keys.EnumerateArray().Select(QueryCaseTests.Value), source.Window.Select(row => rowKey!(row)));
                }
                if (expected.TryGetProperty("handed", out _))
                    Assert.All(source.Window, row => Assert.Same(handed[rowKey!(row)], row));
                lastWindow = source.Window;
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
                source.OnColumnsChanged(names.ValueKind switch
                {
                    JsonValueKind.True => columns,
                    // New objects, each equal to the one it was built after.
                    JsonValueKind.String when names.GetString() == "rebuilt" => columns.Select(c => c with { }).ToArray(),
                    _ => names.EnumerateArray().Select(n => columns.Single(c => c.Name == n.GetString())).ToArray(),
                });
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
                var row = target.ValueKind == JsonValueKind.Number ? held[target.GetInt32()]
                    : target.TryGetProperty("stranger", out var stranger) ? RowOf(-1, stranger)
                    : target.TryGetProperty("window", out var at) ? source.Window[at.GetInt32()]
                    : EntryOf(target);
                var written = step.GetProperty("with");
                var replacement = written.ValueKind == JsonValueKind.String && written.GetString() == "itself"
                    ? row
                    : RowOf(row.Position, written);
                source.ReplaceRow(row, replacement);
                if (row.Position >= 0)
                    held[row.Position] = replacement;
                Hand(replacement);
            }
            else if (step.TryGetProperty("apply", out var batch))
            {
                IReadOnlyList<Row> Rows(string name) => batch.TryGetProperty(name, out var rows) ? rows.EnumerateArray().Select(EntryOf).ToArray() : [];
                var added = Rows("added");
                var changed = Rows("changed");
                var removed = batch.TryGetProperty("removedKeys", out var keys) ? keys.EnumerateArray().Select(QueryCaseTests.Value).ToArray() : [];
                source.Apply(new GridChangeBatch<Row>(added, changed, removed!));
                foreach (var row in added.Concat(changed))
                    Hand(row);
            }
            else if (step.TryGetProperty("replaceAll", out var list))
            {
                var rows = list.EnumerateArray().Select(EntryOf).ToArray();
                source.ReplaceAll(rows);
                foreach (var row in rows)
                    Hand(row);
            }
            else if (step.TryGetProperty("gatherInterval", out var interval))
            {
                source.GatherInterval = TimeSpan.FromMilliseconds(interval.GetDouble());
            }
            else if (step.TryGetProperty("advance", out var advance))
            {
                clock.Advance(TimeSpan.FromMilliseconds(advance.GetDouble()));
            }
            else if (step.TryGetProperty("publishGathered", out _))
            {
                source.PublishGathered();
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
