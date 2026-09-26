# The remaining Chrome — column menu, cell editor, loading indicator

Following [ADR-0009](./0009-filter-panel-contract.md), which fixed the filter panel contract,
this settles the remaining three substitutable UI seams (`IGridChrome`). One rule throughout:
**Chrome renders and calls back; it does not decide meaning.**

## Column menu — the core decides the items

```csharp
public sealed record ColumnMenuContext(
    ColumnInfo Column,
    IReadOnlyList<GridCommand> Commands,   // the core decides; Chrome only lays them out
    Action Close);

public sealed record GridCommand(string Id, string Label, bool Enabled, Action Invoke);
```

*(Amended by [ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md):
`Label` is gone. The core naming the commands is what this section argues for — two Chromes must
not offer different items — and it says nothing about what they are called. A label is rendering,
and the core's were English, which no Consumer could change. The wording is resolved from the
`Id` by Chrome; a command is `(Id, Enabled, Invoke)`.)*

**Chrome does not decide what goes in the menu.** Letting it do so would mean the default and the
MudBlazor implementations offering different items, which breaks the premise that substituting
Chrome does not change behaviour.

The core's standard commands: sort ascending/descending, open the filter, hide this column, pin
the column, size the column to fit. **All of them move View State, so `Invoke` ultimately
notifies the Consumer** ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).

**The Consumer can add its own commands.** Something like "open the report for this book" will
come up. It is added by the Consumer, not by Chrome — Chrome still only lays them out.

**What closes a popover — the core decides, as it decides the items** *(settled after the
first manual session found a filter panel that could not be closed at all)*. A column menu or
filter panel closes three ways, and all three are the core's behaviour, identical under every
Chrome:

- the same ▾ that opened it — the button is a toggle;
- Escape, wherever focus sits ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)
  records where that key fits in its layering);
- a pointer-down anywhere else in the instance — which **keeps its own meaning**: the click
  that dismissed the menu still selects the cell or sorts the header it landed on.

Closing any of these ways without OK **discards**, exactly as
[ADR-0009](./0009-filter-panel-contract.md)'s Cancel does — a dismissal is a Cancel the user
did not have to aim at. The `Close` callback in the contexts is what Chrome's own affordances
(an × button, its framework's backdrop) invoke; it is the same discard.

*(Extended on 2026-09-24 by
[ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md): a popover now
opens from the keyboard too, takes DOM focus when it opens through a `FocusRequest` in its context,
fixes what the keys inside it mean for every Chrome, and returns DOM focus to the root however it
closes. A seam's contents may open **Inner Popups** — a select's options, a picker's calendar —
that their design system draws outside the root; ADR-0039 records what each of the three
dismissals does while one is open. The three dismissals themselves are unchanged.)*

## Loading indicator — receive and render

```csharp
public sealed record LoadingContext(bool IsLoading, int PlaceholderRowCount);
```

**Placeholders are one mechanism** (waiting for data and deliberate skipping during fast
scrolling are the same thing,
[ADR-0004](./0004-cap-the-cells-touched-per-frame.md)), so Chrome does not need to distinguish
them either.

## Cell editor — never inside the row; one floating input

An `<input>` must not go inside a row. It breaks row memoisation
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)) and adds DOM weight.
Use **one input floated over the focused cell** — the same idea as the selection overlay
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)), and the coordinate arithmetic can be
shared.

```csharp
public sealed record CellEditorContext(
    ColumnInfo Column,
    object? InitialValue,      // in Caret, the current value; in Overwrite, the character typed
    CellEditMode Mode,         // Overwrite / Caret
    Action<object?> Commit,
    Action Cancel);
```

*(Wiring narrowed this record: the shipped `CellEditorContext` carries the text as `string` —
`InitialText`, `TextChanged`, an argument-less `Commit` — because parsing is the Consumer's
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md), `GridEditIntent`); a typed
`object?` here implied a parse the core does not perform.
[ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md) later added
`string? Error` to the record, so the editor can paint `aria-invalid` while a Reject holds it
open.)*

### Two editing states

Excel has two editing states, and **the same arrow key means different things in each**.

| State | Entered by | Original value | Arrow keys |
|---|---|---|---|
| **Overwrite** | typing straight onto a selected cell | replaced | **commit and move to the neighbouring cell** |
| **Caret** | F2, or a double click | kept, with the caret inside it | **move the caret within the text** |

F2 moves between the two.

**What ends editing by pointer — Excel's click-away commits** *(settled when review found
the editor had no pointer teardown at all: a click elsewhere moved the selection and left
the editor floating over the old cell with stale text, and the next Enter committed that
text onto it)*. A press anywhere that is not the editor — another cell, the header, a
popover-dismissing press — **commits first**, exactly as Excel does, and the press then
keeps its own meaning: the click still selects, the header press still sorts. The editor's
own elements never let a press through to the delegated viewport (its input stops
propagation, like every interactive element standing over that arithmetic), so "not on the
editor" is exactly what reaches the grid's handlers. Escape remains the one way to discard.

