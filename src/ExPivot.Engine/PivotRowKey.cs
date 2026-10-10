using System.Text;

namespace ExPivot.Engine;

/// <summary>
/// What a report row stands for, as a value (ADR-0140): its role, its Value Field and its Items,
/// outermost first. Two rows of the reports of one layout that stand for the same thing have equal
/// keys, and no two rows of one report do. ExPivot names its report rows to the grid by it, so a
/// live redraw repaints a changed row in place, and pairs a cell with its previous version by it
/// for the Change Highlight.
///
/// <para>It is made with the row, from the row's axis node, whose hash of its Items was made with the
/// node from its parent's and its Item's — and an Item's own hash is computed once per Item. Reading
/// a key, hashing it and comparing it within one report allocate nothing and walk nothing (PV-43).
/// The detached Item path owns no branching axis tree. ExPivot vouches for its requested Window
/// (ADR-0141's section of 2026-10-07; ADR-0153), so the grid does not validate the full report's keys on every redraw.
/// A comparison between distinct paths walks their Items, including comparisons across reports.</para>
/// </summary>
public sealed class PivotRowKey : IEquatable<PivotRowKey>
{
    private readonly PivotItemPath? _path;
    private IReadOnlyList<PivotItemKey>? _items;
    private readonly int _hash;

    internal PivotRowKey(PivotRowRole role, int valueField, AxisNode node)
    {
        Role = role;
        ValueField = valueField;
        _path = node.KeyPath;
        _hash = HashCode.Combine(role, valueField, node.PathHash);
    }

    /// <summary>What the row stands for.</summary>
    public PivotRowRole Role { get; }

    /// <summary>The Value Field the row's cells show when the values stand in rows, or −1.</summary>
    public int ValueField { get; }

    /// <summary>Whether <paramref name="other"/> stands for the same thing: the same role, Value Field
    /// and Items, Item by Item.</summary>
    public bool Equals(PivotRowKey? other)
        => other is not null
           && (ReferenceEquals(this, other)
               || (_hash == other._hash && Role == other.Role && ValueField == other.ValueField
                   && SameItems(_path, other._path)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PivotRowKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;

    /// <summary>The role, the Value Field and the Items, as a refusal names a key:
    /// <c>Group -1 [Text:East / Text:Rates]</c>.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append(Role).Append(' ').Append(ValueField).Append(" [");
        text.AppendJoin(" / ", Items);
        return text.Append(']').ToString();
    }

    /// <summary>Makes a detached key from immutable Item values, copying the sequence.</summary>
    public PivotRowKey(PivotRowRole role, int valueField, IReadOnlyList<PivotItemKey> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Role = role;
        ValueField = valueField;
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            _path = new PivotItemPath(_path, item);
        }
        _hash = HashCode.Combine(role, valueField, _path?.Hash ?? 0);
    }

    /// <summary>The detached Items, outermost first. The immutable sequence is materialized once.</summary>
    public IReadOnlyList<PivotItemKey> Items
    {
        get
        {
            if (_items is not null)
                return _items;
            var items = new PivotItemKey[_path?.Length ?? 0];
            for (var at = _path; at is not null; at = at.Parent)
                items[at.Length - 1] = at.Item;
            return _items = Array.AsReadOnly(items);
        }
    }

    private static bool SameItems(PivotItemPath? a, PivotItemPath? b)
    {
        while (!ReferenceEquals(a, b))
        {
            if (a is null || b is null || a.Hash != b.Hash || a.Length != b.Length || !a.Item.Equals(b.Item))
                return false;
            a = a.Parent;
            b = b.Parent;
        }
        return true;
    }
}

// A parent-only identity path. Unlike an axis node, it owns neither children nor a Cube.
internal sealed class PivotItemPath(PivotItemPath? parent, PivotItemKey item)
{
    public PivotItemPath? Parent { get; } = parent;
    public PivotItemKey Item { get; } = item;
    public int Hash { get; } = HashCode.Combine(parent?.Hash ?? 0, item.GetHashCode());
    public int Length { get; } = (parent?.Length ?? 0) + 1;
}
