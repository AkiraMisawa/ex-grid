using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ExSheet.MudBlazor;

// The picker keeps MudBlazor's colour controls. Its opener is a button: a read-only text
// field would submit Format Cells on an Enter typed before its opening render arrives.
internal sealed class MudMoreColours : MudColorPicker
{
    [Parameter, EditorRequired] public FormatCellsContext Context { get; set; } = default!;
    [Parameter] public Action<MudMoreColours, bool>? PopupChanged { get; set; }

    private ElementReference _entry;
    private ElementReference _popup;
    private List<KeyboardEventArgs>? _keys;
    private Task _holding = Task.CompletedTask;
    private Task _close = Task.CompletedTask;
    private bool _closing;
    private bool _popupOpen;
    private bool _keepPopupDrawn;
    private bool _disposed;
    private long _opening;
    private readonly EventCallback<KeyboardEventArgs> _entryKey;
    private readonly EventCallback<KeyboardEventArgs> _popupKey;
    private readonly EventCallback<MouseEventArgs> _open;
    private readonly RenderFragment _input;
    private readonly RenderFragment _contents;
    private readonly RenderFragment _label;
    private readonly RenderFragment _button;

    public MudMoreColours()
    {
        // HEX on every opening (OpenFromButtonAsync), and never as a parameter: a parameter is set
        // again by each render of the dialog, and took back the mode the user had switched to
        // whenever that render came after the switch (ticket 260).
        ColorPickerMode = ColorPickerMode.HEX;
        _entryKey = EventCallback.Factory.Create<KeyboardEventArgs>(this, EntryKeyAsync);
        _popupKey = EventCallback.Factory.Create<KeyboardEventArgs>(this, PopupKeyAsync);
        _open = EventCallback.Factory.Create<MouseEventArgs>(this, OpenFromButtonAsync);
        _label = builder => builder.AddContent(0, Label);
        _button = DrawButton;
        _input = DrawInput;
        _contents = builder => Wrap(builder, base.PickerContent, false);
    }

    protected override RenderFragment InputContent => _input;
    protected override RenderFragment PickerContent => _contents;

    private void DrawInput(RenderTreeBuilder builder) => Wrap(builder, _button, true);

    private void DrawButton(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MudButton>(0);
        builder.AddComponentParameter(1, nameof(MudButton.Variant), Variant.Outlined);
        builder.AddComponentParameter(2, nameof(MudButton.OnClick), _open);
        builder.AddComponentParameter(3, nameof(MudButton.ChildContent), _label);
        builder.AddComponentParameter(4, nameof(MudButton.ButtonType), ButtonType.Button);
        // The inherited picker registers its key interceptor for this class on first render.
        // Keep the replacement opener in that contract: Enter/Space must not also perform a
        // native action after their handler has handed the keyboard to the opening field.
        builder.AddComponentParameter(5, nameof(MudButton.Class), "mud-input-slot");
        builder.CloseComponent();
    }

    private void Wrap(RenderTreeBuilder builder, RenderFragment? content, bool entry)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", entry ? "mud-ex-sheet-colour-entry" : "mud-ex-sheet-colour-popup");
        builder.AddAttribute(2, "onkeydown", entry ? _entryKey : _popupKey);
        builder.AddEventStopPropagationAttribute(3, "onkeydown", true);
        builder.AddElementReferenceCapture(4, element =>
        {
            if (entry) _entry = element;
            else _popup = element;
        });
        builder.AddContent(5, content);
        builder.CloseElement();
    }

    private async Task OpenFromButtonAsync()
    {
        // An Enter typed on the opener while the popup is in flight belongs to HEX. Its
        // native button click must not toggle that popup closed behind the held Enter.
        if (Open || _closing || _disposed) return;
        var opening = _opening + 1;
        ColorPickerMode = ColorPickerMode.HEX;
        await OpenAsync();
        if (_opening == opening) await FocusAsync();
    }

    protected override async Task OnOpenedAsync()
    {
        var opening = ++_opening;
        _popupOpen = true;
        PopupChanged?.Invoke(this, true);
        _holding = HoldKeysAsync(_entry);
        await _holding;
        if (!Open || _disposed || opening != _opening) return;
        await base.OnOpenedAsync();
    }

    public override async ValueTask FocusAsync()
    {
        var opening = _opening;
        await _holding;
        if (opening == _opening && Open && !_closing && !_disposed && Context.TakeKeyboard is { } take)
            await take(_popup);
    }

    protected override Task OnClosedAsync()
    {
        // MudPicker's key interceptor calls CloseAsync for Escape/Tab even when its popup
        // is already closed. Such a key belongs to the enclosing dialog or the browser;
        // it must not start another hand-back behind that dialog's closing keys.
        if (_disposed || !_popupOpen) return base.OnClosedAsync();
        if (_closing)
        {
            // CloseAsync sets Open before calling this. A second outside press must not
            // remove the field while the first hand-back is still collecting its keys.
            Open = _keepPopupDrawn;
            return _close;
        }
        ++_opening;
        _closing = true;
        _keepPopupDrawn = true;
        // MudBlazor's outside press closes the popup without moving focus first. Keep its
        // input drawn until the hand-back leaves it: removing a focused input need not
        // raise blur, and would discard the held edit's commit notification (ADR-0210).
        Open = true;
        return _close = ClosePopupAsync();
    }

    private async Task ClosePopupAsync()
    {
        await HoldKeysAsync(_popup);
        _keepPopupDrawn = false;
        _popupOpen = false;
        PopupChanged?.Invoke(this, false);
        Open = false;
        if (_disposed)
        {
            _closing = false;
            return;
        }
        await base.OnClosedAsync();
        _closing = false;
        if (_disposed) return;
        StateHasChanged();
        // Complete this close before the returned button receives keys: a held Enter or
        // Space can open the next picker immediately, and this close cannot remove it.
        if (Context.TakeKeyboard is { } take) await take(_entry);
    }

    private Task EntryKeyAsync(KeyboardEventArgs key)
    {
        if (_keys is { } keys)
        {
            keys.Add(key);
            return Task.CompletedTask;
        }
        if (!Open && key is { Key: "Enter" or " ", CtrlKey: false, AltKey: false, MetaKey: false })
            return OpenFromButtonAsync();
        return Open ? PopupKeyAsync(key) : Task.CompletedTask;
    }

    private async Task PopupKeyAsync(KeyboardEventArgs key)
    {
        if (_closing)
        {
            _keys?.Add(key);
            return;
        }
        if (key is not { Key: "Escape", Repeat: false, CtrlKey: false, AltKey: false, ShiftKey: false, MetaKey: false }) return;
        // Keep the popup drawn until the keyboard and following keys have left it. The
        // opening button then hears the next Escape as Format Cells' own Cancel.
        await CloseAsync(false);
    }

    private Task HoldKeysAsync(ElementReference from)
    {
        var keys = new List<KeyboardEventArgs>();
        _keys = keys;
        IReadOnlyList<KeyboardEventArgs> TakeKeys()
        {
            // A delayed collection belongs to the opening that requested it, even if the
            // picker has since closed and opened again. It cannot clear the new queue.
            if (ReferenceEquals(_keys, keys)) _keys = null;
            return keys;
        }
        if (Context.HoldKeysForPopup is { } hold) return hold(from, TakeKeys);
        _keys = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _disposed = true;
        PopupChanged?.Invoke(this, false);
        await base.DisposeAsyncCore();
    }
}
