# 06: Measure loading a million records

Status: done

**What to do:** record DA-17 in `metrics.json`: a million records built from objects, read from a
CSV, and read from Arrow, on CoreCLR and in a published WebAssembly build. Record the machine. Never
gate on the numbers (AGENTS.md).

**Blocked by:** 02, 03, 05

## Comments

2026-10-01: Recorded in `verification/2026-10-01-linux-measure`, with the machine (a 4-vCPU
container; CoreCLR 10.0.12; a published WebAssembly build in Chromium 141). A million records:

| | CoreCLR | Browser |
|---|---|---|
| Built from objects | 426 ms | 3.8 s |
| Read from a CSV (89.7 MiB) | 955 ms | 14.6 s |
| Read from Arrow | 475 ms (77 ms without the unique id) | 3.7 s |

The CoreCLR numbers come from the explicit `MeasureTests` in `tests/ExGrid.Data.Tests` and
`tests/ExGrid.Data.Arrow.Tests`; the browser's from `measure-pivot.spec.mjs`.

