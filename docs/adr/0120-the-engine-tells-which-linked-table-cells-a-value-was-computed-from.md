# The engine tells which Linked Table cells a Value was computed from

*(Decided with the user, 2026-10-03, in the grilling for the CDS marking sample,
`docs/specs/cds-marking/`.)*

**`ExSheet.Engine` answers, for a cell, its Read Set: the Linked Table cells whose values the
evaluation that produced the cell's current Value took as operands, directly or through the Formula
cells it reads.** Each is named by table, row and column, with the row's key when the table has one
([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)). The engine computes it
when asked, from the Values and snapshots the Sheet holds. It records nothing in advance.

## Why

A Formula's Value is a number, a text, a boolean or an Error Value
([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
Whatever the Consumer knew about the rows it read is gone from it. The sample needs that knowledge
back in three places, and each is generic.

- **Sensitivities by finite difference.** The sample marks a proxy curve by a Formula over quoted
  curves. It publishes the Jacobian of each point with respect to the quotes, by bumping each quote
  and recalculating. The user chose a bump over analytic derivatives, so that the derivative comes
  from the same implementation as the Value. Without the Read Set, every row of every table has to be
  bumped: a whole snapshot pushed per row, with every reader recalculated each time.
- **What a Value rests on.** The data provider says, per quote, whether it was observed. The sample
  paints a computed point faintly unless every quote it rests on was observed at its own tenor. That
  is the Consumer's rule over the Read Set's rows.
- **What a Formula may read.** The sample refuses a Formula that reads a curve not yet chosen for
  marking, and offers to choose it. Only the rows actually read tell it which.

## What counts as read

- **A value taken as an operand is read.** For `XLOOKUP`, that is the cell of the return array at
  the match. For an aggregate over a column (`SUM(T[PV])`), it is every cell of the column. For a
  column used as one value, it is its one cell.
- **A cell consulted to find another is not read.** `XLOOKUP`'s lookup array decides which row is
  taken; its cells are reported apart, as the Read Set's consulted cells. A key column holds every
  key of the table, so counting it as read would make every exact lookup rest on the whole table.
- **It follows Formula cells.** A cell that reads `F5`, where `F5` reads the table, reports `F5`'s
  Read Set too. The Read Set is of the Value, not of the text of one Formula.
- **It is the evaluation's, not the text's.** A branch of `IF` that was not taken reads nothing;
  the condition's operands are read. That is the opposite of `#CIRC!`, which ADR-0047 judges from the
  text on purpose, because a cycle is a fault of the Sheet's construction. A Read Set describes one
  Value: a bump of a cell outside it cannot move that Value, and a fact about a cell outside it is
  not a fact about that Value.
- **A table that is waiting contributes nothing.** The Value is `#GETTING_DATA`, and there is no row
  to name.

## Asked, not recorded

The Read Set is computed on request by evaluating the cell again with tracing, over the Values and
snapshots the Sheet already holds. It therefore describes exactly the Value the Sheet shows, from one
completed recalculation, which is ADR-0047's rule for Values. Recording it during every
recalculation was the alternative. It would cost memory per Formula cell and time in every
recalculation, for every Consumer, including those that never ask. Asking is the shape of Cell State
and the Change Highlight ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md),
[ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)):
nothing costs anything until someone asks.

## It is not an Excel answer, and changes none

Excel's nearest feature is Trace Precedents, which names cells, not a table's rows. No Value changes
because of this ADR, so the rule that ExSheet answers as Excel does or not at all is not touched. It
is offered by `ExSheet.Engine`, which a server uses without a browser. The component does not offer
it yet, because the sample computes its marks on the server.

## Considered options

- **Derivatives in the engine (automatic differentiation).** Rejected with the user. Every admitted
  function would need a second implementation, its derivative, and two implementations drift: the
  argument ADR-0047 makes against a second formula engine.
- **Parsing the Formulas in the application.** Rejected. The parser is internal, and a second parser
  outside the engine is a second implementation of the grammar.
- **Bumping every row of every table.** Correct, and it costs one snapshot push and one recalculation
  of every reader per row of the universe.
- **Carrying "observed" through the functions.** Rejected. Each function would need a rule for how
  the flag combines (`MAX` of an observed and an unobserved quote is what?). That is the Consumer's
  rule written into the engine.

## Consequences

- **The Definition of Done's §27 gains SH-56.** It judges ExSheet, and does not gate ExGrid.
- **A Consumer that never asks pays nothing.**
- **The cost of one request is the cost of evaluating the cell and the Formula cells it reads once
  more.** It is measured on the sample's book, with every point of every curve asked once per
  snapshot, before the sample relies on it.
