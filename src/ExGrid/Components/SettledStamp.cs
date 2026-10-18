using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

/// <summary>
/// The core's half of the Settled Mark (ADR-0220), written where the script reads it: one empty
/// element, the root's own first child, carrying the last input answered (<c>data-ex-answered</c>) and
/// whether a render waits on one of the grid's own timers (<c>data-ex-owing</c>), and whether an Escape on
/// the root would now leave the grid (<c>data-ex-escape-leaves</c>, ADR-0230 section 1). A component of its
/// own, so that saying an input was answered renders this element and nothing else: rendered by the
/// root, each input cost two renders of the whole grid, which the measurement of 2026-10-16 saw on
/// WebAssembly (a burst of 40 keys, 63 ms without the mark and 151 ms with it). It takes no
/// parameter, so the root's renders never render it. It wears no class: the script finds it by its
/// attribute, and every class under the root is one ADR-0029 names (UX-4).
/// </summary>
internal sealed class SettledStamp : ComponentBase
{
    private int _answered;
    private bool _owing;
    private bool _escapeLeaves;

    /// <summary>Says <paramref name="answered"/>, <paramref name="owing"/> and
    /// <paramref name="escapeLeaves"/>, rendering only when one of them changed.</summary>
    internal void Show(int answered, bool owing, bool escapeLeaves)
    {
        if (answered == _answered && owing == _owing && escapeLeaves == _escapeLeaves)
            return;
        _answered = answered;
        _owing = owing;
        _escapeLeaves = escapeLeaves;
        StateHasChanged();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(2, "hidden", true);
        builder.AddAttribute(3, "data-ex-answered", _answered.ToString(CultureInfo.InvariantCulture));
        if (_owing)
            builder.AddAttribute(4, "data-ex-owing", "");
        if (_escapeLeaves)
            builder.AddAttribute(5, "data-ex-escape-leaves", "");
        builder.CloseElement();
    }
}
