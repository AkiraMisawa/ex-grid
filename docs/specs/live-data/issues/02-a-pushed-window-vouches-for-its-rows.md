# 02: A pushed Window vouches for its rows

Status: done — decided 2026-10-07; built by ticket 07

**The question:** should a Consumer that pushes its Window itself be able to tell the grid that the
Window holds no row twice, as a bundled source does — so that the grid stops walking every row of
every new Window? ExPivot is such a Consumer, and so is any ExGrid page that pushes a large result.

**Blocked by:** 01 for the numbers it adds; the question can be put now.

## Where it stands

- **The grid checks every new Window it cannot trust.** Without a Row Key it checks instances
  (`RequireDistinctRows`), and with one it checks keys (`RequireDistinctKeys`), in `TakeInWindow`
  (`src/ExGrid/Components/ExGrid.RowKey.cs:88-112`). The check is a hash set built afresh per Window,
  because the grid holds nothing between Windows (ADR-0001).
- **A bundled source vouches, and the grid then does not walk its Windows** (LV-10). It keeps a map of
  keys between changes and refuses a repeated key as each change comes, as ag-grid's node manager
  keeps `allNodesMap` and checks a node only when it is made
  ([`clientSideNodeManager.ts#L304-L320`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/clientSideRowModel/clientSideNodeManager.ts#L304-L320);
  ag-grid warns where ExGrid refuses).
- **A pushed Window has no one to vouch for it.** For ExPivot's report the walk costs 8.3 ms at
  401,001 rows on every redraw (D10). The engine cannot build a report that repeats a key: an axis
  node's children are kept by Item (`AxisNode.Child`, `src/ExPivot.Engine/PivotCube.cs`), as
  ag-grid's groups are kept by key in `childrenMapped`.
- **The vouch is already public on `IGridSource`** (`VouchesDistinctRows`), so a Consumer's own
  source can make the promise today. The code review of 2026-10-06 noted that ADR-0141 gives it to "a
  bundled source" only; whichever way this ticket goes, the ADR should say who may vouch.

## The options put to the user on 2026-10-06

- **(i) A pushed Window can be vouched for.** A grid parameter beside `RowKey` (for example a
  `bool`, meaningful only with a Row Key), which ExPivot sets for its report. It is a promise the
  Consumer keeps by construction or by its own refusal of repeated keys. If it is broken, the grid
  does not see it; Blazor's own exception for clashing keys still stops a repeat among the painted
  rows, and a repeat elsewhere is not painted until it is scrolled to. *Recommended then*: the
  engine's construction already guarantees it, and the check repeats that guarantee.
- **(ii) Keep the check** for every pushed Window. It costs what the check of instances cost before
  the Row Key (9.5 ms at 401,001 rows).

## If (i) is taken

- [ ] ADR-0141 amended (who may vouch, and what a false vouch costs), and LV-10 restated, with the
  user's decision and its date
- [ ] The parameter, its XML doc, and the grid taking it as it takes a source's vouch
- [ ] ExPivot vouches for its report, with a comment at the site naming the engine's guarantee
- [ ] Layer 2: a vouched pushed Window is not walked (count the key function's calls, as LV-10's
  tests do); an unvouched one is; ExPivot's report is vouched
- [ ] PV-43's measurement repeated: the grid's share of a redraw at 401,001 rows

## Comments

2026-10-07: Decided with the user (Q4). A pushed Window may be vouched for, through a parameter beside
`RowKey`. A vouched Window is not walked whole, and the painted rows' keys are checked on every render,
so a repeat on screen is refused by name. ExPivot vouches for its report. Recorded in
[ADR-0141](../../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)'s
section of 2026-10-07; LV-10 restated. Ticket 01's numbers: the check is 92–98% of a pushed 10⁶-row
update on CoreCLR (257 ms in the browser) and 3–4% of an ExPivot redraw.
