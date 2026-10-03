using Bunit;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Stands in for wwwroot/ex-grid.js: the module, the per-instance handle
/// <c>attach</c> returns, and the offsets that handle reads (ADR-0018/0021). bUnit's
/// JSInterop is strict, so an unstubbed call fails the test — which is the signal
/// wanted here: the day the component reaches for JavaScript somewhere new, the tests
/// say so, and the allowlist gets consulted before an ADR is quietly bypassed.
/// </summary>
internal sealed class GridJSInterop
{
    /// <summary>Must match the path ExGrid imports.</summary>
    internal const string ModulePath = "./_content/ExGrid/ex-grid.js";

    private readonly JSRuntimeInvocationHandler<ScrollOffset> _offset;
    private readonly JSRuntimeInvocationHandler _releaseTab;
    private BunitJSModuleInterop? _module;
    private BunitContext _context = default!;
    private BunitJSModuleInterop? _handle;

    private GridJSInterop(
        JSRuntimeInvocationHandler<ScrollOffset> offset,
        JSRuntimeInvocationHandler releaseTab,
        JSRuntimeInvocationHandler dispose)
    {
        _offset = offset;
        _releaseTab = releaseTab;
        Dispose = dispose;
    }

    /// <summary>How many times Escape's Leave has told the gate to release Tab — the way "the
    /// grid kept Tab" is observable without a browser (ADR-0012, rewritten 2026-10-01). The
    /// handle has no blur any more: DOM focus stays on the root, and a call to one fails the
    /// strict stub.</summary>
    internal int TabReleases => _releaseTab.Invocations.Count;

    /// <summary>The handle's own dispose — asserted by the teardown test (ADR-0018).</summary>
    internal JSRuntimeInvocationHandler Dispose { get; }

    /// <summary>How many times the grid has read the offsets. Both axes come back in one
    /// call by design, so this counting is the contract: two reads per scroll would mean
    /// the rows and the columns were painted from different moments.</summary>
    internal int OffsetReads => _offset.Invocations.Count;

    /// <summary>The id of the root the listener was attached to (ADR-0018). Read off the
    /// attach call rather than the markup: bUnit writes an element's reference id into
    /// the markup only at the render that created it, and the render that follows the
    /// attach (ADR-0033's Prerendered state lifting) is not that one.</summary>
    internal string RootReferenceId =>
        ((Microsoft.AspNetCore.Components.ElementReference)_module!.Invocations["attach"][^1].Arguments[0]!).Id;

