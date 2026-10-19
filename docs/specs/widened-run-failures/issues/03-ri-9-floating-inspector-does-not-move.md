# 03: RI-9's floating inspector is not moved by its title bar

Status: needs-triage
Roadmap: ExGrid — A floating Row Inspector dragged by its title bar always follows the drag.

**Seen:** `inspectors.spec.mjs:160`, "RI-9: floating: an inspector is dragged by its title bar, and
closes on its own", Server host, Chrome and Edge, shard 2/4, in both runs: the offset expected
`[120, 90]`, received `[0, 0]` (line 180).

- [ ] Find whether the drag is lost or read too early.
- [ ] Fix it in the product, or where the test reads.
- [ ] The test passes 20 runs under the widened conditions.
