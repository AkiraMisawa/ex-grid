using System.Globalization;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Columns;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// A Board with every input bound to its data (ADR-0341): each input's shape checked against its
/// declaration, and each view made what it draws. Built by <see cref="Bind"/>; drawn by the
/// <c>ExBoard</c> component.
/// </summary>
public sealed class BoundBoard
{
    private readonly Dictionary<BoardLink, LinkCondition> _conditions = [];
    private readonly Dictionary<string, (LinkCondition Condition, Snapshot From)> _taken = new(StringComparer.Ordinal);

    private BoundBoard(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, IReadOnlyDictionary<string, BoardSource> sources,
        IReadOnlyList<BoundView> views, IReadOnlyList<string> dropped)
    {
        Board = board;
        Data = data;
        Sources = sources;
        Views = views;
        Dropped = dropped;
    }

    /// <summary>What of the Diff laid over at <see cref="Bind"/> no longer fit the Board, one sentence
    /// per part naming its path (<see cref="BoardDiff.FitTo"/>); empty when everything applied.</summary>
    public IReadOnlyList<string> Dropped { get; }

    /// <summary>
    /// What the user has changed, as one Board Diff (ADR-0340, Q7): each view's part that differs from
    /// what the Board writes. The Board itself is never rewritten.
    /// </summary>
    public BoardDiff Diff => new(
        Parts(Views.OfType<BoundGridView>().Select(v => (v.View.Id, v.Change))),
        Parts(Views.OfType<BoundPivotView>().Select(v => (v.View.Id, v.Change))),
        Parts(Views.OfType<BoundChartView>().Select(v => (v.View.Id, v.Change))));

