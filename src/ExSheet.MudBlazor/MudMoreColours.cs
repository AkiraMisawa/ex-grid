using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ExSheet.MudBlazor;

// The picker keeps MudBlazor's colour controls. Its opener is a button: a read-only text
// field would submit Format Cells on an Enter typed before its opening render arrives.
//
// In the Sheet's Input Order (ADR-0240 section 6, ticket 06): the opener claims its click, and Enter and
// Space at their keydown, and opens in that input's turn, telling the Sheet that the keyboard is on its
// way to the popup (FormatCellsContext.HandKeyboardToFrame), so the keys typed meanwhile are held and
// typed into HEX, in order, once it takes the keyboard (TakeKeyboard). The popup, drawn by MudBlazor at the
// page's body, joins the Sheet's order while it is open (JoinSurface): its Escape closes it in its turn,
// giving the keyboard back to the opener before it goes, and the keys typed meanwhile go there after it.
internal sealed class MudMoreColours : MudColorPicker
{
    [Parameter, EditorRequired] public FormatCellsContext Context { get; set; } = default!;
    [Parameter] public Action<MudMoreColours, bool>? PopupChanged { get; set; }

    private ElementReference _entry;
    private ElementReference _popup;
    private IAsyncDisposable? _surface;
    private Task? _joining;
    private Task _close = Task.CompletedTask;
    private bool _closing;
    private bool _popupOpen;
    private bool _keepPopupDrawn;
    private bool _disposed;
    private long _opening;
    private readonly EventCallback<KeyboardEventArgs> _entryKey;
    private readonly EventCallback<MouseEventArgs> _open;
    private readonly Func<Task> _openFromButton;
    private readonly Func<Task> _escape;
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
        _openFromButton = OpenFromButtonAsync;
        _escape = CloseInTurnAsync;
        _open = EventCallback.Factory.Create<MouseEventArgs>(this, () => InOrder(GridInputKind.Click, _openFromButton));
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
        // native action after their handler has opened the popup.
        builder.AddComponentParameter(5, nameof(MudButton.Class), "mud-input-slot");
        builder.CloseComponent();
    }

    private void Wrap(RenderTreeBuilder builder, RenderFragment? content, bool entry)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", entry ? "mud-ex-sheet-colour-entry" : "mud-ex-sheet-colour-popup");
        if (entry)
        {
            // The opener's click, and its Enter and Space, which open the popup (OpenFromButtonAsync).
            builder.AddAttribute(2, "data-ex-claims", "click keydown:Enter keydown:Space");
            builder.AddAttribute(3, "onkeydown", _entryKey);
        }
        builder.AddEventStopPropagationAttribute(4, "onkeydown", true);
        builder.AddElementReferenceCapture(5, element =>
        {
            if (entry) _entry = element;
            else _popup = element;
        });
        builder.AddContent(6, content);
        builder.CloseElement();
    }

    private Task InOrder(GridInputKind kind, Func<Task> handling) => Context.InOrder is { } inOrder ? inOrder(kind, handling) : handling();

    // Enter or Space on the opener, claimed at its keydown: it opens the popup in its turn. Any other key
    // there is the dialog's, or the picker's own.
    private Task EntryKeyAsync(KeyboardEventArgs key)
    {
        if (key.Key is not ("Enter" or " ")) return Task.CompletedTask;
        return InOrder(GridInputKind.KeyDown, () => key is { CtrlKey: false, AltKey: false, MetaKey: false } && !Open
            ? OpenFromButtonAsync() : Task.CompletedTask);
    }

    private async Task OpenFromButtonAsync()
    {
        // An Enter typed on the opener while the popup is in flight belongs to HEX. Its
        // native button click must not toggle that popup closed behind the held Enter.
        if (Open || _closing || _disposed) return;
        var opening = _opening + 1;
        ColorPickerMode = ColorPickerMode.HEX;
        // The keys typed from here until HEX has the keyboard are held for it.
        if (Context.HandKeyboardToFrame is { } handOff) await handOff(null);
        await OpenAsync();
        if (_opening == opening) await FocusAsync();
    }

    protected override async Task OnOpenedAsync()
    {
        ++_opening;
        _popupOpen = true;
        PopupChanged?.Invoke(this, true);
        await base.OnOpenedAsync();
    }

    public override async ValueTask FocusAsync()
    {
        var opening = _opening;
        if (!Open || _closing || _disposed) return;
        // The popup joins the Sheet's order before it takes the keyboard, so every key typed in it is heard.
        // MudPicker's own opening asks for focus too: both callers wait on the one join.
        if (_surface is null && Context.JoinSurface is { } join)
        {
            var joining = _joining ??= JoinAsync(join, opening);
            try
            {
                await joining;
            }
            finally
            {
                if (ReferenceEquals(_joining, joining)) _joining = null;
            }
            if (_surface is null) return;
        }
        if (opening != _opening || !Open || _closing || _disposed) return;
        if (Context.TakeKeyboard is { } take) await take(_popup);
    }

    private async Task JoinAsync(Func<ElementReference, string?, Func<Task>?, Task<IAsyncDisposable>> join, long opening)
    {
        var surface = await join(_popup, null, _escape);
        if (opening != _opening || !Open || _closing || _disposed) await surface.DisposeAsync();
        else _surface = surface;
    }

    // The popup's Escape, in its turn: it closes, the keyboard back on the opener first.
    private Task CloseInTurnAsync() => _popupOpen && !_closing && !_disposed ? CloseAsync(false) : Task.CompletedTask;

    protected override Task OnClosedAsync()
    {
        // MudPicker's key interceptor calls CloseAsync for Escape/Tab even when its popup
        // is already closed. Such a key belongs to the enclosing dialog or the browser;
        // it must not start another hand-back.
        if (_disposed || !_popupOpen) return base.OnClosedAsync();
        if (_closing)
        {
            // CloseAsync sets Open before calling this. A second outside press must not
            // remove the field while the first hand-back is still under way.
            Open = _keepPopupDrawn;
            return _close;
        }
        ++_opening;
        _closing = true;
        _keepPopupDrawn = true;
        // Keep the popup drawn until the keyboard has left it: removing a focused input need not
        // raise blur, and would discard the held edit's commit notification (ADR-0210), and DOM focus
        // would fall to the page's body, where no listener of the Sheet's hears a key.
        Open = true;
        return _close = ClosePopupAsync();
    }

    private async Task ClosePopupAsync()
    {
        // The keyboard goes back to the opener while the popup still stands: from the popup, which is the
        // Sheet's, the hand-off is granted, and a key typed after the close goes to the opener, in order.
        if (Context.TakeKeyboard is { } take) await take(_entry);
        _keepPopupDrawn = false;
        _popupOpen = false;
        PopupChanged?.Invoke(this, false);
        Open = false;
        if (_surface is { } surface)
        {
            _surface = null;
            await surface.DisposeAsync();
        }
        if (_disposed)
        {
            _closing = false;
            return;
        }
        await base.OnClosedAsync();
        _closing = false;
        if (_disposed) return;
        StateHasChanged();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _disposed = true;
        PopupChanged?.Invoke(this, false);
        if (_surface is { } surface)
        {
            _surface = null;
            await surface.DisposeAsync();
        }
        await base.DisposeAsyncCore();
    }
}
