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
/// icon each, the filter panel — a value list or a condition form of MudBlazor's
/// controls — and the find panel (ADR-0055), all inside the core's popover; the Formula Bar's
/// two fields, bare inputs in the core's boxes as the Cell Editor is, and the completion list
/// with its argument hint (ADR-0051).
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

    /// <summary>The stylesheet this Chrome brings (ADR-0123): <c>mud-ex-grid.css</c>, with the
    /// stamp this build of ExGrid.MudBlazor ships. The grid's root renders its notice beside its
    /// own, which only another version's <c>mud-ex-grid.css</c> shows. The same list for every
    /// instance: the stylesheet is the package's, not a setting.</summary>
    public IReadOnlyList<ChromeStylesheet> Stylesheets => Brought;

    private static readonly IReadOnlyList<ChromeStylesheet> Brought =
        [new ChromeStylesheet(ShippedAssets.StylesheetName, ShippedAssets.StylesheetStamp)];

    /// <summary>The filter panel, from MudBlazor's controls under the column's commands in the
    /// core's one popover (ADR-0009/0030/0044): a value list with a search and a Blank entry
    /// where the column declares one and the answer arrives, the condition form otherwise.</summary>
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

    /// <summary>The find panel (ADR-0055): a <c>MudTextField</c>, the two options and the two
    /// steps, inside the core's popover.</summary>
    public RenderFragment? FindPanel(FindContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudExGridFindPanel>(0);
            builder.AddComponentParameter(1, nameof(MudExGridFindPanel.Context), context);
            builder.AddComponentParameter(2, nameof(MudExGridFindPanel.Chrome), this);
            builder.CloseComponent();
        };
    }

    /// <summary>The Selection Summary (ADR-0130): each figure a <c>MudText</c> caption in the core's
    /// <c>ex-summary</c> box, named in the Chrome's words; the pending mark and a decline's reason
    /// likewise. The figures' text is the core's, and the right-click on the box stays the core's.</summary>
    public RenderFragment? SelectionSummary(SelectionSummaryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            switch (context.Status)
            {
                case Summarizing.SelectionSummaryStatus.Pending:
                    Caption(builder, 0, "mud-ex-grid-summary-pending", MudExGridWords.Own(this, SummaryLabelIds.Pending));
                    break;
                case Summarizing.SelectionSummaryStatus.Declined:
                    Caption(builder, 10, "mud-ex-grid-summary-declined", context.DeclineReason ?? "");
                    break;
                default:
                    foreach (var figure in context.Figures)
                    {
                        Caption(builder, 20, "mud-ex-grid-summary-figure", $"{MudExGridWords.Own(this, figure.LabelId)}: {figure.Text}", figure.Figure);
                    }
                    break;
            }
        };
    }

    private static void Caption(RenderTreeBuilder builder, int sequence, string cssClass, string text, object? key = null)
    {
        builder.OpenComponent<MudText>(sequence);
        if (key is not null)
            builder.SetKey(key);
        builder.AddComponentParameter(sequence + 1, nameof(MudText.Typo), Typo.caption);
        builder.AddComponentParameter(sequence + 2, nameof(MudText.Class), cssClass);
        builder.AddComponentParameter(sequence + 3, nameof(MudText.ChildContent), (RenderFragment)(b => b.AddContent(0, text)));
        builder.CloseComponent();
    }

    /// <summary>The column menu: the core's commands as <c>MudButton</c> menu items.</summary>
    public RenderFragment? ColumnMenu(ColumnMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest, context.ResolveKey, context.TakeFocus);
    }

    /// <summary>The Context Menu: the same items over the selection's commands (ADR-0036).</summary>
    public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Menu(context.Commands, context.Close, context.FocusRequest, context.ResolveKey, context.TakeFocus);
    }

    /// <summary>The slots this Chrome fills when the Script Core draws the grid (ADR-0410): the column
    /// menu, the filter panel, the find panel, the Context Menu, the loading bar and the Cell Editor so far. Every
    /// other part is the Script Core's own there until this Wrapper fills it too.</summary>
    public ScriptSlots ScriptSlots => ScriptSlots.ColumnMenu | ScriptSlots.ContextMenu | ScriptSlots.LoadingIndicator | ScriptSlots.FilterPanel
        | ScriptSlots.FindPanel | ScriptSlots.CellEditor;

    /// <summary>The column menu when the Script Core draws the grid (ADR-0410): the same
    /// <c>MudButton</c> items, each marked for the script that wires them.</summary>
    public RenderFragment? ColumnMenu(GridMenuView view) => ScriptMenu(view);

    /// <summary>The Context Menu when the Script Core draws the grid (ADR-0410), drawn as the column
    /// menu is.</summary>
    public RenderFragment? ContextMenu(GridMenuView view) => ScriptMenu(view);

    /// <summary>The loading bar when the Script Core draws the grid (ADR-0410): the same
    /// <c>MudProgressLinear</c> while rows are on their way, and nothing otherwise.</summary>
    public RenderFragment? LoadingIndicator(GridLoadingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return LoadingIndicator(new LoadingContext(view.IsLoading, view.PlaceholderRowCount));
    }

    /// <summary>The filter panel when the Script Core draws the grid (ADR-0410): MudBlazor's controls,
    /// drawn from the core's view and marked for the script, which hands each change to the core.</summary>
    public RenderFragment? FilterPanel(GridFilterView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudExGridScriptFilterPanel>(0);
            builder.AddComponentParameter(1, nameof(MudExGridScriptFilterPanel.View), view);
            builder.AddComponentParameter(2, nameof(MudExGridScriptFilterPanel.Chrome), this);
            builder.CloseComponent();
        };
    }

    /// <summary>The find panel when the Script Core draws the grid (ADR-0410): MudBlazor's controls, drawn
    /// from the core's view and marked for the script, which hands each change and press to the core.</summary>
    public RenderFragment? FindPanel(GridFindView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudExGridScriptFindPanel>(0);
            builder.AddComponentParameter(1, nameof(MudExGridScriptFindPanel.View), view);
            builder.AddComponentParameter(2, nameof(MudExGridScriptFindPanel.Chrome), this);
            builder.CloseComponent();
        };
    }

    /// <summary>The Cell Editor when the Script Core draws the grid (ADR-0410): the same bare input with Material's
    /// underline, marked for the script, which hands it to the core. It holds the opening text as drawn and nothing
    /// bound after, so what is typed is never written back a round trip late (SRV-7); the underline turns the error
    /// colour by the <c>aria-invalid</c> the core sets, with no round trip.</summary>
    public RenderFragment? CellEditor(GridCellEditorView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "class", "mud-ex-editor");
            builder.AddAttribute(2, ScriptSlotAttributes.EditorField, "");
            builder.AddAttribute(3, "value", view.InitialText);
            builder.AddAttribute(4, "aria-label", view.Column);
            builder.CloseElement();
        };
    }

    private RenderFragment ScriptMenu(GridMenuView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudExGridScriptMenu>(0);
            builder.AddComponentParameter(1, nameof(MudExGridScriptMenu.View), view);
            builder.AddComponentParameter(2, nameof(MudExGridScriptMenu.Chrome), this);
            builder.CloseComponent();
        };
    }

    private RenderFragment Menu(
        IReadOnlyList<GridCommand> commands, Action close, int focusRequest, MenuKeyResolver? resolveKey,
        Func<ElementReference, bool, Task>? takeFocus)
        => builder =>
        {
            builder.OpenComponent<MudExGridMenu>(0);
            builder.AddComponentParameter(1, nameof(MudExGridMenu.Commands), commands);
            builder.AddComponentParameter(2, nameof(MudExGridMenu.Close), close);
            builder.AddComponentParameter(3, nameof(MudExGridMenu.FocusRequest), focusRequest);
            builder.AddComponentParameter(4, nameof(MudExGridMenu.Chrome), this);
            builder.AddComponentParameter(5, nameof(MudExGridMenu.ResolveKey), resolveKey);
            builder.AddComponentParameter(6, nameof(MudExGridMenu.TakeFocus), takeFocus);
            builder.CloseComponent();
        };

    /// <summary>
    /// The words the grid's live region says a refusal in (ADR-0203), by its
    /// <see cref="RefusalLabelIds"/> id: MudBlazor's localised text where MudBlazor has a key — it has
    /// none for a refused paste or copy — and otherwise the Chrome's own words, as WR-3 has it for
    /// the filter panel: <see cref="Label"/>, then the built-in Chrome's English. <see cref="Label"/>
    /// answering the empty string silences the announcement for that refusal.
    /// </summary>
    /// <param name="id">The id of the words asked for.</param>
    public string? Words(string id) => MudExGridWords.Own(this, id);

    /// <summary>What a command is called (WR-3): MudBlazor's localised text where it has
    /// a key, the Chrome's <see cref="Label"/> for the rest, English where that says
    /// nothing.</summary>
    internal string LabelFor(string id, ILocalizationInterceptor localizer)
        => MudExGridWords.MudKeyForCommand(id) is { } key
            ? MudExGridWords.Mud(localizer, key)
            : MudExGridWords.Own(this, id);

    /// <summary>A command's word over the Script Core (ADR-0410): MudBlazor's where it has a key, then
    /// <see cref="Label"/>, then the words the Script Core sent, which know every command it has.</summary>
    internal string LabelFor(GridMenuItemView item, ILocalizationInterceptor localizer)
        => MudExGridWords.MudKeyForCommand(item.Id) is { } key
            ? MudExGridWords.Mud(localizer, key)
            : Label?.Invoke(item.Id) ?? item.Label;

    /// <summary>A command's icon: the Chrome's <see cref="Icon"/> first, then the
    /// Material icon of each of the core's commands.</summary>
    internal string? IconFor(string id) => Icon?.Invoke(id) ?? id switch
    {
        GridCommandIds.SortAscending => Icons.Material.Filled.ArrowUpward,
        GridCommandIds.SortDescending => Icons.Material.Filled.ArrowDownward,
        GridCommandIds.ClearFilter => Icons.Material.Filled.FilterListOff,
        GridCommandIds.Hide => Icons.Material.Filled.VisibilityOff,
        GridCommandIds.Pin => Icons.Material.Filled.PushPin,
        GridCommandIds.Unpin => Icons.Material.Outlined.PushPin,
        GridCommandIds.SizeToFit => Icons.Material.Filled.FitScreen,
        GridCommandIds.Copy => Icons.Material.Filled.ContentCopy,
        GridCommandIds.CopyWithHeaders => Icons.Material.Filled.CopyAll,
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

    /// <summary>The Formula Bar's Name Box (ADR-0051/0030): a bare input filling the box the
    /// core stands in the bar, inside the core's form, named by
    /// <see cref="MudExGridWords.NameBox"/>.</summary>
    public RenderFragment? NameBox(NameBoxContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var label = MudExGridWords.Own(this, MudExGridWords.NameBox);
        return builder =>
        {
            builder.OpenComponent<MudNameBox>(0);
            builder.AddComponentParameter(1, nameof(MudNameBox.Context), context);
            builder.AddComponentParameter(2, nameof(MudNameBox.Label), label);
            builder.CloseComponent();
        };
    }

    /// <summary>The Formula Bar's text field (ADR-0051/0030): the Cell Editor's second
    /// surface, painted as the Cell Editor is — a bare input with Material's underline while
    /// it takes typing — and focused for each request. Named by
    /// <see cref="MudExGridWords.FormulaBar"/>.</summary>
    public RenderFragment? FormulaBarText(FormulaBarTextContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var label = MudExGridWords.Own(this, MudExGridWords.FormulaBar);
        return builder =>
        {
            builder.OpenComponent<MudFormulaBarText>(0);
            builder.AddComponentParameter(1, nameof(MudFormulaBarText.Context), context);
            builder.AddComponentParameter(2, nameof(MudFormulaBarText.Label), label);
            builder.CloseComponent();
        };
    }

    /// <summary>The completion list and argument hint (ADR-0051/0030): the Consumer's candidates
    /// as a Material list with the chosen one selected, and the hint beneath in the caption
    /// type, inside the box the core stands beneath the editor.</summary>
    public RenderFragment? EditorCompletion(EditorCompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudEditorCompletion>(0);
            builder.AddComponentParameter(1, nameof(MudEditorCompletion.Context), context);
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
