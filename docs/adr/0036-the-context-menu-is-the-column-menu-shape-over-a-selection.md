# The context menu is the column menu's shape, aimed at a selection. Right-click moves the Focus first

A secondary click opens a menu whose items the core decides and the Consumer extends, exactly as
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) settled for the column menu. Almost
all of this ADR is derivation; three things were genuine choices, and a fourth was a contradiction
this decision could not be written on top of.

## What derives, and from where

| Settled by | And so |
|---|---|
| ADR-0010: *Chrome does not decide what goes in the menu* | the core owns the item list. Two Chromes offering different items would break the premise that substituting Chrome does not change behaviour |
| ADR-0010: *the Consumer can add its own commands* | "open the pricing run for this row" is a Consumer command. It was already the worked example there — "open the report for this book" |
| ADR-0010: the three ways a popover closes | the same three, unchanged: the gesture that opened it, Escape, and a pointer-down elsewhere **which keeps its own meaning** |
| ADR-0018 | one menu per instance, ids instance-prefixed |
| ADR-0021 | **no new JavaScript.** `@oncontextmenu:preventDefault` is a Blazor attribute, not a listener the grid installs; the allowlist stays at four |
| ADR-0013 | the menu is a popover, never an element in a row: no row height moves |

Cells are `pointer-events: none` and the Viewport is the event target, so the menu binds where
every other pointer gesture already binds.

## Right-click moves the Focus onto the cell it lands on

The component deliberately left this blank — *"what a secondary click does to a selection is Excel
behaviour that has not been specified here, and inventing it would be worse than leaving the
selection alone."* A context menu is what makes it answerable, because now the click has a
consequence.

**Outside the selection, the press moves the Focus and collapses the selection onto that cell,
then the menu opens. Inside it, the selection stands.** This is Excel, and the product's claim is
Excel-like operability — but the reason to copy it here is narrower than the precedent. **The
commands act on the selection, so the selection has to be the thing the user can see when they
choose one.** A menu offering "Copy" over a rectangle scrolled off-screen, from a click somewhere
else entirely, is the shape of quiet wrongness this project refuses first: it would do exactly
what it said, to something the user was not looking at.

Rejected: **leaving the selection alone**, the smaller change. It buys nothing and pays in a
command whose target is invisible. Rejected: **opening only inside the selection** and letting the
browser's own menu appear outside it — safe, and reads as a component that is broken in half.

## What a command is handed: coordinates, not rows

```csharp
public sealed record ContextMenuContext<TRow>(
    TRow Row,                                  // the row clicked — inside the Window, so it exists
    ColumnInfo Column,
    IReadOnlyList<SelectionRange> Selection,   // index space (ADR-0011)
    int RowSequenceVersion,
    IReadOnlyList<GridCommand> Commands,
    Action Close);
```

The clicked cell arrives as a **row instance**, because a click is inside the Window by
construction. The selection arrives as **rectangles in index space and the version they are
written in** — the shape [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)
already defines and `GridPasteIntent` already carries.

It does not arrive as rows, and cannot. A selection legitimately covers rows outside the Window
and rows never fetched ([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)); five
thousand selected rows are five thousand identities the grid does not hold. **The Consumer does
hold them** — it owns the data (the spine's third rule), so resolving an index into a row is its
question, asked of the source it already has.

Rejected: **resolving the rows before opening the menu**, the way
[ADR-0005](./0005-copy-refuses-rather-than-truncates.md)'s copy fetches what it needs. Copy pays
that cost because the user asked for the data; here the user has asked for a *menu*, and a
right-click that fetches five thousand rows before anything appears is a fetch nobody requested.

Rejected: **handing over the rows that happen to be in the Window.** It is the cheapest and it is
the one that is quietly wrong: a command written for a five-thousand-row selection would receive
forty rows and succeed.

## The core's own commands, and the string that should not have been there

The core's commands here are the clipboard's, because the grid is the only thing that can build
that payload — the copy cap, the misalignment refusal, the Window-coverage check and the rule
that `####` never reaches the clipboard are all ADR-0005's and none of them are reimplementable
from outside.

Writing them exposed a contradiction that predates this ADR. ADR-0010 gave
`GridCommand` a `Label`, and the core fills it in — `"Sort ascending"`, `"Hide this column"` —
while [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md) states that **the grid
holds no UI strings** and leaves a refusal's wording to Chrome. Both cannot be true, and the
second is the one this project keeps saying.

**Decision: a command is an `Id`, an `Enabled` and an `Invoke`. The wording is Chrome's, resolved
from the `Id`.** ADR-0010's reason for core-owned commands is that two Chromes must not offer
*different items*; it says nothing about what they are called, and a label is rendering, not
meaning. The practical half matters as much: the core's built-in labels are English, and a
Consumer shipping to Japanese readers cannot presently change them. `GridCommand` loses `Label`,
and the built-in menu resolves text through the same seam a substituted Chrome does — the English
table becomes the default Chrome's, which is what "Chrome renders" already meant.

Rejected: **narrowing ADR-0035's sentence** to permit command labels while forbidding message
prose. It is defensible — a label is a closed vocabulary, a message is composed — and it leaves
translation impossible without replacing Chrome wholesale.

## Consequences

- **ADR-0010 is amended**: `GridCommand` is `(Id, Enabled, Invoke)`. Its one render site and the
  core's built-in command labels move behind the label seam.
- **ADR-0035's "the grid holds no UI strings" becomes true** rather than aspirational.
- **`CONTEXT.md` gains Context Menu**, and Command keeps the meaning ADR-0010 gave it.
- The Definition of Done gains the criteria for the trigger, the Focus move, the context's
  contents and the absence of new JavaScript.
- A Consumer command that acts on a large selection does its own resolution, and the grid's
  refusal rules do not protect it. That is the same bargain ADR-0001 makes everywhere.

## Open

- ~~Copy with headers~~ — **decided in [ADR-0005](./0005-copy-refuses-rather-than-truncates.md)**,
  which gained two sections for it: the header row is the column's `Header`, needs no new refusal
  in either orientation, reaches both formats, and **counts against the copy cap**; and a menu
  copy always takes the asynchronous clipboard route, because clicking a menu item fires no
  `copy` event. Neither is implemented.
- **The keyboard trigger** (`Shift+F10` and the Context Menu key) and where the menu opens from —
  the Focus cell's box, presumably — sit inside ADR-0012's key layering and have not been placed
  in it.
- ~~The label seam's shape~~ — **settled while implementing**: a `CommandLabel` delegate on the
  component, `id → string?`, consulted by the built-in menu and falling back to a
  `BuiltInCommandLabels` table that belongs to the default Chrome rather than to the core. It is
  not a member of `IGridChrome`: a substituted Chrome already receives the ids and resolves its
  own wording, and the seam exists for the Consumer who keeps the built-in menu and wants it in
  their own language. An unknown id renders as the id — a Consumer command reaching the built-in
  menu unlabelled says what happened rather than showing a blank.
