# What ExPivot asks of ExGrid's core — a double click where no edit opens

*(Numbered ADR-0062 until 2026-10-01. ExSheet's Pointing Scope took ADR-0058 first, and ExPivot's
ADRs moved up by one into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for them. Commit messages before then use the old numbers.)*

*(Proposed 2026-09-30 with [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md).
It is shaped as [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) shaped ExSheet's requests of
the core: opt-in, and right for any Consumer.*

*Decided with the user the same day, as proposed (Q9). The grilling added one more request of the
core, the Change Highlight
([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)), so
this is no longer the only core change ExPivot's first version makes.)*

In Excel a double click on a PivotTable does the two things its users reach for most: on a value
it shows the records behind it (Show Details), and on an outer row label it expands or collapses
the Item. In ExGrid a double click means one thing, **F2's other door**: it opens the Cell Editor in
Caret on an Editable cell ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). On any
other cell it does nothing, and nothing tells the Consumer it happened. A report's cells are never
Editable (ADR-0059), so on a pivot the double click is free, and the Consumer is the only one who
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

- **A criterion in §26, DC-59**, gates ExGrid like every other declaration there (Definition of
  Done §2).
- **ExPivot listens**: Show Details on a value cell, and expand or collapse on the label of an
  outer Item.
- **A plain ExGrid Consumer may listen too**, for its own detail view of a row.

*(Added 2026-10-01.)* **Building Show Details' dialog asked for two more**, decided with the user in
[ADR-0070](./0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md): a Consumer
gives a grid the keyboard back (`ReturnKeyboardAsync()`), and hears an Escape that leaves it
(`OnLeave`). They are §26's DC-57 and DC-58, and gate ExGrid as DC-59 does.
