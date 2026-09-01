# Paste and fill respect the Editable declaration. A target covering a non-editable column is refused whole

`GridColumn.Editable` decided one thing only: whether the Cell Editor opens on a cell
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md),
[ADR-0020](./0020-action-and-template-columns.md)). Every other way of writing walked past it. A
paste consulted the shape rules and nothing else
([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)), and Ctrl+Enter fill went through
the same rules as a 1×1 source — checking editability only of the **one** cell the editor was
opened on, then writing the whole selection.

So a column declared non-editable could be overwritten by selecting across it and pasting. The
declaration held on the path the user could see and failed on the path they could not.

**Decision: a paste or a fill whose target covers even one non-editable column is refused whole,
with a reason of its own.** `Editable` now means *a write may land here*, not merely *an editor
may open here*. Copy is unaffected — it reads.

## Why the whole target, and not the editable part of it

Partial application is the option that looks generous and is the one this component may not take.

- **A partial paste is quietly wrong** (the spine's first rule). The cell count in the status area
  says what was selected; the number written would be smaller, and nothing on screen would say
  which cells were dropped. That is precisely the disagreement ADR-0014 refused to allow when it
  dropped "range → 1 cell".
- **The tiling would move.** A source tiles from each range's top-left by modulo arithmetic. Skip a
  column and the remaining columns either shift (wrong values, silently) or leave a gap (the
  source no longer divides the target). Neither is expressible in a `PastePlan`.
- **`PastePlan` may not hold a per-cell list.** It is ranges plus modulo arithmetic on purpose
  (ADR-0014), so that a whole-column target of a million rows stays one rectangle. Column-skipping
  is representable only as an enumeration of cells, which is the shape that ADR-0014 forbids.

Refusing whole keeps all three: the count means what it says, the arithmetic is unchanged, and the
plan stays compact.

## This is a "may not", where the others were "cannot"

The four existing paste refusals are all statements about shape — the grid *cannot* express the
operation without spilling outside the selection or writing something ragged. This one is
different in kind: the operation is perfectly expressible, and the Consumer has declared that it
must not happen.

That difference shows up in the **order of the checks**. Editability is tested before the shape
rules, immediately after the empty-selection test:

```
EmptySelection  →  TargetNotEditable  →  the shape rules (ADR-0014), unchanged
```

The shape refusals carry advice — "reselect a target of the same shape and it will work"
([ADR-0014](./0014-paste-shape-rules-and-selection-count.md), ERR-2). On a target that covers a
non-editable column that advice is a lie: no reselection of that shape will ever be accepted. The
declaration has to be reported first, or the grid tells the user to try something that cannot
succeed.

Empty selection still outranks it: with nothing selected there is no column to judge.

## Paste and fill go through one gate

Ctrl+Enter fill is a paste of a 1×1 source
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md) / ADR-0014), so it is checked by
the same call and needs no rule of its own. What it did need is a voice: **fill used to refuse
silently.** It now raises `OnPasteRefused` like every other refusal, which is what makes the
promise "a write attempted against a non-editable column is always reported" true rather than
true-on-one-path.

**One gate, not two vocabularies.** What paste and fill share is the gate on the *operation* —
shape, target, editability. They do not share the gate on the *value*: a clipboard paste is
never judged on value and a fill always is, because a fill's value comes from an editor that is
still open ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)).
Reading "a fill is a paste" past the operation is the mistake this paragraph exists to stop.

The editability of the cell the editor opened on is *not* the test — the fill writes the whole
selection, so the whole selection is what is judged. The row half of the component's
`IsEditableCell` (is the row inside the Window?) is deliberately **not** reused here: a paste
target legitimately covers rows that are off screen or not yet fetched (ADR-0014), and applying
that check would refuse a correct paste.

## A refused fill holds the editor

*(Added after the fact. The decision as first written said nothing about the editor's fate, and
the implementation closed it — `CommitFillAsync` tore the editor down before consulting the
gate, so a refused fill took the user's typing with it.)*

The gate runs **before** the editor is torn down. A refused fill leaves the editor open, the text
still in it, and the Focus where it was.

For a 1×1 source this is the whole of the case rather than a branch of it: `EmptySelection` cannot
arise while an editor is open, and every shape rule approves a single cell, so `TargetNotEditable`
is the only refusal a fill can meet.

The reason is the line [ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)
draws, read the way this decision forces it: **a Reject judges the value; a Refusal judges the
operation.** A fill refused for covering a non-editable column never looked at the text. Discarding
it would punish the user for the shape of their selection — and quietly, because the status area
says the fill was refused and nothing says the typing is gone.

It follows that the other commit gestures keep their meaning. **Enter still commits the one cell**
the editor was opened on: that cell is editable by construction — an editor opens nowhere else —
so the operation is legal and the refusal has no standing over it. This is where a Refusal and a
Reject part company. A Reject stops *every* commit gesture (ED-15), because there the value is what
is wrong. A Refusal stops only the operation it named.

Rejected: **closing the editor and discarding the text**, which is what the implementation did. It
costs the user their typing for a mistake that is one keystroke from being legal.

Rejected: **holding the editor with Escape as the only exit**, symmetric with a Reject. It would
forbid a commit the grid has no objection to, turning a mis-sized selection into a trap.

## Consequences

- **`PlanPaste` takes the column predicate as a required argument.** No overload without it —
  an entry point that skips the check is an entry point that reintroduces the defect.
- **A grid with no editable column refuses every paste and every fill.** That is the honest
  reading of the declaration; the JS key gate already refuses to open an editor there.
- **The refusal reason is `TargetNotEditable`, and the message is Chrome's to write**
  (ADR-0010). The grid holds no UI strings — and therefore **cannot announce this one**. Where a
  Reject carries the Consumer's sentence through the grid and is relayed into the root's live
  region (ADR-0034), a refusal arrives as an enum and the sentence exists only in Chrome. So the
  announcement is Chrome's too: **whatever renders a refusal must be a live region**, or a user
  who cannot see it is told nothing while being told, in the code's own terms, that the write was
  "always reported". The obligation is stated here, in `tests/ExGrid.Browser/README.md`, and met
  by the reference Chrome in the DemoHost; a Chrome that ignores it is silent, and that is the
  cost of the grid holding no strings.
- **Hiding a column does not change this.** Editability is judged on the columns the selection
  actually covers, in the current order.
- The Consumer is still free to ignore an approved paste. `Editable` is the grid's gate, not a
  permission system: the Consumer applies the intent and remains the last word (ADR-0007).
- **The Definition of Done gains ED-19**, and CP-16 keeps its clipboard-side half unchanged: the
  refusal itself, its order among the rules, and the silence that used to follow a fill.
