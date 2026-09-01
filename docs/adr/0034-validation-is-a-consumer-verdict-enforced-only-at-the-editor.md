# Validation is a Consumer verdict; the grid enforces it only at the editor

The grid gains cell validation without gaining a judgement of its own. A Column may carry a
Consumer-written validate function; at the moment of committing, the grid asks it for an
**Edit Verdict** — **Accept / Flag / Reject** — and enforces the verb without knowing the
reason. [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)'s line is untouched:
the grid still applies nothing and holds no committed value. What is new is *scheduling* —
the Consumer's judgement runs at the one moment a veto is still possible, while the editor
is open.

## The seam: a verdict per Column, not a rulebook in the grid

```csharp
// on GridColumn<TRow>
public Func<TRow, string, EditVerdict>? Validate { get; init; }

// EditVerdict: Accept | Flag(string message) | Reject(string message)
```

- The delegate receives **the current row instance and the committed text**, so in-row
  correlation ("the break date must precede maturity") is expressible — the row carries the
  other values. This is the same shape as the value accessor: a Column holding a Consumer
  function is established practice, and executing one is not the grid judging.
- **The grid learns only the verb.** Which failures Reject and which Flag is the Consumer's
  convention, not a classification the grid enforces. The intended split — and the reference
  implementation's — is: **an unparseable text Rejects; a parseable value a rule dislikes
  Flags.** A typed Row Model has nowhere to put `abc` committed to a date column
  (accepting it would oblige the Overlay to hold raw text instead of typed values; Excel
  gets away with accept-everything only because its cells are variants), while a date the
  business dislikes fits the model and can be applied wearing its error.
- The verdict runs synchronously on the commit path, so it must be cheap. Anything that
  needs a round trip — uniqueness against a server — is Flag-tier by construction: it
  reports after the fact through the display channels below.

Rejected: **one grid-level delegate** `(row, column, text) → verdict`. It centralises the
rules but turns every per-column definition into one switch, and the Column already owns the
per-column knowledge (type, accessor, format). Rejected: **a veto-only `CanCommit` hook**
returning `bool` — it saves one concept but loses the message, which then needs a second
channel anyway.

## Reject: the editor holds

Every commit gesture stops — Enter, Tab, the Overwrite arrows, click-away. The editor stays
open with the focus in it and the error shown; **Escape remains the only way out without
applying.** This is Excel's *Stop* style, and the product claims Excel-like operability.

- **A rejected press does not keep its own meaning.** [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s
  click-away rule lets the dismissing press still select or sort; under a Reject it must
  not, or the selection walks away from an editor that is still standing — exactly the
  stale-editor failure that rule was written to prevent. ADR-0010 is amended accordingly.
- **Chrome still cannot veto.** `CellEditorContext` gains `string? Error` so the editor can
  paint `aria-invalid` and its own border, but `Commit` stays argument-less and the decision
  to hold is the core's — *Chrome renders and calls back; the core decides meaning*.
- The message is shown by the same popover machinery as a flagged cell (below), anchored to
  the editor's box — the shape [ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)
  already sketched.

Rejected: **click-away as an implicit Escape** (keyboard gestures hold, a stray click
abandons). Gentler, but a slipped click then discards input silently — the quiet loss the
first principle exists to forbid.

## Flag: applied, wearing its mark

A Flag verdict raises the Edit Intent **unchanged** — the flag does not travel in the
intent, just as Cell State does not travel through the Window (ADR-0007). The Consumer
(typically the bundled ruleset below) applies the value and answers `CellState.Error` plus a
message through the display channels. The screen then shows the value the user actually
entered, marked as wrong — loudly wrong, never quietly.

## The message travels the Cell Metadata road

The error text is **asked for on demand**, by (row, column), only at the moment the popover
opens:

```csharp
[Parameter] public Func<TRow, GridColumn<TRow>, string?>? CellMessageOf { get; set; }
```

- `CellStateOf` is consulted every render for every cell, so it stays a cheap enum; the
  message — read for at most one cell at a time — is a separate, lazy question. This is
  Cell Metadata's idiom verbatim: *not stored on the cell; asked for by (row, column)*.
- **The popover opens on Focus (after a short delay, so continuous arrow movement stays
  quiet) and on hover.** Keyboard reachability is the point of the Focus trigger — this
  design makes the keyboard a first-class citizen, and an error only a mouse can read would
  not be. It never shows permanently: the paint is the standing display, and forty popovers
  after a bulk paste would bury the screen.
- **A validation message is Cell State plus a popover, never an element in the row**
  ([ADR-0013](./0013-fixed-row-height.md)) — the row's height does not move. The popover is
  rendered through a Chrome seam, like the filter panel; the core decides when it opens and
  closes and supplies the text.

Rejected: **widening `CellStateOf` to `(state, message)`** — one seam instead of two, but
every cell pays message-construction cost on every render for text almost never read, and it
breaks every existing `CellStateOf`. Rejected: **a pushed error dictionary** keyed by row
identity — the grid does not know identities outside the Window, and push is the wrong
direction for a vocabulary that is otherwise entirely pull.

## Row-wide rules live with the Consumer — implementation bundled

A rule spanning the row ("start before end") is evaluated against the row **after the edit
is applied** — a row that, at commit time, exists nowhere the grid can see, because applying
is the Consumer's (ADR-0007) and the grid cannot compose a hypothetical. So row rules are
**Flag-only by construction**, and they live on the Consumer side, in a **bundled ruleset
helper**: ownership and driving the Consumer's, implementation shipped with the library —
the same reasoning as the undo stack (ADR-0007) and `GridSource`
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). The helper bundles
the column rules and row rules, evaluates on apply, and answers `CellStateOf` and
`CellMessageOf`, so one implementation runs whether a change arrived by single edit, by
paste, or by program code — the single-implementation principle from ADR-0007 again.

