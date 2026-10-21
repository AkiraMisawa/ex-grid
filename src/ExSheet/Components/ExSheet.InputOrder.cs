using ExGrid.Chrome;

namespace ExSheet.Components;

/// <summary>
/// The Sheet's own surfaces in its grid's Input Order (ADR-0240 section 6): the Sheet's box is the Input
/// Root, and the toolbar's band and buttons, the Sheet Tabs, Format Cells' OK and Cancel and the keyboard
/// crossing the Sheet's edge put their input in the grid's order through its seam
/// (<c>ExGrid.InOrderAsync</c>).
/// </summary>
public partial class ExSheet
{
    /// <summary>
    /// <paramref name="handling"/>, an input of the Sheet's own of <paramref name="kind"/>, in its place in
    /// the grid's Input Order. The one place the Sheet reaches the seam from: with no grid yet there is
    /// nothing to order it among, and it is handled at once.
    /// </summary>
    internal Task InOrderAsync(GridInputKind kind, Func<Task> handling)
        => InOrderAsync(_grid is { } grid ? grid.InOrderAsync : null, kind, handling);

    /// <summary><see cref="InOrderAsync(GridInputKind, Func{Task})"/> for work that waits on nothing.</summary>
    internal Task InOrderAsync(GridInputKind kind, Action handling) => InOrderAsync(kind, () =>
    {
        handling();
        return Task.CompletedTask;
    });

    /// <summary>
    /// <paramref name="handling"/> through the order's seam <paramref name="inOrder"/> — the grid's, or
    /// the one a Chrome's context hands its contents (<see cref="GridPopoverContext.InOrder"/>) — or at
    /// once where there is none, as in a context the grid did not make.
    /// </summary>
    internal static Task InOrderAsync(Func<GridInputKind, Func<Task>, Task>? inOrder, GridInputKind kind, Func<Task> handling)
        => inOrder is null ? handling() : inOrder(kind, handling);
}
