using System.Numerics;

namespace ExPivot.Engine;

/// <summary>
/// The leaves of one question (ADR-0065): one per combination of the row and column fields'
/// Items that has an included record, each with its Items and its record count.
/// <para>
/// A row's Items are packed into one 64-bit key, a field of bits per level wide enough for the
/// level's Items; the bits widen as a level meets more Items, and the table is re-keyed from the
/// leaves' own Items. Up to <see cref="DirectBits"/> bits the key indexes an array directly — the
/// common report, whose fields have few Items, never hashes. Past that the key is hashed through a
/// mixing hash (<see cref="LongIntMap"/>): packed small integers hashed by their own
/// <c>lo ^ hi</c> made a 270-date report super-linearly slow (ticket 09). Past 62 bits, a trie of
/// (node, Item) keys takes over.
/// </para>
/// </summary>
internal sealed class LeafIndex
{
    /// <summary>The widest key indexed directly: an array of 2^20 entries, 4 MiB.</summary>
    internal const int DirectBits = 20;

    private const int TrieBits = 62;

    private readonly ItemSpace[] _levels;
    private readonly int _maxLeaves;
    private readonly int[] _bits;
    private readonly int[] _shift;
    private int[]? _direct;     // packed key → leaf + 1
    private LongIntMap? _hashed; // packed key → leaf
    private LongIntMap? _trie;   // (node, Item) → node, or leaf at the last level
    private int _nodes;
    private long[] _keys = [];

    public LeafIndex(ItemSpace[] levels, int maxLeaves)
    {
        _levels = levels;
        _maxLeaves = maxLeaves;
        _bits = new int[levels.Length];
        _shift = new int[levels.Length];
        ItemOfLeaf = new int[levels.Length][];
        for (var level = 0; level < levels.Length; level++)
            ItemOfLeaf[level] = new int[16];
        Records = new long[16];
        Rekey();
    }

    /// <summary>Each level's Item at each leaf.</summary>
    public int[][] ItemOfLeaf { get; }

    /// <summary>The included records at each leaf.</summary>
    public long[] Records { get; private set; }

    /// <summary>How many leaves there are, empty ones included.</summary>
    public int Count { get; private set; }

    /// <summary>How the leaves are found, for the tests: direct, hashed, or trie.</summary>
    internal string Mode => _direct is not null ? "direct" : _hashed is not null ? "hashed" : _levels.Length == 0 ? "single" : "trie";

    /// <summary>The hashed table, when the keys are hashed, for the hash's own test.</summary>
    internal LongIntMap? Hashed => _hashed;

    /// <summary>
    /// Finds or makes the leaf of each row of a chunk whose Items per level are in
    /// <paramref name="items"/>; a row <paramref name="excluded"/> marks gets −1. Answers how many
    /// rows were consumed: all of them, or — when a new leaf would pass the question's cap — the
    /// rows before the one that would make it, with <paramref name="refused"/> set (ADR-0065: the
    /// question is refused as soon as the cap is passed).
    /// </summary>
    public int Assign(int[][] items, ReadOnlySpan<byte> excluded, Span<int> leaves, out bool refused)
    {
        refused = false;
        var count = leaves.Length;
        if (_levels.Length == 0)
        {
            for (var i = 0; i < count; i++)
            {
                if (excluded[i] != 0)
                {
                    leaves[i] = -1;
                    continue;
                }
                if (Count == 0 && !TryNewLeaf(items, i, out _))
                {
                    refused = true;
                    return i;
                }
                Records[0]++;
                leaves[i] = 0;
            }
            return count;
        }

        Widen();
        if (_trie is not null)
            return AssignByTrie(items, excluded, leaves, out refused);

        if (_keys.Length < count)
            _keys = new long[Math.Max(count, 1024)];
        var keys = _keys.AsSpan(0, count);
        keys.Clear();
        for (var level = 0; level < _levels.Length; level++)
        {
            var shift = _shift[level];
            var levelItems = items[level];
            for (var i = 0; i < count; i++)
                keys[i] |= (long)levelItems[i] << shift;
        }

        var records = Records;
        if (_direct is { } direct)
        {
            for (var i = 0; i < count; i++)
            {
                if (excluded[i] != 0)
                {
                    leaves[i] = -1;
                    continue;
                }
                ref var slot = ref direct[keys[i]];
                if (slot == 0)
                {
                    if (!TryNewLeaf(items, i, out var made))
                    {
                        refused = true;
                        return i;
                    }
                    slot = made + 1;
                    records = Records;
                }
                var leaf = slot - 1;
                records[leaf]++;
                leaves[i] = leaf;
            }
            return count;
        }

        var hashed = _hashed!;
        for (var i = 0; i < count; i++)
        {
            if (excluded[i] != 0)
            {
                leaves[i] = -1;
                continue;
            }
            if (!hashed.TryGetValue(keys[i], out var leaf))
            {
                if (!TryNewLeaf(items, i, out leaf))
                {
                    refused = true;
                    return i;
                }
                hashed.GetOrAdd(keys[i], leaf, out _);
                records = Records;
            }
            records[leaf]++;
            leaves[i] = leaf;
        }
        return count;
    }

