namespace ExGrid.Docs.Examples.Sheet;

using ExGrid.Chrome;
using ExSheet;
using Microsoft.AspNetCore.Components;

/// <summary>
/// A Chrome of your own that draws the Sheet Toolbar's menus and colour controls as plain text buttons, each with a
/// list of its own, and leaves every other item and every other part to the built-in Chrome. It draws from each item's
/// view (<see cref="SheetToolbarItemView"/>) and marks its controls with <see cref="SheetScriptSlotAttributes"/>: what
/// a press, a choice and the list's keys do, and where the keyboard goes, are ExSheet's, in the browser.
/// </summary>
public sealed class OwnListChrome : ISheetChrome
{
    /// <inheritdoc />
    public SheetScriptSlots SheetScriptSlots => SheetScriptSlots.ToolbarItem;

    /// <inheritdoc />
    public RenderFragment? ToolbarItem(SheetToolbarItemView view) => builder =>
    {
        builder.OpenComponent<OwnListButton>(0);
        builder.AddComponentParameter(1, nameof(OwnListButton.View), view);
        builder.CloseComponent();
    };

    // Every other part is the built-in Chrome's.
    /// <inheritdoc />
    public RenderFragment? FilterPanel(FilterPanelContext context) => null;

    /// <inheritdoc />
    public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

    /// <inheritdoc />
    public RenderFragment? CellEditor(CellEditorContext context) => null;

    /// <inheritdoc />
    public RenderFragment? LoadingIndicator(LoadingContext context) => null;
}
