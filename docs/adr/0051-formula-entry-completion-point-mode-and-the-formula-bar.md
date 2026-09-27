# Formula entry: completion, Point mode, and a Formula Bar inside the grid's root

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet —
[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md). The
question that shaped the last part was "where does a formula bar even go, in a browser?")*

Typing a Formula into a bare `<input>` is typing it blind. Excel gives four aids, and ExSheet
takes all four: **completion** of function names, the **argument hint**, **Point mode**, and the
**Formula Bar with its Name Box**. A user who reaches for any of them and finds it missing
concludes the sheet is broken. Every aid is built as ExGrid mechanism that a Consumer switches on
and supplies meaning to, in the pattern of
[ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md). The grid does not know what a Formula is.

## The Cell Editor opens on the Entry

A cell holding `=A1*2` shows its Value, say 84. Editing it has to start from `=A1*2`.
**A Consumer can supply the text the editor opens on**, per cell, and ExSheet supplies the Entry.
Without it, the editor opens on the value, as it does today.

## Completion and the argument hint

- **The core reports the editor's text and caret to the Consumer as the user types**, when the
  Consumer asks for it. The Consumer answers with **candidates** (`SU` → `SUM`, `SUMIF`, …; the
  engine's declared functions and the Linked Tables' names) and with an optional **hint** (`SUM(`
  → `SUM(number1, [number2], …)`). ExSheet is the one that knows a Formula's grammar.
- **Chrome paints both, and the core decides what the keys do**, the split
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) makes everywhere. The list is an
  Inner Popup of the editor ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md))
  and stays inside the grid's box ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)).
  While it is open, ↑/↓ choose, Tab accepts (Excel's key), and Escape closes the list before it
  cancels the edit.

## Point mode

In Excel, while a Formula is being typed at a place where a Reference can go (after `=`, an
operator, `(` or `,`), the arrow keys and the mouse **point**. They move an outline over the Sheet
and write its Reference into the Formula, where otherwise they would commit the edit and move.
`=` ↓ ↓ gives `=A3`. Shift extends the outline to a range. Typing an operator ends pointing, and
F2 switches between moving the caret and pointing.

- **Point is a fourth editing state, beside Overwrite, Caret and Interactive.** In it, arrows and
  clicks move the pointing outline, which is painted in the selection overlay
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). The Selection and the Focus do not
  move: the cell being edited stays where it is.
- **Whether the caret is at a place where a Reference can go is the Consumer's answer.** The
  Consumer supplies a synchronous predicate over the text and the caret. ExSheet answers it from
  its parser. **The Reference text written for the pointed range is the Consumer's too.** The grid
  knows positions, and `B7:C9` is a sheet's name for them.
- **Without the predicate, nothing changes.** Overwrite and Caret keep the meanings
  [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) gives the arrow keys.

## Every decision at a keystroke is made from the text that keystroke carries

On a Server circuit, the answers above can arrive after the user has typed further. If the grid
decided an arrow's meaning from the last answer it had, a fast typist would get pointing where
the text no longer allows it. **So the key message carries the editor's current text and caret,
and the core decides from those**, with the predicate answering synchronously in C# on the same
side as the key handling. A candidate list that comes back for text that has since changed is
dropped, not shown.

Carrying the text and caret needs nothing new in JavaScript. The capture-phase `keydown` listener
already runs for this key ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). It adds
the value and caret of the input it is capturing for, which it reads at no cost. This is not a
measurement: no layout is read, and nothing is synchronous on the path to a paint. The text sent
as the user types goes by a Blazor input event. That is one message per keystroke, about ten a
second, which is not ADR-0021's per-frame wire trip.

## The Formula Bar and the Name Box

A Formula's cell shows its Value, so **the Formula Bar is the only place the Entry can be read
without editing the cell.** [ADR-0016](./0016-column-width-and-overflow.md) already asked for "the
focused cell's full value at all times. The same role as Excel's formula bar".

- **The Formula Bar is an ExGrid band, inside the grid's root, above the header.** A Consumer
  switches it on. It holds the **Name Box** (where the Focus is: the Consumer supplies the label,
  so `D200` on a Sheet) and the Focus cell's text (the Entry on a Sheet, the full value on a
  display grid, which is ADR-0016's display).
- **It is the Cell Editor's second surface: there is one uncommitted text, shown in two places.**
  Typing in either updates both, and committing from either commits once. The uncommitted text is
  still the grid's ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). The bar
  gives it somewhere else to be seen.
- **Typing an address into the Name Box** moves the Selection and the Focus there. The Consumer
  resolves the text to a position and asks the grid to place them (ADR-0050, item 4).
- **Its height is geometry, resolved in C#** with the rest of the Grid Metrics
  ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)), and the rows take
  what it leaves. A Consumer that does not switch it on gets no band.

**Why inside the root.** Three existing rules all hold there without being touched:

- The key listener is on the root ([ADR-0018](./0018-multiple-instances-must-be-independent.md)),
  so the bar's keys are captured first, and Point mode works from the bar as it does from the cell.
- Popovers stay inside the grid's box (ADR-0040), so the completion list under the bar is bounded
  the same way.
- The uncommitted text never leaves the component that holds it.

### Considered options

- **A separate component the Consumer places anywhere, such as the application's toolbar.** It
  was rejected. The bar would sit outside the root, where the key listener cannot see it. Point
  mode from the bar would cross two roots. With two grids on a page, which one the bar follows
  would have to be wired by hand, which is the kind of coupling ADR-0018 rules out. If a Consumer
  asks for this placement, that request is the trigger, and it comes with its own ADR.
- **No bar: a Cell Editor that grows for long Formulas, and a tooltip for the Entry.** This was the
  cheapest option, and it was rejected: a user could not read a Formula without starting to edit
  it.

## Consequences

- **`CONTEXT.md`'s editing states become four**: Overwrite, Caret, Interactive, and **Point**.
- **ADR-0012 and ADR-0021 each gain a note**: the arrow keys' meaning while pointing, and the text
  and caret riding on the allowlisted `keydown`.
- **The Chrome seams grow**: the completion list, the argument hint, and the Formula Bar's two
  fields. Each is painted by Chrome from what the core hands it, under the built-in Chrome and
  `ExGrid.MudBlazor`'s alike ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).

## Added while building *(2026-09-27, decided with the user)*

**Keys held while a mode change is in flight go to the surface that has DOM focus.** The
capture-phase listener holds keys typed before the round trip that changes the editing mode has
returned, then types them into an editor. It used to type them into the first `.ex-editor` in the
markup, which is the cell's editor even when the user was typing in the Formula Bar. Characters
could then land at the wrong caret. The listener now types them into the editor surface that holds
DOM focus. This is the same allowlisted listener doing the same job
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).