    /// <summary>Where the grid has told the browser to scroll — how Focus-follows-scroll
    /// is observed without a browser (ADR-0012). A re-anchoring write (ADR-0028/0053) is
    /// among them, in the order asked, with the horizontal offset it leaves alone as NaN.</summary>
    internal IReadOnlyList<(double Top, double Left)> ScrolledTo =>
        [.. _context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier is "setScrollOffset" or "anchorScrollTop")
            .Select(invocation => invocation.Identifier == "setScrollOffset"
                ? ((double)invocation.Arguments[0]!, (double)invocation.Arguments[1]!)
                : ((double)invocation.Arguments[0]!, double.NaN))];

    /// <summary>Every re-anchoring write the grid asked for (ADR-0028/0053): the offset it
    /// asked for, and the offset the browser had to be standing at for it to be written.</summary>
    internal IReadOnlyList<(double Top, double From)> Anchored =>
        [.. _context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "anchorScrollTop")
            .Select(invocation => ((double)invocation.Arguments[0]!, (double)invocation.Arguments[1]!))];

    /// <summary>From here on, the browser refuses a re-anchoring write: it is no longer where
    /// the grid last knew it, because a scroll the grid has not heard yet moved it.</summary>
    internal void RefuseAnchors()
        => _handle!.Setup<bool>("anchorScrollTop", _ => true).SetResult(false);

    /// <summary>The browser has not answered <c>metaIsPrimary</c> yet, which the attach asks before
    /// the grid listens: until the test answers it, the listener is attached and the root is still
    /// no tab stop (ADR-0033, A11Y-20). Set before the grid is rendered.</summary>
    internal JSRuntimeInvocationHandler<bool> UnansweredMetaIsPrimary()
        => _handle!.Setup<bool>("metaIsPrimary", _ => true);

    /// <summary>The browser answers that Meta is this platform's primary modifier — an Apple
    /// platform, where Cmd+click adds a range (ADR-0012). Asked once at attach, so this is set
    /// before the grid is rendered; the keys are told per key instead.</summary>
    internal void MetaIsPrimary()
        => _handle!.Setup<bool>("metaIsPrimary", _ => true).SetResult(true);

    internal static GridJSInterop Setup(BunitContext context)
    {
        var module = context.JSInterop.SetupModule(ModulePath);
        var handle = module.SetupModule("attach", _ => true);
        // Asked once at attach: which modifier this platform's users reach for (ADR-0012).
        // The tests drive OnKeyAsync directly and pass it per key, so the answer here only
        // has to exist.
        var metaIsPrimary = handle.Setup<bool>("metaIsPrimary");
        metaIsPrimary.SetResult(false);
        var offset = handle.Setup<ScrollOffset>("getScrollOffset");
        offset.SetResult(default);
        var setOffset = handle.SetupVoid("setScrollOffset", _ => true);
        setOffset.SetVoidResult();
        // A re-anchoring write, conditional on where the browser stands (ADR-0028/0053):
        // written, unless a test says the browser has moved.
        handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        var releaseTab = handle.SetupVoid("releaseTab");
        releaseTab.SetVoidResult();

        // The Cell Editor's mode reaching the key gate (ADR-0010). The tests drive
        // OnKeyAsync directly, so the mode only has to be accepted here.
        var setEditing = handle.SetupVoid("setEditing", _ => true);
        setEditing.SetVoidResult();
        // A popover's contents reporting a popup of their own (ADR-0039): what the key
        // gate was told is asserted.
        var setInnerPopup = handle.SetupVoid("setInnerPopup", _ => true);
        setInnerPopup.SetVoidResult();
        // Which keys this grid takes, re-told when a parameter change changes the answer
        // (ADR-0007/0010/0020/0055) — what it was told is asserted.
        var setClaims = handle.SetupVoid("setClaims", _ => true);
        setClaims.SetVoidResult();
        // Where the caret goes after the core wrote the editor's text itself (ADR-0051).
        var setCaret = handle.SetupVoid("setCaret", _ => true);
        setCaret.SetVoidResult();
        // The two pointer reports' switches (ADR-0021's fifth entry). The tests drive
        // OnPointerRowAsync and OnPointerRestAsync directly; what is asserted here is
        // what the browser was told to report.
        var setPointerReporting = handle.SetupVoid("setPointerReporting", _ => true);
        setPointerReporting.SetVoidResult();
        var forgetPointer = handle.SetupVoid("forgetPointer");
        forgetPointer.SetVoidResult();
        // A menu copy always takes the asynchronous clipboard route (ADR-0005/0036);
        // the strict stub has to know the call or the first menu Copy throws.
        var writeCopy = handle.SetupVoid("writeCopy", _ => true);
        writeCopy.SetVoidResult();
        // The keyboard handed back to the root, only while focus is still inside it or on
        // nothing (ADR-0021/0018): the condition is the browser's; the request is asserted.
        // Every focus the core asks for — the root's through the handle, any other element's
        // through Blazor's FocusAsync — is logged here as it is made: the two travel by
        // different routes, and only a log kept at the moment of the call orders them.
        var focusLog = new List<string?>();
        context.JSInterop.SetupVoid(invocation =>
        {
            if (invocation.Identifier == BlazorFocus)
                focusLog.Add(((Microsoft.AspNetCore.Components.ElementReference)invocation.Arguments[0]!).Id);
            return false;
        });
        var reclaimFocus = handle.SetupVoid(invocation =>
        {
            if (invocation.Identifier != "reclaimFocus")
                return false;
            focusLog.Add(null);
            return true;
        });
        reclaimFocus.SetVoidResult();
        // The open edit's surface taking the keyboard, asked of the handle, which grants it only
        // while DOM focus is still inside the root or on nothing (ADR-0021's note of 2026-09-30):
        // logged with the other focus requests, as the surface it names.
        var focusEditor = handle.SetupVoid(invocation =>
        {
            if (invocation.Identifier != "focusEditor")
                return false;
            focusLog.Add((bool)invocation.Arguments[0]! ? BarSurface : CellSurface);
            return true;
        });
        focusEditor.SetVoidResult();
        // The keyboard handed on to a control of the Consumer's outside the grid, asked of the
        // handle, which grants it only while DOM focus is still inside the root or on nothing
        // (ADR-0070's note of 2026-10-02): logged with the other focus requests, as the control.
        var handKeyboardTo = handle.SetupVoid(invocation =>
        {
            if (invocation.Identifier != "handKeyboardTo")
                return false;
            focusLog.Add(((Microsoft.AspNetCore.Components.ElementReference)invocation.Arguments[0]!).Id);
            return true;
        });
        handKeyboardTo.SetVoidResult();
        // Where the keyboard is going, told to the key gate (ADR-0039 and ADR-0050 item 16,
        // 2026-10-01): to a Consumer's popover, or to a frame of the Consumer's own.
        var handOff = handle.SetupVoid("handOff", _ => true);
        handOff.SetVoidResult();
        var dispose = handle.SetupVoid("dispose");
        dispose.SetVoidResult();
        return new GridJSInterop(offset, releaseTab, dispose)
        {
            HandedOff = handOff,
            _context = context,
            _module = module,
            _handle = handle,
            PointerReporting = setPointerReporting,
            PointerForgotten = forgetPointer,
            InnerPopupTold = setInnerPopup,
            ClaimsTold = setClaims,
            CaretPlaced = setCaret,
            FocusReclaimed = reclaimFocus,
            EditorFocusAsked = focusEditor,
            KeyboardHandedOn = handKeyboardTo,
            _focusLog = focusLog,
        };
    }

    /// <summary>Every time the core told the key gate where the keyboard is going (ADR-0039 and
    /// ADR-0050 item 16, 2026-10-01): <c>popover</c> or <c>frame</c>.</summary>
    internal JSRuntimeInvocationHandler HandedOff { get; private init; } = default!;

    /// <summary>Every time the core asked for the keyboard back on its root (ADR-0021/0018) —
    /// granted by the browser only while DOM focus is still inside the root or on nothing.</summary>
    internal JSRuntimeInvocationHandler FocusReclaimed { get; private init; } = default!;

    /// <summary>Every time the core asked for an open edit's surface to take the keyboard,
    /// through the handle's conditional <c>focusEditor</c> (ADR-0021's note of 2026-09-30): its
    /// argument says whether the surface is the Formula Bar's text.</summary>
    internal JSRuntimeInvocationHandler EditorFocusAsked { get; private init; } = default!;

    /// <summary>Every time the core asked for the keyboard to be handed on to a control of the
    /// Consumer's, through the handle's conditional <c>handKeyboardTo</c> (ADR-0070's note of
    /// 2026-10-02): its argument is the control.</summary>
    internal JSRuntimeInvocationHandler KeyboardHandedOn { get; private init; } = default!;

    /// <summary>The Cell Editor's surface, as <see cref="Focused"/> names a request for it.</summary>
    internal const string CellSurface = "surface:cell";

    /// <summary>The Formula Bar's text, as <see cref="Focused"/> names a request for it.</summary>
    internal const string BarSurface = "surface:bar";

    /// <summary>Blazor's own <c>FocusAsync</c>, as bUnit records it.</summary>
    internal const string BlazorFocus = "Blazor._internal.domWrapper.focus";

    private List<string?> _focusLog = [];

    /// <summary>Every element the core has asked the browser to focus, in the order asked: the
    /// root, by the handle's conditional reclaim; an open edit's surface, by the handle's
    /// conditional <c>focusEditor</c>, as <see cref="CellSurface"/> or <see cref="BarSurface"/>;
    /// a control of the Consumer's, by the handle's conditional <c>handKeyboardTo</c>; and any
    /// other element by Blazor's.</summary>
    internal IReadOnlyList<string> Focused => [.. _focusLog.Select(id => id ?? RootReferenceId)];

    /// <summary>How many times the core has asked for DOM focus anywhere, by any route.</summary>
    internal int FocusCalls => _focusLog.Count;

    /// <summary>Runs <paramref name="heard"/> at each reclaim as the core makes it, so a test
    /// can see what stood on screen at that moment.</summary>
    internal void OnFocusReclaimed(Action heard)
        => _handle!.SetupVoid(invocation =>
        {
            if (invocation.Identifier == "reclaimFocus")
                heard();
            return false;
        });

    /// <summary>Every (text, caret) the listener was told to place the caret at (ADR-0051).</summary>
    internal JSRuntimeInvocationHandler CaretPlaced { get; private init; } = default!;

    /// <summary>Every time the key gate was re-told which keys this grid takes
    /// (ADR-0007/0010/0020/0047).</summary>
    internal JSRuntimeInvocationHandler ClaimsTold { get; private init; } = default!;

    /// <summary>The keys the gate was told at attach: the fourth argument of <c>attach</c>.</summary>
    internal IReadOnlyList<string> TakenAtAttach =>
        (IReadOnlyList<string>)_module!.Invocations["attach"][^1].Arguments[3]!;

    /// <summary>The Consumer's declared keys the gate was handed at attach for its editing
    /// branch (ADR-0050, item 14): the last argument of <c>attach</c>.</summary>
    internal IReadOnlyList<string> DeclaredAtAttach =>
        (IReadOnlyList<string>)_module!.Invocations["attach"][^1].Arguments[7]!;

    /// <summary>Every time the key gate was told whether a popover's contents have a popup
    /// of their own open (ADR-0039).</summary>
    internal JSRuntimeInvocationHandler InnerPopupTold { get; private init; } = default!;

    /// <summary>How many times a scroll paint told the browser to forget its last
    /// pointer report along with the band it dropped.</summary>
    internal JSRuntimeInvocationHandler PointerForgotten { get; private init; } = default!;

    /// <summary>Every pair the grid told the browser: whether to report row changes
    /// (on with <c>HighlightHoverRow</c>) and rests (on with <c>CellMessageOf</c>).</summary>
    internal JSRuntimeInvocationHandler PointerReporting { get; private init; } = default!;

    /// <summary>A <c>getScrollOffset</c> the browser has not answered yet, answered when
    /// the test says — how a read crossing a write on a Server circuit's wire is staged
    /// (ADR-0012). Every read the grid makes from here on lands on it, until a later call
    /// stands up another.</summary>
    internal JSRuntimeInvocationHandler<ScrollOffset> UnansweredScrollRead()
        => _handle!.Setup<ScrollOffset>("getScrollOffset");

    /// <summary>A <c>setCaret</c> the browser has not carried out yet, finished when the test
    /// says — how a caret report crossing the core's placement on the wire is staged
    /// (ADR-0051). Every placement from here on lands on it.</summary>
    internal JSRuntimeInvocationHandler UnansweredCaretPlacement()
        => _handle!.SetupVoid("setCaret", _ => true);

    /// <summary>A <c>setEditing</c> the browser has not carried out yet, finished when the test
    /// says — how the key gate hearing a mode change a round trip later on a circuit is staged
    /// (ADR-0010). Every mode told from here on lands on it.</summary>
    internal JSRuntimeInvocationHandler UnansweredGateMode()
        => _handle!.SetupVoid("setEditing", _ => true);

    /// <summary>What the next <c>getScrollOffset</c> answers — the browser scroll
    /// position the grid is about to read, on both axes at once.</summary>
    internal void SetScrollOffset(double top, double left)
        => _offset.SetResult(new ScrollOffset(top, left));
}
