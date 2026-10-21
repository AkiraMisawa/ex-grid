# 05: Links narrow by values

Status: done
Roadmap: internal

ADR-0340 (Q5): a link names columns; a selection in its view (a grid's rows, a pivot's cell) becomes an
equality filter on them, executed by the narrowed view's source (ADR-0002). A dropped selection drops
the filter (ADR-0011). Not capped; a source that cannot execute a long value list refuses with the
reason.

Done for a link that starts at a grid: the reader's `links`, `LinkCondition` (the selected rows' tuples,
ADR-0340's implementation notes), `BoundBoard.ConditionOf` / `Narrow`, the component narrowing on the
grid's raised selection, the Docs Site's section and `board.spec.mjs` on `/board`. A link from a pivot's
cell is [ticket 11](11-a-pivot-cell-links.md); a source on a server executing the condition is ticket 02.
