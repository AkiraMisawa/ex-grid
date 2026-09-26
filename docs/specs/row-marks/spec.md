# Row Marks and the Mark Column

Status: ready-for-agent

Decided by [ADR-0043](../../adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md),
with consequences recorded in ADR-0014, ADR-0015, ADR-0020 and ADR-0024. Exit criteria:
`docs/definition-of-done.md` §25, MK-1..MK-8. This spec synthesises those decisions; where it
and they disagree, they win.

## Problem Statement

A user of a line-of-business screen wants to tick rows and then do something to all the ticked
rows at once — approve them, export them, cancel them. ExGrid has no way to say which rows are
"ticked". A Consumer can paint a checkbox in a Template Column, but then:

- the grid does not know what the checkbox means, so the header cannot offer "tick all", and
  there is no keyboard route to tick many rows at once;
- whether "all" means every row, every row on screen, or every row the filter left is decided
  differently on every screen;
- nothing stops the ticks from being positional, so a sort between ticking and acting can aim
  the action at different rows;
- nothing tells the user that some of the ticked rows are no longer visible under the current
  filter — so an action can land on rows nobody is looking at.

On a screen of money and risk numbers, each of those is a way for a bulk action to hit rows the
user did not mean.

## Solution

A new column kind, the **Mark Column**, shows a checkbox on every Detail row and one in its
header. A ticked row carries a **Row Mark**.

- A Row Mark belongs to the row's identity, not its position. It survives sorting, scrolling and
  value updates. It is not a Selection and does not interact with one.
- The Consumer holds the marks. The grid reports each marking gesture as one notification and asks,
  row by row for the rows it paints, whether each is marked. On the bundled Grid Sources this is
  already done: `GridSource.From` needs nothing extra; `GridSource.Fetch` needs a mark adapter
  that knows the rows' keys and can answer counts from the server.
- Pressing the header marks **every Detail row of the current result, after filtering, whether or
  not it is on screen**, as it stood at that moment. Rows arriving later are not marked. Under a
  pager, the header marks the page and the status area offers "Mark all N rows".
- The header shows none / some / all, from counts the Consumer answers — never from the rows that
  happen to be loaded.
- With the Focus in the Mark Column, Space brings every selected row's mark into line: all marked
  if any was unmarked, otherwise all unmarked. Ctrl+A then Space is the header's keyboard route.
- The status area shows how many rows are marked, and says aloud how many of them lie outside
  the current filter.
- The action itself lives outside the grid, in the Consumer's own UI. It runs by identity,
  verified where the data lives, and reports a partial result as partial.

## User Stories

