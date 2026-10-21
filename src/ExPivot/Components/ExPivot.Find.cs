using ExGrid;
using ExGrid.Finding;
using ExGrid.Selection;
using ExPivot.Engine;

namespace ExPivot.Components;

// Find over the report as shown (ADR-0332 §1): Ctrl+F opens the report grid's find panel, and ExPivot
// answers each step as that grid's Consumer (ADR-0055). What is searched is every cell the report lays
// out — row and column labels, values and totals — by its displayed text, the text the user reads. A
// collapsed group is not searched inside, because its rows are not laid out, and nothing is expanded to
// show a match: a search never changes the report's shape. The cells outside the Window are read from
// the report source, by the Report Version on screen, as Copy reads them (ADR-0151/0152); a remote
// report answers them through its copy transport. The painted matches are marked by the reference rule
// over the same displayed text (ADR-0332 §4), so the mark and the step agree.
public partial class ExPivot
{
    // How many cells one read of the report asks for: a step that finds early stops reading, and a
    // remote report is asked in blocks rather than for the whole report at once.
    private const int FindBlockCells = 20_000;

    // The reference rule over a cell's displayed text (ADR-0332 §4): the one the steps below find by.
    // One instance for every ExPivot: its identity reaches the grid (ADR-0003).
    private static readonly FindMatchOf<PivotDisplayRow> ReportMatch =
        static (row, column, pattern) => GridFind.Matches(column.Info.TextOf(row), pattern);

    /// <summary>
    /// One Find step over the report as shown (ADR-0332 §1), in ADR-0055's order: row by row, across a
    /// row in the request's column order, from the cell after the Focus, wrapping, the Focus's own cell
    /// last; within the selection when more than one cell is selected. A step asked of a report that
    /// has since moved to another order answers nothing, and the grid discards it (ADR-0011). A report
    /// version the source no longer holds, under the same order, is searched again in the report now
    /// on screen.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source refused to read the report, other than
    /// for a version it no longer holds: a step is never answered "no match" for cells it could not
    /// read (ADR-0025).</exception>
    private async Task<GridFindResult> FindInReportAsync(GridFindRequest request, CancellationToken cancellationToken)
    {
        PivotReportVersion? searched = null;
        while (true)
        {
            if (_report is not { } report || _reportSource is not { } source || request.RowSequenceVersion != _rowSequenceVersion)
                return GridFindResult.NotFound;
            if (report.Version == searched)
            {
                throw new InvalidOperationException(
                    $"ExPivot: the report source no longer holds the report on screen ({report.Version.Value}), so Find cannot read it (ADR-0332).");
            }
            searched = report.Version;
            if (await FindInAsync(source, report, request, cancellationToken) is { } answer)
                return answer;
        }
    }

    /// <summary>The step over one Report Version, or null when the source no longer holds it.</summary>
    private static async Task<GridFindResult?> FindInAsync(PivotReportSource source, PivotReportMetadata report,
        GridFindRequest request, CancellationToken cancellationToken)
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
            return GridFindResult.NotFound;

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
                var top = backward ? Math.Max(0, row - blockRows + 1) : row;
                var bottom = backward ? row : Math.Min(rows - 1, row + blockRows - 1);
                var query = new PivotReportCopyQuery(report.Version, names,
                    [new PivotReportRange(top, 0, bottom, names.Length - 1)], (long)(bottom - top + 1) * names.Length);
                var answer = await source.CopyAsync(query, cancellationToken);
                if (answer.Refusal is { Kind: PivotReportRefusalKind.ReportVersionNotHeld })
                    return null;
                if (answer.Refusal is { } refusal)
                    throw new InvalidOperationException($"ExPivot: the report source refused to read the report for Find: {refusal.Message} (ADR-0332).");
                if (answer.Version != query.Version || answer.Blocks.Count != 1 || answer.Blocks[0].Rows.Count != bottom - top + 1
                    || answer.Blocks[0].Rows.Any(cells => cells.Count != names.Length))
                    throw new InvalidOperationException("ExPivot: the report source answered Find's read of the report with another version or range than it asked (ADR-0332).");
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
                    return GridFindResult.Found(row, read[index].Name);
            }
        }
        return GridFindResult.NotFound;
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
