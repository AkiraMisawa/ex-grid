using ExGrid.Clipboard;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

public partial class ExGrid<TRow>
{
    /// <summary>
    /// Answers an approved copy asynchronously (ADR-0152). The copy event defers to the existing
    /// asynchronous clipboard route, even when its cells are in the Window. The Consumer captures
    /// what the copy reads — ExPivot, the Report Version on screen — before awaiting, and returns
    /// both clipboard flavours, or a named refusal; the grid never substitutes the current Window
    /// for missing data. The normal selection, shape and cap rules apply before this is called. A
    /// synchronous <see cref="CopyAnswer"/> takes precedence when both are supplied.
    /// </summary>
    [Parameter] public Func<GridCopyRequest, CancellationToken, Task<GridCopyAnswer>>? CopyAnswerAsync { get; set; }
}
