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
/// a key, hashing it and comparing it within one report allocate nothing and walk nothing (PV-43):
/// the grid checks a whole report's keys on every redraw, and that check costs what its check of the
/// rows' instances did. Only a comparison across reports walks the Items, and Blazor makes one for a
/// painted row, not for every row.</para>
/// </summary>
public sealed class PivotRowKey : IEquatable<PivotRowKey>
{
    private readonly AxisNode _node;
    private readonly int _hash;

    internal PivotRowKey(PivotRowRole role, int valueField, AxisNode node)
    {
        Role = role;
        ValueField = valueField;
        _node = node;
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
                   && SameItems(_node, other._node)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PivotRowKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;

    /// <summary>The role, the Value Field and the Items, as a refusal names a key:
    /// <c>Group -1 [Text:East / Text:Rates]</c>.</summary>
    public override string ToString()
    {
        var items = new List<PivotItemKey>();
        for (var at = _node; at.Item is not null; at = at.Parent!)
            items.Add(at.Item.PublicKey);
        items.Reverse();
        var text = new StringBuilder();
        text.Append(Role).Append(' ').Append(ValueField).Append(" [");
        text.AppendJoin(" / ", items);
        return text.Append(']').ToString();
    }

    // Two nodes stand for the same Items when their paths name them level by level. Within one report
    // two rows' nodes are the same node or differ at once; across reports — another cube — the Items
    // are compared, outermost last, the Item of one cube's Items being shared by its nodes.
    private static bool SameItems(AxisNode a, AxisNode b)
    {
        while (!ReferenceEquals(a, b))
        {
            if (a.PathHash != b.PathHash || a.Level != b.Level)
                return false;
            var (itemA, itemB) = (a.Item, b.Item);
            if (itemA is null || itemB is null)
                return itemA is null && itemB is null;
            if (!ReferenceEquals(itemA, itemB) && !itemA.PublicKey.Equals(itemB.PublicKey))
                return false;
            a = a.Parent!;
            b = b.Parent!;
        }
        return true;
    }
}
