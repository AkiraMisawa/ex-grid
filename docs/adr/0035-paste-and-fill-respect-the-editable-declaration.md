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

The editability of the cell the editor opened on is *not* the test — the fill writes the whole
selection, so the whole selection is what is judged. The row half of the component's
`IsEditableCell` (is the row inside the Window?) is deliberately **not** reused here: a paste
target legitimately covers rows that are off screen or not yet fetched (ADR-0014), and applying
that check would refuse a correct paste.

## Consequences

- **`PlanPaste` takes the column predicate as a required argument.** No overload without it —
  an entry point that skips the check is an entry point that reintroduces the defect.
- **A grid with no editable column refuses every paste and every fill.** That is the honest
  reading of the declaration; the JS key gate already refuses to open an editor there.
- **The refusal reason is `TargetNotEditable`, and the message is Chrome's to write**
  (ADR-0010). The grid holds no UI strings.
- **Hiding a column does not change this.** Editability is judged on the columns the selection
  actually covers, in the current order.
- The Consumer is still free to ignore an approved paste. `Editable` is the grid's gate, not a
  permission system: the Consumer applies the intent and remains the last word (ADR-0007).
