# 13: The inspector

Status: ready-for-agent

**What to build:** a row action on every tab that opens a `MudDialog` inspector, as the DemoHost's
inspectors do (spec, "The inspector").

- **The chart** (Blazor-ApexCharts): spread against tenor years, observed Points filled, Auto-quotes
  hollow, Overrides marked. It follows the latest snapshot.
- **Every curve:** identity, recovery, the last snapshot's number and time, its Overrides, and the
  last Marking's values with today's change.
- **A Bespoke Proxy also:** its Formulas, its Read Set with each Point's observed state, and a button
  that computes its Jacobian now (ticket 12).

**Blocked by:** 04, 07

- [ ] Layer 3: two inspectors open side by side follow the feed; no console message on either host
  (ApexCharts' script included)
