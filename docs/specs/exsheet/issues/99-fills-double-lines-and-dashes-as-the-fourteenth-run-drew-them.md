# 99: Fills, double lines and dashes as the fourteenth run drew them

Status: ready-for-agent

**What to build:** ticket 47's painting where the fourteenth Windows run (cases 16 to 18) answered
otherwise than ADR-0071 read. ADR-0071, "What the fourteenth Windows run settled", ticket 47's painting:

- **Where two filled cells meet, the gridline between them takes the lower cell's Fill.** Between two
  filled cells side by side, the right cell's. Where only one of the two is filled, its Fill covers the
  gridline as today.
- **A double line's middle pixel shows what the gridline beneath it would show:** the Fill that covers
  that gridline (by the rule above), or the ground where neither cell is filled.
- **The long dash is 9 device pixels at every scale.** Today it is 8 below 150% (`--ex-dash`). Thin
  dashed stays 3 on and 1 off, in device pixels.

**Blocked by:** None (can start immediately)

- [ ] **Two filled cells, one above the other:** the gridline's device pixel is the lower cell's Fill.
      Side by side: the right cell's. One filled cell still covers all four of its gridlines.
- [ ] **A double bottom over a filled cell:** dark, the Fill, dark. With no Fill on either cell: dark,
      the ground, dark, as before.
- [ ] **Medium dashed reads 9 on and 3 off** in device pixels at 100% and at a real 150%. Every style
      that uses the long dash takes it.
- [ ] **No row height changes, and nothing per cell reaches JavaScript** (ADR-0027 P1 to P9).
- [ ] **Layer 1/2 for the generated rules. Layer-3 pixels at 100% and 150%** (SH-46, DC-59). CI runs
      layer 3.

## Comments