**A Reject holds the commit** *(added with validation,
[ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md))*: when the
Column's Edit Verdict rejects the committed text, no commit gesture closes the editor — and the
rejected press does **not** keep its own meaning. Letting the click select while the editor
stands rejected would recreate exactly the stale-editor failure this rule exists to prevent.
The decision to hold is the core's; Chrome still cannot veto.

**AltGr is typing, not a chord** *(same review, same list)*: Windows reports an AltGr
character as Control and Alt held together, and several European layouts type `@ { [ €`
that way. The gates — JS and the C# mirror — admit a printable key with both held, while
either alone stays a shortcut. And **a grid with no editable column claims no printable
keys at all**: the gate is told whether any column edits, so a display-only grid does not
eat the page's keys or round-trip every keystroke.

Many grid products do not implement this and behave as if always in Caret. Continuous entry —
"type a value, arrow to the next cell" — then does not work, and anyone coming from Excel
notices. This component **claims Excel-like operability, and this is what that claim is made of**,
so it is implemented.

Rejected: **Caret only** — simpler, and the arrows always belong to the editor, but continuous
entry does not work. That is lowering the claim.

### The core arbitrates the keys; Chrome is not trusted with it

If the editor is a component such as a `MudTextField`, it handles keys itself. Left alone, the
mode-dependent meaning of the arrow keys cannot work.

**The core sees key input first, in the capture phase.** A capture-phase `keydown` listener is
attached to the grid root, and keys the core should handle are taken there and `preventDefault`-ed.
Only the keys the core decides to pass reach the editor.

| Key | Handled by |
|---|---|
| Esc / Enter / Tab | **always the core** (cancel, commit, move) |
| Arrows / Home / End | **the core in Overwrite** (commit and move); **the editor in Caret** (move the caret) |
| Everything else | the editor |

Bubbling is too late — once the editor has handled the key and moved the caret, it cannot be taken
back. **The capture phase is the point.**

This approach **requires no cooperation from Chrome.** A contract of the form "the editor forwards
keydown to the core" would make behaviour depend on whether each Chrome implements the forwarding,
which breaks the rule above.

## How the capture works, given that `preventDefault` is synchronous

*(Settled while implementing.)* The listener has to decide **now** whether to take a key:
`invokeMethodAsync` is asynchronous, and an answer that came back after the event would be
too late to stop anything. So the decision cannot be a round trip.

It is not Chrome deciding either. **The core builds the set of keys it claims and hands it
to its own listener at attach time**; the listener takes exactly those, prevents their
default, and forwards *the raw event fields*. What the key **means** is resolved back in
C#, from those fields.

```
C#  the table: which keys are the core's, and what each one means   ← the authority
JS  a Set lookup: take it, or let the browser have it               ← the gate only
```

The canonical form (`[Control+][Shift+][Alt+]{key}`, Meta folded into Control) is the one
rule that exists in both languages. Keeping the *meaning* on the C# side is what makes that
duplication safe: if the two ever disagree, the symptom is a key that is taken and does
nothing — visible — rather than a key that means something different in each place.

**A mode change is a different set**, which is exactly the shape this ADR's table above
asks for: in Caret the arrows come out of the set and reach the editor; in Overwrite they
stay in it.

**And the listener only claims a key aimed at the grid itself.** Capturing on the root
means it also sees keys meant for anything focusable inside — a Consumer's control in a
Template Column, one of the grid's own action buttons ([ADR-0020](./0020-action-and-template-columns.md)),
and one day the editor. Taken from there, Space types nothing, the arrows move the
selection instead of a caret and Ctrl+A selects the grid instead of the field's text. So
the guard is `event.target === root`: **with no editor and no Interactive mode yet, "the
focus is on something inside" is the whole of the case where the core does not
arbitrate.** When those modes arrive they refine exactly this line — the core keeps Esc,
Enter and Tab while a descendant holds the focus, and the arrows according to the mode,
which is the table at the top of this section.

**A composing IME is left alone too** (`isComposing`, and the `229` keyCode older browsers
report). Mid-composition Enter, Escape and the arrows choose and commit a candidate; taking
them there breaks typing in any language that needs an IME, and moves the grid under a
half-finished word.

### Keys that follow a mode change are held until it lands *(added 2026-09-25)*

The gate decides from the mode it was **last told**. The mode is C#'s, and C# tells the
listener after the fact. On WebAssembly the telling is in-process and lands before the next
keystroke can; on a Blazor Server circuit it is at least one round trip. Read against a
circuit while designing the Server host, that gap loses keys in the one place this component
cannot afford to:

- **Typing into a cell.** `1` opens Overwrite. `5`, `0`, `0` follow before the listener hears
  "overwrite": it still sees the root in no mode, takes them as editor-opening keys and
  forwards them, and C# — now editing — has no meaning for a printable key and drops them.
  `1500` becomes `1`, and whatever lands once the editor has DOM focus is appended to that:
  `10`.
- **Continuous entry.** `150`, Enter, `200`, Enter. The first Enter is forwarded; the `2`
  typed before its answer lands in the editor that is about to close, and is lost with it.

