# 224: The Excel-comparison probes type at machine speed

Status: needs-triage
Roadmap: internal

**Found:** by the spec lint (#137, ADR-0220 §6). `sheet-vs-excel.spec.mjs` paces every press, type and
click by `PACE_MS`, a person's pace kept to match the by-hand comparison with Excel. Its own comment
gives the reason: on the Server host a key or click sent before the previous answer landed in the
wrong cell (`verification/2026-09-27-windows-excel/typing-probe-2.mjs`). The five places are allowed,
naming that suspicion.

**Since:** ticket 201 (a press straight after Enter lands on the cell pressed) and ticket 200 (keys
typed after the Name Box's Enter reach the grid) fixed the races of that family on 2026-10-16. The
pacing may now hide nothing.

- [ ] Set `PACE_MS` to 0 and run the file 20 times on each host. If it passes, remove the pacing; if
      not, each failure is a race of its own, ticketed and fixed in the product (ADR-0220 §2).
