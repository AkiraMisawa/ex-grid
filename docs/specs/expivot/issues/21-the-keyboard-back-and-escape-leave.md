# 21: The keyboard given back, and the Escape that leaves

Status: ready-for-agent

**What to build:** [ADR-0069](../../../adr/0069-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md),
in ExGrid's core and in ExPivot's use of it.

- **ExGrid:** `ReturnKeyboardAsync()`, the hand-back the grid already makes for its own popovers,
  offered to the Consumer. And `OnLeave`, raised by an Escape on the root with nothing left to
  dismiss, in place of releasing the DOM focus. No new JavaScript.
- **ExPivot:** Show Details' dialog closes on its grid's `OnLeave`. However it closes, the report's
  grid takes the keyboard back. A details tab that closes while selected hands the keyboard to the
  tab selected next, which is the report's grid when that is the report's tab. Under both Chromes.

**Blocked by:** None

- [ ] DC-56, DC-57 (layer 2; layer 3 for the conditions only a browser can show)
- [ ] PV-39 (layer 2; layer 3 on `/pivot?details=dialog` and `/pivot`, under both Chromes)

## Comments
