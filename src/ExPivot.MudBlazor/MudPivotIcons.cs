using ExPivot.Engine;
using global::MudBlazor;

namespace ExPivot.MudBlazor;

/// <summary>The Material icons of ExPivot's commands and Areas (ADR-0061).</summary>
public static class MudPivotIcons
{
    /// <summary>A command's icon, by its id (<see cref="PivotCommandIds"/>), or null.</summary>
    public static string? ForCommand(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (PivotCommandIds.CaptionOfRemove(id) is not null)
            return Icons.Material.Filled.Close;
        return id switch
        {
            PivotCommandIds.MoveUp => Icons.Material.Filled.ArrowUpward,
            PivotCommandIds.MoveDown => Icons.Material.Filled.ArrowDownward,
            PivotCommandIds.MoveToBeginning => Icons.Material.Filled.VerticalAlignTop,
            PivotCommandIds.MoveToEnd => Icons.Material.Filled.VerticalAlignBottom,
            PivotCommandIds.MoveToFilters => Icons.Material.Filled.FilterList,
            PivotCommandIds.MoveToRows => Icons.Material.Filled.TableRows,
            PivotCommandIds.MoveToColumns => Icons.Material.Filled.ViewColumn,
            PivotCommandIds.MoveToValues => Icons.Material.Filled.Functions,
            PivotCommandIds.RemoveField => Icons.Material.Filled.Close,
            PivotCommandIds.FieldSettings or PivotCommandIds.ValueFieldSettings => Icons.Material.Filled.Settings,
            PivotCommandIds.SortAscending or PivotCommandIds.SortSmallestToLargest => Icons.Material.Filled.ArrowUpward,
            PivotCommandIds.SortDescending or PivotCommandIds.SortLargestToSmallest => Icons.Material.Filled.ArrowDownward,
            PivotCommandIds.FilterItems => Icons.Material.Filled.FilterAlt,
            PivotCommandIds.ExpandField or PivotCommandIds.Expand => Icons.Material.Filled.UnfoldMore,
            PivotCommandIds.CollapseField or PivotCommandIds.Collapse => Icons.Material.Filled.UnfoldLess,
            PivotCommandIds.ShowDetails => Icons.Material.Filled.ManageSearch,
            PivotCommandIds.ShowFieldList or PivotCommandIds.HideFieldList or PivotCommandIds.FieldListToggle => Icons.Material.Filled.ViewSidebar,
            PivotCommandIds.LayoutMenu => Icons.Material.Filled.ViewQuilt,
            PivotCommandIds.Refresh or PivotCommandIds.Retry => Icons.Material.Filled.Refresh,
            _ => null,
        };
    }

    /// <summary>An Area's icon.</summary>
    public static string ForArea(PivotArea area) => area switch
    {
        PivotArea.Filters => Icons.Material.Filled.FilterList,
        PivotArea.Columns => Icons.Material.Filled.ViewColumn,
        PivotArea.Rows => Icons.Material.Filled.TableRows,
        _ => Icons.Material.Filled.Functions,
    };
}
