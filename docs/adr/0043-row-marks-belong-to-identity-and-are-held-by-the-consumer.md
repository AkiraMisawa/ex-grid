# Row Marks belong to the row's identity and are held by the Consumer. The core decides what marking means

A checkbox beside every row, and one in the header to mark them all, is the ordinary way a
line-of-business screen says "these ones — now do something to them": approve, export, cancel.
Nothing in ExGrid covered it. A Template Column can paint a checkbox, but then the grid knows
nothing of what it means, and the header — which has no template of its own — cannot say "all" at
all.

The question that opened this was whose responsibility the header is: **does the Consumer decide
whether "all" means every row, or only the ones the user can see?** The answer is split, along the
line the rest of the design already draws:

```
The core decides        what a gesture means: the header, Space over a selection, "all"
The Consumer holds      which rows are marked, and answers when asked
The Consumer executes   the action — and verifies it where the data lives
```

Leaving the meaning to each Consumer is what
[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) refused when it would not
expose pull and push as peers — "what 'select all' means ends up diverging" — and what
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s rule forbids: Chrome renders and
calls back, the core decides meaning. Holding the marks in the core is what spine 3 forbids: the
grid does not know identities outside the Window, so it cannot hold a set of them.

This is ExGrid's territory, not ExSheet's. Marking rows for an action is not editing and owns no
data; it sits beside the Action Column
([ADR-0020](./0020-action-and-template-columns.md)), which already lets a row fire something.

## A Row Mark is not a Selection

| | Selection | Row Mark |
|---|---|---|
| Of what | cells | rows |
| Held as | rectangles of **positions** in the current order | the rows' **identities** |
| Held by | the grid | the Consumer |
| On a sort | dropped ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) | kept |
| For | the next gesture — copy, paste, fill | an action the Consumer runs later, often on rows off screen |

Selection is dropped on reorder because positions stop naming the same rows. A mark is the input
to an action that may run minutes later over rows nobody is looking at; if it were positional, a
sort between marking and acting would aim the action at other rows — **the quietly wrong outcome
spine 1 exists to rule out**. So it belongs to identity, and it survives sorting, scrolling and
value updates.

Rejected:
- **A Row Mark as a whole-row Selection.** One mechanism instead of two, but every sort would
  clear every mark, and the bulk action would inherit Selection's position semantics — exactly
  the property that makes Selection unsafe to keep across a reorder.
- **A Template Column with a checkbox, and nothing in the core.** Works today for the cells. It
  cannot give the header a meaning, cannot make Space mean anything over a selection, and a
  Chrome swap would change the behaviour.

## The Mark Column

A new column kind, beside Action and Template. **At most one per grid.** It is painted as plain
markup inside the row, as an Action Column is, and adds no component boundary
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). Its header carries the
mark-all checkbox.

One per grid because the header's "all", the three-state header and the count display each have
to say which marks they mean; two sets of marks would make each of them ambiguous.

**A boolean field shown as a checkbox is not this.** "Approved" on the row is the row's data, read
through `Column.Value` and changed, where editing is allowed, through an Edit Intent
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). A Row Mark is not data and is
never written to the row.

**Only Detail rows carry a mark.** A Group or Total row
([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)) paints no checkbox in the
Mark Column, is never marked by "all" or by Space, and does not count towards the header's state.
An action over a subtotal has no defined target.

## The grid asks; the Consumer answers

The grid does not hold the marks, so it asks, **row by row, for the rows it paints**, whether each
is marked — the way it asks for Cell State and Row Kind
([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md),
[ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)). The answer is a parameter of
the row, so a changed mark repaints that row and the others keep skipping their render.

The Consumer answers from whatever it keeps. **The contract is on the answer, not on the
representation:** once the Consumer has received "mark all", every row of that result answers
"marked" until it is unmarked. That is what makes a row scrolled into view after "mark all" paint
as marked — the question the design turned on, because a header that says "all" above a row that
says "not" is a lie on screen.

The natural representation is **"every row of query Q as of T, except these"** — the form webmail
uses for "all conversations selected". `GridSource.From` and `GridSource.Fetch` implement it, so a
Consumer on the bundled Sources writes nothing.

Rejected:
- **The Consumer enumerates every identity when "all" is pressed.** Trivial in memory; a million
  keys fetched from a server for one click is not.
- **The core holds a mark-set type** (all-of-Q-except, or only-these). It would need the rows'
  identities as values. Row Identity is the test for sameness, not a key
  ([CONTEXT.md](../../CONTEXT.md): *Avoid: key, id*), and the grid is told nothing that would let
  it name a row outside the Window. Adding a key contract to hold marks would be the core holding
  Consumer state — spine 3.

## What "all" means

**Every Detail row of the current result, after filtering, whether or not it is on screen.** The
same scope as Ctrl+A ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).

"Only what is visible" in the sense of *on screen* is not offered. Under virtualisation what is on
screen depends on the scroll offset and the read-ahead
([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)); a mark-all that took that set would be
arbitrary to the user. Rows removed by the filter are never marked by "all" — there is no path by
which a hidden row is marked without the user having marked it.

