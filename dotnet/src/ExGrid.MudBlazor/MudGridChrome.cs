using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
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

    /// <summary>The slots this Chrome fills when the Script Core draws the grid (ADR-0410): the column
    /// menu, the filter panel, the find panel, the Context Menu, the loading bar, the Cell Editor, the Formula
    /// Bar's two fields and the completion list so far. Every other part is the Script Core's own there until this
    /// Wrapper fills it too.</summary>
    public ScriptSlots ScriptSlots => ScriptSlots.ColumnMenu | ScriptSlots.ContextMenu | ScriptSlots.LoadingIndicator | ScriptSlots.FilterPanel
        | ScriptSlots.FindPanel | ScriptSlots.CellEditor | ScriptSlots.FormulaBarText | ScriptSlots.NameBox | ScriptSlots.EditorCompletion;

    /// <summary>The column menu when the Script Core draws the grid (ADR-0410): the same
    /// <c>MudButton</c> items, each marked for the script that wires them.</summary>
    public RenderFragment? ColumnMenu(GridMenuView view) => ScriptMenu(view);

    /// <summary>The Context Menu when the Script Core draws the grid (ADR-0410), drawn as the column
    /// menu is.</summary>
    public RenderFragment? ContextMenu(GridMenuView view) => ScriptMenu(view);

    /// <summary>The loading bar when the Script Core draws the grid (ADR-0410): an indeterminate
    /// <c>MudProgressLinear</c> in <see cref="LoadingProgressColor"/> while rows are on their way, and nothing
    /// otherwise. The Placeholder rows stay the core's (ADR-0004/0030).</summary>
    public RenderFragment? LoadingIndicator(GridLoadingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        // Nothing to show is nothing rendered: the Placeholder rows and ex-loading are the core's, and stay (ADR-0004).
        if (!view.IsLoading)
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

    /// <summary>The Formula Bar's text field when the Script Core draws the grid (ADR-0410): the same bare input,
    /// marked for the script, which hands it to the core. Its text, its name and its <c>readonly</c> are the core's,
    /// set in the browser, and the stylesheet reads them there: the underline while it takes typing, dimmed while its
    /// text is borrowed. The core names it in the grid's words, not this Chrome's <see cref="Label"/>.</summary>
    public RenderFragment? FormulaBarText(GridFormulaBarTextView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return BarField("mud-ex-formula-bar-text", ScriptSlotAttributes.FormulaBarField);
    }

    /// <summary>The Name Box when the Script Core draws the grid (ADR-0410): the same bare input, marked for the
    /// script, which hands it to the core. What it shows and what Enter in it means are the core's, in the browser; the
    /// core names it in the grid's words, not this Chrome's <see cref="Label"/>.</summary>
    public RenderFragment? NameBox(GridNameBoxView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return BarField("mud-ex-name-box", ScriptSlotAttributes.NameBoxField);
    }

    /// <summary>The completion list and the argument hint when the Script Core draws the grid (ADR-0410): the same
    /// Material list, each candidate marked for the script, which marks the chosen one and accepts a click in the
    /// browser, and the hint beneath it in the caption type.</summary>
    public RenderFragment? EditorCompletion(GridEditorCompletionView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudExGridScriptCompletion>(0);
            builder.AddComponentParameter(1, nameof(MudExGridScriptCompletion.View), view);
            builder.CloseComponent();
        };
    }

    private static RenderFragment BarField(string className, string mark) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "class", className);
        builder.AddAttribute(2, mark, "");
        builder.CloseElement();
    };

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

    /// <summary>
    /// The words the grid's live region says a refusal in (ADR-0203), by its
    /// <see cref="RefusalLabelIds"/> id: MudBlazor's localised text where MudBlazor has a key — it has
    /// none for a refused paste or copy — and otherwise the Chrome's own words, as WR-3 has it for
    /// the filter panel: <see cref="Label"/>, then the built-in Chrome's English. <see cref="Label"/>
    /// answering the empty string silences the announcement for that refusal.
    /// </summary>
    /// <param name="id">The id of the words asked for.</param>
    public string? Words(string id) => MudExGridWords.Own(this, id);

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
}