    // Each view's part read once, and those with none left out.
    private static Dictionary<string, T> Parts<T>(IEnumerable<(string Id, T? Part)> parts) where T : class
    {
        var kept = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (id, part) in parts)
        {
            if (part is not null)
                kept[id] = part;
        }
        return kept;
    }

    /// <summary>The Board.</summary>
    public BoardReading Board { get; }

    /// <summary>The Snapshot each input held in memory is bound to.</summary>
    public IReadOnlyDictionary<string, Snapshot> Data { get; }

    /// <summary>The source each input held where the data lives is bound to (ADR-0343).</summary>
    public IReadOnlyDictionary<string, BoardSource> Sources { get; }

    /// <summary>The views, in the Board's order.</summary>
    public IReadOnlyList<BoundView> Views { get; }

    /// <summary>The view whose id is <paramref name="id"/>.</summary>
    public BoundView this[string id] => Views.First(v => v.View.Id == id);

    /// <summary>The condition <paramref name="link"/> narrows by now, or null while its view selects nothing.</summary>
    public LinkCondition? ConditionOf(BoardLink link) => _conditions.GetValueOrDefault(link);

    /// <summary>
    /// The condition <paramref name="selection"/>, made in <paramref name="link"/>'s grid, narrows by:
    /// the values in the link's columns of every row the selection covers, in any column, read from
    /// the rows the grid shows; null when it selects nothing, which narrows nothing (ADR-0011), and
    /// when it reaches rows the grid does not hold, which a grid over a source held on a server has not
    /// fetched (ADR-0343).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="link"/> is not one of the Board's, or starts at a pivot.</exception>
    public LinkCondition? ConditionOf(BoardLink link, GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return LinkFrom(link) is BoundGridView grid
            ? Condition(grid, link.On, selection).Condition
            : throw new ArgumentException($"The link starts at pivot '{link.From}', whose selection narrows by the Items its cells stand for.", nameof(link));
    }

    /// <summary>
    /// The condition <paramref name="cells"/>, selected in <paramref name="link"/>'s pivot, narrows by
    /// (ADR-0340, Q5): the values in the link's columns of every row of the pivot's data under any of
    /// the cells, read from the rows the pivot shows. A cell is over the rows whose values are the Items
    /// its paths name, so a cell on a subtotal stands for every Item below it and a grand total for
    /// every row; an Item the pivot hides is under none. Null when no cell is selected, which narrows
    /// nothing (ADR-0011).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="link"/> is not one of the Board's, or starts at a grid.</exception>
    public LinkCondition? ConditionOf(BoardLink link, IReadOnlyList<PivotCellItems> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return LinkFrom(link) is BoundPivotView pivot
            ? CellCondition(pivot, link.On, cells)
            : throw new ArgumentException($"The link starts at grid '{link.From}', whose selection narrows by its rows.", nameof(link));
    }

    private static LinkCondition? CellCondition(BoundPivotView pivot, IReadOnlyList<string> on, IReadOnlyList<PivotCellItems> cells) =>
        cells.Count == 0 ? null : BoardSource.ValuesUnder(pivot.Source!.Snapshot, new BoardCellsQuery(pivot.View.Id, pivot.Layout, cells, on, []));

    internal sealed class ItemsComparer : IEqualityComparer<PivotItemKey[]>
    {
        public static readonly ItemsComparer Instance = new();

        public bool Equals(PivotItemKey[]? x, PivotItemKey[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(PivotItemKey[] items)
        {
            var hash = new HashCode();
            foreach (var item in items)
                hash.Add(item);
            return hash.ToHashCode();
        }
    }

    // The condition the selection makes on the rows the grid holds, and the data its rows were read
    // from: the grid's Snapshot, or, over a fetched source, the page they came back in, null when they
    // came back in pages of different versions. Null throughout when the selection reaches past the rows
    // held (Unheld).
    private static (LinkCondition? Condition, Snapshot? From) Condition(BoundGridView grid, IReadOnlyList<string> on, GridSelection selection)
    {
        if (selection.Ranges.Count == 0)
            return (null, null);
        var source = grid.Source;
        var shown = source.Window;
        var start = source.WindowStart;
        var taken = new HashSet<int>();
        var tuples = new List<object?[]>();
        Snapshot? page = null;
        SnapshotColumn[] columns = [];
        var versions = new HashSet<long>();
        foreach (var range in selection.Ranges)
        {
            // A selection taken against other rows than these is dropped, never read against them.
            if (Unheld(grid, range))
                return (null, null);
            for (var r = range.TopRow; r <= range.BottomRow; r++)
            {
                if (!taken.Add(r))
                    continue;
                var row = shown[r - start];
                if (!ReferenceEquals(row.Page, page))
                {
                    page = row.Page;
                    columns = [.. on.Select(n => page[n])];
                    versions.Add(page.Version);
                }
                tuples.Add([.. columns.Select(c => row.Page.ValueAt(row.At, c))]);
            }
        }
        return (new LinkCondition(on, tuples), versions.Count == 1 ? page : null);
    }

    private static bool Unheld(BoundGridView grid, SelectionRange range) =>
        range.TopRow < grid.Source.WindowStart || range.BottomRow >= grid.Source.WindowStart + grid.Source.Window.Count;

    /// <summary>
    /// Narrows every view <paramref name="link"/> reaches by <paramref name="condition"/>, or, given
    /// null, takes the link's narrowing away (ADR-0340, Q5). A view narrowed by several links shows
    /// the rows all of them let through. A grid keeps its rows' sort and filter, and the user's; a
    /// pivot is given a source over the rows let through, and a chart its data read again from them; a
    /// chart its spec cannot draw from them says why in <see cref="BoundChartView.Refusal"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="link"/> is not one of the Board's, or
    /// <paramref name="condition"/> compares other columns than it does.</exception>
    public void Narrow(BoardLink link, LinkCondition? condition)
    {
        if (Set(link, condition))
        {
            foreach (var target in link.To)
                NarrowView(target);
        }
    }

    /// <summary>
    /// Narrows by <paramref name="selection"/>, made in grid view <paramref name="from"/>: each link
    /// from it narrows by the condition the selection makes (<see cref="ConditionOf(BoardLink, GridSelection)"/>),
    /// and each view they reach is narrowed once. A selection that moves within the same values, an
    /// arrow key inside the selected rows, narrows nothing again.
    /// </summary>
    /// <returns>Whether any view was narrowed again.</returns>
    public bool Select(string from, GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(selection);
        if (this[from] is BoundGridView grid)
        {
            // Over a source held on a server, a selection reaching rows not fetched is said, not read (ADR-0343).
            grid.Unread = grid.Fetched is not null && selection.Ranges.Any(r => Unheld(grid, r))
                ? "The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on."
                : null;
            foreach (var output in Board.Outputs.Where(o => o.From == from))
            {
                var (condition, read) = Condition(grid, output.On, selection);
                if (condition is not null && read is null)
                    grid.Unread = "The selected rows were read at different versions of the source, so nothing is handed on.";
                if (condition is null || read is null)
                    _taken.Remove(output.Name);
                else
                    _taken[output.Name] = (condition, read);
            }
        }
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in Board.Links.Where(l => l.From == from))
        {
            if (Set(link, ConditionOf(link, selection)))
                targets.UnionWith(link.To);
        }
        foreach (var target in targets)
            NarrowView(target);
        return targets.Count > 0;
    }

    /// <summary>
    /// Narrows by <paramref name="cells"/>, selected in pivot view <paramref name="from"/>, as its
    /// <c>ItemsOf</c> reads them: each link from it narrows by the condition the cells make
    /// (<see cref="ConditionOf(BoardLink, IReadOnlyList{PivotCellItems})"/>), and each view they reach is
    /// narrowed once. Null, a selection the pivot could not read, narrows nothing, as no selection does.
    /// </summary>
    /// <returns>Whether any view was narrowed again.</returns>
    /// <exception cref="InvalidOperationException">The pivot is over a source held on a server, whose rows
    /// are asked for there (<see cref="SelectCellsAsync"/>).</exception>
    public bool SelectCells(string from, IReadOnlyList<PivotCellItems>? cells)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (this[from] is not BoundPivotView pivot)
            throw new ArgumentException($"View '{from}' is not a pivot.", nameof(from));
        if (pivot.ReportSource is not null)
            throw new InvalidOperationException($"Pivot '{from}' is over a source held on a server, which is asked for the values under its cells: select them with SelectCellsAsync.");
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in Board.Links.Where(l => l.From == from))
        {
            if (Set(link, cells is null ? null : CellCondition(pivot, link.On, cells)))
                targets.UnionWith(link.To);
        }
        foreach (var target in targets)
            NarrowView(target);
        return targets.Count > 0;
    }

    /// <summary>
    /// <see cref="SelectCells"/> for any pivot: over a source held on a server, each link from it asks the
    /// source for the values under the cells (ADR-0345), and the views it reaches are narrowed once every
    /// answer has come. A selection a later one has replaced narrows nothing when its answers come (spine
    /// rule 6), and answers false.
    /// </summary>
    /// <returns>Whether any view was narrowed again.</returns>
    /// <exception cref="Exception">The source failed to answer the newest selection; its links let go, so nothing is narrowed by it.</exception>
    public async Task<bool> SelectCellsAsync(string from, IReadOnlyList<PivotCellItems>? cells, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (this[from] is not BoundPivotView pivot)
            throw new ArgumentException($"View '{from}' is not a pivot.", nameof(from));
        if (pivot.ReportSource is null)
            return SelectCells(from, cells);
        var selected = ++pivot.Selected;
        var links = Board.Links.Where(l => l.From == from).ToArray();
        var conditions = new LinkCondition?[links.Length];
        if (cells is { Count: > 0 })
        {
            var narrowing = _conditions.Where(c => c.Key.To.Contains(from)).Select(c => c.Value).ToArray();
            var under = Sources[pivot.Input].Under!;
            // Asked together, and resumed where the Board was called (no ConfigureAwait(false)): what follows
            // narrows views, whose renderer is the caller's.
            var asks = links.Select(l => under(new BoardCellsQuery(from, pivot.Layout, cells, l.On, narrowing), cancellationToken).AsTask()).ToArray();
            try
            {
                var answered = await Task.WhenAll(asks);
                answered.CopyTo(conditions, 0);
            }
            catch when (selected == pivot.Selected)
            {
                // The newest selection was not answered: its links let go, so no view stays narrowed by the
                // selection before it while the pivot shows this one (spine rule 1).
                var released = new HashSet<string>(StringComparer.Ordinal);
                foreach (var link in links)
                {
                    if (Set(link, null))
                        released.UnionWith(link.To);
                }
                foreach (var target in released)
                    NarrowView(target);
                throw;
            }
            catch
            {
                // A selection a later one replaced: its failure says nothing about what is shown (spine rule 6).
                return false;
            }
        }
        if (selected != pivot.Selected)
            return false;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < links.Length; i++)
        {
            if (Set(links[i], conditions[i]))
                targets.UnionWith(links[i].To);
        }
        foreach (var target in targets)
            NarrowView(target);
        return targets.Count > 0;
    }

    /// <summary>Whether output <paramref name="name"/> can be confirmed: its grid selects something.</summary>
    public bool CanConfirm(string name) => _taken.ContainsKey(name);

    /// <summary>
    /// What confirming output <paramref name="name"/> hands on (ADR-0340, Q6): the values its grid's
    /// selection holds in its columns, on its input, with the version of the input's data they were
    /// taken against; null while its grid selects nothing. Nothing is raised by a selection on the
    /// way: only this, called when the user confirms.
    /// </summary>
    /// <exception cref="ArgumentException">The Board declares no output <paramref name="name"/>.</exception>
    public BoardOutput? Confirm(string name)
    {
        var declared = Board.Outputs.FirstOrDefault(o => o.Name == name)
            ?? throw new ArgumentException($"The Board declares no output '{name}'.", nameof(name));
        if (!_taken.TryGetValue(name, out var taken))
            return null;
        var input = ((BoardGridView)Board.View(declared.From)!).Grid.Data;
        var shape = Board.Input(input)!.Shape;
        return new BoardOutput(name, input, taken.From, taken.Condition, [.. declared.On.Select(c => shape.Columns.First(k => k.Name == c).Kind)]);
    }

    // Records the link's condition; false when it is the one already held, which narrows nothing again.
    private bool Set(BoardLink link, LinkCondition? condition)
    {
        LinkFrom(link);
        if (condition is not null && !condition.On.SequenceEqual(link.On, StringComparer.Ordinal))
            throw new ArgumentException($"The link compares {string.Join(", ", link.On)}, and the condition {string.Join(", ", condition.On)}.", nameof(condition));
        var held = _conditions.GetValueOrDefault(link);
        if (held is null ? condition is null : held.LetsThroughAs(condition))
            return false;
        if (condition is null)
            _conditions.Remove(link);
        else
            _conditions[link] = condition;
        return true;
    }

    private BoundView LinkFrom(BoardLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!Board.Links.Contains(link))
            throw new ArgumentException("The link is not one of the Board's.", nameof(link));
        return this[link.From];
    }

    private void NarrowView(string id)
    {
        var conditions = _conditions.Where(c => c.Key.To.Contains(id)).Select(c => c.Value).ToArray();
        switch (this[id])
        {
            case BoundGridView { Grid: { } held } grid:
                var rows = held.Rows;
                held.Source.ReplaceAll(conditions.Length == 0 ? rows : [.. Narrowing.Positions(held.Snapshot, conditions).Select(p => rows[p])]);
                LetGo(grid, id);
                break;
            case BoundGridView grid:
                // Held on a server: the source is asked again, handed the conditions to execute (ADR-0343).
                grid.Narrowing = conditions;
                grid.Fetched!.Source.Requery();
                LetGo(grid, id);
                break;
            case BoundPivotView { ReportSource: not null } pivot:
                pivot.ReportSource = Sources[pivot.Input].Report!(conditions);
                // Its report changed, so its selection is dropped, with an answer still on its way for it,
                // and what it narrowed is let go now (ADR-0345).
                pivot.Selected++;
                foreach (var link in Board.Links.Where(l => l.From == id))
                    Narrow(link, null);
                break;
            case BoundPivotView pivot:
                var data = Data[pivot.Input];
                pivot.Source = conditions.Length == 0 ? pivot.Unnarrowed : PivotSource.From(Narrowing.Copy(data, Narrowing.Positions(data, conditions)));
                // Its report changed, so its selection is dropped, and what it narrowed is let go now.
                foreach (var link in Board.Links.Where(l => l.From == id))
                    Narrow(link, null);
                break;
            case BoundChartView { Thinned: true } chart:
                // Asked again, handed the conditions, as a fetched grid is; drawn when the answer comes.
                _ = AskAsync(chart, default);
                break;
            case BoundChartView chart:
                var path = $"views[{Board.Views.ToList().FindIndex(v => v.Id == id)}].chart";
                try
                {
                    chart.Reading = ReadChart((BoardChartView)chart.View, Data, Board.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal), path,
                        conditions.Length == 0 ? null : name => Narrowing.Positions(Data[name], conditions));
                    chart.Refusal = null;
                }
                catch (BoardRefusedException refused)
                {
                    chart.Refusal = $"{refused.Path}: {refused.Message}";
                }
                break;
        }
    }

    // A grid's rows changed, so its selection is dropped (ADR-0011): what the selection held is let go
    // now, not when the grid next raises it, so nothing is confirmed or narrowed by a selection the grid
    // no longer shows.
    private void LetGo(BoundGridView grid, string id)
    {
        grid.Unread = null;
        foreach (var output in Board.Outputs.Where(o => o.From == id))
            _taken.Remove(output.Name);
        foreach (var link in Board.Links.Where(l => l.From == id))
            Narrow(link, null);
    }

    /// <summary>
    /// Binds each of <paramref name="board"/>'s inputs to the Snapshot <paramref name="data"/> holds
    /// under its name, and makes each view. Nothing is drawn unless every input binds and every view
    /// can be made: an input with no data, a Snapshot whose shape is not the declaration, and a chart
    /// its spec cannot draw from the data are refused, naming the input or the path.
    /// </summary>
    /// <param name="board">The Board.</param>
    /// <param name="data">The Snapshot for each input, by the input's name. Data for a name the Board
    /// does not declare is not read.</param>
    /// <param name="culture">The culture a grid's text is written in; the invariant culture by default.</param>
    /// <param name="diff">What the user changed last time, laid over the Board (ADR-0340, Q7): what
    /// still fits applies, and what does not is dropped and named in <see cref="Dropped"/>.</param>
    /// <exception cref="BoardRefusedException">An input or a view is refused.</exception>
    public static BoundBoard Bind(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, CultureInfo? culture = null, BoardDiff? diff = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(data);
        var held = new Dictionary<string, BoardSource>(StringComparer.Ordinal);
        return Make(board, BindData(board, data, held), held, culture, diff);
    }

    /// <summary>
    /// Binds each of <paramref name="board"/>'s inputs to the Snapshot <paramref name="data"/> holds
    /// under its name, or to the source held where the data lives that <paramref name="sources"/> holds
    /// (ADR-0343), and makes each view. Each source is asked its shape, and checked against the input's
    /// declaration, before any view is made or any row is fetched (ADR-0341). Nothing is drawn unless
    /// every input binds and every view can be made. Over a source, a grid's rows are fetched a page at a
    /// time, each fetch handed the narrowing the Board's links put on it; a pivot's report source is made
    /// by the source, given that narrowing; a chart is read through the source's <see cref="BoardSource.Thin"/>
    /// (ADR-0344); and a link from a pivot asks its <see cref="BoardSource.Under"/> (ADR-0345). A chart or
    /// such a link over a source that answers no such question is refused by path.
    /// </summary>
    /// <param name="board">The Board.</param>
    /// <param name="data">The Snapshot for each input held in memory, by the input's name.</param>
    /// <param name="sources">The source for each input held where the data lives, by the input's name. An
    /// input named in both is refused.</param>
    /// <param name="culture">The culture a grid's text is written in; the invariant culture by default.</param>
    /// <param name="diff">What the user changed last time, laid over the Board (ADR-0340, Q7).</param>
    /// <param name="cancellationToken">Cancels the questions to the sources.</param>
    /// <exception cref="BoardRefusedException">An input or a view is refused.</exception>
    public static async Task<BoundBoard> BindAsync(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, IReadOnlyDictionary<string, BoardSource> sources,
        CultureInfo? culture = null, BoardDiff? diff = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sources);
        var held = new Dictionary<string, BoardSource>(StringComparer.Ordinal);
        foreach (var input in board.Inputs)
        {
            if (!sources.TryGetValue(input.Name, out var source) || source is null)
                continue;
            if (data.ContainsKey(input.Name))
                throw new BoardRefusedException($"inputs.{input.Name}", $"Input '{input.Name}' is bound both to a Snapshot and to a source held on a server.");
            try
            {
                await input.CheckAsync(source, cancellationToken).ConfigureAwait(false);
            }
            catch (BoardRefusedException refused)
            {
                throw new BoardRefusedException($"inputs.{input.Name}", refused.Message);
            }
            held[input.Name] = source;
        }
        var made = Make(board, BindData(board, data, held), held, culture, diff);
        // A chart over a server input is read from its sources' first answers before the Board is
        // drawn, and a spec they cannot draw is refused by path, as one over a Snapshot is.
        for (var i = 0; i < board.Views.Count; i++)
        {
            if (made.Views[i] is BoundChartView { Thinned: true } chart)
            {
                try
                {
                    var read = await made.ReadThroughSourcesAsync(chart, [], cancellationToken).ConfigureAwait(false);
                    chart.Reading = read.Reading;
                    chart.Versions = read.Versions;
                }
                catch (ChartSpecRefusedException e)
                {
                    throw new BoardRefusedException(e.Path.Length == 0 ? $"views[{i}].chart" : $"views[{i}].chart.{e.Path}", e.Message);
                }
            }
        }
        return made;
    }

    /// <summary>
    /// How many pixels a chart over a server input asks its source for (ADR-0344): the device pixels of a
    /// plot 1,024 CSS pixels wide, at the two device pixels a CSS pixel ExChart counts in. A wider plot
    /// thins on top in the browser, which keeps every extreme the source kept.
    /// </summary>
    public const int ChartPixels = 2048;

    // A chart read through Chart Sources: a server input through its BoardSource, an input held in
    // memory through the reference implementation over its declared columns, each under the narrowing.
    private Task<ChartSourceReading> ReadThroughSourcesAsync(BoundChartView chart, IReadOnlyList<LinkCondition> conditions, CancellationToken cancellationToken)
    {
        var inputs = Board.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal);
        var view = (BoardChartView)chart.View;
        return ChartSpec.ReadAsync(view.Spec, name =>
        {
            if (!inputs.TryGetValue(name, out var input))
                return null;
            if (Sources.TryGetValue(name, out var source))
                return new SourceThinning(source, view.Id, conditions);
            var snapshot = Data[name];
            return ChartSource.Over(new DeclaredChartData(new SnapshotChartData(snapshot), input,
                conditions.Count == 0 ? null : Narrowing.Positions(snapshot, conditions)), snapshot.Version);
        }, ChartPixels, chart.Range?.From, chart.Range?.To, cancellationToken);
    }

    // Asks a chart over a server input again, under the narrowing and the range it has now. Only the
    // newest ask is drawn, and never an answer older than the version the chart shows (ADR-0322).
    internal async Task AskAsync(BoundChartView chart, CancellationToken cancellationToken)
    {
        var asked = ++chart.Asked;
        var conditions = _conditions.Where(c => c.Key.To.Contains(chart.View.Id)).Select(c => c.Value).ToArray();
        string? refusal = null;
        ChartSourceReading? read = null;
        try
        {
            // Resumed where the ask was made (no ConfigureAwait(false)): the chart's reading is the renderer's.
            read = await ReadThroughSourcesAsync(chart, conditions, cancellationToken);
        }
        catch (ChartSpecRefusedException e)
        {
            var path = $"views[{Board.Views.ToList().FindIndex(v => v.Id == chart.View.Id)}].chart";
            refusal = $"{(e.Path.Length == 0 ? path : $"{path}.{e.Path}")}: {e.Message}";
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            refusal = $"The source did not answer the chart: {e.Message}";
        }
        if (asked != chart.Asked)
            return;
        if (read is not null && read.Versions.FirstOrDefault(v => chart.Versions.TryGetValue(v.Key, out var shown) && v.Value < shown) is { Key: not null } older)
        {
            // Never drawn over the newer data shown, and not left drawing what was asked before as if it were
            // the answer: the chart says so in its place (spine rule 1).
            chart.Refusal = $"The source answered the chart from version {older.Value} of '{older.Key}', older than version {chart.Versions[older.Key]} the chart was drawn from, so it is not drawn.";
        }
        else if (read is not null)
        {
            chart.Reading = read.Reading;
            chart.Versions = read.Versions;
            chart.Refusal = null;
        }
        else
        {
            chart.Refusal = refusal;
        }
        chart.RaiseAnswered();
    }

    private sealed class SourceThinning(BoardSource source, string view, IReadOnlyList<LinkCondition> conditions) : IChartSource
    {
        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default) =>
            source.Thin!(new BoardChartQuery(view, query, conditions), cancellationToken);
    }

    private static Dictionary<string, Snapshot> BindData(BoardReading board, IReadOnlyDictionary<string, Snapshot> data, Dictionary<string, BoardSource> held)
    {
        var bound = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        foreach (var input in board.Inputs.Where(i => !held.ContainsKey(i.Name)))
        {
            if (!data.TryGetValue(input.Name, out var snapshot) || snapshot is null)
                throw new BoardRefusedException($"inputs.{input.Name}", $"Input '{input.Name}' is bound to no data.");
            bound[input.Name] = input.Bind(snapshot);
        }
        return bound;
    }

    private static BoundBoard Make(BoardReading board, Dictionary<string, Snapshot> bound, Dictionary<string, BoardSource> held, CultureInfo? culture, BoardDiff? diff)
    {
        var inputs = board.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal);
        var views = new List<BoundView>();
        for (var i = 0; i < board.Views.Count; i++)
        {
            var path = $"views[{i}]";
            views.Add(board.Views[i] switch
            {
                BoardGridView grid when held.TryGetValue(grid.Grid.Data, out var source) => FetchedGrid(grid, source, culture),
                BoardGridView grid => new BoundGridView(grid, GridOf(bound[grid.Grid.Data], grid.Grid, culture)),
                BoardPivotView pivot when held.TryGetValue(pivot.Input, out var source) => source.Report is { } report
                    ? new BoundPivotView(pivot, report([]), inputs[pivot.Input].Shape)
                    : throw new BoardRefusedException($"{path}.pivot", $"Input '{pivot.Input}' is held on a server, and its source makes no report source for a pivot."),
                BoardPivotView pivot => new BoundPivotView(pivot, PivotSource.From(bound[pivot.Input]), inputs[pivot.Input].Shape),
                BoardChartView chart when chart.Data.FirstOrDefault(d => held.TryGetValue(d, out var s) && s.Thin is null) is { } unthinned => throw new BoardRefusedException($"{path}.chart",
                    $"Input '{unthinned}' is held on a server, and its source thins no chart's points (BoardSource.Thin)."),
                // Read through Chart Sources, the server's thinning where the data lives, once bound (ADR-0344).
                BoardChartView chart when chart.Data.Any(held.ContainsKey) => new BoundChartView(chart, null!) { Thinned = true },
                BoardChartView chart => new BoundChartView(chart, ReadChart(chart, bound, inputs, $"{path}.chart", null)),
                var other => throw new InvalidOperationException($"Unknown view {other.GetType().Name}."),
            });
        }
        for (var l = 0; l < board.Links.Count; l++)
        {
            if (board.View(board.Links[l].From) is BoardPivotView pivot && held.TryGetValue(pivot.Input, out var source) && source.Under is null)
            {
                throw new BoardRefusedException($"links[{l}].from",
                    $"Pivot '{pivot.Id}' is over input '{pivot.Input}', held on a server, where the rows under a cell are, and its source answers no values under cells (BoardSource.Under).");
            }
        }
        var fitted = (diff ?? BoardDiff.Empty).FitTo(board);
        foreach (var view in views)
        {
            switch (view)
            {
                case BoundGridView grid when fitted.Diff.Grids.TryGetValue(grid.View.Id, out var change):
                    grid.LayOver(change);
                    break;
                case BoundPivotView pivot when fitted.Diff.Pivots.TryGetValue(pivot.View.Id, out var layout):
                    pivot.ChangeLayout(layout.Layout);
                    break;
                case BoundChartView chart when fitted.Diff.Charts.TryGetValue(chart.View.Id, out var change):
                    chart.LayOver(change);
                    break;
            }
        }
        var made = new BoundBoard(board, bound, held, views, fitted.Dropped);
        foreach (var chart in views.OfType<BoundChartView>().Where(c => c.Thinned))
            chart.Ask = () => made.AskAsync(chart, default);
        return made;
    }

    // Each fetch hands the source the grid's query and the narrowing the view holds when it is asked.
    private static BoundGridView FetchedGrid(BoardGridView view, BoardSource source, CultureInfo? culture)
    {
        BoundGridView? made = null;
        var grid = ShapedGrid.Fetch(view.Grid, (query, cancellation) => source.Fetch(new BoardQuery(view.Id, query, made?.Narrowing ?? []), cancellation), culture);
        made = new BoundGridView(view, grid);
        return made;
    }

    private static ShapedGrid GridOf(Snapshot snapshot, GridViewSpecReading spec, CultureInfo? culture)
    {
        var grid = ShapedGrid.Of(snapshot, spec, culture);
        // Its rows change only when a link narrows them, a step the user took, which reaches the grid
        // at once rather than at the end of a live feed's gathering interval.
        grid.Source.GatherInterval = TimeSpan.Zero;
        return grid;
    }

    private static ChartSpecReading ReadChart(BoardChartView chart, IReadOnlyDictionary<string, Snapshot> bound, Dictionary<string, BoardInput> inputs, string path,
        Func<string, List<int>>? narrowed)
    {
        try
        {
            // A chart reads only what its inputs declare: a column the source has beside them is
            // not there for it, as it is not for a grid or a pivot (ADR-0341).
            return ChartSpec.Read(chart.Spec, name => bound.TryGetValue(name, out var snapshot) && inputs.TryGetValue(name, out var input)
                ? new DeclaredChartData(new SnapshotChartData(snapshot), input, narrowed?.Invoke(name))
                : null);
        }
        catch (ChartSpecRefusedException e)
        {
            throw new BoardRefusedException(e.Path.Length == 0 ? path : $"{path}.{e.Path}", e.Message);
        }
    }
}

