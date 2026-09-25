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
/// icon each, and the filter panel — a value list or a condition form of MudBlazor's
/// controls — all inside the core's popover.
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
    /// (ADR-0030, WR-3): handed a command's id or one of <see cref="MudExGridWords"/>' ids,
    /// it answers the wording, or null for the English default. Where MudBlazor has a key —
    /// Filter, Hide, Apply, an operator — its localiser answers and this is not asked, so
    /// one registered <c>MudLocalizer</c> translates both.
    /// </summary>
    public Func<string, string?>? Label { get; init; }

    /// <summary>A Material icon for a command's id, or null for the default: each of the
    /// core's commands has one, and a Consumer's own command has none unless this
    /// supplies it (ADR-0010/0036).</summary>
    public Func<string, string?>? Icon { get; init; }

    /// <summary>The filter panel, from MudBlazor's controls inside the core's popover
    /// (ADR-0009/0030): a value list with a search and a Blank entry where the column
    /// declares one and the answer arrives, the condition form otherwise.</summary>
    public RenderFragment? FilterPanel(FilterPanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudExGridFilterPanel>(0);
            builder.AddComponentParameter(1, nameof(MudExGridFilterPanel.Context), context);
            builder.AddComponentParameter(2, nameof(MudExGridFilterPanel.Chrome), this);
            builder.CloseComponent();
        };
    }

    /// <summary>The column menu: the core's commands as <c>MudButton</c> menu items.</summary>
    public RenderFragment? ColumnMenu(ColumnMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest, context.ResolveKey);
    }

    /// <summary>The Context Menu: the same items over the selection's commands (ADR-0036).</summary>
    public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest, context.ResolveKey);
    }

    private RenderFragment Menu(
        IReadOnlyList<GridCommand> commands, Action close, int focusRequest, MenuKeyResolver? resolveKey)
        => builder =>
        {
            builder.OpenComponent<MudExGridMenu>(0);
            builder.AddComponentParameter(1, nameof(MudExGridMenu.Commands), commands);
            builder.AddComponentParameter(2, nameof(MudExGridMenu.Close), close);
            builder.AddComponentParameter(3, nameof(MudExGridMenu.FocusRequest), focusRequest);
            builder.AddComponentParameter(4, nameof(MudExGridMenu.Chrome), this);
            builder.AddComponentParameter(5, nameof(MudExGridMenu.ResolveKey), resolveKey);
            builder.CloseComponent();
        };

    /// <summary>What a command is called (WR-3): MudBlazor's localised text where it has
    /// a key, the Chrome's <see cref="Label"/> for the rest, English where that says
    /// nothing.</summary>
    internal string LabelFor(string id, ILocalizationInterceptor localizer)
        => MudExGridWords.MudKeyForCommand(id) is { } key
            ? MudExGridWords.Mud(localizer, key)
            : MudExGridWords.Own(this, id);

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

    /// <summary>The Cell Editor: a bare input filling the box the core floats over the
    /// cell, with Material's underline painted — in the error colour while a Reject
    /// stands — and DOM focus taken for each focus request (ADR-0010/0030/0034).</summary>
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

    /// <summary>The loading bar: an indeterminate <c>MudProgressLinear</c> in
    /// <see cref="LoadingProgressColor"/> while the grid is loading, and nothing
    /// otherwise. The Placeholder rows stay the core's (ADR-0004/0030).</summary>
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
