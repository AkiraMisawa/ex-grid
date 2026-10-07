# 07: A pushed Window vouches, and the painted rows are checked

Status: ready-for-agent

**What to do:** build ticket 02's decision, recorded in [ADR-0141](../../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)'s section of 2026-10-07. A
Consumer that pushes its Window may vouch that it holds no Row Key twice. A vouched Window is not walked
whole. The painted rows' keys are checked on every render, vouched or not.

**Blocked by:** None

## What to build

- **A grid parameter beside `RowKey`**, meaningful only with a Row Key, by which a Consumer that pushes its
  Window vouches for it, with its XML doc. `TakeInWindow` (`src/ExGrid/Components/ExGrid.RowKey.cs`) then
  treats the Window as it treats a source's `VouchesDistinctRows`.
- **On every render, the grid checks the Row Keys of the rows it paints** before Blazor's own exception,
  and refuses a repeat by name, naming the key and its positions (LV-2's message).
- **ExPivot vouches for its report**, with a comment at the site naming the engine's guarantee
  (`AxisNode.Child` keeps children by Item).

## Done when

- [ ] LV-10 as restated passes, counting the key function's calls (§32)
- [ ] The grid's check of a pushed 10⁶-row vouched Window is gone from ticket 01's measurement. Record the
  pushed-Window figure again beside [ticket 01's](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md): 40 ms on CoreCLR, 257 ms in the browser
- [ ] Layer 1 and 2 green

## Comments
