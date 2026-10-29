# Over the Script Core, the Consumer's calls on a grid cross to the browser, and what only the C# core answered goes with it

*Recorded on 2026-10-29 in the project thread that removes the C# core (ADR-0370 step 3), under the user's standing
instruction to carry 0.3.0 through without stopping ("止まらないで", "don't stop"). It applies the rules
[ADR-0440](./0440-over-the-script-core-what-a-grid-paints-comes-with-each-row-and-what-the-consumer-answers-later-is-asked-in-its-turn.md)
and ADR-0350 section 7 set to the methods the Razor component offered a Consumer; no option the user had not already
chosen for another member was taken.*

## Context

On 2026-10-29:

- **The DemoHost pages and the Docs Examples draw `ExGrid.Script.ExGrid`** (ADR-0370 step 2), and the next step
  deletes `ExGrid.Components.ExGrid` with the C# core. Its parameters were carried by ADR-0440; its **methods** were
  not, beyond `PlaceSelectionAsync`, `FocusAsync`, `ReturnKeyboardAsync` and `HandKeyboardToAsync`.
- **The Razor component answered its methods at once, from state it held in .NET**: the view, the open edit's text,
  the Selection. Over the Script Core that state is the browser's, and the core's handle answers it
  (`readView`, `showViewWith`, `showNewView`, `readEditText`, `inOrder`, `cancellableInOrder`, `openPopover`,
  `closePopover`, `refreshSummary`, `writePointedText`, `takeBackPointedText`). React's ref offers each as it is.
- **Some of the Razor component's public members were its own plumbing or ExSheet's.** The members the browser's
  half called (`OnKeyAsync`, `PressTakenAt`, `InputsSent`, …) were public only because Razor's JavaScript interop
  calls public methods. Others were seams the Razor ExSheet used (`ShowViewWithMatch`, `ForgetCurrentMatch`,
  `EditorTextOf`, `OnPointStateChanged`, …), which ExSheet's Script Core now does inside its own Sheet
  (ADR-0370 step 1, the `ExSheet.Script.ExSheet` merged in #453 to #493).
- **A .NET handler of the grid's events runs in its turn among them** (ADR-0440 section 2), and an event that is a
  write of the user's holds the browser's inputs after it until .NET has handled it (ADR-0142). Work the browser
  places in the Input Order waits behind every input held, including one whose answer .NET has not yet given.

## Decision

### 1. A view is read from the browser, and handed back by the number the browser keeps it under

`ReadViewAsync()` answers the `GridView` the core reads (`readView`): the Selection, the Row Sequence Version it is
written in and the scroll offsets, what .NET sent before the call taken in first. The core knows a view it read by
the object it handed out, so the browser keeps each view read under a number, and the `GridView` carries that number
with the grid that read it (carried through `with`, outside the view's equality, as the Razor component carried its
set of rows). The browser lets a view go once .NET no longer holds any copy of it: .NET tells the numbers of the views
it has collected with the next read.

`ShowViewWith(view)` stays synchronous, as on the Razor component: the Consumer hands it from the handler that moves
the version to `view.RowSequenceVersion`, and the view crosses to the browser at once, ahead of any state the
Consumer's source sends after the call, so the core takes both in one pass (`showViewWith`). A view this grid did
not read is rows of their own, shown for the first time (`showNewView`): it may hold no Selection or one range, at
offsets of zero, which is all a Consumer can make without reading one; any other is refused by name. A view this
grid read comes back as the browser read it, its version aside: one handed back with another Selection or other
offsets, made through `with`, is refused by name, because the browser would show the view it keeps and not the one
handed. `ShowViewWith(view, range, focus)` places `range` with its Focus in place of the view's Selection, whatever
that Selection holds, the Focus revealed from the view's offsets, as on the Razor component. The core's `showViewWith` gains the optional place that
`showNewView` already took, and either reveals the Focus it places.

### 2. The open edit's text is read from the browser

`ReadEditTextAsync()` answers the core's `readEditText`, what .NET sent before the call taken in first.

### 3. Work of the Consumer's takes its place in the browser's Input Order

`InOrderAsync(handling)` and `CancellableInOrderAsync(handling, cancelledTooLate)` place a turn with the core's
`inOrder` and `cancellableInOrder`, numbered; in its turn, the browser asks .NET to run `handling`, and the inputs
made meanwhile wait until it has ended, as on the Razor component. An Escape that cancels the work cancels
`handling`'s token from the browser, and `cancelledTooLate` is called in .NET in the Escape's own turn. Work placed
from inside a turn's `handling` runs at once inside that turn (the core's `GridTurn`), as work placed from a Script
ExSheet's toolbar press runs inside the press. A failure of `handling` is thrown from the call as it was thrown.

