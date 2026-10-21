using ExGrid;
using ExGrid.Finding;
using ExGrid.Selection;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// Find over the report as shown (ADR-0332 §1): Ctrl+F opens the report grid's find panel, and ExPivot
// answers each step as that grid's Consumer (ADR-0055). What is searched is every cell the report's
// grid lays out — the row labels, values and totals — by its displayed text, the text the user reads.
// The column Items and the value captions stand in the grid's headers, which Find cannot land on, so
// they are not searched (ADR-0332 §1 names column labels too; that is an open question). A collapsed
// group is not searched inside, because its rows are not laid out, and nothing is expanded to show a
// match: a search never changes the report's shape. The cells outside the Window are read from the
// report source, by the Report Version on screen, as Copy reads them (ADR-0151/0152); a remote report
// answers them through its copy transport. The painted matches are marked by the reference rule over
// the same displayed text (ADR-0332 §4), so the mark and the step agree. A report is not written, so
// Ctrl+H is refused, and ExPivot says so (§2).
public partial class ExPivot
{
    // How many cells one read of the report asks for: a step that finds early stops reading, and a
    // remote report is asked in blocks rather than for the whole report at once.
    private const int FindBlockCells = 20_000;

    // How many Report Versions one step searches before it says the report kept changing under it.
    private const int FindAttempts = 3;

    // The reference rule over a cell's displayed text (ADR-0332 §4): the one the steps below find by.
    // One instance for every ExPivot: its identity reaches the grid (ADR-0003).
    private static readonly FindMatchOf<PivotDisplayRow> ReportMatch =
        static (row, column, pattern) => GridFind.Matches(column.Info.TextOf(row), pattern);

    // The grid's refusal of a Find or a Replace, heard to say what the grid leaves to its Consumer.
    private readonly EventCallback<FindRefusalReason> _findRefused;

    // What one search over one Report Version came to.
    private enum FindRead { Answered, NotHeld, Refused, Invalid }

    /// <summary>
    /// One Find step over the report as shown (ADR-0332 §1), in ADR-0055's order: row by row, across a
    /// row in the request's column order, from the cell after the Focus, wrapping, the Focus's own cell
    /// last; within the selection when more than one cell is selected. A step asked of a report that
    /// has since moved to another order answers nothing, and the grid discards it (ADR-0011).
    ///
    /// <para>The answer is always about the report on screen: a step whose Report Version was replaced
    /// while it read — a live update under the same order, or a version the source stopped holding — is
    /// searched again over the report now shown, up to <see cref="FindAttempts"/> versions. Where it
    /// cannot go on — the source refuses to be read, answers another version or range than asked,
    /// no longer holds the report shown, or the report keeps changing — the Pivot Toolbar says why, and
    /// the step is given up: nothing moves, and nothing claims "no match" for cells it could not read
    /// (ADR-0025). It is given up by throwing <see cref="OperationCanceledException"/>, which the grid
    /// takes as a step its Consumer gave up.</para>
    /// </summary>
    private async Task<GridFindResult> FindInReportAsync(GridFindRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (_report is not { } report || _reportSource is not { } source || request.RowSequenceVersion != _rowSequenceVersion)
                return GridFindResult.NotFound;
            if (attempt == FindAttempts)
                GiveUpFind(Word("find-report-changing"));
            var (read, answer, refusal) = await FindInAsync(source, report, request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var shown = _report is { } now && now.Version == report.Version;
            switch (read)
            {
                case FindRead.Answered when shown:
                    return answer!;
                case FindRead.NotHeld when shown:
                    GiveUpFind(Word("find-report-not-held"));
                    break;
                case FindRead.Refused:
                    GiveUpFind(SourceRefused(refusal!));
                    break;
                case FindRead.Invalid:
                    GiveUpFind(Word("find-invalid-answer"));
                    break;
            }
            // Another version is on screen now: the step is searched again over it.
        }
    }

    /// <summary>Says on the Pivot Toolbar why a step cannot go on, and gives the step up.</summary>
    private void GiveUpFind(string sentence)
    {
        _refusal = sentence;
        StateHasChanged();
        throw new OperationCanceledException(sentence);
    }

