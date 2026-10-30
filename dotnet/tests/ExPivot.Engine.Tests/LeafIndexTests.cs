using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The leaf index of a pass over a Snapshot (ADR-0066, ticket 09). A row's Items are packed into
/// one key: up to 20 bits it indexes an array, past that it is hashed through a mixing hash, past 62
/// bits a trie of (node, Item) keys takes over. The first engine keyed a leaf as
/// <c>(row &lt;&lt; 32) | column</c>, whose own hash is <c>row ^ column</c>: a layout of 270 dates
/// went super-linear, 23 s against 3.3 s.
/// </summary>
public class LeafIndexTests
{
    private sealed record Cell(string A, string B, string C, string D, string E, string F, string G, DateTime Date, decimal Amount);

    private static PivotFields<Cell> Fields() => PivotFields.Of<Cell>()
        .Text("A", c => c.A).Text("B", c => c.B).Text("C", c => c.C).Text("D", c => c.D)
        .Text("E", c => c.E).Text("F", c => c.F).Text("G", c => c.G)
        .Date("Date", c => c.Date)
        .Number("Amount", c => c.Amount);

    /// <summary>A pass over <paramref name="cells"/>, the leaves it found, and how it found them.</summary>
    private static (PivotAnswer Answer, string Mode) Pass(Cell[] cells, PivotQuery query)
    {
        var fields = Fields();
        var snapshot = fields.Build(cells);
        var (_, bindings) = FieldBinding.Of(snapshot, fields.Fields);
        var pass = new AggregationPass(snapshot, bindings, query, keepRows: false, rowsRead: null);
        Assert.True(pass.Step(0, pass.RowCount));
        pass.Complete();
        return (pass.Answer("1"), pass.Leaves.Mode);
    }

    /// <summary>Each leaf's records and sum, by its Items, held to a grouping of the records.</summary>
    private static void SameAsGrouping(Cell[] cells, PivotAnswer answer, Func<Cell, string> path)
    {
        var expected = cells.GroupBy(path).ToDictionary(g => g.Key, g => (Count: (long)g.Count(), Sum: g.Sum(c => c.Amount)));
        var axes = answer.Rows.Concat(answer.Columns).ToArray();
        Assert.Equal(expected.Count, answer.LeafCount);
        for (var leaf = 0; leaf < answer.LeafCount; leaf++)
        {
            var key = string.Join("|", axes.Select(axis => axis.Items[axis.ItemAt(leaf)].Value));
            Assert.True(expected.TryGetValue(key, out var group), $"no records at {key}");
            Assert.Equal(group.Count, answer.RecordsAt(leaf));
            Assert.Equal(PivotNumber.Exact(group.Sum), answer.Values[0].SumAt(leaf));
        }
    }

    private static string Iso(DateTime date) => PivotItemKey.Date(date).Value!;

    [Fact] // ADR-0066 (ticket 09): 270 dates by 12 books are 3,240 leaves, found by indexing, never by a hash that would collide
    public void Two_hundred_seventy_dates_by_twelve_books_are_found_directly()
    {
        var start = new DateTime(2026, 1, 2);
        var cells = (from day in Enumerable.Range(0, 270)
                     from book in Enumerable.Range(0, 12)
                     from copy in Enumerable.Range(0, 2)
                     select new Cell("B" + book.ToString(CultureInfo.InvariantCulture), "", "", "", "", "", "", start.AddDays(day), day + book + copy)).ToArray();
        var query = new PivotQuery(rows: [new("A")], columns: [new("Date")], values: [new("Amount", PivotParts.Sum)]);

        var (answer, mode) = Pass(cells, query);

        Assert.Equal("direct", mode);
        Assert.Equal(3_240, answer.LeafCount);
        SameAsGrouping(cells, answer, c => c.A + "|" + Iso(c.Date));
    }

    [Fact] // ADR-0066 (ticket 09): (node, Item) keys of a 12-by-270 layout spread over the table, where lo ^ hi gives a few hundred hash codes
    public void Packed_keys_are_hashed_apart()
    {
        var keys = (from node in Enumerable.Range(0, 12) from item in Enumerable.Range(0, 270) select CellKey.Of(node, item)).ToArray();
        var map = new LongIntMap(keys.Length);
        foreach (var key in keys)
            map.GetOrAdd(key, 0, out _);

        var homes = keys.Select(map.HomeSlot).Distinct().Count();
        var packed = keys.Select(key => key.GetHashCode() & (map.Capacity - 1)).Distinct().Count();

        // As good as random: about 1 − e^(−n/m) of the m slots are some key's home.
        var random = map.Capacity * (1 - Math.Exp(-(double)keys.Length / map.Capacity));
        Assert.True(homes > random * 0.95, $"{homes} home slots for {keys.Length} keys in {map.Capacity}; random gives {random:F0}");
        Assert.True(packed <= 512, $"lo ^ hi gave {packed} home slots");
    }

    [Fact] // ADR-0066 (ticket 09): past 20 bits the leaves are hashed, and are the records' groups
    public void A_wider_layout_is_hashed_and_right()
    {
        var random = new Random(9);
        var start = new DateTime(2026, 1, 2);
        var cells = Enumerable.Range(0, 20_000).Select(i => new Cell(
            "B" + random.Next(12).ToString(CultureInfo.InvariantCulture),
            "C" + random.Next(400).ToString(CultureInfo.InvariantCulture),
            "", "", "", "", "", start.AddDays(random.Next(270)), random.Next(1000))).ToArray();
        var query = new PivotQuery(rows: [new("A"), new("B")], columns: [new("Date")], values: [new("Amount", PivotParts.Sum)]);

        var (answer, mode) = Pass(cells, query);

        Assert.Equal("hashed", mode);
        SameAsGrouping(cells, answer, c => c.A + "|" + c.B + "|" + Iso(c.Date));
    }

    [Fact] // ADR-0066 (ticket 09): past 62 bits a trie of (node, Item) keys finds the leaves, and they are the records' groups
    public void A_layout_past_62_bits_is_found_through_a_trie()
    {
        var random = new Random(7);
        string Pick(string prefix) => prefix + random.Next(700).ToString(CultureInfo.InvariantCulture);
        var cells = Enumerable.Range(0, 3_000).Select(i => new Cell(Pick("a"), Pick("b"), Pick("c"), Pick("d"), Pick("e"), Pick("f"), Pick("g"),
            new DateTime(2026, 1, 1), i)).ToArray();
        var query = new PivotQuery(
            rows: [new("A"), new("B"), new("C"), new("D")],
            columns: [new("E"), new("F"), new("G")],
            values: [new("Amount", PivotParts.Sum)]);

        var (answer, mode) = Pass(cells, query);

        Assert.Equal("trie", mode);
        SameAsGrouping(cells, answer, c => string.Join("|", c.A, c.B, c.C, c.D, c.E, c.F, c.G));
    }
}
