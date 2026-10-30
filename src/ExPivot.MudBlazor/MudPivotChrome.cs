using ExGrid.Chrome;
using ExGrid.MudBlazor;
using ExPivot.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExPivot.MudBlazor;

/// <summary>
/// ExPivot's Chrome under MudBlazor (ADR-0062, ADR-0410): every slot the Script Core leaves to Razor drawn with
/// MudBlazor's own controls — the PivotTable Fields pane with Defer Layout Update, a placed field's
/// menu and the Layout menu, Filter…, Field Settings…, Value Field Settings…, the Pivot Toolbar
/// above the report with its report filter band, Show Details' tabs and the content of its dialog,
/// and the Stale Report's notice — and the report grid dressed by <see cref="MudGridChrome"/>, so
/// one parameter dresses both.
///
/// <para>It draws what ExPivot hands it and calls back. Which commands there are, what a drop
/// means, the drafts, which tab is selected, and where a menu, panel or dialog opens and what
/// closes it are ExPivot's; swapping this Chrome for the Script Core's own parts changes no behaviour
/// (ADR-0061). Every frame is ExPivot's — the dialog's too, which is never a <c>MudDialog</c> — and
/// the Chrome draws inside it. A <c>MudSelect</c>'s list is an Inner Popup of its panel, reported
/// so that Escape closes the list first (ADR-0039). The words are ExPivot's, by id: one
/// <c>Label</c> written on ExPivot words the pane, its panels, the Pivot Toolbar, the tabs and the
/// report's Context Menu alike.</para>
/// </summary>
public sealed class MudPivotChrome : IPivotChrome
{
    /// <summary>The one every Consumer can share.</summary>
    public static MudPivotChrome Default { get; } = new();

    /// <summary>A Material icon for a command's id — ExPivot's (<see cref="PivotCommandIds"/>) or
    /// the grid's — or null for the default: the Wrapper's own Material icon for ExPivot's command,
    /// then the grid Wrapper's.</summary>
    public Func<string, string?>? Icon { get; init; }

    /// <summary>The report grid's Chrome: <see cref="MudGridChrome"/>, wording ExPivot's commands
    /// in ExPivot's words, which the grid Wrapper would otherwise paint as their ids (ADR-0061).</summary>
    public IGridChrome? GridChrome(Func<string, string?> commandLabel)
    {
        ArgumentNullException.ThrowIfNull(commandLabel);
        return new MudGridChrome
        {
            Label = commandLabel,
            Icon = IconFor,
        };
    }

    /// <summary>The pivot's slots this Chrome fills with Razor (ADR-0410): every part. The report grid's are
    /// <see cref="MudGridChrome"/>'s.</summary>
    public PivotScriptSlots ScriptSlots => PivotScriptSlots.Menu | PivotScriptSlots.Toolbar | PivotScriptSlots.StaleReport
        | PivotScriptSlots.ReportFilters | PivotScriptSlots.ItemFilter | PivotScriptSlots.FieldSettings | PivotScriptSlots.ValueFieldSettings
        | PivotScriptSlots.FieldList | PivotScriptSlots.DetailsTabs | PivotScriptSlots.DetailsDialog;

    /// <summary>A menu when the Script Core draws the pivot (ADR-0410): <c>MudButton</c> items, each marked
    /// for the script that wires them.</summary>
    public RenderFragment? Menu(PivotMenuView view) => ScriptView<MudPivotScriptMenu, PivotMenuView>(view);

    /// <summary>The Pivot Toolbar when the Script Core draws the pivot (ADR-0410): <c>MudButton</c>s, marked
    /// for the script that wires them.</summary>
    public RenderFragment? Toolbar(PivotToolbarView view) => ScriptView<MudPivotScriptToolbar, PivotToolbarView>(view);

    /// <summary>The Stale Report's notice when the Script Core draws the pivot (ADR-0410): a warning
    /// <c>MudAlert</c>, its Retry marked for the script that wires it.</summary>
    public RenderFragment? StaleReport(PivotStaleReportView view) => ScriptView<MudPivotScriptStaleReport, PivotStaleReportView>(view);

