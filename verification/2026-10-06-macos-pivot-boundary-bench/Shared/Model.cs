using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoundaryBench;

// Disposable, original arithmetic model. Nothing here implements a proposed public API.
// Two row fields, one exact decimal Sum, no missing Items, stable Item identities.
public sealed record Leaf(int Id, int Group, string Label, decimal Value);
public sealed record Change(int Id, decimal Value);
public sealed record Row(int Id, string Label, decimal Value, string Kind);
public sealed record Patch(int Index, Row Row);
public sealed record Answer(int Version, bool Reset, Leaf[]? Leaves, Change[]? Changes,
    Row[]? Rows, Patch[]? Patches, int ReportRows);
public sealed record Oracle(string FullHash, string WindowHash, int ReportRows);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(Oracle))]
public partial class BenchJsonContext : JsonSerializerContext { }
public static class Codec
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Hash(IEnumerable<Row> rows)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var row in rows)
            hash.AppendData(Encoding.UTF8.GetBytes($"{row.Id}|{row.Label}|{row.Value.ToString(CultureInfo.InvariantCulture)}|{row.Kind}\n"));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed class Source
{
    private readonly decimal[] _records;
    private readonly decimal[] _sums;
    public int Groups { get; }
    public int LeavesCount => _sums.Length;
    public int PerGroup => _sums.Length / Groups;
    public int Version { get; private set; }
    public Source(int records, int leaves)
    {
        if (leaves is not (2976 or 100000 or 400000) || records < leaves)
            throw new ArgumentOutOfRangeException(nameof(leaves));
        Groups = leaves == 2976 ? 24 : 100;
        _records = new decimal[records];
        _sums = new decimal[leaves];
        for (var i = 0; i < records; i++)
        {
            var value = (10000 + i % 19001) / 100m;
            _records[i] = value;
            _sums[i % leaves] += value;
        }
    }
    public Leaf[] Leaves() => Enumerable.Range(0, _sums.Length)
        .Select(i => new Leaf(i, i / PerGroup, $"Item {i:D6}", _sums[i])).ToArray();
    public Change[] Apply(int batch, bool visible)
    {
        var result = new Change[batch];
        for (var i = 0; i < batch; i++)
        {
            // One visible leaf at most. All others are outside the first group's Window.
            var id = visible && i == 0 ? 0 : PerGroup + i;
            var increment = 0.01m + (Version % 11) / 100m;
            _records[id] += increment;
            _sums[id] += increment;
            result[i] = new Change(id, _sums[id]);
        }
        Version++;
        return result;
    }
    public Report FreshReport()
    {
        var totals = new decimal[_sums.Length];
        for (var i = 0; i < _records.Length; i++) totals[i % totals.Length] += _records[i];
        for (var i = 0; i < totals.Length; i++)
            if (totals[i] != _sums[i]) throw new InvalidOperationException("Source delta differs from a fresh fold.");
        return new Report(Enumerable.Range(0, totals.Length)
            .Select(i => new Leaf(i, i / PerGroup, $"Item {i:D6}", totals[i])).ToArray());
    }
}

public sealed class Report
{
    private readonly Row[] _leaves;
    private readonly Row[] _groups;
    private readonly int[] _leafGroups;
    private readonly int[][] _children;
    private Row _grand;
    public bool Collapsed { get; private set; }
    public bool Sorted { get; private set; }
    public int RowCount => _leaves.Length + _groups.Length + 1 - (Collapsed ? _children[0].Length : 0);
    public Report(Leaf[] leaves)
    {
        _leaves = new Row[leaves.Length];
        _leafGroups = new int[leaves.Length];
        _groups = new Row[leaves.Max(l => l.Group) + 1];
        var children = Enumerable.Range(0, _groups.Length).Select(_ => new List<int>()).ToArray();
        var sums = new decimal[_groups.Length];
        decimal grand = 0;
        foreach (var leaf in leaves)
        {
            _leaves[leaf.Id] = new Row(leaf.Id, leaf.Label, leaf.Value, "leaf");
            _leafGroups[leaf.Id] = leaf.Group;
            children[leaf.Group].Add(leaf.Id);
            sums[leaf.Group] += leaf.Value;
            grand += leaf.Value;
        }
        _children = children.Select(c => c.ToArray()).ToArray();
        for (var g = 0; g < _groups.Length; g++)
            _groups[g] = new Row(-2 - g, $"Group {g:D3}", sums[g], "subtotal");
        _grand = new Row(-1, "Grand Total", grand, "grand");
    }
    public void Apply(Change[] changes)
    {
        var totals = new Dictionary<int, decimal>();
        decimal grandDelta = 0;
        foreach (var change in changes)
        {
            var previous = _leaves[change.Id];
            var delta = change.Value - previous.Value;
            if (delta == 0) continue;
            _leaves[change.Id] = previous with { Value = change.Value };
            var group = _leafGroups[change.Id];
            totals[group] = totals.GetValueOrDefault(group) + delta;
            grandDelta += delta;
        }
        foreach (var (id, delta) in totals)
        {
            _groups[id] = _groups[id] with { Value = _groups[id].Value + delta };
            if (Sorted) SortGroup(id);
        }
        if (grandDelta != 0) _grand = _grand with { Value = _grand.Value + grandDelta };
    }
    public void ToggleExpand() => Collapsed = !Collapsed;
    public void Sort()
    {
        Sorted = !Sorted;
        for (var g = 0; g < _groups.Length; g++)
            if (Sorted) SortGroup(g); else Array.Sort(_children[g]);
    }
    private void SortGroup(int g) => Array.Sort(_children[g], (a, b) =>
    {
        var value = _leaves[b].Value.CompareTo(_leaves[a].Value);
        return value != 0 ? value : a.CompareTo(b);
    });
    public IEnumerable<Row> Rows()
    {
        for (var g = 0; g < _groups.Length; g++)
        {
            yield return _groups[g];
            if (g == 0 && Collapsed) continue;
            foreach (var id in _children[g]) yield return _leaves[id];
        }
        yield return _grand;
    }
    public Row[] Window() => Rows().Take(40).ToArray();
    public string FullHash() => Codec.Hash(Rows());
    public void MatchLayout(Report other)
    {
        if (other.Collapsed) ToggleExpand();
        if (other.Sorted) Sort();
    }
}