/// <summary>A view of a bound Board.</summary>
public abstract class BoundView
{
    private protected BoundView(BoardView view) => View = view;

    /// <summary>The view as the Board writes it.</summary>
    public BoardView View { get; }
}

/// <summary>
/// A grid view, its columns built from its input's shape (ADR-0342), and shown as the user has
/// changed them since: the columns' order, widths, pinning and those hidden, here, and the sort and the
/// filter in its source. The Board's own spec is never rewritten (ADR-0340, Q7); what differs from it
/// is <see cref="Change"/>.
/// </summary>
public sealed class BoundGridView : BoundView
{
    private readonly Dictionary<string, GridColumn<ShapedRow>> _declared;
    private List<ColumnViewState> _shown;
    private SortedSet<string> _hidden = new(StringComparer.Ordinal);

    internal BoundGridView(BoardGridView view, ShapedGrid grid)
        : this(view, grid.Spec, grid.Columns, grid.Source) => Grid = grid;

    internal BoundGridView(BoardGridView view, FetchedShapedGrid grid)
        : this(view, grid.Spec, grid.Columns, grid.Source) => Fetched = grid;

    private BoundGridView(BoardGridView view, GridViewSpecReading spec, IReadOnlyList<GridColumn<ShapedRow>> columns, IGridSource<ShapedRow> source)
        : base(view)
    {
        Spec = spec;
        Source = source;
        _declared = columns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        _shown = [.. spec.ViewState.Columns];
        Columns = columns;
    }

