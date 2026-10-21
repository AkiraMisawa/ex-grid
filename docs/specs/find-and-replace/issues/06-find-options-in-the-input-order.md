# 06: Match case and Whole cell are taken in the Input Order

Status: ready-for-agent
Roadmap: ExGrid, ExSheet — Ticking Match case or Whole cell and then pressing Enter at machine speed searches with the ticked option, on every host.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md)'s Input Order and the spine's sixth rule. The find panel's Match case and Whole cell are applied as soon as their change arrives (`FindContext.MatchCaseChanged` and `WholeCellChanged` are synchronous `Action<bool>`), so a step numbered before the click, but still held behind an earlier input, runs with the option changed after it. ExSheet's Within was moved into the Input Order in ticket 04; these two were left because every Chrome hands them on.

- [ ] Both options are claimed clicks in every Chrome (Built-in, MudBlazor, ExSheet's panels) and are applied in their place in the Input Order
- [ ] `FindContext.MatchCaseChanged` and `WholeCellChanged` become `Func<bool, Task>`; a Change Note says what a custom Chrome changes
- [ ] Layer 2 and a layer-3 test that ticks and steps at machine speed with no wait between, both hosts

**Blocked by:** 04, 05.