`SelectionAfterInputsAsync()` reads the Selection in such a turn, after every input the browser received before the
call. A call to the browser that waits on a turn waits as long as the inputs ahead of it and the work take: the
circuit's time for a call does not cut it short. Disposing of the grid cancels `handling`'s token for the work under
way.

**From a handler of the grid's own events**, each runs at once instead, as part of the event under way, which is what
the Razor component did with nothing pending: the event is the turn, and placed in the browser it would wait behind
the inputs held after the event, which wait for this very handler. A handler of a write of the user's (an edit, a
paste, a fill, a declared key, a Replace All, a marking gesture, a Context Menu's command) holds the inputs after it,
so the work is in its place. A handler of a notice the browser does not wait on (`SelectionChanged`,
`OnRefused`, …) runs its work in .NET's order of events, not among the browser's inputs, as its handler does.
`CancellableInOrderAsync` from such a handler is refused by name: its Escape would have to cancel an event's turn,
which no input made after the event can reach. **Inside cancellable work** the same holds: the core's
`cancellableInOrder` hands its work no `GridTurn`, the work holds the inputs after it until it ends, so work placed
from it runs at once, its `SelectionAfterInputsAsync()` reads at once, and cancellable work placed from it is refused.

### 4. The Consumer's popover is drawn as a Razor slot in the core's frame

`OpenPopoverAsync(content, label)` opens the core's popover frame (`openPopover`), and the contents are rendered by
.NET as a Razor slot (ADR-0410) moved into the frame, so the frame's rules are the core's: the keyboard, Tab's wrap,
Escape, a press elsewhere and the box shrinking close it. Until the contents stand, the inputs made after the opening
wait (ADR-0410 section 5), and the contents' first control then takes the keyboard where it is still the frame's.
`ClosePopoverAsync()` and the context's `Close` close it; `InnerPopupChanged` is the core's. A control in the
contents that names its event in `data-ex-claims` is numbered as it happens, and the inputs after it wait until the
contents' handler hands its work to the context's `InOrder(kind, work)`, which runs it in that place: the contract the
Razor component set (ADR-0240 section 6), so a key typed after a click on the contents' OK reaches what the OK left,
at any speed.

How it is carried:

- **The slot is drawn per opening.** Each opening is numbered in .NET, and the slot the browser mounts for it draws
  the Consumer's fragment for that opening and nothing once another stands. The browser tells .NET when the popover
  closes, however it closed. The contents are drawn again with each render of the page, whose state they read. The
  context's `FocusRequest` and `FocusLastRequest` stay zero: the browser hands the keyboard to the first control and
  wraps Tab itself. `TakeFocus` is `HandKeyboardToAsync`.
