using ExGrid;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExPivot.Components;

/// <summary>
/// A details sheet's records (ADR-0059): an ExGrid over the sheet's fetching source — as tall and
/// as wide as the box it stands in — or, once the source refused or failed, the sentence that says
/// why, in place of records that would not add up. Like the report's grid, it renders only when
/// what it reads changed (ADR-0003's rule, written by hand: a parameter of a reference type is
/// "changed" to Blazor every time), so the pane's gestures do not reach it.
/// </summary>
internal sealed class PivotDetailsGrid : ComponentBase
{
    private object? _rendered;

    /// <summary>The sheet whose records are shown.</summary>
    [Parameter, EditorRequired] public PivotDetailsSheet Sheet { get; set; } = default!;

    /// <summary>The sheet's version: it moves when its problem does.</summary>
    [Parameter] public int Version { get; set; }

    /// <summary>Why the records cannot be shown, in words, or null.</summary>
    [Parameter] public string? Problem { get; set; }

    /// <summary>The grid's Chrome — the report's.</summary>
    [Parameter] public IGridChrome? Chrome { get; set; }

    /// <summary>The words of the grid's commands — the report's.</summary>
    [Parameter] public Func<string, string?>? CommandLabel { get; set; }

    /// <summary>The row height, or null for the Density's.</summary>
    [Parameter] public double? RowHeight { get; set; }

    /// <summary>The Density, or null for a Wrapper's, or Compact.</summary>
    [Parameter] public GridDensity? Density { get; set; }

    /// <summary>The Cell Metrics, or null for a Wrapper's, or the Density's.</summary>
    [Parameter] public CellTextMetrics? CellMetrics { get; set; }

    /// <summary>The grid's <c>OnLeave</c> (ADR-0070): the dialog's way out, raised by an Escape the
    /// grid has nothing left to dismiss. A tab's grid declares none: Escape does not close a sheet.
    /// Held in a field by ExPivot, so it is the same callback on every render.</summary>
    [Parameter] public EventCallback OnLeave { get; set; }

    private object State => (Sheet, Version, Problem, Chrome, CommandLabel, RowHeight, Density, CellMetrics, OnLeave);

    protected override bool ShouldRender() => !Equals(State, _rendered);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _rendered = State;
        if (Problem is { } problem)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "ex-pivot-details-status");
            builder.AddAttribute(2, "role", "alert");
            builder.AddContent(3, problem);
            builder.CloseElement();
            return;
        }
        builder.OpenComponent<ExGrid<PivotDetailRecord>>(4);
        // A grid is bound to its source for its lifetime: another sheet is another grid.
        builder.SetKey(Sheet);
        builder.AddComponentParameter(5, nameof(ExGrid<PivotDetailRecord>.Source), Sheet.Source);
        builder.AddComponentParameter(6, nameof(ExGrid<PivotDetailRecord>.Columns), Sheet.Columns);
        builder.AddComponentParameter(7, nameof(ExGrid<PivotDetailRecord>.ViewportHeight), ViewportSize.Stretch);
        builder.AddComponentParameter(8, nameof(ExGrid<PivotDetailRecord>.ViewportWidth), ViewportSize.Stretch);
        // The records are in the data's order and none is left out: the header click selects, and
        // there is no column menu to sort or filter them by (ADR-0059).
        builder.AddComponentParameter(9, nameof(ExGrid<PivotDetailRecord>.HeaderClickSelects), true);
        builder.AddComponentParameter(10, nameof(ExGrid<PivotDetailRecord>.HideColumnMenu), true);
        builder.AddComponentParameter(11, nameof(ExGrid<PivotDetailRecord>.CellType), PivotDetailsSheet.CellTypeOf);
        if (CommandLabel is { } label)
            builder.AddComponentParameter(12, nameof(ExGrid<PivotDetailRecord>.CommandLabel), label);
        if (Chrome is { } chrome)
            builder.AddComponentParameter(13, nameof(ExGrid<PivotDetailRecord>.Chrome), chrome);
        if (RowHeight is { } rowHeight)
            builder.AddComponentParameter(14, nameof(ExGrid<PivotDetailRecord>.RowHeight), rowHeight);
        if (Density is { } density)
            builder.AddComponentParameter(15, nameof(ExGrid<PivotDetailRecord>.Density), density);
        if (CellMetrics is { } metrics)
            builder.AddComponentParameter(16, nameof(ExGrid<PivotDetailRecord>.CellMetrics), metrics);
        if (OnLeave.HasDelegate)
            builder.AddComponentParameter(17, nameof(ExGrid<PivotDetailRecord>.OnLeave), OnLeave);
        builder.CloseComponent();
    }
}
