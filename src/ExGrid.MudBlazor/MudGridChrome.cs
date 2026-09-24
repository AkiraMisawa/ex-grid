using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace ExGrid.MudBlazor;

/// <summary>
/// MudBlazor controls in the grid's Chrome seams (ADR-0010/0030). The seams this
/// package fills, in the order they were verified: the Cell Editor — a bare input
/// inside the box the core hands it, never a <c>MudTextField</c>, which does not fit
/// a 28px cell — and the loading bar, a <c>MudProgressLinear</c> where the core
/// places its loading seam. The filter panel and the column menu fall back to the
/// core's own (null): when they are filled they render as content inside the core's
/// popover, never as a <c>MudPopover</c>, whose provider renders outside the instance
/// root and would break the three dismissals of ADR-0010 and the independence of
/// ADR-0018. Chrome renders and calls back; it decides nothing.
/// </summary>
public sealed class MudGridChrome : IGridChrome
{
    /// <summary>The one every Consumer can share: an Info-coloured loading bar.</summary>
    public static MudGridChrome Default { get; } = new();

    /// <summary>The loading bar's colour — MudBlazor's own <c>LoadingProgressColor</c>,
    /// with its default. The bar is the seam's content, so its colour lives with the
    /// Chrome rather than on the paper.</summary>
    public Color LoadingProgressColor { get; init; } = Color.Info;

    /// <summary>The core's panel, inside the core's popover.</summary>
    public RenderFragment? FilterPanel(FilterPanelContext context) => null;

    /// <summary>The core's menu, inside the core's popover.</summary>
    public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

    public RenderFragment? CellEditor(CellEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudCellEditor>(0);
            builder.AddComponentParameter(1, nameof(MudCellEditor.Context), context);
            builder.CloseComponent();
        };
    }

    public RenderFragment? LoadingIndicator(LoadingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Nothing to show is nothing rendered: the Placeholder rows and ex-loading are
        // the core's, and stay (ADR-0004).
        if (!context.IsLoading)
            return null;
        return builder =>
        {
            builder.OpenComponent<MudProgressLinear>(0);
            builder.AddComponentParameter(1, nameof(MudProgressLinear.Color), LoadingProgressColor);
            builder.AddComponentParameter(2, nameof(MudProgressLinear.Indeterminate), true);
            builder.AddComponentParameter(3, nameof(MudProgressLinear.Class), "mud-ex-grid-loading");
            builder.CloseComponent();
        };
    }
}
