namespace ExGrid.Docs.Examples.Sheet;

using ExGrid.Chrome;
using ExSheet;
using Microsoft.AspNetCore.Components;

/// <summary>
/// A Chrome of your own that draws the toolbar's lists — Number Format, Borders, the colours — in a panel
/// beside the Sheet, outside it, and leaves every other part to the built-in Chrome. The panel is on the
/// Sheet's seam (ADR-0240 section 6): it joins the Sheet's Input Order while open, takes the keyboard
/// through the Sheet, puts its keys and presses in their turn, and tells the toolbar when it closes.
/// </summary>
public sealed class OwnListChrome : ISheetChrome
{
    /// <summary>The item whose list is open, as last described; null while none is.</summary>
    public ToolbarItemContext? Open { get; private set; }

    /// <summary>Whether the list was opened from the toolbar's keys, and so takes the keyboard.</summary>
    public bool FromKeys { get; private set; }

    /// <summary>Raised when the list opens, closes, or its item is described again.</summary>
    public event Action? Changed;

    /// <inheritdoc />
    public RenderFragment? ToolbarItem(ToolbarItemContext context)
    {
        if (context.Kind is not (ToolbarItemKind.Menu or ToolbarItemKind.Split)) return null;
        // The item is described again on every render: the open list follows its latest context.
        if (Open?.Id == context.Id && !ReferenceEquals(Open, context))
        {
            Open = context;
            Changed?.Invoke();
        }
        return builder =>
        {
            builder.OpenComponent<OwnListButton>(0);
            builder.AddComponentParameter(1, nameof(OwnListButton.Context), context);
            builder.AddComponentParameter(2, nameof(OwnListButton.Chrome), this);
            builder.CloseComponent();
        };
    }

    private TaskCompletionSource? _joining;

    /// <summary>
    /// Opens <paramref name="context"/>'s list in the panel. The task ends once the list has joined the
    /// Sheet's Input Order (<see cref="Joined"/>) or has closed: a press that opens it waits for that in its
    /// turn, so whatever comes after the press is heard by the list.
    /// </summary>
    public Task OpenList(ToolbarItemContext context, bool fromKeys)
    {
        _joining?.TrySetResult();
        var joining = _joining = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Open = context;
        FromKeys = fromKeys;
        Changed?.Invoke();
        return joining.Task;
    }

    /// <summary>The panel's list has joined the Sheet's Input Order.</summary>
    public void Joined()
    {
        _joining?.TrySetResult();
        _joining = null;
    }

    /// <summary>Closes the list, if <paramref name="id"/>'s is the one open.</summary>
    public void CloseList(string id)
    {
        if (Open?.Id != id) return;
        Joined();
        Open = null;
        Changed?.Invoke();
    }

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
