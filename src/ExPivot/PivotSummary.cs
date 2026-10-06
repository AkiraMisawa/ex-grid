using ExGrid.Summarizing;
using ExPivot.Engine;

namespace ExPivot;

/// <summary>
/// ExPivot's answer to a Selection Summary (ADR-0130): the cells it lays out, as they read — a
/// Value Field's figure, a subtotal's and a grand total's alike, summed with the rest when selected,
/// as Excel sums them — folded into the family's one definition of the figures. A figure exact in
/// <c>decimal</c> stays exact; an error value (<c>#DIV/0!</c>, <c>#NUM!</c>) is counted and leaves
/// Count alone; a label is counted; an empty cell is in none.
/// </summary>
internal static class PivotSummary
{
    /// <summary>The figures <paramref name="request"/> asks for over the report's rows.</summary>
    internal static GridSummaryResult Answer(PivotReport report, GridSummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        // Each requested column: a label column's index, a value column's, or neither.
        var labels = report.LabelColumns.Count;
        var columns = new (bool IsValue, int Index)?[request.Columns.Count];
        for (var i = 0; i < request.Columns.Count; i++)
        {
            for (var l = 0; l < labels && columns[i] is null; l++)
            {
                if (report.LabelColumns[l].Name == request.Columns[i])
                    columns[i] = (false, l);
            }
            for (var v = 0; v < report.ValueColumns.Count && columns[i] is null; v++)
            {
                if (report.ValueColumns[v].Name == request.Columns[i])
                    columns[i] = (true, v);
            }
        }

        var cells = new GridSummaryCells();
        var rows = report.Rows;
        foreach (var (r, c) in GridSummary.Cells(request, rows.Count))
        {
            if (columns[c] is not { } column)
                continue;
            var row = rows[r];
            if (!column.IsValue)
            {
                if (!string.IsNullOrEmpty(row.Labels[column.Index].Text))
                    cells.AddOther();
                continue;
            }
            switch (row.ValueAt(column.Index))
            {
                case null:
                    break;
                case { IsError: true }:
                    cells.AddError();
                    break;
                case { Exact: { } exact }:
                    cells.AddNumber(exact);
                    break;
                case { } value:
                    cells.AddNumber(value.Number);
                    break;
            }
        }
        // Shown in the Focus cell's Value Field format, as the report shows that cell — Excel's status
        // bar shows its figures in the active cell's format.
        Func<object, string?>? textOf = null;
        if (request.Focus is { } focus && focus.Row < rows.Count && focus.Column < columns.Length
            && columns[focus.Column] is { IsValue: true } focused)
        {
            var valueField = report.ValueFieldAt(rows[focus.Row], focused.Index);
            if (valueField >= 0 && valueField < report.Layout.Values.Count)
            {
                var field = report.Layout.Values[valueField];
                var format = field.NumberFormat ?? (field.ShowValuesAs != PivotShowValuesAs.NoCalculation ? "0.00%" : "G15");
                textOf = figure => ((IFormattable)figure).ToString(format, report.Culture);
            }
        }
        return cells.Answer(request.Figures, textOf);
    }
}
