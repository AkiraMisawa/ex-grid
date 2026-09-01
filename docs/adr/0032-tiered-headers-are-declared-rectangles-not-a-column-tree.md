# Tiered headers are declared rectangles over the leaf columns, not a column tree

The use case is ordinary in the first Consumer's domain: CVA / FVA / MVA each showing
Before / After / Diff — an upper tier carrying the grouping, with the flexibility of
`colspan`/`rowspan`. MudBlazor's table does this trivially **because it is a real `<table>` with
every column in the DOM**, which is also why it pays for every column on every frame; ExGrid
takes columns *out* of the DOM ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)), so a
group's leaves may not exist to carry a `colspan`. The model below has colspan/rowspan's
expressiveness without needing them.

## The model: rectangles above the leaf row, declared by member names

Leaf headers stay what they are — `Column.Header`, the bottom tier. Above them the Consumer
declares labelled rectangles by **naming their member columns**:

```csharp
HeaderGroups = [
    new HeaderGroup("CVA", columns: ["CvaBefore", "CvaAfter", "CvaDiff"]),     // tier 1
    new HeaderGroup("FVA", columns: ["FvaBefore", "FvaAfter", "FvaDiff"]),
    new HeaderGroup("MVA", columns: ["MvaBefore", "MvaAfter", "MvaDiff"]),
    new HeaderGroup("Valuation Adjustments", tier: 2,
        columns: ["CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter", "FvaDiff",
                  "MvaBefore", "MvaAfter", "MvaDiff"]),
];
```

- **Membership is a set; the left-to-right order is the flat `Columns` order**, never the
  declaration's. The member count is the colspan; **`tierSpan` is the rowspan** (default 1, for a
  mid-band rectangle occupying several tiers).
- **The commonest rowspan needs no declaration**: a column no tier covers has its leaf header
  stretch the full band — a `TradeId` beside the example above stands two tiers tall, label
  centred, with nothing written. (The one colspan/rowspan layout this cannot express directly —
  several columns *sharing* their bottom-most label — is a group plus empty leaf headers.)

**Refused by name at declaration**: a member name no column carries; members that are **not
adjacent** in the current order; overlapping rectangles; and a rectangle **straddling the pinned
boundary** — half sticky, half scrolling cannot be drawn honestly, and a dishonest header over
money is the failure this design refuses everywhere.

**The first draft declared groups by index (`firstColumn`, `columnCount`), and reordering is what
killed it.** Once a column can be dragged ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
an index declaration the Consumer forgot to update stays *formally valid* while labelling the
wrong columns — "CVA" painted over a stranger, undetectably. Names turn that same mistake into a
refusal the grid can make (**members no longer adjacent**), and they record the Consumer's actual
intent — "these three belong to CVA" — which no reorder changes.

## It groups header cells and the reorder gesture, and nothing else

The name is **Header Group**, not Column Group, because no *data* behaviour is grouped: no
collapse, no group-sort, no aggregate, no depth the grid understands. This is the line
[ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md) drew for rows — a declared
paint, not a hierarchy — and it keeps the Column a flat runtime object (`CONTEXT.md`), which
saved reordering, saved views and the whole width machinery from a tree.

The one behaviour it does carry is the reorder gesture, below — because once the rectangles
exist, the drag has to answer to them anyway, and the answer had better be deliberate.

## Reordering: what you grab is the unit that moves

[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md) settled the
gesture — the grid notifies, the Consumer pushes back a reordered `Columns`, and a drag never
crosses the pinned boundary. Header Groups add one rule in the same shape: **a group's edge is a
boundary that carries meaning, and a drag never crosses it.**

| Grabbed | Moves | Past an edge |
|---|---|---|
| a group's rectangle | the whole group, members in their current order | between peer units; clamps at the pinned boundary |
| a leaf header inside a group | that column, **within its group** | the drop indicator clamps at the group's edge |
| an uncovered leaf header | that column, among the top-level units | clamps likewise |
| a higher-tier rectangle | its whole span — the same rule, one level up | likewise |

- **A leaf drag does not escalate into a group move.** A ten-pixel overshoot must not turn "put
  Before at the end of CVA" into six columns changing places — the misfire logic of
  [ADR-0020](./0020-action-and-template-columns.md)'s "Enter never fires". Moving CVA past FVA is
  done by grabbing CVA's own rectangle, a visually distinct handle one tier up.
- **Membership never changes by gesture** — not dragged out of, not dropped into. Changing what
  belongs to CVA is a declaration change in the Consumer's code, the way pinning changes through
  its own explicit act (ADR-0011). One gesture, one fact.
- **Because membership is gesture-proof and membership is what the declaration states, the
  declaration survives every drag unchanged.** Swapping Before and After inside CVA, or CVA past
  FVA wholesale, needs no `HeaderGroups` edit at all; the adjacency refusal remains as the safety
  net for Consumer-side mistakes arriving by other routes.
- **A synchronised edit across "parallel" groups is the Consumer's**, deliberately: the grid does
  not know CVA, FVA and MVA are the same shape. A Consumer that wants Before/After swapped in all
  three applies the notified reorder to each and pushes once — the order is its View State.

## Geometry and painting are the existing arithmetic

- **The band is `(1 + tierCount) × HeaderHeight`** tall, and the cancellation
  [ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md) proved holds for the
  band's total: `first row = floor(scrollTop / RowHeight)` is untouched. The band's height comes
  off the scroll budget exactly as the one-row header's did (ADR-0013's ceiling check
  generalises).
- **Rectangles are absolutely positioned** from the numbers layout already has:
  `left = OffsetPxOf(first member)`, `width = OffsetPxOf(past-last member) − left`,
  `height = tierSpan × HeaderHeight`. A rectangle stands correctly whether or not its leaves are
  in the DOM — which is the whole answer to the collision ADR-0004 recorded as open. (Members
  resolve to indices once per push, off the render path.)
- **Cost is per rectangle**, the selection overlay's economy
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)): there are never more rectangles
  than groups declared, and never more groups than painted leaves worth labelling.
- **A group label never hashes and never feeds Auto width.** It spans several columns, so it
  sizes none of them; a label that does not fit gets the Text treatment — a visible ellipsis
  ([ADR-0016](./0016-column-width-and-overflow.md)) — and the columns stay the widths their own
  contents earned.
- **Alignment**: a Header Group's label is **centred** by default (a caption over its members),
  overridable by the closed enum recorded in ADR-0016's alignment refinement; vertically it is
  centred in its rectangle by arithmetic, and no vertical alignment option exists anywhere in
  the grid.

## What does not change

- **Selection and copy**: headers are not selectable, and a copy carries cell values only
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)) — tiered or not, no header row is
  ever pasted.
- **The keyboard**: Focus never enters the header ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)).
- **Rows and cells**: nothing below the band knows tiers exist.

## Consequences

- **`HeaderHeight` means the height of one tier**; the band is its multiple. `ViewportHeight`
  must exceed the band plus one row, and the refusal names the band.
- **Header Group** enters `CONTEXT.md`.
- **`ex-header-group`** joins the stable classes
  ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)); its
  ground defaults to the header background token.
- **ARIA** (`aria-colspan` on group cells) lands with the structural surface of
  [ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md).
- The bUnit layer can pin all of it — rectangle positions are strings computed from
  `ColumnGeometry`, no browser needed.
