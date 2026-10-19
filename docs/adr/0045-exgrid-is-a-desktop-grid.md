# The Ex family keeps its grid on desktop and touch devices; Chrome adapts to the box

*(Scope revised with the user, 2026-10-19, in the responsive/touch design session, Q1–Q3.
The original decision deliberately excluded phones and touch. The product now aims to let users
complete ordinary work by touch, including editing, rather than offer a read-only mobile view.
This records the agreed scope, not implemented or verified mobile support. Q4–Q7 settled the
basic gestures and permitted adaptive frames later in the same session. Q9–Q27 settle mixed
input, Fill, reference selection, column operations, browser coverage and the mobile-support
completion boundary, interruption, external frame hosting and bounded Pivot target detection.
The final design review distinguishes implementation parameters and proof obligations from
further changes of policy.)*

*(Numbered 0043 when it was written. Row Marks reached the main branch first under that number,
so this one became 0045 on the merge, 2026-09-26; nothing in it changed.)*

*(Decided with the user, 2026-09-25, after comparing ExGrid with MudBlazor's `MudTable` and
`MudDataGrid`. Both switch to a stacked "small-device mode" below a `Breakpoint`, `Xs` by default.
The question was what this grid does instead.)*

Until now the answer was spread across three places, and one of them gave a reason that did not
hold:

- [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) put touch and pen out of scope in one
  sentence: "this is a desktop grid".
- [ADR-0017](./0017-target-chromium-browsers-only.md) put Safari out of scope. Every browser on
  iOS and iPadOS is built on WebKit, so that excluded the iPhone and the iPad as well, although
  nothing said so.
- [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) refused the
  Wrapper's `Breakpoint` with "no stacked layout, the grid is an island
  ([ADR-0031](./0031-the-grid-lays-out-left-to-right-only.md))". ADR-0031 is about right-to-left
  layout. The "island" there is a `dir="ltr"` attribute, and it says nothing about stacking.

This ADR is the one place the answer lives.

## Decision

**Desktop keyboard and mouse remain supported; touch and phones join the target.** ExGrid,
ExSheet, ExPivot and their existing MudBlazor Wrappers are all in scope. The destination is
ordinary work completed through touch alone: ExGrid's selection, copy, sort, filter and permitted
editing; ExSheet's range operations, editing and Fill; and ExPivot's report configuration.
Delivery may be staged, but read-only access is not the final goal. Pen-specific support remains
outside this initial delivery; a pen is not silently treated as a mouse Fill gesture.

**The initial mobile design and verification include Android Chrome and Safari on iPhone and
iPad**, alongside the desktop browsers in ADR-0017. A mobile path not checked on a real device
is not described as verified. This expands the target; it does not assert that today's code works
on those devices.

**"Responsive" preserves the grid and adapts Chrome to the available box.** A window is resized,
a Drawer opens, a split pane moves, or a phone turns. The grid follows its box through
`ViewportSize.Stretch`, reported by the browser
([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)). Column widths are not
squeezed to fit. When the columns are wider than the box, they remain reachable by horizontal
scrolling. Density is the Consumer's choice, and the grid does not switch it by width. Narrowness
does not hide columns, turn rows into cards, shrink text or numbers, or change data semantics.
Toolbars, menus and editing surfaces are the parts to adapt. They must retain access to supported
commands. Presentation and switching follow the contracts below; numerical breakpoints and
control dimensions are implementation parameters to establish and verify in the Examples.

**Columns narrower than the box are not stretched.** The space to their right stays empty, as an
empty sheet does in Excel. Stretching them, as `MudDataGrid` does, would make a column's width
depend on the window. A number readable at one window size would then show `####` at another,
and the width painted would stop being the width recorded in View State
([ADR-0016](./0016-column-width-and-overflow.md)).

