using ExGrid.Chrome;
using ExGrid.MudBlazor;
using ExPivot.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExPivot.MudBlazor;

/// <summary>
/// ExPivot's Chrome under MudBlazor (ADR-0061): the PivotTable Fields pane, a placed field's menu,
/// Filter…, Field Settings…, Value Field Settings… and the report filter band drawn with
/// MudBlazor's own controls, and the report grid dressed by <see cref="MudGridChrome"/> — so one
/// parameter dresses both.
///
/// <para>It draws what ExPivot hands it and calls back. Which commands there are, what a drop
/// means, the drafts, and where a menu or panel opens and what closes it are ExPivot's; swapping
/// this Chrome for the built-in markup changes no behaviour (ADR-0060). A <c>MudSelect</c>'s list
/// is an Inner Popup of its panel, reported so that Escape closes the list first (ADR-0039). The
/// words are ExPivot's, by id: one <c>Label</c> written on ExPivot words the pane, its panels and
/// the report's Context Menu alike.</para>
///
/// <para>The Layout menu is a menu, so it is drawn by <see cref="MudPivotMenu"/>, with the group
/// headings and the marked choice ExPivot hands it. The toolbar above the report, Show Details'
/// tabs and the content of its dialog are not drawn here yet: those members are left at null, so
/// ExPivot draws its built-in markup for them, the report filter band inside the toolbar drawn by
/// <see cref="MudPivotReportFilters"/> all the same.</para>
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
    /// in ExPivot's words, which the grid Wrapper would otherwise paint as their ids (ADR-0060).</summary>
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

    /// <summary>A placed field's menu (<see cref="MudPivotMenu"/>).</summary>
    public RenderFragment? Menu(PivotMenuContext context) => View<MudPivotMenu, PivotMenuContext>(context);

    /// <summary>Filter… (<see cref="MudPivotItemFilter"/>).</summary>
    public RenderFragment? ItemFilter(PivotItemFilterContext context) => View<MudPivotItemFilter, PivotItemFilterContext>(context);

    /// <summary>Field Settings… (<see cref="MudPivotFieldSettings"/>).</summary>
    public RenderFragment? FieldSettings(PivotFieldSettingsContext context) => View<MudPivotFieldSettings, PivotFieldSettingsContext>(context);

    /// <summary>Value Field Settings… (<see cref="MudPivotValueFieldSettings"/>).</summary>
    public RenderFragment? ValueFieldSettings(PivotValueFieldSettingsContext context)
        => View<MudPivotValueFieldSettings, PivotValueFieldSettingsContext>(context);

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
