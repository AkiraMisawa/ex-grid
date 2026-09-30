# 08: Measure aggregation and re-layout

Status: ready-for-agent

**What to do:** PV-21 is observational and nothing has been measured. Record, in `metrics.json`,
how long `PivotEngine.Aggregate` takes over a large record set (100,000 and 1,000,000 records,
three row fields, one column field, two Value Fields) and how long `PivotEngine.Report` takes to
lay out a changed layout from a held cube. Never gate on it (AGENTS.md). Until it is measured,
nothing is claimed about ExPivot's speed — including whether Defer Layout Update is needed, which
ADR-0058 leaves for later on exactly that ground.

**Blocked by:** None

- [ ] A measurement mode, in `spikes/render-bench` or beside the engine's tests, that writes the numbers
- [ ] The numbers recorded, with the machine they came from

## Comments

2026-09-30: Measured during the grilling and recorded in ADR-0063 and ADR-0065: the first
engine took 687 ms on CoreCLR and 6.8 s in a published WebAssembly build to aggregate a million
records, and a columnar prototype 33 ms and 0.20 s. PV-21 now holds the targets the user set (Q52);
what remains is recording them for the rebuilt engine, which this ticket keeps.