    /// <summary>The grid over its input's Snapshot, as the Board's spec builds it; null when its input is
    /// held where the data lives, and its rows are fetched (<see cref="Fetched"/>).</summary>
    public ShapedGrid? Grid { get; }

    /// <summary>The grid whose rows are fetched from its input's source (ADR-0343); null when its input
    /// is held in memory (<see cref="Grid"/>).</summary>
    public FetchedShapedGrid? Fetched { get; }

    /// <summary>The grid's source, in memory or fetching.</summary>
    public IGridSource<ShapedRow> Source { get; }

    /// <summary>The grid view's spec, as the Board writes it.</summary>
    public GridViewSpecReading Spec { get; }

    /// <summary>Why the grid's selection narrows nothing and hands nothing on although it selects rows, a
    /// sentence the Board shows under the grid; null otherwise (ADR-0343).</summary>
    public string? Unread { get; internal set; }

    // The links' conditions narrowing a fetched grid now, handed to its source with each fetch.
    internal IReadOnlyList<LinkCondition> Narrowing { get; set; } = [];

    /// <summary>The columns shown, in the user's order at the user's widths: a new list each time
    /// they change, which is what tells the grid (ADR-0003).</summary>
    public IReadOnlyList<GridColumn<ShapedRow>> Columns { get; private set; }

