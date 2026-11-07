# A verdict or a refusal may be answered later, and the next input waits for it

*Decided with the user on 2026-10-26, from the survey of how AG Grid and Apex Grid validate an edit,
made in the same project thread. The user's aim is in Context; asked whether the grid's
contract judges the candidate row and may answer later, the user chose that over synchronous declarative
rules only, and over leaving it until after 0.3.0.*

## Context

The user's aim, in their words of 2026-10-26: a grid shows objects that carry domain logic, and a Consumer
wants to refuse an object that cannot exist. Validating that properly, whether the page is Blazor or
React, is the product's value.

What the family offers today:

- **[ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)**: a Column's
  `Validate(row, text)` answers an Edit Verdict, Accept, Flag or Reject, on the commit path, **at once**.
  A Reject holds the editor. The function already receives the row, so a Consumer can build the object
  the edit would make and ask the domain whether it can exist. Nothing in the grid stops that. What stops
  it is the timing: the answer must be there before the function returns.
- **[ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md)**:
  `GridEditIntent.Refuse(message)`, called before `OnEdit` completes, holds the editor as a Reject does.
  The C# core lets `OnEdit` be asynchronous. It holds a gesture made meanwhile until the commit lands, and
  then answers that gesture against what the commit left ("One commit at a time", ED-12).
- **The Script Core** carries `validate` and `refuse`, both synchronous. Its `onEdit` returns `void`, and
  a refusal after it returns throws.
- **Blazor's ExGrid over the Script Core** (ADR-0350 section 7, PR #374) therefore refuses a column that
  declares `Validate`, by name. A handler that calls `Refuse` fails with `NotSupportedException`. On a
  circuit the browser cannot ask .NET anything synchronously: the core has committed before .NET hears
  the edit. A Blazor Consumer's domain lives in .NET, so today it cannot refuse an object that cannot
  exist. It can only flag the object after it has landed.

What others do (read from their sources on 2026-10-26):

- **AG Grid** asks synchronously (`getValidationErrors(): string[] | null`, and
  `getFullRowEditValidationErrors` for a whole row). By default (`invalidEditValueMode: 'revert'`) it
  throws an invalid entry away as the edit ends. `'block'` holds the editor. A paste and a fill write
  without asking. Its only asynchronous road is `readOnlyEdit` with `onCellEditRequest`, where the grid
  writes nothing, and a refusal is simply not applying, with no message and no editor held.