For the existing desktop presentation, what a resize does to the rest is decided where each
thing lives: the Focus is not chased
(ADR-0012), a popover with no room closes
([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)), and the first frame after a
change is painted from the previous size (ADR-0028). On a Server circuit that first frame lasts a
round trip rather than a frame. How long, during a window drag, has not been measured. It is
listed with the other unmeasured numbers in the Definition of Done.

**There is no stacked layout, and the Wrapper keeps refusing `Breakpoint`.** Every rule this
grid is built on is defined on a two-dimensional grid of cells: selection as rectangles in index
space ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
the painted Overlay (ADR-0008), what an arrow key means
([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)), the shape of a copy and a paste
([ADR-0005](./0005-copy-refuses-rather-than-truncates.md),
[ADR-0014](./0014-paste-shape-rules-and-selection-count.md)), the fixed row height
([ADR-0013](./0013-fixed-row-height.md)), Pinned Columns and the header. A card per row keeps
none of them. Supporting one would mean defining every one of those rules a second time, for a
layout that Excel itself does not use: its mobile apps keep the grid. A Consumer who wants cards
below a width swaps in a different component at a breakpoint of its own choosing. `MudDataGrid`
itself is a reasonable choice there.

**Pinning is suspended while it would leave nothing to scroll.** On a narrow box the Pinned
block can cover the whole Viewport. The rows then show only pinned columns, panning moves
nothing visible, and a Focus moved into a scrollable column stands underneath the pinned block,
which breaks ADR-0012's "the Focus must always be visible" without anything looking wrong. So
while the band the Pinned block leaves for the scrollable columns is narrower than the default
`MinWidth` (40px), **every column scrolls together**, as if nothing were pinned. When the box
widens again, pinning comes back. This changes how the grid is painted, not what was asked for:
the pinned count in View State is untouched, so nothing is written back to a saved view. Before
this, `ColumnGeometry` called the covered case "legal — the user sees only pinned columns" in a
comment, and nothing else decided it.

## Touch gestures and adaptive frames *(2026-10-19, Q4–Q7)*

**Ordinary cell content keeps native panning.** A one-finger swipe scrolls without changing
Selection or beginning an edit. A tap selects the cell without opening the software keyboard;
a double tap begins editing where editing is permitted. The distinction must hold on both
Server and WebAssembly. A touch-capable computer still supports the mouse's ordinary selection
drag. Input behavior is not inferred from the width of the box.

**Selection actions have a visible entry point as well as long press.** A long press inside
Selection preserves it and opens its actions; outside it, the pressed cell becomes the Selection
before its actions open. A candidate that turns into a scroll is cancelled. Opening the menu
writes no cell data. Copy, Paste, Edit and Clear are reached where the product supports and permits
them, through the existing command meanings. Gesture recognition thresholds still need an
implementation contract; the timer alone must not make a cancelled gesture act. The interruption
outcomes are settled below.

**The Selection Handle and the fill handle have different meanings.** One Selection Handle
moves Extent from the fixed Focus and never changes cell data. Fill is entered explicitly
from the action menu, through the existing Fill semantics. The user qualified this choice as
applying on devices with touch screens. In Q9 the user clarified that, where a mouse is available,
the default should instead be the fill handle. The proposed permanently touch-oriented presentation
on a hybrid device was not accepted. The revised Q9 settled the following rules:

- A browser reporting mouse/trackpad-like pointing capabilities starts with the fill handle,
  where Fill is enabled. Touch-only operation starts with the Selection Handle. Capability
  reporting is the initial hint, not a reliable inventory of attached hardware.
- Actual touch input switches to the Selection Handle. An actual mouse click restores the
  mouse presentation; mouse movement alone does not. Input is distinguished by its origin,
  not by the compatibility mouse events a touch may produce.
- An active drag or a pending Fill confirmation does not switch presentation. The input type
  and meaning captured at the gesture's start survive until its end, including held input.
