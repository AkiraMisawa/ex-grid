using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// What the user changed in a running Board (ADR-0340, Q7), one part per view that differs from what
/// the Board writes: a grid's View State and the columns hidden from it, a pivot's layout, and a
/// chart's hidden series, range and axis (ADR-0272). Running a Board never rewrites it; the host may
/// keep this per user (<see cref="ToJson"/>), read it back (<see cref="Read"/>) and lay it over the
/// Board next time (the <c>ExBoard</c> component's <c>Diff</c>, <see cref="FitTo"/>). Making it the default is writing a
/// new Board.
/// </summary>
public sealed class BoardDiff
{
    /// <summary>The version this writer writes and the only one this reader reads.</summary>
    public const int Version = 1;

    private static readonly string[] RootKeys = ["version", "views"];
    private static readonly string[] Kinds = ["grid", "pivot", "chart"];
    private static readonly string[] GridKeys = ["viewState", "hidden", "kinds"];
    private static readonly string[] PivotKeys = ["layout", "kinds"];
    private static readonly string[] ChartKeys = ["hidden", "range", "scale"];
    private static readonly string[] RangeKeys = ["from", "to", "yFrom", "yTo"];

    /// <summary>A Diff of nothing: the Board as it is written.</summary>
    public static BoardDiff Empty { get; } = new(null, null, null);