    private int AssignByTrie(int[][] items, ReadOnlySpan<byte> excluded, Span<int> leaves, out bool refused)
    {
        refused = false;
        var trie = _trie!;
        var last = _levels.Length - 1;
        for (var i = 0; i < leaves.Length; i++)
        {
            if (excluded[i] != 0)
            {
                leaves[i] = -1;
                continue;
            }
            var node = 0;
            for (var level = 0; level < last; level++)
            {
                var key = CellKey.Of(node, items[level][i]);
                if (!trie.TryGetValue(key, out var next))
                {
                    next = ++_nodes;
                    trie.GetOrAdd(key, next, out _);
                }
                node = next;
            }
            var leafKey = CellKey.Of(node, items[last][i]) | long.MinValue;
            if (!trie.TryGetValue(leafKey, out var leaf))
            {
                if (!TryNewLeaf(items, i, out leaf))
                {
                    refused = true;
                    return i;
                }
                trie.GetOrAdd(leafKey, leaf, out _);
            }
            Records[leaf]++;
            leaves[i] = leaf;
        }
        return leaves.Length;
    }

    private bool TryNewLeaf(int[][] items, int row, out int leaf)
    {
        if (Count >= _maxLeaves)
        {
            leaf = -1;
            return false;
        }
        leaf = Count++;
        if (leaf == Records.Length)
        {
            var size = Records.Length * 2;
            var records = Records;
            Array.Resize(ref records, size);
            Records = records;
            for (var level = 0; level < _levels.Length; level++)
                Array.Resize(ref ItemOfLeaf[level], size);
        }
        for (var level = 0; level < _levels.Length; level++)
            ItemOfLeaf[level][leaf] = items[level][row];
        return true;
    }

    // Widens a level whose Items no longer fit its bits, and re-keys.
    private void Widen()
    {
        if (_trie is not null)
            return;
        var wider = false;
        for (var level = 0; level < _levels.Length; level++)
        {
            if (_levels[level].Count > (1L << _bits[level]))
            {
                wider = true;
                break;
            }
        }
        if (wider)
            Rekey();
    }

    private void Rekey()
    {
        if (_levels.Length == 0)
            return;
        var total = 0;
        for (var level = 0; level < _levels.Length; level++)
        {
            var needed = BitsFor(_levels[level].Count);
            // A level that grows takes a bit of room beyond what it needs, so that a field met an
            // Item at a time re-keys only as often as it doubles.
            _bits[level] = needed > _bits[level] && _bits[level] > 0 ? needed + 1 : Math.Max(_bits[level], needed);
            total += _bits[level];
        }
        _direct = null;
        _hashed = null;
        if (total > TrieBits)
        {
            _trie = new LongIntMap(Count * 2);
            _nodes = 0;
            var trie = _trie;
            var last = _levels.Length - 1;
            for (var leaf = 0; leaf < Count; leaf++)
            {
                var node = 0;
                for (var level = 0; level < last; level++)
                {
                    var key = CellKey.Of(node, ItemOfLeaf[level][leaf]);
                    if (!trie.TryGetValue(key, out var next))
                    {
                        next = ++_nodes;
                        trie.GetOrAdd(key, next, out _);
                    }
                    node = next;
                }
                trie.GetOrAdd(CellKey.Of(node, ItemOfLeaf[last][leaf]) | long.MinValue, leaf, out _);
            }
            return;
        }
        var shift = 0;
        for (var level = _levels.Length - 1; level >= 0; level--)
        {
            _shift[level] = shift;
            shift += _bits[level];
        }
        if (total <= DirectBits)
            _direct = new int[1 << total];
        else
            _hashed = new LongIntMap(Math.Max(16, Count));
        for (var leaf = 0; leaf < Count; leaf++)
        {
            long key = 0;
            for (var level = 0; level < _levels.Length; level++)
                key |= (long)ItemOfLeaf[level][leaf] << _shift[level];
            if (_direct is not null)
                _direct[key] = leaf + 1;
            else
                _hashed!.GetOrAdd(key, leaf, out _);
        }
    }

    // The bits that hold Items 0 to count − 1, and at least one.
    private static int BitsFor(int count) => count <= 2 ? 1 : 64 - BitOperations.LeadingZeroCount((ulong)(count - 1));
}