A `DataAnnotations` / `EditContext` bridge is a thin Consumer-side adapter over `Validate`
and the ruleset — `Validator.TryValidateProperty` wrapped in a delegate. It adds no
dependency to the core and the core never references it.

Rejected: **a `RowValidate` seam on the grid**, symmetric with `Validate`. Flag-only means
the grid would gain nothing but a seam — plus the evaluation timing and cost, which on the
Consumer side resolve naturally to "when the Overlay is applied".

## A clipboard paste is never rejected on value

The shape refusals of [ADR-0014](./0014-paste-shape-rules-and-selection-count.md) are
unchanged, and the grid raises the paste intent **without consulting any verdict function**:
a veto is a property of an open editor, and during a clipboard paste none is open. Excel
behaves the same way — paste goes through data validation and the invalid cells are circled
afterwards.

*(The heading said "a paste" until [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)
made a Ctrl+Enter fill a 1×1 paste. The word had to narrow: the rationale above is about the
clipboard, and it does not reach a fill — see the next section.)*

## ...but a fill is a commit, and is judged

A fill would sit on the wrong side of that rule by construction, and must not. The reason the
clipboard is exempt is that a veto belongs to an open editor and during a paste none is open.
**During a fill one is.** The value is a single value the user typed a moment ago, in an editor
still standing — the very moment this ADR chose to ask.

Left unjudged, the gate would point the wrong way: `abc` typed into a date column and committed
with Enter Rejects and reaches one cell, while the same keystrokes with Ctrl+Enter would reach
three hundred cells unexamined. The wider operation would carry the weaker gate.

- **The verdict runs once**, against the row the editor was opened on, with the text the editor
  holds. It cannot run per row: a fill's target legitimately covers rows outside the Window and
  rows not yet fetched (ADR-0014), so for most of them there is no row instance to pass.
- **That representative evaluation is sound for the only tier that can stop a fill.** The
  intended split above makes Reject the unparseable-text tier, and unparseable does not depend
  on the row — `abc` is not a date anywhere. Row-dependent judgements are Flag-tier, evaluated
  after apply, per row, by the Consumer's ruleset, exactly as for a clipboard paste. The split
  stays the Consumer's convention rather than something the grid enforces; a Consumer that
  Rejects on a row-dependent rule gets one row's answer applied to the whole fill, which is its
  own choice to make.
- **A Reject holds the editor and raises nothing** — no paste intent, no cell written — the same
  hold as any other commit gesture (ED-15).
- **The Refusal is asked first, the verdict second.** A Refusal judges the operation and never
  the value, so if the operation is refused there is nothing to ask about the value; the
  editability refusal keeps its place at the head of the order (ADR-0035).

Rejected: **leaving the fill unjudged**, the literal reading of the previous section. It is
defensible only while a fill is thought of as a paste, and a fill is a commit that happens to be
expressed as one.

Rejected: **downgrading a Reject to a Flag for a fill**. It dodges the representative-row
question, but a Reject means the text cannot be a value at all; applying it anyway would oblige
the Overlay to hold raw text, which is the thing the Reject tier exists to prevent.

The bundled helper's default for a payload with unappliable cells is **partial apply plus
Flag**: every cell that parses is applied; a cell that does not keeps its old value, is
painted `Error`, and its message carries the text that could not be applied ("could not
apply 'N/A'"). Fixing three cells by hand beats fixing the source and re-pasting three
hundred.

Rejected: **all-or-nothing**, the symmetry with
[ADR-0005](./0005-copy-refuses-rather-than-truncates.md)'s copy refusal. The symmetry is
false: a partial *copy* is quietly wrong — the loss is invisible at the destination — while
a partially applied paste is **loudly** wrong on the screen the user is looking at, which is
what the first principle actually demands.

## Reject is not Refusal

The line falls on **what is being judged**. A **Refusal** is the grid's own "no", raised on the
**operation** — its target, its shape, its size: caps, misalignment, a paste shape, a target
covering a column that is not Editable. It never looks at the value being written. A **Reject**
judges **the value**, and is the Consumer's *wrong*, relayed and enforced by the grid. Keeping
the words apart keeps Definition of Done ERR-1 — every refusal names which rule it is — worth
stating.

The axis is not decorative: it decides behaviour. A Reject holds the editor and stops *every*
commit gesture, because the value is what is wrong and the editor is where a value is corrected.
A Refusal stops only the operation it named — so a fill refused for covering a non-editable
column holds the editor too, but leaves the single-cell Enter alone
([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)).

*(This section replaced an earlier one that drew the line as the grid's own structural* cannot
*against the Consumer's* wrong. *ADR-0035 broke that reading by adding a refusal that enforces a
Consumer's declaration — `Editable` — and is, in its own words, a "may not" where the others were
a "cannot". Who is judging no longer separates the two; what is judged still does.)*

## Consequences

- `CONTEXT.md` gains **Edit Verdict**.
- ADR-0010 is amended twice: the shipped `CellEditorContext` (already narrowed to text-only
  during wiring) gains `Error`, and the click-away rule gains its Reject exception.
- The Definition of Done gains ED-14…ED-18, and ED-12 gains the Reject clause. Until the
  verdict seam, the message channel, the popover and the bundled ruleset are built, those
  criteria stand failing — that is the intended state of an accepted, unimplemented
  decision.
- The Overwrite flow acquires its one interruption: continuous entry stops only on a value
  that could not be a value at all. That is the trade this ADR makes, with Excel as the
  precedent that users already navigate it.