    /// <summary>A Diff of these parts, each keyed by its view's id; a view has one part at most.</summary>
    /// <exception cref="ArgumentException">A view has more than one part.</exception>
    public BoardDiff(IReadOnlyDictionary<string, BoardGridChange>? grids, IReadOnlyDictionary<string, BoardPivotChange>? pivots,
        IReadOnlyDictionary<string, BoardChartChange>? charts)
    {
        Grids = Copy(grids);
        Pivots = Copy(pivots);
        Charts = Copy(charts);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in Grids.Keys.Concat(Pivots.Keys).Concat(Charts.Keys))
        {
            if (!named.Add(id))
                throw new ArgumentException($"View '{id}' has more than one part; a view is one of grid, pivot and chart.");
        }
    }

    /// <summary>The grids' parts, by view id.</summary>
    public IReadOnlyDictionary<string, BoardGridChange> Grids { get; }

    /// <summary>The pivots' parts, by view id.</summary>
    public IReadOnlyDictionary<string, BoardPivotChange> Pivots { get; }

    /// <summary>The charts' parts, by view id.</summary>
    public IReadOnlyDictionary<string, BoardChartChange> Charts { get; }

    /// <summary>Whether the user changed nothing.</summary>
    public bool IsEmpty => Grids.Count == 0 && Pivots.Count == 0 && Charts.Count == 0;

    /// <summary>
    /// The Diff as JSON, its views in the order of their ids:
    /// <c>{"version": 1, "views": {"blotter": {"grid": {"viewState": {…}, "hidden": […], "kinds": {"pnl": "decimal"}}},
    /// "byDesk": {"pivot": {"layout": {…}, "kinds": {…}}}, "pnl": {"chart": {"hidden": […], "range": {"from": …, "to": …},
    /// "scale": "categories"}}}}</c>. A grid's View State is <see cref="GridViewStateJson"/>'s and a pivot's layout
    /// <see cref="PivotLayoutJson"/>'s, each with its own version; <c>kinds</c> are the kinds the input declared
    /// the columns a part depends on as, when it was made.
    /// </summary>
    /// <exception cref="NotSupportedException">A grid's View State holds what its JSON cannot write
    /// (<see cref="GridViewStateJson.Write"/>).</exception>
    public string ToJson()
    {
        var views = new JsonObject();
        foreach (var id in Grids.Keys.Concat(Pivots.Keys).Concat(Charts.Keys).Order(StringComparer.Ordinal))
        {
            views[id] = Grids.TryGetValue(id, out var grid)
                ? new JsonObject
                {
                    ["grid"] = new JsonObject
                    {
                        ["viewState"] = JsonNode.Parse(GridViewStateJson.Write(grid.ViewState)),
                        ["hidden"] = Strings(grid.Hidden),
                        ["kinds"] = KindsJson(grid.Kinds),
                    },
                }
                : Pivots.TryGetValue(id, out var pivot)
                    ? new JsonObject
                    {
                        ["pivot"] = new JsonObject
                        {
                            ["layout"] = JsonNode.Parse(PivotLayoutJson.Write(pivot.Layout)),
                            ["kinds"] = KindsJson(pivot.Kinds),
                        },
                    }
                    : new JsonObject { ["chart"] = Chart(Charts[id]) };
        }
        return new JsonObject { ["version"] = Version, ["views"] = views }.ToJsonString();
    }

    /// <summary>Whether <paramref name="other"/> holds the same parts: what <see cref="ToJson"/> writes is the same.</summary>
    public bool SameAs(BoardDiff? other) => other is not null && (ReferenceEquals(this, other) || ToJson() == other.ToJson());

    /// <summary>
    /// The Diff <paramref name="json"/> holds, as <see cref="ToJson"/> wrote it. It is not yet laid
    /// against a Board: <see cref="BoundBoard.Bind"/> and the <c>ExBoard</c> component do that
    /// (<see cref="FitTo"/>), and say what no longer fits.
    /// </summary>
    /// <exception cref="BoardRefusedException">The JSON is not a Diff this reader reads: not JSON, a
    /// version other than <see cref="Version"/>, a member it does not have, a view that is not one of
    /// grid, pivot and chart, or a part its own reader refuses. Naming the path, as a Board's refusal does.</exception>
    public static BoardDiff Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            throw new BoardRefusedException("", $"A Board Diff is JSON: {e.Message}");
        }
        if (root is not JsonObject document)
            throw new BoardRefusedException("", "A Board Diff is an object.");
        Members(document, RootKeys, "", "A Board Diff");
        if (document["version"] is not JsonValue version || !version.TryGetValue<int>(out var number))
            throw new BoardRefusedException("version", $"A Board Diff names its version; this reader reads version {Version}.");
        if (number != Version)
            throw new BoardRefusedException("version", $"Version {number} is not one this reader reads; it reads version {Version}.");
        if (document["views"] is not JsonObject views)
            throw new BoardRefusedException("views", "A Board Diff holds its views' parts in 'views', an object keyed by view id.");

        var grids = new Dictionary<string, BoardGridChange>(StringComparer.Ordinal);
        var pivots = new Dictionary<string, BoardPivotChange>(StringComparer.Ordinal);
        var charts = new Dictionary<string, BoardChartChange>(StringComparer.Ordinal);
        foreach (var (id, value) in views)
        {
            var path = $"views.{id}";
            if (value is not JsonObject part || part.Count != 1 || !Kinds.Contains(part.First().Key))
                throw new BoardRefusedException(path, $"View '{id}''s part is an object holding one of grid, pivot and chart.");
            var (kind, inner) = part.First();
            path = $"{path}.{kind}";
            switch (kind)
            {
                case "grid":
                    grids[id] = ReadGrid(inner, path);
                    break;
                case "pivot":
                    pivots[id] = ReadPivot(inner, path);
                    break;
                default:
                    charts[id] = ReadChart(inner, path);
                    break;
            }
        }
        return new BoardDiff(grids, pivots, charts);
    }

    /// <summary>
    /// This Diff laid against <paramref name="board"/>: each part kept where it still fits, and what
    /// does not dropped by name, never applied to something else (spine rule 1). A part of a view the
    /// Board no longer has, or no longer has of that kind, is dropped; so is a column, a sort level or
    /// a column's filter on a column the grid no longer shows, and a pivot layout placing a field its
    /// input no longer declares. A column the grid shows that the part neither shows nor hides — one
    /// the Board has added since — is shown, last. A part that comes to what the Board writes is left out.
    /// </summary>
    public BoardDiffReading FitTo(BoardReading board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var dropped = new List<string>();
        var grids = new Dictionary<string, BoardGridChange>(StringComparer.Ordinal);
        var pivots = new Dictionary<string, BoardPivotChange>(StringComparer.Ordinal);
        var charts = new Dictionary<string, BoardChartChange>(StringComparer.Ordinal);
        foreach (var (id, change) in Grids)
        {
            if (Of<BoardGridView>(board, id, "grid", dropped) is { } view && FitGrid(board, view, change, dropped) is { } fitted)
                grids[id] = fitted;
        }
        foreach (var (id, change) in Pivots)
        {
            if (Of<BoardPivotView>(board, id, "pivot", dropped) is not { } view)
                continue;
            var declared = board.Input(view.Input)!.Shape;
            string? misfit = null;
            foreach (var field in Placed(change.Layout).Distinct(StringComparer.Ordinal))
            {
                if (!declared.TryGetColumn(field, out var column))
                    misfit = $"the layout places field '{field}', which input '{view.Input}' no longer declares.";
                else if (change.Kinds.TryGetValue(field, out var was) && was != column.Kind)
                    misfit = $"the layout places field '{field}' as {BoardInput.NameOf(was)}, which input '{view.Input}' now declares {BoardInput.NameOf(column.Kind)}.";
                if (misfit is not null)
                    break;
            }
            if (misfit is not null)
                dropped.Add($"views.{id}.pivot: {misfit}");
            else if (PivotLayoutJson.Write(change.Layout) != PivotLayoutJson.Write(view.Layout))
                pivots[id] = new BoardPivotChange(change.Layout, KindsOf(declared, Placed(change.Layout)));
        }
        foreach (var (id, change) in Charts)
        {
            if (Of<BoardChartView>(board, id, "chart", dropped) is not null && !change.IsNone)
                charts[id] = change;
        }
        return new BoardDiffReading(new BoardDiff(grids, pivots, charts), dropped.AsReadOnly());
    }

    internal static IEnumerable<string> Placed(PivotLayout layout) => layout.Filters.Select(f => f.Field)
        .Concat(layout.Rows.Select(f => f.Field)).Concat(layout.Columns.Select(f => f.Field)).Concat(layout.Values.Select(v => v.Field));

    private static T? Of<T>(BoardReading board, string id, string kind, List<string> dropped) where T : BoardView
    {
        switch (board.View(id))
        {
            case T view:
                return view;
            case null:
                dropped.Add($"views.{id}.{kind}: the Board no longer has view '{id}'.");
                return null;
            case var other:
                dropped.Add($"views.{id}.{kind}: view '{id}' is now a {BoardSpec.KindOf(other)}, not a {kind}.");
                return null;
        }
    }

    private static BoardGridChange? FitGrid(BoardReading board, BoardGridView view, BoardGridChange change, List<string> dropped)
    {
        var path = $"views.{view.Id}.grid";
        var shown = view.Grid.Columns.Select(c => c.Column).ToHashSet(StringComparer.Ordinal);
        var kinds = view.Grid.Columns.ToDictionary(c => c.Column, c => c.Kind, StringComparer.Ordinal);
        var state = change.ViewState;
        var columns = new List<ColumnViewState>();
        foreach (var column in state.Columns)
        {
            if (shown.Contains(column.Column))
                columns.Add(column);
            else
                dropped.Add($"{path}: column '{column.Column}' is no longer shown by view '{view.Id}'.");
        }
        var hidden = new List<string>();
        foreach (var column in change.Hidden)
        {
            if (shown.Contains(column))
                hidden.Add(column);
            else
                dropped.Add($"{path}: column '{column}', hidden, is no longer shown by view '{view.Id}'.");
        }
        var placed = columns.Select(c => c.Column).Concat(hidden).ToHashSet(StringComparer.Ordinal);
        foreach (var added in view.Grid.ViewState.Columns.Where(c => !placed.Contains(c.Column)))
            columns.Add(new ColumnViewState(added.Column, added.Width));
        var sorts = new List<SortSpec>();
        foreach (var sort in state.Sorts)
        {
            if (shown.Contains(sort.Column))
                sorts.Add(sort);
            else
                dropped.Add($"{path}: the sort on column '{sort.Column}', which view '{view.Id}' no longer shows.");
        }
        GridFilter? filter = null;
        if (state.Filter is { } held)
        {
            var kept = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
            foreach (var (column, spec) in held.Columns)
            {
                // A filter's operands are of its column's kind: one set on a column declared another kind
                // then would be laid over a column it no longer reads (spine rule 1).
                if (!shown.Contains(column))
                    dropped.Add($"{path}: the filter on column '{column}', which view '{view.Id}' no longer shows.");
                else if (change.Kinds.TryGetValue(column, out var was) && was != kinds[column])
                    dropped.Add($"{path}: the filter on column '{column}', set on {BoardInput.NameOf(was)}, which the column now is not; it is {BoardInput.NameOf(kinds[column])}.");
                else
                    kept[column] = spec;
            }
            filter = kept.Count == 0 && held.Opaque is null ? null : new GridFilter(kept, held.Opaque);
        }
        var fitted = new BoardGridChange(new GridViewState(columns, sorts, filter), hidden,
            filter is null ? null : filter.Columns.Keys.ToDictionary(c => c, c => kinds[c], StringComparer.Ordinal));
        return fitted.ViewState == view.Grid.ViewState && fitted.Hidden.Count == 0 ? null : fitted;
    }

    private static BoardGridChange ReadGrid(JsonNode? node, string path)
    {
        if (node is not JsonObject grid)
            throw new BoardRefusedException(path, "A grid's part is an object holding its 'viewState' and its 'hidden' columns.");
        Members(grid, GridKeys, path, "A grid's part");
        if (grid["viewState"] is not JsonObject viewState)
            throw new BoardRefusedException($"{path}.viewState", "A grid's part holds its View State in 'viewState'.");
        // Read against the names it holds itself: whether each is still the grid's is FitTo's, which
        // drops by name what this reader would refuse whole.
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in new[] { viewState["columns"], viewState["sorts"] })
        {
            if (list is JsonArray items)
            {
                foreach (var item in items)
                {
                    if (item is JsonObject one && one["column"] is JsonValue name && name.TryGetValue<string>(out var text))
                        names.Add(text);
                }
            }
        }
        if (viewState["filter"] is JsonObject filter && filter["columns"] is JsonObject filtered)
            names.UnionWith(filtered.Select(p => p.Key));
        GridViewState state;
        try
        {
            state = GridViewStateJson.Read(viewState.ToJsonString(), names);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            throw new BoardRefusedException($"{path}.viewState", e.Message);
        }
        return new BoardGridChange(state, Texts(grid["hidden"], $"{path}.hidden", "A grid's 'hidden' is a list of column names."),
            ReadKinds(grid["kinds"], $"{path}.kinds"));
    }

    private static BoardPivotChange ReadPivot(JsonNode? node, string path)
    {
        if (node is not JsonObject pivot)
            throw new BoardRefusedException(path, "A pivot's part is an object holding its 'layout'.");
        Members(pivot, PivotKeys, path, "A pivot's part");
        if (pivot["layout"] is not JsonObject layout)
            throw new BoardRefusedException($"{path}.layout", "A pivot's part holds its Pivot Layout in 'layout'.");
        try
        {
            return new BoardPivotChange(PivotLayoutJson.Read(layout.ToJsonString()), ReadKinds(pivot["kinds"], $"{path}.kinds"));
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException or InvalidOperationException)
        {
            throw new BoardRefusedException($"{path}.layout", $"The pivot's layout cannot be read: {e.Message}");
        }
    }

    private static Dictionary<string, SnapshotKind>? ReadKinds(JsonNode? node, string path)
    {
        if (node is null)
            return null;
        if (node is not JsonObject kinds)
            throw new BoardRefusedException(path, "A part's 'kinds' is an object naming each column's kind.");
        var read = new Dictionary<string, SnapshotKind>(StringComparer.Ordinal);
        foreach (var (column, value) in kinds)
        {
            if (value is not JsonValue name || !name.TryGetValue<string>(out var text) || !BoardInput.KindNames.Contains(text))
                throw new BoardRefusedException($"{path}.{column}", $"A column's kind is one of {string.Join(", ", BoardInput.KindNames)}.");
            read[column] = Enum.GetValues<SnapshotKind>().First(k => BoardInput.NameOf(k) == text);
        }
        return read;
    }

    private static JsonObject KindsJson(IReadOnlyDictionary<string, SnapshotKind> kinds)
    {
        var json = new JsonObject();
        foreach (var (column, kind) in kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            json[column] = BoardInput.NameOf(kind);
        return json;
    }

    internal static Dictionary<string, SnapshotKind> KindsOf(DataShape shape, IEnumerable<string> columns)
    {
        var kinds = new Dictionary<string, SnapshotKind>(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            if (shape.TryGetColumn(column, out var declared))
                kinds[column] = declared.Kind;
        }
        return kinds;
    }

    private static BoardChartChange ReadChart(JsonNode? node, string path)
    {
        if (node is not JsonObject chart)
            throw new BoardRefusedException(path, "A chart's part is an object.");
        Members(chart, ChartKeys, path, "A chart's part");
        var hidden = Texts(chart["hidden"], $"{path}.hidden", "A chart's 'hidden' is a list of series keys.");
        ChartRange? range = null;
        if (chart["range"] is { } rangeNode)
        {
            if (rangeNode is not JsonObject value)
                throw new BoardRefusedException($"{path}.range", "A chart's range is an object.");
            Members(value, RangeKeys, $"{path}.range", "A chart's range");
            double? End(string key) => value[key] switch
            {
                null => null,
                JsonValue end when end.TryGetValue<double>(out var number) => number,
                _ => throw new BoardRefusedException($"{path}.range.{key}", "An end of a range is a number in the axis' values."),
            };
            range = new ChartRange(End("from"), End("to"), End("yFrom"), End("yTo"));
        }
        AxisScale? scale = null;
        if (chart["scale"] is { } scaleNode)
        {
            scale = scaleNode is JsonValue name && name.TryGetValue<string>(out var text)
                && Enum.GetNames<AxisScale>().FirstOrDefault(n => Camel(n) == text) is { } known
                ? Enum.Parse<AxisScale>(known)
                : throw new BoardRefusedException($"{path}.scale", $"A chart's scale is one of {string.Join(", ", Enum.GetNames<AxisScale>().Select(Camel))}.");
        }
        return new BoardChartChange(hidden, range, scale);
    }

    private static JsonObject Chart(BoardChartChange change)
    {
        var chart = new JsonObject { ["hidden"] = Strings(change.HiddenSeries) };
        if (change.Range is { } range)
        {
            var value = new JsonObject();
            void End(string key, double? end)
            {
                if (end is { } number)
                    value[key] = number;
            }
            End("from", range.From);
            End("to", range.To);
            End("yFrom", range.YFrom);
            End("yTo", range.YTo);
            chart["range"] = value;
        }
        if (change.Scale is { } scale)
            chart["scale"] = Camel(scale.ToString());
        return chart;
    }

    private static string Camel(string name) => string.Concat(char.ToLowerInvariant(name[0]).ToString(CultureInfo.InvariantCulture), name[1..]);

    private static JsonArray Strings(IEnumerable<string> values) => [.. values.Select(v => (JsonNode)JsonValue.Create(v))];

    private static string[] Texts(JsonNode? node, string path, string message)
    {
        if (node is null)
            return [];
        if (node is not JsonArray items)
            throw new BoardRefusedException(path, message);
        var texts = new List<string>();
        foreach (var item in items)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var text))
                throw new BoardRefusedException(path, message);
            texts.Add(text);
        }
        return [.. texts];
    }

    private static void Members(JsonObject node, string[] known, string path, string what)
    {
        foreach (var (name, _) in node)
        {
            if (!known.Contains(name))
                throw new BoardRefusedException(path.Length == 0 ? name : $"{path}.{name}", $"{what} has no member '{name}'.");
        }
    }

    private static IReadOnlyDictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T>? parts)
    {
        var copy = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (id, part) in parts ?? new Dictionary<string, T>())
        {
            ArgumentNullException.ThrowIfNull(id);
            copy[id] = part ?? throw new ArgumentException($"View '{id}''s part is null.");
        }
        return copy.AsReadOnly();
    }
}

