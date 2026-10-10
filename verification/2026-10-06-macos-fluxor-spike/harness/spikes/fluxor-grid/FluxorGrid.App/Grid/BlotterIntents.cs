using ExGrid;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Keys;
using ExGrid.Selection;
using Fluxor;
using FluxorGrid.Store;

namespace FluxorGrid.Grid;

/// <summary>
/// What both pages do with the grid's intents: each becomes one action for the store. An intent
/// names cells by position, so the positions are resolved against the Window the grid was given
/// — W1's pushed Window, W2's source Window — and refused when its Row Sequence Version is not
/// that Window's (ADR-0011/0014): the store may be a version ahead of the grid, and positions
/// read against it would name other trades.
/// </summary>
public sealed class BlotterIntents(
    IDispatcher dispatcher,
    TimeProvider clock,
    Func<IReadOnlyList<Trade>> window,
    Func<int> sequenceVersion,
    Func<IReadOnlyList<CellEdit>, IReadOnlyDictionary<string, Trade>?>? beforeDispatch = null)
{
    public static readonly string[] UpstreamKeys = ["F7", "F8", "F9"];

    public string EditStatus { get; private set; } = "—";

    public string CommitRefused { get; private set; } = "—";

    public string PasteRefused { get; private set; } = "—";

    public string Upstream { get; private set; } = "—";

    public int UpstreamCount { get; private set; }

    public void Edit(GridEditIntent<Trade> intent)
    {
        EditStatus = $"edit {intent.Row.Id}.{intent.Column}={intent.Value}";
        Write([new CellEdit(intent.Row.Id, intent.Column, intent.Value, intent.Row)], "edit");
    }

    public void Paste(GridPasteIntent intent)
    {
        var rows = window();
        if (intent.RowSequenceVersion != sequenceVersion())
        {
            intent.Refuse();
            EditStatus = $"paste refused: version {intent.RowSequenceVersion}, the Window is {sequenceVersion()}";
            return;
        }
        var edits = new List<CellEdit>();
        foreach (var target in intent.Plan.Targets)
        {
            for (var row = target.TopRow; row <= Math.Min(target.BottomRow, rows.Count - 1); row++)
            {
                for (var column = target.LeftColumn; column <= target.RightColumn; column++)
                {
                    var trade = rows[row];
                    edits.Add(new CellEdit(trade.Id, TradeColumns.All[column].Name, intent.ValueFor(new CellPosition(row, column)), trade));
                }
            }
        }
        var origin = intent.FillSource is null ? "paste" : "fill-key";
        EditStatus = $"{origin} {edits.Count} cells";
        Write(edits, origin);
    }

    /// <summary>A fill-handle drag: each target cell takes the source cell above or below it in its
    /// column, cycling through the source.</summary>
    public void Fill(GridFillIntent intent)
    {
        var rows = window();
        if (intent.RowSequenceVersion != sequenceVersion())
        {
            intent.Refuse();
            EditStatus = "fill refused: the order moved";
            return;
        }
        if (intent.Direction is not (GridDirection.Down or GridDirection.Up))
        {
            intent.Refuse();
            EditStatus = "fill refused: down or up only";
            return;
        }
        var source = intent.Source;
        var target = intent.Target;
        var height = source.BottomRow - source.TopRow + 1;
        var edits = new List<CellEdit>();
        for (var row = target.TopRow; row <= Math.Min(target.BottomRow, rows.Count - 1); row++)
        {
            var from = rows[source.TopRow + ((((row - source.TopRow) % height) + height) % height)];
            for (var column = target.LeftColumn; column <= target.RightColumn; column++)
            {
                var name = TradeColumns.All[column].Name;
                edits.Add(new CellEdit(rows[row].Id, name, TradeColumns.RawText(from, name), rows[row]));
            }
        }
        EditStatus = $"fill-drag {edits.Count} cells";
        Write(edits, "fill-drag");
    }

    public void Clear(GridClearIntent intent)
    {
        var rows = window();
        if (intent.RowSequenceVersion != sequenceVersion())
        {
            EditStatus = "clear ignored: the order moved";
            return;
        }
        var edits = new List<CellEdit>();
        foreach (var target in intent.Targets)
        {
            for (var row = target.TopRow; row <= Math.Min(target.BottomRow, rows.Count - 1); row++)
            {
                for (var column = target.LeftColumn; column <= target.RightColumn; column++)
                    edits.Add(new CellEdit(rows[row].Id, TradeColumns.All[column].Name, null, rows[row]));
            }
        }
        EditStatus = $"clear {edits.Count} cells";
        Write(edits, "clear");
    }

    public void OnCommitRefused(GridCommitRefusal refusal)
        => CommitRefused = $"{refusal.Reason}: {refusal.Column} shows {refusal.PaintedText}";

    public void OnPasteRefused(PasteRefusalReason reason) => PasteRefused = reason.ToString();

    /// <summary>The checks' upstream changes, declared to the grid so they reach it in its turn
    /// among the keys, with an edit open too (ADR-0050, item 14): F9 amends the Focus cell; F8 gives
    /// the trade at position 10 the largest P&amp;L (a tick that moves a row under a sort by P&amp;L);
    /// F7 moves the price of the first ten positions, P&amp;L unchanged (a tick that changes values
    /// only).</summary>
    public void Key(GridDeclaredKeyPress press)
    {
        var rows = window();
        var now = clock.GetUtcNow();
        UpstreamCount++;
        switch (press.Key)
        {
            case "F9":
                var focus = press.Selection.Focus;
                var trade = rows[focus.Row];
                var column = TradeColumns.All[focus.Column].Name;
                dispatcher.Dispatch(new UpstreamAmend(trade.Id, column, now));
                Upstream = $"F9 amended {trade.Id}.{column} (×{UpstreamCount})";
                break;
            case "F8":
                var top = rows.Max(t => t.Pnl);
                var moved = rows[10];
                dispatcher.Dispatch(new UpstreamPnl(moved.Id, top + 1_000m, now));
                Upstream = $"F8 moved {moved.Id} to P&L {top + 1_000m} (×{UpstreamCount})";
                break;
            case "F7":
                var ids = rows.Take(10).Select(t => t.Id).ToArray();
                dispatcher.Dispatch(new UpstreamPrices(ids, now));
                Upstream = $"F7 moved 10 prices (×{UpstreamCount})";
                break;
        }
    }

    private void Write(IReadOnlyList<CellEdit> edits, string origin)
    {
        var replacements = beforeDispatch?.Invoke(edits);
        dispatcher.Dispatch(new EditCells(edits, origin, replacements));
    }
}