    /// <summary>The grid refused a Find or a Replace (ADR-0055, ADR-0332 §2): Ctrl+H on a report, which
    /// is not written, is said on the Pivot Toolbar in Excel's words. The refusals the find panel shows
    /// itself are left to it.</summary>
    private Task OnFindRefusedAsync(FindRefusalReason reason)
    {
        if (reason is FindRefusalReason.ReplaceUnavailable)
        {
            _refusal = Word("replace-unavailable");
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    /// <summary>The step over one Report Version.</summary>
    private static async Task<(FindRead Read, GridFindResult? Answer, PivotReportRefusal? Refusal)> FindInAsync(
        PivotReportSource source, PivotReportMetadata report, GridFindRequest request, CancellationToken cancellationToken)
    {
        var rows = report.RowCount;
        // The request's columns the report lays out, by their position in the request. One it does not
        // know left the report while the step was out; the grid says so if a match is answered there.
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in report.LabelColumns) known.Add(label.Name);
        foreach (var value in report.ValueColumns) known.Add(value.Name);
        var read = new List<(string Name, int Position)>(request.Columns.Count);
        for (var i = 0; i < request.Columns.Count; i++)
        {
            if (known.Contains(request.Columns[i]))
                read.Add((request.Columns[i], i));
        }
        var columns = request.Columns.Count;
        if (string.IsNullOrEmpty(request.Text) || rows == 0 || read.Count == 0)
            return (FindRead.Answered, GridFindResult.NotFound, null);

        // Where the walk starts: past the Focus, which is looked at last; without one, from the first
        // cell (the last, backward), as GridFind.Step walks.
        var backward = request.Backward;
        var (startRow, startColumn) = request.From is { } from
            && from.Row >= 0 && from.Row < rows && from.Column >= 0 && from.Column < columns
            ? (from.Row, from.Column)
            : backward ? (0, 0) : (rows - 1, columns - 1);
        var step = backward ? -1 : 1;
        var pattern = request.Pattern;
        var names = read.Select(column => column.Name).ToArray();
        var blockRows = Math.Max(1, FindBlockCells / names.Length);

        PivotReportCopyBlock? block = null;
        var blockTop = 0;
        for (var visit = 0; visit <= rows; visit++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = (((startRow + (step * visit)) % rows) + rows) % rows;
            if (request.Scope is { } scope && !RowInScope(scope, row))
                continue;
            if (block is null || row < blockTop || row >= blockTop + block.Rows.Count)
            {
                // The thread is given up between two reads, so a long search over a large report holds
                // neither the page nor a circuit, and a step that supersedes it, or Escape, stops it.
                if (block is not null)
                    await PivotSlicing.Default.YieldAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var top = backward ? Math.Max(0, row - blockRows + 1) : row;
                var bottom = backward ? row : Math.Min(rows - 1, row + blockRows - 1);
                var query = new PivotReportCopyQuery(report.Version, names,
                    [new PivotReportRange(top, 0, bottom, names.Length - 1)], (long)(bottom - top + 1) * names.Length);
                var answer = await source.CopyAsync(query, cancellationToken);
                if (answer.Refusal is { Kind: PivotReportRefusalKind.ReportVersionNotHeld })
                    return (FindRead.NotHeld, null, null);
                if (answer.Refusal is { } refusal)
                    return (FindRead.Refused, null, refusal);
                if (answer.Version != query.Version || answer.Blocks.Count != 1 || answer.Blocks[0].Rows.Count != bottom - top + 1
                    || answer.Blocks[0].Rows.Any(cells => cells.Count != names.Length))
                    return (FindRead.Invalid, null, null);
                block = answer.Blocks[0];
                blockTop = top;
            }
            var cells = block.Rows[row - blockTop];
            for (var k = 0; k < read.Count; k++)
            {
                var index = backward ? read.Count - 1 - k : k;
                var position = read[index].Position;
                // The Focus's row is visited twice: first past the Focus, last up to it and on it.
                if (visit == 0 && (backward ? position >= startColumn : position <= startColumn))
                    continue;
                if (visit == rows && (backward ? position < startColumn : position > startColumn))
                    continue;
                if (request.Scope is { } within && !InScope(within, row, position))
                    continue;
                if (GridFind.Matches(cells[index].Text, pattern))
                    return (FindRead.Answered, GridFindResult.Found(row, read[index].Name), null);
            }
        }
        return (FindRead.Answered, GridFindResult.NotFound, null);
    }

    private static bool RowInScope(IReadOnlyList<SelectionRange> scope, int row)
    {
        foreach (var range in scope)
        {
            if (row >= range.TopRow && row <= range.BottomRow)
                return true;
        }
        return false;
    }

    private static bool InScope(IReadOnlyList<SelectionRange> scope, int row, int column)
    {
        foreach (var range in scope)
        {
            if (range.Contains(new CellPosition(row, column)))
                return true;
        }
        return false;
    }
}
