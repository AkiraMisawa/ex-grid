# Entering a cell: the core holds the mode over its own actions, and a template's control takes focus when asked

[ADR-0020](./0020-action-and-template-columns.md) decided what Space means on each kind of cell and
that being *inside* a cell is the third mode, **Interactive**. The keyboard half that needed no new
mechanism was built with it — Space fires a single action, and Escape from a focusable descendant
hands the keyboard back — but **entering** a cell by key was not, and
`docs/implementation-status.md` recorded why: *focus into plain-markup cells needs a decision the
ADRs do not make.* Cells hold no references
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)), a Template's control is the
Consumer's, and JavaScript may not move focus
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

**Decision: entering a cell never has the core reach into content it did not render.**

- **A cell with several actions** is entered **without moving DOM focus at all**. The keyboard stays
  on the root, the core holds which action is chosen, `aria-activedescendant` names that action's
  button, and the row paints it — one class on one button.
- **A Template cell** is entered by **asking its content to take DOM focus**. The fragment receives
  a `TemplateCellContext<TRow>` whose `FocusRequest` is handed over non-zero on exactly one
  render; the Consumer's control focuses itself with Blazor's own `FocusAsync`. This is the rule
  [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) already applies
  to a Chrome's Cell Editor — *a control the Chrome rendered is the Chrome's to focus; the core has
  no reference to it and does not try* — applied to the Consumer's content.

No JavaScript is added, and the allowlist does not grow.

## What Space does, now complete

| Focus cell | Space |
|---|---|
| Editable | opens the Cell Editor in Overwrite, **containing the space** — as if it had been typed ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) |
| Action Column, one action | fires it (unchanged) |
| Action Column, several | enters Interactive with the **first** action chosen |
| Template Column | hands the cell's content one focus request |
| any other cell | nothing |
| no Focus at all | **only places the Focus** on the first visible cell, as every first key does ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)) — it never fires or enters a cell the user has not seen focused |

The editable row was in ADR-0020's table from the start and was never wired: Space reached
`Engage`, and `Engage` knew only about actions.

## Inside a cell with several actions

| Key | Meaning |
|---|---|
| ← / → | choose the previous / next action — **clamped** at both ends, never wrapping and never leaving the cell by overshoot |
| Home / End (and Ctrl+← / Ctrl+→, which resolve to the same edge move) | choose the first / last action |
| Space | fires the chosen action, **and leaves** |
| Escape | leaves; the grid keeps the keyboard |
| Enter / Tab | leave, and **keep their own meaning** — the Focus cycles exactly as it would have. **Nothing fires on Enter** ([ADR-0020](./0020-action-and-template-columns.md): Enter is a key people hold down to move) |
| every other key the grid claims | leaves, and keeps its own meaning |
| a pointer press anywhere | leaves |

**Firing leaves**, because an action is the kind of thing that changes the row under it — it opens a
view, or deletes the row. Staying inside a cell whose row may have just slid away would put the
chosen action over *the next row*, one Space from firing on something the user never chose. That is
ADR-0020's chaining accident arrived at by a different key.

**Interactive is tied to the Focus.** It is valid only while the selection's Focus is the cell it
was entered on and that column still declares at least two actions; anything that moves or drops
the Focus — a click, a sort ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
a new column set — ends it. It is state the core holds, like the Focus, so it outlives the row
element it is painted over, as the Focus and the selection do (P7,
[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)).

**Escape's layering gains one layer**, inside the ones [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)
recorded: an open popover closes first; then an Interactive cell is left; only then does Escape
release the grid's DOM focus.

### Why the keyboard stays on the root

The root is where keys are captured ([ADR-0018](./0018-multiple-instances-must-be-independent.md))
and where `aria-activedescendant` lives
([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)), and ADR-0033
warned that anything moving DOM focus into a cell breaks both at once. For the core's **own**
buttons nothing requires breaking them: `aria-activedescendant` may name any descendant, a button
included. Chromium's accessibility tree was checked over CDP with a cell Interactive: the grid
keeps the focused state and its active descendant resolves to the chosen button. That an
assistive technology then reads *"Query, button"* as it would under real focus is the ARIA
pattern's promise, **not a measurement** — no screen reader has been run against it, which is the
same open item as ADR-0033's announcement wording. Every key arrives where every key already
arrives, so the key gate is unchanged, and there is no race between a keypress and a focus move
still in flight.

What this costs is the one thing real focus would have given free: **the chosen button has to be
painted.** It cannot be the overlay's job — the overlay draws what the geometry knows, and a
button's box is the browser's layout of a label, not arithmetic the core holds
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md),
[ADR-0016](./0016-column-width-and-overflow.md)). So the row that holds the chosen button re-renders
with it, and the button carries **`ex-action-chosen`** and the id `aria-activedescendant` names.
That is one row, on a discrete keypress; every other row keeps skipping (P3). The class joins
ADR-0029's stable list, marking a meaning — *the action Space will fire* — and its outline reads the
Focus outline's token, because it is where the keyboard is.