    /// <summary>How many leading columns are pinned.</summary>
    public int PinnedColumnCount => _shown.Count(c => c.Pinned);

    /// <summary>The columns the user hid, in the order of their names.</summary>
    public IReadOnlyList<string> Hidden => [.. _hidden];

    /// <summary>The View State the grid is shown under now (ADR-0207).</summary>
    public GridViewState ViewState => new(_shown, Source.Sorts, Source.Filter);

    /// <summary>What differs from the Board's spec, or null where nothing does.</summary>
    public BoardGridChange? Change
    {
        get
        {
            var state = ViewState;
            if (state == Spec.ViewState && _hidden.Count == 0)
                return null;
            var kinds = Spec.Columns.ToDictionary(c => c.Column, c => c.Kind, StringComparer.Ordinal);
            return new BoardGridChange(state, _hidden,
                state.Filter?.Columns.Keys.Where(kinds.ContainsKey).ToDictionary(c => c, c => kinds[c], StringComparer.Ordinal));
        }
    }

    // A gesture is taken against the columns the grid was shown: one naming a column no longer shown,
    // a round trip behind a hide, is refused rather than applied to others (spine rule 6), and each says
    // whether it was taken.

    /// <summary>A resize drag released (ADR-0016): the column takes the width as Fixed.</summary>
    /// <returns>Whether it was taken: false for a column not shown, or a width that is not a number.</returns>
    public bool Resize(ColumnWidthChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var at = Shown(change.Column);
        if (at < 0 || !double.IsFinite(change.WidthPx) || change.WidthPx <= 0)
            return false;
        _shown[at] = new ColumnViewState(change.Column, ColumnWidth.Fixed(change.WidthPx), _shown[at].Pinned);
        Rebuild();
        return true;
    }

