# Input to one product instance is taken in one order

Decided with the user, 2026-10-18, for `docs/specs/input-order/issues/01`. Grilled in two rounds; the
user took the recommendation of every question, and of (1) after it was explained with an example.
The inventory it rests on is `docs/specs/input-order/inventory.md`. Tickets 02 to 07 of the same spec build
it in turn (7); ticket 02 is built ("As built", below).

## Why

The races of 2026-10-16 and 2026-10-17 were each fixed at the entry point where a test happened to
find them: ADR-0230's three (with `InputOvertaken`, which is what PR #148 added), ADR-0204's
addendum (ticket 240), tickets 260 and 261. Each fix is right and each is local. The inventory shows
why they keep coming:

- **One entry point is ordered, and only part of it.** The key gate (`ex-grid.js`) numbers keys,
  presses on the rows, paste, copy and the Keyboard Field's composition, and the core echoes the
  numbers on the Settled Stamp. Text typed into the Cell Editor, the Formula Bar and the Name Box, a
  click on a heading, a ▾ opener, a menu item, the completion list, the pager, ExSheet's toolbar and
  Format Cells, and all of ExPivot's field list reach the core as Blazor events with no place in that
  order.
- **ExSheet and ExPivot each keep a hold of their own.** The Sheet Toolbar's band replays its own
  `_typedOnTheWay`, ExPivot's popups their `_popupKeys`, the Wrapper's Format Cells dialog its own
  `_keysReady` gate. Three products, four queues, none aware of the others.
- **The C# side is not serialized.** Each `[JSInvokable]` and each Blazor handler runs on the
  renderer's context and lets the next one in at every `await`. Order comes from the script's hold
  (only for keys whose verdict is `mode` or `popover`), the commit barrier, `_pressAnswer`'s chain and
  the reorder buffer in `InputAnswered`; a key with the verdict `core` is sent without waiting, so its
  handler can overlap the one before.
- **Some waits end on a clock.** `editorSettled` polls a frame at a time for the editor's or a
  popover's focus and gives up after 2 s, replaying the held keys against whatever is there. On a
  slow circuit that is an outcome that depends on timing, which the spine's sixth principle refuses.

The user's goal: the product family takes input at machine speed, on Blazor Server and WebAssembly
alike, without dropping or reordering it, with no overhead it does not need, and **the same way in
every product**, not ad hoc per product.

## Decision

### 1. What is in the order: every input that changes the instance's state or where the keyboard is

The **Input Order** of an instance takes, one at a time and in the order the user made them:

- keys, presses and releases on the rows, the double click, paste, copy, the Keyboard Field;
- **text typed into the Cell Editor, the Formula Bar and the Name Box** (it changes what the next key
  means, so it cannot stay outside);
- a click on a heading, a ▾ opener, a menu item, a completion candidate, a toolbar command, a pager or
  status button;
- a popover's or dialog's opening and closing, and the input that commits it: OK, Apply, Find's
  Next, a Pivot layout dropped;
