using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExPivot.MudBlazor;

/// <summary>
/// A MudBlazor button of the pivot's in its Input Order (ADR-0240 section 6), as ExPivot's built-in
/// buttons are: it claims its click and its keys (<c>data-ex-claims</c>), so the report grid numbers
/// each, holds the keys typed behind one still being answered, and hands them on where its answer left
/// the keyboard. The press runs through the context's <c>InOrder</c>. Enter, and Space but for its
/// repeats, press it at the keydown, in that key's turn, so a key the grid hands on — a keydown
/// dispatched from script, which activates no button — presses it too; the click the browser then makes
/// of the same key (detail 0) is that key's, and does nothing. The button says so
/// (<c>data-ex-key-presses</c>), and the grid does not number that click (ADR-0362). Any other key is ExPivot's
/// frame's around the button, if any, in the same turn (Escape closes a menu), through the frame's
/// cascaded handler: the turn then ends with the keyboard where the frame's answer left it, and the keys
/// typed behind it go there. A MudBlazor component cannot stop a key's propagation, so the frame hears
/// the key again after, and finds nothing left to close.
/// </summary>
internal sealed class MudPivotPress
{
    private readonly EventCallback<MouseEventArgs> _click;
    private readonly EventCallback<KeyboardEventArgs> _keyDown;
    private readonly Func<bool> _disabled;
    private EventCallback<MouseEventArgs> _pressed;
    // The clicks the browser owes for the keys that pressed the button at their keydown.
    private int _keyClicks;

    internal MudPivotPress(global::MudBlazor.MudBaseButton button)
    {
        _click = EventCallback.Factory.Create<MouseEventArgs>(button, OnClickAsync);
        _keyDown = EventCallback.Factory.Create<KeyboardEventArgs>(button, OnKeyDownAsync);
        _disabled = () => button.Disabled;
    }

    /// <summary>The context's seam into the pivot's Input Order; null leaves the button as MudBlazor's.</summary>
    internal Func<GridInputKind, Func<Task>, Task>? InOrder { get; set; }

    /// <summary>ExPivot's frame's key handler around the button, if any.</summary>
    internal Func<KeyboardEventArgs, Task>? FrameKeys { get; set; }

    /// <summary>
    /// The button's <c>OnClick</c> as its parent gave it, made ordered: the press it stands for is kept,
    /// and the button's own click and keys are claimed in <paramref name="attributes"/>. Returns what the
    /// button's <c>OnClick</c> becomes.
    /// </summary>
    internal EventCallback<MouseEventArgs> Order(EventCallback<MouseEventArgs> onClick, Dictionary<string, object?> attributes)
    {
        if (!onClick.Equals(_click))
            _pressed = onClick;
        if (InOrder is null)
        {
            attributes.Remove("data-ex-claims");
            attributes.Remove("data-ex-key-presses");
            attributes.Remove("onkeydown");
            return _pressed;
        }
        attributes["data-ex-claims"] = "click keydown";
        attributes["data-ex-key-presses"] = true;
        attributes["onkeydown"] = _keyDown;
        return _click;
    }

    private Task InOrderAsync(GridInputKind kind, Func<Task> handling)
        => InOrder is { } inOrder ? inOrder(kind, handling) : handling();

    private Task PressAsync(MouseEventArgs e) => _disabled() ? Task.CompletedTask : _pressed.InvokeAsync(e);

    private Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        var press = e is { Key: "Enter" or " ", CtrlKey: false, AltKey: false, MetaKey: false, ShiftKey: false } && !(e.Key == " " && e.Repeat);
        if (press)
        {
            // The key owes one click, made at its keydown (Enter) or its keyup (Space); a key typed
            // between them, a Shift while Space is held, leaves it owed. A press held and replayed
            // makes none, and the click it leaves owed is taken by the next click a key or nothing
            // pointed makes on this button (ADR-0240's trade-offs).
            _keyClicks = 1;
            return InOrderAsync(GridInputKind.KeyDown, () => PressAsync(new MouseEventArgs()));
        }
        var frame = FrameKeys;
        return InOrderAsync(GridInputKind.KeyDown, () => frame is null ? Task.CompletedTask : frame(e));
    }

    private Task OnClickAsync(MouseEventArgs e)
    {
        if (e.Detail == 0 && _keyClicks > 0)
        {
            _keyClicks--;
            return Task.CompletedTask;
        }
        _keyClicks = 0;
        return InOrderAsync(GridInputKind.Click, () => PressAsync(e));
    }
}