A number that is not the one typed, on a screen that looks normal, is the failure this
component's first principle refuses. **Decision: while a key that can change the editing
mode is being answered, every key after it is held in the listener, in order, and replayed
against the mode the answer leaves** — and, when the answer is an open editor, until that
editor holds DOM focus: the answer and the render that paints the editor travel separately,
and a key landing on the root in between would find no mode that claims it. A key that can change the mode is one the gate takes
to open the Cell Editor (F2, a printable key, Space) and, while editing, every key it
claims (each commits, cancels, moves or switches). On replay a held key goes through the
gate again: a key the core claims in the new mode is forwarded (and may hold the rest
again); a printable key while the editor stands is typed into it at its caret, as if it had
been pressed there; anything else is the browser's, as it would have been. If the answer is
that nothing opened — the Focus cell is not Editable, say — the held keys replay as ordinary
keys, which is exactly what they would have done had the answer come first.

Plain navigation is not held. An arrow outside editing changes no mode; holding behind it
would pace a held-down arrow key to one row per round trip on Server, for no correctness
gained.

Rejected: **appending on the C# side** — C# receiving the printable keys and adding them to
the editor's text. Keys typed after the editor has DOM focus go straight into its input,
while keys still in flight arrive later and are appended behind them: `1500` can come out
`1050`. Order can only be kept where the keys originate. And **accepting it as a property of
Server hosting** — it is a quietly wrong number.

This stays inside the capture-phase entry of
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md): the listener already decides
per key whether to take it; it now also decides *when* a taken key is forwarded.

**Widened the same day: a mode change is any change of who holds the keyboard.** Layer 3 on
the Server host found the same loss one step over. `Alt+↓` then `↓`, typed together, moved
the Focus instead of the menu's item: the menu takes DOM focus a round trip after `Alt+↓`
([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)), and
the `↓` landed on the root first. So the keys that open a popover from the root — `Alt+↓`,
`Shift+F10`, `ContextMenu` — are mode-changing keys too, and the keys after them are held
until the popover holds DOM focus. They are then handed to the element that holds it, in
order, as the keydown it would have received; what they mean there is the popover's
(ADR-0039's table). The hold ends after two seconds whatever happens, as it does for the
editor, rather than hold keys forever for a popover that never took focus.

*(Added 2026-09-26, with [ADR-0044](./0044-alt-down-opens-one-popover-the-commands-above-the-filter.md).)*
Inside a column's popover the keyboard moves again, from the commands to the filter below them
(Tab, Shift+Tab, E) and back (the sentinels the core renders either side of the filter). Each
move is a change of who holds the keyboard, and the keys after it are held the same way.
*(Decided with the user the same day, after review.)* **So is closing a popover by a key**:
Enter or Space on an item, a column's letter that runs a command, Enter in the filter's text
field. The popover closes a round trip later on a circuit, and a key typed behind it reached
the menu first: O then Enter sorted descending, and then ran the item the Enter stood on,
Sort ascending. The keys behind a closing key are held until the popover is gone and are then
the grid's. The core writes the letters that run an enabled command on the commands
(`data-ex-letters`), and a Chrome marks its value list `ex-value-list`, so the listener mirrors
nothing but Tab, Shift+Tab and E. The two seconds count from the start of the hold, however
many moves it holds behind.
A keydown dispatched from script types nothing, so a held key handed to a text field is typed
at its caret, as a held key is in the Cell Editor, and a held Enter submits the field's form.
A held key whose effect is the browser's own — Tab moving DOM focus, Space ticking a checkbox,
an arrow in a select — does nothing when dispatched from script, and doing it from script would
move DOM focus from script, which [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)
keeps out. *(Decided with the user, 2026-09-26.)* **Such a key, and every key held behind it, is
dropped.** Handing the rest on would put them in a field they were not meant for: "Alpha", Tab,
Space, Enter would search for "Alpha " and apply it. The typing stops short instead, visibly,
and nothing is applied that was not typed where it was meant. It happens only faster than a
round trip, so on a Server circuit.

## Consequences

- **The core carries a small amount of JavaScript.** A capture-phase listener can only be attached
  through JS interop. Reading scroll position and the clipboard need it anyway, so this is not a
  new dependency — and the complete list of permitted JS is fixed in
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md).
- **The editor sits outside the cell, so it will not match the cell exactly.** Its font, line
  height and padding must be pulled from the cell's CSS variables. A mismatch makes the text jump
  at the moment editing starts.
- **The editor's position uses the same coordinate system as the selection overlay.** This is
  another reason the row height must go through a C# parameter — changing it in CSS alone
  displaces the editor.
- **`GridCommand.Id` is a stable identifier.** It is the hook for Chrome to assign icons per
  command, and for a Consumer to substitute a particular one.
- **Do not lose the character typed in Overwrite mode.** The first character is pressed before the
  editor exists. The core holds it and passes it as `InitialValue`.
- **There is a third mode, Interactive**, for cells whose content is interactive
  ([ADR-0020](./0020-action-and-template-columns.md)). It rides on the same capture-phase key
  handling.
