# 02: A pushed Window vouches for its rows

Status: done locally — ADR-0150 implemented, tested and measured; feature push and PR await user confirmation

**The question:** should a Consumer that pushes its Window itself be able to tell the grid that the
Window holds no row twice, as a bundled source does — so that the grid stops walking every row of
every new Window? ExPivot is such a Consumer, and so is any ExGrid page that pushes a large result.

**Prerequisites complete:** the shared design review is accepted; ticket 01 is recorded.

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

## Implementation of (i), chosen by the user

- [x] ADR-0150 recorded, ADR-0141 amended (who may vouch, and what a false vouch costs), and LV-10 restated, with the
  user's decision and its date
- [x] The parameter, its XML doc, and the grid taking it as it takes a source's vouch
- [x] ExPivot vouches for its report, with a comment at the site naming the engine's guarantee
- [x] Layer 2: a vouched pushed Window is not walked (count the key function's calls, as LV-10's
  tests do); an unvouched one is; ExPivot's report is vouched
- [x] PV-43's measurement repeated: the grid's share of a redraw at 401,001 rows

## Comments

2026-10-06: Ticket 01's [measurements after PR #64](../../../../verification/2026-10-06-macos-live-update-costs/README.md)
put the key check at 0.191 / 1.909 / 9.045 ms for 11,001 / 101,001 / 401,001 report rows
(CoreCLR Release, the minimum of nine isolated observations). A vouch removes a whole-Window
pass, but Cube and Report construction dominate the large redraw. This is evidence for the
decision, not a decision to add the parameter.

The contract review found two details to put to the user: the check also refuses null rows and
null keys, so a vouch must account for those; and Blazor is not a fallback validator for a false
vouch. Repeated keys that never coexist in the painted slice need not trigger its duplicate-key
exception. The public source contract already lets a Consumer's own source vouch, as noted above.

2026-10-06, continuation Q1: the user accepted the recommendation. A pushed Window with a Row Key
may explicitly be vouched for; validation remains the default. The promise covers null rows, null
keys and duplicate keys, and a false promise has no guaranteed Blazor fallback. The decision is
[ADR-0150](../../../adr/0150-a-consumer-can-vouch-for-a-pushed-windows-rows.md); LV-10 and LV-19 record
its criteria. No implementation is claimed by this status.

2026-10-06, implementation: `VouchesDistinctRows` is implemented for pushed Windows, with
validation as the default and revalidation when the promise is withdrawn. ExPivot vouches for
its detached report Window. Layer 2 counts key calls, and the [follow-up measurements](../../../../verification/2026-10-06-macos-live-report-after/README.md)
record the real grid admission cost at all three report sizes. The grid now receives 64 report
rows at each size; the old full-report check is not repeated elsewhere in the client.
