using System.Text;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// In UTF-8, a CSV's Text column tells its values apart by their bytes, and the dictionary's codes by
/// text are made when first asked for (ticket 07). The dictionary must still hold every distinct value
/// once, in order of first appearance, and answer a lookup by text — from a Change Batch or a reader,
/// from any thread — as if it had been made during the load (ADR-0063).
/// </summary>
public class CsvTextTests
{
    [Fact] // ADR-0063: a Text column of many distinct values takes each once, in order of first appearance, however many there are
    public void Many_distinct_texts_take_codes_in_order_of_first_appearance()
    {
        // 100,000 distinct values, then each again in another order: far past where a cache that only
        // spares decoding would give up.
        const int distinct = 100_000;
        var text = new StringBuilder("V\n");
        for (var i = 0; i < distinct; i++)
            text.Append('v').Append(i).Append('\n');
        for (var i = 0; i < distinct; i++)
            text.Append('v').Append((i * 7_919L) % distinct).Append('\n');

        var snapshot = Read(Texts("V"), text.ToString());

        var dictionary = ((TextColumn)snapshot["V"]).Dictionary;
        Assert.Equal(Enumerable.Range(0, distinct).Select(i => $"v{i}"), dictionary);
        var codes = Codes(snapshot, "V");
        Assert.Equal(Enumerable.Range(0, distinct), codes.Take(distinct));
        Assert.Equal(Enumerable.Range(0, distinct).Select(i => (int)((i * 7_919L) % distinct)), codes.Skip(distinct));
        Assert.True(dictionary.TryGetCode("v99999", out var code));
        Assert.Equal(99_999, code);
        Assert.True(dictionary.TryGetCode("v123".AsSpan(), out code));
        Assert.Equal(123, code);
        Assert.False(dictionary.TryGetCode("v100000", out _));
    }

    [Fact] // ADR-0063: a Change Batch applied to a Snapshot read from a CSV finds its keys and its texts by their text
    public void A_change_batch_finds_the_texts_of_a_snapshot_read_from_a_csv()
    {
        var schema = new CsvSchema([new("Id", SnapshotKind.Text), new("Desk", SnapshotKind.Text), new("Qty", SnapshotKind.Integer)]) { RecordKey = "Id" };
        var text = new StringBuilder("Id,Desk,Qty\n");
        for (var i = 0; i < 70_000; i++)
            text.Append('T').Append(i).Append(',').Append(i % 3 == 0 ? "FX" : "Rates").Append(',').Append(i).Append('\n');
        var snapshot = Read(schema, text.ToString());
        var batchSchema = schema with { RecordKey = null };

        var change = snapshot.Apply(ChangeBatch.Of(
            added: Read(batchSchema, "Id,Desk,Qty\nT70000,FX,1\nT70001,Credit,2\n"),
            changed: Read(batchSchema, "Id,Desk,Qty\nT5,Credit,50\nT69999,FX,7\n"),
            removedKeys: ["T0", "T42"]));

        var after = change.After;
        Assert.Equal(70_000, after.RowCount);
        Assert.Equal(["FX", "Rates", "Credit"], ((TextColumn)after["Desk"]).Dictionary);
        var id = (TextColumn)after["Id"];
        var rows = after.Rows.ToDictionary(r => (string)after.ValueAt(r, id)!);
        Assert.Equal("Credit", after.ValueAt(rows["T5"], after["Desk"]));
        Assert.Equal(50L, after.ValueAt(rows["T5"], after["Qty"]));
        Assert.Equal("FX", after.ValueAt(rows["T69999"], after["Desk"]));
        Assert.False(rows.ContainsKey("T0"));
        Assert.False(rows.ContainsKey("T42"));
        Assert.Equal(2L, after.ValueAt(rows["T70001"], after["Qty"]));
        // A key carried twice is still refused by name.
        Assert.Equal("T7", Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(added: Read(batchSchema, "Id,Desk,Qty\nT7,FX,1\n")))).Key);
    }

    [Fact] // ADR-0063: a Snapshot read from a CSV answers lookups by text from several threads at once
    public void A_dictionary_read_from_a_csv_is_looked_up_from_several_threads_at_once()
    {
        const int distinct = 20_000;
        var text = new StringBuilder("V\n");
        for (var i = 0; i < distinct; i++)
            text.Append("value ").Append(i).Append('\n');
        var dictionary = ((TextColumn)Read(Texts("V"), text.ToString())["V"]).Dictionary;
        var wrong = 0;

        Parallel.For(0, 8, worker =>
        {
            for (var i = 0; i < distinct; i++)
            {
                var at = (i + (worker * 2_500)) % distinct;
                if (!dictionary.TryGetCode($"value {at}", out var code) || code != at)
                    Interlocked.Increment(ref wrong);
            }
        });

        Assert.Equal(0, wrong);
    }

    [Fact] // ADR-0063: texts appended unindexed are found by an interner that is then asked to look one up
    public void An_interner_indexes_what_was_appended_unindexed_before_it_looks_up()
    {
        var interner = new TextInterner();
        Assert.Equal(0, interner.AddNew("a"));
        Assert.Equal(1, interner.AddNew("b"));

        Assert.Equal(1, interner.Intern("b"));
        Assert.Equal(0, interner.Intern("a".AsSpan()));
        Assert.Equal(2, interner.Intern("c"));
        Assert.Equal(3, interner.AddNew("d"));
        Assert.Equal(3, interner.Intern("d"));

        var store = interner.ToStore();
        Assert.True(store.TryGetCode("c", 4, out var code));
        Assert.Equal(2, code);
        Assert.False(store.TryGetCode("d", 3, out _));
    }
}