**Under a pager, the header marks the page first and offers the whole result explicitly**, as
Ctrl+A does ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)): a single gesture
does not silently vault past the visible context. The page is a page of the filtered result, so
"mark all N" offers the filtered N.

**"All" is the result as it stood when the header was pressed.** A row that arrives afterwards — a
new trade on a live screen — is not marked, and the header turns to "some". Rejected: **a live
"all"** that takes in rows as they arrive. On a screen of money, a trade that arrived after the
click and was swept into a bulk action is the outcome this component exists to prevent; the
header turning to "some" says, honestly, that something new has come in.

## The header has three states, and the Consumer counts

None, some, all — decided by comparing **the number of marked Detail rows in the current result**
with the number of Detail rows in it. The Consumer answers the count, as it answers everything
else about the marks. Counting only the Window would call "all" whenever the loaded rows happen to
be marked.

Pressing the header when it shows **some** marks all. It unmarks only from **all**.

## Space over a selection marks every selected row

[ADR-0020](./0020-action-and-template-columns.md) made Space "engage with the Focus cell's
content", decided by the Focus cell's kind. The Mark Column joins that table: **with the Focus in
the Mark Column, Space applies to every row that has a selected cell in the Mark Column** — as
Ctrl+Enter fills every selected range
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)). With
the Focus anywhere else, Space means what it already meant; a selection reaching into the Mark
Column from a data cell does not toggle anything.

The rows are **brought into line**, not flipped one by one: if any of them is unmarked, all become
marked; only when all are marked do all become unmarked. Flipping each would leave a mixed
selection scrambled and the result unpredictable from the gesture. This is also what Excel does
over a range of checkbox cells.

It follows that **Ctrl+A then Space, in the Mark Column, is the header's gesture**, and this is the
header's keyboard route; the header checkbox gets no key of its own.

The rows past the Window have no identity the grid can name, so Space's intent is **positional,
like a bulk paste** ([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)): the rectangles
and the Row Sequence Version they were made under. The Consumer resolves them to identities, and
**refuses to resolve them under a different Row Sequence Version** — the rule Held Selection
carries for the selection itself. Each gesture — a Space, a header press, one checkbox — is **one**
notification, never one per row.

## Changing the filter keeps the marks, and says so

Marks survive a filter change. Marking a few under one filter, switching filters, marking a few
more and then acting on all of them is a legitimate way to work, and clearing the marks would
throw it away.

The danger is acting on marks the user cannot see. It is closed the way
[ADR-0015](./0015-paging-is-another-driver-for-range-requests.md) closed pasting into an off-screen
selection — **by saying so, every time**:

```
120 marked
120 marked (70 outside the current filter)
```

The count sits with the selection count display
([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)). Counting is the Consumer's and the
core's, displaying is Chrome's.

Rejected:
- **Clear all marks when the filter changes** (as webmail does on a new search) — loses work the
  user did on purpose.
- **Keep them but leave the out-of-filter marks out of the action** — a mark that looks placed and
  is not acted on is silently wrong in the other direction.

## A mark whose row has gone

When a marked row leaves the data, the Consumer drops the mark and the count falls. Counting a
mark on a row that no longer exists would promise an action that cannot run.

That keeps the count honest. **It does not make the action safe, and nothing in the grid can:**
between the moment a row is deleted elsewhere and the moment the Window carrying that news
arrives, the user can press the action, and the marks still name the deleted row. That race is
outside anything the grid sees. What the design does guarantee is that it fails loudly rather
than landing elsewhere — because marks are identities, an action naming a row that has gone finds
nothing, where a positional one would have hit whichever row moved up into its place.

So the execution side is written down as a **requirement on the Consumer**, which the grid cannot
enforce:

1. **The grid's promise ends at the marks.** It never runs the action.
2. **The action names identities** — or the "all of Q as of T, except" form — **never positions**,
   and the side that holds the data verifies that each row still exists as it executes.
3. **A partial result is reported, never passed off as success**: "118 of 120 approved; 2 no
   longer exist."

`GridSource.Fetch`'s sample and the DemoHost carry this shape as the model to copy.

## Where the action lives

Outside the grid. The Consumer's toolbar holds "Approve", "Export", and asks for the current marks
when pressed. The grid's part is to have reported every change and to show the count.

## Consequences

- **A fourth column kind**, the Mark Column, joins [ADR-0020](./0020-action-and-template-columns.md)'s
  table: Space brings every selected row's mark into line.
- **The selection count display gains a line** for marks, with the out-of-filter clause
  ([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)).
- **The pager offer gains a second use**: "mark all N" beside "select all N"
  ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)).
- **Row Kind gains a consequence**: only Detail rows are marked
  ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)).
- **The bundled Sources implement the Consumer's half** — the as-of "all except" form, the counts
  inside and outside the current result, dropping marks on removed rows, and refusing a positional
  intent under a stale Row Sequence Version.
- **Open:** the public shape of the notification and of the per-row question (a delegate, as Row
  Kind's, is the likely one), and how the mark count is handed to Chrome. They are implementation,
  held to what is written here.