1. As a user, I want a checkbox beside each row, so that I can pick the rows an action should apply to.
2. As a user, I want to tick a row by clicking its checkbox, so that marking one row is one click.
3. As a user, I want clicking a ticked row's checkbox to untick it, so that I can correct a mistake.
4. As a user, I want clicking a row's checkbox not to move my cell selection elsewhere, so that marking does not disturb where I am.
5. As a user, I want a checkbox in the header of the Mark Column, so that I can mark every row at once.
6. As a user, I want the header checkbox to mark every row the current filter leaves, including rows I have not scrolled to, so that "all" means all.
7. As a user, I want the header checkbox never to mark rows the filter has hidden, so that "all" never reaches rows I cannot see.
8. As a user, I want rows I scroll to after pressing the header to appear ticked, so that the screen never contradicts the header.
9. As a user, I want the header to show "some" when only some rows are marked, so that I can tell at a glance that the marking is partial.
10. As a user, I want the header to show "all" only when every row of the result is marked, so that it is never "all" just because the loaded rows are.
11. As a user, I want pressing the header while it shows "some" to mark all, so that one press completes a partial marking.
12. As a user, I want pressing the header while it shows "all" to unmark all, so that I can clear the marking in one press.
13. As a user, I want a row that arrives after I pressed the header not to be marked, so that a bulk action never sweeps in something I have not seen.
14. As a user, I want the header to turn to "some" when a new row arrives after I marked all, so that I am told something new came in.
15. As a keyboard user, I want Space in the Mark Column to toggle the Focus row's mark, so that I can mark without the mouse.
16. As a keyboard user, I want Space with several rows selected in the Mark Column to apply to all of them, so that I can mark a block in one key.
17. As a keyboard user, I want Space over a mixed block to mark all of it, so that the result is predictable rather than scrambled.
18. As a keyboard user, I want Space over an all-marked block to unmark all of it, so that I can clear a block in one key.
19. As a keyboard user, I want Ctrl+A then Space in the Mark Column to mark the whole result, so that the header has a keyboard route.
20. As a keyboard user, I want Space on a data cell to keep its existing meaning even when my selection reaches into the Mark Column, so that marking never happens by surprise.
21. As a user, I want my marks to stay on the same rows after I sort, so that sorting to check my choice does not undo it.
22. As a user, I want my marks to stay when the values in the rows update, so that a live screen does not clear my work.
23. As a user, I want my marks to stay when I change the filter, so that I can gather rows across several filters before acting.
24. As a user, I want the status area to say how many rows are marked, so that I know the scale of what the action will touch.
25. As a user, I want the status area to say how many marked rows are outside the current filter, so that I am never surprised by an action on rows I cannot see.
26. As a user, I want the marked rows I bring back by widening the filter to still be ticked, so that the count I was shown was true.
27. As a user of a paged grid, I want the header to mark only the current page, so that one press does not reach beyond what the page shows.
28. As a user of a paged grid, I want to be offered "Mark all N rows" after marking a page, so that marking the whole result is one deliberate step.
29. As a user of a paged grid, I want "Mark all N rows" to count only the rows the filter leaves, so that the offer matches the result.
30. As a user of a paged grid, I want my marks to stay when I turn the page, so that I can mark across pages.
31. As a user, I want group and total rows to carry no checkbox, so that I cannot mark something an action has no meaning for.
32. As a user, I want "mark all" and Space to skip group and total rows, so that a subtotal is never in the marked set.
33. As a user, I want the header's state to count only detail rows, so that group and total rows do not keep it at "some".
34. As a user, I want a marked row that has been deleted elsewhere to drop out of the count, so that the count only promises rows that exist.
35. As a user, I want an action that could not reach every marked row to say how many it did reach, so that a partial result is never passed off as success.
36. As a screen-reader user, I want each row checkbox and the header checkbox to announce its state, so that I know what is marked without seeing it.
37. As a Consumer developer, I want to declare a Mark Column like the other column kinds, so that adding it is one line.
38. As a Consumer developer, I want to be refused, by name, if I declare two Mark Columns, so that an ambiguous "all" is never painted.
39. As a Consumer developer, I want one notification per gesture — a click, a header press, a Space — so that a million-row marking is one call, not a million.
40. As a Consumer developer, I want a Space notification to carry the rectangles and the Row Sequence Version they were made under, so that I can resolve them to my own rows.
41. As a Consumer developer, I want guidance to refuse a positional notification under a different Row Sequence Version, so that a sort between gesture and resolution cannot hit other rows.
42. As a Consumer developer, I want the grid to ask me per painted row whether it is marked, so that I keep my marks in whatever shape suits my data.
43. As a Consumer developer, I want a changed mark to repaint only the affected rows, so that marking stays cheap on a large grid.
44. As a Consumer developer, I want to supply the marked counts the header and status area need, so that they are right beyond the Window.
45. As a Consumer developer using `GridSource.From`, I want marks to work with nothing extra to write, so that the in-memory case is free.
46. As a Consumer developer using `GridSource.From`, I want a mark to follow a row through `ReplaceRow`, so that a value update keeps it.
47. As a Consumer developer using `GridSource.Fetch`, I want to supply a mark adapter — how to key a row, and how to answer counts and "as of" from my server — so that marks are correct at a million rows.
48. As a Consumer developer using `GridSource.Fetch`, I want a Mark Column without a mark adapter to be refused by name, so that I cannot ship counts the Source cannot know.
49. As a Consumer developer, I want to ask for the current marks when my toolbar action is pressed, so that the action runs over exactly what the user sees marked.
50. As a Consumer developer, I want the marks expressed as identities or as "all of Q as of T, except these" — never as positions — so that my server can verify each row still exists.
51. As a Consumer developer, I want a sample showing an action that verifies rows and reports a partial result, so that I have a model to copy.
52. As a design-system Wrapper author, I want the mark count line to come through the same Chrome seam as the selection count, so that my Wrapper can restyle it without changing its meaning.
53. As a Consumer developer, I want marks unaffected by the selection being dropped on a sort, so that the two concepts never leak into each other.

