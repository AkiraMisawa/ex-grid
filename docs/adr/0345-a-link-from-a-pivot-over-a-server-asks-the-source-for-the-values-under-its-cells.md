# A link from a pivot over a server asks the source for the values under its cells

*Taken in the user's absence, 2026-10-21, under their standing authorization (2026-10-20) for a fork
only they could settle: a reasonable default, recorded here, which they may overturn. The user asked
that the link [ADR-0343](0343-a-board-over-a-source-held-on-a-server-hands-it-the-query-and-the-narrowing.md)
refused be built.*

## Context

A link from a pivot narrows by the values in the link's columns of every row under the selected
cells ([ADR-0340](0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md), Q5). Over
a Snapshot the Board reads those rows itself. Over a source held on a server (ADR-0343) the rows are
there, so ADR-0343 refused such a link. A link whose columns are among the pivot's own fields could be
read from the cells' Items, but only while every cell names them all: a subtotal or a total names
fewer, and stands for every Item below it, which only the rows know.

## Decisions

- **The source is asked, always.** `BoardSource.Under` answers a `BoardCellsQuery` (the pivot view, its
  layout, the cells, the link's columns and the narrowing the pivot's own links put on it) with the
  distinct values of those columns among the rows under any of the cells. Reading a link's columns from
  the Items where the cells happen to name them would be a second rule, right for some cells and wrong
  for a subtotal; one question answered where the rows are is right for all. The layout carries the
  hidden Items, which are under no cell.
- **`BoardSource.ValuesUnder(Snapshot, BoardCellsQuery)` is the reference implementation**, and the
  specification a server's source answers as: the same reading the Board makes of a pivot over a
  Snapshot, which now goes through it too.
- **Selecting cells in such a pivot is asynchronous** (`BoundBoard.SelectCellsAsync`). The views its
  links reach keep their rows until the answer comes, and an answer to a selection a later one has
  replaced is dropped (spine rule 6). A source that fails narrows nothing and says why under the pivot.
- **A link from a pivot over a server input whose source has no `Under` is refused at binding**, by the
  link's path, as a chart whose source thins nothing is (ADR-0344).
- **The answer is not capped** (spine rule 5): a link on a column of many values under a grand total is
  handed back to the sources it narrows, which execute it where the data lives.

## Consequences

- ExBoard gains `BoardSource.Under`, `BoardSource.ValuesUnder`, `BoardCellsQuery` and
  `BoundBoard.SelectCellsAsync`; `SelectCells` stays for a pivot over a Snapshot, and refuses one over a
  server, whose answer cannot be had at once.
