using ExGrid.Chrome;
using ExGrid.MudBlazor;
using ExPivot.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExPivot.MudBlazor;

/// <summary>
/// ExPivot's Chrome under MudBlazor (ADR-0062): every surface ExPivot hands a Chrome drawn with
/// MudBlazor's own controls — the PivotTable Fields pane with Defer Layout Update, a placed field's
/// menu and the Layout menu, Filter…, Field Settings…, Value Field Settings…, the toolbar above the
/// report with its report filter band, Show Details' tabs and the content of its dialog, and the
/// Stale Report's notice — and the report grid dressed by <see cref="MudGridChrome"/>, so one
/// parameter dresses both.
///
/// <para>It draws what ExPivot hands it and calls back. Which commands there are, what a drop
/// means, the drafts, which tab is selected, and where a menu, panel or dialog opens and what
/// closes it are ExPivot's; swapping this Chrome for the built-in markup changes no behaviour
/// (ADR-0061). Every frame is ExPivot's — the dialog's too, which is never a <c>MudDialog</c> — and
/// the Chrome draws inside it. A <c>MudSelect</c>'s list is an Inner Popup of its panel, reported
/// so that Escape closes the list first (ADR-0039). The words are ExPivot's, by id: one
/// <c>Label</c> written on ExPivot words the pane, its panels, the toolbar, the tabs and the
/// report's Context Menu alike.</para>
/// </summary>
public sealed class MudPivotChrome : IPivotChrome
{
    /// <summary>The one every Consumer can share.</summary>
    public static MudPivotChrome Default { get; } = new();

    /// <summary>A Material icon for a command's id — ExPivot's (<see cref="PivotCommandIds"/>) or
    /// the grid's — or null for the default: <see cref="MudPivotIcons.ForCommand"/>, then the grid
    /// Wrapper's own.</summary>
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

    /// <summary>The PivotTable Fields pane (<see cref="MudPivotFieldList"/>).</summary>
    public RenderFragment? FieldList(PivotFieldListContext context) => View<MudPivotFieldList, PivotFieldListContext>(context);

    /// <summary>The report filter band (<see cref="MudPivotReportFilters"/>).</summary>
    public RenderFragment? ReportFilters(PivotReportFiltersContext context) => View<MudPivotReportFilters, PivotReportFiltersContext>(context);

    /// <summary>The toolbar above the report (<see cref="MudPivotToolbar"/>): the report filter band,
    /// then Layout ▾, Refresh and the Field List's toggle as <c>MudButton</c>s, and a refusal as an
    /// error <c>MudAlert</c> (ADR-0061).</summary>
    public RenderFragment? Toolbar(PivotToolbarContext context) => View<MudPivotToolbar, PivotToolbarContext>(context);

    /// <summary>A placed field's menu, and the toolbar's Layout menu (<see cref="MudPivotMenu"/>).</summary>
    public RenderFragment? Menu(PivotMenuContext context) => View<MudPivotMenu, PivotMenuContext>(context);

    /// <summary>Filter… (<see cref="MudPivotItemFilter"/>).</summary>
    public RenderFragment? ItemFilter(PivotItemFilterContext context) => View<MudPivotItemFilter, PivotItemFilterContext>(context);

    /// <summary>Field Settings… (<see cref="MudPivotFieldSettings"/>).</summary>
    public RenderFragment? FieldSettings(PivotFieldSettingsContext context) => View<MudPivotFieldSettings, PivotFieldSettingsContext>(context);

    /// <summary>Value Field Settings… (<see cref="MudPivotValueFieldSettings"/>).</summary>
    public RenderFragment? ValueFieldSettings(PivotValueFieldSettingsContext context)
        => View<MudPivotValueFieldSettings, PivotValueFieldSettingsContext>(context);

    /// <summary>Show Details' tabs at the report's foot (<see cref="MudPivotDetailsTabs"/>):
    /// <c>MudTabs</c> placed at the bottom, bound to the tab ExPivot selects (ADR-0059).</summary>
    public RenderFragment? DetailsTabs(PivotDetailsTabsContext context) => View<MudPivotDetailsTabs, PivotDetailsTabsContext>(context);

    /// <summary>The content of Show Details' dialog, inside ExPivot's frame
    /// (<see cref="MudPivotDetailsDialog"/>): the cell's title, the records and a Close
    /// <c>MudButton</c> (ADR-0059).</summary>
    public RenderFragment? DetailsDialog(PivotDetailsDialogContext context) => View<MudPivotDetailsDialog, PivotDetailsDialogContext>(context);

    /// <summary>The Stale Report's notice (<see cref="MudPivotStaleReport"/>): a warning
    /// <c>MudAlert</c> with a Retry <c>MudButton</c> (ADR-0067).</summary>
    public RenderFragment? StaleReport(PivotStaleReportContext context) => View<MudPivotStaleReport, PivotStaleReportContext>(context);

    /// <summary>A command's icon: <see cref="Icon"/> first, then ExPivot's Material icons; null
    /// leaves the grid's own commands to the grid Wrapper's.</summary>
    internal string? IconFor(string id) => Icon?.Invoke(id) ?? MudPivotIcons.ForCommand(id);

    private RenderFragment View<TView, TContext>(TContext context) where TView : IComponent
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<TView>(0);
            builder.AddComponentParameter(1, "Context", context);
            builder.AddComponentParameter(2, "Chrome", this);
            builder.CloseComponent();
        };
    }
}