## Inside a Template cell

The fragment's signature changes from `RenderFragment<TRow>` to
`RenderFragment<TemplateCellContext<TRow>>`:

```csharp
public readonly record struct TemplateCellContext<TRow>(TRow Row, int FocusRequest);
```

`FocusRequest` is **handed over on exactly one render of exactly one cell**, and every other cell,
and every later render of that one, is handed zero. A control that wants to be entered focuses
itself when it sees a non-zero number **it has not acted on** — acting once per number, because a
component that re-renders on its own keeps its last parameters until the row renders again:

```razor
<input class="ex-interactive" tabindex="-1" @ref="_input" … />
@code {
    [Parameter] public TemplateCellContext<Position> Cell { get; set; }
    private ElementReference _input;
    private int _acted;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Cell.FocusRequest != 0 && Cell.FocusRequest != _acted)
        {
            _acted = Cell.FocusRequest;
            await _input.FocusAsync();
        }
    }
}
```

Three rules make that contract safe:

- **The request waits until it can be delivered.** Space reveals the Focus first — you cannot enter
  a cell that is not painted — and the request is handed over on the first render that paints the
  Focus cell with real data: in the Window, not a Placeholder, its column on screen.
- **It is dropped if the Focus moves first.** A request for a cell the user has left is not
  delivered late.
- **It is cleared after it is handed over.** Rows are recycled by virtualisation
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)): a row scrolled out and back is a new
  component, and a stale non-zero number reaching it would make the control **steal focus** in the
  middle of whatever the user is doing. Nothing re-created ever sees one.

Once the control has focus, everything is as it already was when a control was clicked: **the
control owns every key but Escape** (KB-11), and Escape hands the keyboard back without leaving the
grid (KB-18). A template that ignores the request — a bar, a badge — stays inert: Space does
nothing and the root keeps the keyboard, which is the right answer for a cell with nothing in it to
enter.

## The grid's own buttons never hold the keyboard

Keeping the keyboard on the root only holds if nothing else can take it, and two routes could:

- **Tab.** The buttons were in the page's tab sequence, so the root was **not** the grid's only tab
  stop once an Action Column was on screen: Shift+Tab from the element after the grid landed on the
  last row's button. They now carry `tabindex="-1"`.
- **A press dragged off a button** — the platform's way to cancel a click. No click fires, but the
  press had already focused the button, and Enter then fired it natively: the key ADR-0020 forbids,
  on an action the user had just declined. The buttons' `mousedown` default is now prevented, so
  the pointer never focuses them. The click itself still lands — preventing the press's default
  does not stop it — and the grid takes the keyboard back after every action, as it already did.
  *(Superseded 2026-09-26, below: the grid no longer takes the keyboard back after an action.)*

A grid button is therefore only ever *pressed*, never *focused*. A Consumer's control inside a
template is different — it is meant to be focused — but it should carry `tabindex="-1"` for the tab
stop's sake. The core cannot rewrite the Consumer's markup and does not try; the demo does it, and
the rule is stated here so that a Template Column that breaks the one tab stop is visibly the
Consumer's choice.

## An action leaves the keyboard where it was *(amended 2026-09-26, decided with the user)*

The paragraph above ended "the grid takes the keyboard back after every action, as it already did":
after the Consumer's `OnAction` handler returned, the core focused its own root. That was written
when a clicked button kept the keys. Once the buttons' `mousedown` default was prevented, a press
no longer moved the keyboard at all, and the reclaim was left doing one thing only — **taking the
keyboard from wherever it was and bringing it to the root**.

That is the wrong thing whenever the action opened something. The DemoHost's row inspectors
(docs/specs/row-inspectors) measured it: a floating inspector opened by Space, which focuses itself
as a Consumer's panel ordinarily does, lost the keyboard to the grid's root **three runs out of
three on WebAssembly** — a panel that looks usable while the keys go to the grid behind it. Every
other combination passed, but only by order: a `MudDialog` focuses itself late enough to land after
the reclaim, and on the Server host the messages happened to arrive the right way round. A rule
that holds by the order of messages is not a rule.

**Decision: after an action fires, the core moves no focus.** The keyboard is where it was before
the press — on the root, if the user was in the grid; in whatever the handler opened, if it took
the keyboard; wherever else it was, if it was elsewhere. The grid reports the press and does
nothing else, which is ADR-0020's own sentence, now true of the keyboard too.

