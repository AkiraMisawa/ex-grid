# The sign-off's first run, at 0.2.0-rc.3

Date: 2026-11-07. The code is `bd2baee6`, `v0.2.0-rc.3`'s commit ([`commit.txt`](commit.txt)). This run is not a
sign-off: it found two things that needed a candidate of their own, a refusal's sentence left over the next report
and ExGrid's public surface left out of its shipped baseline, and `v0.2.0-rc.4` was cut for them and for what `main`
had gained (the user's choice of 2026-11-09). The sign-off is made against rc.4's commit in
[`../2026-11-09-linux-signoff/results.md`](../2026-11-09-linux-signoff/results.md), where every step ran again.

What is kept here is what that sign-off reads:

- [`read-through.md`](read-through.md): the read-through of all 703 rows at `c8b721f9`, whose verdicts the
  sign-off's table carries to rc.4's commit.
- [`measure-coreclr.log`](measure-coreclr.log) and [`measure/`](measure/README.md): Step 6's numbers at rc.3, which
  the sign-off compares with 0.1.0's, since rc.4's container read 20–60% slower and rc.4's code measured level with
  rc.3's in one container.

This run's layer 1 and 2 logs and `.trx` files, its static checks and its copy of the release run's layer-3 logs are
not kept: rc.4's record holds the same steps at the commit signed off. The release run of `v0.2.0-rc.3` is
[42402908462](https://github.com/akiramisawa/ex-grid/actions/runs/42402908462).