- A first touch directly on the displayed fill handle only changes to the Selection Handle.
  That contact starts no Fill and no selection drag. The next drag may extend Selection. It is
  not enough to repaint the handle later while letting that first touch enter the mouse Fill path.

The mechanism must agree with the product's Input Order (ADR-0240); that ADR is a decided design,
not an already completed general input implementation. Q24 settles unknown initial capability
as Selection Handle, with an actual mouse click able to establish mouse presentation later.
Unknown input must not be inferred to be mouse input merely because a fill handle is painted.

**Initial touch range creation is one rectangle (Q15).** It includes whole-row and whole-column
Selection. Creating disjoint ranges by touch is deferred; existing mouse and keyboard multi-range
operations remain. Switching to touch alone neither clears an existing disjoint Selection nor
merges it into one rectangle. The actions available over such a preserved Selection retain their
existing shape rules. **While disjoint ranges remain selected, no Selection Handle is shown
(Q17).** The Selection and supported menu commands remain available. A new cell tap selects a
single range and makes its handle available; the input-mode change alone does neither.

**Touch Fill previews before it writes (Q10).** Entering Fill captures the selected source range.
A handle chooses the target under the existing Fill shape rules and shows a preview. Releasing
the finger does not write. Apply executes once; Cancel changes no cell data. On ExSheet the
operation is one Undo step. Existing Source and order refusals still apply; confirmation cannot
silently rebind the operation to a different source or order. This adds a confirmation stage to
the touch gesture, not a second implementation of Fill semantics.

**Adaptive editing and command surfaces may use space outside the grid's box.** The user accepted
extending the current cell-sized editor and grid-bounded popover contracts so a narrow or short
embedded grid does not trap its controls inside an unusable rectangle. Editing target, draft,
validation and the meanings of commit and cancel remain common across presentations and Chromes.
This extends the frame, not a Wrapper's authority over those meanings.

**An edit begun by touch opens in an editing dialog (Q11).** It shows the input, commit and
cancel controls from the start. Its presentation is retained throughout that edit: rotation or
the software keyboard changes its placement, not its editing mode or uncommitted text. Ordinary
mouse editing keeps the inline presentation. The frame, keyboard, Inner Popup and instance
ownership boundaries are specified below; their implementation must prove those outcomes.

**Reference selection is an explicit stage of that same edit (Q14).** In ExSheet, a control to
choose a Reference makes the Sheet available for selecting its range while retaining the edit's
draft and caret. Completing reference selection returns to the editing surface. It does not
commit and reopen the edit, and Focus remains on the cell being edited. The chosen range supplies
the Reference under the existing Point rules (ADR-0051), not an ordinary replacement Selection.
This user-requested stage is distinct from an automatic presentation switch on rotation, which
Q11 rejected. **Cancelling this stage restores the draft and caret from its entry, and continues
the edit (Q21).** It does not cancel the whole edit. The existing Pointing Scope still defines
eligible targets, including other grids in that scope; touch does not restrict references to
the edited Sheet alone. The stage's presentation and keyboard return must preserve this
ownership and scope; it must not create a second editing state or cancel an IME composition by
replacing the active field during reflow.

**The Field List design includes both tap commands and touch drag and drop (Q8).** The user
asked for both unless the extra implementation cost is excessive. Inspection found that both
routes can reuse the existing movement semantics and the callbacks shared by both Chromes;
there is no evidence to exclude drag and drop on cost grounds. This is not a measured effort
estimate.

**A narrow Field List opens as a dedicated surface (Q12).** Touch drag begins from a dedicated
grip; the rest of an item remains available for taps and native scrolling. The destination and
insertion position are shown. Only release on a valid target moves the entry. Release outside
the dedicated surface cancels, rather than treating a report behind it as a removal target.
Removal is available as Remove or a drop on an explicit removal target within that surface.
This changes the touch targets, not the underlying placement or removal meanings. The existing
desktop report-removal target remains. Target detection and interruption boundaries are settled
below; their implementation and scrolling still need verification.