- **Apex Grid** (ApexCharts') asks synchronously too: a Column's `validators` each return a message or
  `null`. An invalid value holds the editor. A paste and a fill ask per cell and apply the rest.
- **Blazor-ApexGrid** cannot declare a validator from C#. It forwards events with a fire-and-forget
  `invokeMethodAsync`, so its `OnCellValueChanging` arrives after the write and cannot veto it. A C#
  Consumer has no validation.

No product surveyed lets a Consumer answer an edit later and still hold the editor. That gap is what
leaves a Blazor wrapper over a script grid unable to validate.

## Decision

### 1. A verdict may be a promise

`validate` may answer an Edit Verdict or a promise of one: in TypeScript
`(row, text) => EditVerdict | Promise<EditVerdict>`, in C# an overload answering `ValueTask<EditVerdict>`.
An answer given at once behaves exactly as today. The grid does not inspect which kind of rule produced
it, and Accept, Flag and Reject keep ADR-0034's meanings.

### 2. `onEdit` may be a promise, and `refuse` holds until it settles

`onEdit` may return a promise. `intent.refuse(message)` may be called until that promise settles, and
holds the editor as a Reject does. This restores ADR-0142's "One commit at a time" in the Script Core. It
is what a Consumer needs to refuse against the newest version it holds: a reducer, a server, a domain
service. `onPaste` takes the same, so that a `GridPasteIntent.refuse` answered later refuses the paste as
ADR-0050 item 3 has it. *(2026-10-27: `onFill`, the fill handle's intent, which reached the Script Core after
this was decided, takes the same, so that a `GridFillIntent.refuse` answered later leaves the Selection on the
source as ADR-0050 item 5 has it. On Blazor it crosses as a paste's answer does (section 5), with the token on
`GridFillIntent.CancellationToken`; a fill refused there no longer fails the component.)*

### 3. While an answer is out, the commit is pending

- **The editor stands, with the text and the keyboard, and takes no input of its own.** It keeps DOM
  focus and its text, and carries `aria-busy` and `readonly`. The root carries a stamp naming the
  pending commit, `data-ex-pending-commit`, which the stylesheet paints: the pointer shows the grid
  busy, and the editor's outline is dashed in the colour and width its Visual Token gives. Nothing is
  applied, and no Edit Intent is raised until the verdict has accepted or flagged the commit.
  *(Implemented in the Script Core on 2026-10-26. This first said "a class of its own … and a stamp".
  The stamp alone marks the root, since one attribute both says that a commit is pending and names
  it, and the root's class list is written by every pass.)*
- **The pending state starts in the commit gesture's own handler.** The Script Core takes the gesture
  in the browser, synchronously, and marks the commit pending before that handler returns, on every host.
  So any later input's handler sees it. This includes the next keydown, however fast it follows. On
  Blazor the answer crosses the circuit, but the pending state never does.
- **Every input made meanwhile waits in the Input Order**
  ([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md) sections 1 and 3). This
  covers a key, a press, a typed character, and a clipboard key. The root's capture listener holds
  each one: a key's default is prevented, so a character never reaches the read-only field natively, and
  a composition begun meanwhile is held whole and taken as its text. Each is taken after the answer, against
  what the answer left. Typed fast or slow, `150` Enter `200` Enter therefore ends with the same cells,
  whatever the answer and however late it comes (spine rule 6). Under a Reject, the Enter that followed
  is taken in the still-open editor and asks again, as it would have done after an immediate Reject.
  *Note of 2026-11-05:* what a held key's default would have done in a field (a character typed, a deletion, a paste,
  a composition's text) is done through the browser's own editing command where the field holds the keyboard, so it
  joins the field's undo history as the same input typed does, run for run, and the field sends its `change` when it
  is left. It was done with `setRangeText`, which joins no history, and a `Ctrl+Z` after held typing undid nothing where
  the keys typed one at a time were undone (ADR-0451, the second widened night). A field that does not hold the
  keyboard, which no typing reaches either, is still written from script.
  *Note of 2026-11-07:* the field's undo history is no longer what an open edit's `Ctrl+Z` walks. The browser closes a
  field's typing run whenever anything else in the document is removed or has its text changed, so the run still ended
  wherever the page happened to repaint beside the typing: typed at machine speed, `7.5Bravo 2` undid to `7.5`, the
  cells of the commit before it repainting between the `5` and the `B`, where typed one key at a time it undid to `7`
  (ADR-0451, the fourth widened night). An open edit now keeps its own history, one over the Cell Editor and the
  Formula Bar (ADR-0051's one text in two places): characters typed one after another at the caret are one step, and
  so are deletions one after another; a change of kind, a caret moved between two inputs, a paste, a drop, a cut, a
  composition and text the grid writes itself (a Reference pointed at, a completion) are each a step of their own;
  one pointing, however far its outline moves, and one drag of a Reference's grip are one step, and one cancelled
  leaves none; the text the edit opened on is not undone. `Ctrl+Z` and `Ctrl+Y`, typed, held and taken again, or asked by the
  browser's menu (`beforeinput`'s `historyUndo` and `historyRedo`), walk that history, and the field's own is never
  asked. The keys are taken as they are pressed, not heard through `beforeinput`: the browser asks only while its own
  history has a step, and after an undo of the grid's its redo has none. What a held key types still goes through the browser's editing, as the note above has it. ADR-0007's last
  bullet, "Ctrl+Z inside the editor undoes uncommitted typing", is unchanged: this says which typing.
- **An answer is judged against what the commit was taken against.** A row that left, a Source
  replaced, or an order that moved while the answer was out refuses the commit with ADR-0142's reasons.
  The verdict is not consulted again.
- **Escape cancels a pending commit: a second input that acts ahead of its turn**, beside ADR-0240
  section 12's and on its terms. What decides is what the Escape's keydown sees, never when the answer
  comes. An Escape whose keydown sees the pending stamp cancels. The `AbortSignal` (C#:
  `CancellationToken`) handed to the verdict and to `onEdit` is aborted, and the editor stays open with
  the text. The Escape's own turn then closes nothing. If the answer had already come when the Escape's
  turn arrives, the cancel finds it landed and says so. Both outcomes are said aloud: cancelled, or
  already answered. An Escape whose keydown sees no stamp is an ordinary key. Since the stamp is set in
  the commit's own handler (above), an Escape typed after the commit always sees it. Without this, a Consumer that never answered
  would hold the grid, and the grid will not end it on a clock (ADR-0240 section 4).

### 4. A domain object is judged by the Consumer composing it

The grid does not compose the object an edit would make. It has no constructor for the Consumer's type,
and ADR-0007 keeps applying an edit the Consumer's job. The Consumer composes the object inside its
verdict, with C#'s `row with { … }` or TypeScript's spread, and asks its domain. **A Reject on a domain
invariant is legitimate.** ADR-0034's "a row-dependent judgement is Flag-tier" was a convention, stated as
such, and its reason was that the verdict had to be cheap and immediate. Under section 1 it need not be.
The Docs Site's Editing page says so, with an Example of each product: a domain type refusing to be built.

A fill still asks the verdict once, against the row the editor was opened on (ADR-0034). That single
question was sound only while a Reject could not depend on the row. **A domain Reject can, so the verdict's
Accept for the editor's row says nothing about the other rows of a fill.** Those rows are judged where the
whole target is visible, in `onPaste`, which a fill raises like a paste. A Consumer whose domain judges
rows applies there only the objects that can exist. For the rest it either refuses the fill whole, now
possibly later, or keeps their old values and flags them with the text it could not apply (ADR-0034's
partial apply). It never applies an object that cannot exist. A clipboard paste still asks no verdict
(ED-18) and is judged the same way in `onPaste`. The Docs Site's domain Example shows a fill over rows
where some objects cannot exist.

### 5. Blazor carries it over the circuit, React passes it through

- **Blazor's ExGrid** carries `Validate` and `OnEdit`'s `Refuse` as pending answers. The browser numbers
  the commit, .NET judges it and answers by that number, and the core takes the answer. The checks that
  throw `NotSupportedException` for them go. A commit costs one round trip, and the input behind it waits
  for that round trip, as it waited on the C# core. What crosses per commit is recorded with section 7's
  measurement.
- **The display channels for a Flag**, `CellStateOf` and `CellMessageOf`, are carried too: a Flag
  without its paint would be a commit quietly marked as nothing (spine rule 1). *(2026-10-26: a cell's
  message is asked on demand (ED-17), so on Blazor it is a round trip too: the Script Core's `cellMessage`
  may answer with a promise, the message opens once answered if its cell is still the one settled on, and
  the grid is not settled meanwhile. A state crosses with its row.)*
- **React's adapter** passes the function through. A synchronous rule stays synchronous, and nothing
  crosses a wire.

## Considered options

- **Synchronous declarative rules only** (required, a range, a pattern, a length), serialized into the
  browser, as Apex Grid's `validators` are. They answer at once on every host. But a Blazor Consumer then
  cannot refuse on its domain, only flag after the fact, which misses the aim. *Recommended against.*
  Declarative rules may still come later as a convenience over section 1. They are not needed for it.
- **Commit at once and reopen the editor on a late Reject.** No input waits, but the outcome then depends
  on when the answer arrives. The Selection moves away and then is pulled back, and keys typed in between
  land in a different cell. That breaks spine rule 6. *Refused.*
- **The grid composes the candidate object through an `apply(row, column, value)` seam**, as AG Grid's
  full-row `editorsState` and Apex Grid's staged row do. It adds a second seam for what the verdict already
  receives the means to do, and it makes the grid hold a hypothetical row. ADR-0034 refused that for the
  Overlay. *Refused.*
- **Leave Blazor without validation until after 0.3.0.** No work, but Blazor's ExGrid would ship weaker
  than the C# core it replaces, which validates today. *Recommended against.*

## Consequences

- ADR-0034 gains a dated note: a verdict may be answered later, and a domain Reject is legitimate.
- ADR-0142's "One commit at a time" holds in the Script Core, with a note.
- ADR-0240 section 12 gains a note: Escape on a Pending Commit is a second input that acts ahead of its
  turn, on its terms.
- ADR-0350 section 7 gains a note: Blazor carries `Validate` and `Refuse` as pending answers.
- `CONTEXT.md` gains **Pending Commit**.
- The Definition of Done gains ED-32 to ED-35 (the promise verdict, the waiting input, Escape's cancel,
  Blazor's round trip), and ED-12 and ED-14 point here.
- The Script Core's `onEdit` and `onPaste` types widen to `void | Promise<void>`. That is not
  breaking for a TypeScript caller. Blazor's `Validate` gains an asynchronous overload.
- The Docs Site's Editing pages, for ExGrid in both frameworks, say what a pending commit looks like and
  show a domain refusal. ExSheet's and ExPivot's pages say whether they offer it.
