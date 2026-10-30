using ExGrid.Columns;
using ExGrid.Rows;

namespace ExGrid.DemoPages;

/// <summary>A row of the Row Marks page: every value derives from its index, which is
/// also its key and — because rows are only ever appended — its creation stamp.</summary>
public sealed class DemoMarkRow
{
    public int Index;
    public string Desk = "";
    public decimal Amount;
}

/// <summary>
/// Stands in for the server behind the Row Marks page (ADR-0043): a million rows, a filter
/// on the desk, rows that can be added and deleted "elsewhere", and the Consumer's half of
/// the marks — the snapshot marker, the counts and the action — answered from the marks'
/// steps, never from positions. Not a model of a real server's storage; a model of what
/// one has to answer.
/// </summary>
public sealed class DemoMarkServer
{
    public static readonly string[] Desks = ["Alpha", "Bravo", "Charlie", "Delta", "Echo"];

    private readonly List<int> _live;
    private readonly HashSet<int> _deleted = [];
    private int _next;
    private (string? Desk, int Version, List<int> Rows)? _filtered;
    private int _version;

    public DemoMarkServer(int rows)
    {
        _live = new List<int>(rows);
        for (var i = 0; i < rows; i++)
            _live.Add(i);
        _next = rows;
    }

    public static readonly GridColumn<DemoMarkRow>[] Columns =
    [
        GridColumn<DemoMarkRow>.MarkColumn(width: new ColumnWidthSpec(ColumnWidth.Fixed(40))),
        new("Index", ColumnType.Number, r => r.Index, width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
        new("Desk", ColumnType.Text, r => r.Desk, width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
        new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(140))),
    ];

    public static string DeskOf(int index) => Desks[index % Desks.Length];

    private static DemoMarkRow Row(int index) => new()
    {
        Index = index,
        Desk = DeskOf(index),
        Amount = ((index * 7919L) % 1_999_999L) / 100m,
    };

    /// <summary>The one filter this server understands: Desk equals a value. Anything else
    /// is refused rather than ignored — a server that answered an unfiltered result under a
    /// filter would be quietly wrong.</summary>
    public static string? DeskFilter(GridFilter? filter)
    {
        if (filter is null || filter.Columns.Count == 0)
            return null;
        if (filter.Columns.Count == 1
            && filter.Columns.TryGetValue("Desk", out var spec)
            && spec.Clauses is [{ Operator: FilterOperator.Equals, Value: string desk }])
        {
            return desk;
        }
        throw new NotSupportedException("The demo server filters on Desk equals a value, and on nothing else.");
    }

    private static bool Matches(int index, string? desk)
        => desk is null || string.Equals(DeskOf(index), desk, StringComparison.OrdinalIgnoreCase);

    private List<int> Result(GridFilter? filter)
    {
        var desk = DeskFilter(filter);
        if (desk is null)
            return _live;
        if (_filtered is { } cached && cached.Version == _version
            && string.Equals(cached.Desk, desk, StringComparison.OrdinalIgnoreCase))
        {
            return cached.Rows;
        }
        var rows = _live.Where(i => Matches(i, desk)).ToList();
        _filtered = (desk, _version, rows);
        return rows;
    }

    public GridPage<DemoMarkRow> Fetch(GridQuery query)
    {
        if (query.Sorts.Count > 0)
            throw new NotSupportedException("The demo server keeps its own order; it does not sort.");
        var result = Result(query.Filter);
        var start = Math.Min(query.Range.Start, result.Count);
        var count = Math.Min(query.Range.Count, result.Count - start);
        var rows = new DemoMarkRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = Row(result[start + i]);
        return new GridPage<DemoMarkRow>(rows, start, result.Count);
    }

    /// <summary>A row added elsewhere: it joins the result after any "all" already taken.</summary>
    public void Add()
    {
        _live.Add(_next++);
        _version++;
    }

    /// <summary>A row deleted elsewhere: its mark can still name it until the screen hears.</summary>
    public void Delete(int index)
    {
        if (_live.Remove(index))
        {
            _deleted.Add(index);
            _version++;
        }
    }

    // ---- The Consumer's half of Row Marks ------------------------------------------

    public RowMarkAdapter<DemoMarkRow> Adapter() => new(
        key: row => row.Index,
        openSnapshot: (_, _) => ValueTask.FromResult<object>(_next),
        belongsTo: (row, snapshot) => BelongsTo(row.Index, snapshot),
        count: (state, filter, _) => ValueTask.FromResult(Count(state, filter)));

    private static bool BelongsTo(int index, RowMarkSnapshot snapshot)
        => index < (int)snapshot.AsOf && Matches(index, DeskFilter(snapshot.Filter));

    /// <summary>The marked live rows, as indices — the same evaluation the Source makes
    /// per painted row, made here over the whole data without a closure per row.</summary>
    private IEnumerable<int> Marked(RowMarkState state, IEnumerable<int> rows)
    {
        var keyed = new Dictionary<int, (int Step, bool Marked)>();
        var snapshots = new List<(int Step, RowMarkSnapshot Snapshot, string? Desk, bool Marked)>();
        for (var s = 0; s < state.Steps.Count; s++)
        {
            switch (state.Steps[s])
            {
                case RowMarkStep.OneKey one:
                    keyed[(int)one.Key] = (s, one.Marked);
                    break;
                case RowMarkStep.AllOf all:
                    snapshots.Add((s, all.Snapshot, DeskFilter(all.Snapshot.Filter), all.Marked));
                    break;
            }
        }
        foreach (var index in rows)
        {
            var (step, marked) = keyed.TryGetValue(index, out var key) ? key : (-1, false);
            for (var s = snapshots.Count - 1; s >= 0 && snapshots[s].Step > step; s--)
            {
                var snapshot = snapshots[s];
                if (index < (int)snapshot.Snapshot.AsOf && Matches(index, snapshot.Desk))
                {
                    marked = snapshot.Marked;
                    break;
                }
            }
            if (marked)
                yield return index;
        }
    }

    public RowMarkCounts Count(RowMarkState state, GridFilter? filter)
    {
        var desk = DeskFilter(filter);
        var inResult = 0;
        var total = 0;
        foreach (var index in Marked(state, _live))
        {
            total++;
            if (Matches(index, desk))
                inResult++;
        }
        return new RowMarkCounts(inResult, Result(filter).Count, total - inResult);
    }

    /// <summary>
    /// The action (ADR-0043): run over the marks by identity, verified against what exists
    /// now, and reported as it went — a mark naming a row deleted elsewhere is counted as
    /// gone, never passed off as done.
    /// </summary>
    public (int Applied, int Gone) Approve(RowMarkState state)
    {
        var applied = Marked(state, _live).Count();
        var gone = state.Steps.OfType<RowMarkStep.OneKey>()
            .Count(step => step.Marked && _deleted.Contains((int)step.Key));
        return (applied, gone);
    }
}