## Interruption and external frames *(2026-10-19, Q20–Q23)*

**An interrupted drag is not a successful release (Q20).** A second contact, browser
cancellation or rotation that makes the drag untenable stops it. A Selection drag keeps its
last valid Selection. Unconfirmed Fill, resize, reorder and Field List drag previews are
discarded without applying. These are the interruption rules, not a change to Q10's normal
finger release, which leaves the Fill preview available for Apply. Source/order changes still
use the existing invalidation and refusal rules. Rotation alone does not discard ordinary
editing text (Q11), and pinch remains the browser's zoom. Cancellation must be represented as
such through input holding and dispatch, never replayed as a normal mouse release.

**Built-in Chrome uses one shared dialog host installed at application level (Q22).** This
provides a rendering destination outside a grid's clipping ancestors. MudBlazor connects to its
existing dialog infrastructure. Sharing a rendering destination does not share editing state:
the surface belongs to the product instance that opened it, uses that instance's editing state,
keyboard ownership and Input Order, and opens no separate edit or queue. ADR-0240's external
surface registration is the contract to follow, not evidence that this infrastructure is built.
The default host and each Wrapper must keep Inner Popups usable and preserve ownership when the
frame moves between editing and reference selection. Missing-host handling and dismissal details
follow Q27 below. The host must be installed outside ancestors that clip its contents or give
viewport-positioned children a different containing block; placing it inside an arbitrary
transformed application panel does not meet the external-frame contract.

**Closing is not Apply (Q27).** Cancel, Escape and a backdrop tap cancel the active surface
through its owner's Input Order. An open Inner Popup closes before its parent, as ADR-0039
already requires. Reference-stage cancellation follows Q21; Fill cancels without writing and
an edit discards only its uncommitted draft. This does not capture browser history or turn the
browser Back action into a new grid command. If the required rendering host is absent, report
that configuration failure and do not pretend to have opened an operable editing surface.

**Pivot touch drag may ask which registered target is under the pointer (Q23).** While an
ExPivot touch drag is active, `elementFromPoint()` may resolve only registered Area, entry and
explicit removal targets for that instance. The browser reports target changes and the final
target with the identity of the rendering they refer to; C# decides whether the move is valid.
This does not allow reading grid-cell geometry, scanning cells, measuring during ordinary
rendering, or finding a target again when a held event is replayed. ADR-0021's specific rejection
of finding what is under an old coordinate *at replay time* still holds: that would name what is
there now, not what the gesture was made on.

The reason for this bounded exception is that the Field List has browser-laid-out targets,
whereas the grid's cells have existing C# geometry. The current HTML drag events supply targets;
a captured pointer stream does not provide the same target transitions. Reproducing the Field
List's variable layout in C# would create a second layout model. The agreed exception is recorded in
[ADR-0263](./0263-pivot-touch-drag-resolves-only-registered-live-targets.md) and ADR-0021's allowlist.
The number was reserved before use; registration does not claim that the feature is built.

## Browser integration boundaries *(2026-10-19, Q24–Q26)*

**The input bridge reports browser facts and gesture candidates (Q24).** It is scoped to the
owning product's Input Root and registered external surfaces, not every cell or the whole page.
It retains pointer identity, original input type, the target and Paint at the start, movement,
event timing and end/cancel state. It captures a pointer for a dedicated handle and releases it
when the gesture ends or is cancelled. Tap and long-press candidates are recognized from the
browser's event sequence, not from a timer started after a Server round trip. C# decides what
they mean, including hit testing over the grid's geometry; JS does not change Selection, Fill,
column widths or column order on its own.

Recognized touch input and the compatibility mouse/click/context-menu events it causes must
execute once, including the first touch on a painted mouse fill handle. Keyboard and assistive
technology activation must remain operable. A pointer's `preventDefault` alone is not a promise
that subsequent click/context-menu events disappear. Native panning and pinch permissions must
be in effect when contact begins; changing `touch-action` after the second finger arrives does
not restore a browser gesture that was disabled at its start. The acceptance test exercises
pinch from handles as well as ordinary content, rather than checking CSS text alone.

