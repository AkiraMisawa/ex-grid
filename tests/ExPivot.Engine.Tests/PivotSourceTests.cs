using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>The Pivot Source (ADR-0066): the bundled source is the reference, a server answering
/// through <c>Fetch</c> is held to it question for question, answers carry their Source Version,
/// the bundled source works in slices, and a layout too large to read is refused by name.</summary>
public class PivotSourceTests
{
    private static PivotLayout[] Layouts =>
    [
        new()
        {
            Rows = [P("Desk")],
            Values = [.. Enum.GetValues<PivotAggregation>().Select(a => Value("Amount", a)), .. Enum.GetValues<PivotAggregation>().Select(a => Value("Risk", a))],
        },
        new() { Rows = [P("Desk"), P("Book")], Columns = [P("Live")], Values = [Sum("Amount"), Value("Tag", PivotAggregation.Count)] },
        new()
        {
            Rows = [P("Book")],
            Columns = [P("Date")],
            Filters = [P("Live") with { HiddenItems = [PivotItemKey.Boolean(false)] }],
            Values = [Value("Risk", PivotAggregation.Max), Sum("Risk"), Value("Risk", PivotAggregation.Average)],
        },
        new() { Columns = [P("Tag")], Values = [Value("Amount", PivotAggregation.StdDev), Value("Amount", PivotAggregation.Product)] },
        new() { Values = [Sum("Amount")] },
        new()
        {
            Rows = [P("Desk") with { HiddenItems = [PivotItemKey.Text("rates"), PivotItemKey.Blank] }],
            Values = [Sum("Amount"), Value("Risk", PivotAggregation.Var)],
            ValuesAxis = PivotAxis.Rows,
        },
        new() { Rows = [P("Tag")], Columns = [P("Desk") with { HiddenItems = [PivotItemKey.Text("")] }], Values = [Value("Risk", PivotAggregation.Product)] },
        new() { Rows = [P("Date")], Filters = [P("Book") with { HiddenItems = [PivotItemKey.Text("NY-2")] }] },
        PivotLayout.Empty,
    ];

    [Fact] // ADR-0066 (PV-22): a Fetch whose transport is JSON answers as From does — Leaf Aggregates, reports, Items and Details
    public async Task Fetch_over_json_answers_as_from_does()
    {
        var reference = PivotSource.From(Deals, DealFields);
        var fetched = OverJson(reference);

        foreach (var layout in Layouts)
        {
            var query = PivotQuery.For(layout);
            var expected = await reference.AggregateAsync(query, Ct);
            var actual = await fetched.AggregateAsync(query, Ct);
            SameAnswer(expected, actual);

            var report = PivotEngine.Report(PivotEngine.Cube(query, expected, DealFields), layout, EnUs);
            var fetchedReport = PivotEngine.Report(PivotEngine.Cube(query, actual, DealFields), layout, EnUs);
            Assert.Equal(Lines(report), Lines(fetchedReport));
            Assert.Equal(Lines(PivotEngine.Compute(Deals, DealFields, layout, EnUs)), Lines(report));

            // The records behind every cell, labels included, all of them and a page of them.
            foreach (var row in report.Rows)
            {
                for (var column = -1; column < report.ValueColumns.Count; column++)
                {
                    foreach (var (start, count) in new[] { (0, int.MaxValue), (1, 2) })
                    {
                        var details = report.DetailsQuery(row, column, start, count);
                        SameDetails(await reference.DetailsAsync(details, Ct), await fetched.DetailsAsync(details, Ct));
                    }
                }
            }

            // Every placed field's Items, whole, searched and capped.
            foreach (var field in query.Placed.Select(f => f.Field))
            {
                foreach (var items in new[]
                         {
                             new PivotItemsQuery(field, expected.SourceVersion),
                             new PivotItemsQuery(field, expected.SourceVersion, search: "e"),
                             new PivotItemsQuery(field, expected.SourceVersion, max: 2),
                         })
                {
                    SameItems(await reference.ItemsAsync(items, Ct), await fetched.ItemsAsync(items, Ct));
                }
            }
        }
    }

