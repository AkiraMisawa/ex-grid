using System.Text.Json;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The fetching source's shared cases (<c>tests/cases/source</c>, kind <c>fetch</c>; ADR-0350 section 5), run
/// against <see cref="GridSource.Fetch{TRow}"/>: a source over a server the case answers by hand, one fetch
/// at a time, with no clock and no delay. A row is a text, <c>Row n</c> for the row at position n. The Script
/// Core's runner is <c>packages/core/test/fetch.test.ts</c>.
/// </summary>
public class FetchCaseTests
{
    private const string Area = "source";

    /// <summary>One fetch the server was asked, waiting for the case to answer it.</summary>
    private sealed record Call(GridQuery Query, TaskCompletionSource<GridPage<string>> Answer, CancellationToken Cancellation);

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "fetch");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a fetch case holds on the C# core
    public void Fetch(string file, string name)
    {
        // With no context, an answer runs the source's continuations as it is given, so every step reads
        // what the answer did, as the Script Core's runner reads it once its promises have run.
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            Run(file, name);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    private static void Run(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = document.GetProperty("columns").EnumerateArray()
            .Select(column => new ColumnInfo<string>(column.GetString()!, ColumnType.Text, row => row))
            .ToArray();
        var calls = new List<Call>();
        var handed = new List<string[]>();
        var distinct = entry.TryGetProperty("distinct", out var values)
            ? values.EnumerateArray().Select(value => (object?)value.GetString()).ToArray()
            : null;
        var source = GridSource.Fetch<string>(
            (query, cancellation) =>
            {
                var call = new Call(query, new TaskCompletionSource<GridPage<string>>(), cancellation);
                calls.Add(call);
                return new ValueTask<GridPage<string>>(call.Answer.Task);
            },
            entry.GetProperty("readAhead").GetInt32(),
            distinctValues: distinct is null ? null : (column, filter, _) =>
            {
                handed.Add(filter is null ? [] : [.. filter.Columns.Keys]);
                return Task.FromResult(Chrome.DistinctValues.Of(distinct));
            },
            rowKey: entry.TryGetProperty("rowKey", out var keyed) && keyed.GetBoolean() ? row => row : null);
        var heard = 0;
        source.StateChanged += () => heard++;
        var reported = new List<string>();
        if (entry.GetProperty("hears").GetBoolean())
            source.FetchFailed += error => reported.Add(error.Message);
        var promises = new Dictionary<string, Task>();
        var copies = new Dictionary<string, Task<IReadOnlyList<string>>>();

        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("bind", out _))
            {
                source.OnColumnsChanged(columns);
            }
            else if (step.TryGetProperty("setSorts", out var sorts))
            {
                source.OnSortChanged(Sorts(sorts));
            }
            else if (step.TryGetProperty("setFilter", out var filter))
            {
                source.OnFilterChanged(filter.ValueKind == JsonValueKind.Null ? null : QueryCaseTests.FilterOf(filter));
            }
            else if (step.TryGetProperty("requery", out _))
            {
                source.Requery();
            }
            else if (step.TryGetProperty("dispose", out _))
            {
                source.Dispose();
            }
            else if (step.TryGetProperty("needRange", out var range))
            {
                if (step.TryGetProperty("throws", out var refusal))
                {
                    SharedCases.Refused(() => source.OnRangeNeededAsync(RangeOf(range)), refusal.GetString()!);
                    continue;
                }
                promises.Add(step.GetProperty("as").GetString()!, source.OnRangeNeededAsync(RangeOf(range)));
            }
            else if (step.TryGetProperty("rows", out var rows))
            {
                copies.Add(step.GetProperty("as").GetString()!, source.GetRowsAsync(RangeOf(rows), CancellationToken.None));
            }
            else if (step.TryGetProperty("answer", out var answered))
            {
                calls[answered.GetInt32()].Answer.SetResult(PageOf(step.GetProperty("page")));
            }
            else if (step.TryGetProperty("fail", out var failed))
            {
                calls[failed.GetInt32()].Answer.SetException(new TimeoutException(step.GetProperty("error").GetString()));
            }
            else if (step.TryGetProperty("settled", out var label))
            {
                var promise = promises[label.GetString()!];
                Assert.True(promise.IsCompleted, $"{label} has not settled");
                if (step.TryGetProperty("throws", out var refusal))
                {
                    SharedCases.Refused(() => promise.GetAwaiter().GetResult(), refusal.GetString()!,
                        step.TryGetProperty("saying", out var saying) ? saying.GetString() : null);
                }
                else if (step.TryGetProperty("rejects", out var rejection))
                {
                    var error = Assert.ThrowsAny<Exception>(() => promise.GetAwaiter().GetResult());
                    Assert.Equal(rejection.GetString(), error.Message);
                }
                else
                {
                    Assert.True(promise.IsCompletedSuccessfully, $"{label} failed: {promise.Exception}");
                }
            }
            else if (step.TryGetProperty("pending", out var waiting))
            {
                Assert.False(promises[waiting.GetString()!].IsCompleted, $"{waiting} has settled");
            }
            else if (step.TryGetProperty("same", out var pair))
            {
                Assert.Same(promises[pair[0].GetString()!], promises[pair[1].GetString()!]);
            }
            else if (step.TryGetProperty("read", out var copy))
            {
                var task = copies[copy.GetString()!];
                Assert.True(task.IsCompletedSuccessfully, $"{copy} has not been read");
                Assert.Equal(Strings(step.GetProperty("is")), task.Result.ToArray<string?>());
            }
            else if (step.TryGetProperty("asked", out var asked))
            {
                var query = calls[asked.GetInt32()].Query;
                Assert.Equal(RangeOf(step.GetProperty("range")), query.Range);
                Assert.Equal(Sorts(step.GetProperty("sorts")), query.Sorts);
                Assert.Equal(Strings(step.GetProperty("filtered")), query.Filter is null ? [] : [.. query.Filter.Columns.Keys]);
            }
            else if (step.TryGetProperty("distinctValues", out var column))
            {
                var answer = source.GetDistinctValuesAsync(column.GetString()!, CancellationToken.None);
                Assert.True(answer.IsCompletedSuccessfully);
                if (step.TryGetProperty("tooMany", out _))
                {
                    Assert.True(answer.Result.IsTooMany);
                    continue;
                }
                Assert.Equal(Strings(step.GetProperty("is")), answer.Result.Values.Select(value => (string?)value).ToArray());
                Assert.Equal(Strings(step.GetProperty("handed")), handed[^1]);
            }
            else if (step.TryGetProperty("expect", out var expected))
            {
                Expect(source, expected, calls, heard, reported);
                heard = 0;
                reported.Clear();
            }
            else
            {
                throw new InvalidOperationException($"{step} is no step a fetch case takes.");
            }
        }
    }

    private static void Expect(FetchingGridSource<string> source, JsonElement expected, List<Call> calls, int heard, List<string> reported)
    {
        if (expected.TryGetProperty("calls", out var asked))
            Assert.Equal(asked.EnumerateArray().Select(RangeOf), calls.Select(call => call.Query.Range));
        if (expected.TryGetProperty("cancelled", out var cancelled))
            Assert.Equal(cancelled.EnumerateArray().Select(flag => flag.GetBoolean()), calls.Select(call => call.Cancellation.IsCancellationRequested));
        if (expected.TryGetProperty("windowStart", out var start))
            Assert.Equal(start.GetInt32(), source.WindowStart);
        if (expected.TryGetProperty("windowCount", out var count))
            Assert.Equal(count.GetInt32(), source.Window.Count);
        if (expected.TryGetProperty("first", out var first))
            Assert.Equal(first.GetString(), source.Window[0]);
        if (expected.TryGetProperty("totalCount", out var total))
            Assert.Equal(total.GetInt32(), source.TotalCount);
        if (expected.TryGetProperty("loading", out var loading))
            Assert.Equal(loading.GetBoolean(), source.IsLoading);
        if (expected.TryGetProperty("version", out var version))
            Assert.Equal(version.GetInt32(), source.RowSequenceVersion);
        if (expected.TryGetProperty("sorts", out var sorts))
            Assert.Equal(Sorts(sorts), source.Sorts);
        if (expected.TryGetProperty("filter", out var filter))
        {
            Assert.True(GridFilters.Equal(filter.ValueKind == JsonValueKind.Null ? null : QueryCaseTests.FilterOf(filter), source.Filter),
                $"the filter in force is not {filter}");
        }
        if (expected.TryGetProperty("lastError", out var error))
        {
            if (error.ValueKind == JsonValueKind.Null)
                Assert.Null(source.LastError);
            else
                Assert.Contains(error.GetString()!, source.LastError?.Message ?? throw new InvalidOperationException("No failure is kept."), StringComparison.Ordinal);
        }
        if (expected.TryGetProperty("reported", out var heardFailures))
        {
            var said = Strings(heardFailures);
            Assert.Equal(said.Length, reported.Count);
            for (var i = 0; i < said.Length; i++)
                Assert.Contains(said[i]!, reported[i], StringComparison.Ordinal);
        }
        if (expected.TryGetProperty("vouches", out var vouches))
            Assert.Equal(vouches.GetBoolean(), source.VouchesDistinctRows);
        if (expected.TryGetProperty("heard", out var times))
            Assert.Equal(times.GetInt32(), heard);
    }

    /// <summary>A page as a case writes it: <c>count</c> rows named by their positions from <c>start</c>, or
    /// the <c>rows</c> it lists, of a result <c>total</c> rows long, with the server's order token if any.</summary>
    private static GridPage<string> PageOf(JsonElement written)
    {
        var start = written.GetProperty("start").GetInt32();
        var rows = written.TryGetProperty("rows", out var listed)
            ? Strings(listed).Select(row => row!).ToArray()
            : [.. Enumerable.Range(start, written.GetProperty("count").GetInt32()).Select(at => $"Row {at}")];
        return new GridPage<string>(rows, start, written.GetProperty("total").GetInt32())
        {
            OrderToken = written.TryGetProperty("orderToken", out var token) ? token.GetString() : null,
        };
    }

    private static RowRange RangeOf(JsonElement pair) => new(pair[0].GetInt32(), pair[1].GetInt32());

    private static SortSpec[] Sorts(JsonElement sorts) => [.. sorts.EnumerateArray()
        .Select(sort => new SortSpec(sort.GetProperty("column").GetString()!, QueryCaseTests.Member<SortDirection>(sort.GetProperty("direction"))))];

    private static string?[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];
}
