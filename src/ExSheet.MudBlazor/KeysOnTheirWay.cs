using Microsoft.AspNetCore.Components.Web;

namespace ExSheet.MudBlazor;

/// <summary>
/// The keys typed while the keyboard is on its way into Format Cells' dialog (ADR-0010's hold, as a
/// Chrome keeps it): on a circuit the dialog is drawn, and its tabs take the keyboard, round trips
/// after the gesture that opened it, and meanwhile the keyboard waits on the frame's own element.
/// The keys typed there are held, in order, and handed to the tab shown once the tabs hold the
/// keyboard; a key typed after that goes straight on.
///
/// <para>As the core's hold does, a key whose effect would be the browser's own — Tab moving DOM
/// focus — is dropped, with every key held behind it: it cannot be reproduced without script
/// (ADR-0021), and the keys after it were meant for wherever it would have gone.</para>
/// </summary>
internal sealed class KeysOnTheirWay
{
    private readonly List<KeyboardEventArgs> _held = [];
    private Func<KeyboardEventArgs, Task<bool>>? _hand;
    private bool _dropping;

    /// <summary>A key typed on the frame's own element: held until the tabs hold the keyboard, or handed on now if they do.</summary>
    public async Task TypedAsync(KeyboardEventArgs key)
    {
        if (_dropping) return;
        if (_hand is null)
        {
            _held.Add(key);
            return;
        }
        if (!await _hand(key)) _dropping = true;
    }

    /// <summary>
    /// The tabs hold the keyboard: the held keys are handed on, in order, to <paramref name="hand"/>,
    /// which answers false for a key that ends the handing on — Tab, or Escape, which closes the dialog.
    /// </summary>
    public async Task ArrivedAsync(Func<KeyboardEventArgs, Task<bool>> hand)
    {
        _hand = hand;
        while (_held.Count > 0 && !_dropping)
        {
            var key = _held[0];
            _held.RemoveAt(0);
            if (!await hand(key)) _dropping = true;
        }
        _held.Clear();
    }
}