/// <summary>A Board Diff read back and laid against a Board (<see cref="BoardDiff.FitTo"/>).</summary>
/// <param name="Diff">The parts that still fit.</param>
/// <param name="Dropped">One sentence per part, or part of one, that no longer fits, naming its path.</param>
public sealed record BoardDiffReading(BoardDiff Diff, IReadOnlyList<string> Dropped);

/// <summary>A grid's part of a Board Diff: the View State it is shown under, and the columns the user
/// hid, which the View State leaves out — so that a column the Board adds later is shown, not taken
/// for one hidden.</summary>
public sealed class BoardGridChange
{
    /// <summary>A grid's part: <paramref name="viewState"/>, and <paramref name="hidden"/>, the columns it
    /// leaves out; <paramref name="kinds"/> are the kinds of the columns its filter reads, as the input
    /// declared them when it was set, so a filter on a column declared another kind since is dropped,
    /// not laid over it. Null where they are not known, which checks nothing.</summary>
    /// <exception cref="ArgumentException">A column is both shown and hidden.</exception>
    public BoardGridChange(GridViewState viewState, IEnumerable<string> hidden, IReadOnlyDictionary<string, SnapshotKind>? kinds = null)
    {
        Kinds = new Dictionary<string, SnapshotKind>(kinds ?? new Dictionary<string, SnapshotKind>(), StringComparer.Ordinal).AsReadOnly();
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(hidden);
        ViewState = viewState;
        Hidden = Array.AsReadOnly(hidden.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        if (Hidden.FirstOrDefault(h => viewState.Columns.Any(c => c.Column == h)) is { } both)
            throw new ArgumentException($"Column '{both}' is both shown and hidden.", nameof(hidden));
    }

    /// <summary>The columns shown, their order, widths and pinning, and the sort and the filter.</summary>
    public GridViewState ViewState { get; }

    /// <summary>The columns the user hid, in the order of their names.</summary>
    public IReadOnlyList<string> Hidden { get; }

    /// <summary>The kinds of the columns its filter reads, as declared when it was set.</summary>
    public IReadOnlyDictionary<string, SnapshotKind> Kinds { get; }
}

/// <summary>A pivot's part of a Board Diff: the layout the user made, and the kinds of the fields it
/// places, as the input declared them then.</summary>
public sealed class BoardPivotChange
{
    /// <summary>A pivot's part: <paramref name="layout"/>, its fields of <paramref name="kinds"/>; null
    /// where they are not known, which checks nothing.</summary>
    public BoardPivotChange(PivotLayout layout, IReadOnlyDictionary<string, SnapshotKind>? kinds = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Layout = layout;
        Kinds = new Dictionary<string, SnapshotKind>(kinds ?? new Dictionary<string, SnapshotKind>(), StringComparer.Ordinal).AsReadOnly();
    }

    /// <summary>The layout.</summary>
    public PivotLayout Layout { get; }

    /// <summary>The kinds of the fields it places, as declared when it was made.</summary>
    public IReadOnlyDictionary<string, SnapshotKind> Kinds { get; }
}

/// <summary>A chart's part of a Board Diff (ADR-0272), in the data's terms.</summary>
public sealed class BoardChartChange
{
    /// <summary>A chart's part: the series hidden, the range narrowed to, and the axis switched to.</summary>
    public BoardChartChange(IEnumerable<string> hiddenSeries, ChartRange? range, AxisScale? scale)
    {
        ArgumentNullException.ThrowIfNull(hiddenSeries);
        HiddenSeries = new SortedSet<string>(hiddenSeries, StringComparer.Ordinal);
        Range = range;
        Scale = scale;
    }

    /// <summary>The keys of the series hidden, in order.</summary>
    public IReadOnlySet<string> HiddenSeries { get; }

    /// <summary>The range shown, or null for the whole.</summary>
    public ChartRange? Range { get; }

    /// <summary>The axis the user switched to, or null for the declared one.</summary>
    public AxisScale? Scale { get; }

    internal bool IsNone => HiddenSeries.Count == 0 && Range is null && Scale is null;
}