    /// <summary>A header drag dropped (ADR-0011/0032): the shown columns take <paramref name="order"/>,
    /// the pinned ones still leading; a shown column it leaves out keeps its place after them.</summary>
    /// <returns>Whether it was taken: false for an order naming a column not shown, or one twice.</returns>
    public bool Reorder(IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Distinct(StringComparer.Ordinal).Count() != order.Count || order.Any(name => Shown(name) < 0))
            return false;
        var pinned = PinnedColumnCount;
        var moved = order.Select(name => _shown[Shown(name)]).ToList();
        moved.AddRange(_shown.Where(c => !order.Contains(c.Column, StringComparer.Ordinal)));
        _shown = [.. moved.Select((c, i) => new ColumnViewState(c.Column, c.Width, i < pinned))];
        Rebuild();
        return true;
    }

    /// <summary>The column menu's "hide this column" (ADR-0010). The last column shown is kept: a grid
    /// of none could not be shown a column again.</summary>
    /// <returns>Whether the column was hidden: false for a column not shown, or the last one.</returns>
    public bool Hide(string column)
    {
        var at = Shown(column);
        if (at < 0 || _shown.Count == 1)
            return false;
        _shown.RemoveAt(at);
        _hidden.Add(column);
        Rebuild();
        return true;
    }

    /// <summary>The column menu's "pin up to this column" (ADR-0004/0010): the leading
    /// <paramref name="count"/> columns are pinned.</summary>
    /// <returns>Whether it was taken: false for a count outside [0, the columns shown].</returns>
    public bool Pin(int count)
    {
        if (count < 0 || count > _shown.Count)
            return false;
        _shown = [.. _shown.Select((c, i) => new ColumnViewState(c.Column, c.Width, i < count))];
        return true;
    }

    // A part of a Diff already fitted to this view (BoardDiff.FitTo).
    internal void LayOver(BoardGridChange change)
    {
        _shown = [.. change.ViewState.Columns];
        _hidden = new SortedSet<string>(change.Hidden, StringComparer.Ordinal);
        Rebuild();
        Source.OnSortChanged(change.ViewState.Sorts);
        Source.OnFilterChanged(change.ViewState.Filter);
    }

    private int Shown(string column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return _shown.FindIndex(c => c.Column == column);
    }

    private void Rebuild() => Columns = [.. _shown.Select(c => Sized(_declared[c.Column], c.Width))];

    private static GridColumn<ShapedRow> Sized(GridColumn<ShapedRow> column, ColumnWidth width) => width == column.Width.Width
        ? column
        : new(column.Name, column.Type, column.Value, column.Header,
            new ColumnWidthSpec(width, column.Width.MinWidthPx, column.Width.MaxWidthPx),
            align: column.Align, format: column.Format,
            dateType: column.Type == ColumnType.Date ? column.DateType : null);
}