What is given up: a user whose keyboard was **outside** the grid — in a search field, another
grid, an inspector — who clicks an action button no longer finds the arrows moving the grid
afterwards. They find them where they left them. Little is lost: a press on a button has never
moved the Focus to that row (ADR-0020: the press stops before the Viewport's arithmetic), so the
arrows were always resuming from wherever the Focus already was, and a click on any cell still gives
the grid the keyboard.

Rejected:
- **Reclaim only while the root still holds the keyboard.** Right in every case, but knowing where
  the keyboard is means reading the active element, which is a synchronous read by script — a new
  entry on ADR-0021's allowlist to keep an optimisation nobody needs.
- **Stop reclaiming after Space only.** The smallest change, and the failing case passes — but the
  click path would keep passing by the order of messages alone, which is what this amendment exists
  to stop relying on.

Escape from inside a cell's control is unaffected: there the keyboard is leaving a control the
grid's own cell holds, and handing it back to the root is the point (ADR-0020).

## A held Space engages once

Held down, Space repeats. On a one-action cell each repeat fired the action again: a held Space on a
Delete column raised Delete for the same row once per auto-repeat. The capture-phase gate now
**takes a repeated plain Space and drops it** — `event.repeat` is the browser's to report and the
gate already runs on every keydown, so this adds no listener and no allowlist entry, only a line in
the first entry's filter. One press, one engagement. (Shift+Space and Ctrl+Space name whole regions;
repeating them is idempotent and they are left alone.)

## What is announced

**Nothing new from the grid.** A chosen action is exposed by its accessible name — the label, or
the `aria-label` an icon-only action already carries (ADR-0020) — because `aria-activedescendant`
moves to it; leaving moves it back to the Focus cell. (Exposed is what was checked; announced
awaits a real screen reader, as above.) A Consumer's control is announced by its own
label when it takes focus. The live region stays what ADR-0033 made it: the selection extent and the
Reject, and nothing else.

## Rejected

- **JavaScript focusing the first `.ex-interactive` in the cell.** No work for the Consumer — and a
  sixth allowlist entry bought with convenience. ADR-0021 admits an entry when *Blazor cannot do the
  job*, and here it can, with the Consumer's cooperation. "The first focusable inside a marked
  element" is also a guess about markup a component library controls: MudBlazor puts a component's
  class on a wrapper `div`, so the marked element is not the one to focus.
- **The Consumer handing the core an `ElementReference`** (`@ref="cell.FocusTarget"`). One attribute
  for a plain element, but the core would then hold a reference to a control it did not render —
  what ADR-0030 refuses for a Chrome's editor, for the same reason — and a component library's
  control is not an `ElementReference` at all.
- **Real DOM focus on the core's own buttons**, the roving-focus form of the ARIA grid pattern. The
  native focus ring and native activation come free, but the key gate would need a third kind of
  target (the root, a foreign control, *the grid's own button*) claiming the grid's keys back from
  it; each button would need a captured reference, which Blazor assigns **once per element**, so a
  button the diff reuses for another column after a horizontal scroll would keep the old column's
  reference unless every button were keyed; and every keypress would race a focus move still in
  flight. `aria-activedescendant` is how this grid already says "the keyboard is over there", and it
  says it for a button as well as for a cell.
- **Enter entering a cell** (the pattern's Enter / F2). Enter always moves (ADR-0012, ADR-0020); F2
  is Caret.

## Consequences

- **The Template signature is a breaking change**, taken before any release: `cell.Row` where the
  fragment used to receive the row. Every template now receives the context, so a template that
  later gains a control needs no second overload and no declaration.
- **Interactive is two mechanisms under one name**, and the glossary says so: over the core's own
  actions it is a mode the core holds; in a template it is the Consumer's control holding DOM focus,
  read off the event target exactly as it was for a click. What they share is the contract — Space
  enters, Escape leaves, and nothing fires on Enter.
- **A class joins the stable surface** (`ex-action-chosen`,
  [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)), restated
  under forced colors like every other state.
- **Engaging costs row renders only on the engaged row**: entering, choosing and leaving re-render
  that row; a template request renders the Focus row at most twice, the request and its clearing.

## Verified by

Layer 2: Space on a multi-action cell chooses the first action and moves `aria-activedescendant` to
it; the arrows, Home and End choose, clamped; Space fires the chosen action once and leaves; Enter
and Tab fire nothing and cycle the Focus; Escape leaves without blurring; only the engaged row
re-renders; a template sees exactly one non-zero `FocusRequest`, and a row re-created afterwards sees
none; Space on an editable cell opens Overwrite containing a space; action buttons carry
`tabindex="-1"`. Layer 3 on `/cells`: the same keys, pressed for real, move the chosen action and
fire it; Space puts a real caret in the note field and Escape brings the keyboard back; a held Space
fires a one-action cell once; Shift+Tab from after the grid lands on the root; a press dragged off an
action leaves no button focused, and Enter afterwards fires nothing. *(Added 2026-09-26:)* Layer 2: firing an
action, by pointer or by Space, asks for no focus. Layer 3 on `/inspectors`: an inspector opened by
an action keeps the keyboard, modal or floating, by click or by Space (RI-4 … RI-7); once a modal
closes, the keyboard is back where it was before the press — the grid's root if it was there
(RI-4), not the root if it was elsewhere (RI-26). The dialog gives it back; the grid takes nothing.
