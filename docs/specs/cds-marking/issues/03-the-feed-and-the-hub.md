# 03: The feed and the hub

Status: ready-for-agent

**What to build:** the fake provider in `samples/ExGrid.DemoApi`, and the hub that pushes it
(spec, "The feed").

- **Curves:**
  - single names by `ticker.ccy.seniority.doc_clause`, with reference entity names and recovery
    rates;
  - index families with several series, an on-the-run series per family, and a version attribute;
  - Official Proxies.
- **Points:** a par spread in bp and `observed`, quoted at realistic tenors per kind of curve.
  Official Proxy Points are observed.
- **Ticks:** deterministic from a seed. A share of the Points moves, the rest stays, and each
  snapshot has a sequence number.
- **The control panel's endpoints:** interval, pause, step, roll a family, and inject a fault (a
  curve's quotes gone, one Point gone, a new series without quotes).
- **`CdsHub`** pushes each snapshot. The pages hold the latest only.

**Blocked by:** None

- [ ] Layer 1: the same seed and steps give the same snapshots; a roll moves the on-the-run; each
  fault does what it says
- [ ] The control panel works with the timer paused, so a test never waits on time
- [ ] DemoApi's README (or its `Program.cs` comment) names the CDS area