    /// <summary>The report filter band when the Script Core draws the pivot (ADR-0410): outlined
    /// <c>MudButton</c>s, each marked with its field for the script that wires it.</summary>
    public RenderFragment? ReportFilters(PivotReportFiltersView view) => ScriptView<MudPivotScriptReportFilters, PivotReportFiltersView>(view, chrome: false);

    /// <summary>Filter… when the Script Core draws the pivot (ADR-0410): MudBlazor's search and checkboxes,
    /// marked for the script that hands each change to the pivot in the browser.</summary>
    public RenderFragment? ItemFilter(PivotItemFilterView view) => ScriptView<MudPivotScriptItemFilter, PivotItemFilterView>(view, chrome: false);

    /// <summary>Field Settings… when the Script Core draws the pivot (ADR-0410): MudBlazor's radios and a native
    /// select in its outline, marked for the script that hands each choice to the pivot in the browser.</summary>
    public RenderFragment? FieldSettings(PivotFieldSettingsView view) => ScriptView<MudPivotScriptFieldSettings, PivotFieldSettingsView>(view, chrome: false);

    /// <summary>Value Field Settings… when the Script Core draws the pivot (ADR-0410): MudBlazor's text fields and
    /// native selects in its outline, marked for the script that hands each change to the pivot in the browser.</summary>
    public RenderFragment? ValueFieldSettings(PivotValueFieldSettingsView view)
        => ScriptView<MudPivotScriptValueFieldSettings, PivotValueFieldSettingsView>(view, chrome: false);

    /// <summary>The PivotTable Fields pane when the Script Core draws the pivot (ADR-0410): MudBlazor's search,
    /// checkboxes and entry buttons, marked for the script that hands each tick, press and drag to the pivot in the
    /// browser.</summary>
    public RenderFragment? FieldList(PivotFieldListView view) => ScriptView<MudPivotScriptFieldList, PivotFieldListView>(view, chrome: false);

    /// <summary>Show Details' sheet tabs when the Script Core draws the pivot (ADR-0410): text MudButtons in a tab list,
    /// each with its close MudIconButton, marked for the script that selects and closes in the browser.</summary>
    public RenderFragment? DetailsTabs(PivotDetailsTabsView view) => ScriptView<MudPivotScriptDetailsTabs, PivotDetailsTabsView>(view, chrome: false);

    /// <summary>Show Details' dialog when the Script Core draws the pivot (ADR-0410): its title, the records' anchor and
    /// Close as a MudButton, marked for the script.</summary>
    public RenderFragment? DetailsDialog(PivotDetailsDialogView view) => ScriptView<MudPivotScriptDetailsDialog, PivotDetailsDialogView>(view, chrome: false);

    /// <summary>The stylesheet this Chrome brings (ADR-0123): <c>mud-ex-pivot.css</c>, with the stamp
    /// this build of ExPivot.MudBlazor ships. ExPivot's root renders its notice beside its own, which
    /// only another version's <c>mud-ex-pivot.css</c> shows. The report grid's
    /// <see cref="MudGridChrome"/> brings <c>mud-ex-grid.css</c>, whose notice the grid renders.</summary>
    public IReadOnlyList<ChromeStylesheet> Stylesheets => Brought;

    private static readonly IReadOnlyList<ChromeStylesheet> Brought =
        [new ChromeStylesheet(ShippedAssets.StylesheetName, ShippedAssets.StylesheetStamp)];

    /// <summary>A command's icon: <see cref="Icon"/> first, then ExPivot's Material icons; null
    /// leaves the grid's own commands to the grid Wrapper's.</summary>
    internal string? IconFor(string id) => Icon?.Invoke(id) ?? MudPivotIcons.ForCommand(id);

    // A part drawn from its view, handed this Chrome too where it asks the Chrome's icons and words (`chrome`).
    private RenderFragment ScriptView<TPart, TView>(TView view, bool chrome = true) where TPart : IComponent
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<TPart>(0);
            builder.AddComponentParameter(1, "View", view);
            if (chrome)
                builder.AddComponentParameter(2, "Chrome", this);
            builder.CloseComponent();
        };
    }
}
