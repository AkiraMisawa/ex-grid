# What ExPivot asks of ExGrid's core — a double click where no edit opens

*(Proposed 2026-09-30 with [ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and not yet decided with the user. It is the only change to the core ExPivot's first version
makes, and it is the shape [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) gave ExSheet's:
opt-in, and right for any Consumer.)*

In Excel a double click on a PivotTable does the two things its users reach for most: on a value
it shows the records behind it (Show Details), and on an outer row label it expands or collapses
the Item. In ExGrid a double click means one thing, **F2's other door**: it opens the Cell Editor in
Caret on an Editable cell ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). On any
other cell it does nothing, and nothing tells the Consumer it happened. A report's cells are never
Editable (ADR-0058), so on a pivot the double click is free, and the Consumer is the only one who
knows what it means.

## The notification

- **A Consumer can listen for a double click on a cell where no edit opens.** The grid raises
  `OnCellDoubleClick` with the cell's position, once per double click, after the click has placed
  the Focus there. It changes nothing itself.
- **Where an edit opens, the edit is the double click's meaning**, as before: an Editable cell
  enters Caret and nothing is raised. While an edit is open, a double click is the editor's, and
  nothing is raised either.
- **Without a listener nothing changes**, so no existing criterion moves.
- **The position is the one the grid's own geometry names**, read from the Viewport's offsets as
  every press is ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)). A double click on a
  control inside an Action, Template or Mark cell — the only elements under the rows that take the
  pointer — **stops at that cell**, as its `mousedown` already does
  ([ADR-0020](./0020-action-and-template-columns.md)). Carried on, its offsets would be measured
  from the control and name a different cell: a notification naming the wrong cell is the quietly
  wrong answer this design refuses. *(The same stop also closes a latent case: an Editable cell
  mis-resolved from a control's offsets would have opened its editor.)*
- **There is no key for it.** A double click is a pointer gesture; a Consumer offers the same
  commands in the Context Menu ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)),
  as ExPivot does with Show Details and Expand / Collapse.
- **No JavaScript.** The grid already listens for `dblclick` on the Viewport, in Blazor
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

## Consequences

- **A criterion in §26, DC-52**, gates ExGrid like every other declaration there (Definition of
  Done §2).
- **ExPivot listens**: Show Details on a value cell, and expand or collapse on the label of an
  outer Item.
- **A plain ExGrid Consumer may listen too**, for its own detail view of a row.
