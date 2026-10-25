using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Rows;
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
                Format: column.TryGetProperty("format", out var format) ? DisplayTextCaseTests.FormatOf(format.GetString()!) : null,
                DateType: column.TryGetProperty("dateType", out var dateType) ? QueryCaseTests.Member<DateType>(dateType) : DateType.DateTime))
            .ToArray();
        // A row written {"sameAs": place} is the object at that place again: one row by identity (ADR-0003).
        var held = new List<Row>();
        foreach (var written in entry.GetProperty("rows").EnumerateArray())
            held.Add(written.ValueKind == JsonValueKind.Object ? held[written.GetProperty("sameAs").GetInt32()] : RowOf(held.Count, written));
        var clock = new FakeTimeProvider();
        var start = clock.GetUtcNow();
        CellChangeOf<Row>? changedAt = null;
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
        // A row a step names: by its place, a stranger, by its position in the Window, or under a key.
        Row Named(JsonElement target) => target.ValueKind == JsonValueKind.Number ? held[target.GetInt32()]
            : target.TryGetProperty("stranger", out var stranger) ? RowOf(-1, stranger)
            : target.TryGetProperty("window", out var at) ? source.Window[at.GetInt32()]
            : EntryOf(target);
        int VersionOf(JsonElement step) => step.TryGetProperty("version", out var version) ? version.GetInt32() : source.RowSequenceVersion;
        var heard = 0;
        var marksHeard = 0;
        var lastWindow = source.Window;
        source.StateChanged += () => heard++;
        source.Marks.Changed += () => marksHeard++;

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
                if (expected.TryGetProperty("changeTimes", out var changeTimes))
                    Assert.Equal(changeTimes.GetBoolean(), source.CellChangedAt is not null);
                if (expected.TryGetProperty("changeTimesKeptFor", out var keptFor))
                    Assert.Equal(TimeSpan.FromMilliseconds(keptFor.GetDouble()), source.ChangeTimesKeptFor);
                if (expected.TryGetProperty("changedAt", out var times))
                {
                    foreach (var byKey in times.EnumerateObject())
                    {
                        var row = source.Window.Single(r => Equals(rowKey!(r), byKey.Name));
                        foreach (var byColumn in byKey.Value.EnumerateObject())
                        {
                            var at = changedAt!(row, new GridColumn<Row>(byColumn.Name, ColumnType.Text, _ => null));
                            Assert.True(byColumn.Value.ValueKind == JsonValueKind.Null
                                ? at is null
                                : at is { } time && (time - start).TotalMilliseconds == byColumn.Value.GetDouble(),
                                $"changedAt of {byKey.Name}, {byColumn.Name}: {(at is { } t ? (t - start).TotalMilliseconds : "null")}");
                        }
                    }
                }
                if (expected.TryGetProperty("marked", out var marked))
                {
                    Assert.Equal(marked.EnumerateArray().Select(p => p.GetInt32()),
                        Enumerable.Range(0, held.Count).Where(place => source.Marks.IsMarked(held[place])));
                }
                if (expected.TryGetProperty("windowMarks", out var windowMarks))
                    Assert.Equal(windowMarks.EnumerateArray().Select(m => m.GetBoolean()), source.Window.Select(source.Marks.IsMarked));
                if (expected.TryGetProperty("markCounts", out var markCounts))
                {
                    var counts = source.Marks.Counts ?? throw new InvalidOperationException("in memory, the counts are always known");
                    Assert.Equal(markCounts.EnumerateArray().Select(n => n.GetInt32()),
                        [counts.MarkedInResult, counts.RowsInResult, counts.MarkedOutsideResult]);
                }
                if (expected.TryGetProperty("markHeader", out var header))
                {
                    var counts = source.Marks.Counts ?? throw new InvalidOperationException("in memory, the counts are always known");
                    Assert.Equal(QueryCaseTests.Member<RowMarkHeaderState>(header), RowMarkRules.HeaderState(counts));
                }
                if (expected.TryGetProperty("markedRows", out var markedRows))
                {
                    if (rowKey is null)
                    {
                        var rows = markedRows.EnumerateArray().Select(row => row.EnumerateArray().Select(QueryCaseTests.Value).ToArray()).ToArray();
                        Assert.Equal(rows.Length, source.Marks.MarkedRows.Count);
                        for (var i = 0; i < rows.Length; i++)
                            Assert.Equal(rows[i], source.Marks.MarkedRows[i].Values);
                    }
                    else
                        Assert.Equal(markedRows.EnumerateArray().Select(QueryCaseTests.Value), source.Marks.MarkedRows.Select(rowKey));
                }
                if (expected.TryGetProperty("marksHeard", out var marksTimes))
                    Assert.Equal(marksTimes.GetInt32(), marksHeard);
                lastWindow = source.Window;
                heard = 0;
                marksHeard = 0;
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
                var row = Named(target);
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
            else if (step.TryGetProperty("cellChangedAt", out _))
            {
                changedAt = source.CellChangedAt;
            }
            else if (step.TryGetProperty("changeTimesKeptFor", out var keptFor))
            {
                source.ChangeTimesKeptFor = TimeSpan.FromMilliseconds(keptFor.GetDouble());
            }
            else if (step.TryGetProperty("markRow", out var toMark))
            {
                await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Row>.OneRow(Named(toMark), step.GetProperty("marked").GetBoolean()));
            }
            else if (step.TryGetProperty("markAll", out var all))
            {
                await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Row>.AllRows(all.GetBoolean(), VersionOf(step)));
            }
            else if (step.TryGetProperty("markPositions", out var ranges))
            {
                await source.Marks.OnMarkIntentAsync(new RowMarkIntent<Row>.Positions(ranges.EnumerateArray().Select(RangeOf).ToArray(), VersionOf(step)));
            }
            else if (step.TryGetProperty("rowKind", out var rowKind))
            {
                var column = rowKind.GetProperty("column").GetString();
                var at = Array.FindIndex(columns, c => c.Name == column);
                if (at < 0)
                    throw new InvalidOperationException($"\"{name}\" names no column {column} for its Row Kind.");
                var kinds = rowKind.GetProperty("kinds").EnumerateObject().ToDictionary(k => k.Name, k => QueryCaseTests.Member<RowKind>(k.Value));
                source.Marks.OnRowKindChanged(row => Convert.ToString(row.Values[at], System.Globalization.CultureInfo.InvariantCulture) is { } text
                    && kinds.TryGetValue(text, out var kind) ? kind : RowKind.Detail);
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
