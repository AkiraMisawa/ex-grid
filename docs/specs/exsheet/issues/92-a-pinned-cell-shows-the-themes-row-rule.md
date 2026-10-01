# 92: A pinned cell shows the theme's row rule

Status: ready-for-agent

**What to build:** a defect ticket 88's agent read from the stylesheets but did not check in a browser. When a
theme turns the row rule on, as `ExGrid.MudBlazor` does, the core's Pinned Column cells hide it. Their ground is
opaque, and nothing paints the rule on them. A row's rule then stops at the pinned block.

Ticket 90 gave a lined cell the `--ex-row-rule` hook, which ExSheet sets on its own pinned cells. The core sets it
nowhere.

**Blocked by:** None (can start immediately)

- [ ] **Read it in a browser first.** Use `/features?chrome=mud` or any grid with a Pinned Column under
      `ExGrid.MudBlazor`, and say whether the rule really stops there.
- [ ] **If it does, a pinned cell paints the row rule as the scrollable cells show it.** The core does this by
      itself (for example by setting `--ex-row-rule` on `.ex-pinned`), so a bare ExGrid with the rule off paints
      as before.
- [ ] **ExSheet's own gridline on pinned cells (ticket 90) is unchanged.**
- [ ] **Layer 2 for the stylesheet. Layer-3 pixels under both Chromes.** CI runs them.
