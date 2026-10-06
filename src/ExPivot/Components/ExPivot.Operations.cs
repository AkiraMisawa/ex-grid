using System.Globalization;
using ExGrid.Clipboard;
using ExGrid.Selection;
using ExGrid.Summarizing;
using ExPivot.Engine;

namespace ExPivot.Components;

public partial class ExPivot
{
    private static PivotReportRange ReportRange(SelectionRange range)
        => new(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn);

    private async Task<GridCopyAnswer> CopyReportAsync(GridCopyRequest request, CancellationToken cancellationToken)
    {
        // Capture the entire question before yielding; a new Window never rebinds this gesture.
        if (_report is not { } report || _reportSource is not { } source || request.RowSequenceVersion != _rowSequenceVersion)
            return GridCopyAnswer.Refuse(Word("data-changed"));
        var columns = _columns.Select(column => column.Name).ToArray();
        var plan = request.Plan;
        var query = new PivotReportCopyQuery(report.Version, columns, plan.Segments.Select(ReportRange).ToArray());
        var answer = await source.CopyAsync(query, cancellationToken);
        if (answer.Version != query.Version)
            return GridCopyAnswer.Refuse("The copy answered another Report Version.");
        if (answer.Refusal is { } refusal)
            return GridCopyAnswer.Refuse(refusal.Message);
        if (answer.Blocks.Count != plan.Segments.Count)
            return GridCopyAnswer.Refuse("The copy did not answer every selected range.");
        for (var i = 0; i < plan.Segments.Count; i++)
        {
            var segment = plan.Segments[i];
            var block = answer.Blocks[i];
            if (block.Rows.Count != segment.BottomRow - segment.TopRow + 1
                || block.Headers.Count != segment.RightColumn - segment.LeftColumn + 1
                || block.Rows.Any(row => row.Count != block.Headers.Count))
                return GridCopyAnswer.Refuse("The copy returned an incomplete selected range.");
        }
        var hint = 0;
        PivotReportCopyCell Cell(int row, int column)
        {
            var segment = plan.Segments[hint];
            if (row < segment.TopRow || row > segment.BottomRow || column < segment.LeftColumn || column > segment.RightColumn)
            {
                var low = 0;
                var high = plan.Segments.Count - 1;
                while (low <= high)
                {
                    var middle = low + ((high - low) / 2);
                    var at = plan.Segments[middle];
                    var position = plan.Orientation == CopyOrientation.Vertical ? row : column;
                    var start = plan.Orientation == CopyOrientation.Vertical ? at.TopRow : at.LeftColumn;
                    var end = plan.Orientation == CopyOrientation.Vertical ? at.BottomRow : at.RightColumn;
                    if (position < start) high = middle - 1;
                    else if (position > end) low = middle + 1;
                    else { hint = middle; break; }
                }
                segment = plan.Segments[hint];
            }
            return answer.Blocks[hint].Rows[row - segment.TopRow][column - segment.LeftColumn];
        }
        string Header(int column)
        {
            for (var i = 0; i < plan.Segments.Count; i++)
                if (column >= plan.Segments[i].LeftColumn && column <= plan.Segments[i].RightColumn)
                    return answer.Blocks[i].Headers[column - plan.Segments[i].LeftColumn];
            throw new InvalidOperationException("The copy header is outside the approved plan.");
        }
        var payload = ClipboardData.Assemble(plan, (row, column) => Cell(row, column).Text,
            (row, column) => Cell(row, column).Raw ?? "", request.WithHeaders ? Header : null);
        return GridCopyAnswer.Write(payload.Text, payload.Html);
    }

    private async Task<GridSummaryResult> SummarizeReportAsync(GridSummaryRequest request, CancellationToken cancellationToken)
    {
        if (_report is not { } report || _reportSource is not { } source || request.RowSequenceVersion != _rowSequenceVersion)
            return GridSummaryResult.Declined(Word("data-changed"));
        var query = new PivotReportSummaryQuery(report.Version, request.Columns,
            request.Ranges.Select(ReportRange).ToArray(), request.Focus?.Row, request.Focus?.Column);
        var answer = await source.SummaryAsync(query, cancellationToken);
        if (answer.Version != query.Version)
            return GridSummaryResult.Declined("The summary answered another Report Version.");
        if (answer.Refusal is { } refusal)
            return GridSummaryResult.Declined(refusal.Message);
        var cells = new GridSummaryCells();
        var counts = answer.Counts;
        if (answer.HasError)
        {
            if (counts.Values <= 0) return GridSummaryResult.Declined("The summary returned inconsistent error counts.");
            // AddError supplies the error flag and one already-counted cell.
            counts.Values--;
            cells.AddError();
        }
        cells.Merge(counts, answer.Sum, answer.Extremes);
        var culture = CultureInfo.GetCultureInfo(answer.CultureName);
        return cells.Answer(request.Figures, answer.NumberFormat is { } format
            ? value => ((IFormattable)value).ToString(format, culture) : null);
    }
}
