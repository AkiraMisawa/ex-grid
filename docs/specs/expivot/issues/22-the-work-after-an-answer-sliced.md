# 22: The work after an answer, sliced

Status: ready-for-agent

**What to build:** what ADR-0065 settled on 2026-10-01. Near the 200,000-leaf cap, the work after a
question's pass held a browser for 1.9 s in one task (`verification/2026-10-01-linux-measure`):

- the bundled source assembling its answer;
- ExPivot making the cube;
- ExPivot laying out the report.

Each yields to the browser at least every 30 ms, as the pass does. A gesture made meanwhile
supersedes the question, and the report on screen stays until the new one is complete. Nothing is
shown half-built (ADR-0066's "never half a batch").

**Blocked by:** None

- [ ] PV-40 in layer 2: the yields counted for a large answer, and a gesture that supersedes a
  question mid-build
- [ ] The longest task near the cap observed again with `measure-pivot.spec.mjs` on a published
  build, and recorded beside the first measurement

## Comments
