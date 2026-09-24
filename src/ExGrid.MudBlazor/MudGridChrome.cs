using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace ExGrid.MudBlazor;

/// <summary>
/// MudBlazor controls in the grid's Chrome seams (ADR-0010/0030). The seams this
/// package fills, in the order they were verified: the Cell Editor — a bare input
/// inside the box the core hands it, never a <c>MudTextField</c>, which does not fit
/// a 28px cell — the loading bar, a <c>MudProgressLinear</c> where the core places its
/// loading seam, and the column menu and Context Menu, <c>MudButton</c>s with a Material
/// icon each, inside the core's popover. The filter panel falls back to the core's own
/// (null) until it is filled.
///
/// <para>What goes inside a popover is the Wrapper's, and may open popups of its own —
/// a select's options, a picker's calendar — which MudBlazor draws outside the instance
/// root (ADR-0039's Inner Popups; this replaced ADR-0030's "never a <c>MudPopover</c>").
/// The popover itself stays the core's, under the root. Chrome renders and calls back;
/// it decides nothing: which commands there are, what closes a menu and what each key
/// means inside one are the core's.</para>
/// </summary>
public sealed class MudGridChrome : IGridChrome
{
    /// <summary>The one every Consumer can share: an Info-coloured loading bar.</summary>
    public static MudGridChrome Default { get; } = new();

    /// <summary>The loading bar's colour — MudBlazor's own <c>LoadingProgressColor</c>,
    /// with its default. The bar is the seam's content, so its colour lives with the
    /// Chrome rather than on the paper.</summary>
    public Color LoadingProgressColor { get; init; } = Color.Info;

    /// <summary>
    /// The Chrome's own words, for what MudBlazor has no localised text of its own
    /// (ADR-0030, WR-3): handed a command's id, it answers the wording, or null for the
    /// English default. Where MudBlazor has a key — Filter, Hide — its localiser answers
    /// and this is not asked, so one registered <c>MudLocalizer</c> translates both.
    /// </summary>
    public Func<string, string?>? Label { get; init; }

    /// <summary>A Material icon for a command's id, or null for the default: each of the
    /// core's commands has one, and a Consumer's own command has none unless this
    /// supplies it (ADR-0010/0036).</summary>
    public Func<string, string?>? Icon { get; init; }

    /// <summary>The core's panel, inside the core's popover.</summary>
    public RenderFragment? FilterPanel(FilterPanelContext context) => null;

    /// <summary>The column menu: the core's commands as <c>MudButton</c> menu items.</summary>
    public RenderFragment? ColumnMenu(ColumnMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest);
    }

    /// <summary>The Context Menu: the same items over the selection's commands (ADR-0036).</summary>
    public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest);
    }

    private RenderFragment Menu(IReadOnlyList<GridCommand> commands, Action close, int focusRequest)
        => builder =>
        {
            builder.OpenComponent<MudExGridMenu>(0);
            builder.AddComponentParameter(1, nameof(MudExGridMenu.Commands), commands);
            builder.AddComponentParameter(2, nameof(MudExGridMenu.Close), close);
            builder.AddComponentParameter(3, nameof(MudExGridMenu.FocusRequest), focusRequest);
            builder.AddComponentParameter(4, nameof(MudExGridMenu.Chrome), this);
            builder.CloseComponent();
        };

    /// <summary>MudBlazor's own key for a command's word, where it has one.</summary>
    private static string? MudKeyFor(string id) => id switch
    {
        "filter" => "MudDataGrid_Filter",
        "hide" => "MudDataGrid_Hide",
        _ => null,
    };

    /// <summary>What a command is called (WR-3): MudBlazor's localised text where it has
    /// a key, the Chrome's <see cref="Label"/> for the rest, English where that says
    /// nothing.</summary>
    internal string LabelFor(string id, ILocalizationInterceptor localizer)
        => MudKeyFor(id) is { } key
            ? localizer.Handle(key).Value
            : Label?.Invoke(id) ?? BuiltInCommandLabels.For(id);

    /// <summary>A command's icon: the Chrome's <see cref="Icon"/> first, then the
    /// Material icon of each of the core's commands.</summary>
    internal string? IconFor(string id) => Icon?.Invoke(id) ?? id switch
    {
        "sort-ascending" => Icons.Material.Filled.ArrowUpward,
        "sort-descending" => Icons.Material.Filled.ArrowDownward,
        "filter" => Icons.Material.Filled.FilterList,
        "hide" => Icons.Material.Filled.VisibilityOff,
        "pin" => Icons.Material.Filled.PushPin,
        "unpin" => Icons.Material.Outlined.PushPin,
        "size-to-fit" => Icons.Material.Filled.FitScreen,
        "copy" => Icons.Material.Filled.ContentCopy,
        "copy-with-headers" => Icons.Material.Filled.CopyAll,
        _ => null,
    };

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