/// <summary>A pivot view over its input's Snapshot, or over a report source its input's source held
/// where the data lives makes (ADR-0343).</summary>
public sealed class BoundPivotView : BoundView
{
    internal BoundPivotView(BoardPivotView view, SnapshotPivotSource source, DataShape shape)
        : this(view, shape) => Source = Unnarrowed = source;

    internal BoundPivotView(BoardPivotView view, PivotReportSource source, DataShape shape)
        : this(view, shape) => ReportSource = source;

    private BoundPivotView(BoardPivotView view, DataShape shape)
        : base(view)
    {
        Input = view.Input;
        Layout = view.Layout;
        _shape = shape;
    }

    private readonly DataShape _shape;

    /// <summary>The user changed the layout from the field list or a header (ADR-0059).</summary>
    public void ChangeLayout(PivotLayout layout) => Layout = layout ?? throw new ArgumentNullException(nameof(layout));

    /// <summary>The layout, or null where it is the Board's.</summary>
    public BoardPivotChange? Change => PivotLayoutJson.Write(Layout) == PivotLayoutJson.Write(((BoardPivotView)View).Layout)
        ? null
        : new BoardPivotChange(Layout, BoardDiff.KindsOf(_shape, BoardDiff.Placed(Layout)));

    /// <summary>The pivot's source: over its input's rows, or, while a link narrows it, over the rows
    /// let through; null when its input is held where the data lives (<see cref="ReportSource"/>).</summary>
    public SnapshotPivotSource? Source { get; internal set; }

