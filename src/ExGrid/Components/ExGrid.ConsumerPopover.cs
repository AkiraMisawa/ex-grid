using System.Globalization;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// A Consumer's popover (ADR-0050, item 16): the Consumer's own contents in the grid's popover
// frame. The frame is every popover's — placed inside the grid's box and bounded by it, keyed by
// its opening, dismissed by Escape, by a pointer-down elsewhere in the instance and by the box
// shrinking below one row (ADR-0010/0039/0040) — and the core holds no reference to anything the
// contents render: they take DOM focus themselves when their context's count asks. Nothing of it
// stands until the Consumer opens one, so a grid whose Consumer never does is unchanged (DC-1).
public partial class ExGrid<TRow>
{
    // The Consumer's popover while it stands: its contents and its accessible name.
    private ConsumerPopover? _consumerPopover;

    // The requests for the contents' first control (each opening, and Tab wrapping back to it)
    // and for their last (Shift+Tab wrapping back to it, counted from zero at each opening).
    private int _consumerFocusRequest;
    private int _consumerFocusLastRequest;

    private sealed record ConsumerPopover(RenderFragment<GridPopoverContext> Content, string Label);

    /// <summary>
    /// Shows a Consumer's own contents in the grid's popover frame (ADR-0050, item 16). The frame
    /// stands under the header band, centred across the grid and inside its box, and is bounded by
    /// the box: contents taller or wider than the room scroll inside it (ADR-0040). It is a popover
    /// by every popover rule: it replaces any popover that stands, the contents take the keyboard
    /// through <see cref="GridPopoverContext.FocusRequest"/> (ADR-0039), Tab and Shift+Tab wrap
    /// inside it, and Escape, a pointer-down elsewhere in the instance and the box shrinking below
    /// one row close it as a Cancel (ADR-0010/0040). However it closes, the keyboard returns to the
    /// root. Calling it again opens new contents in place of the old.
    /// </summary>
    /// <param name="content">The contents, rendered with the frame's context.</param>
    /// <param name="label">The popover's accessible name, in the Consumer's words: the frame is a
    /// <c>dialog</c>, and the grid holds no sentence of its own (ADR-0036/0039).</param>
    /// <returns>Whether it opened: false while an edit is open, which a popover would otherwise
    /// take the keyboard from with its text uncommitted, and once the grid is disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty: a dialog that names
    /// nothing says nothing to assistive technology (ADR-0033).</exception>
    public async Task<bool> OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        // On the renderer's own context, as every request of the Consumer's is.
        var opened = false;
        await InvokeAsync(() => opened = OpenConsumerPopover(content, label));
        return opened;
    }

    /// <summary>
    /// Closes the Consumer's popover if it stands (ADR-0050, item 16), and the keyboard returns to
    /// the root, as however else it closes (ADR-0039). Another popover opened since is not the
    /// Consumer's, and stays.
    /// </summary>
    public Task ClosePopoverAsync()
        => InvokeAsync(() =>
        {
            if (_disposed || _consumerPopover is null)
                return;
            ClosePopoverAndReturnFocus();
            _suppressRender = false;
            StateHasChanged();
        });

    /// <summary>
    /// The core's focus function for a Chrome whose frame lies outside the grid (ADR-0010's note
    /// of 2026-09-30, ADR-0063): the keyboard goes back to the grid's root, granted only while it
    /// is still this grid's — DOM focus inside the root or on nothing — as every hand-back is
    /// (ADR-0021's note of 2026-09-30). A grid or a control of the page's that the user has moved
    /// to keeps the keyboard. A Chrome calls it once its frame has closed, so that the next arrow
    /// moves the Focus again.
    /// </summary>
    public Task ReturnKeyboardAsync() => InvokeAsync(() => ReclaimFocusAsync());

    private bool OpenConsumerPopover(RenderFragment<GridPopoverContext> content, string label)
    {
        if (_disposed || _editMode != EditMode.None)
            return false;
        CloseMenus();
        _consumerPopover = new ConsumerPopover(content, label);
        BeginPopoverFocus();
        // The contents are the Consumer's, and focus themselves on their count.
        _popoverFocusPending = PopoverFocus.None;
        _consumerFocusRequest++;
        _consumerFocusLastRequest = 0;
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    /// <summary>The frame's context, for the contents (ADR-0050, item 16).</summary>
    private GridPopoverContext BuildConsumerPopoverContext()
        => new(CloseFromChrome, _consumerFocusRequest, _consumerFocusLastRequest, InnerPopupChangedFromChrome);

    /// <summary>Tab off the contents' last control lands on the trailing sentinel, which hands
    /// the keyboard back to their first (ADR-0039): this handler is the grid's own, so the
    /// render that carries the count follows.</summary>
    private void WrapConsumerPopoverToFirst() => _consumerFocusRequest++;

    /// <summary>Shift+Tab off the contents' first control lands on the leading sentinel, which
    /// hands the keyboard on to their last.</summary>
    private void WrapConsumerPopoverToLast() => _consumerFocusLastRequest++;

    /// <summary>
    /// Where a Consumer's popover stands (ADR-0040): under the header band, as a column's popover
    /// does, centred across the Viewport and as wide as its contents ask, never wider than the
    /// Viewport — so either edge stays inside the grid's box — and bounded below by the Viewport's
    /// bottom. Nothing is measured: the numbers are the ones the grid is laid out from.
    /// </summary>
    private string ConsumerPopoverStyle()
    {
        var widest = Math.Max(0, _visibleWidthPx);
        var narrowest = Math.Min(PopoverMinWidthPx, widest);
        return string.Create(CultureInfo.InvariantCulture,
            $"left: {widest / 2}px; top: {FormulaBarPx + BandHeightPx}px; transform: translateX(-50%); width: max-content; min-width: {narrowest}px; max-width: {widest}px; max-height: {HeaderPopoverRoomPx}px");
    }
}