- **A claim is told before Blazor hears its event.** The browser hears a claimed event on the contents in the capture
  phase, after the grid's own listeners on its root, which hold or take an input first. It numbers the event below
  zero, apart from .NET's work, tells .NET the number at once, and places a turn that asks .NET to run what the
  handler hands. Blazor hears the event on the document after that, so the handler that hears it finds the number
  told. `submit` and `focus`, which Blazor hears on the document in the capture phase, are heard in the window's
  capture phase, filtered to the contents (ADR-0021's twelfth use). A handler that hears another kind than the one
  told voids it and takes a place of its own; a telling no handler takes is voided by the next.
- **Every claim is said passed once its dispatch is over.** Whether a handler hears the event is not the browser's to
  see: the contents' own script may stop it before it reaches the document, and a component may stop it inside
  Blazor's own dispatch — MudSelect stops its press there, so the handler on a box around it never runs. Either would
  hold every input behind it. So in a task after the dispatch, which ends before any task runs, the browser tells .NET
  the event passed, behind the dispatch on the same channel, and a telling no handler took by then is void: its turn
  ends. A handler therefore hands its work before its first `await`; work it hands later takes a place of its own.
  *(Rewritten in the implementing branch: the first version said an event unheard only where the window never heard
  it bubble, and the first real-browser run held every input behind a MudSelect's press.)*
- **The same contract on the grid.** A Template cell's content is the Consumer's markup too: its claims are heard the
  same way, and `InOrderAsync(kind, handling)` on the grid is the context's `InOrder`. `KeyboardIn` and `KeyboardOut`
  are told only by a product's Input Root around a grid, and this grid has none: their handling does not run, as the
  Razor component ran none it did not tell.

### 5. The rest of the handle crosses as it is

`RefreshSummaryAsync()`, `WritePointedTextAsync(text)` and `TakeBackPointedTextAsync()` call the core's
`refreshSummary`, `writePointedText` and `takeBackPointedText`, what .NET sent before the call taken in first;
`CanSummarize` is answered in .NET, where the component knows its `OnSummarize` and its source.

### 6. What only the C# core answered goes with it

These Razor members are not carried, and leave with `ExGrid.Components.ExGrid`:

- **The browser half's plumbing**, public only for Razor's interop: every method a script called (`OnKeyAsync`,
  `PressTakenAt`, `PressInTurn`, `InputsSent`, `InputTakenAt`, `BeginRowClick`, `OnPasteStreamsAsync`, …).
- **The C# core's Window model**, which ADR-0440 replaced with the Grid Source: `Window`, `WindowStart`,
  `TotalCount`, `RowKey`, `RowSequenceVersion`, `Sorts`, `Filters`, `OnSortChanged`, `OnFilterChanged`, `IsLoading`,
  `OnRangeNeeded`, `VouchesDistinctRows`, `OnCopyRowsNeeded`, `OnDistinctValuesNeeded` and `OnFind`, all of which a
  source (`GridSource.From`, `GridSource.Fetch` or the Consumer's own `IGridSource`) answers.
- **The refusals by kind**, `OnPasteRefused`, `OnCopyRefused`, `OnMarkRefused`, `OnActionRefused`,
  `OnCommitRefused` and `OnFindRefused`, which `OnRefused` raises as one `GridRefusal`.
- **ExSheet's seams**, which ExSheet's Script Core does inside its Sheet: `ShowViewWithMatch`, `ForgetCurrentMatch`,
  `OpenPopoverAsync(content, label, closedByEscape)`, `DiscardEditAsync`, `CellAlign`, `CellType`, `EdgeBorder`,
  `EditorAppearance`, `PaintedText`, `RootId`, `Clock`, `EditorTextOf`, `FormulaBarBorrowedTextOf`,
  `NameBoxSizeLabel`, `OnBrowserTimeZone`, `OnPointArrowFromOutside` and `OnPointStateChanged`.
- **The keys held for a frame**, `JoinSurfaceAsync`, `HandKeyboardToFrameAsync`, `PassKeysOutAsync`,
  `KeysTypedOnTheWay` and `ReturnKeyboardAsync(from, typedOnTheWay)`. The Script grid joins no surface outside its
  Input Root: a key typed on a frame of the Consumer's is the frame's, and `ReturnKeyboardAsync(from)` and
  `HandKeyboardToAsync` hand the keyboard back and on under the condition DC-61 sets (ADR-0350 section 7's first
  slice).
- **Reads of state .NET no longer holds**: `ReadSelection()` and `GetFocusedValue()` (`SelectionChanged`,
  `ReadViewAsync` and the Consumer's own rows say both), and `BuildCopyPayload`, whose copy the browser builds and
  `CopyAnswerAsync` answers.

## Considered options

- **`ReadView` answered from what .NET last heard.** .NET hears the Selection through `SelectionChanged`, a round
  trip after the browser, and never hears the scroll offsets. Rejected: a view read straight after a press would be
  the one before it.
- **The view sent with the next state.** Rejected: a Consumer that hands the view after the state it bumps would see
  it never shown, where the order of the two calls the Consumer makes already says which comes first.
- **A view read crossing whole and rebuilt in the browser.** Rejected: the core knows the set of rows a view was read
  of only by the object it handed out, and an edit open over another set comes back only with that set's own view
  (ADR-0331 section 5).
- **`InOrderAsync` from an event's handler placed in the browser like any other.** It waits behind the inputs held
  after the event, which wait for the handler: the grid would stop. Rejected.
- **A Consumer popover whose contents' handlers run when they arrive, with no claims.** A key typed after a click on
  the contents' OK would reach whatever held the keyboard as the round trip ended. Rejected as the spine's sixth rule
  rejects any outcome that depends on timing.

## Consequences

- A Consumer that switched sets of rows, read the edit's text, or placed work in the order with the Razor component
  awaits each read now, and keeps `ShowViewWith`'s call where it was.
- The browser holds a few numbers and a Selection for each view .NET still holds, and lets them go as .NET collects
  them.
- The members in section 6 are a Breaking Change Note of the step that deletes the Razor component; each names what
  replaces it.
