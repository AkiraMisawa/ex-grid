# 10: The readiness panel

Status: ready-for-agent

**What to build:** the panel at the top of `/cds` (spec, "The readiness panel").

- **Counts:** errors and warnings, computed on the server for the current snapshot and versions.
- **Each line** goes to its cell, and an error's line offers its fix.
- **The errors and warnings** are exactly the spec's lists.

**Blocked by:** 06, 07

- [ ] Layer 1: each error and each warning of the spec, produced from a stored state, and nothing
  else
- [ ] Layer 3: a line's link selects its cell on the right tab