## Implementation Decisions

- **A new column kind: the Mark Column.** Declared through a factory beside the Action and Template
  Column factories. At most one per grid; a second is refused by name when the columns are bound.
  It carries no value accessor of the row's data, is neither sortable nor filterable, and has no
  column menu of its own. Its cells and header are plain markup inside the row, as the Action
  Column's are, and add no component boundary (ADR-0003/0020).
- **Only Detail rows paint a checkbox.** A Group or Total row's Mark cell is empty. Row Kind is
  already asked per row; the Mark Column reads the same answer (ADR-0024).
- **The per-row question.** The grid asks the Consumer whether a row is marked, per painted row,
  in the same shape as Row Kind's question: a delegate whose identity is the change signal. The
  answer reaches each row as a parameter, so a changed answer repaints that row and only that row;
  the row's hand-written `ShouldRender` compares it.
- **One notification per gesture, three shapes.**
  - one row's checkbox: the row instance (identity, as an Edit Intent carries it) and the new state;
  - the header, or "Mark all N rows": "mark all" or "unmark all" over the current result, with the
    Row Sequence Version it was pressed under — the as-of point;
  - Space: the selection's rectangles restricted to the Mark Column, the Row Sequence Version they
    were made under, and the target state already decided by the line-up rule.
  None is ever one call per row.
- **The pure rules live in their own module** — the new test seam. It decides:
  - Space's line-up: given the marked state of the covered rows (as counts), the target state;
  - the header's state: from "marked Detail rows in the current result" and "Detail rows in the
    current result", none / some / all;
  - the header press: from the header's state, mark all or unmark all.
  It knows nothing of rendering or of the Sources.
- **The counts come from the Consumer.** Marked Detail rows in the current result; marked rows
  outside it; the grid never counts the Window as a substitute.
- **Space in the Mark Column.** The key dispatch gains the Mark Column as a cell kind: with the
  Focus in it, Space raises the Space notification over every selected row in that column. With
  the Focus elsewhere, Space is unchanged. Ctrl+A is unchanged; Ctrl+A then Space is the header's
  route, and the header gets no key of its own (ADR-0020).
- **A click on a checkbox** is taken by the cell, which stops the event, so it never reaches the
  Viewport's selection arithmetic (ADR-0020's seam note).
