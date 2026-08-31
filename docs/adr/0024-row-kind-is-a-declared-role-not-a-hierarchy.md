# Row Kind is a role the Consumer declares. The grid paints it and computes nothing

[ADR-0013](./0013-fixed-row-height.md) settled that the row height is fixed and that
pivot-like views are therefore supported by **making group rows ordinary rows that look
different**. It left one thing explicitly undesigned: "Group rows look different from
detail rows — needs a **Row Kind** (detail / group / total). **Not yet designed**." This
is that design.

**A Row Kind is a role, declared per row by the Consumer.** The grid is told which of
detail / group / total a row plays, paints it, and does nothing else with it.

```
Consumer                                     Grid
  computes the grouping and the aggregate      is told: this row is a Group
  pushes group and total rows in the Window    paints ex-row-group
  holds what is expanded                       paints them the same height as any row
```

## Three roles, and nothing else in the enum

| Role | What it is | Who computed it |
|---|---|---|
| **Detail** | an ordinary row of the result. The default | — |
| **Group** | a row standing for a block of rows | the Consumer |
| **Total** | a row aggregating others — subtotal or grand total | the Consumer |

Detail is `0`, so a Consumer that says nothing gets exactly what it got before Row Kind
existed. An undefined value cast in from an integer is **refused**, not painted as a
detail row, for the reason an undefined `CellState` is
([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)): a mapping bug that
paints a perfectly ordinary-looking row is the one outcome worth ruling out.

## What it deliberately does not carry

- **Not depth.** No level, no parent, no children. The glossary's `_Avoid_` list already
  says "Row Kind is the role, not the depth", and the implementation holds to it: nothing
  is indented, because the grid is told neither how deep a group sits nor what it
  contains. A Consumer that wants indentation puts it in the value
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) — the grid does
  not group).
- **Not the aggregate.** A total row's numbers are values in the Window like any other
  row's, extracted through `Column.Value`. The grid sums nothing, so a total row copies,
  formats and overflows (`####`) exactly as its neighbours do.
- **Not expansion state.** Whether a group is open is the Consumer's, and expanding it
  changes the row count — which ADR-0013 already treats like a sort change: the Consumer
  returns a new Window and `TotalCount`, and bumps the Row Sequence Version so the
  selection is dropped ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).

## Asked through a delegate, not required of the row type

`Func<TRow, RowKind>`, called once per painted row, exactly as Cell State is asked for
per cell.

- **`TRow` is the Consumer's own type**, often a DTO from a service it does not own.
  Requiring an interface on it would mean a wrapper type per screen, and
  `GridSource.From` deliberately works on plain rows
  ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).
- **The delegate's identity is the change signal**, as with Cell State (ADR-0006) and row
  instances ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)):
  hold it in a field, hand over a new one when the roles change. Changing what an
  unchanged delegate would answer does not repaint.
- Rejected: **a parallel array of kinds pushed beside the Window**. It is a second thing
  to keep index-aligned with the rows, and the failure mode — off by one after a Window
  arrives — paints the total row's rule across an ordinary row. A delegate cannot slip.

## Consequences

- **The height is untouched, and that is enforced in CSS.** `ex-row-total`'s separating
  rule is a 1px `background-image` gradient, not a `border-top`: `.ex-row`'s height is
  content-box, so a 1px border would make every total row one pixel taller than the
  arithmetic says — the drift ADR-0013 exists to prevent, and invisible on screen. The
  tint and the rule are painted on the row **and on its cells**, because a Pinned Column
  carries its own opaque background and a tint on the row alone would stop at the pinned
  block — the role would be visible everywhere except the column the group's name is in.
  Where a cell's own **Cell State** wants the same layer, the state wins (ADR-0006).
- **A Placeholder keeps its Kind.** The role comes from the delegate, not from the cells,
  so a group row skipped mid-fling still paints as a group row
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)).
- **Selection does not know about roles.** A total row is selectable and copyable like any
  other; selection is rectangles in index space and holds no per-row knowledge
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). Whether a bulk paste should
  refuse to write onto a total row is **open**, and belongs with the Overlay
  ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)).
- **Open:** the expand / collapse gesture itself. It is a press on a group row, which is
  what an Action Column already is
  ([ADR-0020](./0020-action-and-template-columns.md)) — the likely shape is an Action
  Column the Consumer puts first, not a new mechanism. Also open: pinning a grand total to
  the bottom edge, which is a Viewport question, not a Row Kind one.