    [Fact] // ADR-0066 (PV-22): the same refusals, whichever side refuses
    public async Task Fetch_over_json_refuses_as_from_does()
    {
        var reference = PivotSource.From(Deals, DealFields);
        var fetched = OverJson(reference);
        var version = (await reference.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;

        foreach (var query in new[]
                 {
                     new PivotQuery(rows: [F("Desk"), F("Book")], maxLeaves: 3),
                     new PivotQuery(rows: [F("Desk")], values: [V("Pnl")]),
                     new PivotQuery(filters: [F("Region")]),
                 })
        {
            var expected = await reference.AggregateAsync(query, Ct);
            Assert.True(expected.IsRefused);
            SameAnswer(expected, await fetched.AggregateAsync(query, Ct));
        }
        foreach (var items in new[] { new PivotItemsQuery("Desk", "stale"), new PivotItemsQuery("Region", version) })
            SameItems(await reference.ItemsAsync(items, Ct), await fetched.ItemsAsync(items, Ct));
        foreach (var details in new[]
                 {
                     new PivotDetailsQuery("stale"),
                     new PivotDetailsQuery(version, rowItems: [new("Region", PivotItemKey.Text("East"))]),
                 })
        {
            SameDetails(await reference.DetailsAsync(details, Ct), await fetched.DetailsAsync(details, Ct));
        }
    }

    [Fact] // ADR-0066 (PV-23): Items and records are asked for under the answer's Source Version, and add up to it
    public async Task Items_and_records_are_asked_for_under_the_answers_version()
    {
        var source = PivotSource.From(Deals, DealFields);
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Amount", PivotParts.Counts)]);
        var answer = await source.AggregateAsync(query, Ct);

        var items = await source.ItemsAsync(new PivotItemsQuery("Desk", answer.SourceVersion), Ct);
        Assert.Equal(answer.SourceVersion, items.SourceVersion);
        var report = PivotEngine.Report(PivotEngine.Cube(query, answer, DealFields), new PivotLayout { Rows = [P("Desk")], Values = [Value("Amount", PivotAggregation.Count)] });
        foreach (var row in report.Rows)
        {
            var details = await source.DetailsAsync(report.DetailsQuery(row, 0), Ct);
            Assert.Equal(answer.SourceVersion, details.SourceVersion);
            // The records behind a cell are the ones it counts: its Amounts that are not Blank.
            var amounts = details.Records.Count(record => record.Values[3] is not null);
            Assert.Equal(report.ValueAt(row, 0)?.Text, amounts == 0 ? null : amounts.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(details.Records.Count, details.Total);
        }
    }

    [Fact] // ADR-0066 (PV-23): a source that can no longer answer under a version refuses, rather than answer from newer data
    public async Task A_version_the_source_no_longer_holds_is_refused()
    {
        var answer = await PivotSource.From(Deals, DealFields).AggregateAsync(new PivotQuery(rows: [F("Desk")]), Ct);
        // A new source over the same records is a refresh: another version.
        var refreshed = PivotSource.From(Deals, DealFields);

        var items = await refreshed.ItemsAsync(new PivotItemsQuery("Desk", answer.SourceVersion), Ct);
        var details = await refreshed.DetailsAsync(new PivotDetailsQuery(answer.SourceVersion), Ct);

        Assert.Equal(PivotSourceRefusalKind.SourceVersionNotHeld, items.Refusal!.Kind);
        Assert.Equal(PivotSourceRefusalKind.SourceVersionNotHeld, details.Refusal!.Kind);
        Assert.Contains(answer.SourceVersion, details.Refusal.Message);
        // A refusal is not an empty page.
        Assert.Throws<InvalidOperationException>(() => items.Items);
        Assert.Throws<InvalidOperationException>(() => items.Total);
        Assert.Throws<InvalidOperationException>(() => details.Records);
        Assert.Throws<InvalidOperationException>(() => details.Total);
        Assert.NotEqual(answer.SourceVersion, (await refreshed.AggregateAsync(new PivotQuery(), Ct)).SourceVersion);
    }

    [Fact] // ADR-0060/0066: a field's Items over all the data — not narrowed by Hidden Items — first spelling, invariant order, search and cap
    public async Task A_fields_items_are_over_all_the_data()
    {
        var source = PivotSource.From(Deals, DealFields);
        var version = (await source.AggregateAsync(new PivotQuery(rows: [F("Desk", PivotItemKey.Text("FX"))]), Ct)).SourceVersion;

        var desks = await source.ItemsAsync(new PivotItemsQuery("Desk", version), Ct);
        var searched = await source.ItemsAsync(new PivotItemsQuery("Desk", version, search: "R"), Ct);
        var capped = await source.ItemsAsync(new PivotItemsQuery("Desk", version, max: 2), Ct);
        var tags = await source.ItemsAsync(new PivotItemsQuery("Tag", version), Ct);

        SameKeys([PivotItemKey.Text(""), PivotItemKey.Text("Credit"), PivotItemKey.Text("FX"), PivotItemKey.Text("Rates"), PivotItemKey.Blank], desks.Items);
        Assert.Equal(5, desks.Total);
        SameKeys([PivotItemKey.Text("Credit"), PivotItemKey.Text("Rates")], searched.Items);
        Assert.Equal(2, searched.Total);
        SameKeys([PivotItemKey.Text(""), PivotItemKey.Text("Credit")], capped.Items);
        Assert.Equal(5, capped.Total);
        SameKeys(
        [
            PivotItemKey.Number(12), PivotItemKey.Date(new DateTime(2026, 1, 1)), PivotItemKey.Text("7d3f7a52-1b49-4a3e-9d57-2b8c3c0e6f11"),
            PivotItemKey.Text("High"), PivotItemKey.Text("Low"), PivotItemKey.Boolean(true), PivotItemKey.Blank,
        ], tags.Items);
    }

    [Fact] // ADR-0066 (PV-27): the bundled source works in slices and yields between them — for every question
    public async Task The_bundled_source_yields_between_slices()
    {
        var yields = 0;
        var slicing = new PivotSlicing { Budget = TimeSpan.Zero, RecordsPerCheck = 2, Yield = _ => { yields++; return ValueTask.CompletedTask; } };
        var source = PivotSource.From(Deals, DealFields, slicing);
        var query = new PivotQuery(rows: [F("Desk")], columns: [F("Live")], values: [V("Amount")]);

        var answer = await source.AggregateAsync(query, Ct);
        // Nine records in slices of two: four yields between five slices.
        Assert.Equal(4, yields);
        SameAnswer(await PivotSource.From(Deals, DealFields).AggregateAsync(query, Ct), answer, sameVersion: false);

        yields = 0;
        await source.ItemsAsync(new PivotItemsQuery("Desk", answer.SourceVersion), Ct);
        Assert.Equal(4, yields);
        yields = 0;
        await source.DetailsAsync(new PivotDetailsQuery(answer.SourceVersion), Ct);
        Assert.Equal(4, yields);
    }

    [Fact] // ADR-0066 (PV-27): a slice ends when its time is spent, by the clock the source is given
    public async Task A_slice_ends_when_its_budget_is_spent()
    {
        var yields = 0;
        var slicing = new PivotSlicing
        {
            // Every look at the clock is 10 ms on; a slice of 25 ms reads three records.
            TimeProvider = new SteppingClock(TimeSpan.FromMilliseconds(10)),
            Budget = TimeSpan.FromMilliseconds(25),
            RecordsPerCheck = 1,
            Yield = _ => { yields++; return ValueTask.CompletedTask; },
        };

        await PivotSource.From(Deals, DealFields, slicing).AggregateAsync(new PivotQuery(rows: [F("Desk")]), Ct);

        Assert.Equal(2, yields);
    }

    // Rewritten with ADR-0064/0066 when the engine moved onto the Snapshot: the records are read
    // once, into a Snapshot, and a question reads the Snapshot's rows, so it is the rows a question
    // reads that are counted — through the slicing's own counter — where the accessor's calls were.
    [Fact] // ADR-0066 (PV-27): a cancelled question stops at the next slice, and reads nothing more
    public async Task A_cancelled_question_stops_at_the_next_slice()
    {
        foreach (var ask in new Func<PivotSource, string, CancellationToken, Task>[]
                 {
                     (source, _, token) => source.AggregateAsync(new PivotQuery(rows: [F("Desk")]), token).AsTask(),
                     (source, version, token) => source.ItemsAsync(new PivotItemsQuery("Desk", version), token).AsTask(),
                     (source, version, token) => source.DetailsAsync(new PivotDetailsQuery(version, rowItems: [new("Desk", PivotItemKey.Text("FX"))]), token).AsTask(),
                 })
        {
            long read = 0;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            var armed = false;
            var slicing = new PivotSlicing
            {
                Budget = TimeSpan.Zero,
                RecordsPerCheck = 2,
                // The question is cancelled while the thread is yielded after its first slice.
                Yield = _ =>
                {
                    if (armed)
                        cancellation.Cancel();
                    return ValueTask.CompletedTask;
                },
                RowsRead = rows => read += rows,
            };
            var source = PivotSource.From(Deals, DealFields, slicing);
            var version = (await source.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;
            read = 0;
            armed = true;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ask(source, version, cancellation.Token));

            // The first slice's two records, and not one more.
            Assert.Equal(2, read);
        }
    }

    [Fact] // ADR-0066 (PV-27): a question cancelled before it starts reads nothing
    public async Task A_question_cancelled_before_it_starts_reads_nothing()
    {
        var read = 0;
        var source = PivotSource.From(Deals, CountingDesk(() => read++));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.AggregateAsync(new PivotQuery(rows: [F("Desk")]), new CancellationToken(canceled: true)).AsTask());

        Assert.Equal(0, read);
    }

    [Fact] // ADR-0066 (PV-29): an answer that would pass MaxLeaves is refused with the bound, as soon as it is passed
    public async Task Too_many_leaves_are_refused_as_soon_as_the_bound_is_passed()
    {
        // The rows the question reads from the Snapshot are counted (rewritten with ADR-0064, as above).
        long read = 0;
        var source = PivotSource.From(Deals, DealFields, new PivotSlicing { RowsRead = rows => read += rows });
        var query = new PivotQuery(rows: [F("Desk"), F("Book")], maxLeaves: 3);

        var answer = await source.AggregateAsync(query, Ct);

        var refusal = answer.Refusal!;
        Assert.Equal(PivotSourceRefusalKind.TooManyLeaves, refusal.Kind);
        Assert.Equal(3, refusal.Limit);
        Assert.Equal("This layout needs more than 3 cells.", refusal.Message);
        // The fourth leaf, (blank) × NY-2, is the fifth record: nothing after it is read.
        Assert.Equal(5, read);
        Assert.Throws<InvalidOperationException>(() => answer.Rows);
        Assert.Equal("This layout needs more than 200,000 cells.", PivotSourceRefusal.TooManyLeaves(200_000).Message);

        // Eight leaves in all: answered at eight, refused at seven.
        Assert.Equal(8, (await source.AggregateAsync(new PivotQuery(rows: [F("Desk"), F("Book")], maxLeaves: 8), Ct)).LeafCount);
        Assert.True((await source.AggregateAsync(new PivotQuery(rows: [F("Desk"), F("Book")], maxLeaves: 7), Ct)).IsRefused);
        Assert.Equal(PivotQuery.DefaultMaxLeaves, new PivotQuery().MaxLeaves);
        Assert.Equal(200_000, PivotQuery.DefaultMaxLeaves);
    }

    [Fact] // ADR-0066: a field the source does not have is refused by name, in any Area and in any question
    public async Task An_unknown_field_is_refused_by_name()
    {
        var source = PivotSource.From(Deals, DealFields);
        var version = (await source.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;

        foreach (var query in new[]
                 {
                     new PivotQuery(rows: [F("Region")]), new PivotQuery(columns: [F("Region")]),
                     new PivotQuery(filters: [F("Region", PivotItemKey.Blank)]), new PivotQuery(values: [V("Region")]),
                 })
        {
            var refusal = (await source.AggregateAsync(query, Ct)).Refusal!;
            Assert.Equal(PivotSourceRefusalKind.UnknownField, refusal.Kind);
            Assert.Equal("Region", refusal.Field);
            Assert.Contains("'Region'", refusal.Message);
        }
        Assert.Equal(PivotSourceRefusalKind.UnknownField, (await source.ItemsAsync(new PivotItemsQuery("Region", version), Ct)).Refusal!.Kind);
        Assert.Equal(PivotSourceRefusalKind.UnknownField, (await source.DetailsAsync(
            new PivotDetailsQuery(version, hiddenItems: [F("Region", PivotItemKey.Blank)]), Ct)).Refusal!.Kind);
    }

    [Fact] // ADR-0066 (PV-24's engine side): a source declares what it answers, and a part no offered Aggregation reads is refused without asking
    public async Task A_part_no_offered_aggregation_reads_is_refused_without_asking()
    {
        var reference = PivotSource.From(Deals, DealFields);
        var asked = 0;
        var features = new PivotSourceFeatures(
            [PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.Average, PivotAggregation.Max, PivotAggregation.Min, PivotAggregation.CountNumbers],
            canRefresh: true);
        var sql = PivotSource.Fetch(DealFields, features,
            (query, ct) => { asked++; return reference.AggregateAsync(query, ct); },
            (query, ct) => reference.ItemsAsync(query, ct),
            (query, ct) => reference.DetailsAsync(query, ct));

        var product = await sql.AggregateAsync(new PivotQuery(values: [V("Amount", PivotParts.Sum | PivotParts.Product)]), Ct);
        var variance = await sql.AggregateAsync(new PivotQuery(values: [V("Risk", PivotParts.Variance)]), Ct);
        var unknown = await sql.AggregateAsync(new PivotQuery(rows: [F("Region")]), Ct);
        Assert.Equal(0, asked);
        Assert.Equal(PivotSourceRefusalKind.AggregationNotOffered, product.Refusal!.Kind);
        Assert.Equal("Amount", product.Refusal.Field);
        Assert.Contains("Product", product.Refusal.Message);
        Assert.Contains("StdDev or StdDevp or Var or Varp", variance.Refusal!.Message);
        Assert.Equal(PivotSourceRefusalKind.UnknownField, unknown.Refusal!.Kind);

        var answered = await sql.AggregateAsync(new PivotQuery(values: [V("Amount", PivotParts.Sum | PivotParts.Extremes)]), Ct);
        Assert.Equal(1, asked);
        Assert.False(answered.IsRefused);
        Assert.True(features.Offers(PivotAggregation.Max));
        Assert.False(features.Offers(PivotAggregation.Product));
        Assert.Equal(Enum.GetValues<PivotAggregation>(), reference.Features.Aggregations);
        Assert.False(reference.Features.CanRefresh);
        Assert.True(sql.Features.CanRefresh);
    }

    [Fact] // ADR-0066: an answer to another question than the one asked is refused by name, never laid out
    public async Task An_answer_to_another_question_is_refused()
    {
        var reference = PivotSource.From(Deals, DealFields);
        var confused = PivotSource.Fetch(DealFields, reference.Features,
            (query, ct) => reference.AggregateAsync(new PivotQuery(rows: [F("Book")], values: query.Values), ct),
            (query, ct) => reference.ItemsAsync(query, ct),
            (query, ct) => reference.DetailsAsync(query, ct));
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Amount", PivotParts.Sum)]);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => confused.AggregateAsync(query, Ct).AsTask());
        Assert.Contains("[Book]", refused.Message);
        var other = await reference.AggregateAsync(new PivotQuery(rows: [F("Book")], values: query.Values), Ct);
        Assert.Throws<InvalidOperationException>(() => PivotEngine.Cube(query, other, DealFields));
        Assert.Throws<InvalidOperationException>(() => PivotEngine.Cube(query, PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(1)), DealFields));
    }

    [Fact] // ADR-0066/0067: a server's source says its data moved on, and Refresh asks again; the bundled source is refreshed by a new one
    public async Task A_servers_source_says_when_its_data_moved_on()
    {
        var reference = PivotSource.From(Deals, DealFields);
        var fetched = OverJson(reference);
        var notices = new List<PivotSourceChanged>();
        fetched.Changed += notices.Add;
        var bundled = new List<PivotSourceChanged>();
        reference.Changed += bundled.Add;

        fetched.NotifyChanged("42");
        await fetched.RefreshAsync(Ct);
        await reference.RefreshAsync(Ct);

        Assert.Equal([new PivotSourceChanged("42"), new PivotSourceChanged()], notices);
        Assert.Empty(bundled);
    }

    [Fact] // ADR-0066: the bundled source captures no caller's context — a question a UI thread blocks on still completes, slices and all
    public void A_question_completes_though_its_callers_context_is_blocked()
    {
        var slicing = new PivotSlicing { Budget = TimeSpan.Zero, RecordsPerCheck = 2 };
        var typed = PivotFields.Of<Deal>().Text("Desk", d => d.Desk).Number("Risk", d => d.Risk);
        var blocked = new BlockedContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(blocked);
        try
        {
            foreach (var source in new[] { PivotSource.From(Deals, DealFields, slicing), PivotSource.From(Deals, typed, slicing) })
            {
                var answer = source.AggregateAsync(new PivotQuery(rows: [F("Desk")], values: [V("Risk")]), Ct).AsTask();
                // Blocking is what is tested: a caller that blocks on its own thread, as a test of a
                // component does, must not wait on a slice posted back to that thread.
#pragma warning disable xUnit1031
                Assert.True(answer.Wait(TimeSpan.FromSeconds(30), Ct), "the question waited on its caller's context");
                Assert.Equal(5, answer.Result.LeafCount);
#pragma warning restore xUnit1031
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        Assert.Equal(0, blocked.Posted);
    }

    /// <summary>A UI thread that is blocked: what is posted to it never runs.</summary>
    private sealed class BlockedContext : SynchronizationContext
    {
        public int Posted { get; private set; }

        public override void Post(SendOrPostCallback d, object? state) => Posted++;

        public override void Send(SendOrPostCallback d, object? state) => Posted++;
    }

    private static PivotField<Deal>[] CountingDesk(Action read)
        => [new("Desk", PivotFieldType.Text, d => { read(); return d.Desk; }), .. DealFields.Skip(1)];

    /// <summary>A clock that moves on by <paramref name="step"/> every time it is read.</summary>
    private sealed class SteppingClock(TimeSpan step) : TimeProvider
    {
        private long _now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _now += step.Ticks;
    }
}
