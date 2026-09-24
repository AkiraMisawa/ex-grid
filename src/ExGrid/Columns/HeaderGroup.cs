namespace ExGrid.Columns;

/// <summary>
/// A labelled rectangle over adjacent leaf columns in the tiers above the header row,
/// declared by member column names (ADR-0032) — colspan/rowspan expressiveness without
/// a column tree. Membership is a set: the painted left-to-right order comes from the
/// flat Columns order, never this declaration's.
///
/// <para>Names, not indices, deliberately: an index declaration survives a reorder
/// formally valid while labelling the wrong columns; names turn the same mistake into
/// a refusal the grid can make (members no longer adjacent), and they record the
/// intent no reorder changes.</para>
/// </summary>
public sealed record HeaderGroup
{
    /// <summary>Declares one rectangle. Refused here: no member, an empty or repeated
    /// member name, and a span reaching below the leaf row. Adjacency is refused at
    /// resolution (ADR-0032).</summary>
    /// <param name="label">The caption. May be empty — a spacer rectangle — never null.</param>
    /// <param name="columns">The member column names. Adjacency in the current order is
    /// checked at resolution, where the order is known.</param>
    /// <param name="tier">The rectangle's top tier, 1 sitting directly above the leaf
    /// row.</param>
    /// <param name="tierSpan">How many tiers the rectangle spans downward from
    /// <paramref name="tier"/> — the rowspan (ADR-0032).</param>
    public HeaderGroup(string label, IReadOnlyList<string> columns, int tier = 1, int tierSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
            throw new ArgumentException($"Header Group '{label}' names no member columns.", nameof(columns));
        foreach (var name in columns)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"Header Group '{label}' names a null or empty member column.", nameof(columns));
        }
        if (columns.Distinct(StringComparer.Ordinal).Count() != columns.Count)
            throw new ArgumentException($"Header Group '{label}' names a member column twice.", nameof(columns));
        ArgumentOutOfRangeException.ThrowIfLessThan(tier, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(tierSpan, 1);
        if (tierSpan > tier)
        {
            throw new ArgumentOutOfRangeException(nameof(tierSpan), tierSpan,
                $"Header Group '{label}' spans {tierSpan} tiers downward from tier {tier}, past the leaf row.");
        }

        Label = label;
        Columns = columns.ToArray();
        Tier = tier;
        TierSpan = tierSpan;
    }

    /// <summary>The caption; empty for a spacer rectangle.</summary>
    public string Label { get; }

    /// <summary>The member column names, as a set: the painted order is the flat Columns
    /// order, never this list's.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>The rectangle's top tier, 1 sitting directly above the leaf row.</summary>
    public int Tier { get; }

    /// <summary>How many tiers the rectangle spans downward from <see cref="Tier"/> — the
    /// rowspan (ADR-0032).</summary>
    public int TierSpan { get; }
}
