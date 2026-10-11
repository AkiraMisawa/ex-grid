# 15: The grid asks for rows before the Viewport runs out of them

Status: ready-for-agent

**What to build:** the Range Request Margin of
[ADR-0183](../../../adr/0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md).
- ExGrid takes a margin of M rows, 0 by default. At M, it raises its Range Request as soon as the
  rows on screen plus M either side, clamped into the total, are not all in the Window. The request
  still names the rows on screen.
- ExPivot sets its grid's margin to the smaller of 10 and its report source's read-ahead. It keeps a
  question in flight while that question's Window covers the rows on screen.

**Blocked by:** ticket 14 for ExPivot's half: the read-ahead the margin relies on. The grid's half can
start now.

- [ ] ExGrid: the margin parameter, 0 by default. At 0 nothing changes (FN-3). At M, the early request
      names the rows on screen and is not repeated until the Window moves (FN-3a)
- [ ] Layer 2 counting invocations: at M = 0 and M = 10, near both edges, at the total's ends, and
      with a Consumer that answers exactly the rows asked (it is asked again at the next move, which is
      why the default is 0)
- [ ] ExPivot: the margin from its source's read-ahead, and the covering question kept in flight; the
      lab branch's 21d7a94 shows the condition, and its test fails under the first build's
- [ ] Layer 3 over `/pivot-db`, or a page like the lab's `/lab-read-ahead`, behind the latency proxy:
      count the report source's questions while scrolling, never a time
- [ ] The Docs Site: ExGrid's page on Range Requests and the Window says what the margin does and why
      it is 0 by default, with an Example; ExPivot's Sources page says a remote source asks early
- [ ] `GridSource.Fetch` is left as ADR-0025 has it. Whether it takes the margin is measured first on
      its own path, as a ticket of its own

## Comments

2026-10-11: written with the user's decision, ADR-0183, from the second record's numbers
(`verification/2026-10-11-linux-remote-read-ahead-blocks/`). The lab branch's `RangeMarginRows` and
`ReadAheadTriggerRows` are the measured prototype, not the product's names.