- **The status area.** The existing status line (selection count, off-screen indicator, "Select all
  N rows") gains a mark line: "N marked", and "(M outside the current filter)" whenever M > 0.
  Under a pager it gains "Mark all N rows" beside "Select all N rows". Counting is the core's and
  the Consumer's; displaying is Chrome's (ADR-0010/0014/0015).
- **Paging.** The header press under a pager marks the page's Detail rows; the offer marks the
  whole filtered result (ADR-0015).
- **The Grid Source contract grows the Consumer's half.** A Source that supports marks answers the
  per-row question, the two counts, and applies the three notification shapes. The bundled
  Sources implement it:
  - `GridSource.From`: reference identity, carried across `ReplaceRow`; "all" held as the snapshot
    of the result at the press, minus exceptions; the counts computed in memory; a positional
    notification under a stale Row Sequence Version resolves to nothing; a row removed from the
    data drops its mark.
  - `GridSource.Fetch`: takes an optional **mark adapter** from the Consumer — how to read a row's
    key, and how to answer the counts and the as-of question from the server. A Mark Column over a
    `Fetch` Source without an adapter is **refused by name** (ADR-0043, refined while writing
    this spec).
- **Reading the marks for an action.** The Source exposes the current marks as identities, or as
  "all of Q as of T, except these", never as positions. The DemoHost carries a toolbar action that
  verifies each row and reports "X of Y applied; Z no longer exist".
- **Accessibility.** Each checkbox exposes its state to assistive technology within the surface the
  root owns (ADR-0033); the header's mixed state is announced as mixed.
- **Glossary.** Row Mark and Mark Column are already in `CONTEXT.md`; the implementation uses
  those names, and not "checked", "selected row" or "checkbox column".

## Testing Decisions

- **A good test observes behaviour through a public seam and names the ADR it pins** (`// ADR-0043:
  …`). It asserts what the user or the Consumer sees — which rows paint ticked, which notification
  arrives, what the header shows, what the status area reads — never private fields or the order
  of internal calls.
- **Layer 1, the pure rules module (new seam):** the line-up rule over all-marked, none-marked and
  mixed inputs; the header state over zero, some and all, including the case where every loaded
  row is marked but the result is not (MK-1, MK-2). Prior art: the selection and clipboard rule
  tests.
- **Layer 1, the Grid Source contract (existing seam):** `GridSource.From` — snapshot "all", a row
  arriving after it answers unmarked, marks survive a sort and a `ReplaceRow`, a removed row drops
  its mark, a stale positional notification marks nothing, the two counts (MK-3 second half,
  MK-5, MK-8). `GridSource.Fetch` — the same through a fake adapter, and refusal without one.
  Prior art: the existing From and Fetch contract tests.
- **Layer 2, the ExGrid component (existing seam):** one notification per gesture over a 10⁶-row
  selection (MK-3); only the changed row re-renders, counted with the render counters (MK-4); marks
  survive a sort while the selection is dropped (MK-5); Group and Total rows paint no checkbox;
  a second Mark Column is refused; a checkbox click does not move the selection; the status line
  reads the counts; the header turns to "some" when a new row is pushed after "all" (MK-8). Prior
  art: the row-memoisation, row-kind, action-and-template-column and pager component tests.
- **Layer 3, the DemoHost in real browsers (existing seam):** after "mark all" at 10⁶ rows through
  `GridSource.Fetch`, rows scrolled into view far away paint ticked (MK-6); narrowing the filter
  shows the out-of-filter count, and widening it back shows the same rows ticked (MK-7); the
  console stays clean throughout. Prior art: the existing large-data and pager browser tests.
- **The invariants still hold:** the DOM does not grow with the row count, rows still skip their
  render, and nothing per-cell reaches JavaScript. The feature adds no JavaScript at all.

## Out of Scope

- A boolean field shown as a checkbox, and editing it. That is the row's data, not a Row Mark.
- More than one Mark Column per grid, or several independent sets of marks.
- The actions themselves (approve, export, …) — they are the Consumer's, outside the grid. Only
  the DemoHost sample action is in scope, as a model.
- Persisting marks across sessions, or into a Saved View.
- A live "all" that takes in rows arriving later — rejected by ADR-0043.
- Clearing marks on a filter change — rejected by ADR-0043.
- The MudBlazor Wrapper's styling of the new status line beyond what the Chrome seam already
  carries.

## Further Notes

- **The race cannot be closed in the grid.** A row deleted elsewhere can still be named by the
  marks until the Window carrying that news arrives. Identities make that fail loudly rather than
  land on another row; the Consumer's action verifies and reports. This is written as a
  requirement on the Consumer in ADR-0043 and demonstrated in the DemoHost.
- **Row Identity is reference identity** in the grid. `GridSource.From` can rely on it because it
  sees every replacement; `GridSource.Fetch` cannot, which is why its adapter carries a key.
- A ticket that turns out to need a new decision stops and records it first (`AGENTS.md`,
  "Implement").