    /// <summary>The report source its input's source made, handed the links' narrowing, when its input is
    /// held where the data lives; null when it is held in memory (<see cref="Source"/>).</summary>
    public PivotReportSource? ReportSource { get; internal set; }

    internal SnapshotPivotSource? Unnarrowed { get; }

    // The selections made in a pivot over a server, so only the newest one's answers narrow (ADR-0345).
    internal int Selected { get; set; }

    internal string Input { get; }

    /// <summary>The layout shown: the Board's, until the user changes it.</summary>
    public PivotLayout Layout { get; private set; }
}

/// <summary>A chart view, its Chart Spec read from its inputs' data.</summary>
public sealed class BoundChartView : BoundView
{
    internal BoundChartView(BoardChartView view, ChartSpecReading reading)
        : base(view) => Reading = reading;

    /// <summary>The chart's title and definition, read from the rows its links let through.</summary>
    public ChartSpecReading Reading { get; internal set; }

    /// <summary>The keys of the series the user hid: a new set each time it changes, which is what
    /// tells the chart (ADR-0272).</summary>
    public IReadOnlySet<string> HiddenSeries { get; private set; } = new SortedSet<string>(StringComparer.Ordinal);

    /// <summary>The range the user narrowed the chart to, or null for the whole.</summary>
    public ChartRange? Range { get; private set; }

    /// <summary>The axis the user switched to, or null for the declared one.</summary>
    public AxisScale? Scale { get; private set; }

    /// <summary>What the user changed, or null where nothing is.</summary>
    public BoardChartChange? Change => HiddenSeries.Count == 0 && Range is null && Scale is null ? null : new BoardChartChange(HiddenSeries, Range, Scale);

    /// <summary>A series hidden or shown from the legend (ADR-0271).</summary>
    public void ChangeVisibility(SeriesVisibility change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var hidden = new SortedSet<string>(HiddenSeries, StringComparer.Ordinal);
        if (change.Hidden ? hidden.Add(change.Key) : hidden.Remove(change.Key))
            HiddenSeries = hidden;
    }

    /// <summary>The range narrowed, or returned to the whole with null (ADR-0273). A chart over a server
    /// input asks its sources again for the range, drawn at full detail (ADR-0344).</summary>
    public void ChangeRange(ChartRange? range)
    {
        var moved = range?.From != Range?.From || range?.To != Range?.To;
        Range = range;
        if (moved && Ask is { } ask)
            _ = ask();
    }

    /// <summary>
    /// Whether the chart is read through Chart Sources (ADR-0344): an input of it is held on a server,
    /// thinned there, so it holds only the points drawn, and is asked again as it is narrowed.
    /// </summary>
    public bool Thinned { get; internal init; }

    /// <summary>Raised when an answer to the chart's sources has been taken, or has said why it is not drawn.</summary>
    public event Action? Answered;

    internal void RaiseAnswered() => Answered?.Invoke();

    // How it asks again, set by the Board that bound it, and the asks made so far.
    internal Func<Task>? Ask { get; set; }

    internal int Asked { get; set; }

    // The version of each input the reading drawn was read at.
    internal IReadOnlyDictionary<string, long> Versions { get; set; } = new Dictionary<string, long>();

    /// <summary>The axis switched under Chart Filters (ADR-0251).</summary>
    public void ChangeScale(AxisScale scale) => Scale = scale;

    internal void LayOver(BoardChartChange change)
    {
        HiddenSeries = new SortedSet<string>(change.HiddenSeries, StringComparer.Ordinal);
        Range = change.Range;
        Scale = change.Scale;
    }

    /// <summary>Why the chart is not drawn, naming the path, when its spec cannot draw the rows its links
    /// let through; null while it is drawn. <see cref="Reading"/> is then of other rows, and not shown.</summary>
    public string? Refusal { get; internal set; }
}

/// <summary>A chart's table showing only the columns its input declares, and, while a link narrows
/// it, only the rows at <c>rows</c>.</summary>
internal sealed class DeclaredChartData(IChartData data, BoardInput input, IReadOnlyList<int>? rows) : IChartData
{
    public IReadOnlyList<string> Columns { get; } = [.. input.Columns.Select(c => c.Name)];

    public int RowCount => rows?.Count ?? data.RowCount;

    public object? Value(string column, int row) => input.Shape.TryGetColumn(column, out _)
        ? data.Value(column, rows is null ? row : rows[row])
        : throw new ArgumentException($"Input '{input.Name}' does not declare column '{column}'.", nameof(column));
}
