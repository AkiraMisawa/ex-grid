using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.LiveDataTests;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// An answer may say which of its leaves changed since an earlier Source Version (ADR-0161;
/// ADR-0066's note of 2026-10-07). The bundled source says it for the version the question names,
/// when that is the version of the last answer it gave the question: the leaves its fold touched,
/// or that the leaves were made afresh when it could not fold. A question that names no version, or
/// another, is answered as before.
/// </summary>
public class ChangedLeavesTests
{
    private static readonly PivotQuery ByDesk = new(rows: [F("Desk")], values: [V("Amount", PivotParts.Sum)]);

    private static string[] Desks(PivotAnswer answer, IEnumerable<int> leaves)
        => leaves.Select(leaf => answer.Rows[0].Items[answer.Rows[0].ItemAt(leaf)].Value!).ToArray();

    [Fact] // ADR-0161: a batch folded in names the leaves it touched, since the version the question names
    public async Task The_folded_leaves_are_named()
    {
        var fields = Declarations();
        var source = PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m), Row(3, "North", 5m), Row(4, "West", 1m)], fields);
        var first = await source.AggregateAsync(ByDesk, Ct);

        source.Apply(fields.Batch(changed: [Row(2, "West", 21m)]));
        var second = await source.AggregateAsync(ByDesk.WithChangedSince(first.SourceVersion), Ct);

        var changes = second.ChangedLeaves!;
        Assert.Equal(first.SourceVersion, changes.Since);
        Assert.True(changes.SameLeaves);
        Assert.Equal(["West"], Desks(second, changes.Leaves));
    }

    [Fact] // ADR-0161: two batches folded before the question are both named, once each
    public async Task Every_batch_since_the_version_is_named()
    {
        var fields = Declarations();
        var source = PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m), Row(3, "North", 5m)], fields);
        var first = await source.AggregateAsync(ByDesk, Ct);

        source.Apply(fields.Batch(changed: [Row(3, "North", 6m)]));
        source.Apply(fields.Batch(changed: [Row(1, "East", 11m), Row(3, "North", 7m)]));
        var answer = await source.AggregateAsync(ByDesk.WithChangedSince(first.SourceVersion), Ct);

        Assert.Equal(["East", "North"], Desks(answer, answer.ChangedLeaves!.Leaves).Order());
    }

    [Fact] // ADR-0161: a record that moves between leaves touches both
    public async Task A_record_that_moves_touches_both_leaves()
    {
        var fields = Declarations();
        var source = PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m), Row(3, "West", 5m), Row(4, "North", 1m)], fields);
        var first = await source.AggregateAsync(ByDesk, Ct);

        source.Apply(fields.Batch(changed: [Row(3, "East", 5m)]));
        var answer = await source.AggregateAsync(ByDesk.WithChangedSince(first.SourceVersion), Ct);

        Assert.True(answer.ChangedLeaves!.SameLeaves);
        Assert.Equal(["East", "West"], Desks(answer, answer.ChangedLeaves.Leaves).Order());
    }

    [Fact] // ADR-0161: an Item that appears, or a leaf that empties, makes the leaves afresh
    public async Task Leaves_that_come_or_go_are_made_afresh()
    {
        var fields = Declarations();
        var source = PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m)], fields);
        var first = await source.AggregateAsync(ByDesk, Ct);

        source.Apply(fields.Batch(added: [Row(3, "South", 1m)]));
        var appeared = await source.AggregateAsync(ByDesk.WithChangedSince(first.SourceVersion), Ct);
        source.Apply(fields.Batch(removedKeys: [1L]));
        var left = await source.AggregateAsync(ByDesk.WithChangedSince(appeared.SourceVersion), Ct);

        Assert.False(appeared.ChangedLeaves!.SameLeaves);
        Assert.Empty(appeared.ChangedLeaves.Leaves);
        Assert.False(left.ChangedLeaves!.SameLeaves);
    }

    [Fact] // ADR-0161/0067: a batch the source could not fold — a compaction — makes the leaves afresh
    public async Task A_compaction_makes_the_leaves_afresh()
    {
        var fields = Declarations();
        var source = PivotSource.From(Enumerable.Range(1, 40).Select(i => Row(i, i % 2 == 0 ? "East" : "West", i)).ToArray(), fields);
        var answer = await source.AggregateAsync(ByDesk, Ct);

        for (var i = 0; ; i++)
        {
            var id = 1 + (i % 40);
            var change = source.Apply(fields.Batch(changed: [Row(id, id % 2 == 0 ? "East" : "West", i)]));
            var next = await source.AggregateAsync(ByDesk.WithChangedSince(answer.SourceVersion), Ct);
            Assert.Equal(!change.Compacted, next.ChangedLeaves!.SameLeaves);
            answer = next;
            if (change.Compacted)
                break;
        }
    }

    [Fact] // ADR-0161: a question that names no version, another source's, or an answer the source has moved past, is answered without changed leaves
    public async Task Another_version_is_answered_without_them()
    {
        var fields = Declarations();
        var source = PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m)], fields);
        var other = PivotSource.From([Row(1, "East", 10m)], fields);
        var first = await source.AggregateAsync(ByDesk, Ct);
        source.Apply(fields.Batch(changed: [Row(1, "East", 11m)]));
        var second = await source.AggregateAsync(ByDesk, Ct);
        source.Apply(fields.Batch(changed: [Row(1, "East", 12m)]));

        Assert.Null(second.ChangedLeaves);
        Assert.Null((await source.AggregateAsync(ByDesk.WithChangedSince((await other.AggregateAsync(ByDesk, Ct)).SourceVersion), Ct)).ChangedLeaves);
        // The source answered `second` since `first`: changes since `first` are no longer what it knows.
        source.Apply(fields.Batch(changed: [Row(2, "West", 21m)]));
        Assert.Null((await source.AggregateAsync(ByDesk.WithChangedSince(first.SourceVersion), Ct)).ChangedLeaves);
    }

    [Fact] // ADR-0161: the version a question names is no part of what it asks — the same leaves, and the same held answer
    public void The_version_named_takes_no_part_in_the_question()
    {
        var named = ByDesk.WithChangedSince("v1");

        Assert.Equal(ByDesk, named);
        Assert.Equal(ByDesk.GetHashCode(), named.GetHashCode());
        Assert.Equal("v1", named.ChangedSince);
        Assert.Null(ByDesk.ChangedSince);
        Assert.Same(named, named.WithChangedSince("v1"));
    }

    [Fact] // ADR-0161: an answer refuses changed leaves it does not have, out of order or twice
    public async Task An_answer_refuses_leaves_it_has_not()
    {
        var fields = Declarations();
        var answer = await PivotSource.From([Row(1, "East", 10m), Row(2, "West", 20m)], fields).AggregateAsync(ByDesk, Ct);

        Assert.Throws<ArgumentException>(() => answer.WithChangedLeaves(PivotLeafChanges.Of("v", [2])));
        Assert.Throws<ArgumentException>(() => answer.WithChangedLeaves(PivotLeafChanges.Of("v", [1, 0])));
        Assert.Throws<ArgumentException>(() => answer.WithChangedLeaves(PivotLeafChanges.Of("v", [1, 1])));
        Assert.Equal([1], answer.WithChangedLeaves(PivotLeafChanges.Of("v", [1])).ChangedLeaves!.Leaves);
        Assert.Null(answer.WithChangedLeaves(PivotLeafChanges.Of("v", [1])).WithChangedLeaves(null).ChangedLeaves);
    }
}
