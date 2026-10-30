using ExGrid.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Chrome;

/// <summary>
/// ExPivot's Chrome (ADR-0061, ADR-0410): what draws the Field List, the Pivot Toolbar above the report with
/// its report filter band, a menu — a placed field's, and the Layout menu — the three panels, Show
/// Details' tabs and dialog, and the Stale Report's notice, in the slots the Script Core leaves to Razor. Each member
/// returns the content to draw, or null for the Script Core's own. It renders and calls back; the rules, the
/// state and the frames are ExPivot's, so swapping it changes no behaviour.
/// </summary>
public interface IPivotChrome
{
    /// <summary>
    /// The report grid's Chrome, so that one Chrome dresses both (ADR-0061); a <c>Chrome</c>
    /// written on ExPivot beats it. Null for ExGrid's built-in. ExPivot asks once per Pivot
    /// Chrome and holds the answer, so a new instance per call is right here.
    /// </summary>
    /// <param name="commandLabel">ExPivot's word for a command of its Context Menu — the
    /// Consumer's through ExPivot's <c>Label</c>, then ExPivot's English — or null for an id that
    /// is not ExPivot's. A grid Chrome that words its menus itself never consults the grid's
    /// <c>CommandLabel</c>, so it asks this first.</param>
    IGridChrome? GridChrome(Func<string, string?> commandLabel) => null;

    /// <summary>The stylesheets this Chrome brings (ADR-0123): a Wrapper's own, each as its shipped
    /// stylesheet's notice rules name it, with the stamp it was built with. ExPivot's root renders a
    /// notice for each beside its own, which only another version's stylesheet shows. The report
    /// grid's Chrome (<see cref="GridChrome"/>) declares its own, which the grid renders. Empty for
    /// the built-in markup, which brings none. Read on every render of the root: answer the same list
    /// each time.</summary>
    IReadOnlyList<ChromeStylesheet> Stylesheets => [];

    /// <summary>The pivot's slots this Chrome fills with Razor when the Script Core draws the pivot (ADR-0410): each
    /// is asked for through its view overload. <see cref="PivotScriptSlots.None"/>, the default, leaves every part
    /// the Script Core's own. The report grid's are its grid Chrome's <c>ScriptSlots</c>. Read once, as the pivot
    /// attaches: answer the same each time.</summary>
    PivotScriptSlots ScriptSlots => PivotScriptSlots.None;

    /// <summary>A placed field's menu or the Layout menu when the Script Core draws the pivot (ADR-0410), for a
    /// Chrome that names <see cref="PivotScriptSlots.Menu"/>: one control per item, each marked with
    /// <see cref="PivotScriptSlotAttributes.MenuItem"/>, and no handler of its own.</summary>
    RenderFragment? Menu(PivotMenuView view) => null;

    /// <summary>The Pivot Toolbar when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.Toolbar"/>: its buttons and anchors marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? Toolbar(PivotToolbarView view) => null;

    /// <summary>The Stale Report's notice when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.StaleReport"/>: the sentence and Retry, marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? StaleReport(PivotStaleReportView view) => null;

    /// <summary>The report filter band when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.ReportFilters"/>: each filter's button and anchor marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? ReportFilters(PivotReportFiltersView view) => null;

    /// <summary>Filter… when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.ItemFilter"/>: its controls marked with <see cref="PivotScriptSlotAttributes"/>,
    /// and no handler of its own.</summary>
    RenderFragment? ItemFilter(PivotItemFilterView view) => null;

    /// <summary>Field Settings… when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.FieldSettings"/>: its controls marked with <see cref="PivotScriptSlotAttributes"/>,
    /// and no handler of its own.</summary>
    RenderFragment? FieldSettings(PivotFieldSettingsView view) => null;

    /// <summary>Value Field Settings… when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.ValueFieldSettings"/>: its controls marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? ValueFieldSettings(PivotValueFieldSettingsView view) => null;

    /// <summary>The PivotTable Fields pane when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.FieldList"/>: its fields, Areas and controls marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own, the drags' included.</summary>
    RenderFragment? FieldList(PivotFieldListView view) => null;

    /// <summary>Show Details' sheet tabs when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.DetailsTabs"/>: each tab and close marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? DetailsTabs(PivotDetailsTabsView view) => null;

    /// <summary>Show Details' dialog when the Script Core draws the pivot (ADR-0410), for a Chrome that names
    /// <see cref="PivotScriptSlots.DetailsDialog"/>: its title, the records' anchor and Close marked with
    /// <see cref="PivotScriptSlotAttributes"/>, and no handler of its own.</summary>
    RenderFragment? DetailsDialog(PivotDetailsDialogView view) => null;
}
