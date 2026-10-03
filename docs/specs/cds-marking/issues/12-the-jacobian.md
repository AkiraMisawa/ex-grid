# 12: The Jacobian

Status: ready-for-agent

**What to build:** the Jacobian files of a Marking, and the inspector's button that computes one now
(spec, "The Jacobian").

- **The bumps:** +1 bp, one-sided, on each Point in a proxy's Read Set. The proxy is recomputed and
  the change divided by 1 bp. Recovery rates are not bumped.
- **The coordinates** are the Points read: the on-the-run row when read through `Cds[Otr]`, the
  series' row otherwise. An overridden Point is bumped at its overridden value.
- **The files:** `jacobian/d_<bespoke>_d_<reference>.csv` for each pair, identities with any `n/a`
  part left out. Each is a matrix: a `tenor` column and one column per Reference tenor, one row per
  proxy tenor.
- **The bump size** is written in the manifest.

**Blocked by:** 01, 11

- [ ] Layer 1: `a × reference + b` gives `a` on the diagonal; an average of n names gives 1/n; a
  `max` gives the winning branch; a level Override gives an empty row and a warning
- [ ] The cost of a Marking's bumps on the seed book, measured and recorded in the comments
