# 09: What holds the page near the combination cap (PV-21)

Status: ready-for-agent
Roadmap: ExPivot — A layout near the combination cap keeps the page responsive while its report is built.

**Why:** PV-21's longest task near the cap grew to 192–626 ms at 0.2.0, from 0.1.0's 141–243, while the answers came
sooner, and read the same at rc.3 and at 0.2.0's commit in one container (the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), Step 6). Which step
holds the page is not known.

**What:** a profile of PV-21's placements near the cap (`tests/browser/measure-pivot.spec.mjs`) that names the step
holding the main thread, then the fix, measured before and after (CLAUDE.md, "Measure before claiming anything about
performance"). Never gated.