- the keyboard moving into or out of a Chrome surface (a popover, a dialog, a Consumer's frame).

**A draft inside a popover or dialog is not in it**: the filter's search field and check boxes,
Format Cells' controls, a Pivot settings dialog's fields. They change nothing in the instance until
the input that commits them, which is in the order and judges the draft once. Inside the draft the
browser's and Blazor's own event order holds, because every one of those events comes through the
same Blazor path; the races found so far were all between the script's path and Blazor's.

What a draft needs instead is a rule: **a render never writes back over what the user chose in a
draft.** Ticket 260 was that defect, and an order cannot prevent it. A draft's value is set when it
opens and by the user, never re-read from a parameter on a later render (the CLAUDE.md `@bind` trap,
stated for drafts).

*Option rejected: every control inside a popover numbered as well.* The outcome is the same, because
the commit is judged after the opening and the next key after the commit; it costs a number and a
wait per check box and rewires every MudBlazor control in the Wrappers.

### 2. Where the order is made: the script numbers, Blazor carries, C# takes them by number

The capture-phase listeners the key gate already has give each input its number as it happens, as
`PressTakenAt` does today for a press on the rows. The input's content still reaches C# the way it
does now: a key through `OnKeyAsync`, a click or a commit through its Blazor event, which claims the
number told ahead of it (`AsTaken`, generalised). The core takes inputs by number, never by arrival.
An input whose number was told and whose event never comes is answered void, as a press is today.

*Option rejected: every input through the script, Blazor's `@on…` handlers removed.* It would widen
ADR-0021 a long way (a listener per kind of control, and the script reading controls' values) for an
order the numbers already give.

### 3. C# takes one input at a time

Each instance has **one queue**. An input's handler does not start until the previous input's
handler has finished, `await`s included; finishing means its state is written and its render asked
for, not that the render has reached the browser (the Settled Stamp still says that). The commit
barrier (`AfterCommitInFlightAsync`), `_pressAnswer`'s chain and the `core` keys sent without waiting
fold into the queue. A handler that waits on the Consumer (a commit the Consumer hears) holds the
inputs behind it, which the commit barrier does already.

This closes WebAssembly's gap, where a handler that awaits lets the next event in, by construction
rather than per handler.

### 4. Nothing waits on a clock

Every wait is on a number or on a mark the render carries: the editor's or a popover's focus, the
hand-off of the keyboard, a render the input owes. `editorSettled`'s 2 s give-up and every fallback
like it go. An input that cannot be carried out any more, because what it was taken against no
longer holds, is **refused visibly**: the live region says so, as `KeyRefusalLabelIds` does today
(ADR-0203), and the inputs behind it are judged against what now stands. It is never replayed
against whatever happens to be there.

### 5. The text of a field rides with the next numbered input

Text typed into a field gets no number of its own. **Every numbered input carries the text of the
field that holds the keyboard**, and the core adopts it before handling the input, then drops the
Blazor input events older than that text as overtaken. `InputOvertaken` does this today for a key
alone; it becomes the rule for every numbered input: a press, a click on a heading, a commit.
Typing adds no message, so a character still goes at the browser's speed.

The Settled Mark gains a condition: the text the field holds is the text its surface last rendered
(`data-ex-mode-text`), so a burst of typing is not settled until the core has heard all of it. ASY-7's
exclusion of "text typed into a field" goes.

*Option rejected: a number per character.* A message more per character on the Server host, for an
order the next input's text already gives.

### 6. One order per product instance, offered to every surface through Chrome

- **The unit is the product instance**, not the grid: an ExGrid on its own; an ExSheet with its grids,
  Headings, Formula Bar, Name Box and Sheet Toolbar; an ExPivot with its report, field list, popups
  and details. Its outermost element is the **Input Root**, where the key gate's capture listeners
  attach (for an ExGrid on its own it is the grid's root, as ADR-0018 has it). A grid an ExSheet or
  ExPivot draws takes its place in the product's order rather than keeping one of its own.
- **ExGrid's core owns the order**, and offers it through the Chrome contexts as a seam: a surface
  that takes input (a Built-in Chrome, a Wrapper's, a Consumer's, ExSheet's toolbar, ExPivot's field
  list) puts its input in the order through its context, as `GridPopoverContext.TakeFocus` hands it
  the core's focus today. **The seam is public**: a Consumer's own Chrome or popover would otherwise
  be the next hole. The queues of ExSheet's toolbar, ExPivot's popups and the Wrapper's Format Cells
  dialog are replaced by it.
- **A surface MudBlazor renders outside the Input Root** (a `MudDialog` under its provider, a
  `MudPopover` at the body) joins the instance's handle while it is open: the context gives the
  script its element, and the same capture listeners hear it. It joins nothing else on the page.
- **A Pointing Scope stays** what ADR-0058 says: grids grouped for pointing. A press handed on to a
  grid of the same scope that belongs to another instance takes a number in the order of the
  instance that holds the keyboard.
- ExCharts, when it comes, takes input through the same seam from its start.

### 7. How it is built: in stages, each green before the next

Each stage is a pull request of its own. Normal CI must pass. Before the next stage starts, the
**related input tests** must also pass the hunt's repeated run and the widened run
(ADR-0220 §4, §5), on Server and WebAssembly in Chrome and Edge. Use the hunt's current settings:
ten repetitions, plus the widened run with a 150 ms round-trip floor and CPU throttled by four.
Record the selected specs, why they cover the changed input paths and their neighbours, the
implementation revision, and the results in the stage's ticket or PR. Select the scope before
reading its results; a failure is fixed, not excluded to make the gate green. Documentation-only
changes do not invalidate results for an otherwise unchanged implementation and test revision.

**The full hunt runs alongside the work; its completion is not a condition for starting the next
stage.** Its findings still enter ADR-0220 §3's defect process. An observed defect in the stage's
input paths must be resolved; an unfinished whole-suite run does not hold unrelated implementation.

*Revised with the user, 2026-10-20:* this section originally required the whole daily hunt and
widened run to finish before every stage. Running every browser test ten times made unrelated
coverage the critical path for input work. The user chose to retain repeated and widened
verification of the related input paths as the stage gate, and let the whole-suite hunt continue
asynchronously. Normal CI and the rule against treating a passing re-run as a fix are unchanged.

The stages are:

1. **02** — the core's queue (3), numbers for every input the script hears (2), no clock (4), in ExGrid.
2. **03** — field text rides with the next input (5), and the Settled Mark covers typing.
3. **04** — ExSheet: the toolbar, Headings, Name Box, Format Cells and the Pointing Scope on the order.
4. **05** — ExPivot: the field list, popups, toggles and details on the order.
5. **06** — the Wrappers: their dialogs and popovers join the handle (6), drafts follow (1)'s rule.
6. **07** — the local fixes that the order now covers are removed (see Consequences), and the
   differential test of (8) is the acceptance.

### 8. Accepted by a test that types fast and slow and compares

Layer 3 gains a **differential test**: a sequence of keys, presses, typed text and menu and toolbar
commands, generated from a seed, sent at machine speed on the Server host (150 ms round trip through
the latency proxy) and on WebAssembly, and compared with the same sequence sent one input at a time,
each read after the Settled Mark. The two must agree in every product. The seed is fixed per test and
printed, so a red reproduces from its seed alone (ADR-0220 §3: a red is a defect, never settled by a
re-run). The Definition of Done gains it as a criterion when it is built.

### 9. Breaking changes are allowed in 0.2.0

The Chrome contexts' delegates may change shape to carry the order. Each change is a Change Note
(ADR-0190). No old path is kept beside the new one: two ways in is the defect this decision removes.

## As built: ticket 02 (2026-10-18)

- **The queue** is `InputOrder` (`src/ExGrid/InputOrder.cs`): one per instance, numbered inputs handled
  one at a time in number order, every `await` included; a handling asked for inside the flow of the one
  under way runs at once (the commit barrier's `_commitFlow`, generalised). Every numbered entry point
  goes through it (`AnsweredAsync`); the commit barrier holds only what is asked for outside it.
- **A told number is never voided by a later number.** A press the script told of can reach the core
  after a key numbered behind it on the Server host (`SettledMarkTests`), so the key waits for the press.
  A told number is void only at the next telling of the same kind of input, or at a claim of another kind
  (`AsTaken`'s rule since 2026-10-16), or at disposal.
- **Clicks, and the keys a popover's own handlers answer, are numbered** through `data-ex-claims` on the
  control (`click`, `dblclick`, `contextmenu`, `mousedown`, `keydown`, `submit`, `focus`) and
  `InputTakenAt`; the handler claims the number (`ClaimedAsync`, `ClaimedByKeyOrAsync`). The Wrapper's
  controls carry the same marks, so swapping Chrome does not change the order. A copy run by a numbered
  menu click or key takes no number of its own, which would wait behind the input running it. ADR-0021
  gains two entries for the listeners this needed.
- **No clock is left in the script.** Every wait ends on a number the Settled Stamp answers or on
  `data-ex-focus-owed`, which the stamp renders while the editor, a popover's control, a substituted
  Chrome's contents or a Consumer's popover contents still owe the keyboard. A Consumer's popover owes it
  from its opening until DOM focus enters its contents (however they take it: `TakeFocus`, or their own
  focus, as the toolbar's lists do), until a `TakeFocus` it asked for has been told, or until it closes; owed
  only from its sentinels at first, the keys held for a list opened from the ExSheet toolbar's KeyTips
  were refused before the list's focus landed (CI, 2026-10-18). The core tells the script of a hand-off
  to such a popover before the render that draws it, so the stamp read as that telling arrives is a
  render behind: the script numbers the telling, the core answers it in its place after the opening
  (`HandOffHeardAsync`), and the wait counts that number before it judges the keyboard missed. Held input that can no longer be carried out is refused aloud
  with `KeyRefusalLabelIds.KeyboardDidNotArrive`. A Consumer's frame is waited for until it takes the
  keyboard, another grid does, or the grid is disposed.
- **A drag's moves are held behind its press until the press is answered.** The core attaches a drag's
  move and release handlers in the render that answers its press (a heading drag, the rows' selection
  drag, a grip's or a reorder's), so on the Server host a move made within a round trip of the press
  reached no handler, and a drag faster than that grew nothing (`sizing.spec.mjs` SR-2d under the widened
  run). While the press, or a press held before it, is unanswered and the primary button is down, ADR-0021's
  pointer listener holds the move among the held inputs, keeping only the last; the release and the click
  behind it stay behind it. At its turn the move is dispatched where it was made and the drag's handler,
  attached by then, takes it. Decided on the recommended option of the question put to the user on
  2026-10-18; ADR-0021's fifth entry is widened, not added to.
- **A Chrome that marks a control `data-ex-claims` must reach the core every time that event comes**: a
  telling no handler claims holds the inputs behind it until the next telling. The Wrapper's filter form
  therefore hands every submission to `Apply`, which refuses one it cannot apply.
- **Measured** (2026-10-18, `spikes/render-bench/results/20261018-input-order/`, headless Chromium,
  interleaved against `main`): typing, presses, and a menu opened and closed with each step waited for
  cost the same on the Server host at 0 and 60 ms and on WebAssembly; the largest difference, 20 clicks in
  one task on WebAssembly, is about 10 ms in 170 and swaps sides between passes. Ten Shift+F10, Escape, ↓
  in one task at 60 ms end on the wrong row on `main` in every round (four of the ↓ lost); the branch ends
  on the right one in about 3.1 s, the round trips each step waits for.

## Added 2026-10-20, decided with the user: the Input Journal, a circuit that is down, and a paste cancelled

Three questions the order left open, asked after the Hunt traces of 2026-10-19 had to infer which inputs
were held and answered (FD-6, KB-31, ED-22 nested), and after the ~10 MB paste was seen to hold every
input behind it for a minute on the widened run. Each was put to the user with a recommendation.

### 10. The Input Journal is internal

Each instance keeps an **Input Journal**: in order, every input the script numbers (number, kind, held or
sent, the mode it was gated against), every hold and its release, every answer the core gives, and the
core's side of each number (entered, finished, made void). The script's side is a bounded ring buffer,
always on, kept by the listeners the order already has: it adds no listener, reads no layout and sets
no timer, so ADR-0021 gains no entry. The core's side goes to `ILogger` at Debug, under its own
category. Neither is a public API: no parameter turns it on and no Consumer reads it. Layer 3 attaches
both to a failed test, per instance. *Rejected:* a Consumer-facing opt-in (a public surface and a
Change Note for a diagnostic), and a hook only the DemoHost turns on (blind outside CI).

*As built (input-order ticket 08, 2026-10-20):* the script keeps 512 entries per instance, read through
`Symbol.for('ex-grid.input-journal')` on the root and deleted with it; calls that report the pointer are
not journaled, and a value is kept as a number, a boolean, a string of up to 16 characters or its kind,
so a paste or a field's text never is. The core writes under `ExGrid.InputOrder`
(`InputOrder.JournalCategory`); the Server DemoHost lets that category through to its log file at Debug,
and `fixtures.mjs` attaches both halves to a failed test as `input-journal.json`. On WebAssembly the
core's half is not written to the console, which CON-8 keeps silent.

### 11. Input made while the Server circuit is down is refused, never replayed

A numbered input made while the circuit is disconnected is **not** held for the reconnection. Its number
is made void, so the inputs behind it are not kept waiting, and the input is refused aloud (section 4,
`KeyRefusalLabelIds.LostOnTheWay`) only if it never reached the core: a call that reached the core before
its reply was lost is not refused. A circuit that does not come back is Blazor's to show. *Rejected:*
holding the input and replaying it after the reconnection, which would act against whatever stands then
(section 4).

**How the loss is found (the mechanism was replaced once, 2026-10-20, before it was built).** As first
written, the script was to learn of the failure from its own call's rejected promise. It cannot:
`blazor.web.js` (10.0.7) does not await the send of a call to .NET, so on a connection that is down the
send's rejection escapes as an uncaught error and the call's promise never ends. Reproduced on the
Server host through the latency proxy: two ↓ typed while the connection was cut never arrived, and the ↓
typed after the reconnection reached the core and waited for them for good.

So the loss is found by count, decided with the user (decision card, 2026-10-20; *rejected:* listening
to Blazor's reconnection, which needs an ADR-0021 entry and does not fire under a Consumer's own
reconnection handler):

- **Every number reaches the core in order, or not at all.** The script gives a number in the call that
  carries it (`nextInput` as an argument), so numbers are sent in order, over one connection that
  delivers in order, and each call's first synchronous step runs in that order on the renderer. This rests
  on the hub taking one invocation at a time per client (`HubOptions.MaximumParallelInvocationsPerClient`,
  1 by default); a host that raises it can see a number overtaken, which reads here as lost.
- **So a number missing when a later one arrives was lost.** Every call that carries a number marks it as
  having reached the core first (`InputOrder.Reached`), and the numbers between the last that reached it
  and this one are void and refused. An earlier number never arrives after a later one any more, so the
  layer-2 tests that sent them out of order now hold the order with a paste still being read instead.
- **A hold says how far the numbers went.** A key typed behind an unanswered call is held, so nothing
  numbered follows a lost call that a hold waits on. An input taken into the hold while a numbered call is
  unanswered sends the core how far the numbers have gone (`InputsSent`, unnumbered), which finds the lost
  ones the same way. One small message per input held on a slow circuit is the cost.
- **The script's waits end.** The core tells the script the numbers it found lost (`inputsLost` on the
  instance's handle), and the calls that carried them end with null, as a call that did nothing, so a
  hold waiting on one lets go.

Nothing listens to the reconnection, so the refusal is said when the user's next input after it finds
the loss, not at the reconnection itself; ADR-0021 gains no entry. **Not covered**, each a case of the
connection dropping between two messages already on the wire rather than of input made while it is down:
a reply lost after its call reached the core (the script's wait on it does not end), and a click whose
telling arrived and whose Blazor event did not (its number waits for the next telling, as section 2
says).

### 12. Escape cancels a paste still being read: the one input that acts ahead of its turn

A paste whose streams are still being read or parsed is under way: the root says so (`aria-busy`) and
the live region announces it once. **An Escape typed while it is under way cancels it.** The script
numbers that Escape as a cancel and also tells the core at once, outside the order; the paste, if it
has not raised its Edit Intent, stops reading and is refused aloud (`PasteRefusalReason.Cancelled`). If
it has, the cancel finds nothing, and the Escape's turn says aloud that the paste had already landed.
The Escape's turn does nothing else: it closes no editor and no popover.

This is the one place an outcome depends on timing, against the spine's sixth rule, and it is accepted
deliberately: a cancel exists to race the work it cancels. What keeps it honest is that both outcomes
are said aloud, and which one happened is never left to be guessed. *Recommended against, and chosen:*
progress alone, with no cancel.

*As built (2026-10-20, large-paste ticket 01):* the Escape is one call, `OnPasteCancelAsync(paste,
input)`, naming the paste by its number; the core hears it on arrival and answers it in its own turn.
A cancel that arrives while the paste still waits for its turn cancels it as that turn starts, and one
naming a paste already finished finds it landed. The visible sign is the root's `ex-pasting` class,
painted from Visual Tokens (`--ex-pasting-bar`, `--ex-pasting-bar-height`, `--ex-pasting-cursor`): a
bar across the top edge and a progress pointer, nothing dimmed, since the rows shown are the ones
being pasted over.

## What it may cost

Nothing per character (5) and nothing per cell. A click that was not numbered gains the number told
ahead of it, a message without a round trip, as `PressTakenAt` is. On the Server host nothing waits a
round trip that does not wait one today. Rows still skip their render (ADR-0003, ADR-0027 P1–P9). It
is measured before it is called done: `spikes/render-bench` gains a mode that types, presses and
opens menus at machine speed over the Server host's 0 and 60 ms round trips and on WebAssembly,
against the same page built from `main`.

The script uses the capture-phase `keydown`, `mousedown` and `input` it already has (ADR-0021's first
and seventh entries), attached to the Input Root and, while one is open, to a surface rendered
outside it. If building it shows another listener is needed, ADR-0021 gains the entry in the same
pull request, with its reason.

## Consequences

- ADR-0018's per-instance listener moves from the grid's root to the Input Root; one product instance
  is still independent of every other on the page.
- ADR-0220: ASY-7's exclusions narrow to drafts inside a popover (5); RR-14 still holds.
- ADR-0230 §1–§3, `InputOvertaken`, ADR-0204's addendum (`pressedPopovers`) and ADR-0210's synthetic
  `change` are each kept, folded into, or removed by ticket 07, and each of those ADRs gains a note
  saying which.
- The Docs Site's page on automation (the Settled Mark) and the Chrome seams' pages say what the order
  is and how a Consumer's Chrome takes part; each stage writes its Change Note.
- `CONTEXT.md` gains **Input Order** and **Input Root**.

## Row Action and row Mark paths, 2026-10-19

Upstream ticket 02's implementation left the row Action and row Mark callbacks outside the
order: an isolated component reproduction on main `04530f84` passed ordered keys and header
clicks, but both row callbacks overtook a preceding asynchronous Consumer key. These paths join
the existing Input Order; no second queue is introduced.

Decided with the user: a waiting row command tracks identity only while continuously present
in the current Window. If it leaves, the command releases the row/key and is refused in its
turn even if the identity returns. ADR-0160 records the retention tradeoff; ADR-0142 and ADR-0043
record the resulting Action/Mark behavior. Source replacement continues to take precedence.

The row listener registers the existing press descriptor at the click, before the browser
hold, including a Mark's painted checked state. C# tracks its current-Window target from that
point. The browser puts execution in its existing held sequence and assigns the number in
that turn, so an earlier held key or heading click cannot be overtaken. It tells the ticket and
number, dispatches the captured element once, and tells that dispatch finished. The normal
Blazor callback claims the command; if it was discarded, the finishing notification recovers
the same command and number. A missing target is never looked up anew in the DOM. Ticket
cancellation and ordinary claim abandonment release the target and void any assigned number.
Existing unnumbered interop methods remain callable; the shipped row listener uses the ticket
protocol. This adds no parallel command queue: tickets capture targets, and Input Order still
executes every command.

*As built, corrected after the 2026-10-20 hunt (RI-13):* a row Mark with no earlier browser-held
input is dispatched immediately through that same ticket protocol. Mark changes no keyboard mode
(ADR-0043), so C# can serialise its handling without a browser round trip before the next Mark is
sent. Holding every Mark until its reply had made a Consumer's button outside the root read only
the first of two rapidly marked rows. A Mark behind a held key or Action still joins that hold;
target retention, numbering, refusal and discarded-event recovery are unchanged.

Integrating section 11 also removed the browser's wait for the unnumbered registration and
dispatch-end replies. Those calls are still sent and their errors reported, but a row click waits
for its numbered Begin and answered stamp. Otherwise a click made while disconnected kept the
hold waiting for two replies Blazor never supplies, even after the core declared its number lost.
The partial-wire exclusions in section 11 are unchanged.

## As built: ticket 03 field text (2026-10-20)

The existing numbered interop calls carry `GridInputText` for the owning Cell Editor, Formula Bar
or Name Box. Keys reuse the editor text they already carry rather than sending it twice.
Tellings ahead of Blazor events keep their text by input number; the existing Input Order adopts
it in that number's turn, and voiding or disposal releases it. This is no second command queue.
The existing overtaken-input rule covers presses and claims as well as keys. Name Box submission
claims its existing form event before reading the carried text; a later acknowledgement cannot
restore a Name Box submission that has already ended.

The first implementation treated each carried reading as fresh typing. The widened Server
run exposed the mistake: a press inserted a Reference, then its acknowledgement or release
carried the same pre-render text and erased that Reference. The field reading now carries a
revision shared by inputs that saw the same text. The core adopts a revision once; unchanged
text can still report a moved caret. The existing input listener advances the revision for
new typing even when it returns to the same value, and a changed field value also advances it
when read. These revisions add neither an input number nor an interop message per character.
Keys carry the revision alongside their existing text argument, without repeating the string.

Typing still uses its existing Blazor input message. The existing root input listener remembers
only typed owned surfaces, and the Settled Mark remains off until their rendered text agrees
with the field. Animation frames check attributes and values while text is owed; they read no
layout. A removed editor or a Formula Bar whose editing mark has gone no longer owes typing;
its closing command still owes its numbered answer. No per-cell observer or new event listener
is introduced. Popover and dialog drafts keep section 5's exclusion.

The typing-then-menu regression also exposed two gaps in section 4's existing hand-off. A
right-click now holds following keys for its popover just as Shift+F10 does, including when no
edit was open. An older render's after-work must not consume a newer popover's focus request
before that opening has rendered its control references. The request remains pending until
the render bearing it is built. Neither wait uses a clock or adds another queue.
