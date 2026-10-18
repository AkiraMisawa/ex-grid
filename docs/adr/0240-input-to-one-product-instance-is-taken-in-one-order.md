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

Each stage is a pull request of its own, and the next starts only once the daily hunt and the widened
run (ADR-0220 §4, §5) are green on it:

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
- **A Chrome that marks a control `data-ex-claims` must reach the core every time that event comes**: a
  telling no handler claims holds the inputs behind it until the next telling. The Wrapper's filter form
  therefore hands every submission to `Apply`, which refuses one it cannot apply.
- **Measured** (2026-10-18, `spikes/render-bench/results/20261018-input-order/`, headless Chromium,
  interleaved against `main`): typing, presses, and a menu opened and closed with each step waited for
  cost the same on the Server host at 0 and 60 ms and on WebAssembly; the largest difference, 20 clicks in
  one task on WebAssembly, is about 10 ms in 170 and swaps sides between passes. Ten Shift+F10, Escape, ↓
  in one task at 60 ms end on the wrong row on `main` in every round (four of the ↓ lost); the branch ends
  on the right one in about 3.1 s, the round trips each step waits for.

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
