# Selection is rectangles in position space, and is dropped when the order changes

Selection is held as a **list of rectangles**. The coordinates are **positions in the current
order** (row index, column index), not row identities. **When the sort order or filter changes,
the selection is cleared.**

Disjoint multi-range selection (Ctrl+click) is supported. **There is no cap on selection itself.**

## It cannot be held as a set of cells

The naive form is a list of selected cells, but Ctrl+A over a million rows × 50 columns is 50
million coordinates. A list of rectangles is the only workable form, as in Excel, and Ctrl+A is
one rectangle.

## Why positions, and why they are dropped

Since [ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) made the interface
push, the grid holds only the Window (tens of rows). **The grid does not know which row "row 10"
actually is**, so identities are not even available to hold. Expressing Ctrl+A by identity would
mean enumerating a million of them.

Positions point at something different once the order changes.

```
1. Displayed in descending price. Rows 10–509 are selected (the 500 largest)
2. The user re-sorts ascending by book
3. "Rows 10–509" now points at an ENTIRELY DIFFERENT 500 rows
```

**This is dangerous because the operation that follows a selection is a bulk paste**
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). Re-sort, paste dates from
Excel, and a different 500 rows get the value — **and the screen looks normal**. It is the same
class of failure as the truncation in
[ADR-0005](./0005-copy-refuses-rather-than-truncates.md) and the "colour changed, value stale"
case in ADR-0007.

## Considered Options

- **Keep positions as they are (the same place on screen stays selected)** — rejected. It
  resembles how Excel behaves through a sort, but **Excel has all the data in front of the user,
  who can see the whole selection**. This component shows 40 of the 500 rows selected out of a
  million; the premises differ. Something invisible must not quietly become something else.
- **Remember small selections by identity and try to restore them; drop large ones** — rejected.
  **The same operation would produce different results either side of a threshold.** When
  ADR-0005 capped copy, the answer stayed consistent ("past the cap, refuse"); this one would be
  "past the cap, behave differently, quietly", which users cannot learn.

**The price of dropping is a few keystrokes to reselect** — what is lost is convenience, not
correctness.

## Ctrl+A, and the absence of a cap

**Ctrl+A selects every row after filtering, across every visible column.** It is one rectangle,
so it can be expressed without holding the data. Hidden columns are excluded (View State
decides).

**There is no cap on selection itself.** The danger is not the selection but the operation that
follows, so the brake belongs on the operation.

- Copy: [ADR-0005](./0005-copy-refuses-rather-than-truncates.md) already refuses past its cap.
- Bulk paste: only the Consumer knows what a million overrides mean, so **confirmation is the
  Consumer's job**. The grid does not pre-emptively forbid it.

**Capping the selection was rejected.** If it cannot be selected, the "select all → copy → get
refused → export" path designed in ADR-0005 cannot be entered. Two caps in two places contradict
each other.

> **Selection is cheap, so it is not capped. Caps belong on what cannot be executed.**

## Disjoint multi-range selection

**Supported.** Representation and painting are cheap (a few more rectangles; the overlay in
[ADR-0008](./0008-selection-is-painted-by-an-overlay.md) is one per range); the expense is in the
meaning of the operations. Behaviour follows Excel.

- **Copy refuses when the ranges do not line up** (Excel imposes the same restriction). Together
  with ADR-0005 there are two grounds for refusing a copy — too large, and misaligned shape.
  **Say which one when refusing.**
- **Bulk entry into disjoint ranges is allowed** (type a value and press Ctrl+Enter to fill every
  selected cell). Without it the main use of disjoint selection does not work.

The main use is picking a handful of scattered rows and applying one value to all of them. The
rows a user wants are rarely adjacent, so **scattered is the natural case**.

Rejected:
- **Single rectangle only** — drops that use; the user repeats the operation one row at a time.
- **Disjoint whole rows only** — copy shapes would always line up so no new refusal reason
  appears, but **the rule cannot be explained to a user**. "Rows can be disjoint but cell ranges
  cannot" is a component-specific restriction that nobody arriving from Excel would predict.

## When the data itself is replaced — decide by a row sequence version

The order of rows can change while neither the sort nor the filter changed. A Consumer whose
data refreshes every few minutes **will hit this**.

```
User  : 50 rows selected, mid-task
Batch : a new feed version arrives; three rows added, one removed
      → same filter, same sort, but every position shifted
      → the selection points at different rows
```

The Consumer pushes a **`RowSequenceVersion`** — a version identifying the **order** of rows, not
their values — and **selection, Anchor and Focus are dropped when it changes**.

| What the update contained | Version | Selection |
|---|---|---|
| same set of rows, values updated | **not bumped** | **survives** |
| rows added or removed, order changed | bumped | cleared |

For the first Consumer, most intraday updates are the former (bookings happen in an upstream
system and are not that frequent during the day), so **selection survives the frequent case**. It
is cleared only when the order genuinely changed, and reselecting is then the correct thing —
whether newly arrived rows should be part of the selection is a question only the user can
answer.

**The grid cannot detect this on its own.** Holding only the Window, it can see neither that rows
were added nor that positions shifted. Receiving it from the Consumer is the only solution.

### Why there is no attempt to re-map to "the same rows"

Deletion does not break contiguity — remove one row inside a selection and everything after it
shifts up, leaving one rectangle. **Insertion is what breaks it.**

```
One row is inserted into a selection of 100–199 (100 rows)
  → the original 100 rows now occupy 100–149 and 151–200: the rectangle splits in two
  → N insertions inside the range give up to N+1 rectangles
```

Three costs:

- **The representation degrades.** Rectangles were chosen so that Ctrl+A is one of them. A
  million-row selection with a thousand insertions scattered through it becomes up to 1,001,
  approaching the set-of-cells form that was rejected.
- **The constant painting cost is lost.**
  [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) settled on one overlay per range, and
  confirmed on real hardware that it costs **1.2 ms (max 2.1 ms) independent of both cell count
  and selection size**. At 1,001 overlays that property is gone.
- **The re-mapping cost is proportional to the selection size.** Every selected row has to be
  resolved to an identity and its new position looked up — a million times for Ctrl+A, on every
  refresh.

The design cost is heavier still. **The grid cannot do the mapping** (it does not know
identities), so it would hand the selection to the Consumer and take it back, and Selection would
cross the line
[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md) drew between "uncommitted belongs
to the grid" and "committed belongs to the Consumer".

**When to revisit:** if in real use rows are added and removed often enough that selection keeps
disappearing, consider re-mapping and pay the three costs above knowingly.

## Consequences

- **When the Consumer changes the sort or filter, the grid clears the selection.** In the push
  form the Consumer passes `Sorts` / `Filter`, so the change is visible to the grid.
- **Ctrl+Down jumps to the last row.** Excel jumps to the edge of a contiguous block, but this
  component displays query results with no blank rows in the middle, and finding a block edge
  would require the whole dataset (which the grid does not have). Jumping to the last row is the
  only implementable behaviour and is correct for this kind of data.
- **Whether editing clears the selection is a separate matter.** Editing does not change the
  order, so the selection stays. If the sort is on an overridden column and the Consumer re-orders
  the rows, it goes.
- **Bulk paste is expressed positionally.** The grid does not know identities outside the Window,
  so the edit intent takes the form "rows N–M of the current order, this column", and the Consumer
  resolves it to actual rows (ADR-0007). Disjoint selection just makes it several ranges.
