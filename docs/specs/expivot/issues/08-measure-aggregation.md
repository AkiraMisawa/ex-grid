# 08: Measure aggregation and re-layout

Status: done

**What to do:** PV-21 is observational and nothing has been measured. Record, in `metrics.json`,
how long `PivotEngine.Aggregate` takes over a large record set (100,000 and 1,000,000 records,
three row fields, one column field, two Value Fields) and how long `PivotEngine.Report` takes to
lay out a changed layout from a held cube. Never gate on it (AGENTS.md). Until it is measured,
nothing is claimed about ExPivot's speed — including whether Defer Layout Update is needed, which
ADR-0058 leaves for later on exactly that ground.

**Blocked by:** None

- [x] A measurement mode, in `spikes/render-bench` or beside the engine's tests, that writes the numbers
- [x] The numbers recorded, with the machine they came from

## Comments

2026-09-30: Measured during the grilling and recorded in ADR-0063 and ADR-0065: the first
engine took 687 ms on CoreCLR and 6.8 s in a published WebAssembly build to aggregate a million
records, and a columnar prototype 33 ms and 0.20 s. PV-21 now holds the targets the user set (Q52);
what remains is recording them for the rebuilt engine, which this ticket keeps.

2026-10-01: Recorded in `verification/2026-10-01-linux-measure` (`results.md`, `metrics.json`), on a
4-vCPU container, CoreCLR and a published WebAssembly build in Chromium 141, over a million
trades. The modes: the engine's explicit `Measurements` (CoreCLR) and `measure-pivot.spec.mjs`
(the browser, only with `EXGRID_MEASURE=pivot`). Against PV-21's targets, in the browser:

- every gesture laid out from the held answer — collapse, sort, a form — in 28–32 ms;
- a new question for `/pivot`'s 50-leaf report in 159–229 ms, its worst 333 ms;
- 1,000 changes on screen in 43 ms;
- a CSV of a million rows in 14.6 s, 3.7 times the 4 s target (955 ms on CoreCLR);
- the page blocked for up to about 0.2 s in a gesture's worst case, and for 1.9 s by a question
  near the 200,000-leaf cap, whose work after the source's last slice is one task.

A question costs 0.25 s at 1,350 leaves and 2.7 s at 197,151, so Defer Layout Update earns its
place for the large reports it was decided for (ADR-0060). Settling the cap is the user's
(ADR-0065).
