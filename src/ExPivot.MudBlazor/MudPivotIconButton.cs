using System.ComponentModel;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExPivot.MudBlazor;

/// <summary>
/// A <c>MudIconButton</c> in the pivot's Input Order (ADR-0240 section 6), as <see cref="MudPivotButton"/>
/// is: its press runs through <see cref="InOrder"/>, the context's seam, and Enter and Space press it at
/// their keydown.
///
/// <para>Not the contract (ADR-0201): the Wrapper's own parts place it, and no Consumer does. It is
/// public only because Razor places in markup only a public component, is hidden from a Consumer's
/// editor, and may change in any version.</para>
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MudPivotIconButton : global::MudBlazor.MudIconButton
{
    private readonly MudPivotPress _press;

    /// <summary>Creates the button.</summary>
    public MudPivotIconButton() => _press = new MudPivotPress(this);

    /// <summary>The context's seam into the pivot's Input Order; null leaves the button as MudBlazor's.</summary>
    [Parameter] public Func<GridInputKind, Func<Task>, Task>? InOrder { get; set; }

    /// <summary>ExPivot's frame's key handler around the button, cascaded by the frame.</summary>
    [CascadingParameter(Name = global::ExPivot.Components.PivotPopupFrame.KeysName)]
    internal Func<KeyboardEventArgs, Task>? FrameKeys { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _press.InOrder = InOrder;
        _press.FrameKeys = FrameKeys;
        OnClick = _press.Order(OnClick, UserAttributes);
    }
}
