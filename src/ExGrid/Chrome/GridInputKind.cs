namespace ExGrid.Chrome;

/// <summary>
/// The kind of input a handler of a Chrome's, a Consumer's or a product's own takes its place in the
/// Input Order with (ADR-0240 section 6): the browser event that brought it. A control whose handler
/// changes the instance's state, or where the keyboard is, names the events it takes in its
/// <c>data-ex-claims</c> attribute, in the browser's names (<c>click</c>, <c>keydown</c>, …); the grid's
/// listener numbers each such event as it happens, and the handler, calling
/// <c>ExGrid.InOrderAsync</c> or <see cref="GridPopoverContext.InOrder"/> with the kind of that event as
/// it starts, takes the number and is handled in its place.
/// </summary>
public enum GridInputKind
{
    /// <summary>A click: <c>data-ex-claims="click"</c>.</summary>
    Click,

    /// <summary>A double click: <c>data-ex-claims="dblclick"</c>.</summary>
    DoubleClick,

    /// <summary>A context menu, the secondary button's click: <c>data-ex-claims="contextmenu"</c>.</summary>
    ContextMenu,

    /// <summary>A press of the pointer's button: <c>data-ex-claims="mousedown"</c>.</summary>
    Press,

    /// <summary>A key going down: <c>data-ex-claims="keydown"</c>.</summary>
    KeyDown,

    /// <summary>A form's submission: <c>data-ex-claims="submit"</c>.</summary>
    Submit,

    /// <summary>An element taking DOM focus: <c>data-ex-claims="focus"</c>.</summary>
    Focus,

    /// <summary>
    /// The keyboard coming into the Input Root from outside it: a <c>focusin</c> heard on an element that
    /// declares <c>data-ex-claims="focusin"</c>. Only the one that crosses the Input Root's edge is an
    /// input: the handling of a <c>focusin</c> that moves DOM focus inside the Input Root does not run.
    /// </summary>
    KeyboardIn,

    /// <summary>
    /// The keyboard leaving the Input Root for somewhere outside it: a <c>focusout</c> heard on an
    /// element that declares <c>data-ex-claims="focusout"</c>. Only the one that crosses the Input Root's
    /// edge is an input: the handling of a <c>focusout</c> that moves DOM focus inside the Input Root does
    /// not run, nor does one the window's own losing focus raises.
    /// </summary>
    KeyboardOut,
}