The browser reports fine/hover input capability once for initial presentation, with unknown
capability using Selection Handle. Actual input then takes precedence under Q9. This is a hint,
not hardware enumeration or a requirement for a perpetual device monitor. Pen-specific gestures
are outside initial support, and an unknown or pen pointer is not a mouse Fill authorization.

The reason for this boundary is browser pointer capture and event arbitration before defaults,
which a Server round trip cannot perform at the point of contact. It does not authorize general
per-move JavaScript optimizations: any such optimization of work Blazor can do still requires
the measurement ADR-0021 demands. There are no per-cell listeners or JS cell-coordinate lookup.
The [Pointer Events specification](https://www.w3.org/TR/pointerevents3/) defines the capture,
compatibility-event and direct-manipulation behavior this implementation must respect.

**A touch edit candidate prepares keyboard input during the user gesture (Q25).** C# registers
an actual input surface in advance; during the browser's activating edit gesture, JS focuses it
and preserves immediately typed text for that candidate's target. This is provisional keyboard
preparation, not C# accepting an edit. The final decision enters the owning instance's Input
Order. Acceptance gives those characters to that edit only; refusal writes nothing, closes the
candidate and says why. A later render must not overwrite text typed while acceptance was pending,
and a delayed answer must not steal focus back from another instance the user has entered.
An ordinary selection tap does not prepare keyboard input. Missing-host refusal must happen
without leaving an invisible focused input holding the keyboard.

The reason is browser activation: waiting for the Server's acceptance before focusing can lose
the opportunity to show the software keyboard. This must be proved on the supported real devices,
not inferred from DOM focus. See [HTML user activation](https://html.spec.whatwg.org/multipage/interaction.html#tracking-user-activation)
and [WebKit's focus policy](https://bugs.webkit.org/show_bug.cgi?id=195884). The prepared field and
the dialog use the same draft; a Wrapper does not introduce its own pending edit.

**External frames follow the browser's visual viewport (Q26).** While at least one external
surface is open, report the current visual viewport on opening and its resize/scroll changes;
unsubscribe when the last surface closes. Reports contain its offsets, dimensions and scale,
not a guessed keyboard height: zoom and viewport motion also change them. Place the external
frame against that rectangle so the input, commit and cancel remain reachable. Ordinary adaptive
Chrome still uses the grid container's size. This adds no cell or anchor rectangle measurement,
no render-time polling and no new grid geometry model. The shared host may share one subscription
while dispatching to the surfaces it owns; it does not share product state.

This uses browser notifications that Blazor does not expose. The [CSSOM View specification](https://drafts.csswg.org/cssom-view/#visualviewport)
defines those values and events. A Server notification can take a round trip to affect placement;
the contract is correct, usable placement once the notification is handled, not a claim of zero
transient overlap during every keyboard animation frame.

**The bounded uses have formal allowlist records (Q13).** The number reservation was published
on 2026-10-19 when the user asked to complete implementation prerequisites. Q24 is
[ADR-0260](./0260-touch-input-reports-browser-facts-and-keeps-command-meaning-in-csharp.md); Q25 is
[ADR-0261](./0261-touch-editing-prepares-the-keyboard-during-user-activation.md); Q26 is
[ADR-0262](./0262-external-frames-follow-visual-viewport-notifications.md); Q23 is
[ADR-0263](./0263-pivot-touch-drag-resolves-only-registered-live-targets.md). ADR-0021 lists all four.
This replaces the unnumbered draft and the former pending-registration notes without changing
the agreed bounds. Existing clipboard work remains in its own entry; touch Copy/Paste still
obey browser consent and the existing full-payload/refusal rules. Selection, Fill, editing and
placement meanings remain in C#. The [implementation entry point](../specs/responsive-touch/README.md)
identifies the existing Input Order dependency and verification obligations.

## Column operations *(2026-10-19, Q16)*

The initial mobile delivery includes width changes and reordering where the Consumer enables
those operations:

- **Resize uses a dedicated grip.** A guide follows the drag; release applies the width through
  the existing notification. Size to fit stays available without dragging. It retains
  ADR-0016's meaning, rather than continuously resizing all painted cells during the drag.
- **Reordering starts explicitly from the column menu.** In that operation a drag chooses the
  placement. Non-drag movement commands are offered too; their precise vocabulary remains to be
  specified. Ordinary header swipes scroll instead of beginning a reorder.
- **A header tap retains the product's existing meaning.** ExGrid's ordinary header sorts;
  ExSheet's selects the column. ExGrid also offers explicit column selection through the menu.
  Existing Consumer declarations still govern the header's meaning.
- **Pinned and Header Group boundaries still hold.** A leaf does not escape its group, and a
  group moves as its own unit under ADR-0032. No touch path invents another column-order model.

These entries add input paths, not ownership of View State: the grid still notifies and the
Consumer applies the change. This decision does not add arbitrary numeric width entry.

## Verification and the completion boundary *(2026-10-19, Q18–Q19)*

**The initial mobile version policy** is Safari on the latest updated releases of the two most
recent iOS and iPadOS major versions, and the latest stable Android Chrome. The exact OS and
browser versions are recorded against the release being verified, not inferred from an emulator's
device name. Existing desktop Chrome and Edge coverage remains.

**Real Android, iPhone and iPad runs accompany automated tests.** They exercise native panning,
the software keyboard, Japanese IME, copy and paste, rotation and pinch. The matrix covers Server
and WebAssembly and Built-in and MudBlazor Chrome for each applicable product. Emulated touch
or a desktop WebKit run is not a real-device Safari verification. The precise test allocation and
record format still need to be specified; unrun cases do not count as passed.

**Mobile support is complete only when all three products pass the agreed operations**, including
Fill, Formula reference selection, column operations and Field List configuration, under both
Chromes and hosting models. The attached brief's M1+M2 minimum is not the completion criterion
for this expanded scope. Work may be delivered as separate reviewable changes; an intermediate
preview states exactly what works and what remains. Basic scrolling alone is not a mobile-support
claim. This decision chooses no release number or date and authorizes no commit or publication.

## Rejected

- **A stacked layout in the core.** See above: it is a second component sharing a name with the
  first.
- **Originally rejected, reversed on 2026-10-19: touch as a first-class input** (read-only display on tablets, or full touch gestures). A
  touch drag is a different gesture vocabulary, not the same vocabulary with a different device
  (ADR-0008). The product's claim is Excel's keyboard and mouse operability, and that is where
  the effort went. The user now wants ordinary work completed by touch too; the two-dimensional
  grid and its semantics remain the same, with the touch gesture vocabulary decided above.
- **Leaving a covered Viewport as it was**, which is also what Excel does with frozen panes wider
  than the window. It fails quietly: the Focus is somewhere the reader cannot see. The rule in
  this repository is to say so or show it, never to leave something that looks right and is not.

## Consequences

- **ADR-0030's reason for refusing a stacked-layout `Breakpoint` still points here.** Keeping
  the grid is independent of supporting touch. ADR-0008's former exclusion of touch is replaced
  by this scope; its mouse behavior is retained alongside the decided touch gestures.
- **Suspended pinning is a geometry fact, decided in `ColumnGeometry`**, not in the stylesheet:
  the sticky offsets, `ScrollLeftToReveal` and the horizontal slice all have to agree on it, as
  they already agree on the pinned width.
- **Layer 3 can check it** on the Linux runner by narrowing the page below the pinned width and
  arrowing into a scrollable column.
