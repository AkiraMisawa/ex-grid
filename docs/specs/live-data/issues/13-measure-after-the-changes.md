# 13: Measure after the changes

Status: ready-for-agent

**What to do:** repeat ticket 01's measurement once 04 to 12 are built, and record the before and after
beside [ticket 01's record](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md). Nothing in `src/` changes. Every number is observational and never
gates.

**Blocked by:** 04 to 12

## What to measure

- **PV-48:** ExPivot's redraw step by step on CoreCLR, and Apply to the painted frame in the browser, at
  about 10⁴, 10⁵ and 4×10⁵ report rows, with the collector's pauses. PV-21 beside it.
- **LV-23:** the out-of-memory loop at 101,001 rows, 20 redraws, the managed heap after a full collection.
- **LV-15's parts again**, and the pushed 10⁶-row Window's update after ticket 07.
- **Ticket 01's traps hold:** Release, `DOTNET_TieredCompilation=0`, the least of 7 to 15 runs, steps
  isolated, fixed result paths, idle machine checks.

## Done when

- [ ] A `verification/<date>-<platform>-live-update-costs-after/README.md` in ticket 01's shape, with the
  before and after side by side
- [ ] `metrics.json` carries PV-48 and LV-23

## Comments
